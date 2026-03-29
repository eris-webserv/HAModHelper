using HAModHelper.GamePlugin.Core;
using HAModHelper.GamePlugin.Helpers;
using HAModHelper.GamePlugin.Items.Interfaces;
using System.Reflection;

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
    public static Dictionary<string, Dictionary<string, string>> NormalizeHybridItemDictionary(Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.Dictionary<string, string>> dict)
    {
        var d = new Dictionary<string, Dictionary<string, string>>();
        foreach (var kvp in dict)
            d[kvp.Key] = DictHelper.NormalizeIL2CPPDictionary(kvp.Value);
        return d;
    }

    public static Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.Dictionary<string, string>> DenormalizeHybridItemDictionary(Dictionary<string, Dictionary<string, string>> dict)
    {
        var d = new Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.Dictionary<string, string>>();
        foreach (var kvp in dict)
        {
            d[kvp.Key] = DictHelper.DenormalizeIL2CPPDictionary(kvp.Value);
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

    public static (string modId, string id) SplitFullId(string fullId)
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

    // these 2 variables are used for intercepting item actions
    public delegate bool ItemActionHandler(string itemName, string action, int slotId);
    private List<ItemActionHandler> _interceptors = new();

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
        try
        {
            var rc = UnityEngine.Object.FindObjectOfType<ResourceControl>();
            if (rc == null) return null;
            return new UnityResourceControl(rc);
        }
        catch (Exception)
        {
            return null;
        }
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
        var split = ItemConverter.SplitFullId(id);
        return split.modId == "base";
    }

    public bool IsBaseItemBlocked(string id)
        => _removedBaseItems.Contains(id);

    // ---------- injection helpers ----------

    public void TryInjectIntoGameCache(string id, Item item)
    {
        var rcProxy = GetResourceControl();
        if (rcProxy == null)
        {
            try { HAMHMod.Logger.LogInfo($"[HAMH] ResourceControl not ready, queuing item {id}"); } catch { }
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
                HAMHMod.Logger.LogInfo($"[HAMH] Processing queued item {kvp.Key}");
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
                HAMHMod.Logger.LogInfo($"[HAMH] Processed queued items in {watch.ElapsedMilliseconds}ms.");
            }
            catch { }
        ;
    }

    public Dictionary<string, string> ConvertItem(Item item)
    {
        return ItemConverter.ToGameFields(item);
    }

    public void RegisterInterceptor(ItemActionHandler handler)
    {
        _interceptors.Add(handler);
    }

    public bool HandleItemDoubleClick(object inventoryCtr, int uiSlotId, string action)
    {
        int trueSlotId = GetMappedIndex(inventoryCtr, uiSlotId);
        string itemName = GetItemName(inventoryCtr, trueSlotId);

        bool allowOriginal = true; // this tells the game whether to add the mod's logic on top of the original item logic (true) or to skip the original handling entirely (false)

        foreach (var handler in _interceptors)
        {
            if (!handler.Invoke(itemName, action, trueSlotId))
            {
                allowOriginal = false;
            }
        }

        return allowOriginal;
    }


    // some helpers
    private string GetItemName(object instance, int trueSlotId)
    {
        try
        {
            var invProp = instance.GetType().GetProperty("player_inventory");
            var playerInv = invProp?.GetValue(instance);
            if (playerInv == null) return "Empty";

            var getItem = playerInv.GetType().GetMethod("get_Item", [typeof(int)]);
            var itemPair = getItem?.Invoke(playerInv, [trueSlotId]);
            var item = itemPair?.GetType().GetProperty("item")?.GetValue(itemPair);
            return item?.GetType().GetProperty("item_name")?.GetValue(item)?.ToString() ?? "Empty";
        }
        catch { return "Empty"; }
    }

    private int GetMappedIndex(object instance, int uiSlotId)
    {
        try
        {
            var method = instance.GetType().GetMethod("GetTrueInventoryIndex", (System.Reflection.BindingFlags)62);
            if (method != null) return (int)method.Invoke(instance, [uiSlotId]);

            var field = instance.GetType().GetField("current_page") ?? instance.GetType().GetField("page_index");
            if (field != null) return ((int)field.GetValue(instance) * 15) + uiSlotId;
        }
        catch { }
        return uiSlotId;
    }
}