using System.IO;
using System.Reflection;
using HAModHelper.GamePlugin.Core;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HAModHelper.GamePlugin.Assets.Systems;

/// <summary>
/// Loads prefabs and sprites from a plain (non-Addressables) <c>AssetBundle</c> and feeds them into
/// Hybrid Animals' asset loaders.
///
/// <para>
/// Every runtime asset in the game is streamed through <c>ResourceControl.GenericTryLoad&lt;T,C&gt;</c>,
/// which asks Unity Addressables for an asset by its full project path (e.g.
/// <c>Assets/Prefabs/MyItem.prefab</c>). A plain AssetBundle is invisible to Addressables, so this
/// manager intercepts the concrete loader entry points and, when the requested key belongs to a
/// registered bundle, serves the asset from that bundle instead.
/// </para>
///
/// <para>
/// This covers held items / hats / armor (<c>AsyncInstantiateEquipment</c>), furniture
/// (<c>AsyncInstantiateHouseInterior</c>), placed/dropped world objects and 3D inventory-icon
/// previews (<c>AsyncInstantiateWorldObjectPrefab</c>), and 2D inventory icons
/// (<c>LoadAndAssignSprite</c>).
/// </para>
///
/// <para>
/// <b>Why bytes instead of an <see cref="AssetBundle"/>?</b> This il2cpp build has the synchronous
/// AssetBundle APIs (<c>LoadFromMemory</c>, sync <c>LoadAsset</c>, <c>GetAllAssetNames</c>) stripped.
/// Only the async APIs survive, so the manager stages your bundle to disk, loads it with
/// <c>LoadFromFileAsync</c>, and pre-loads assets with <c>LoadAssetAsync</c> off
/// <c>AsyncOperation.completed</c>. You only have to hand over the raw bytes.
/// </para>
///
/// <example>
/// <code>
/// AssetBundleManager.Instance
///     .RegisterBundleFromEmbeddedResource(Assembly.GetExecutingAssembly(), "MyMod.Bundles.mybundle")
///     .AddPrefab("Assets/Prefabs/MyItem.prefab", "MyMod:MyItem");
/// </code>
/// </example>
/// </summary>
public sealed class AssetBundleManager
{
    public static AssetBundleManager Instance { get; } = new();
    private AssetBundleManager() { }

    private readonly Dictionary<string, ModBundle> _bundles = new(StringComparer.OrdinalIgnoreCase);

    // Global routing tables shared by every registered bundle.
    private readonly Dictionary<string, ModBundle> _prefabKeyToBundle = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ModBundle> _spriteKeyToBundle = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _itemNameToPrefabKey = new(StringComparer.OrdinalIgnoreCase);

    // Roots il2cpp delegates handed to native code so the GC can't collect them mid-flight.
    private readonly List<Il2CppSystem.Object> _keepAlive = new();

    public void Initialize() { }

    // ── Public registration API ───────────────────────────────────────────────

    /// <summary>
    /// Registers an AssetBundle from its raw bytes and returns a handle for declaring its contents.
    /// The bundle is staged to disk and loaded asynchronously in the background. Registering the
    /// same <paramref name="bundleId"/> twice returns the existing registration.
    /// </summary>
    public ModBundle RegisterBundle(byte[] bundleBytes, string bundleId)
    {
        if (_bundles.TryGetValue(bundleId, out var existing))
            return existing;

        var bundle = new ModBundle(this, bundleId);
        _bundles[bundleId] = bundle;
        bundle.BeginLoad(bundleBytes);
        return bundle;
    }

