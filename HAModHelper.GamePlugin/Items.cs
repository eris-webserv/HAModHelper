using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Security.Cryptography;

namespace HAModHelper.GamePlugin.Items;

// Every item loaded in HA has a relevant class instantiated. There will be a HashSet available for access by id.
// Modifying an item's Item class will modify it at runtime in-game.
// Instantiating a new Item is totally legal and will cause it to become available ingame.
public class Item
{
    public required string ModId { get; set; } // melon mod id or "base"
    public required string ItemId { get; set; }
    public string Id => $"{ModId}:{ItemId}";

    public string? Description { get; set; }
    public int StackLimit { get; set; } = 1;
    public ItemActions Actions { get; set; } = 0;
    public string? SpritePath { get; set; }

    // Escape hatch for anything not modeled yet (including keys with spaces)
    public Dictionary<string, string> ExtraFields { get; } = new();
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
    public const string KeySpritePath = "Inventory_sprite_path";

    public static Il2CppSystem.Collections.Generic.Dictionary<string, string> ToGameFields(Item item)
    {
        var d = new Il2CppSystem.Collections.Generic.Dictionary<string, string>();

        // Sprite path (real key)
        if (!string.IsNullOrWhiteSpace(item.SpritePath))
            d[KeySpritePath] = item.SpritePath!;

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
        };

        if (fields.TryGetValue(KeySpritePath, out var sprite))
            item.SpritePath = sprite;

        // Everything else goes into ExtraFields (including keys with spaces)
        foreach (var kvp in fields)
        {
            // Skip ones we modeled above
            if (kvp.Key == KeySpritePath) continue;

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

    public bool TryGetModItem(string id, out Item item)
        => _items.TryGetValue(id, out item);

    public bool IsBaseItemBlocked(string id)
        => _removedBaseItems.Contains(id);

    // ---------- injection helpers ----------

    void TryInjectIntoGameCache(string id, Item item)
    {
        var rc = UnityEngine.Object.FindObjectOfType<ResourceControl>();

        rc.loaded_inventory_item_files[id] = ConvertItem(item);
    }

    void RemoveFromGameCache(string id)
    {
        var rc = UnityEngine.Object.FindObjectOfType<ResourceControl>();

        rc.loaded_inventory_item_files.Remove(id);
    }

    public Il2CppSystem.Collections.Generic.Dictionary<string, string> ConvertItem(Item item)
    {
        return ItemConverter.ToGameFields(item);
    }
}