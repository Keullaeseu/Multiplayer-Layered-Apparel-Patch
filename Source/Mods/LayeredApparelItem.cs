using System.Collections;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace MultiplayerLayeredApparelPatch.Source.Mods;

/// <summary>
///     Syncs LayeredApparelItem mutations (the per-slot cosmetic entry).
///     ITab_LayeredApparel itself is per-client UI state (selection, scroll,
///     preview) and is intentionally NOT synced; only the game-state mutations
///     it performs on items/comps are synced (see LayeredApparelPatch).
/// </summary>
public static class LayeredApparelItemPatch
{
    private const string LogPrefix = "[Multiplayer Layered Apparel Item Patch]";

    // Item fields (private in the mod)
    private static FieldInfo apparelDefField;
    private static FieldInfo styleDefField;
    private static FieldInfo wornGraphicPathField;
    private static FieldInfo colorField;
    private static FieldInfo appearanceField;
    private static FieldInfo linkedDefField;
    private static FieldInfo displayCondField;
    private static FieldInfo connectionModeField;
    private static FieldInfo manualLinksField;
    private static FieldInfo requireAllManualField;
    private static FieldInfo manualGroupsField;
    private static FieldInfo isEnabledField;

    private static PropertyInfo pawnProp;
    private static PropertyInfo isAutoDisabledProp;
    private static PropertyInfo offsetFrontProp;
    private static PropertyInfo offsetBackProp;
    private static PropertyInfo offsetSideProp;

    public static void Patch()
    {
        if (LayeredApparelTypes.ItemType == null)
        {
            Log.Error($"{LogPrefix} ItemType missing, aborting item patch.");
            return;
        }

        CacheItemFields();
        RegisterItemWorker();
        RegisterItemMethods();
    }

    private static void CacheItemFields()
    {
        var itemType = LayeredApparelTypes.ItemType;
        apparelDefField = LayeredApparelSyncHelpers.GetField(itemType, "_apparelDef");
        styleDefField = LayeredApparelSyncHelpers.GetField(itemType, "_styleDef");
        wornGraphicPathField = LayeredApparelSyncHelpers.GetField(itemType, "_wornGraphicPath");
        colorField = LayeredApparelSyncHelpers.GetField(itemType, "_color");
        appearanceField = LayeredApparelSyncHelpers.GetField(itemType, "_appearance");
        linkedDefField = LayeredApparelSyncHelpers.GetField(itemType, "_linkedApparelDef");
        displayCondField = LayeredApparelSyncHelpers.GetField(itemType, "_displayCondition");
        connectionModeField = LayeredApparelSyncHelpers.GetField(itemType, "_connectionMode");
        manualLinksField = LayeredApparelSyncHelpers.GetField(itemType, "_manualLinks");
        requireAllManualField = LayeredApparelSyncHelpers.GetField(itemType, "_requireAllManualLinks");
        manualGroupsField = LayeredApparelSyncHelpers.GetField(itemType, "_manualGroups");
        isEnabledField = LayeredApparelSyncHelpers.GetField(itemType, "_isEnabled");

        pawnProp = AccessTools.Property(itemType, "Pawn");
        isAutoDisabledProp = AccessTools.Property(itemType, "IsAutoDisabled");
        offsetFrontProp = AccessTools.Property(itemType, "OffsetFront");
        offsetBackProp = AccessTools.Property(itemType, "OffsetBack");
        offsetSideProp = AccessTools.Property(itemType, "OffsetSide");
    }

    private static void RegisterItemWorker()
    {
        try
        {
            MP.RegisterSyncWorker<object>(SyncLayeredApparelItem, LayeredApparelTypes.ItemType, shouldConstruct: true);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Register item worker failed: {exception}");
        }
    }