    /// <summary>
    /// Convenience overload that reads the bundle out of an embedded resource. If
    /// <paramref name="bundleId"/> is omitted the resource name is used.
    /// </summary>
    public ModBundle RegisterBundleFromEmbeddedResource(Assembly assembly, string resourceName, string? bundleId = null)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded bundle resource '{resourceName}' not found in {assembly.GetName().Name}.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return RegisterBundle(ms.ToArray(), bundleId ?? resourceName);
    }

    // ── Internal wiring used by ModBundle / patches ───────────────────────────

    internal void MapPrefabKey(string key, ModBundle bundle, IEnumerable<string> itemIds)
    {
        _prefabKeyToBundle[key] = bundle;
        foreach (var id in itemIds)
            if (!string.IsNullOrEmpty(id))
                _itemNameToPrefabKey[id] = key;
    }

    internal void MapSpriteKey(string key, ModBundle bundle, IEnumerable<string> itemIds)
    {
        _spriteKeyToBundle[key] = bundle;
        foreach (var id in itemIds)
            if (!string.IsNullOrEmpty(id))
                _itemNameToPrefabKey[id] = key; // also lets icon paths resolve by item name
    }

    internal void KeepAlive(Il2CppSystem.Object il2cppDelegate) => _keepAlive.Add(il2cppDelegate);

    // ── Lookups used by the Harmony patches ───────────────────────────────────

    internal bool IsPrefabKey(string key) => _prefabKeyToBundle.ContainsKey(key);
    internal bool IsSpriteKey(string key) => _spriteKeyToBundle.ContainsKey(key);

    internal bool TryGetKeyForItemName(string? itemName, out string key)
    {
        key = string.Empty;
        return itemName != null && _itemNameToPrefabKey.TryGetValue(itemName, out key!);
    }

    /// <summary>
    /// Instantiates the prefab for <paramref name="key"/> and hands the fresh instance to
    /// <paramref name="callback"/>, matching the game's contract (its own loader instantiates the
    /// loaded prefab before invoking the callback). Returns false if the key is not ours.
    /// </summary>
    internal bool TryServePrefab(string key, Il2CppSystem.Action<GameObject>? callback)
    {
        if (!_prefabKeyToBundle.TryGetValue(key, out var bundle))
            return false;

        bundle.InstantiatePrefabAsync(key, callback);
        return true;
    }

    /// <summary>Loads a sprite for <paramref name="key"/>, assigns it to <paramref name="img"/>, then fires <paramref name="callback"/>.</summary>
    internal bool TryServeSprite(string key, Image? img, Il2CppSystem.Action? callback)
    {
        if (!_spriteKeyToBundle.TryGetValue(key, out var bundle))
            return false;

        bundle.AssignSpriteAsync(key, img, callback);
        return true;
    }

    internal static void LogInfo(string msg)
    {
        try { HAMHMod.Logger.LogInfo($"[HAMH/assets] {msg}"); } catch { }
    }

    internal static void LogError(string msg)
    {
        try { HAMHMod.Logger.LogError($"[HAMH/assets] {msg}"); } catch { }
    }
}

/// <summary>
/// A single registered AssetBundle. Use the fluent <see cref="AddPrefab"/> / <see cref="AddSprite"/>
/// methods to declare which assets it provides and (optionally) which item ids they belong to.
/// </summary>
public sealed class ModBundle
{
    private readonly AssetBundleManager _owner;
    private readonly string _id;

