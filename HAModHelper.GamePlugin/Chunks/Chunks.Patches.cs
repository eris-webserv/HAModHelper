using HarmonyLib;
using HAModHelper.GamePlugin.Chunks.Systems;
using System;
using System.Collections.Generic;
using HAModHelper.GamePlugin.Chunks.Events;
using HAModHelper.GamePlugin.Base.Events;

namespace HAModHelper.GamePlugin.Chunks.Patches;

/// <summary>
/// Injects custom registered biome objects into the overworld biome configuration on game start.
/// </summary>
/// <remarks>
/// ChunkControl doesn't use Unity's Start() -- it implements the game's custom OrderedStart
/// interface instead (public Start_0()/Start_1(), called by an ordered-init system rather than
/// Unity's message pump). biomes/biome_scenic is serialized scene data already populated before
/// either fires, so either hook works; Start_1 is used here since it's the later of the two.
/// </remarks>
[HarmonyPatch(typeof(ChunkControl), "Start_1")]
public static class ChunkControl_Start_Patch
{
    /// <summary>
    /// Postfix patch that expands the biome scenic spawn arrays with mod-registered items.
    /// </summary>
    [HarmonyPostfix]
    public static void Postfix(ChunkControl __instance)
    {
        if (__instance.biomes == null) return;

        var manager = ChunkManager.Instance;

        for (int i = 0; i < __instance.biomes.Length; i++)
        {
            if (manager.CustomBiomeObjects.TryGetValue(i, out var customList) && customList.Count > 0)
            {
                // Retrieve original scenic list
                var originalScenic = __instance.biomes[i].biome_scenic ?? new ChunkControl.biome_obj[0];
                
                // Create a new expanded array
                var newScenic = new ChunkControl.biome_obj[originalScenic.Length + customList.Count];
                Array.Copy(originalScenic, newScenic, originalScenic.Length);
                
                // Append custom objects
                for (int j = 0; j < customList.Count; j++)
                {
                    newScenic[originalScenic.Length + j] = customList[j];
                }
                
                // Reassign back to the struct array
                __instance.biomes[i].biome_scenic = newScenic;
            }
        }
    }
}

/// <summary>
/// Hooks chunk loading to trigger events for modders when chunks become available.
/// </summary>
/// <remarks>
/// ChunkControl.HostGetChunk's real signature is
/// <c>ChunkData HostGetChunk(string zone, int chunkX, int chunkZ)</c> -- the postfix's
/// pass-through parameters must be named exactly chunkX/chunkZ (Harmony binds pass-through
/// parameters by name) and __result must be typed ChunkData, not Chunk; the mismatch on both
/// made Harmony's IL patcher throw "Parameter X not found" and abort PatchAll partway through.
/// </remarks>
[HarmonyPatch(typeof(ChunkControl), "HostGetChunk")]
public static class ChunkControl_HostGetChunk_Patch
{
    /// <summary>
    /// Postfix patch triggered when a chunk is retrieved or loaded by the host.
    /// </summary>
    [HarmonyPostfix]
    public static void Postfix(string zone, int chunkX, int chunkZ, ChunkData __result)
    {
        if (__result != null)
        {
            EventBus.Instance.Fire(new ChunkLoadedEvent(zone, chunkX, chunkZ, __result));
        }
    }
}