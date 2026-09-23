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
    private static FieldInfo fApparelDef;
    private static FieldInfo fStyleDef;
    private static FieldInfo fWornGraphicPath;
    private static FieldInfo fColor;
    private static FieldInfo fAppearance;
    private static FieldInfo fLinkedDef;
    private static FieldInfo fDisplayCond;
    private static FieldInfo fConnectionMode;
    private static FieldInfo fManualLinks;
    private static FieldInfo fRequireAllManual;
    private static FieldInfo fManualGroups;
    private static FieldInfo fIsEnabled;

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
        var _itemType = LayeredApparelTypes.ItemType;
        fApparelDef = LayeredApparelSyncHelpers.GetField(_itemType, "_apparelDef");
        fStyleDef = LayeredApparelSyncHelpers.GetField(_itemType, "_styleDef");
        fWornGraphicPath = LayeredApparelSyncHelpers.GetField(_itemType, "_wornGraphicPath");
        fColor = LayeredApparelSyncHelpers.GetField(_itemType, "_color");
        fAppearance = LayeredApparelSyncHelpers.GetField(_itemType, "_appearance");
        fLinkedDef = LayeredApparelSyncHelpers.GetField(_itemType, "_linkedApparelDef");
        fDisplayCond = LayeredApparelSyncHelpers.GetField(_itemType, "_displayCondition");
        fConnectionMode = LayeredApparelSyncHelpers.GetField(_itemType, "_connectionMode");
        fManualLinks = LayeredApparelSyncHelpers.GetField(_itemType, "_manualLinks");
        fRequireAllManual = LayeredApparelSyncHelpers.GetField(_itemType, "_requireAllManualLinks");
        fManualGroups = LayeredApparelSyncHelpers.GetField(_itemType, "_manualGroups");
        fIsEnabled = LayeredApparelSyncHelpers.GetField(_itemType, "_isEnabled");

        pawnProp = AccessTools.Property(_itemType, "Pawn");
        isAutoDisabledProp = AccessTools.Property(_itemType, "IsAutoDisabled");
        offsetFrontProp = AccessTools.Property(_itemType, "OffsetFront");
        offsetBackProp = AccessTools.Property(_itemType, "OffsetBack");
        offsetSideProp = AccessTools.Property(_itemType, "OffsetSide");
    }

    private static void RegisterItemWorker()
    {
        try
        {
            MP.RegisterSyncWorker<object>(SyncLayeredApparelItem, LayeredApparelTypes.ItemType, shouldConstruct: true);
        }
        catch (Exception _exception)
        {
            Log.Error($"{LogPrefix} Register item worker failed: {_exception}");
        }
    }

    private static void RegisterItemMethods()
    {
        var _itemType = LayeredApparelTypes.ItemType;

        // Direct toggles/sliders on attached items (ITab.DrawItemCardDraggable,
        // ITab.SetRenderOffset). ApparelDef/StyleDef/Color/WornGraphicPath setters
        // are deliberately NOT synced: they only run during construction of detached
        // items (AddApparelToList, PresetItem.ToItem); syncing them would spam
        // throwaway syncs. Color changes on attached items go through ApplyAppearance.
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredPropertySetter(_itemType, "IsEnabled"),
            "Item.set_IsEnabled");
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredPropertySetter(_itemType, "OffsetFront"),
            "Item.set_OffsetFront");
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredPropertySetter(_itemType, "OffsetBack"),
            "Item.set_OffsetBack");
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredPropertySetter(_itemType, "OffsetSide"),
            "Item.set_OffsetSide");

        // Linking UI (ITab.CompleteLinkingCosmeticToEquipped sets LinkedApparel on
        // the attached _linkingCosmetic). Construction uses RestoreConnections, not
        // these setters, so no spurious syncs. NoCancel: null means "unlink".
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredPropertySetter(_itemType, "LinkedApparel"),
            "Item.set_LinkedApparel");
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredPropertySetter(_itemType, "LinkedApparelDef"),
            "Item.set_LinkedApparelDef");

        // Appearance picker Apply (Dialog_ColorPicker.Apply -> _item.ApplyAppearance).
        // Draft edits mutate the detached _draft and are correctly unsynced.
        // NoCancel: a null appearance (reset to default) is legitimate.
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredMethod(_itemType, "ApplyAppearance"),
            "Item.ApplyAppearance");

        // Condition editor commit (ITab.ApplyEditedCondition -> _conditionItem.ApplyDisplayCondition).
        // Intermediate edits mutate a detached clone; only the commit syncs.
        // NoCancel: linkedDef is routinely null here (every Match any/all, layer,
        // coverage and armor button passes null). Cancelling on null would silently
        // drop all of those edits in MP, making the whole section look dead.
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredMethod(_itemType, "ApplyDisplayCondition"),
            "Item.ApplyDisplayCondition");

        // Manual link groups editor (ITab draws groups, commits via SetManualGroups).
        // NoCancel: empty/null group lists are legitimate (clearing links).
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredMethod(_itemType, "SetManualGroups"),
            "Item.SetManualGroups");
        LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredMethod(_itemType, "SetManualLinks"),
            "Item.SetManualLinks");

        // Connection mode switch (ITab condition/mode UI on attached items).
        // Nested calls from SetManual*/RestoreConnections re-run locally inside
        // sync execution without re-syncing (MP suppresses nested syncs), so this is safe.
        // Note: the synced call applies a tick later, so the ITab's same-frame read of
        // DisplayCondition is covered by the DrawConditionFilters null-guard in
        // LayeredApparelItabGuard.
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredMethod(_itemType, "SetConnectionMode"),
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

    private static bool TryGetLocation(object _item, out Pawn _pawn, out int _listType, out int _index)
    {
        _pawn = null;
        _listType = 0;
        _index = -1;
        try
        {
            _pawn = pawnProp?.GetValue(_item) as Pawn;
            if (_pawn == null) return false;
            var _layeredComp = LayeredApparelSyncHelpers.GetComp(_pawn);
            if (_layeredComp == null) return false;
            for (var _listTypeIdx = 0; _listTypeIdx < 4; _listTypeIdx++)
            {
                var _itemList = LayeredApparelSyncHelpers.GetItemsList(_layeredComp, _listTypeIdx);
                if (_itemList == null) continue;
                for (var _listIdx = 0; _listIdx < _itemList.Count; _listIdx++)
                    if (ReferenceEquals(_itemList[_listIdx], _item))
                    {
                        _listType = _listTypeIdx;
                        _index = _listIdx;
                        return true;
                    }
            }
        }
        catch (Exception _exception)
        {
            Log.Error($"{LogPrefix} TryGetLocation failed: {_exception}");
        }

        return false;
    }

    private static object ResolveItem(Pawn _pawn, int _listType, int _index)
    {
        try
        {
            var _layeredComp = LayeredApparelSyncHelpers.GetComp(_pawn);
            var _itemList = _layeredComp == null
                ? null
                : LayeredApparelSyncHelpers.GetItemsList(_layeredComp, _listType);
            if (_itemList != null && _index >= 0 && _index < _itemList.Count)
                return _itemList[_index];
            Log.Error(
                $"{LogPrefix} ResolveItem failed: pawn={_pawn?.LabelShort} list={_listType} index={_index} count={_itemList?.Count}");
        }
        catch (Exception _exception)
        {
            Log.Error($"{LogPrefix} ResolveItem failed: {_exception}");
        }

        return null;
    }

    private static void SyncLayeredApparelItem(SyncWorker _sync, ref object _object)
    {
        if (_sync.isWriting)
        {
            if (_object == null)
            {
                _sync.Write(false);
                return;
            }

            _sync.Write(true); // not null
            if (TryGetLocation(_object, out var _pawn, out var _listTypeIdx, out var _itemIdx))
            {
                _sync.Write(true); // by reference
                _sync.Write(_pawn);
                _sync.Write(_listTypeIdx);
                _sync.Write(_itemIdx);
            }
            else
            {
                _sync.Write(false); // by value
                WriteItemData(_sync, _object);
            }
        }
        else
        {
            if (!_sync.Read<bool>())
            {
                _object = null;
                return;
            }

            if (_sync.Read<bool>())
            {
                var _pawn = _sync.Read<Pawn>();
                var _listTypeIdx = _sync.Read<int>();
                var _itemIdx = _sync.Read<int>();
                _object = ResolveItem(_pawn, _listTypeIdx, _itemIdx);
            }
            else
            {
                _object = ReadItemData(_sync);
            }
        }
    }

    private static void WriteItemData(SyncWorker _sync, object _item)
    {
        // Mirrors LayeredApparelItem.ExposeData (minus transient _apparelCached and Pawn,
        // which AddItem reassigns on each client). Order must exactly match ReadItemData.
        _sync.Write((Def)fApparelDef.GetValue(_item));
        _sync.Write((Def)fStyleDef.GetValue(_item));
        _sync.Write((string)fWornGraphicPath.GetValue(_item));
        _sync.Write((Color)fColor.GetValue(_item));
        LayeredApparelNestedSync.WriteAppearanceInline(_sync, fAppearance.GetValue(_item));
        _sync.Write((Def)fLinkedDef.GetValue(_item));
        LayeredApparelNestedSync.WriteConditionInline(_sync, fDisplayCond.GetValue(_item));
        _sync.Write((int)fConnectionMode.GetValue(_item));
        LayeredApparelSyncHelpers.WriteStringList(_sync, fManualLinks.GetValue(_item) as List<string>);
        _sync.Write((bool)fRequireAllManual.GetValue(_item));
        LayeredApparelNestedSync.WriteLinkGroupList(_sync, fManualGroups.GetValue(_item) as IList);
        _sync.Write((bool)fIsEnabled.GetValue(_item));
        _sync.Write((bool)isAutoDisabledProp.GetValue(_item));
        _sync.Write((float)offsetFrontProp.GetValue(_item));
        _sync.Write((float)offsetBackProp.GetValue(_item));
        _sync.Write((float)offsetSideProp.GetValue(_item));
    }

    private static object ReadItemData(SyncWorker _sync)
    {
        var _instance = Activator.CreateInstance(LayeredApparelTypes.ItemType);
        fApparelDef.SetValue(_instance, _sync.Read<Def>());
        fStyleDef.SetValue(_instance, _sync.Read<Def>());
        fWornGraphicPath.SetValue(_instance, _sync.Read<string>());
        fColor.SetValue(_instance, _sync.Read<Color>());
        fAppearance.SetValue(_instance, LayeredApparelNestedSync.ReadAppearanceInline(_sync));
        fLinkedDef.SetValue(_instance, _sync.Read<Def>());
        fDisplayCond.SetValue(_instance, LayeredApparelNestedSync.ReadConditionInline(_sync));
        fConnectionMode.SetValue(_instance, _sync.Read<int>());
        fManualLinks.SetValue(_instance, LayeredApparelSyncHelpers.ReadStringList(_sync));
        fRequireAllManual.SetValue(_instance, _sync.Read<bool>());
        // Convert IList (List<ApparelLinkGroup>) back via reflection SetValue; the
        // mod's ManualGroups getter lazily rebuilds from _manualLinks if _manualGroups
        // is null, but we preserve the exact list when present.
        fManualGroups.SetValue(_instance, LayeredApparelNestedSync.ReadLinkGroupList(_sync));
        fIsEnabled.SetValue(_instance, _sync.Read<bool>());
        isAutoDisabledProp.SetValue(_instance, _sync.Read<bool>());
        offsetFrontProp.SetValue(_instance, _sync.Read<float>());
        offsetBackProp.SetValue(_instance, _sync.Read<float>());
        offsetSideProp.SetValue(_instance, _sync.Read<float>());
        // Pawn left null; Comp.AddItem assigns it on each client. _apparelCached
        // stays null and rebuilds lazily via GetCachedApparel.
        return _instance;
    }
}