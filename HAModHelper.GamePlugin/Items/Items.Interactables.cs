using System.Reflection;
using HarmonyLib;
using HAModHelper.GamePlugin.Core;
using UnityEngine;

namespace HAModHelper.GamePlugin.Items.Systems;

// ---------------------------------------------------------------------------
// Hybrid Animals decides what interacting with a placed object does through TWO
// hardcoded, item_name-keyed layers (item_name == the full id, e.g.
// "MyMod:MyChair", which is what the game stores in Interactable.item_name):
//
//   1. Dispatch:  GameController.player_interact(GameObject) reads the target's
//                 Interactable.item_name and runs a compiler-generated
//                 switch(item_name) (string-hash jump table) to choose a
//                 behaviour (sit, sleep, open a specific craft list, ...).
//                 Unknown ids hit the default and do nothing.
//   2. Gates:     InventoryUtils.IsChairObject / IsBedObject / ... are hardcoded
//                 string-equality chains. Six call sites gate chair behaviour on
//                 IsChairObject: the sit animation (SharedCreature.TrySitInChairObj)
//                 and five ConstructionControl placement/snap/companion/removal sites.
//
// Neither layer is component- or interface-driven, so a modded object's id
// matches nothing and the object is inert. This manager is the single source of
// truth both layers consult at runtime: the player_interact prefix dispatches
// registered ids to their behaviour, and one shared postfix on each
// InventoryUtils.IsXObject predicate answers true for registered ids.
// ---------------------------------------------------------------------------

/// <summary>The kind of interactable an item id is registered as.</summary>
public enum InteractableType
{
    /// <summary>A seat. Classified by <c>IsChairObject</c>; interacting makes the player sit.</summary>
    Chair,

    /// <summary>A bed. Classified by <c>IsBedObject</c>; interacting makes the player lie down.</summary>
    Bed,

    /// <summary>
    /// A crafting station. Interacting opens the crafting menu for
    /// <see cref="InteractableRegistration.CraftListName"/>, titled
    /// <see cref="InteractableRegistration.CraftMenuTitle"/>.
    /// </summary>
    OpenCrafting,

    /// <summary>
    /// A storage container (chest). Interacting opens the world-container UI for the object, exactly
    /// like a vanilla chest. The tab shows <see cref="InteractableRegistration.DisplayName"/> instead
    /// of the raw id, and the container has <see cref="InteractableRegistration.Pages"/> pages of slots.
    /// </summary>
    Container,

    /// <summary>
    /// No built-in behaviour or classification — <see cref="InteractableRegistration.OnInteract"/>
    /// runs on interact. Use for anything not covered above (custom menus, scripted effects, ...).
    /// </summary>
    Custom,
}

/// <summary>Context handed to an interaction behaviour when a registered object is used.</summary>
public readonly struct InteractContext(GameController controller, GameObject target, Interactable interactable)
{
    /// <summary>The GameController whose <c>player_interact</c> was invoked.</summary>
    public GameController Controller { get; } = controller;

    /// <summary>The interacted world object (the argument to <c>player_interact</c>).</summary>
    public GameObject Target { get; } = target;

    /// <summary>The object's <c>Interactable</c> component (carries item_name, active_obj_str, ...).</summary>
    public Interactable Interactable { get; } = interactable;
}

/// <summary>One registered interactable: an item id plus how it should behave.</summary>
public sealed class InteractableRegistration
{
    /// <summary>
    /// The id the game stores in <c>Interactable.item_name</c> — the full mod id
    /// (e.g. <c>"MyMod:MyChair"</c>), the same id passed to <see cref="ItemManager.AddItem"/>.
    /// </summary>
    public required string ItemName { get; init; }

    /// <summary>Which kind of interactable this id is.</summary>
    public required InteractableType Type { get; init; }

    /// <summary>
    /// Behaviour invoked on interact. Required for <see cref="InteractableType.Custom"/>;
    /// ignored for the other kinds, which run their own behaviour.
    /// </summary>
    public Action<InteractContext>? OnInteract { get; init; }

