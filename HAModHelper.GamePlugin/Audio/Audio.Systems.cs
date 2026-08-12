using HAModHelper.GamePlugin.Core;
using HAModHelper.GamePlugin.Audio.Interfaces;
using UnityEngine;

namespace HAModHelper.GamePlugin.Audio.Systems;

/// <summary>
/// Identifies one of the four exploration-music mood pools (<c>explore_music_QUIRKY</c>,
/// <c>_JUNGLEY</c>, <c>_TWINKLEY</c>, <c>_SERIOUS</c>) that <c>AudioControl</c> rotates through
/// while the player is out exploring.
/// </summary>
public enum ExploreMusicMood
{
    Quirky,
    Jungley,
    Twinkley,
    Serious
}

/// <summary>
/// Lets mods play sound effects and dialogue, control battle/background music, and register
/// custom tracks into the exploration music rotation. Thin wrapper around <c>AudioControl</c>.
/// </summary>
public sealed class AudioManager
{
    public static AudioManager Instance { get; } = new AudioManager();

    // TEST-ONLY: Spoof a fake AudioControl for tests
    public IAudioControl? DebugAudioControlSource { get; set; }

    private readonly Dictionary<ExploreMusicMood, List<string>> _customTracks = new();

    private AudioManager() { }

    private IAudioControl? GetAudioControl()
    {
        if (DebugAudioControlSource?.GetType() == typeof(DebugNoLoadAudioControl))
            return null;

        if (DebugAudioControlSource != null)
        {
            return DebugAudioControlSource;
        }

        try
        {
            var ac = AudioControl.Instance;
            if (ac == null) return null;
            return new UnityAudioControl(ac);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>TEST-ONLY: Reset system state.</summary>
    public void Reset()
    {
        DebugAudioControlSource = null;
        _customTracks.Clear();
    }

    /// <summary>Initialize the audio manager (called on game start).</summary>
    public void Initialize()
    {
    }

    /// <summary>Gets the current background music state (e.g. <c>"intro"</c>, <c>"menu"</c>, <c>"explore"</c>), or <c>null</c> if the audio control isn't loaded yet.</summary>
    public string? MusicState => GetAudioControl()?.MusicState;

    /// <summary>Play a one-shot sound effect clip. Does nothing if the audio control isn't loaded yet.</summary>
    public bool Play(AudioClip clip, float volume = 1f)
    {
        var proxy = GetAudioControl();
        if (proxy == null)
        {
            try { HAMHMod.Logger.LogInfo($"[HAMH] AudioControl not ready, dropping Play for {clip?.name}"); } catch { }
            return false;
        }

        proxy.Play(clip, volume);
        return true;
    }

    /// <summary>Play a one-shot sound effect clip at a specific pitch. Does nothing if the audio control isn't loaded yet.</summary>
    public bool PlayPitch(AudioClip clip, float pitch, float vol = 1f)
    {
        var proxy = GetAudioControl();
        if (proxy == null)
        {
            try { HAMHMod.Logger.LogInfo($"[HAMH] AudioControl not ready, dropping PlayPitch for {clip?.name}"); } catch { }
            return false;
        }

        proxy.PlayPitch(clip, pitch, vol);
        return true;
    }

    /// <summary>Play the game's generic UI click sound. Does nothing if the audio control isn't loaded yet.</summary>
    public bool PlayGenericClick()
    {
        var proxy = GetAudioControl();
        if (proxy == null) return false;

        proxy.PlayGenericClick();
        return true;
    }

    /// <summary>Play a voice line through a freshly instantiated dialogue prefab. Does nothing if the audio control isn't loaded yet.</summary>
    public bool PlayDialogue(string voice, float delay)
    {
        var proxy = GetAudioControl();
        if (proxy == null)
        {
            try { HAMHMod.Logger.LogInfo($"[HAMH] AudioControl not ready, dropping dialogue: {voice}"); } catch { }
            return false;
        }

        proxy.PlayDialogue(voice, delay);
        return true;
    }

    /// <summary>
    /// Start battle music. The game itself no-ops this if battle music is already playing --
    /// see <see cref="Events.BattleMusicStartedEvent"/>.
    /// </summary>
    public bool PlayBattleMusic(string trackName)
    {
        var proxy = GetAudioControl();
        if (proxy == null)
        {
            try { HAMHMod.Logger.LogInfo($"[HAMH] AudioControl not ready, dropping battle music: {trackName}"); } catch { }
            return false;
        }

        proxy.PlayBattleMusic(trackName);
        return true;
    }

    /// <summary>Stop battle music and resume background music.</summary>
    public bool EndBattleMusic()
    {
        var proxy = GetAudioControl();
        if (proxy == null) return false;

        proxy.EndBattleMusic();
        return true;
    }

    /// <summary>Stop all currently playing music sources.</summary>
    public bool StopAllMusic()
    {
        var proxy = GetAudioControl();
        if (proxy == null) return false;

        proxy.StopAllMusic();
        return true;
    }

    /// <summary>Pause the background music source without clearing its clip.</summary>
    public bool PauseBackgroundMusic()
    {
        var proxy = GetAudioControl();
        if (proxy == null) return false;

        proxy.PauseBackgroundMusic();
        return true;
    }

    /// <summary>
    /// Attempt to resume background music. Unless <paramref name="overrideSuccess"/> is set,
    /// the game itself skips the resume while a music box song or battle music is active.
    /// </summary>
    public bool TryResumeGameMusic(bool overrideSuccess = false)
    {
        var proxy = GetAudioControl();
        if (proxy == null) return false;

        proxy.TryResumeGameMusic(overrideSuccess);
        return true;
    }

    /// <summary>Set the pitch of the background music source.</summary>
    public bool SetMusicPitch(float pitch)
    {
        var proxy = GetAudioControl();
        if (proxy == null) return false;

        proxy.SetMusicPitch(pitch);
        return true;
    }

    // ---------- exploration music registration ----------

    /// <summary>
    /// Registers a track name to be added into an exploration mood pool the next time
    /// <c>AudioControl</c> starts, so it can be picked by the game's own rotation logic.
    /// </summary>
    /// <param name="mood">Target exploration mood pool.</param>
    /// <param name="trackName">Track name, as looked up by <c>ResourceControl.PlayExploreMusic</c>.</param>
    public void RegisterExploreTrack(ExploreMusicMood mood, string trackName)
    {
        if (!_customTracks.TryGetValue(mood, out var list))
        {
            list = new List<string>();
            _customTracks[mood] = list;
        }
        list.Add(trackName);
    }

    /// <summary>Gets the track names registered for a given exploration mood pool.</summary>
    public IReadOnlyList<string> GetCustomTracks(ExploreMusicMood mood)
        => _customTracks.TryGetValue(mood, out var list) ? list : Array.Empty<string>();
}
