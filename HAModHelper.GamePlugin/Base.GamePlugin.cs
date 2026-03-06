using MelonLoader;
using Il2Cpp;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using HAModHelper.GamePlugin.Items.Systems;
using HAModHelper.GamePlugin.Perks.Systems;
using HAModHelper.GamePlugin.Debug;

namespace HAModHelper.GamePlugin.Core;

internal class HAMHMod : MelonPlugin
{
    public override void OnApplicationStarted()
    {
        base.OnApplicationStarted();

        MelonLogger.Msg($"[HAMH] Launching with mod version {Info.Version}, hash {MelonAssembly.Hash}");

        MelonLogger.Msg("[HAMH] Initializing subsystems...");

        // Subsystem init
        var stopwatch = Stopwatch.StartNew();
        ItemManager.Instance.Initialize();
        stopwatch.Stop();
        MelonLogger.Msg($"[HAMH] Initialized ItemManager in {stopwatch.ElapsedMilliseconds}ms.");

        var stopwatch2 = Stopwatch.StartNew();
        PerkManager.Instance.Initialize();
        stopwatch.Stop();
        MelonLogger.Msg($"[HAMH] Initialized PerkManager in {stopwatch2.ElapsedMilliseconds}ms.");

        // Patch init
        MelonLogger.Msg("[HAMH] Applying Harmony patches...");
        HarmonyInstance.PatchAll(MelonAssembly.Assembly);
        var patches = HarmonyInstance.GetPatchedMethods();
        MelonLogger.Msg($"[HAMH] Applied {patches.Count()} Harmony patches.");

        MelonLogger.Msg("[HAMH] Early initialization complete.");

        // Debug init
        #if DEBUG
            MelonLogger.Msg("[HAMH] Running DebugHelper (use Release plugin to disable this!)");
            DebugHelper.Initialize();
        #endif

    }

    private static void DebugLog(string toLog)
    {
        #if DEBUG
            MelonLogger.Msg(toLog);
        #endif
    }

    [HarmonyPatch(typeof(ResourceControl), "TryLoadInventoryItem", new Type[] { typeof(string) })]
    private static class TryLoadInventoryItemPatch
    {
        [HarmonyPrefix]
        static bool Prefix(string item_name, ref bool __result)
        {
            DebugLog($"[HAMH] TryLoadInventoryItem called for item: {item_name}");

            var mgr = ItemManager.Instance;

            mgr.ProcessQueuedItems();

            var isBaseItem = mgr.IsBaseItem(item_name);

            if (isBaseItem)
            {
                if (mgr.IsBaseItemBlocked(item_name))
                {
                    DebugLog($"[HAMH] Blocking base game item {item_name}");
                    __result = false;
                    return false;
                }
                else
                {
                    DebugLog($"[HAMH] Ignoring base game item {item_name}");
                    return true; // Let the game handle the rest from here...
                }
            }

            var item = mgr.GetItem(item_name);
            if (item != null)
            {
                DebugLog($"[HAMH] Providing modded item for {item_name}");
                mgr.TryInjectIntoGameCache(item_name, item);
                __result = true;
                return false;
            }

            DebugLog($"[HAMH] No item found for {item_name}");
            return true; // Let the game handle the rest from here...
        }
    }

    [HarmonyPatch(typeof(inventory_ctr), "GiveItem", new Type[] { typeof(string), typeof(int), typeof(string), typeof(bool) })]
    private static class GiveItemPatch
    {
        [HarmonyPrefix]
        static bool Prefix(string item_name, int count, string fn_validator, bool visual)
        {
            DebugLog("[HAMH] GiveItem called for " + item_name);
            return true; // Let the game handle the rest from here...
        }
    }

    [HarmonyPatch(typeof(inventory_ctr), "GetFullItemName", new Type[] { typeof(InventoryItem) })]
    private static class GetFullItemNamePatch
    {
        [HarmonyPrefix]
        static bool Prefix(InventoryItem item, ref string __result)
        {
            DebugLog("[HAMH] GetFullItemName called for item: " + item.item_name);

            var mgr = ItemManager.Instance;

            var modItem = mgr.GetItem(item.item_name);

            if (modItem != null)
            {
                DebugLog($"[HAMH] Returning modded name for {item.item_name}: {modItem.Name}");
                __result = modItem.Name;
                return false;
            }

            DebugLog($"[HAMH] No modded item found for {item.item_name}");

            return true; // Let the game handle the rest from here...
        }
    }
}