    private static void RegisterItemMethods()
    {
        var itemType = LayeredApparelTypes.ItemType;

        // Direct toggles/sliders on attached items (ITab.DrawItemCardDraggable,
        // ITab.SetRenderOffset). ApparelDef/StyleDef/Color/WornGraphicPath setters
        // are deliberately NOT synced: they only run during construction of detached
        // items (AddApparelToList, PresetItem.ToItem); syncing them would spam
        // throwaway syncs. Color changes on attached items go through ApplyAppearance.
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredPropertySetter(itemType, "IsEnabled"),
            "Item.set_IsEnabled");
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredPropertySetter(itemType, "OffsetFront"),
            "Item.set_OffsetFront");
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredPropertySetter(itemType, "OffsetBack"),
            "Item.set_OffsetBack");
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredPropertySetter(itemType, "OffsetSide"),
            "Item.set_OffsetSide");

        // Linking UI (ITab.CompleteLinkingCosmeticToEquipped sets LinkedApparel on
        // the attached _linkingCosmetic). Construction uses RestoreConnections, not
        // these setters, so no spurious syncs. NoCancel: null means "unlink".
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredPropertySetter(itemType, "LinkedApparel"),
            "Item.set_LinkedApparel");
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredPropertySetter(itemType, "LinkedApparelDef"),
            "Item.set_LinkedApparelDef");

        // Appearance picker Apply (Dialog_ColorPicker.Apply -> _item.ApplyAppearance).
        // Draft edits mutate the detached _draft and are correctly unsynced.
        // NoCancel: a null appearance (reset to default) is legitimate.
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredMethod(itemType, "ApplyAppearance"),
            "Item.ApplyAppearance");

        // Condition editor commit (ITab.ApplyEditedCondition -> _conditionItem.ApplyDisplayCondition).
        // Intermediate edits mutate a detached clone; only the commit syncs.
        // NoCancel: linkedDef is routinely null here (every Match any/all, layer,
        // coverage and armor button passes null). Cancelling on null would silently
        // drop all of those edits in MP, making the whole section look dead.
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredMethod(itemType, "ApplyDisplayCondition"),
            "Item.ApplyDisplayCondition");

        // Manual link groups editor (ITab draws groups, commits via SetManualGroups).
        // NoCancel: empty/null group lists are legitimate (clearing links).
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredMethod(itemType, "SetManualGroups"),
            "Item.SetManualGroups");
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredMethod(itemType, "SetManualLinks"),
            "Item.SetManualLinks");

        // Connection mode switch (ITab condition/mode UI on attached items).
        // Nested calls from SetManual*/RestoreConnections re-run locally inside
        // sync execution without re-syncing (MP suppresses nested syncs), so this is safe.
        // Note: the synced call applies a tick later, so the ITab's same-frame read of
        // DisplayCondition is covered by the DrawConditionFilters null-guard in
        // LayeredApparelItabGuard.
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredMethod(itemType, "SetConnectionMode"),
            "Item.SetConnectionMode");

        // NOTE: RestoreConnections, Duplicate, UpdateLinkedState, InvalidateCache,
        // GetCachedApparel are NOT synced. RestoreConnections/Duplicate run during
        // construction of detached items; UpdateLinkedState/InvalidateCache are
        // deterministic side effects re-run locally via NotifyApparelChanged.
        //
        // NOTE: File preset load (Dialog_PresetLoad) and bulk management
        // (Dialog_ApparelManagement) are out of scope for v1. Manual wardrobe edits,
        // which funnel through Comp.AddItem/AddItems (synced in LayeredApparelCompSync),
        // cover the main MP use case. Preset files are per-client disk state.
    }

    // ------------------------------------------------------------------
    // Hybrid sync worker for LayeredApparelItem.
    // Attached items (in a pawn's comp list) sync by reference
    //   (Pawn + listType + index) so mutations hit the real entry on all clients.
    // Detached items (new entries not yet added) sync by value (full outfit data)
    //   so Comp.AddItem/AddItems can transfer new entries.
    // ------------------------------------------------------------------

    private static bool TryGetLocation(object item, out Pawn pawn, out int listType, out int index)
    {
        pawn = null;
        listType = 0;
        index = -1;
        try
        {
            pawn = pawnProp?.GetValue(item) as Pawn;
            if (pawn == null) return false;
            var layeredComp = LayeredApparelSyncHelpers.GetComp(pawn);
            if (layeredComp == null) return false;
            for (var listTypeIdx = 0; listTypeIdx < 4; listTypeIdx++)
            {
                var itemList = LayeredApparelSyncHelpers.GetItemsList(layeredComp, listTypeIdx);
                if (itemList == null) continue;
                for (var listIdx = 0; listIdx < itemList.Count; listIdx++)
                    if (ReferenceEquals(itemList[listIdx], item))
                    {
                        listType = listTypeIdx;
                        index = listIdx;
                        return true;
                    }
            }
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} TryGetLocation failed: {exception}");
        }

        return false;
    }

    private static object ResolveItem(Pawn pawn, int listType, int index)
    {
        try
        {
            var layeredComp = LayeredApparelSyncHelpers.GetComp(pawn);
            var itemList = layeredComp == null
                ? null
                : LayeredApparelSyncHelpers.GetItemsList(layeredComp, listType);
            if (itemList != null && index >= 0 && index < itemList.Count)
                return itemList[index];
            Log.Error(
                $"{LogPrefix} ResolveItem failed: pawn={pawn?.LabelShort} list={listType} index={index} count={itemList?.Count}");
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} ResolveItem failed: {exception}");
        }

        return null;
    }

    private static void SyncLayeredApparelItem(SyncWorker sync, ref object obj)
    {
        if (sync.isWriting)
        {
            if (obj == null)
            {
                sync.Write(false);
                return;
            }

            sync.Write(true); // not null
            if (TryGetLocation(obj, out var pawn, out var listTypeIdx, out var itemIdx))
            {
                sync.Write(true); // by reference
                sync.Write(pawn);
                sync.Write(listTypeIdx);
                sync.Write(itemIdx);
            }
            else
            {
                sync.Write(false); // by value
                WriteItemData(sync, obj);
            }
        }
        else
        {
            if (!sync.Read<bool>())
            {
                obj = null;
                return;
            }

            if (sync.Read<bool>())
            {
                var pawn = sync.Read<Pawn>();
                var listTypeIdx = sync.Read<int>();
                var itemIdx = sync.Read<int>();
                obj = ResolveItem(pawn, listTypeIdx, itemIdx);
            }
            else
            {
                obj = ReadItemData(sync);
            }
        }
    }

    private static void WriteItemData(SyncWorker sync, object item)
    {
        // Mirrors LayeredApparelItem.ExposeData (minus transient _apparelCached and Pawn,
        // which AddItem reassigns on each client). Order must exactly match ReadItemData.
        sync.Write((Def)apparelDefField.GetValue(item));
        sync.Write((Def)styleDefField.GetValue(item));
        sync.Write((string)wornGraphicPathField.GetValue(item));
        sync.Write((Color)colorField.GetValue(item));
        LayeredApparelNestedSync.WriteAppearanceInline(sync, appearanceField.GetValue(item));
        sync.Write((Def)linkedDefField.GetValue(item));
        LayeredApparelNestedSync.WriteConditionInline(sync, displayCondField.GetValue(item));
        sync.Write((int)connectionModeField.GetValue(item));
        LayeredApparelSyncHelpers.WriteStringList(sync, manualLinksField.GetValue(item) as List<string>);
        sync.Write((bool)requireAllManualField.GetValue(item));
        LayeredApparelNestedSync.WriteLinkGroupList(sync, manualGroupsField.GetValue(item) as IList);
        sync.Write((bool)isEnabledField.GetValue(item));
        sync.Write((bool)isAutoDisabledProp.GetValue(item));
        sync.Write((float)offsetFrontProp.GetValue(item));
        sync.Write((float)offsetBackProp.GetValue(item));
        sync.Write((float)offsetSideProp.GetValue(item));
    }

    private static object ReadItemData(SyncWorker sync)
    {
        var instance = Activator.CreateInstance(LayeredApparelTypes.ItemType);
        apparelDefField.SetValue(instance, sync.Read<Def>());
        styleDefField.SetValue(instance, sync.Read<Def>());
        wornGraphicPathField.SetValue(instance, sync.Read<string>());
        colorField.SetValue(instance, sync.Read<Color>());
        appearanceField.SetValue(instance, LayeredApparelNestedSync.ReadAppearanceInline(sync));
        linkedDefField.SetValue(instance, sync.Read<Def>());
        displayCondField.SetValue(instance, LayeredApparelNestedSync.ReadConditionInline(sync));
        connectionModeField.SetValue(instance, sync.Read<int>());
        manualLinksField.SetValue(instance, LayeredApparelSyncHelpers.ReadStringList(sync));
        requireAllManualField.SetValue(instance, sync.Read<bool>());
        // Convert IList (List<ApparelLinkGroup>) back via reflection SetValue; the
        // mod's ManualGroups getter lazily rebuilds from _manualLinks if _manualGroups
        // is null, but we preserve the exact list when present.
        manualGroupsField.SetValue(instance, LayeredApparelNestedSync.ReadLinkGroupList(sync));
        isEnabledField.SetValue(instance, sync.Read<bool>());
        isAutoDisabledProp.SetValue(instance, sync.Read<bool>());
        offsetFrontProp.SetValue(instance, sync.Read<float>());
        offsetBackProp.SetValue(instance, sync.Read<float>());
        offsetSideProp.SetValue(instance, sync.Read<float>());
        // Pawn left null; Comp.AddItem assigns it on each client. _apparelCached
        // stays null and rebuilds lazily via GetCachedApparel.
        return instance;
    }
}