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

    internal static FieldInfo GetField(Type type, string fieldName)
    {
        if (type == null) return null;
        var fieldInfo = AccessTools.Field(type, fieldName);
        if (fieldInfo != null) return fieldInfo;
        // Fallback to explicit flags (covers private backing fields).
        return type.GetField(fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
    }

    internal static void TryRegister(MethodInfo methodInfo, string label)
    {
        try
        {
            if (methodInfo == null)
            {
                Log.Warning($"{LogPrefix} Skip sync (null method): {label}");
                return;
            }

            MP.RegisterSyncMethod(methodInfo).CancelIfAnyArgNull();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Register {label} failed: {exception}");
        }
    }

    internal static void TryRegisterNoCancel(MethodInfo methodInfo, string label)
    {
        try
        {
            if (methodInfo == null)
            {
                Log.Warning($"{LogPrefix} Skip sync (null method): {label}");
                return;
            }

            MP.RegisterSyncMethod(methodInfo);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Register {label} failed: {exception}");
        }
    }

    internal static object GetComp(Pawn pawn)
    {
        if (pawn == null || LayeredApparelTypes.CompType == null) return null;
        try
        {
            if (pawn.AllComps == null) return null;
            foreach (var thingComp in pawn.AllComps)
                if (thingComp != null && thingComp.GetType() == LayeredApparelTypes.CompType)
                    return thingComp;
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} GetComp failed: {exception}");
        }

        return null;
    }

    internal static IList GetItemsList(object comp, int listTypeInt)
    {
        try
        {
            var enumValue = Enum.ToObject(LayeredApparelTypes.ListTypeEnum, listTypeInt);
            var getItemsMethod = AccessTools.Method(LayeredApparelTypes.CompType, "GetItems",
                new[] { LayeredApparelTypes.ListTypeEnum });
            var result = getItemsMethod?.Invoke(comp, new[] { enumValue }) as IList;
            return result;
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} GetItemsList failed: {exception}");
            return null;
        }
    }

    internal static void WriteStringList(SyncWorker sync, List<string> stringList)
    {
        if (stringList == null)
        {
            sync.Write(-1);
            return;
        }

        sync.Write(stringList.Count);
        foreach (var entry in stringList) sync.Write(entry);
    }

    internal static List<string> ReadStringList(SyncWorker sync)
    {
        var count = sync.Read<int>();
        if (count < 0) return null;
        var stringList = new List<string>(count);
        for (var index = 0; index < count; index++) stringList.Add(sync.Read<string>());
        return stringList;
    }
}