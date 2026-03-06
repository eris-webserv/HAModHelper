using HAModHelper.GamePlugin.Helpers;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Newtonsoft.Json;
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
        GUI.Box(new Rect(0, 0, 300, 200), "Test Menu");
        // button that gives you item "hamltest:minosprime"
        if (GUI.Button(new Rect(10, 30, 280, 30), "Give Minos Prime"))
        {
            var ictr = UnityEngine.Object.FindObjectOfType<inventory_ctr>();
            if (ictr != null)
            {
                ictr.GiveItem("hamltest:minosprime", 1, null);
            }
        }
        if (GUI.Button(new Rect(10, 50, 280, 30), "Distort"))
        {
            var pctr = UnityEngine.Object.FindObjectOfType<PerkControl>();
            MelonLogger.Msg("Distorting...");
            foreach (var perk in pctr.loaded_perks)
            {
               MelonLogger.Msg("Perk key:", perk.Key, "Data:", DictHelper.NormalizeIL2CPPDictionary(perk.Value.all_effects));
            }
        }
    }
}