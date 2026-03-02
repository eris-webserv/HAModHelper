using System.Diagnostics.CodeAnalysis;
using HAModHelper.GamePlugin.Items.Interfaces;
using Il2Cpp;
using MelonLoader;
using Newtonsoft.Json;

namespace HAModHelper.GamePlugin.Items.Systems;

// Every item loaded in HA has a relevant class instantiated. There will be a HashSet available for access by id.
// Modifying an item's Item class will modify it at runtime in-game.
// Instantiating a new Item is totally legal and will cause it to become available ingame.
public class Item
{
    public required string ModId { get; set; } // melon mod id or "base"
    public required string ItemId { get; set; }
    public string Id => $"{ModId}:{ItemId}";

    public required string Name { get; set; }
    public string? Description { get; set; }
    public int StackLimit { get; set; } = 1;
    public ItemActions Actions { get; set; } = 0; // UNUSED AS OF YET UNTIL I FIND A WAY TO PATCH THE NECCESARY FUNCTIONS!!
    public string? SpritePath { get; set; }

    // Escape hatch for anything not modeled yet (including keys with spaces)
    public Dictionary<string, string> ExtraFields { get; set; } = new();
}

[Flags]
public enum ItemActions
{
    IsTool = 1 << 0,
    IsUsable = 1 << 1,
    IsConsumable = 1 << 2,
    IsPlaceable = 1 << 3,
}

public static class ItemConverter
{
    public static Dictionary<T1, T2> NormalizeIL2CPPDictionary<T1, T2>(Il2CppSystem.Collections.Generic.Dictionary<T1, T2> dict) where T1 : notnull
    {
        var d = new Dictionary<T1, T2>();
        foreach (var kvp in dict)
            d[kvp.Key] = kvp.Value;
        return d;
    }

    public static Dictionary<string, Dictionary<string, string>> NormalizeHybridItemDictionary(Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.Dictionary<string, string>> dict)
    {
        var d = new Dictionary<string, Dictionary<string, string>>();
        foreach (var kvp in dict)
            d[kvp.Key] = NormalizeIL2CPPDictionary(kvp.Value);
        return d;
    }

    public static Il2CppSystem.Collections.Generic.Dictionary<T1, T2> DenormalizeIL2CPPDictionary<T1, T2>(Dictionary<T1, T2> dict) where T1 : notnull
    {
        var d = new Il2CppSystem.Collections.Generic.Dictionary<T1, T2>();
        foreach (var kvp in dict)
            d[kvp.Key] = kvp.Value;
        return d;
    }

    public static Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.Dictionary<string, string>> DenormalizeHybridItemDictionary(Dictionary<string, Dictionary<string, string>> dict)
    {
        var d = new Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.Dictionary<string, string>>();
        foreach (var kvp in dict)
        {
            d[kvp.Key] = DenormalizeIL2CPPDictionary(kvp.Value);
        }
        return d;
    }

    public static Dictionary<string, string> ToGameFields(Item item)
    {
        var d = new Dictionary<string, string>();

        // Sprite path (real key)
        if (!string.IsNullOrWhiteSpace(item.SpritePath))
            d["Inventory_sprite_path"] = item.SpritePath;

        d["Name"] = item.Name ?? "Modded Item";

        d["Max_stack"] = item.StackLimit.ToString();

        if (!string.IsNullOrWhiteSpace(item.Description))
            d["Description"] = item.Description;

        // Extra fields override everything else (modder wins).
        foreach (var (k, v) in item.ExtraFields)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            d[k] = v ?? "";
        }

        return d;
    }

    // Optional helper: turn a game dict back into an Item (useful for GetItem(base:...)) (actually only for that)
    public static Item FromGameFields(string fullId, Dictionary<string, string> fields)
    {
        var (modId, id) = SplitFullId(fullId);

        var item = new Item
        {
            ModId = modId,
            ItemId = id,
            Name = fields.TryGetValue("Name", out var name) ? name : id,
        };

        if (fields.TryGetValue("Inventory_sprite_path", out var sprite))
            item.SpritePath = sprite;

        if (fields.TryGetValue("Description", out var desc))
            item.Description = desc;

        if (fields.TryGetValue("Max_stack", out var stackStr) && int.TryParse(stackStr, out var stack))
            item.StackLimit = stack;

        // Everything else goes into ExtraFields (including keys with spaces)
        foreach (var kvp in fields)
        {
            // Skip ones we modeled above
            if (kvp.Key == "Inventory_sprite_path") continue;
            if (kvp.Key == "Name") continue;
            if (kvp.Key == "Description") continue;

            item.ExtraFields[kvp.Key] = kvp.Value;
        }

        return item;
    }

    private static (string modId, string id) SplitFullId(string fullId)
    {
        var idx = fullId.IndexOf(':');
        if (idx <= 0) return ("base", fullId);
        return (fullId.Substring(0, idx), fullId.Substring(idx + 1));
    }
}

