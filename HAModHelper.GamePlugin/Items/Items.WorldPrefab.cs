using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HAModHelper.GamePlugin.Items.Systems;

/// <summary>
/// Allows mods to register custom AssetBundle prefabs for world-placeable items whose
/// <c>World_obj_path</c> is not present in the game's Addressables catalog.
/// <para/>
/// Patches both overloads of <c>ResourceControl.AsyncInstantiateWorldObjectPrefab</c>:
/// the <see cref="InventoryItem"/> overload (initial placement / move via CreateMouseObj)
/// and the <c>string</c> overload (move via ResetDevMouseObject / TryEnterBuildMode).
/// All registered mods are checked in a single shared patch — multiple mods can safely
/// register prefabs at the same time.
/// </summary> 
public sealed class WorldPrefabManager
{
    public static WorldPrefabManager Instance { get; } = new();
    private WorldPrefabManager() { }

    private sealed class PrefabEntry
    {
        public required AssetBundle Bundle;
        public required string      AssetName;
        public          GameObject? Cache;
    }

    // keyed by full item id  ("Expansion:Gem")
    private readonly Dictionary<string, PrefabEntry> _byItemId  = new();
    // keyed by World_obj_path ("Prefabs/Expansion/gem") — shared entry when path is reused
    private readonly Dictionary<string, PrefabEntry> _byObjPath = new();

    public void Initialize() { }

    /// <summary>
    /// Register a custom world prefab.
    /// </summary>
    /// <param name="itemFullId">Full mod item id, e.g. <c>"MyMod:MyItem"</c>.</param>
    /// <param name="worldObjPath">The value set in the item's <c>World_obj_path</c> field.</param>
    /// <param name="bundle">AssetBundle that contains the prefab. Keep a static reference
    ///   in your plugin so the native backing is never freed.</param>
    /// <param name="assetName">Name of the prefab asset inside the bundle.</param>
    public void Register(string itemFullId, string worldObjPath, AssetBundle bundle, string assetName)
    {
        var entry = new PrefabEntry { Bundle = bundle, AssetName = assetName };
        _byItemId[itemFullId]   = entry;
        _byObjPath[worldObjPath] = entry;
    }

    // Called by the InventoryItem-overload patch.
    internal bool TryInvokeByItemId(string itemFullId, Il2CppSystem.Action<GameObject> callback)
        => _byItemId.TryGetValue(itemFullId, out var entry) && Invoke(entry, callback);

    // Called by the string-overload patch.
    internal bool TryInvokeByObjPath(string worldObjPath, Il2CppSystem.Action<GameObject> callback)
        => _byObjPath.TryGetValue(worldObjPath, out var entry) && Invoke(entry, callback);

    private static bool Invoke(PrefabEntry entry, Il2CppSystem.Action<GameObject> callback)
    {
        if (entry.Cache == null)
        {
            var asset = entry.Bundle.LoadAsset(entry.AssetName);
            if (asset == null) return false;
            entry.Cache = asset.Cast<GameObject>();
        }

        // Instantiate a fresh copy: the game's CreateMouseObj b__0 callback mutates
        // the received object in-place and calls Object.Destroy on it when the player
        // exits build mode — passing the prefab directly would destroy the source asset.
        var instance = Object.Instantiate(entry.Cache);
        callback?.Invoke(instance);
        return true;
    }
}

// ── Harmony patches ──────────────────────────────────────────────────────────

/// <summary>
/// Intercepts <c>ResourceControl.AsyncInstantiateWorldObjectPrefab(InventoryItem, Chunk, Action)</c>
/// (the placement path: CreateMouseObj → EnterBuildMode → GrabFurniture).
/// Uses <c>[HarmonyTargetMethod]</c> because <c>Il2CppSystem.Action&lt;GameObject&gt;</c>
/// cannot be resolved from attribute syntax at compile time in an IL2CPP context.
/// </summary>
[HarmonyPatch]
internal static class WorldPrefabItemOverloadPatch
{
    [HarmonyTargetMethod]
    static System.Reflection.MethodBase TargetMethod()
    {
        foreach (var m in typeof(ResourceControl).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (m.Name != "AsyncInstantiateWorldObjectPrefab") continue;
            var p = m.GetParameters();
            if (p.Length == 3 && p[0].ParameterType == typeof(InventoryItem))
                return m;
        }
        return null!;
    }

    [HarmonyPrefix]
    static bool Prefix(InventoryItem item, Il2CppSystem.Action<GameObject> on_asset_ready)
    {
        if (item == null) return true;
        return !WorldPrefabManager.Instance.TryInvokeByItemId(item.item_name, on_asset_ready);
    }
}

/// <summary>
/// Intercepts <c>ResourceControl.AsyncInstantiateWorldObjectPrefab(string, Chunk, Action)</c>
/// (the move path: ResetDevMouseObject / TryEnterBuildMode).
/// </summary>
[HarmonyPatch]
internal static class WorldPrefabStringOverloadPatch
{
    [HarmonyTargetMethod]
    static System.Reflection.MethodBase TargetMethod()
    {
        foreach (var m in typeof(ResourceControl).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (m.Name != "AsyncInstantiateWorldObjectPrefab") continue;
            var p = m.GetParameters();
            if (p.Length == 3 && p[0].ParameterType == typeof(string))
                return m;
        }
        return null!;
    }

    [HarmonyPrefix]
    static bool Prefix(string obj_path, Il2CppSystem.Action<GameObject> on_asset_ready)
    {
        if (string.IsNullOrEmpty(obj_path)) return true;
        return !WorldPrefabManager.Instance.TryInvokeByObjPath(obj_path, on_asset_ready);
    }
}