    /// <summary>
    /// For <see cref="InteractableType.OpenCrafting"/>: the craft-list file name to open,
    /// e.g. <c>"Crafting - Oven"</c>. Required for that type, ignored otherwise.
    /// </summary>
    public string? CraftListName { get; init; }

    /// <summary>
    /// For <see cref="InteractableType.OpenCrafting"/>: the title shown on the crafting tab.
    /// When null, the item's display name (or, failing that, its id) is used — instead of the
    /// raw id the game would otherwise show.
    /// </summary>
    public string? CraftMenuTitle { get; init; }

    /// <summary>
    /// For <see cref="InteractableType.Container"/>: the title shown on the container tab. When null,
    /// the item's display name (or, failing that, its id) is used — instead of the raw id
    /// (e.g. <c>"MyMod:MyChest"</c>) the game would otherwise show.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// For <see cref="InteractableType.Container"/>: how many pages of slots the container has.
    /// The game supports 1–3 pages (each page is <c>inventory_ctr.n_slots_per_page_</c> slots);
    /// values are clamped into that range. Defaults to 1.
    /// </summary>
    public int Pages { get; init; } = 1;

    /// <summary>Run this registration's behaviour.</summary>
    internal void Execute(InteractContext ctx)
    {
        switch (Type)
        {
            case InteractableType.Chair:
            case InteractableType.Bed:
                Sit(ctx);
                break;
            case InteractableType.OpenCrafting:
                OpenCrafting();
                break;
            case InteractableType.Container:
                OpenContainer(ctx);
                break;
            case InteractableType.Custom:
                OnInteract?.Invoke(ctx);
                break;
        }
    }

    // Container case of player_interact. Reverse-engineered from GameController.player_interact: it
    // records the interacted object via GameController.NoteInteractingElement (chunk/inner grid coords
    // + the object's InventoryItem + rot, all carried on the Interactable), then calls
    // inventory_ctr.TryOpenWorldContainer with the same values. TryOpenWorldContainer handles both
    // the online path (server request for the container contents) and the offline path
    // (LoadFromDiskAsContainer -> SucceedOpenWorldContainer -> OpenInventoryAndContainer, which shows
    // the UI). It keys the container off GameController.interacting_element_item, so we set that here
    // rather than relying on it already being current.
    private static void OpenContainer(InteractContext ctx)
    {
        var inv = inventory_ctr.Instance;
        if (inv == null) return;

        var it = ctx.Interactable;
        var item = it.corresponding_item;          // the placed object's InventoryItem (the container)
        if (item == null) return;

        ctx.Controller.NoteInteractingElement(
            it.origin_chunkX, it.origin_chunkZ,
            it.origin_innerX, it.origin_innerZ,
            item, it.temp_rot);

        inv.TryOpenWorldContainer(
            item, it.temp_rot,
            it.origin_innerX, it.origin_innerZ,
            it.origin_chunkX, it.origin_chunkZ);
    }

    // Chair/bed case of player_interact. TrySitInChairObj itself calls IsChairObject/IsBedObject
    // (which our classification postfix has made answer true for this id) to pick sit vs sleep.
    // Requires the placed prefab to expose a child transform named "target" as the seat anchor.
    private static void Sit(InteractContext ctx)
    {
        var player = ctx.Controller.player;
        if (player == null) return;

        var creature = player.GetComponent<SharedCreature>();
        if (creature == null) return;
        if (creature.snapped_to_chair_obj) return;            // already seated -> vanilla no-ops too

        var seatId = ctx.Interactable.active_obj_str;          // world-position key of the seat object
        creature.TrySitInChairObj(seatId);

        var gui = GameplayGUIControl.Instance;                 // show the "stand up" button
        if (gui != null && gui.end_sit_button != null)
            gui.end_sit_button.SetActive(true);

        var sender = GameServerSender.Instance;                // sync to other players
        if (sender != null)
            sender.SendSitInChair(seatId);
    }

