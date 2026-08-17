using UnityEngine;

namespace HAModHelper.GamePlugin.Chunks.Events;

/// <summary>
/// Event triggered when a world chunk completes loading or fetching.
/// </summary>
public class ChunkLoadedEvent : Base.Events.BaseEvent 
{ 
    /// <summary>Gets the zone identifier of the loaded chunk.</summary>
    public string Zone { get; }

    /// <summary>Gets the X coordinate of the chunk in world grid space.</summary>
    public int ChunkX { get; }

    /// <summary>Gets the Z coordinate of the chunk in world grid space.</summary>
    public int ChunkZ { get; }

    /// <summary>Gets the native ChunkData instance -- the type ChunkControl.HostGetChunk actually returns.</summary>
    public ChunkData ChunkData { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkLoadedEvent"/> class.
    /// </summary>
    public ChunkLoadedEvent(string zone, int chunkX, int chunkZ, ChunkData chunkData)
    {
        Zone = zone;
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        ChunkData = chunkData;
    }
}

/// <summary>
/// Event triggered when a specific grid tile within a chunk has its item modified or replaced.
/// </summary>
public class TileModifiedEvent : Base.Events.BaseEvent
{
    /// <summary>Gets the zone identifier where the tile modification occurred.</summary>
    public string Zone { get; }

    /// <summary>Gets the X coordinate of the chunk containing the tile.</summary>
    public int ChunkX { get; }

    /// <summary>Gets the Z coordinate of the chunk containing the tile.</summary>
    public int ChunkZ { get; }

    /// <summary>Gets the internal sub-grid X coordinate inside the chunk.</summary>
    public int GridX { get; }

    /// <summary>Gets the internal sub-grid Z coordinate inside the chunk.</summary>
    public int GridZ { get; }

    /// <summary>Gets the new inventory item placed on the tile, if any.</summary>
    public InventoryItem? NewItem { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TileModifiedEvent"/> class.
    /// </summary>
    public TileModifiedEvent(string zone, int chunkX, int chunkZ, int gridX, int gridZ, InventoryItem? newItem)
    {
        Zone = zone;
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        GridX = gridX;
        GridZ = gridZ;
        NewItem = newItem;
    }
}