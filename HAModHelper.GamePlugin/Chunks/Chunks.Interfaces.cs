using System.Collections.Generic;
using UnityEngine;

namespace HAModHelper.GamePlugin.Chunks.Interfaces;

/// <summary>
/// Defines public operations for chunk querying, terrain modification, and biome object registration.
/// </summary>
public interface IChunkManager
{
    /// <summary>
    /// Retrieves an actively loaded chunk by its zone and chunk coordinates.
    /// </summary>
    /// <param name="zone">The world zone identifier.</param>
    /// <param name="chunkX">The chunk X coordinate.</param>
    /// <param name="chunkZ">The chunk Z coordinate.</param>
    /// <returns>The active <see cref="Chunk"/> instance if loaded; otherwise, <c>null</c>.</returns>
    Chunk? GetActiveChunk(string zone, int chunkX, int chunkZ);

    /// <summary>
    /// Attempts to place or replace an object at a specific sub-grid tile inside a chunk.
    /// </summary>
    /// <param name="zone">The world zone identifier.</param>
    /// <param name="chunkX">The target chunk X coordinate.</param>
    /// <param name="chunkZ">The target chunk Z coordinate.</param>
    /// <param name="gridX">The sub-grid tile X coordinate (0-9).</param>
    /// <param name="gridZ">The sub-grid tile Z coordinate (0-9).</param>
    /// <param name="newItemName">The item/object registry name to instantiate.</param>
    /// <returns><c>true</c> if the object was successfully placed; otherwise, <c>false</c>.</returns>
    bool TryPlaceObject(string zone, int chunkX, int chunkZ, int gridX, int gridZ, string newItemName);

    /// <summary>
    /// Removes a buildable or world object from a specific tile in a chunk.
    /// </summary>
    /// <param name="zone">The world zone identifier.</param>
    /// <param name="chunkX">The target chunk X coordinate.</param>
    /// <param name="chunkZ">The target chunk Z coordinate.</param>
    /// <param name="gridX">The sub-grid tile X coordinate.</param>
    /// <param name="gridZ">The sub-grid tile Z coordinate.</param>
    /// <returns><c>true</c> if the object was removed; otherwise, <c>false</c>.</returns>
    bool RemoveObject(string zone, int chunkX, int chunkZ, int gridX, int gridZ);

    /// <summary>
    /// Registers a custom object to be naturally generated within a biome's spawn pool.
    /// </summary>
    /// <param name="biomeIndex">The numerical ID of the biome.</param>
    /// <param name="obj">The biome object definition containing spawn rate and item information.</param>
    void RegisterBiomeObject(int biomeIndex, ChunkControl.biome_obj obj);

    /// <summary>
    /// Checks whether a specific sub-grid tile within a chunk is currently unoccupied.
    /// </summary>
    /// <param name="zone">The world zone identifier.</param>
    /// <param name="chunkX">The target chunk X coordinate.</param>
    /// <param name="chunkZ">The target chunk Z coordinate.</param>
    /// <param name="gridX">The sub-grid tile X coordinate.</param>
    /// <param name="gridZ">The sub-grid tile Z coordinate.</param>
    /// <returns><c>true</c> if the tile is empty; otherwise, <c>false</c>.</returns>
    bool IsTileEmpty(string zone, int chunkX, int chunkZ, int gridX, int gridZ);
}