    private readonly Dictionary<string, GameObject> _prefabCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _prefabKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string key, Il2CppSystem.Action<GameObject>? cb)> _pendingPrefabs = new();

    private AssetBundle? _bundle;
    private bool _failed;

    internal ModBundle(AssetBundleManager owner, string id)
    {
        _owner = owner;
        _id = id;
    }

    /// <summary>
    /// Declares a prefab this bundle provides, keyed by its full asset path (the same string the
    /// game asks Addressables for, e.g. <c>Assets/Prefabs/MyItem.prefab</c>). Pass the item id(s)
    /// it belongs to so 3D-icon previews and world-object placement (which work off the
    /// <c>InventoryItem</c>) can match it by name as well as by key.
    /// </summary>
    public ModBundle AddPrefab(string assetPath, params string[] itemIds)
    {
        _prefabKeys.Add(assetPath);
        _owner.MapPrefabKey(assetPath, this, itemIds);
        if (_bundle != null)
            LoadPrefabThen(assetPath, null); // bundle already loaded — preload now
        return this;
    }

    /// <summary>
    /// Declares a sprite this bundle provides, keyed by its full asset path (e.g.
    /// <c>Assets/Images/InventorySprites/MyIcon.psd</c>). Sprites are loaded on demand.
    /// </summary>
    public ModBundle AddSprite(string assetPath, params string[] itemIds)
    {
        _owner.MapSpriteKey(assetPath, this, itemIds);
        return this;
    }

    // ── Loading ───────────────────────────────────────────────────────────────

    internal void BeginLoad(byte[] bytes)
    {
        string file;
        try
        {
            file = StageToDisk(bytes);
        }
        catch (Exception ex)
        {
            AssetBundleManager.LogError($"bundle '{_id}': failed to stage to disk: {ex}");
            _failed = true;
            return;
        }

        AssetBundleManager.LogInfo($"bundle '{_id}': loading from {file}");
        var req = AssetBundle.LoadFromFileAsync(file);
        HookCompleted(req, _ =>
        {
            _bundle = req.assetBundle;
            if (_bundle == null)
            {
                AssetBundleManager.LogError($"bundle '{_id}': LoadFromFileAsync returned null");
                _failed = true;
                return;
            }

            AssetBundleManager.LogInfo($"bundle '{_id}': loaded; preloading {_prefabKeys.Count} prefab(s)");
            foreach (var key in _prefabKeys)
                LoadPrefabThen(key, null);

            var pending = _pendingPrefabs.ToArray();
            _pendingPrefabs.Clear();
            foreach (var (key, cb) in pending)
                InstantiatePrefabAsync(key, cb);
        });
    }

    internal void InstantiatePrefabAsync(string key, Il2CppSystem.Action<GameObject>? cb)
    {
        if (_prefabCache.TryGetValue(key, out var cached) && cached != null)
        {
            InvokeWithInstance(cached, cb);
            return;
        }

        if (_failed)
            return;

        if (_bundle == null)
        {
            _pendingPrefabs.Add((key, cb)); // bundle still loading; flushed on completion
            return;
        }

        LoadPrefabThen(key, prefab =>
        {
            if (prefab != null)
                InvokeWithInstance(prefab, cb);
        });
    }

    internal void AssignSpriteAsync(string key, Image? img, Il2CppSystem.Action? cb)
    {
        if (_failed || _bundle == null)
            return;

        var ar = _bundle.LoadAssetAsync(key, Il2CppType.Of<Sprite>());
        HookCompleted(ar, _ =>
        {
            var asset = ar.asset;
            if (asset == null)
            {
                AssetBundleManager.LogError($"bundle '{_id}': sprite '{key}' not found");
                return;
            }
            if (img != null)
            {
                img.sprite = asset.Cast<Sprite>();
                img.enabled = true;
            }
            cb?.Invoke();
        });
    }

    private void LoadPrefabThen(string key, Action<GameObject?>? onReady)
    {
        var ar = _bundle!.LoadAssetAsync(key, Il2CppType.Of<GameObject>());
        HookCompleted(ar, _ =>
        {
            var asset = ar.asset;
            if (asset == null)
            {
                AssetBundleManager.LogError($"bundle '{_id}': prefab '{key}' not found");
                onReady?.Invoke(null);
                return;
            }
            var go = asset.Cast<GameObject>();
            _prefabCache[key] = go;
            AssetBundleManager.LogInfo($"bundle '{_id}': prefab ready '{key}'");
            onReady?.Invoke(go);
        });
    }

    private static void InvokeWithInstance(GameObject prefab, Il2CppSystem.Action<GameObject>? cb)
    {
        // Fresh instance: the game's callbacks parent/mutate/destroy what they receive, so handing
        // over the prefab directly would corrupt the cached source asset.
        var instance = Object.Instantiate(prefab).Cast<GameObject>();
        cb?.Invoke(instance);
    }

    private void HookCompleted(AsyncOperation op, Action<AsyncOperation> cb)
    {
        if (op.isDone)
        {
            cb(op);
            return;
        }
        var il2 = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<AsyncOperation>>(cb);
        _owner.KeepAlive(il2!);
        op.add_completed(il2);
    }

    private string StageToDisk(byte[] bytes)
    {
        string dir;
        try { dir = Path.Combine(BepInEx.Paths.CachePath, "HAModHelper", "bundles"); }
        catch { dir = Path.Combine(Path.GetTempPath(), "HAModHelper", "bundles"); }
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, _id + ".bundle");
        File.WriteAllBytes(file, bytes);
        return file;
    }
}
