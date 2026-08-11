using HAModHelper.GamePlugin.Core;
using HAModHelper.GamePlugin.Gui.Events;
using HAModHelper.GamePlugin.Gui.Interfaces;
using HAModHelper.GamePlugin.Base.Events;

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
