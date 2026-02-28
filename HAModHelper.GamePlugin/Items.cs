using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Security.Cryptography;
using MelonLoader;

namespace HAModHelper.GamePlugin.Items;

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

internal static class ItemConverter
{
    public static Il2CppSystem.Collections.Generic.Dictionary<string, string> ToGameFields(Item item)
    {
        var d = new Il2CppSystem.Collections.Generic.Dictionary<string, string>();

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
    public static Item FromGameFields(string fullId, Il2CppSystem.Collections.Generic.Dictionary<string, string> fields)
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
    private ItemManager()
    {

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
            _removedBaseItems.Add(item.Id);

        RemoveFromGameCache(item.Id);
    }

    public void PatchItem(Item item)
    {
        DeleteItem(item);
        AddItem(item);
    }

    public Item? GetItem(string fullId)
    {
        var rc = UnityEngine.Object.FindObjectOfType<ResourceControl>();

        // Normalize "base:foo" → "foo"
        var lookupId = fullId.StartsWith("base:", StringComparison.OrdinalIgnoreCase)
            ? fullId.Substring(5)
            : fullId;

        if (_items.TryGetValue(fullId, out var modItem))
            return modItem;

        if (rc.loaded_inventory_item_files != null && rc.loaded_inventory_item_files.TryGetValue(lookupId, out var gameFields))
        {
            var item = ItemConverter.FromGameFields(lookupId, gameFields);
            return item;
        }

        return null;
    }

    public bool IsBaseItemBlocked(string id)
        => _removedBaseItems.Contains(id);

    // ---------- injection helpers ----------

    public void TryInjectIntoGameCache(string id, Item item)
    {
        try {
        var rc = UnityEngine.Object.FindObjectOfType<ResourceControl>();

        rc.loaded_inventory_item_files[id] = ConvertItem(item);
        } catch (Exception)
        {
            MelonLogger.Msg($"[HAMH] ResourceControl not ready, queuing item {id}");
            // queue for when ResourceControl spawns
            _queuedItems[id] = item;
        }
    }

    public void RemoveFromGameCache(string id)
    {
        var rc = UnityEngine.Object.FindObjectOfType<ResourceControl>();

        rc.loaded_inventory_item_files.Remove(id);
    }

    public void ProcessQueuedItems()
    {
        var processedItem = false;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var kvp in _queuedItems)
        {
            processedItem = true;
            MelonLogger.Msg($"[HAMH] Processing queued item {kvp.Key}");
            TryInjectIntoGameCache(kvp.Key, kvp.Value);
        }
        _queuedItems.Clear();
        watch.Stop();
        if (processedItem)
            MelonLogger.Msg($"[HAMH] Processed queued items in {watch.ElapsedMilliseconds}ms.");
    }

    public Il2CppSystem.Collections.Generic.Dictionary<string, string> ConvertItem(Item item)
    {
        return ItemConverter.ToGameFields(item);
    }
}