using HAModHelper.GamePlugin.Items.Systems;

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

        MelonEvents.OnGUI.Subscribe(DrawMenu, 100); // The higher the value, the lower the priority.    
    }

    private static void DrawMenu()
    {
        GUI.Box(new Rect(0, 0, 200, 200), "Test Menu");
        // button that gives you item "hamltest:minosprime"
        if (GUI.Button(new Rect(0, 30, 100, 30), "Give Minos Prime"))
        {
            var ictr = UnityEngine.Object.FindObjectOfType<inventory_ctr>();
            if (ictr != null)
            {
                ictr.GiveItem("hamltest:minosprime", 1, null);
            }
        }
        if (GUI.Button(new Rect(0, 60, 100, 30), "Dump Perks"))
        {
            try
            {
                var pctr = UnityEngine.Object.FindObjectOfType<PerkControl>();
                Log.LogInfo("[HAMH-DBG] Dumping all perks...");
                foreach (var perk in pctr.loaded_perks)
                {
                }
            }
            catch (Exception)
            {
                Log.LogInfo("Something went wrong. You probably tried to dump perks before PerkControl was loaded.");
            }
        }
        if (GUI.Button(new Rect(0, 90, 100, 30), "Give Genomes"))
        {
            var pctr = UnityEngine.Object.FindObjectOfType<PerkControl>();
            pctr.genomes = 69420;
            pctr.SaveGenomes();
        }
    }
}