namespace HAModHelper.GamePlugin.Gui.Interfaces;

public interface IPopupControl
{
    bool PopupOpen { get; }

    void ShowMessage(string message);

    void ShowYesNo(string message, string yesLabel, string noLabel, Action? onYes, Action? onNo);

    void HideAll();
}

// runtime adapter that wraps the real game PopupControl
public class UnityPopupControl : IPopupControl
{
    private readonly PopupControl _pc;
    public UnityPopupControl(PopupControl pc) => _pc = pc;

    public bool PopupOpen => _pc.popup_open;

    public void ShowMessage(string message)
    {
        _pc.ShowMessage(message, PopupControl.context.message);
    }

    public void ShowYesNo(string message, string yesLabel, string noLabel, Action? onYes, Action? onNo)
    {
        // PopupControl invokes these fields itself from PressYes()/PressNo() once the
        // player taps a button — there is no callback parameter on ShowYesNo itself.
        _pc.on_yes_pressed = onYes;
        _pc.on_no_pressed = onNo;
        _pc.ShowYesNo(message, yesLabel, noLabel, PopupControl.context.yesno_ACTION);
    }

    public void HideAll()
    {
        _pc.HideAll();
    }
}

public class DebugNoLoadPopupControl : IPopupControl
{
    public bool PopupOpen => false;
    public void ShowMessage(string message) { }
    public void ShowYesNo(string message, string yesLabel, string noLabel, Action? onYes, Action? onNo) { }
    public void HideAll() { }
}

public interface IWindowPrefabsControl
{
    Transform? GuiCanvasTransform { get; }

    GameObject? GetScreen(string screenId);

    GameObject? GetObject(string screenId, string objName);

    bool HasScreen(string screenId);

    /// <summary>Register an already-instantiated screen so the game's own GetScreen/GetObject calls find it too.</summary>
    void RegisterInstantiated(string screenId, GameObject instance);

    void DestroyScreen(string screenId);
}

// runtime adapter that wraps the real game WindowPrefabsControl/WindowControl
public class UnityWindowPrefabsControl : IWindowPrefabsControl
{
    private readonly WindowPrefabsControl _wpc;
    private readonly WindowControl _wc;

    public UnityWindowPrefabsControl(WindowPrefabsControl wpc, WindowControl wc)
    {
        _wpc = wpc;
        _wc = wc;
    }

    public Transform? GuiCanvasTransform => _wc.gui_canvas != null ? _wc.gui_canvas.transform : null;

    public GameObject? GetScreen(string screenId) => _wpc.GetScreen(screenId);

    public GameObject? GetObject(string screenId, string objName) => _wpc.GetObject(screenId, objName);

    public bool HasScreen(string screenId) => _wpc.prefab_screens_instantiated.ContainsKey(screenId);

    public void RegisterInstantiated(string screenId, GameObject instance)
    {
        _wpc.prefab_screens_instantiated[screenId] = instance;
    }

    public void DestroyScreen(string screenId) => _wpc.DestroyScreen(screenId);
}

public class DebugNoLoadWindowPrefabsControl : IWindowPrefabsControl
{
    public Transform? GuiCanvasTransform => null;
    public GameObject? GetScreen(string screenId) => null;
    public GameObject? GetObject(string screenId, string objName) => null;
    public bool HasScreen(string screenId) => false;
    public void RegisterInstantiated(string screenId, GameObject instance) { }
    public void DestroyScreen(string screenId) { }
}
