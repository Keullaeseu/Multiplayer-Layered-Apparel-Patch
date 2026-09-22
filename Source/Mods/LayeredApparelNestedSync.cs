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
        var _conditionType = LayeredApparelTypes.ConditionType;
        condLayer = LayeredApparelSyncHelpers.GetField(_conditionType, "Layer");
        condLayers = LayeredApparelSyncHelpers.GetField(_conditionType, "_layers");
        condRequireAll = LayeredApparelSyncHelpers.GetField(_conditionType, "RequireAllLayers");
        condBodyGroups = LayeredApparelSyncHelpers.GetField(_conditionType, "BodyGroups");
        condUseArmor = LayeredApparelSyncHelpers.GetField(_conditionType, "UseArmor");
        condArmorStat = LayeredApparelSyncHelpers.GetField(_conditionType, "ArmorStat");
        condMinimumArmor = LayeredApparelSyncHelpers.GetField(_conditionType, "MinimumArmor");
        condArmorMinimums = LayeredApparelSyncHelpers.GetField(_conditionType, "_armorMinimums");
        condArmorSystem = LayeredApparelSyncHelpers.GetField(_conditionType, "_armorSystem");

        var _linkGroupType = LayeredApparelTypes.LinkGroupType;
        linkEquipment = LayeredApparelSyncHelpers.GetField(_linkGroupType, "Equipment");
        linkRequireAll = LayeredApparelSyncHelpers.GetField(_linkGroupType, "RequireAll");

        var _appearanceType = LayeredApparelTypes.AppearanceType;
        appearFront = LayeredApparelSyncHelpers.GetField(_appearanceType, "Front");
        appearSide = LayeredApparelSyncHelpers.GetField(_appearanceType, "Side");
        appearBack = LayeredApparelSyncHelpers.GetField(_appearanceType, "Back");
        appearBodyType = LayeredApparelSyncHelpers.GetField(_appearanceType, "BodyType");

        var _transformType = LayeredApparelTypes.TransformType;
        transScale = LayeredApparelSyncHelpers.GetField(_transformType, "Scale");
        transOffset = LayeredApparelSyncHelpers.GetField(_transformType, "Offset");
        transRotation = LayeredApparelSyncHelpers.GetField(_transformType, "Rotation");
    }

    private static void RegisterWorkers()
    {
        try
        {
            MP.RegisterSyncWorker<object>(SyncApparelTransform, LayeredApparelTypes.TransformType,
                shouldConstruct: true);
        }
        catch (Exception _exception)
        {
            Log.Error($"{LogPrefix} Register ApparelTransform worker failed: {_exception}");
        }

        try
        {
            MP.RegisterSyncWorker<object>(SyncApparelAppearance, LayeredApparelTypes.AppearanceType,
                shouldConstruct: true);
        }
        catch (Exception _exception)
        {
            Log.Error($"{LogPrefix} Register ApparelAppearance worker failed: {_exception}");
        }

        try
        {
            MP.RegisterSyncWorker<object>(SyncApparelLinkGroup, LayeredApparelTypes.LinkGroupType,
                shouldConstruct: true);
        }
        catch (Exception _exception)
        {
            Log.Error($"{LogPrefix} Register ApparelLinkGroup worker failed: {_exception}");
        }

        try
        {
            MP.RegisterSyncWorker<object>(SyncApparelDisplayCondition, LayeredApparelTypes.ConditionType,
                shouldConstruct: true);
        }
        catch (Exception _exception)
        {
            Log.Error($"{LogPrefix} Register ApparelDisplayCondition worker failed: {_exception}");
        }
    }

    private static void SyncApparelTransform(SyncWorker _sync, ref object _object)
    {
        if (_sync.isWriting)
        {
            var _isNotNull = _object != null;
            _sync.Write(_isNotNull);
            if (!_isNotNull) return;
            var _scale = (Vector2)transScale.GetValue(_object);
            var _offset = (Vector2)transOffset.GetValue(_object);
            var _rotation = (float)transRotation.GetValue(_object);
            _sync.Write(_scale.x);
            _sync.Write(_scale.y);
            _sync.Write(_offset.x);
            _sync.Write(_offset.y);
            _sync.Write(_rotation);
        }
        else
        {
            if (!_sync.Read<bool>())
            {
                _object = null;
                return;
            }

            float _scaleX = _sync.Read<float>(), _scaleY = _sync.Read<float>();
            float _offsetX = _sync.Read<float>(), _offsetY = _sync.Read<float>();
            var _rotation = _sync.Read<float>();
            var _instance = Activator.CreateInstance(LayeredApparelTypes.TransformType);
            transScale.SetValue(_instance, new Vector2(_scaleX, _scaleY));
            transOffset.SetValue(_instance, new Vector2(_offsetX, _offsetY));
            transRotation.SetValue(_instance, _rotation);
            _object = _instance;
        }
    }

    private static void WriteTransformInline(SyncWorker _sync, object _transform)
    {
        var _isNotNull = _transform != null;
        _sync.Write(_isNotNull);
        if (!_isNotNull) return;
        var _scale = (Vector2)transScale.GetValue(_transform);
        var _offset = (Vector2)transOffset.GetValue(_transform);
        _sync.Write(_scale.x);
        _sync.Write(_scale.y);
        _sync.Write(_offset.x);
        _sync.Write(_offset.y);
        _sync.Write((float)transRotation.GetValue(_transform));
    }

    private static object ReadTransformInline(SyncWorker _sync)
    {
        if (!_sync.Read<bool>()) return null;
        float _scaleX = _sync.Read<float>(), _scaleY = _sync.Read<float>();
        float _offsetX = _sync.Read<float>(), _offsetY = _sync.Read<float>();
        var _rotation = _sync.Read<float>();
        var _instance = Activator.CreateInstance(LayeredApparelTypes.TransformType);
        transScale.SetValue(_instance, new Vector2(_scaleX, _scaleY));
        transOffset.SetValue(_instance, new Vector2(_offsetX, _offsetY));
        transRotation.SetValue(_instance, _rotation);
        return _instance;
    }

    private static void SyncApparelAppearance(SyncWorker _sync, ref object _object)
    {
        if (_sync.isWriting)
        {
            var _isNotNull = _object != null;
            _sync.Write(_isNotNull);
            if (!_isNotNull) return;
            WriteTransformInline(_sync, appearFront.GetValue(_object));
            WriteTransformInline(_sync, appearSide.GetValue(_object));
            WriteTransformInline(_sync, appearBack.GetValue(_object));
            _sync.Write((Def)appearBodyType.GetValue(_object));
        }
        else
        {
            if (!_sync.Read<bool>())
            {
                _object = null;
                return;
            }

            var _instance = Activator.CreateInstance(LayeredApparelTypes.AppearanceType);
            appearFront.SetValue(_instance, ReadTransformInline(_sync));
            appearSide.SetValue(_instance, ReadTransformInline(_sync));
            appearBack.SetValue(_instance, ReadTransformInline(_sync));
            appearBodyType.SetValue(_instance, _sync.Read<Def>());
            _object = _instance;
        }
    }

    internal static void WriteAppearanceInline(SyncWorker _sync, object _appearance)
    {
        var _isNotNull = _appearance != null;
        _sync.Write(_isNotNull);
        if (!_isNotNull) return;
        WriteTransformInline(_sync, appearFront.GetValue(_appearance));
        WriteTransformInline(_sync, appearSide.GetValue(_appearance));
        WriteTransformInline(_sync, appearBack.GetValue(_appearance));
        _sync.Write((Def)appearBodyType.GetValue(_appearance));
    }

    internal static object ReadAppearanceInline(SyncWorker _sync)
    {
        if (!_sync.Read<bool>()) return null;
        var _instance = Activator.CreateInstance(LayeredApparelTypes.AppearanceType);
        appearFront.SetValue(_instance, ReadTransformInline(_sync));
        appearSide.SetValue(_instance, ReadTransformInline(_sync));
        appearBack.SetValue(_instance, ReadTransformInline(_sync));
        appearBodyType.SetValue(_instance, _sync.Read<Def>());
        return _instance;
    }

    private static void SyncApparelLinkGroup(SyncWorker _sync, ref object _object)
    {
        if (_sync.isWriting)
        {
            var _isNotNull = _object != null;
            _sync.Write(_isNotNull);
            if (!_isNotNull) return;
            LayeredApparelSyncHelpers.WriteStringList(_sync, linkEquipment.GetValue(_object) as List<string>);
            _sync.Write((bool)linkRequireAll.GetValue(_object));
        }
        else
        {
            if (!_sync.Read<bool>())
            {
                _object = null;
                return;
            }

            var _instance = Activator.CreateInstance(LayeredApparelTypes.LinkGroupType);
            linkEquipment.SetValue(_instance, LayeredApparelSyncHelpers.ReadStringList(_sync));
            linkRequireAll.SetValue(_instance, _sync.Read<bool>());
            _object = _instance;
        }
    }

    private static void WriteLinkGroupInline(SyncWorker _sync, object _linkGroup)
    {
        var _isNotNull = _linkGroup != null;
        _sync.Write(_isNotNull);
        if (!_isNotNull) return;
        LayeredApparelSyncHelpers.WriteStringList(_sync, linkEquipment.GetValue(_linkGroup) as List<string>);
        _sync.Write((bool)linkRequireAll.GetValue(_linkGroup));
    }

    private static object ReadLinkGroupInline(SyncWorker _sync)
    {
        if (!_sync.Read<bool>()) return null;
        var _instance = Activator.CreateInstance(LayeredApparelTypes.LinkGroupType);
        linkEquipment.SetValue(_instance, LayeredApparelSyncHelpers.ReadStringList(_sync));
        linkRequireAll.SetValue(_instance, _sync.Read<bool>());
        return _instance;
    }

    internal static void WriteLinkGroupList(SyncWorker _sync, IList _linkGroups)
    {
        if (_linkGroups == null)
        {
            _sync.Write(-1);
            return;
        }

        _sync.Write(_linkGroups.Count);
        foreach (var _linkGroup in _linkGroups) WriteLinkGroupInline(_sync, _linkGroup);
    }

    internal static IList ReadLinkGroupList(SyncWorker _sync)
    {
        var _count = _sync.Read<int>();
        if (_count < 0) return null;
        var _genericListType = typeof(List<>).MakeGenericType(LayeredApparelTypes.LinkGroupType);
        var _groupList = (IList)Activator.CreateInstance(_genericListType);
        for (var _index = 0; _index < _count; _index++) _groupList.Add(ReadLinkGroupInline(_sync));
        return _groupList;
    }

    private static void SyncApparelDisplayCondition(SyncWorker _sync, ref object _object)
    {
        if (_sync.isWriting) WriteConditionInline(_sync, _object);
        else _object = ReadConditionInline(_sync);
    }

    internal static void WriteConditionInline(SyncWorker _sync, object _condition)
    {
        var _isNotNull = _condition != null;
        _sync.Write(_isNotNull);
        if (!_isNotNull) return;
        _sync.Write((string)condLayer.GetValue(_condition));
        LayeredApparelSyncHelpers.WriteStringList(_sync, condLayers.GetValue(_condition) as List<string>);
        _sync.Write((bool)condRequireAll.GetValue(_condition));
        LayeredApparelSyncHelpers.WriteStringList(_sync, condBodyGroups.GetValue(_condition) as List<string>);
        _sync.Write((bool)condUseArmor.GetValue(_condition));
        _sync.Write((string)condArmorStat.GetValue(_condition));
        _sync.Write((float)condMinimumArmor.GetValue(_condition));
        var _armorMinimums = condArmorMinimums.GetValue(_condition) as IDictionary;
        if (_armorMinimums == null)
        {
            _sync.Write(-1);
        }
        else
        {
            _sync.Write(_armorMinimums.Count);
            foreach (DictionaryEntry _dictEntry in _armorMinimums)
            {
                _sync.Write((string)_dictEntry.Key);
                _sync.Write((float)_dictEntry.Value);
            }
        }

        _sync.Write((int)condArmorSystem.GetValue(_condition));
    }

    internal static object ReadConditionInline(SyncWorker _sync)
    {
        if (!_sync.Read<bool>()) return null;
        var _instance = Activator.CreateInstance(LayeredApparelTypes.ConditionType);
        condLayer.SetValue(_instance, _sync.Read<string>());
        condLayers.SetValue(_instance, LayeredApparelSyncHelpers.ReadStringList(_sync));
        condRequireAll.SetValue(_instance, _sync.Read<bool>());
        condBodyGroups.SetValue(_instance, LayeredApparelSyncHelpers.ReadStringList(_sync));
        condUseArmor.SetValue(_instance, _sync.Read<bool>());
        condArmorStat.SetValue(_instance, _sync.Read<string>());
        condMinimumArmor.SetValue(_instance, _sync.Read<float>());
        var _count = _sync.Read<int>();
        if (_count < 0)
        {
            condArmorMinimums.SetValue(_instance, null);
        }
        else
        {
            var _armorMinimums = (IDictionary)Activator.CreateInstance(typeof(Dictionary<string, float>));
            for (var _index = 0; _index < _count; _index++) _armorMinimums[_sync.Read<string>()] = _sync.Read<float>();
            condArmorMinimums.SetValue(_instance, _armorMinimums);
        }

        condArmorSystem.SetValue(_instance, _sync.Read<int>());
        return _instance;
    }
}