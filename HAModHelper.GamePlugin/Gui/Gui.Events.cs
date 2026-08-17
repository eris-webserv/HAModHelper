using HAModHelper.GamePlugin.Base.Events;

namespace HAModHelper.GamePlugin.Gui.Events;

public class CustomScreenOpenedEvent : BaseEvent
{
    public string ScreenId { get; }
    public CustomScreenOpenedEvent(string screenId) => ScreenId = screenId;
}

public class CustomScreenClosedEvent : BaseEvent
{
    public string ScreenId { get; }
    public CustomScreenClosedEvent(string screenId) => ScreenId = screenId;
}
