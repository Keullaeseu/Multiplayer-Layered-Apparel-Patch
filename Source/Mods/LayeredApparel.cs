using Multiplayer.Compat;
using Verse;

namespace MultiplayerLayeredApparelPatch.Source.Mods;

/// <summary>
///     Multiplayer Patch for Layered Apparel by Costel, Last Update: 21 Sep @ 8:51pm 2026
///     https://steamcommunity.com/workshop/filedetails/?id=3632480044
///     Entry point: LatePatch wires up each component below.
///     - LayeredApparelTypes: reflected mod types (Comp, Item, Condition, ...).
///     - LayeredApparelNestedSync: sync workers for ApparelTransform, ApparelAppearance,
///     ApparelLinkGroup and ApparelDisplayCondition.
///     - LayeredApparelCompSync: CompLayeredApparel list mutations (add/remove/move/clear/toggle).
///     - LayeredApparelItemPatch: LayeredApparelItem worker + item mutations
///     (appearance, offsets, links, display conditions).
///     - LayeredApparelItabGuard: Auto-link UI null-guard (DrawConditionFilters).
///     Rendering-only caches are intentionally left unsynced.
/// </summary>
[MpCompatFor("costel.layeredapparel")]
public class LayeredApparelPatch
{
    private const string LogPrefix = "[Multiplayer Layered Apparel Patch]";

    public LayeredApparelPatch(ModContentPack contentPack)
    {
        LongEventHandler.ExecuteWhenFinished(LatePatch);
    }

    private static void LatePatch()
    {
        Log.Message($"{LogPrefix} Initializing...");

        if (!LayeredApparelTypes.Lookup())
        {
            Log.Error($"{LogPrefix} Type lookup failed, patch aborted.");
            return;
        }

        LayeredApparelNestedSync.Patch();
        LayeredApparelCompSync.Patch();
        LayeredApparelItemPatch.Patch();
        LayeredApparelItabGuard.Patch();

        Log.Message($"{LogPrefix} Initialized.");
    }
}