    // Workbench case of player_interact. Uses the 4-arg overload so the tab title is ours rather
    // than the raw id the 2-arg overload derives from the item name.
    private void OpenCrafting()
    {
        var inv = inventory_ctr.Instance;
        if (inv == null) return;

        var list = inv.GetCraftList(CraftListName!);
        var title = CraftMenuTitle ?? ItemManager.Instance.GetItem(ItemName)?.Name ?? ItemName;
        inv.OpenInventoryAndCrafting(WindowControl.tab.right, list, title, "INVENTORY");
    }
}

/// <summary>
/// Registers placed/spawned objects as recognised interactables (chairs, beds, crafting stations,
/// or fully custom behaviours). Populate it with the <c>Register*</c> methods; the Harmony patches
/// in this file (applied by the helper's <c>Harmony.PatchAll</c>) consult it live, so registrations
/// made at any time take effect.
/// </summary>
public sealed class InteractableManager
{
    public static InteractableManager Instance { get; } = new();
    private InteractableManager() { }

    // item_name -> registration, consulted by the player_interact dispatch prefix.
    private readonly Dictionary<string, InteractableRegistration> _byItemName = new(StringComparer.Ordinal);

    // InteractableType -> ids registered as that type, consulted by the IsXObject postfixes.
    private readonly Dictionary<InteractableType, HashSet<string>> _byType = new();

    // Built-in classifications: type -> the InventoryUtils predicate that must answer true for it.
    // Add a new classification by adding one row here (and an InteractableType member).
    private static readonly (InteractableType Type, string PredicateMethod)[] ClassificationTable =
    [
        (InteractableType.Chair, nameof(InventoryUtils.IsChairObject)),
        (InteractableType.Bed,   nameof(InventoryUtils.IsBedObject)),
    ];

    public void Initialize() { }

    // TEST-ONLY: reset system state.
    public void Reset()
    {
        _byItemName.Clear();
        _byType.Clear();
    }

    /// <summary>Register (or replace) an interactable. Safe to call at any time.</summary>
    public void Register(InteractableRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (string.IsNullOrEmpty(registration.ItemName))
            throw new ArgumentException("ItemName is required.", nameof(registration));
        if (registration.Type == InteractableType.Custom && registration.OnInteract is null)
            throw new ArgumentException("Custom interactables require an OnInteract handler.", nameof(registration));
        if (registration.Type == InteractableType.OpenCrafting && string.IsNullOrEmpty(registration.CraftListName))
            throw new ArgumentException("OpenCrafting interactables require a CraftListName.", nameof(registration));

        _byItemName[registration.ItemName] = registration;

        if (!_byType.TryGetValue(registration.Type, out var set))
            _byType[registration.Type] = set = new HashSet<string>(StringComparer.Ordinal);
        set.Add(registration.ItemName);
    }

    /// <summary>Register a seat. Interacting makes the player sit (and placement treats it as a chair).</summary>
    public void RegisterChair(string itemName)
        => Register(new InteractableRegistration { ItemName = itemName, Type = InteractableType.Chair });

    /// <summary>Register a bed. Interacting makes the player lie down (and placement treats it as a bed).</summary>
    public void RegisterBed(string itemName)
        => Register(new InteractableRegistration { ItemName = itemName, Type = InteractableType.Bed });

    /// <summary>
    /// Register a crafting station. Interacting opens the crafting menu for <paramref name="craftListName"/>
    /// (e.g. <c>"Crafting - Oven"</c>), titled <paramref name="menuTitle"/> (defaults to the item's
    /// display name).
    /// </summary>
    public void RegisterCraftingStation(string itemName, string craftListName, string? menuTitle = null)
        => Register(new InteractableRegistration
        {
            ItemName = itemName,
            Type = InteractableType.OpenCrafting,
            CraftListName = craftListName,
            CraftMenuTitle = menuTitle,
        });

