using HAModHelper.GamePlugin.Items.Systems;
using HAModHelper.GamePlugin.Perks.Systems;

namespace HAModHelper.GamePlugin.Debug;

public static class DebugHelper
{
    public static void Initialize()
    {
        ItemManager.Instance.AddItem(new Item
        {
            ModId = "hamltest",
            ItemId = "minosprime",
            Name = "Minos Prime",
            Description = "debug test item",
            SpritePath = "item egg",
            StackLimit = 10,
        });
        
        PerkManager.Instance.AddPerk(new Perk
        {
            ModId = "hamltest",
            PerkId = "sisyphyusprime",
            Name = "Sisyphus Prime",
            Description = "Die.",
            DetailedDescription = "Kills everything around you (when I code it)",
            SpritePath = "item egg",
        });
    }
}