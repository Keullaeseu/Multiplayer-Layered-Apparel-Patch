using System.Collections;
using System.Reflection;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace MultiplayerLayeredApparelPatch.Source.Mods;

/// <summary>
///     Sync workers (by value) for the nested LayeredApparel data types:
///     ApparelTransform, ApparelAppearance, ApparelLinkGroup, ApparelDisplayCondition.
///     All use ref object + explicit Type so no compile-time reference to
///     LayeredApparel.dll is required.
/// </summary>
internal static class LayeredApparelNestedSync
{
    private const string LogPrefix = "[Multiplayer Layered Apparel Patch]";

    // Condition fields
    private static FieldInfo condLayer;
    private static FieldInfo condLayers; // _layers List<string>
    private static FieldInfo condRequireAll;
    private static FieldInfo condBodyGroups;
    private static FieldInfo condUseArmor;
    private static FieldInfo condArmorStat;
    private static FieldInfo condMinimumArmor;
    private static FieldInfo condArmorMinimums; // Dictionary<string,float>
    private static FieldInfo condArmorSystem;

    // LinkGroup fields
    private static FieldInfo linkEquipment;
    private static FieldInfo linkRequireAll;

    // Appearance fields
    private static FieldInfo appearFront;
    private static FieldInfo appearSide;
    private static FieldInfo appearBack;
    private static FieldInfo appearBodyType;

    // Transform fields
    private static FieldInfo transScale;
    private static FieldInfo transOffset;
    private static FieldInfo transRotation;

    internal static void Patch()
    {
        CacheFields();
        RegisterWorkers();
    }

    private static void CacheFields()
    {
        var conditionType = LayeredApparelTypes.ConditionType;
        condLayer = LayeredApparelSyncHelpers.GetField(conditionType, "Layer");
        condLayers = LayeredApparelSyncHelpers.GetField(conditionType, "_layers");
        condRequireAll = LayeredApparelSyncHelpers.GetField(conditionType, "RequireAllLayers");
        condBodyGroups = LayeredApparelSyncHelpers.GetField(conditionType, "BodyGroups");
        condUseArmor = LayeredApparelSyncHelpers.GetField(conditionType, "UseArmor");
        condArmorStat = LayeredApparelSyncHelpers.GetField(conditionType, "ArmorStat");
        condMinimumArmor = LayeredApparelSyncHelpers.GetField(conditionType, "MinimumArmor");
        condArmorMinimums = LayeredApparelSyncHelpers.GetField(conditionType, "_armorMinimums");
        condArmorSystem = LayeredApparelSyncHelpers.GetField(conditionType, "_armorSystem");

        var linkGroupType = LayeredApparelTypes.LinkGroupType;
        linkEquipment = LayeredApparelSyncHelpers.GetField(linkGroupType, "Equipment");
        linkRequireAll = LayeredApparelSyncHelpers.GetField(linkGroupType, "RequireAll");

        var appearanceType = LayeredApparelTypes.AppearanceType;
        appearFront = LayeredApparelSyncHelpers.GetField(appearanceType, "Front");
        appearSide = LayeredApparelSyncHelpers.GetField(appearanceType, "Side");
        appearBack = LayeredApparelSyncHelpers.GetField(appearanceType, "Back");
        appearBodyType = LayeredApparelSyncHelpers.GetField(appearanceType, "BodyType");

        var transformType = LayeredApparelTypes.TransformType;
        transScale = LayeredApparelSyncHelpers.GetField(transformType, "Scale");
        transOffset = LayeredApparelSyncHelpers.GetField(transformType, "Offset");
        transRotation = LayeredApparelSyncHelpers.GetField(transformType, "Rotation");
    }

    private static void RegisterWorkers()
    {
        try
        {
            MP.RegisterSyncWorker<object>(SyncApparelTransform, LayeredApparelTypes.TransformType,
                shouldConstruct: true);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Register ApparelTransform worker failed: {exception}");
        }

        try
        {
            MP.RegisterSyncWorker<object>(SyncApparelAppearance, LayeredApparelTypes.AppearanceType,
                shouldConstruct: true);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Register ApparelAppearance worker failed: {exception}");
        }

        try
        {
            MP.RegisterSyncWorker<object>(SyncApparelLinkGroup, LayeredApparelTypes.LinkGroupType,
                shouldConstruct: true);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Register ApparelLinkGroup worker failed: {exception}");
        }

        try
        {
            MP.RegisterSyncWorker<object>(SyncApparelDisplayCondition, LayeredApparelTypes.ConditionType,
                shouldConstruct: true);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Register ApparelDisplayCondition worker failed: {exception}");
        }
    }

