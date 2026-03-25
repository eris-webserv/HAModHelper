using System.Diagnostics;
using HAModHelper.GamePlugin.Items.Systems;
using HAModHelper.GamePlugin.Perks.Systems;
using HAModHelper.GamePlugin.Core.Debug;
using System.Net;
using BepInEx.Unity.IL2CPP;
using BepInEx;
using HarmonyLib;
using BepInEx.Logging;

namespace HAModHelper.GamePlugin.Core;

[BepInAutoPlugin]
public partial class HAMHMod : BasePlugin
{

    public Harmony Harmony { get; } = new(Id);

    static HAMHMod()
    {
        AssemblyManager.SetOurResolveHandlerAtFront();
    }
    internal static new ManualLogSource Log;

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo($"[HAMH] Starting initialization with mod version {Info.Version}, hash {MelonAssembly.Hash}");

        Log.LogInfo("[HAMH] Initializing subsystems...");
        try
        {
            // Subsystem init
            var stopwatch = Stopwatch.StartNew();
            ItemManager.Instance.Initialize();
            stopwatch.Stop();
            Log.LogInfo($"[HAMH] Initialized ItemManager in {stopwatch.ElapsedMilliseconds}ms.");

            var stopwatch2 = Stopwatch.StartNew();
            PerkManager.Instance.Initialize();
            stopwatch2.Stop();
            Log.LogInfo($"[HAMH] Initialized PerkManager in {stopwatch2.ElapsedMilliseconds}ms.");
        }
        catch (Exception ex)
        {
            Log.LogError("[HAMH] Something went terribly wrong during subsystems initialization, please contact the developer! Below is the thrown exception:");

            Log.LogError(ex);
        }

        Log.LogInfo("[HAMH] Applying Harmony patches...");
        try
        {
            Harmony.PatchAll();
        }
        catch (Exception ex)
        {
            Log.LogError("[HAMH] Failed to apply Harmony patches, please contact the developer! Below is the thrown exception:");

            Log.LogError(ex);
        }
        var patches = Harmony.GetPatchedMethods();
        if (patches.Count() == 1)
        {
            Log.LogError("[HAMH] Failed to apply Harmony patches, please contact the developer with your log.");
        }
        Log.LogInfo($"[HAMH] Applied {patches.Count()} Harmony patches.");

        //var stopwatch3 = Stopwatch.StartNew();
        //UniverseLib.Config.UniverseLibConfig config = new()
        //{
        //     
        //};
        //UniverseLib.Universe.Init(0f, null, (string msg, UnityEngine.LogType _) => { Log.LogInfo(msg); }, config);
        //stopwatch3.Stop();
        //Log.LogInfo($"[HAMH] Initialized UniverseLib in {stopwatch3.ElapsedMilliseconds}ms.");

        // Debug init
#if DEBUG
        Log.LogInfo("[HAMH-DBG] Running DebugHelper (use Release plugin to disable this!)");
        // DebugHelper.Initialize();
#endif

        Log.LogInfo("[HAMH] Initialization complete.");
    }

    private static void DebugLog(string toLog)
    {
#if DEBUG
        Log.LogDebug(toLog);
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

    [HarmonyPatch(typeof(PopupControl), "ShowRewardAskPopup", new Type[] { typeof(AdvertControl.reward_ad_type) })]

    private static class IReallyHateAds
    {
        static bool Prefix(AdvertControl.reward_ad_type reward_ad_type_t)
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

    [HarmonyPatch(typeof(Connection), "TryConnect")]
    public static class ConnectionPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Connection __instance)
        {
            try
            {
                // string targetHost = "192.168.1.196";
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
                    DebugLog($"[HAML] Redirected the Friend Server to {targetHost} ({resolvedIp}):{targetPort}");
                }
            }
            catch (Exception e)
            {
                Log.LogError($"[SHJ-ERR] Connection hijack failed: {e.Message}");
            }
        }
    }
}

