namespace HAModHelper.GamePlugin.Items;

// Every item loaded in HA has a relevant class instantiated. There will be a HashSet available for access by id.
// Modifying an item's Item class will modify it at runtime in-game.
// Instantiating a new Item is totally legal and will cause it to become available ingame.
public class Item
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required int StackLimit { get; set; }
    public required ItemActions Actions { get; set; }

    public Item(string Id, string Name, int StackLimit, ItemActions Actions)
    {
        this.Id = Id;
        this.Name = Name;
        this.StackLimit = StackLimit;
        this.Actions = Actions;
    }
}

[Flags]
public enum ItemActions
{
    IsTool = 1 << 0,
    IsUsable = 1 << 1,
    IsConsumable = 1 << 2,
    IsPlaceable = 1 << 3,
}

public sealed class ItemManager
{
    public static ItemManager Instance { get; } = new ItemManager();

    private readonly Dictionary<Type, List<Delegate>> _subs = new();

    private ItemManager()
    {
        
    }

}