    private static void SyncApparelTransform(SyncWorker sync, ref object obj)
    {
        if (sync.isWriting)
        {
            var isNotNull = obj != null;
            sync.Write(isNotNull);
            if (!isNotNull) return;
            var scale = (Vector2)transScale.GetValue(obj);
            var offset = (Vector2)transOffset.GetValue(obj);
            var rotation = (float)transRotation.GetValue(obj);
            sync.Write(scale.x);
            sync.Write(scale.y);
            sync.Write(offset.x);
            sync.Write(offset.y);
            sync.Write(rotation);
        }
        else
        {
            if (!sync.Read<bool>())
            {
                obj = null;
                return;
            }

            float scaleX = sync.Read<float>(), scaleY = sync.Read<float>();
            float offsetX = sync.Read<float>(), offsetY = sync.Read<float>();
            var rotation = sync.Read<float>();
            var instance = Activator.CreateInstance(LayeredApparelTypes.TransformType);
            transScale.SetValue(instance, new Vector2(scaleX, scaleY));
            transOffset.SetValue(instance, new Vector2(offsetX, offsetY));
            transRotation.SetValue(instance, rotation);
            obj = instance;
        }
    }

    private static void WriteTransformInline(SyncWorker sync, object transform)
    {
        var isNotNull = transform != null;
        sync.Write(isNotNull);
        if (!isNotNull) return;
        var scale = (Vector2)transScale.GetValue(transform);
        var offset = (Vector2)transOffset.GetValue(transform);
        sync.Write(scale.x);
        sync.Write(scale.y);
        sync.Write(offset.x);
        sync.Write(offset.y);
        sync.Write((float)transRotation.GetValue(transform));
    }

    private static object ReadTransformInline(SyncWorker sync)
    {
        if (!sync.Read<bool>()) return null;
        float scaleX = sync.Read<float>(), scaleY = sync.Read<float>();
        float offsetX = sync.Read<float>(), offsetY = sync.Read<float>();
        var rotation = sync.Read<float>();
        var instance = Activator.CreateInstance(LayeredApparelTypes.TransformType);
        transScale.SetValue(instance, new Vector2(scaleX, scaleY));
        transOffset.SetValue(instance, new Vector2(offsetX, offsetY));
        transRotation.SetValue(instance, rotation);
        return instance;
    }

    private static void SyncApparelAppearance(SyncWorker sync, ref object obj)
    {
        if (sync.isWriting)
        {
            var isNotNull = obj != null;
            sync.Write(isNotNull);
            if (!isNotNull) return;
            WriteTransformInline(sync, appearFront.GetValue(obj));
            WriteTransformInline(sync, appearSide.GetValue(obj));
            WriteTransformInline(sync, appearBack.GetValue(obj));
            sync.Write((Def)appearBodyType.GetValue(obj));
        }
        else
        {
            if (!sync.Read<bool>())
            {
                obj = null;
                return;
            }

            var instance = Activator.CreateInstance(LayeredApparelTypes.AppearanceType);
            appearFront.SetValue(instance, ReadTransformInline(sync));
            appearSide.SetValue(instance, ReadTransformInline(sync));
            appearBack.SetValue(instance, ReadTransformInline(sync));
            appearBodyType.SetValue(instance, sync.Read<Def>());
            obj = instance;
        }
    }

    internal static void WriteAppearanceInline(SyncWorker sync, object appearance)
    {
        var isNotNull = appearance != null;
        sync.Write(isNotNull);
        if (!isNotNull) return;
        WriteTransformInline(sync, appearFront.GetValue(appearance));
        WriteTransformInline(sync, appearSide.GetValue(appearance));
        WriteTransformInline(sync, appearBack.GetValue(appearance));
        sync.Write((Def)appearBodyType.GetValue(appearance));
    }

    internal static object ReadAppearanceInline(SyncWorker sync)
    {
        if (!sync.Read<bool>()) return null;
        var instance = Activator.CreateInstance(LayeredApparelTypes.AppearanceType);
        appearFront.SetValue(instance, ReadTransformInline(sync));
        appearSide.SetValue(instance, ReadTransformInline(sync));
        appearBack.SetValue(instance, ReadTransformInline(sync));
        appearBodyType.SetValue(instance, sync.Read<Def>());
        return instance;
    }

    private static void SyncApparelLinkGroup(SyncWorker sync, ref object obj)
    {
        if (sync.isWriting)
        {
            var isNotNull = obj != null;
            sync.Write(isNotNull);
            if (!isNotNull) return;
            LayeredApparelSyncHelpers.WriteStringList(sync, linkEquipment.GetValue(obj) as List<string>);
            sync.Write((bool)linkRequireAll.GetValue(obj));
        }
        else
        {
            if (!sync.Read<bool>())
            {
                obj = null;
                return;
            }

            var instance = Activator.CreateInstance(LayeredApparelTypes.LinkGroupType);
            linkEquipment.SetValue(instance, LayeredApparelSyncHelpers.ReadStringList(sync));
            linkRequireAll.SetValue(instance, sync.Read<bool>());
            obj = instance;
        }
    }

