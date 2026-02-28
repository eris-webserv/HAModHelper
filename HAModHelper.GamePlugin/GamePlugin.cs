using MelonLoader;
using Il2Cpp;
using HAModHelper.GamePlugin.Items;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace HAModHelper.GamePlugin.Core;

internal class HAMHMod : MelonPlugin
{
    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("[HAMH] Initializing subsystems...");
        var stopwatch = Stopwatch.StartNew();
        ItemManager.Instance.Initialize();
        stopwatch.Stop();
        MelonLogger.Msg($"[HAMH] Initialized ItemManager in {stopwatch.ElapsedMilliseconds}ms.");

        MelonLogger.Msg("[HAMH] Applying Harmony patches...");
        HarmonyInstance.PatchAll();

        var patches = HarmonyInstance.GetPatchedMethods();
        MelonLogger.Msg($"[HAMH] Applied {patches.Count()} Harmony patches.");

        ItemManager.Instance.AddItem(new Item
        {
            ModId = "hamltest",
            ItemId = "minosprime",
            Name = "Minos Prime",
            Description = "debug test item",
            SpritePath = "item egg",
        });

        MelonLogger.Msg("[HAMH] Initialization complete.");

        MelonEvents.OnGUI.Subscribe(DrawMenu, 100); // The higher the value, the lower the priority.
    }

    private void DrawMenu()
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
    }

    [HarmonyPatch(typeof(ResourceControl), "TryLoadInventoryItem", new Type[] { typeof(string) })]
    private static class TryLoadInventoryItemPatch
    {
        [HarmonyPrefix]
        static bool Prefix(string item_name, ref bool __result)
        {
            MelonLogger.Msg("[HAMH] TryLoadInventoryItem called for item: " + item_name);

            var mgr = ItemManager.Instance;

            mgr.ProcessQueuedItems();

            var item = mgr.GetItem(item_name);
            if (item != null)
            {
                MelonLogger.Msg($"[HAMH] Providing modded item for {item_name}");
                mgr.TryInjectIntoGameCache(item_name, item);
                __result = true;
                return false;
            }

            if (mgr.IsBaseItemBlocked(item_name))
            {
                MelonLogger.Msg($"[HAMH] Blocking base game item {item_name}");
                __result = false;
                return false;
            }

            MelonLogger.Msg($"[HAMH] No modded item found for {item_name}");
            return true; // Let the game handle the rest from here...
        }
    }

    [HarmonyPatch(typeof(inventory_ctr), "GiveItem", new Type[] { typeof(string), typeof(int), typeof(string), typeof(bool) })]
    private static class GiveItemPatch
    {
        [HarmonyPrefix]
        static bool Prefix(string item_name, int count, string fn_validator, bool visual)
        {
            MelonLogger.Msg("[HAMH] GiveItem called for " + item_name);
            return true; // Let the game handle the rest from here...
        }
    }

    [HarmonyPatch(typeof(inventory_ctr), "GetFullItemName", new Type[] { typeof(InventoryItem) })]
    private static class GetFullItemNamePatch
    {
        [HarmonyPrefix]
        static bool Prefix(InventoryItem item, ref string __result)
        {
            MelonLogger.Msg("[HAMH] GetFullItemName called for item: " + item.item_name);

            var mgr = ItemManager.Instance;

            var modItem = mgr.GetItem(item.item_name);

            if (modItem != null)
            {
                MelonLogger.Msg($"[HAMH] Returning modded full ID for {item.item_name}");
                __result = modItem.Name;
                return false;
            }

            return true; // Let the game handle the rest from here...
        }
    }
}