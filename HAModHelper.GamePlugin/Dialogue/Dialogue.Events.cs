namespace HAModHelper.GamePlugin.Dialogue.Events;

/// <summary>Event triggered when a dialogue is entered.</summary>
public class DialogueEnteredEvent : Base.Events.BaseEvent
{
    /// <summary>Gets the display name of the NPC/focus being talked to, if any.</summary>
    public string? NpcDisplayName { get; }

    /// <summary>Gets the dialogue node ID the conversation started at.</summary>
    public int EntryPoint { get; }

    public DialogueEnteredEvent(string? npcDisplayName, int entryPoint)
    {
        NpcDisplayName = npcDisplayName;
        EntryPoint = entryPoint;
    }
}

/// <summary>Event triggered when the active dialogue is exited.</summary>
public class DialogueExitedEvent : Base.Events.BaseEvent
{
}

/// <summary>Event triggered when the player picks one of the two dialogue options.</summary>
public class DialogueOptionSelectedEvent : Base.Events.BaseEvent
{
    /// <summary>Gets whether option A (as opposed to option B) was selected.</summary>
    public bool WasOptionA { get; }

    public DialogueOptionSelectedEvent(bool wasOptionA)
    {
        WasOptionA = wasOptionA;
    }
}
