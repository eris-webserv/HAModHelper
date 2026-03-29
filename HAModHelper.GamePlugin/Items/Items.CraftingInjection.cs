using HarmonyLib;

namespace HAModHelper.GamePlugin.Items.Systems;

/// <summary>
/// Allows mods to inject custom items into any vanilla crafting station list
/// (e.g. <c>"Crafting - Crafting Table"</c>, <c>"Crafting - Crucible"</c>).
/// <para/>
/// A single Harmony postfix on <c>inventory_ctr.GetCraftList</c> processes all
/// registered injections, so multiple mods injecting into the same list are both
/// applied correctly. Insertion is idempotent — opening the crafting menu multiple
/// times will not produce duplicate entries.
/// </summary>
public sealed class CraftingInjectionManager
{
    public static CraftingInjectionManager Instance { get; } = new();
    private CraftingInjectionManager() { }

    private sealed class InjectionEntry
    {
        public required string CraftListName;
        public required string ItemFullId;
        public          string? InsertAfter;   // null → append at end
    }

    private readonly List<InjectionEntry> _registrations = new();

    public void Initialize() { }

    // TEST-ONLY
    public void Reset() => _registrations.Clear();

    /// <summary>
    /// Inject <paramref name="itemFullId"/> into <paramref name="craftListName"/>.
    /// </summary>
    /// <param name="craftListName">File name of the craft list (without extension), e.g.
    ///   <c>"Crafting - Crafting Table"</c>.</param>
    /// <param name="itemFullId">Full item id to inject, e.g. <c>"Expansion:Gem"</c>.</param>
    /// <param name="insertAfter">Item name after which to insert. Pass <c>null</c> to
    ///   append at the end of the list. The search uses <c>item_name</c> (full id for
    ///   mod items, plain name for base-game items).</param>
    public void Inject(string craftListName, string itemFullId, string? insertAfter = null)
    {
        _registrations.Add(new InjectionEntry
        {
            CraftListName = craftListName,
            ItemFullId    = itemFullId,
            InsertAfter   = insertAfter,
        });
    }

    /// <summary>Called by the Harmony patch on every GetCraftList invocation.</summary>
    internal void ProcessInjections(string craftListName, ref new_craft_list result)
    {
        if (result?.items == null) return;

        var pending = _registrations
            .Where(r => r.CraftListName == craftListName)
            .ToList();
        if (pending.Count == 0) return;

        // Work on a local reference so each injection builds on the previous result.
        var current = result.items;

        foreach (var entry in pending)
        {
            // Idempotent: skip if already present (handles repeated GetCraftList calls
            // when the game caches the list object across menu opens).
            bool alreadyPresent = false;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i]?.item?.item_name == entry.ItemFullId)
                {
                    alreadyPresent = true;
                    break;
                }
            }
            if (alreadyPresent) continue;

            // Locate insertion point.
            int insertAt = current.Length; // default: append
            if (entry.InsertAfter != null)
            {
                for (int i = 0; i < current.Length; i++)
                {
                    if (current[i]?.item?.item_name == entry.InsertAfter)
                    {
                        insertAt = i + 1;
                        break;
                    }
                }
            }

            // Build expanded array.
            var next = new ItemCountPair[current.Length + 1];
            for (int i = 0; i < insertAt; i++)
                next[i] = current[i];

            int nCraft = 1;
            try { nCraft = inventory_ctr.Instance?.GetItem_nCraft(entry.ItemFullId) ?? 1; } catch { }
            next[insertAt] = new ItemCountPair(new InventoryItem(entry.ItemFullId), nCraft);

            for (int i = insertAt; i < current.Length; i++)
                next[i + 1] = current[i];

            current = next;
        }

        result.items = current;
    }
}

// ── Harmony patch ─────────────────────────────────────────────────────────────

[HarmonyPatch(typeof(inventory_ctr), nameof(inventory_ctr.GetCraftList))]
internal static class GetCraftListInjectionPatch
{
    [HarmonyPostfix]
    static void Postfix(string craft_list_file_name, ref new_craft_list __result)
    {
        CraftingInjectionManager.Instance.ProcessInjections(craft_list_file_name, ref __result);
    }
}
