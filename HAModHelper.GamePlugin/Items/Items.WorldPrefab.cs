using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HAModHelper.GamePlugin.Items.Systems;

/// <summary>
/// Allows mods to register custom AssetBundle prefabs for world-placeable items whose
/// <c>World_obj_path</c> is not present in the game's Addressables catalog.
/// <para/>
/// Pass your mod's <see cref="Assembly"/>, the embedded-resource name, and the prefab
/// asset name — HAModHelper loads and caches the bundle internally so the calling mod
/// needs only a single <c>Register</c> call per item.
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
    // keyed by World_obj_path ("Prefabs/Expansion/gem")
    private readonly Dictionary<string, PrefabEntry> _byObjPath = new();
    // loaded bundles keyed by "<assembly-name>:<resourceName>" to avoid double-loading
    private readonly Dictionary<string, AssetBundle> _bundles   = new();

    public void Initialize() { }

    /// <summary>
    /// Register a custom world prefab by providing the embedded-resource bundle directly.
    /// HAModHelper extracts the bundle from <paramref name="assembly"/>, writes it to the
    /// device temp path, and keeps it loaded for the lifetime of the session.
    /// </summary>
    /// <param name="itemFullId">Full mod item id, e.g. <c>"MyMod:MyItem"</c>.</param>
    /// <param name="worldObjPath">The value set in the item's <c>World_obj_path</c> field.</param>
    /// <param name="assembly">The calling assembly that contains the bundle as an embedded resource.</param>
    /// <param name="resourceName">
    ///   Fully-qualified embedded-resource name, e.g. <c>"MyMod.my_prefab.bundle"</c>.
    /// </param>
    /// <param name="assetName">Name of the prefab asset inside the bundle.</param>
    public void Register(string itemFullId, string worldObjPath,
                         Assembly assembly, string resourceName, string assetName)
    {
        var bundle = GetOrLoadBundle(assembly, resourceName);
        if (bundle == null) return;

        var entry = new PrefabEntry { Bundle = bundle, AssetName = assetName };
        _byItemId[itemFullId]    = entry;
        _byObjPath[worldObjPath] = entry;
    }

    /// <summary>
    /// Register a custom world prefab using a pre-loaded <see cref="AssetBundle"/>.
    /// Keep a static reference to the bundle in your plugin so the native backing is
    /// never freed.
    /// </summary>
    public void Register(string itemFullId, string worldObjPath, AssetBundle bundle, string assetName)
    {
        var entry = new PrefabEntry { Bundle = bundle, AssetName = assetName };
        _byItemId[itemFullId]    = entry;
        _byObjPath[worldObjPath] = entry;
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private AssetBundle? GetOrLoadBundle(Assembly assembly, string resourceName)
    {
        var cacheKey = assembly.GetName().Name + ":" + resourceName;
        if (_bundles.TryGetValue(cacheKey, out var cached))
            return cached;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        var bytes = new byte[stream.Length];
        _ = stream.Read(bytes, 0, bytes.Length);

        // Android requires a file on disk; use a name that won't clash across mods.
        var fileName = cacheKey.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
        var tmpPath  = System.IO.Path.Combine(Application.temporaryCachePath, fileName);
        System.IO.File.WriteAllBytes(tmpPath, bytes);

        var bundle = AssetBundle.LoadFromFile(tmpPath);
        if (bundle != null)
            _bundles[cacheKey] = bundle;

        return bundle;
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

        // Instantiate a fresh copy: the game's b__0 callback mutates the received
        // object in-place and calls Object.Destroy on it when the player exits build
        // mode — passing the prefab directly would destroy the source asset.
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
    static MethodBase TargetMethod()
    {
        foreach (var m in typeof(ResourceControl).GetMethods(
            BindingFlags.Public | BindingFlags.Instance))
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
    static MethodBase TargetMethod()
    {
        foreach (var m in typeof(ResourceControl).GetMethods(
            BindingFlags.Public | BindingFlags.Instance))
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

/// <summary>
/// Intercepts <c>ResourceControl.AsyncInstantiateEquipment(string, Action)</c>
/// (the equipment/helmet loading path: SharedCreature → LiteModel.ApplyHat).
/// </summary>
[HarmonyPatch]
internal static class EquipmentPrefabPatch
{
    [HarmonyTargetMethod]
    static MethodBase TargetMethod()
    {
        foreach (var m in typeof(ResourceControl).GetMethods(
            BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.Name != "AsyncInstantiateEquipment") continue;
            var p = m.GetParameters();
            if (p.Length == 2 && p[0].ParameterType == typeof(string))
                return m;
        }
        return null!;
    }

    [HarmonyPrefix]
    static bool Prefix(string equipment_path, Il2CppSystem.Action<GameObject> on_asset_ready)
    {
        if (string.IsNullOrEmpty(equipment_path)) return true;
        return !WorldPrefabManager.Instance.TryInvokeByObjPath(equipment_path, on_asset_ready);
    }
}
