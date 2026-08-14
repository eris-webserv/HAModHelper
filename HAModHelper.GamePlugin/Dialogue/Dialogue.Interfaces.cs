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

    /// <summary>Starts a dialogue directly, bypassing NPC lookup entirely.</summary>
    /// <param name="dialogueData">Dialogue tree, keyed by node ID. See <see cref="RegisterNpcDialogue"/> for the required field shape.</param>
    /// <param name="enterAt">Node ID to start at.</param>
    /// <param name="voice">Voice key used for dialogue audio.</param>
    /// <returns><c>true</c> if the dialogue control was available and the dialogue was started.</returns>
    bool EnterDialogue(Dictionary<int, Dictionary<string, object>> dialogueData, int enterAt, string voice);
}