    private static void WriteLinkGroupInline(SyncWorker sync, object linkGroup)
    {
        var isNotNull = linkGroup != null;
        sync.Write(isNotNull);
        if (!isNotNull) return;
        LayeredApparelSyncHelpers.WriteStringList(sync, linkEquipment.GetValue(linkGroup) as List<string>);
        sync.Write((bool)linkRequireAll.GetValue(linkGroup));
    }

    private static object ReadLinkGroupInline(SyncWorker sync)
    {
        if (!sync.Read<bool>()) return null;
        var instance = Activator.CreateInstance(LayeredApparelTypes.LinkGroupType);
        linkEquipment.SetValue(instance, LayeredApparelSyncHelpers.ReadStringList(sync));
        linkRequireAll.SetValue(instance, sync.Read<bool>());
        return instance;
    }

    internal static void WriteLinkGroupList(SyncWorker sync, IList linkGroups)
    {
        if (linkGroups == null)
        {
            sync.Write(-1);
            return;
        }

        sync.Write(linkGroups.Count);
        foreach (var linkGroup in linkGroups) WriteLinkGroupInline(sync, linkGroup);
    }

    internal static IList ReadLinkGroupList(SyncWorker sync)
    {
        var count = sync.Read<int>();
        if (count < 0) return null;
        var genericListType = typeof(List<>).MakeGenericType(LayeredApparelTypes.LinkGroupType);
        var groupList = (IList)Activator.CreateInstance(genericListType);
        for (var index = 0; index < count; index++) groupList.Add(ReadLinkGroupInline(sync));
        return groupList;
    }

    private static void SyncApparelDisplayCondition(SyncWorker sync, ref object obj)
    {
        if (sync.isWriting) WriteConditionInline(sync, obj);
        else obj = ReadConditionInline(sync);
    }

    internal static void WriteConditionInline(SyncWorker sync, object condition)
    {
        var isNotNull = condition != null;
        sync.Write(isNotNull);
        if (!isNotNull) return;
        sync.Write((string)condLayer.GetValue(condition));
        LayeredApparelSyncHelpers.WriteStringList(sync, condLayers.GetValue(condition) as List<string>);
        sync.Write((bool)condRequireAll.GetValue(condition));
        LayeredApparelSyncHelpers.WriteStringList(sync, condBodyGroups.GetValue(condition) as List<string>);
        sync.Write((bool)condUseArmor.GetValue(condition));
        sync.Write((string)condArmorStat.GetValue(condition));
        sync.Write((float)condMinimumArmor.GetValue(condition));
        var armorMinimums = condArmorMinimums.GetValue(condition) as IDictionary;
        if (armorMinimums == null)
        {
            sync.Write(-1);
        }
        else
        {
            sync.Write(armorMinimums.Count);
            foreach (DictionaryEntry dictEntry in armorMinimums)
            {
                sync.Write((string)dictEntry.Key);
                sync.Write((float)dictEntry.Value);
            }
        }

        sync.Write((int)condArmorSystem.GetValue(condition));
    }

    internal static object ReadConditionInline(SyncWorker sync)
    {
        if (!sync.Read<bool>()) return null;
        var instance = Activator.CreateInstance(LayeredApparelTypes.ConditionType);
        condLayer.SetValue(instance, sync.Read<string>());
        condLayers.SetValue(instance, LayeredApparelSyncHelpers.ReadStringList(sync));
        condRequireAll.SetValue(instance, sync.Read<bool>());
        condBodyGroups.SetValue(instance, LayeredApparelSyncHelpers.ReadStringList(sync));
        condUseArmor.SetValue(instance, sync.Read<bool>());
        condArmorStat.SetValue(instance, sync.Read<string>());
        condMinimumArmor.SetValue(instance, sync.Read<float>());
        var count = sync.Read<int>();
        if (count < 0)
        {
            condArmorMinimums.SetValue(instance, null);
        }
        else
        {
            var armorMinimums = (IDictionary)Activator.CreateInstance(typeof(Dictionary<string, float>));
            for (var index = 0; index < count; index++) armorMinimums[sync.Read<string>()] = sync.Read<float>();
            condArmorMinimums.SetValue(instance, armorMinimums);
        }

        condArmorSystem.SetValue(instance, sync.Read<int>());
        return instance;
    }
}