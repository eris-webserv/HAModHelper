# Hybrid Animals — object interaction system (reverse-engineered)

How the game decides what interacting with a placed/spawned object does, and how the
`InteractableManager` API hooks into it. Source: il2cpp dump + Ghidra of `libil2cpp.so`
(game v2026.3.4). Implementation: `Items/Items.Interactables.cs`.

## TL;DR

There is **no** generic component/interface/flag system. Interaction behaviour is
**hardcoded in two layers, both keyed on the object's `item_name` string** — which is
the full mod id (e.g. `MyMod:MyChair`, the same id passed to `ItemManager.AddItem`), *not*
the display name:

1. **Dispatch** — `GameController.player_interact(GameObject)` reads the target's
   `Interactable.item_name` and runs a compiler-generated `switch(item_name)`
   (string-hash jump table) to pick a behaviour. Unknown ids hit the default → nothing.
2. **Classification gates** — `InventoryUtils.IsChairObject(string)`,
   `IsBedObject(string)`, etc. are hardcoded `string ==` chains. They gate the actual
   behaviour (sit animation) *and* the placement logic.

To make a modded object behave like a chair, **both** layers must recognise its id.

## Layer 1 — the dispatch: `GameController.player_interact(GameObject interaction_target)`

- Resolves the target's `Interactable` (bails on `Collectible`, follows `replace_for`
  redirects, honours `is_house_exit`, lock checks, ...).
- `switch (ComputeStringHash(interactable.item_name))` — one case per recognised id:
  - **Chair / Throne / Metal Chair / Sofa Chair / Park Bench / Stump Chair / Bed** →
    local player `SharedCreature` (`GameController.player` 0x178 → `GetComponent<SharedCreature>`),
    bail if `snapped_to_chair_obj` (0x6c), `TrySitInChairObj(interactable.active_obj_str)`,
    show `GameplayGUIControl.Instance.end_sit_button`, `GameServerSender.SendSitInChair(id)`.
  - **Crafting stations** → `inventory_ctr.GetCraftList("<list>")` →
    `OpenInventoryAndCrafting(...)`. The 2-arg overload titles the tab with
    `TranslateItemName(item.item_name)` (→ the raw id for mod items); the 4-arg overload
    `OpenInventoryAndCrafting(WindowControl.tab.right, list, non_inv_tab_name, "INVENTORY")`
    lets us set the title ourselves.
  - Navposts, companions, karaoke, vending, trophies, ... each their own case.
- Unrecognised id → default → nothing. **This is why a modded object is inert.**

`Interactable` offsets (TypeDefIndex 1681): `corresponding_item` 0x18 · `item_name` 0x60 ·
`active_obj_str` 0x68 (world-position key / seat id) · `replace_for` 0x50.

## Layer 2 — the gates: `InventoryUtils.IsXObject(string item_name)`

Static predicates, each a hardcoded equality chain:

- `IsChairObject` → `"Chair" | "Throne" | "Metal Chair" | "Sofa Chair" | "Park Bench" | "Stump Chair"`
- `IsBedObject` → `"Bed"`
- siblings (all `static bool (string)`, patchable identically): `IsHouseObject`, `IsCaveObject`,
  `IsHeavenDimension`/`IsHellDimension`/`IsPureDimension`, `IsPaintbrush`, `IsStamp`, ...

`IsChairObject` has **6 real call sites**: `SharedCreature.TrySitInChairObj` (picks sit anim 4
vs `IsBedObject` → sleep anim 5) and `ConstructionControl.{AllowedToPlace,
SnapMousePositionToObjectOrigins, AdjustBuildableInstance, PlayerRemoveAt,
TryMakeCompanionSitOnChair}`. Patching the predicate covers all six.

> `Interactable.CustomStart` has the chair list **inlined** (not a call), but that path is a
> cosmetic furniture-pairing feature, not the sit interaction, so it needs no patching.

`TrySitInChairObj` requires the placed prefab to have a **child transform named `"target"`** as
the seat anchor (position + rotation the player snaps to) — a **prefab requirement**, mirror the
vanilla `Chair` prefab.

## The API (`InteractableManager`)

`Items/Items.Interactables.cs`. `InteractableManager` is the single source of truth both layers
consult; the Harmony patches in that file are applied by the helper's `Harmony.PatchAll`:

- `PlayerInteractInteractablePatch` (prefix on `player_interact`): registered id → run its
  behaviour, skip the original; otherwise vanilla. A *parallel dispatch table* for modded ids — it
  mirrors the game's switch, it does not spoof a vanilla id.
- `InteractableClassificationPatch` (one shared postfix over each `InventoryUtils.IsXObject` via
  `TargetMethods()`): forces `true` for ids registered under that predicate's type. Never flips
  `true`→`false`. `__originalMethod` selects which predicate ran.

```csharp
InteractableManager.Instance.RegisterChair("MyMod:MyChair");
InteractableManager.Instance.RegisterBed("MyMod:MyBed");
// crafting station: its own type — craft list + optional tab title (defaults to the item's name)
InteractableManager.Instance.RegisterCraftingStation("MyMod:MyOven", "Crafting - Oven", "Oven");
// anything else
InteractableManager.Instance.RegisterCustom("MyMod:MyThing", ctx => { /* ctx.Controller/Target/Interactable */ });
```

### Adding a new built-in classification

Add an `InteractableType` member and one row to `InteractableManager.ClassificationTable`
(`(Type, nameof(InventoryUtils.IsXObject))`). The shared postfix handles the rest. Behaviours
that need a `player_interact` action get a `case` in `InteractableRegistration.Execute`.
