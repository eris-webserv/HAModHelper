using HarmonyLib;
using System;
using HAModHelper.GamePlugin.Audio.Systems;
using HAModHelper.GamePlugin.Audio.Events;
using HAModHelper.GamePlugin.Base.Events;

namespace HAModHelper.GamePlugin.Audio.Patches;

/// <summary>
/// Injects mod-registered tracks into the exploration music mood pools on game start.
/// </summary>
/// <remarks>
/// AudioControl doesn't use Unity's Start() -- like ChunkControl/PopupControl it implements the
/// game's custom OrderedStart interface instead (Start_0()/Start_1()). The mood pool arrays are
/// already populated from serialized data by the time Start_1 runs, so patching after it lets us
/// safely append without clobbering the vanilla lists.
/// </remarks>
[HarmonyPatch(typeof(AudioControl), "Start_1")]
public static class AudioControl_Start_Patch
{
    [HarmonyPostfix]
    public static void Postfix(AudioControl __instance)
    {
        __instance.explore_music_QUIRKY = AppendCustomTracks(__instance.explore_music_QUIRKY, ExploreMusicMood.Quirky);
        __instance.explore_music_JUNGLEY = AppendCustomTracks(__instance.explore_music_JUNGLEY, ExploreMusicMood.Jungley);
        __instance.explore_music_TWINKLEY = AppendCustomTracks(__instance.explore_music_TWINKLEY, ExploreMusicMood.Twinkley);
        __instance.explore_music_SERIOUS = AppendCustomTracks(__instance.explore_music_SERIOUS, ExploreMusicMood.Serious);
    }

    private static string[] AppendCustomTracks(string[]? pool, ExploreMusicMood mood)
    {
        var custom = AudioManager.Instance.GetCustomTracks(mood);
        if (custom.Count == 0) return pool ?? Array.Empty<string>();

        var original = pool ?? Array.Empty<string>();
        var expanded = new string[original.Length + custom.Count];
        Array.Copy(original, expanded, original.Length);
        for (int i = 0; i < custom.Count; i++)
        {
            expanded[original.Length + i] = custom[i];
        }

        return expanded;
    }
}

/// <summary>
/// Fires <see cref="MusicTrackSelectedEvent"/> whenever the game picks a track to play next.
/// </summary>
[HarmonyPatch(typeof(AudioControl), nameof(AudioControl.PickSong), new Type[] { typeof(string[]) })]
public static class AudioControl_PickSong_Patch
{
    [HarmonyPostfix]
    public static void Postfix(string __result)
    {
        if (!string.IsNullOrEmpty(__result))
        {
            EventBus.Instance.Fire(new MusicTrackSelectedEvent(__result));
        }
    }
}

/// <summary>
/// Fires <see cref="BattleMusicStartedEvent"/> whenever battle music is requested to start.
/// </summary>
[HarmonyPatch(typeof(AudioControl), nameof(AudioControl.PlayBattleMusic), new Type[] { typeof(string) })]
public static class AudioControl_PlayBattleMusic_Patch
{
    [HarmonyPostfix]
    public static void Postfix(string track_name)
    {
        EventBus.Instance.Fire(new BattleMusicStartedEvent(track_name));
    }
}

/// <summary>
/// Fires <see cref="BattleMusicEndedEvent"/> whenever battle music is requested to end.
/// </summary>
[HarmonyPatch(typeof(AudioControl), nameof(AudioControl.EndBattleMusic))]
public static class AudioControl_EndBattleMusic_Patch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        EventBus.Instance.Fire(new BattleMusicEndedEvent());
    }
}
