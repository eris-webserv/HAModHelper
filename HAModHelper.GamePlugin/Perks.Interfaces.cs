using System.Diagnostics.CodeAnalysis;
using HAModHelper.GamePlugin.Helpers;
using Il2Cpp;

namespace HAModHelper.GamePlugin.Perks.Interfaces;

public interface IPerkControl
{
    Dictionary<string, PerkData> loaded_perks { get; set; }

    public bool GetPerk(string id, [NotNullWhen(true)] out PerkData? outData)
    {
        if (loaded_perks.TryGetValue(id, out var data))
        {
            outData = data;
            return true;
        }
        outData = null;
        return false;
    }

    public void SetPerk(string id, PerkData data)
    {
        loaded_perks[id] = data;
    }

    public void RemovePerk(string id)
    {
        loaded_perks.Remove(id);
    }
}

// runtime adapter that wraps the real game PerkControl
public class UnityPerkControl : IPerkControl
{
    private readonly PerkControl _pc;
    public UnityPerkControl(PerkControl pc) => _pc = pc;
    
    public Dictionary<string, PerkData> loaded_perks
    {
        get => DictHelper.NormalizeIL2CPPDictionary(_pc.loaded_perks);
        set { _pc.loaded_perks = DictHelper.DenormalizeIL2CPPDictionary(value); }
    }

    public void SetPerk(string id, PerkData data)
    {
        _pc.loaded_perks[id] = data;
    }

    public void RemovePerk(string id)
    {
        _pc.loaded_perks.Remove(id);
    }
}

public class DebugNoLoadPerkControl : IPerkControl
{
    public Dictionary<string, PerkData> loaded_perks { get; set; } = new();
}