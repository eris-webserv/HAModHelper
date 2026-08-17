using HAModHelper.GamePlugin.Core;
using HAModHelper.GamePlugin.Base.Events;
using HAModHelper.GamePlugin.Gui.Events;
using HAModHelper.GamePlugin.Gui.Interfaces;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HAModHelper.GamePlugin.Gui.Systems;

/// <summary>
/// Lets mods show the game's own popup UI (message boxes and yes/no confirmations)
/// instead of having to build their own. Thin wrapper around <c>PopupControl</c>.
/// </summary>
public sealed class PopupManager
{
    public static PopupManager Instance { get; } = new PopupManager();

    // TEST-ONLY: Spoof a fake PopupControl for tests
    public IPopupControl? DebugPopupControlSource { get; set; }

    private PopupManager() { }

    private IPopupControl? GetPopupControl()
    {
        if (DebugPopupControlSource?.GetType() == typeof(DebugNoLoadPopupControl))
            return null;

        if (DebugPopupControlSource != null)
        {
            return DebugPopupControlSource;
        }

        try
        {
            var pc = UnityEngine.Object.FindObjectOfType<PopupControl>();
            if (pc == null) return null;
            return new UnityPopupControl(pc);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>TEST-ONLY: Reset system state.</summary>
    public void Reset()
    {
        DebugPopupControlSource = null;
    }

    /// <summary>Initialize the popup manager (called on game start).</summary>
    public void Initialize()
    {
    }

    /// <summary>Show a simple message popup. Does nothing if the popup UI isn't loaded yet.</summary>
    public bool ShowMessage(string message)
    {
        var proxy = GetPopupControl();
        if (proxy == null)
        {
            try { HAMHMod.Logger.LogInfo($"[HAMH] PopupControl not ready, dropping message popup: {message}"); } catch { }
            return false;
        }

        proxy.ShowMessage(message);
        EventBus.Instance.Fire(new PopupShownEvent(message, isYesNo: false));
        return true;
    }

    /// <summary>
    /// Show a yes/no confirmation popup. <paramref name="onYes"/>/<paramref name="onNo"/> are invoked
    /// by the game once the player taps the corresponding button.
    /// </summary>
    public bool ShowYesNo(string message, string yesLabel, string noLabel, Action? onYes = null, Action? onNo = null)
    {
        var proxy = GetPopupControl();
        if (proxy == null)
        {
            try { HAMHMod.Logger.LogInfo($"[HAMH] PopupControl not ready, dropping yes/no popup: {message}"); } catch { }
            return false;
        }

        proxy.ShowYesNo(message, yesLabel, noLabel, onYes, onNo);
        EventBus.Instance.Fire(new PopupShownEvent(message, isYesNo: true));
        return true;
    }

    /// <summary>Hide any currently-open popup.</summary>
    public bool HideAll()
    {
        var proxy = GetPopupControl();
        if (proxy == null) return false;

        proxy.HideAll();
        return true;
    }
}

/// <summary>
/// Lets mods register their own AssetBundle-backed UI screens and show/hide them under
/// the game's main GUI canvas. Registered screens are written into
/// <c>WindowPrefabsControl.prefab_screens_instantiated</c> so the game's own
/// <c>GetScreen</c>/<c>GetObject</c>/<c>DestroyScreen</c> calls keep working on them too —
/// mirrors how <see cref="Items.Systems.WorldPrefabManager"/> injects custom world prefabs.
/// </summary>
public sealed class WindowManager
{
    public static WindowManager Instance { get; } = new();

    private sealed class ScreenEntry
    {
        public required AssetBundle Bundle;
        public required string AssetName;
        public GameObject? Cache;
    }

    private readonly Dictionary<string, ScreenEntry> _screens = new();

    // TEST-ONLY: Spoof a fake WindowPrefabsControl for tests
    public IWindowPrefabsControl? DebugWindowPrefabsControlSource { get; set; }

    private WindowManager() { }

    private IWindowPrefabsControl? GetControl()
    {
        if (DebugWindowPrefabsControlSource?.GetType() == typeof(DebugNoLoadWindowPrefabsControl))
            return null;

        if (DebugWindowPrefabsControlSource != null)
            return DebugWindowPrefabsControlSource;

        try
        {
            var wpc = WindowPrefabsControl.Instance;
            var wc = WindowControl.Instance;
            if (wpc == null || wc == null) return null;
            var proxy = new UnityWindowPrefabsControl(wpc, wc);
            return proxy.GuiCanvasTransform == null ? null : proxy;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>TEST-ONLY: Reset system state.</summary>
    public void Reset()
    {
        _screens.Clear();
        DebugWindowPrefabsControlSource = null;
    }

    /// <summary>Initialize the window manager (called on game start).</summary>
    public void Initialize()
    {
    }

    /// <summary>
    /// Register a custom screen prefab.
    /// </summary>
    /// <param name="screenId">Unique id for the screen, used with <see cref="ShowScreen"/> etc.</param>
    /// <param name="bundle">AssetBundle that contains the prefab. Keep a static reference
    ///   in your plugin so the native backing is never freed.</param>
    /// <param name="assetName">Name of the prefab asset inside the bundle.</param>
    public void RegisterScreen(string screenId, AssetBundle bundle, string assetName)
    {
        _screens[screenId] = new ScreenEntry { Bundle = bundle, AssetName = assetName };
    }

    /// <summary>
    /// Instantiate (first call) or re-show a registered custom screen, parented under the
    /// game's main GUI canvas. Returns null if the screen isn't registered or the game's
    /// window system isn't loaded yet.
    /// </summary>
    public GameObject? ShowScreen(string screenId)
    {
        var control = GetControl();
        if (control == null)
        {
            try { HAMHMod.Logger.LogInfo($"[HAMH] WindowPrefabsControl not ready, can't show screen {screenId}"); } catch { }
            return null;
        }

        var existing = control.GetScreen(screenId);
        if (existing != null)
        {
            existing.SetActive(true);
            EventBus.Instance.Fire(new CustomScreenOpenedEvent(screenId));
            return existing;
        }

        if (!_screens.TryGetValue(screenId, out var entry))
        {
            try { HAMHMod.Logger.LogInfo($"[HAMH] No custom screen registered for {screenId}"); } catch { }
            return null;
        }

        if (entry.Cache == null)
        {
            var asset = entry.Bundle.LoadAsset(entry.AssetName);
            if (asset == null) return null;
            entry.Cache = asset.Cast<GameObject>();
        }

        var instance = Object.Instantiate(entry.Cache, control.GuiCanvasTransform);
        instance.SetActive(true);
        control.RegisterInstantiated(screenId, instance);

        EventBus.Instance.Fire(new CustomScreenOpenedEvent(screenId));
        return instance;
    }

    /// <summary>Hide a shown custom (or base-game) screen without destroying it.</summary>
    public bool HideScreen(string screenId)
    {
        var existing = GetControl()?.GetScreen(screenId);
        if (existing == null) return false;

        existing.SetActive(false);
        EventBus.Instance.Fire(new CustomScreenClosedEvent(screenId));
        return true;
    }

    /// <summary>Destroy a shown custom (or base-game) screen entirely.</summary>
    public bool DestroyScreen(string screenId)
    {
        var control = GetControl();
        if (control == null || !control.HasScreen(screenId)) return false;

        control.DestroyScreen(screenId);
        EventBus.Instance.Fire(new CustomScreenClosedEvent(screenId));
        return true;
    }

    /// <summary>Look up a child object inside a screen (custom or base-game) by name.</summary>
    public GameObject? GetObject(string screenId, string objName) => GetControl()?.GetObject(screenId, objName);
}
