namespace HAModHelper.GamePlugin.Helpers;

public static class DictHelper
{
    public static Dictionary<T1, T2> NormalizeIL2CPPDictionary<T1, T2>(Il2CppSystem.Collections.Generic.Dictionary<T1, T2> dict) where T1 : notnull
    {
        var d = new Dictionary<T1, T2>();
        foreach (var kvp in dict)
            d[kvp.Key] = kvp.Value;
        return d;
    }

    public static Il2CppSystem.Collections.Generic.Dictionary<T1, T2> DenormalizeIL2CPPDictionary<T1, T2>(Dictionary<T1, T2> dict) where T1 : notnull
    {
        var d = new Il2CppSystem.Collections.Generic.Dictionary<T1, T2>();
        foreach (var kvp in dict)
            d[kvp.Key] = kvp.Value;
        return d;
    }
}