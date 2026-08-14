namespace HAModHelper.GamePlugin.Dialogue.Interfaces;

/// <summary>
/// Defines public operations for registering custom NPC dialogue trees and starting dialogues.
/// </summary>
public interface IDialogueManager
{
    /// <summary>
    /// Registers dialogue data for an NPC, so <c>DialogueControl</c> uses it instead of loading
    /// the vanilla NPC file.
    /// </summary>
    /// <param name="npcFileName">
    /// The NPC's <c>npc_file</c> identifier (e.g. <c>"Shindo Warrior (Sleeping)"</c>, matching the
    /// game's own NPC text file names) -- NOT the item name. Every placed NPC in the game is an
    /// instance of the same generic "DEBUG-npc" item; individual NPCs are distinguished by this
    /// custom field (<c>InventoryItem.GetString("npc_file")</c>), so registering under a vanilla
    /// NPC's npc_file replaces that NPC's dialogue everywhere it's placed in the world.
    /// </param>
    /// <param name="translatedDisplayName">Display name shown in the dialogue window header.</param>
    /// <param name="dialogueData">
    /// Dialogue tree, keyed by node ID, in the same field format as the game's own NPC text files
    /// (all values are strings, including numeric-looking ones). A narration node uses
    /// <c>otherspeak</c> (the line spoken) and <c>goto</c> (next node ID, or <c>"-1"</c> to end).
    /// A branching node uses <c>optionA</c>/<c>gotoA</c> and <c>optionB</c>/<c>gotoB</c> instead.
    /// Example single-node dialogue: <c>{ [0] = { ["otherspeak"] = "Hello!", ["goto"] = "-1" } }</c>.
    /// Missing required fields (e.g. no <c>goto</c> on a narration node) crash inside the game's
    /// own native code, not this API, so match this shape closely.
    /// </param>
    void RegisterNpcDialogue(string npcFileName, string translatedDisplayName, Dictionary<int, Dictionary<string, object>> dialogueData);

    /// <summary>
    /// Starts a dialogue directly, bypassing NPC lookup entirely. Primes focus first (matching
    /// every vanilla call site), then requires <c>GameController.Instance.player</c> to be set --
    /// the game's own <c>EnterDialogue</c> dereferences it and several other singletons
    /// unconditionally, so calling this before the player has spawned returns <c>false</c>
    /// instead of crashing.
    /// </summary>
    /// <param name="dialogueData">Dialogue tree, keyed by node ID. See <see cref="RegisterNpcDialogue"/> for the required field shape.</param>
    /// <param name="enterAt">Node ID to start at.</param>
    /// <param name="voice">Voice key used for dialogue audio.</param>
    /// <param name="npcDisplayName">Display name shown in the dialogue window header.</param>
    /// <returns><c>true</c> if the dialogue control and player were available and the dialogue was started.</returns>
    bool EnterDialogue(Dictionary<int, Dictionary<string, object>> dialogueData, int enterAt, string voice, string npcDisplayName = "");

    /// <summary>
    /// Registers dialogue for a world-placed "guard"/"wait"/"merchant"-mode companion NPC
    /// (a <c>Companion</c> item, not a file-backed <c>DEBUG-npc</c>), matched by its
    /// <c>npc_display_name</c> (case-insensitive).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="RegisterNpcDialogue"/>, there's no clean way to intercept these NPCs'
    /// dialogue before it starts -- <c>GameController.OnInteractWithCompanion</c> resolves the
    /// companion item through an internal chunk/buildable lookup with no exposed hook point, and
    /// (confirmed via native decompile) never reads "guard_message1"/"guard_message2" at all, so
    /// there's nothing to override even for vanilla guard NPCs. Instead, this replaces whatever
    /// dialogue the vanilla interaction just started, immediately after it starts.
    /// </remarks>
    /// <param name="npcDisplayName">The companion's <c>npc_display_name</c> custom field, e.g. <c>"Shindo Guard"</c>.</param>
    /// <param name="dialogueData">Dialogue tree, keyed by node ID. See <see cref="RegisterNpcDialogue"/> for the required field shape.</param>
    void RegisterCompanionDialogue(string npcDisplayName, Dictionary<int, Dictionary<string, object>> dialogueData);
}
