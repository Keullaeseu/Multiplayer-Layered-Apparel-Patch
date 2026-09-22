using HarmonyLib;
using Verse;

namespace MultiplayerLayeredApparelPatch.Source.Mods;

/// <summary>
///     Syncs CompLayeredApparel list mutations (add/remove/move/clear/toggle).
///     Cache and render-only methods (InvalidateRenderCache, NotifyApparelChanged,
///     UpdateLinkedStates, ...) are deliberately NOT synced; they re-run locally
///     on every client as a side effect of the synced calls below.
/// </summary>
internal static class LayeredApparelCompSync
{
    private const string LogPrefix = "[Multiplayer Layered Apparel Comp Sync Patch]";

    internal static void Patch()
    {
        var _compType = LayeredApparelTypes.CompType;

        // AddItem overloads: AddItem(CosmeticListType, LayeredApparelItem) [internal],
        // AddItem(LayeredApparelItem), AddDraftedItem, AddVacuumItem, AddPilotItem
        foreach (var _methodInfo in AccessTools.GetDeclaredMethods(_compType))
            if (_methodInfo.Name is "AddItem" or "AddDraftedItem" or "AddVacuumItem" or "AddPilotItem")
                LayeredApparelSyncHelpers.TryRegister(_methodInfo, $"Comp.{_methodInfo}");

        // AddItems(CosmeticListType, IEnumerable<LayeredApparelItem>, bool)
        // Bulk imports (wardrobe multi-select, preset apply via AddItems). If MP can't
        // handle IEnumerable, registration logs an error but single-item Adds still sync.
        var _addItemsMethod = AccessTools.DeclaredMethod(_compType, "AddItems");
        if (_addItemsMethod != null) LayeredApparelSyncHelpers.TryRegister(_addItemsMethod, "Comp.AddItems");
        else Log.Warning($"{LogPrefix} Comp.AddItems not found");

        // Clear variants
        foreach (var _methodName in new[] { "ClearItems", "ClearDraftedItems", "ClearVacuumItems", "ClearPilotItems" })
        foreach (var _methodInfo in AccessTools.GetDeclaredMethods(_compType)
                     .Where(_candidateInfo => _candidateInfo.Name == _methodName))
            LayeredApparelSyncHelpers.TryRegister(_methodInfo, $"Comp.{_methodName}");

        // Remove variants
        foreach (var _methodInfo in AccessTools.GetDeclaredMethods(_compType)
                     .Where(_candidateInfo => _candidateInfo.Name is "RemoveItem" or "RemoveItemAt"))
            LayeredApparelSyncHelpers.TryRegister(_methodInfo, $"Comp.{_methodInfo}");

        // Move variants
        foreach (var _methodInfo in AccessTools.GetDeclaredMethods(_compType)
                     .Where(_candidateInfo => _candidateInfo.Name is "MoveItem" or "MoveItemUp" or "MoveItemDown"))
            LayeredApparelSyncHelpers.TryRegisterNoCancel(_methodInfo, $"Comp.{_methodInfo}");

        // Hidden toggle (takes Apparel Thing, syncable by MP)
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredMethod(_compType, "ToggleHidden"),
            "Comp.ToggleHidden");

        // Enabled toggles for the four cosmetic lists. These are game state (which list
        // renders) and must sync. IsInExosuit is deliberately skipped: it's derived
        // from worn apparel via EmergencyStateSync/NotifyApparelChanged on all clients.
        foreach (var _propertyName in new[] { "GlobalEnabled", "DraftedEnabled", "VacuumEnabled", "PilotEnabled" })
            LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredPropertySetter(_compType, _propertyName),
                $"Comp.set_{_propertyName}");

        // NOTE: TryApplyPreset is NOT synced directly. Its signature contains
        // Action<> / nullable / collection params that MP can't serialize, and its
        // data comes from per-client preset files. Preset application funnels into
        // AddItems/ClearItems above when applied through the ITab, and file-based
        // Dialog_PresetLoad.ApplyPreset is out of scope for v1.
    }
}