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
        var compType = LayeredApparelTypes.CompType;

        // AddItem overloads: AddItem(CosmeticListType, LayeredApparelItem) [internal],
        // AddItem(LayeredApparelItem), AddDraftedItem, AddVacuumItem, AddPilotItem
        foreach (var methodInfo in AccessTools.GetDeclaredMethods(compType))
            if (methodInfo.Name is "AddItem" or "AddDraftedItem" or "AddVacuumItem" or "AddPilotItem")
                LayeredApparelSyncHelpers.TryRegister(methodInfo, $"Comp.{methodInfo}");

        // AddItems(CosmeticListType, IEnumerable<LayeredApparelItem>, bool)
        // Bulk imports (wardrobe multi-select, preset apply via AddItems). If MP can't
        // handle IEnumerable, registration logs an error but single-item Adds still sync.
        var addItemsMethod = AccessTools.DeclaredMethod(compType, "AddItems");
        if (addItemsMethod != null) LayeredApparelSyncHelpers.TryRegisterNoCancel(addItemsMethod, "Comp.AddItems");
        else Log.Warning($"{LogPrefix} Comp.AddItems not found");

        // Clear variants
        foreach (var methodName in new[] { "ClearItems", "ClearDraftedItems", "ClearVacuumItems", "ClearPilotItems" })
        foreach (var methodInfo in AccessTools.GetDeclaredMethods(compType)
                     .Where(candidateInfo => candidateInfo.Name == methodName))
            LayeredApparelSyncHelpers.TryRegister(methodInfo, $"Comp.{methodName}");

        // Remove variants
        foreach (var methodInfo in AccessTools.GetDeclaredMethods(compType)
                     .Where(candidateInfo => candidateInfo.Name is "RemoveItem" or "RemoveItemAt"))
            LayeredApparelSyncHelpers.TryRegister(methodInfo, $"Comp.{methodInfo}");

        // Move variants
        foreach (var methodInfo in AccessTools.GetDeclaredMethods(compType)
                     .Where(candidateInfo => candidateInfo.Name is "MoveItem" or "MoveItemUp" or "MoveItemDown"))
            LayeredApparelSyncHelpers.TryRegisterNoCancel(methodInfo, $"Comp.{methodInfo}");

        // Hidden toggle (takes Apparel Thing, syncable by MP)
        LayeredApparelSyncHelpers.TryRegister(AccessTools.DeclaredMethod(compType, "ToggleHidden"),
            "Comp.ToggleHidden");

        // Enabled toggles for the four cosmetic lists. These are game state (which list
        // renders) and must sync. IsInExosuit is deliberately skipped: it's derived
        // from worn apparel via EmergencyStateSync/NotifyApparelChanged on all clients.
        foreach (var propertyName in new[] { "GlobalEnabled", "DraftedEnabled", "VacuumEnabled", "PilotEnabled" })
            LayeredApparelSyncHelpers.TryRegisterNoCancel(AccessTools.DeclaredPropertySetter(compType, propertyName),
                $"Comp.set_{propertyName}");

        // NOTE: TryApplyPreset is NOT synced directly. Its signature contains
        // Action<> / nullable / collection params that MP can't serialize, and its
        // data comes from per-client preset files. Preset application funnels into
        // AddItems/ClearItems above when applied through the ITab, and file-based
        // Dialog_PresetLoad.ApplyPreset is out of scope for v1.
    }
}