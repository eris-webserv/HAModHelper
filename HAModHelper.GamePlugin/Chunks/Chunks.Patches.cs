using HarmonyLib;
using HAModHelper.GamePlugin.Chunks.Systems;
using System;
using System.Collections.Generic;
using HAModHelper.GamePlugin.Chunks.Events;

namespace HAModHelper.GamePlugin.Chunks.Patches;

/// <summary>
/// Injects custom registered biome objects into the overworld biome configuration on game start.
/// </summary>
[HarmonyPatch(typeof(ChunkControl), "Start")]
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
[HarmonyPatch(typeof(ChunkControl), "HostGetChunk")]
public static class ChunkControl_HostGetChunk_Patch
{
    /// <summary>
    /// Postfix patch triggered when a chunk is retrieved or loaded by the host.
    /// </summary>
    [HarmonyPostfix]
    public static void Postfix(string zone, int X, int Z, Chunk __result)
    {
        if (__result != null)
        {
            // Trigger custom event for modders when a chunk completes loading/fetching
            var evt = new ChunkLoadedEvent(zone, X, Z, __result);
            // Dispatch via your framework's event manager
        }
    }
}