    /// <summary>
    /// Register a storage container (chest). Interacting opens the world-container UI for the placed
    /// object, exactly like a vanilla chest (works online and offline).
    /// </summary>
    /// <param name="itemName">The full mod id stored in <c>Interactable.item_name</c> (e.g. <c>"MyMod:MyChest"</c>).</param>
    /// <param name="displayName">
    /// Title shown on the container tab. When null, the item's display name (or its id) is used,
    /// instead of the raw id the game would otherwise show.
    /// </param>
    /// <param name="pages">How many pages of slots the container has (1–3, clamped). Defaults to 1.</param>
    public void RegisterContainer(string itemName, string? displayName = null, int pages = 1)
        => Register(new InteractableRegistration
        {
            ItemName = itemName,
            Type = InteractableType.Container,
            DisplayName = displayName,
            Pages = pages,
        });

    /// <summary>Register an interactable with fully custom behaviour run on interact.</summary>
    public void RegisterCustom(string itemName, Action<InteractContext> onInteract)
        => Register(new InteractableRegistration { ItemName = itemName, Type = InteractableType.Custom, OnInteract = onInteract });

    // ── consulted by the patches ────────────────────────────────────────────────

    internal InteractableRegistration? Lookup(string itemName)
        => _byItemName.TryGetValue(itemName, out var reg) ? reg : null;

    /// <summary>The registration for <paramref name="itemName"/> if it is a registered container, else null.</summary>
    internal InteractableRegistration? LookupContainer(string? itemName)
        => itemName is not null && _byItemName.TryGetValue(itemName, out var reg) && reg.Type == InteractableType.Container
            ? reg
            : null;

    /// <summary>
    /// The registration for the container the player is currently interacting with (keyed off
    /// <c>GameController.interacting_element_item</c>), if that object is a registered container.
    /// Used by the container UI patches, which only see the inventory controller, not the object.
    /// </summary>
    internal InteractableRegistration? CurrentContainerRegistration()
    {
        var item = GameController.Instance?.interacting_element_item;
        return item == null ? null : LookupContainer(item.item_name);
    }

    internal bool IsRegisteredClassification(string predicateMethod, string? itemName)
    {
        if (itemName is null) return false;
        foreach (var (type, predicate) in ClassificationTable)
            if (predicate == predicateMethod)
                return _byType.TryGetValue(type, out var set) && set.Contains(itemName);
        return false;
    }

    /// <summary>The InventoryUtils predicate methods the classification postfix is applied to.</summary>
    internal static IEnumerable<MethodBase> ClassificationTargetMethods()
    {
        foreach (var (_, predicate) in ClassificationTable)
        {
            var method = AccessTools.Method(typeof(InventoryUtils), predicate, [typeof(string)]);
            if (method is null)
                throw new MissingMethodException($"InventoryUtils.{predicate}(string) not found in the game assembly.");
            yield return method;
        }
    }
}

// ── Harmony patches (applied by the helper's Harmony.PatchAll) ──────────────────

/// <summary>
/// Prefix on the interact dispatch. The vanilla method switches on <c>Interactable.item_name</c> and
/// does nothing for ids it doesn't recognise; for a registered id we run the behaviour and skip the
/// original, otherwise we defer to vanilla.
/// </summary>
[HarmonyPatch(typeof(GameController), nameof(GameController.player_interact))]
internal static class PlayerInteractInteractablePatch
{
    [HarmonyPrefix]
    static bool Prefix(GameController __instance, GameObject interaction_target)
    {
        if (interaction_target == null) return true;

        Interactable interactable;
        InteractableRegistration? reg;
        try
        {
            interactable = interaction_target.GetComponent<Interactable>();
            if (interactable == null) return true;             // not an interactable -> vanilla
            reg = InteractableManager.Instance.Lookup(interactable.item_name);
        }
        catch (Exception ex)
        {
            HAMHMod.Logger?.LogError($"[HAMH] Interactable identify failed: {ex}");
            return true;
        }

        if (reg is null) return true;                          // not one of ours -> vanilla

        try
        {
            reg.Execute(new InteractContext(__instance, interaction_target, interactable));
        }
        catch (Exception ex)
        {
            HAMHMod.Logger?.LogError($"[HAMH] Interactable handler for '{interactable.item_name}' threw: {ex}");
        }
        return false;                                          // we own this interaction
    }
}