public sealed class ItemManager
{
    public static ItemManager Instance { get; } = new ItemManager();

    private Dictionary<string, Item> _items = new();
    private Dictionary<string, Item> _queuedItems = new();
    private HashSet<string> _removedBaseItems = new();

    // TEST-ONLY: Spoof a fake ResourceControl for HAModHelper.Tests to use
    public IResourceControl? DebugResourceControlSource { get; set; }
    private ItemManager() { }

    // helper used by methods to obtain a proxy object
    private IResourceControl? GetResourceControl()
    {
        if (DebugResourceControlSource?.GetType() == typeof(DebugNoLoadResourceControl))
            return null; // don't

        if (DebugResourceControlSource != null)
        {
            return DebugResourceControlSource;
        }

        // runtime path: try to locate the game object
        var rc = UnityEngine.Object.FindObjectOfType<ResourceControl>();
        if (rc == null) return null;
        MelonLogger.Msg("[HAMH] ResourceControl found, initializing proxy");
        return new UnityResourceControl(rc);
    }

    // TEST-ONLY: Reset system state.
    public void Reset()
    {
        _items = [];
        _queuedItems = [];
        _removedBaseItems = [];
        DebugResourceControlSource = null;
    }

    public void Initialize()
    {
    }

    public void AddItem(Item item)
    {
        _items[item.Id] = item;

        TryInjectIntoGameCache(item.Id, item);
    }

    public void DeleteItem(Item item)
    {
        _items.Remove(item.Id);

        if (item.ModId == "base")
            _removedBaseItems.Add(item.ItemId);

        RemoveFromGameCache(item.Id);
    }

    public void PatchItem(Item item)
    {
        DeleteItem(item);
        AddItem(item);
    }

    public Item? GetItem(string fullId)
    {
        var rcProxy = GetResourceControl();

        if (rcProxy == null)
        {
            return null;
        }

        // Normalize "base:foo" → "foo"
        var lookupId = fullId.StartsWith("base:", StringComparison.OrdinalIgnoreCase)
            ? fullId.Substring(5)
            : fullId;

        if (_items.TryGetValue(fullId, out var modItem))
            return modItem;

        if (rcProxy.GetItem(lookupId, out var gameFields))
        {
            var item = ItemConverter.FromGameFields(lookupId, gameFields);
            return item;
        }

        return null;
    }

    public bool IsBaseItem(string id)
    {
        return !_items.ContainsKey(id);
    }

    public bool IsBaseItemBlocked(string id)
        => _removedBaseItems.Contains(id);

    // ---------- injection helpers ----------

    public void TryInjectIntoGameCache(string id, Item item)
    {
        var rcProxy = GetResourceControl();
        if (rcProxy == null)
        {
            try { MelonLogger.Msg($"[HAMH] ResourceControl not ready, queuing item {id}"); } catch { }
            _queuedItems[id] = item;
            return;
        }

        rcProxy.SetItem(id, ConvertItem(item));
    }

    public void RemoveFromGameCache(string id)
    {
        var rcProxy = GetResourceControl();
        if (rcProxy == null)
            return;

        rcProxy.RemoveItem(id);
    }

    public void ProcessQueuedItems()
    {
        var processedItem = false;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var kvp in _queuedItems)
        {
            processedItem = true;
            try
            {
                MelonLogger.Msg($"[HAMH] Processing queued item {kvp.Key}");
            }
            catch { }
            ;
            TryInjectIntoGameCache(kvp.Key, kvp.Value);
        }
        _queuedItems.Clear();
        watch.Stop();
        if (processedItem)
            try
            {
                MelonLogger.Msg($"[HAMH] Processed queued items in {watch.ElapsedMilliseconds}ms.");
            }
            catch { }
        ;
    }

    public Dictionary<string, string> ConvertItem(Item item)
    {
        return ItemConverter.ToGameFields(item);
    }
}