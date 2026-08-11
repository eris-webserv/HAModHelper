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