/// <summary>
/// Shared postfix on the <c>InventoryUtils.IsXObject(string)</c> classification predicates. Forces the
/// result true for ids registered under that predicate's type; never flips true to false, so vanilla
/// items are unaffected. <c>__originalMethod</c> identifies which predicate ran.
/// </summary>
[HarmonyPatch]
internal static class InteractableClassificationPatch
{
    static IEnumerable<MethodBase> TargetMethods() => InteractableManager.ClassificationTargetMethods();

    [HarmonyPostfix]
    static void Postfix(string __0, ref bool __result, MethodBase __originalMethod)
    {
        if (__result) return;
        try
        {
            if (InteractableManager.Instance.IsRegisteredClassification(__originalMethod.Name, __0))
                __result = true;
        }
        catch (Exception ex)
        {
            HAMHMod.Logger?.LogError($"[HAMH] Interactable classification postfix failed: {ex}");
        }
    }
}

// ── Container UI patches ────────────────────────────────────────────────────────
//
// A modded chest opens through the vanilla container flow (see OpenContainer above), but two pieces
// of that flow are hardcoded against the game's own chest ids and so don't know about ours:
//
//   • The tab title. SucceedOpenWorldContainer derives it from TranslateItemName(item_name); for a
//     modded id that resolves to the raw id (e.g. "MyMod:MyChest"). We rewrite the title that
//     inventory_ctr.OpenInventoryAndContainer is about to display.
//   • The page count. goto_crafting_tab switches on item_name to pick how many pages to lay out and
//     hits a 1-page default for unknown ids. The count is funnelled into inventory_ctr.LayOutInvSlots'
//     n_pages argument (which alone drives the page-switcher buttons; navigation/redraw are generic),
//     and drag/drop validity per page comes from inventory_ctr.GetPermittedContainerSlots. We override
//     both for our containers.

/// <summary>
/// Prefix on the world-container UI open. Replaces the tab title with the registered container's
/// <see cref="InteractableRegistration.DisplayName"/> (falling back to the item's display name, then
/// its id) instead of the raw id the game derives from the item name.
/// </summary>
[HarmonyPatch(typeof(inventory_ctr), nameof(inventory_ctr.OpenInventoryAndContainer))]
internal static class ContainerTabTitlePatch
{
    [HarmonyPrefix]
    static void Prefix(ref string non_inv_tab_name)
    {
        try
        {
            var reg = InteractableManager.Instance.CurrentContainerRegistration();
            if (reg is null) return;
            non_inv_tab_name = reg.DisplayName
                ?? ItemManager.Instance.GetItem(reg.ItemName)?.Name
                ?? reg.ItemName;
        }
        catch (Exception ex)
        {
            HAMHMod.Logger?.LogError($"[HAMH] Container tab-title prefix failed: {ex}");
        }
    }
}

/// <summary>
/// Prefix on the inventory/container slot layout. When the container tab is being laid out for one of
/// our registered containers, overrides <c>n_pages</c> with the registered page count (1–3) so the
/// page-switcher buttons appear; the game's own navigation and redraw handle the rest generically.
/// </summary>
[HarmonyPatch(typeof(inventory_ctr), nameof(inventory_ctr.LayOutInvSlots))]
internal static class ContainerPageCountPatch
{
    [HarmonyPrefix]
    static void Prefix(ref int n_pages)
    {
        try
        {
            // Only touch the container tab (the same gate inv_page_switch uses), never the inventory
            // tab or other miniwindows that also call LayOutInvSlots.
            var wc = WindowControl.Instance;
            if (wc == null
                || wc.curr_miniwindow != WindowControl.miniwindow_type_t.inventory_and_container
                || wc.curr_miniwindow_tab_selected != WindowControl.tab.right)
                return;

            var reg = InteractableManager.Instance.CurrentContainerRegistration();
            if (reg is null) return;

            n_pages = Math.Clamp(reg.Pages, 1, 3);
        }
        catch (Exception ex)
        {
            HAMHMod.Logger?.LogError($"[HAMH] Container page-count prefix failed: {ex}");
        }
    }
}

