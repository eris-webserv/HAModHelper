using HarmonyLib;
using HAModHelper.GamePlugin.Base.Events;
using HAModHelper.GamePlugin.Dialogue.Events;
using HAModHelper.GamePlugin.Dialogue.Systems;

namespace HAModHelper.GamePlugin.Dialogue.Patches;

/// <summary>
/// Returns mod-registered NPC dialogue instead of loading the vanilla file, when the NPC's
/// <c>npc_file</c> custom field has been registered via <c>DialogueManager.RegisterNpcDialogue</c>.
/// </summary>
/// <remarks>
/// Every placed NPC in the game is an instance of the same generic "DEBUG-npc" item; individual
/// NPCs are distinguished by a custom field <c>npc_file</c> (e.g. "Shindo Warrior (Sleeping)"),
/// not by item_name -- confirmed by checking placed-NPC world data, where every entry shares the
/// item type "DEBUG-npc" and only npc_file differs. Keying off item_name (as this used to) would
/// never match a real NPC, since they're all the same item type.
/// <para/>
/// Both GetFullNPC and LoadFullNpcFile are patched (mirroring TryLoadInventoryItemPatch/
/// GetFullItemNamePatch's dual-entry-point pattern for items in Base.GamePlugin.cs) since
/// GetFullNPC's cache-check relative to LoadFullNpcFile wasn't independently confirmed via
/// native decompilation -- patching both entry points is cheap and removes the ambiguity.
/// </remarks>
[HarmonyPatch(typeof(DialogueControl), nameof(DialogueControl.GetFullNPC))]
public static class DialogueControl_GetFullNPC_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(InventoryItem npc_item, ref FullNPC __result)
    {
        if (npc_item == null) return true;
        if (!DialogueManager.Instance.TryGetRegisteredNpc(npc_item.GetString("npc_file"), out var npc)) return true;

        __result = npc!;
        return false;
    }
}

[HarmonyPatch(typeof(DialogueControl), nameof(DialogueControl.LoadFullNpcFile))]
public static class DialogueControl_LoadFullNpcFile_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(InventoryItem npc_item, ref FullNPC __result)
    {
        if (npc_item == null) return true;
        if (!DialogueManager.Instance.TryGetRegisteredNpc(npc_item.GetString("npc_file"), out var npc)) return true;

        __result = npc!;
        return false;
    }
}

/// <summary>Fires dialogue lifecycle events for mods to observe.</summary>
[HarmonyPatch(typeof(DialogueControl), nameof(DialogueControl.EnterDialogue))]
public static class DialogueControl_EnterDialogue_Patch
{
    [HarmonyPostfix]
    public static void Postfix(DialogueControl __instance, int enter_dialogue_at)
    {
        EventBus.Instance.Fire(new DialogueEnteredEvent(__instance.curr_NPC_display_name, enter_dialogue_at));
    }
}

[HarmonyPatch(typeof(DialogueControl), nameof(DialogueControl.ExitDialogue))]
public static class DialogueControl_ExitDialogue_Patch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        EventBus.Instance.Fire(new DialogueExitedEvent());
    }
}

[HarmonyPatch(typeof(DialogueControl), nameof(DialogueControl.ClickOptionA))]
public static class DialogueControl_ClickOptionA_Patch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        EventBus.Instance.Fire(new DialogueOptionSelectedEvent(true));
    }
}

[HarmonyPatch(typeof(DialogueControl), nameof(DialogueControl.ClickOptionB))]
public static class DialogueControl_ClickOptionB_Patch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        EventBus.Instance.Fire(new DialogueOptionSelectedEvent(false));
    }
}

/// <summary>
/// Replaces a world-placed companion NPC's dialogue (guard/wait/merchant modes) with
/// mod-registered content, matched by display name.
/// </summary>
/// <remarks>
/// GameController.OnInteractWithCompanion resolves its InventoryItem through an internal
/// chunk/buildable lookup with no exposed parameter or hook point -- confirmed via native
/// decompile, "companion_item" only exists as a local variable, not a method parameter -- so
/// there's no way to intervene before it starts the vanilla dialogue. It's also confirmed to
/// never read "guard_message1"/"guard_message2" at all (dead data in this build), so there's
/// nothing to restore for unmodded guard NPCs either. Instead, this postfix lets the vanilla
/// interaction run, then immediately swaps in custom content if the resulting
/// curr_NPC_display_name matches a mod registration -- a brief re-open rather than a clean
/// intercept, but avoids re-implementing the game's own chunk/buildable lookup.
/// </remarks>
[HarmonyPatch(typeof(GameController), "OnInteractWithCompanion")]
public static class GameController_OnInteractWithCompanion_Patch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        var dc = DialogueControl.Instance;
        if (dc == null) return;
        if (!DialogueManager.Instance.TryGetCompanionDialogue(dc.curr_NPC_display_name, out var dialogueData)) return;

        var displayName = dc.curr_NPC_display_name;
        dc.ExitDialogue(false);
        DialogueManager.Instance.EnterDialogue(dialogueData!, 0, "", displayName);
    }
}
