using System.Diagnostics.CodeAnalysis;
using HAModHelper.GamePlugin.Items.Systems;
using Il2Cpp;

namespace HAModHelper.GamePlugin.Items.Interfaces;
public interface IResourceControl
{
    Dictionary<string, Dictionary<string, string>> loaded_inventory_item_files { get; set; }

    public bool GetItem(string id, [NotNullWhen(true)] out Dictionary<string, string>? outFields)
    {
        if (loaded_inventory_item_files.TryGetValue(id, out var fields))
        {
            outFields = fields;
            return true;
        }
        outFields = null;
        return false;
    }

    public void SetItem(string id, Dictionary<string, string> fields)
    {
        loaded_inventory_item_files[id] = fields;
    }

    public void RemoveItem(string id)
    {
        loaded_inventory_item_files.Remove(id);
    }
}

// runtime adapter that wraps the real game ResourceControl
public class UnityResourceControl : IResourceControl
{
    private readonly ResourceControl _rc;
    public UnityResourceControl(ResourceControl rc) => _rc = rc;
    public Dictionary<string, Dictionary<string, string>> loaded_inventory_item_files
    {
        get => ItemConverter.NormalizeHybridItemDictionary(_rc.loaded_inventory_item_files);
        set { _rc.loaded_inventory_item_files = ItemConverter.DenormalizeHybridItemDictionary(value); }
    }

    public void SetItem(string id, Dictionary<string, string> fields)
    {
        _rc.loaded_inventory_item_files[id] = ItemConverter.DenormalizeIL2CPPDictionary(fields);
    }

    public void RemoveItem(string id)
    {
        _rc.loaded_inventory_item_files.Remove(id);
    }
}

public class DebugNoLoadResourceControl : IResourceControl
{
    public Dictionary<string, Dictionary<string, string>> loaded_inventory_item_files { get; set; } = new();
}