/// <summary>
/// Postfix on the per-page permitted-slot lookup (drives which slots accept drag/drop). For a
/// registered container it returns the slot indices for the requested page, mirroring the layout the
/// game uses for its own multi-page chests (page 1 → [0, n), page 2 → [p2_begin, p2_begin+n),
/// page 3 → [p2_begin+n, p2_begin+2n), where n = n_slots_per_page_). Pages beyond the registered
/// count get no slots.
/// </summary>
[HarmonyPatch(typeof(inventory_ctr), nameof(inventory_ctr.GetPermittedContainerSlots))]
internal static class ContainerPermittedSlotsPatch
{
    [HarmonyPostfix]
    static void Postfix(inventory_ctr.container_style_t style, string item_name, inventory_ctr.ptype page,
        ref Il2CppSystem.Collections.Generic.List<int> __result)
    {
        try
        {
            if (style != inventory_ctr.container_style_t.world_container) return;
            var reg = InteractableManager.Instance.LookupContainer(item_name);
            if (reg is null) return;

            int pages = Math.Clamp(reg.Pages, 1, 3);
            int nspp = inventory_ctr.n_slots_per_page_;
            int p2 = inventory_ctr.p2_begin_;

            var list = new Il2CppSystem.Collections.Generic.List<int>();
            void AddPage(int pageIndex)
            {
                int start = pageIndex == 0 ? 0 : p2 + (pageIndex - 1) * nspp;
                for (int i = 0; i < nspp; i++) list.Add(start + i);
            }

            switch (page)
            {
                case inventory_ctr.ptype.firstPage: AddPage(0); break;
                case inventory_ctr.ptype.secondPage: if (pages >= 2) AddPage(1); break;
                case inventory_ctr.ptype.thirdPage: if (pages >= 3) AddPage(2); break;
                case inventory_ctr.ptype.eitherPage:
                    for (int p = 0; p < pages; p++) AddPage(p);
                    break;
            }

            __result = list;
        }
        catch (Exception ex)
        {
            HAMHMod.Logger?.LogError($"[HAMH] Container permitted-slots postfix failed: {ex}");
        }
    }
}

/// <summary>
/// Postfix on <c>InventoryUtils.UsesBasketId(InventoryItem)</c> — the predicate that decides whether an
/// object stores its contents under a per-instance <c>basket_id</c>.
///
/// <para>
/// This is what actually keys a chest's contents to the specific placed object. At put-down time
/// <c>inventory_ctr.FinalizeItemBeforePutDown</c> assigns a fresh unique <c>basket_id</c>
/// (<c>ConstructionControl.GetNewUniqueId</c>) — but only when this predicate is true and the item
/// doesn't already have one — and stores it in the object's saved data; opening then loads/saves the
/// container file for that id. The predicate is a hardcoded item_name list, so a modded container never
/// qualifies: it keeps <c>basket_id == 0</c> and every modded chest in the world shares that one
/// container. Forcing true for registered containers gives each placed instance its own id.
/// </para>
///
/// <para>
/// Because the id lives in the object's world data, relocating the chest with the build/move tools
/// keeps the same object and so keeps its contents; only taking it back into the inventory (which
/// resets <c>basket_id</c> via <c>DropExtraItemDataOnPickup</c>) and placing a fresh one starts an empty
/// container — i.e. exactly how vanilla chests behave.
/// </para>
/// </summary>
[HarmonyPatch(typeof(InventoryUtils), nameof(InventoryUtils.UsesBasketId), new Type[] { typeof(InventoryItem) })]
internal static class ContainerUsesBasketIdPatch
{
    [HarmonyPostfix]
    static void Postfix(InventoryItem item, ref bool __result)
    {
        if (__result) return;                                  // already a basket-id object -> leave it
        try
        {
            if (item != null && InteractableManager.Instance.LookupContainer(item.item_name) != null)
                __result = true;
        }
        catch (Exception ex)
        {
            HAMHMod.Logger?.LogError($"[HAMH] Container UsesBasketId postfix failed: {ex}");
        }
    }
}
