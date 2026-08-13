namespace HAModHelper.GamePlugin.Entities.Events;

/// <summary>
/// Event triggered when a registered companion ability actually fires.
/// </summary>
public class AbilityTriggeredEvent : Base.Events.BaseEvent
{
    /// <summary>Gets the ID of the ability that was triggered.</summary>
    public string AbilityId { get; }

    /// <summary>Gets the companion the ability was triggered on.</summary>
    public ActiveCompanion Companion { get; }

    public AbilityTriggeredEvent(string abilityId, ActiveCompanion companion)
    {
        AbilityId = abilityId;
        Companion = companion;
    }
}
