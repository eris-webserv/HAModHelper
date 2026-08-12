using UnityEngine;

namespace HAModHelper.GamePlugin.Audio.Interfaces;

/// <summary>
/// Defines public operations for playing sound effects, dialogue, and music through the game's audio system.
/// </summary>
public interface IAudioControl
{
    /// <summary>Gets the current background music state (e.g. <c>"intro"</c>, <c>"menu"</c>, <c>"explore"</c>).</summary>
    string MusicState { get; }

    /// <summary>Plays a one-shot sound effect clip.</summary>
    void Play(AudioClip clip, float volume = 1f);

    /// <summary>Plays a one-shot sound effect clip at a specific pitch.</summary>
    void PlayPitch(AudioClip clip, float pitch, float vol = 1f);

    /// <summary>Plays the game's generic UI click sound.</summary>
    void PlayGenericClick();

    /// <summary>Plays a voice line through a freshly instantiated dialogue prefab.</summary>
    void PlayDialogue(string voice, float delay);

    /// <summary>Starts battle music on the dedicated custom-music source, pausing background music underneath it.</summary>
    void PlayBattleMusic(string trackName);

    /// <summary>Stops battle music and resumes background music.</summary>
    void EndBattleMusic();

    /// <summary>Stops all currently playing music sources.</summary>
    void StopAllMusic();

    /// <summary>Pauses the background music source without clearing its clip.</summary>
    void PauseBackgroundMusic();

    /// <summary>Attempts to resume background music. See remarks on <c>AudioControl.TryResumeGameMusic</c> for gating behavior.</summary>
    void TryResumeGameMusic(bool overrideSuccess = false);

    /// <summary>Sets the pitch of the background music source.</summary>
    void SetMusicPitch(float pitch);
}

// runtime adapter that wraps the real game AudioControl
public class UnityAudioControl : IAudioControl
{
    private readonly AudioControl _ac;
    public UnityAudioControl(AudioControl ac) => _ac = ac;

    public string MusicState => _ac.music;

    public void Play(AudioClip clip, float volume = 1f) => _ac.Play(clip, volume);

    public void PlayPitch(AudioClip clip, float pitch, float vol = 1f) => _ac.PlayPitch(clip, pitch, vol);

    public void PlayGenericClick() => _ac.PlayGenericClick();

    public void PlayDialogue(string voice, float delay) => _ac.PlayDialogue(voice, delay);

    public void PlayBattleMusic(string trackName) => _ac.PlayBattleMusic(trackName);

    public void EndBattleMusic() => _ac.EndBattleMusic();

    public void StopAllMusic() => _ac.StopAllMusic();

    public void PauseBackgroundMusic() => _ac.PauseBackgroundMusic();

    public void TryResumeGameMusic(bool overrideSuccess = false) => _ac.TryResumeGameMusic(overrideSuccess);

    public void SetMusicPitch(float pitch) => _ac.SetMusicPitch(pitch);
}

public class DebugNoLoadAudioControl : IAudioControl
{
    public string MusicState => "";
    public void Play(AudioClip clip, float volume = 1f) { }
    public void PlayPitch(AudioClip clip, float pitch, float vol = 1f) { }
    public void PlayGenericClick() { }
    public void PlayDialogue(string voice, float delay) { }
    public void PlayBattleMusic(string trackName) { }
    public void EndBattleMusic() { }
    public void StopAllMusic() { }
    public void PauseBackgroundMusic() { }
    public void TryResumeGameMusic(bool overrideSuccess = false) { }
    public void SetMusicPitch(float pitch) { }
}
