using System.Collections.Generic;
using UnityEngine;
using HAModHelper.GamePlugin.Chunks.Interfaces;

namespace HAModHelper.GamePlugin.Chunks.Systems;

/// <summary>
/// Central manager for handling chunk access, world modifications, and custom biome object generation.
/// </summary>
public sealed class ChunkManager : IChunkManager
{
    /// <summary>Gets the singleton instance of the <see cref="ChunkManager"/>.</summary>
    public static ChunkManager Instance { get; } = new ChunkManager();
    
    /// <summary>
    /// Registry mapping biome IDs to custom <see cref="ChunkControl.biome_obj"/> items.
    /// </summary>
    public Dictionary<int, List<ChunkControl.biome_obj>> CustomBiomeObjects { get; } = new();

    private ChunkManager() { }

    /// <summary>Initializes the ChunkManager subsystem.</summary>
    public void Initialize() { }

    /// <summary>
    /// Registers a custom object to naturally spawn in a biome during generation.
    /// </summary>
    /// <param name="biomeIndex">Target biome index ID.</param>
    /// <param name="obj">Biome object definition struct.</param>
    public void RegisterBiomeObject(int biomeIndex, ChunkControl.biome_obj obj)
    {
        if (!CustomBiomeObjects.TryGetValue(biomeIndex, out var list))
        {
            list = new List<ChunkControl.biome_obj>();
            CustomBiomeObjects[biomeIndex] = list;
        }
        list.Add(obj);
    }

    /// <summary>
    /// Retrieves a chunk from the world manager if currently active and loaded.
    /// </summary>
    /// <param name="zone">World zone name.</param>
    /// <param name="chunkX">Chunk grid X coordinate.</param>
    /// <param name="chunkZ">Chunk grid Z coordinate.</param>
    /// <returns>Active <see cref="Chunk"/> if loaded; otherwise, <c>null</c>.</returns>
    public Chunk? GetActiveChunk(string zone, int chunkX, int chunkZ)
    {
        if (ChunkControl.Instance == null) return null;
        
        string key = $"{zone}_{chunkX}_{chunkZ}"; 
        
        if (ChunkControl.Instance.Chunks != null && ChunkControl.Instance.Chunks.TryGetValue(key, out var chunk))
        {
            return chunk;
        }
        return null;
    }

    /// <summary>
    /// Live-places or updates an item at a given tile position, synchronizing logical data and visual representation.
    /// </summary>
    public bool TryPlaceObject(string zone, int chunkX, int chunkZ, int gridX, int gridZ, string newItemName)
    {
        Chunk? chunk = GetActiveChunk(zone, chunkX, chunkZ);
        if (chunk == null || chunk.chunk_data == null || chunk.chunk_obj == null) 
            return false;

        InventoryItem newItem = new InventoryItem(newItemName);
        
        chunk.chunk_obj.ReplaceElementItemInstance(
            chunkX, chunkZ, gridX, gridZ, 
            newItem, null, 0, 
            chunk.chunk_data, 
            (Il2CppSystem.Action<GameObject>)null
        );

        return true;
    }

    /// <summary>
    /// Destroys a buildable or element instance at a specific tile coordinate.
    /// </summary>
    public bool RemoveObject(string zone, int chunkX, int chunkZ, int gridX, int gridZ)
    {
        Chunk? chunk = GetActiveChunk(zone, chunkX, chunkZ);
        if (chunk == null || chunk.chunk_data == null || chunk.chunk_obj == null) 
            return false;

        chunk.chunk_obj.DestroyBuildableInstance(chunkX, chunkZ, gridX, gridZ, null, 0);
        return true;
    }

    /// <summary>
    /// Checks if a tile coordinate is empty in the chunk's logical data.
    /// </summary>
    public bool IsTileEmpty(string zone, int chunkX, int chunkZ, int gridX, int gridZ)
    {
        Chunk? chunk = GetActiveChunk(zone, chunkX, chunkZ);
        if (chunk == null || chunk.chunk_data == null) return false;

        return chunk.chunk_data.EmptyAt(gridX, gridZ);
    }

    /// <summary>
    /// Converts Unity world coordinates (e.g. player position) into chunk grid coordinates.
    /// </summary>
    /// <param name="worldPos">3D world position vector.</param>
    /// <returns>A 2D integer vector containing Chunk X and Z coordinates.</returns>
    public Vector2Int WorldToChunkCoords(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / 10f);
        int z = Mathf.FloorToInt(worldPos.z / 10f);
        return new Vector2Int(x, z);
    }
}