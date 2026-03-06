using HAModHelper.GamePlugin.Base.Events;

namespace HAModHelper.GamePlugin.Perks.Events;
public class PerkCastEvent : BaseEvent
{
    public string PerkId { get; }
    public PerkCastEvent(string perkId)
    {
        PerkId = perkId;
    }
}