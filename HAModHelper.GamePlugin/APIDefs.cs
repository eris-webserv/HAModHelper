using System;
using System.Collections.Generic;
using System.Numerics;
using HAModHelper.Events;
using HAModHelper.GamePlugin.Items;

namespace HAModHelper.FakeAPIDefinitionsForPlanningDontUseThesePleaseGod
{
    public abstract class API
    {
        public abstract void AddItem(string id, string name, ItemActions actions);
        public abstract void StartPlaceEntity(string id);
    }

    public class ItemUseEvent : BaseEvent
    {
        public Item Item { get; set; }
        public Player Player { get; set; }
    }

    public class ItemEatenEvent : BaseEvent
    {
        public Item Item { get; set; }
        public Player Player { get; set; }
    }

    public class ItemHeldUseEvent : BaseEvent
    {
        public Item Item { get; set; }
        public WorldEntity Target { get; set; }
        public Player Player { get; set; }
    }

    public class ItemAquiredEvent : BaseEvent
    {
        public Player Player { get; set; }
        public int InventoryItem { get; set; }
    }

    public class ItemDeletedEvent : BaseEvent
    {
        public Player Player { get; set; }
        public Item Item { get; set; }
    }

    public class StructureDestroyedEvent : BaseEvent
    {
        public WorldEntity Structure { get; set; }
        public Player Player { get; set; } // May be null
    }

    public class StructurePlacedEvent : BaseEvent
    {
        public WorldEntity Structure { get; set; }
        public Player Player { get; set; } // May be null
    }

    public class PlayerDiedEvent : BaseEvent
    {
        public Player Player { get; set; }
    }

    public class PlayerDamagedEvent : BaseEvent
    {
        public Player Player { get; set; }
    }

    public class PlayerKilledEntityEvent : BaseEvent
    {
        public Player Player { get; set; }
        public WorldEntity Entity { get; set; }
    }

    public class DimensionSwitchedEvent : BaseEvent
    {
        public Player Player { get; set; }
        public Dimension OldDimension { get; set; }
        public Dimension NewDimension { get; set; }
    }

    public class Player
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Health { get; set; }
        public Vector3 Position { get; set; }
        public Dimension Dimension { get; set; }
        public List<Item> Inventory { get; set; }
    }

    public class WorldEntity
    {
        public string Id { get; set; }
        public Vector3 Position { get; set; }
        public Dimension Dimension { get; set; }
        public bool IsTerminating { get; set; }
    }

    public class Dimension
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // "overworld" | "mine" | "house" | "sky"
    }
}
