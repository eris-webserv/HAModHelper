namespace HAModHelper.GamePlugin.Audio.Events;

/// <summary>
/// Event triggered whenever the game picks a music track to play next, whether from the
/// exploration mood rotation or any other caller of <c>AudioControl.PickSong</c>.
/// </summary>
public class MusicTrackSelectedEvent : Base.Events.BaseEvent
{
    /// <summary>Gets the name of the track that was selected.</summary>
    public string TrackName { get; }

    public MusicTrackSelectedEvent(string trackName)
    {
        TrackName = trackName;
    }
}

/// <summary>
/// Event triggered when battle music is requested to start via <c>AudioControl.PlayBattleMusic</c>.
/// </summary>
/// <remarks>
/// PlayBattleMusic is a no-op on the game's side if battle music is already playing, so mods
/// should not assume every event corresponds to an actual audible transition.
/// </remarks>
public class BattleMusicStartedEvent : Base.Events.BaseEvent
{
    /// <summary>Gets the name of the requested battle music track.</summary>
    public string TrackName { get; }

    public BattleMusicStartedEvent(string trackName)
    {
        TrackName = trackName;
    }
}

/// <summary>
/// Event triggered when battle music is requested to end via <c>AudioControl.EndBattleMusic</c>.
/// </summary>
public class BattleMusicEndedEvent : Base.Events.BaseEvent
{
}
