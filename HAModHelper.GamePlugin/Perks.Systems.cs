using HAModHelper.GamePlugin.Helpers;
using HAModHelper.GamePlugin.Perks.Interfaces;
using Il2Cpp;
using MelonLoader;

namespace HAModHelper.GamePlugin.Perks.Systems;

public class Perk
{
    public string ModId { get; set; } = "base";
    public string PerkId { get; set; } = "";
    public string Id => $"{ModId}:{PerkId}";

    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? DetailedDescription { get; set; }

    public Dictionary<string, SinglePerkEffect>? PerkEffects { get; set; }

}

public static class PerkConverter
{
    public static PerkData ToPerkData(Perk perk)
    {
        var pdata = new PerkData();

        pdata.full_name = perk.Name ?? "Modded Perk";

        if (!string.IsNullOrWhiteSpace(perk.Description))
            pdata.description = perk.Description;

        if (!string.IsNullOrWhiteSpace(perk.DetailedDescription))
            pdata.detailed_description = perk.DetailedDescription;

        if (perk.PerkEffects != null)
            pdata.all_effects = DictHelper.DenormalizeIL2CPPDictionary(perk.PerkEffects);

        return pdata;
    }

    public static Perk FromPerkData(string fullId, PerkData data)
    {
        var (modId, id) = SplitFullId(fullId);

        var perk = new Perk
        {
            ModId = modId,
            PerkId = id,
            Name = data.full_name,
            Description = data.description,
            PerkEffects = DictHelper.NormalizeIL2CPPDictionary(data.all_effects)
        };

        perk.DetailedDescription = data.detailed_description ?? data.description;

        return perk;
    }

    /// <summary>Splits a full ID like "mod:perkname" into (mod, perkname).</summary>
    public static (string ModId, string PerkId) SplitFullId(string fullId)
    {
        var colonIndex = fullId.IndexOf(':');
        if (colonIndex > 0)
        {
            return (fullId.Substring(0, colonIndex), fullId.Substring(colonIndex + 1));
        }
        return ("base", fullId);
    }
}

public sealed class PerkManager
{
    public static PerkManager Instance { get; } = new PerkManager();

    private Dictionary<string, Perk> _perks = new();
    private Dictionary<string, Perk> _queuedPerks = new();
    private HashSet<string> _removedBasePerks = new();

    // TEST-ONLY: Spoof a fake PerkControl for tests
    public IPerkControl? DebugPerkControlSource { get; set; }
    
    private PerkManager() { }

    /// <summary>Helper to get the current perk control proxy (game or debug).</summary>
    private IPerkControl? GetPerkControl()
    {
        if (DebugPerkControlSource?.GetType() == typeof(DebugNoLoadPerkControl))
            return null;

        if (DebugPerkControlSource != null)
        {
            return DebugPerkControlSource;
        }

        // runtime path: try to locate the game object
        try
        {
            var pc = UnityEngine.Object.FindObjectOfType<PerkControl>();
            if (pc == null) return null;
            return new UnityPerkControl(pc);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>TEST-ONLY: Reset system state.</summary>
    public void Reset()
    {
        _perks = [];
        _queuedPerks = [];
        _removedBasePerks = [];
        DebugPerkControlSource = null;
    }

    /// <summary>Initialize the perk manager (called on game start).</summary>
    public void Initialize()
    {
    }

    /// <summary>Add a perk to the system.</summary>
    public void AddPerk(Perk perk)
    {
        _perks[perk.Id] = perk;
        TryInjectIntoGameCache(perk.Id, perk);
    }

    /// <summary>Delete a perk from the system.</summary>
    public void DeletePerk(Perk perk)
    {
        _perks.Remove(perk.Id);

        if (perk.ModId == "base")
            _removedBasePerks.Add(perk.PerkId);

        RemoveFromGameCache(perk.Id);
    }

    /// <summary>Patch (update) an existing perk.</summary>
    public void PatchPerk(Perk perk)
    {
        DeletePerk(perk);
        AddPerk(perk);
    }

    /// <summary>Get a perk by its full ID (e.g., "mod:perkid").</summary>
    public Perk? GetPerk(string fullId)
    {
        var pcProxy = GetPerkControl();

        if (pcProxy == null)
        {
            return null;
        }

        // Normalize "base:foo" → "foo"
        var lookupId = fullId.StartsWith("base:", StringComparison.OrdinalIgnoreCase)
            ? fullId.Substring(5)
            : fullId;

        if (_perks.TryGetValue(fullId, out var modPerk))
            return modPerk;

        if (pcProxy.GetPerk(lookupId, out var perkData))
        {
            var perk = PerkConverter.FromPerkData(lookupId, perkData);
            return perk;
        }

        return null;
    }

    /// <summary>Check if a perk ID is a base game perk.</summary>
    public bool IsBasePerk(string id)
    {
        return !_perks.ContainsKey(id);
    }

    /// <summary>Check if a base perk has been blocked/removed.</summary>
    public bool IsBasePerkBlocked(string id)
        => _removedBasePerks.Contains(id);

    // ---------- injection helpers ----------

    /// <summary>Try to inject a perk into the game cache, queueing if needed.</summary>
    public void TryInjectIntoGameCache(string id, Perk perk)
    {
        var pcProxy = GetPerkControl();
        if (pcProxy == null)
        {
            try { MelonLogger.Msg($"[HAMH] PerkControl not ready, queuing perk {id}"); } catch { }
            _queuedPerks[id] = perk;
            return;
        }

        pcProxy.SetPerk(id, ConvertPerk(perk));
    }

    /// <summary>Remove a perk from the game cache.</summary>
    public void RemoveFromGameCache(string id)
    {
        var pcProxy = GetPerkControl();
        if (pcProxy == null)
            return;

        pcProxy.RemovePerk(id);
    }

    /// <summary>Process any perks that were queued waiting for game initialization.</summary>
    public void ProcessQueuedPerks()
    {
        var processedPerk = false;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var kvp in _queuedPerks)
        {
            processedPerk = true;
            try
            {
                MelonLogger.Msg($"[HAMH] Processing queued perk {kvp.Key}");
            }
            catch { }
            TryInjectIntoGameCache(kvp.Key, kvp.Value);
        }
        _queuedPerks.Clear();
        watch.Stop();
        if (processedPerk)
            try
            {
                MelonLogger.Msg($"[HAMH] Processed queued perks in {watch.ElapsedMilliseconds}ms.");
            }
            catch { }
    }

    /// <summary>Convert a Perk object to game field dictionary.</summary>
    public PerkData ConvertPerk(Perk perk)
    {
        return PerkConverter.ToPerkData(perk);
    }
}