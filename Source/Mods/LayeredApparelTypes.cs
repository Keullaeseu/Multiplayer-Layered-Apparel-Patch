using HarmonyLib;
using Verse;

namespace MultiplayerLayeredApparelPatch.Source.Mods;

/// <summary>
///     Reflected LayeredApparel types. Looked up once from <see cref="LayeredApparelPatch.LatePatch" />.
///     Core types abort the patch when missing; preset and ITab types are optional.
/// </summary>
internal static class LayeredApparelTypes
{
    private const string LogPrefix = "[Multiplayer Layered Apparel Types Patch]";

    internal static Type CompType;
    internal static Type ItemType;
    internal static Type ConditionType;
    internal static Type LinkGroupType;
    internal static Type AppearanceType;
    internal static Type TransformType;
    internal static Type ListTypeEnum;
    private static Type presetItemType;
    internal static Type ItabType;

    internal static bool Lookup()
    {
        CompType = AccessTools.TypeByName("LayeredApparel.CompLayeredApparel");
        ItemType = AccessTools.TypeByName("LayeredApparel.LayeredApparelItem");
        ConditionType = AccessTools.TypeByName("LayeredApparel.ApparelDisplayCondition");
        LinkGroupType = AccessTools.TypeByName("LayeredApparel.ApparelLinkGroup");
        AppearanceType = AccessTools.TypeByName("LayeredApparel.ApparelAppearance");
        TransformType = AccessTools.TypeByName("LayeredApparel.ApparelTransform");
        ListTypeEnum = AccessTools.TypeByName("LayeredApparel.CosmeticListType");
        presetItemType = AccessTools.TypeByName("LayeredApparel.LayeredApparelPreset+PresetItem");
        ItabType = AccessTools.TypeByName("LayeredApparel.ITab_LayeredApparel");

        if (CompType == null) Log.Error($"{LogPrefix} Missing type LayeredApparel.CompLayeredApparel");
        if (ItemType == null) Log.Error($"{LogPrefix} Missing type LayeredApparel.LayeredApparelItem");
        if (ConditionType == null) Log.Error($"{LogPrefix} Missing type LayeredApparel.ApparelDisplayCondition");
        if (LinkGroupType == null) Log.Error($"{LogPrefix} Missing type LayeredApparel.ApparelLinkGroup");
        if (AppearanceType == null) Log.Error($"{LogPrefix} Missing type LayeredApparel.ApparelAppearance");
        if (TransformType == null) Log.Error($"{LogPrefix} Missing type LayeredApparel.ApparelTransform");
        if (ListTypeEnum == null) Log.Error($"{LogPrefix} Missing type LayeredApparel.CosmeticListType");

        // PresetItem is optional (only needed for preset-load sync). Don't abort if missing.
        if (presetItemType == null)
            Log.Warning(
                $"{LogPrefix} Optional type LayeredApparelPreset+PresetItem not found, preset sync will be skipped.");

        // ITab type is optional: only needed for the DrawConditionFilters null-guard.
        // The sync itself works without it.
        if (ItabType == null)
            Log.Warning(
                $"{LogPrefix} Optional type LayeredApparel.ITab_LayeredApparel not found, UI null-guard will be skipped.");

        return CompType != null && ItemType != null && ConditionType != null
               && LinkGroupType != null && AppearanceType != null && TransformType != null
               && ListTypeEnum != null;
    }
}