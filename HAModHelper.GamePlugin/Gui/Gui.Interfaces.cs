using UnityEngine;

namespace HAModHelper.GamePlugin.Gui.Interfaces;

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
