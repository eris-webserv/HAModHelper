using HAModHelper.GamePlugin.Base.Events;

namespace HAModHelper.GamePlugin.Gui.Events;

public class PopupShownEvent : BaseEvent
{
    public string Message { get; }
    public bool IsYesNo { get; }

    public PopupShownEvent(string message, bool isYesNo)
    {
        Message = message;
        IsYesNo = isYesNo;
    }
}
