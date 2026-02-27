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

        MelonLogger.Msg("[HAMH] Initialization complete.");

        MelonEvents.OnGUI.Subscribe(DrawMenu, 100); // The higher the value, the lower the priority.
    }

    private void DrawMenu()
    {
        GUI.Box(new Rect(0, 0, 300, 500), "My Menu");
    }

    [HarmonyPatch(typeof(ResourceControl), "TryLoadInventoryItem")]
    static class TryLoadInventoryItemPatch
    {
        static bool Prefix(object __instance, string item_name, ref bool __result)
        {
            var mgr = ItemManager.Instance;

            var item = mgr.GetItem(item_name);
            if (item != null)
            {
                Inject(item_name, item);
                __result = true;
                return false;
            }

            if (mgr.IsBaseItemBlocked(item_name))
            {
                __result = false;
                return false;
            }

            return true; // Let the game handle the rest from here...
        }

        static void Inject(string id, Item item)
        {
            var rc = UnityEngine.Object.FindObjectOfType<ResourceControl>();

            if (rc.loaded_inventory_item_files == null)
                return;

            rc.loaded_inventory_item_files[id] = ItemManager.Instance.ConvertItem(item);
        }
    }
}