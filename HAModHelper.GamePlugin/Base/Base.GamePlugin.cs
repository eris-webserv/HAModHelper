using System.Diagnostics;
using HAModHelper.GamePlugin.Items.Systems;
using HAModHelper.GamePlugin.Perks.Systems;
using HAModHelper.GamePlugin.Dialogue.Systems;
using HAModHelper.GamePlugin.Core.Debug;
using System.Net;
using BepInEx.Unity.IL2CPP;
using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using System.Reflection;
using System.Security.Cryptography;
using HAModHelper.GamePlugin.Debug;
using static FriendServerInterface;

namespace HAModHelper.GamePlugin.Core;

[BepInAutoPlugin]
public partial class HAMHMod : BasePlugin
{
    internal static BepInPlugin? PluginData;
    internal Harmony? Harmony { get; } = new(Id);
    internal static ManualLogSource Logger { private set; get; } = null!;
    internal static readonly string AssemblyHash = ComputeHash();

    static string ComputeHash()
    {
        var path = Assembly.GetExecutingAssembly().Location;
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(File.ReadAllBytes(path));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    static HAMHMod()
    {
        AssemblyManager.SetOurResolveHandlerAtFront();
    }

    public override void Load()
    {
        Logger = base.Log;
        PluginData = MetadataHelper.GetMetadata(this);

        if (Harmony is null)
        {
            Log.LogError("[HAMH] WHERE THE FUCK IS HARMONY?!?!?!? (Harmony didn't load. The mod will crash once it gets to hooking required functions.)");
        }

        Log.LogInfo($"[HAMH] Starting initialization with mod version {Version}, hash {AssemblyHash}");

        Log.LogInfo("[HAMH] Checking mods...");

        List<string> blacklistedModHashes = new()
        {

        };

        List<string> blacklistedModNames = new()
        {

        };

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

            var stopwatch3 = Stopwatch.StartNew();
            WorldPrefabManager.Instance.Initialize();
            stopwatch3.Stop();
            Log.LogInfo($"[HAMH] Initialized WorldPrefabManager in {stopwatch3.ElapsedMilliseconds}ms.");

            var stopwatch4 = Stopwatch.StartNew();
            CraftingInjectionManager.Instance.Initialize();
            stopwatch4.Stop();
            Log.LogInfo($"[HAMH] Initialized CraftingInjectionManager in {stopwatch4.ElapsedMilliseconds}ms.");

            var stopwatch5 = Stopwatch.StartNew();
            DialogueManager.Instance.Initialize();
            stopwatch5.Stop();
            Log.LogInfo($"[HAMH] Initialized DialogueManager in {stopwatch5.ElapsedMilliseconds}ms.");

            //var stopwatch3 = Stopwatch.StartNew();
            //UniverseLibConfig uvlconfig = new UniverseLibConfig
            //{
            //    Disable_EventSystem_Override = true
            //};
            //Universe.Init(1f, null, (string msg, UnityEngine.LogType _) => { Log.LogInfo(msg);}, uvlconfig);
            //stopwatch3.Stop();
            //Log.LogInfo($"[HAMH] Initialized UniverseLib in {stopwatch3.ElapsedMilliseconds}ms.");
        }
        catch (Exception ex)
        {
            Log.LogError("[HAMH] Something went terribly wrong during subsystems initialization, please contact the developer! Below is the thrown exception:");

            Log.LogError(ex);
        }

        Log.LogInfo("[HAMH] Applying Harmony patches...");
        try
        {
            Harmony!.PatchAll();
        }
        catch (Exception ex)
        {
            Log.LogError("[HAMH] Failed to apply Harmony patches, please contact the developer! Below is the thrown exception:");

            Log.LogError(ex);
        }
        var patches = Harmony!.GetPatchedMethods();
        if (patches.Count() == 1)
        {
            Log.LogError("[HAMH] Failed to apply Harmony patches, please contact the developer with your log.");
        }
        Log.LogInfo($"[HAMH] Applied {patches.Count()} Harmony patches.");

        // Debug init
#if DEBUG
        Log.LogInfo("[HAMH-DBG] Running DebugHelper (use Release plugin to disable this!)");
        DebugHelper.Initialize();
#endif

        Log.LogInfo("[HAMH] Initialization complete.");
    }

    private static void DebugLog(string toLog)
    {
#if DEBUG
        Logger.LogDebug(toLog);
#endif
    }

    // Restore the left/right miniwindow tabs when the friends list (screen 4) is opened.
    // The current game calls HideMiniwindowHeaders for screen 4, hiding the tab that lets
    // you swap to the public server browser. This postfix re-shows them after the setup runs.
    [HarmonyPatch(typeof(FriendServerInterface), "ChangeFriendScreen", new Type[] { typeof(friend_window_screen) })]
    private static class RestoreServerListTabPatch
    {
        static void Postfix(friend_window_screen new_screen)
        {
            if (new_screen != friend_window_screen.friend_list) return;
            var wc = WindowControl.Instance;
            if (wc == null) return;
            wc.ShowMiniwindowHeaders();
            var wpc = WindowPrefabsControl.Instance;
            if (wpc == null) return;

            var windowPrefabScreen = wpc.prefab_screens_instantiated["FRIENDS-friends_list"];
            var header = windowPrefabScreen.transform.Find("header");
            if (header != null)
                header.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(AdvertControl), nameof(AdvertControl.LaunchAds))]
    public static class IHateAds
    {
        public static bool Prefix()
        {
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
                Logger.LogError($"[SHJ-ERR] Connection hijack failed: {e.Message}");
            }
        }
    }
}

