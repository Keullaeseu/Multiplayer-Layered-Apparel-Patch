using System.Collections;
using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace MultiplayerLayeredApparelPatch.Source.Mods;

/// <summary>
///     Reflection and MP-registration helpers shared by all LayeredApparel components.
/// </summary>
internal static class LayeredApparelSyncHelpers
{
    private const string LogPrefix = "[Multiplayer Layered Apparel Sync Helpers Patch]";

    internal static FieldInfo GetField(Type _type, string _fieldName)
    {
        if (_type == null) return null;
        var _fieldInfo = AccessTools.Field(_type, _fieldName);
        if (_fieldInfo != null) return _fieldInfo;
        // Fallback to explicit flags (covers private backing fields).
        return _type.GetField(_fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
    }

    internal static void TryRegister(MethodInfo _methodInfo, string _label)
    {
        try
        {
            if (_methodInfo == null)
            {
                Log.Warning($"{LogPrefix} Skip sync (null method): {_label}");
                return;
            }

            MP.RegisterSyncMethod(_methodInfo).CancelIfAnyArgNull();
        }
        catch (Exception _exception)
        {
            Log.Error($"{LogPrefix} Register {_label} failed: {_exception}");
        }
    }

    internal static void TryRegisterNoCancel(MethodInfo _methodInfo, string _label)
    {
        // For setters where default(T) like false/0 is valid, CancelIfAnyArgNull
        // only cancels on null refs, so it's safe. But bool/int/float params are
        // never null, so both variants behave the same. Keep a separate helper
        // for readability at call sites.
        TryRegister(_methodInfo, _label);
    }

    internal static object GetComp(Pawn _pawn)
    {
        if (_pawn == null || LayeredApparelTypes.CompType == null) return null;
        try
        {
            if (_pawn.AllComps == null) return null;
            foreach (var _thingComp in _pawn.AllComps)
                if (_thingComp != null && _thingComp.GetType() == LayeredApparelTypes.CompType)
                    return _thingComp;
        }
        catch (Exception _exception)
        {
            Log.Error($"{LogPrefix} GetComp failed: {_exception}");
        }

        return null;
    }

    internal static IList GetItemsList(object _comp, int _listTypeInt)
    {
        try
        {
            var _enumValue = Enum.ToObject(LayeredApparelTypes.ListTypeEnum, _listTypeInt);
            var _getItemsMethod = AccessTools.Method(LayeredApparelTypes.CompType, "GetItems",
                new[] { LayeredApparelTypes.ListTypeEnum });
            var _result = _getItemsMethod?.Invoke(_comp, new[] { _enumValue }) as IList;
            return _result;
        }
        catch (Exception _exception)
        {
            Log.Error($"{LogPrefix} GetItemsList failed: {_exception}");
            return null;
        }
    }

    internal static void WriteStringList(SyncWorker _sync, List<string> _stringList)
    {
        if (_stringList == null)
        {
            _sync.Write(-1);
            return;
        }

        _sync.Write(_stringList.Count);
        foreach (var _entry in _stringList) _sync.Write(_entry);
    }

    internal static List<string> ReadStringList(SyncWorker _sync)
    {
        var _count = _sync.Read<int>();
        if (_count < 0) return null;
        var _stringList = new List<string>(_count);
        for (var _index = 0; _index < _count; _index++) _stringList.Add(_sync.Read<string>());
        return _stringList;
    }
}