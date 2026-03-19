using MelonLoader;
using Il2Cpp;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using HAModHelper.GamePlugin.Items.Systems;
using HAModHelper.GamePlugin.Perks.Systems;
using HAModHelper.GamePlugin.Debug;
using HAModHelper.GamePlugin.Core.Debug;
using System.Net;

namespace HAModHelper.GamePlugin.Core;

internal class HAMHMod : MelonPlugin
{
    static HAMHMod()
    {
        AssemblyManager.SetOurResolveHandlerAtFront();
    }
    public override void OnPreModsLoaded()
    {
        base.OnApplicationStarted();

        MelonLogger.Msg($"[HAMH] Starting early initialization with mod version {Info.Version}, hash {MelonAssembly.Hash}");

        MelonLogger.Msg("[HAMH] Initializing subsystems...");
        try
        {

            // Subsystem init
            var stopwatch = Stopwatch.StartNew();
            ItemManager.Instance.Initialize();
            stopwatch.Stop();
            MelonLogger.Msg($"[HAMH] Initialized ItemManager in {stopwatch.ElapsedMilliseconds}ms.");

            var stopwatch2 = Stopwatch.StartNew();
            PerkManager.Instance.Initialize();
            stopwatch2.Stop();
            MelonLogger.Msg($"[HAMH] Initialized PerkManager in {stopwatch2.ElapsedMilliseconds}ms.");

            //var stopwatch3 = Stopwatch.StartNew();
            //UniverseLib.Config.UniverseLibConfig config = new()
            //{
            //    Force_Unlock_Mouse = true // no idea if this'll do anything but this feels prudent for Android.
            //};
            //UniverseLib.Universe.Init(1f, null, null, config);
            //stopwatch3.Stop();
            //MelonLogger.Msg($"[HAMH] Initialized UniverseLib in {stopwatch3.ElapsedMilliseconds}ms.");

        }
        catch (Exception ex)
        {
            MelonLogger.Error("[HAMH] Something went terribly wrong during subsystems initialization, please contact the developer! Below is the thrown exception:");

            MelonLogger.Error(ex);

            Application.Quit(1);
        }

        MelonLogger.Msg("[HAMH] Early initialization complete.");
    }

    public override void OnInitializeMelon()
    {
        base.OnInitializeMelon();

        MelonLogger.Msg("[HAMH] Handling late initialization");

        // Patch init
        MelonLogger.Msg("[HAMH] Applying Harmony patches...");
        try
        {
            HarmonyInstance.PatchAll(MelonAssembly.Assembly);
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[HAMH] Failed to apply Harmony patches, please contact the developer! Below is the thrown exception:");

            MelonLogger.Error(ex);

            Application.Quit(1);
        }
        var patches = HarmonyInstance.GetPatchedMethods();
        if (patches.Count() == 1)
        {
             MelonLogger.Error("[HAMH] Failed to apply Harmony patches, please contact the developer with your log.");

            Application.Quit(1);           
        }
        MelonLogger.Msg($"[HAMH] Applied {patches.Count()} Harmony patches.");


        // Debug init
#if DEBUG
        MelonLogger.Msg("[HAMH-DBG] Testing the isEditor patch...");
        if (!Application.isEditor)
        {
            MelonLogger.Error("[HAMH-DBG] Patch to isEditor didn't apply >:/");
        }

        MelonLogger.Msg("[HAMH-DBG] Running DebugHelper (use Release plugin to disable this!)");
        DebugHelper.Initialize();
#endif

        MelonLogger.Msg("[HAMH] Late initialization complete.");
    }

    private static void DebugLog(string toLog)
    {
#if DEBUG
        MelonLogger.Msg(toLog);
#endif
    }

    [HarmonyPatch(typeof(AdvertControl), "TryShowInterstitialAd", new Type[] { typeof(AdvertControl.ad_context) })]
    private static class IHateAds
    {
        static bool Prefix(AdvertControl.ad_context context)
        {
            DebugLog("Blocked an ad");
            return false;
        }
    }

    [HarmonyPatch(typeof(RewardsControl), "ShowRewardAskPopup")]
    private static class IReallyHateAds
    {
        static bool Prefix()
        {
            DebugLog("Blocked an ad");
            return false;
        }
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

    /*  [HarmonyPatch(typeof(inventory_ctr), "GiveItem", new Type[] { typeof(string), typeof(int), typeof(string), typeof(bool) })]
        private static class GiveItemPatch
        {
            [HarmonyPrefix]
            static bool Prefix(string item_name, int count, string fn_validator, bool visual)
            {
                DebugLog("[HAMH] GiveItem called for " + item_name);
                return true; // Let the game handle the rest from here...
            }
        }*/

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

    [HarmonyPatch(typeof(WindowPrefabsControl), "DestroyScreen", new Type[] { typeof(string) })]
    private static class DestroyScreenPatch
    {
        [HarmonyPrefix]
        static bool Prefix(string parent_name)
        {
#if DEBUG
            if (parent_name.Contains("dev -"))
            {
                MelonLogger.Msg("Denying devscreen destruction");
                return false; // No.
            }
            return true;
#else 
            return true; // Go ahead.
#endif
        }
    }

    [HarmonyPatch(typeof(Connection), "TryConnect")]
    public static class ConnectionPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Connection __instance)
        {
            try
            {
                string targetHost = "45.8.201.48";
                int targetPort = 7002;

                // We use GetHostAddresses for both. 
                // If targetHost is already an IP ("127.0.0.1"), it returns it immediately.
                // If it's a domain ("...localto.net"), it resolves it first.
                IPAddress[] addresses = Dns.GetHostAddresses(targetHost);

                if (addresses.Length > 0)
                {
                    string resolvedIp = addresses[0].ToString();

                    // Hijack the Connection instance fields
                    if (__instance.port == 7002 || __instance.port == 7003)
                    {
                        if (__instance.ip == "104.45.198.157")
                        {
                            __instance.ip = resolvedIp;
                            __instance.port = targetPort;
                        }

                    }
                    MelonLogger.Msg($"[SHJ] Redirected to {targetHost} ({resolvedIp}):{targetPort}");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[SHJ-ERR] Connection hijack failed: {e.Message}");
            }
        }
    }
}

