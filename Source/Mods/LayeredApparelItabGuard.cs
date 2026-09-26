using System.Reflection;
using HarmonyLib;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerLayeredApparelPatch.Source.Mods;

/// <summary>
///     Crash guard for the ITab condition editor. Clicking the "Auto link" (mode 2)
///     button calls _conditionItem.SetConnectionMode(2) and then, in the SAME GUI pass,
///     DrawDisplayConditions -> DrawConditionFilters reads
///     _conditionItem.DisplayCondition (null until mode==2 takes effect).
///     In singleplayer SetConnectionMode applies instantly so the condition exists by
///     the time filters draw. With MP sync the mutation is queued and applies a tick
///     later, leaving a one-frame window where _conditionMode==2 but
///     DisplayCondition==null -> NullReferenceException at DrawConditionFilters+0x43.
///     The mod itself null-guards the same property in ToggleConditionLayer /
///     ToggleConditionGroup / ConditionEquipmentReason, but not here.
///     Fix: skip drawing the filters for frames where the condition is not there yet.
///     No game state is touched, so no desync risk; next frame (after the synced
///     SetConnectionMode applies) draws normally. Applied always, not just in MP:
///     in SP the guard never triggers.
/// </summary>
internal static class LayeredApparelItabGuard
{
    private const string LogPrefix = "[Multiplayer Layered Apparel Itab Guard Patch]";

    private static FieldInfo itabConditionItemField;
    private static PropertyInfo itemDisplayConditionProp;

    internal static void Patch()
    {
        var itabType = LayeredApparelTypes.ItabType;
        if (itabType == null) return;
        try
        {
            itabConditionItemField = LayeredApparelSyncHelpers.GetField(itabType, "_conditionItem");
            itemDisplayConditionProp = AccessTools.Property(LayeredApparelTypes.ItemType, "DisplayCondition");
            if (itabConditionItemField == null || itemDisplayConditionProp == null)
            {
                Log.Warning($"{LogPrefix} UI null-guard skipped (field/property not found).");
                return;
            }

            var drawFiltersMethod = AccessTools.DeclaredMethod(itabType, "DrawConditionFilters");
            if (drawFiltersMethod == null)
            {
                Log.Warning($"{LogPrefix} UI null-guard skipped (DrawConditionFilters not found).");
                return;
            }

            MpCompat.harmony.Patch(drawFiltersMethod,
                new HarmonyMethod(typeof(LayeredApparelItabGuard), nameof(PreDrawConditionFilters)));
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} UI null-guard patch failed: {exception}");
        }
    }

    private static bool PreDrawConditionFilters(object __instance)
    {
        // true = run original, false = skip this frame (condition arrives next tick).
        try
        {
            var conditionItem = itabConditionItemField?.GetValue(__instance);
            if (conditionItem == null) return false;
            if (itemDisplayConditionProp?.GetValue(conditionItem) == null) return false;
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"{LogPrefix} UI null-guard check failed: {exception}", 0x6A11E6);
            return false;
        }

        return true;
    }
}