namespace HAModHelper.GamePlugin.Dialogue.Interfaces;

/// <summary>
/// Defines public operations for registering custom NPC dialogue trees and starting dialogues.
/// </summary>
public interface IDialogueManager
{
    /// <summary>
    /// Registers dialogue data for an NPC item, so <c>DialogueControl</c> uses it instead of
    /// loading the vanilla NPC file.
    /// </summary>
    /// <param name="npcItemFullId">The NPC's item name, as read from <c>InventoryItem.item_name</c>.</param>
    /// <param name="translatedDisplayName">Display name shown in the dialogue window header.</param>
    /// <param name="dialogueData">
    /// Dialogue tree, keyed by node ID. Each node is a loosely-typed field dictionary in the same
    /// shape the game's own NPC files use (e.g. text/option/goto fields).
    /// </param>
    void RegisterNpcDialogue(string npcItemFullId, string translatedDisplayName, Dictionary<int, Dictionary<string, object>> dialogueData);

    /// <summary>Starts a dialogue directly, bypassing NPC lookup entirely.</summary>
    /// <param name="dialogueData">Dialogue tree, keyed by node ID.</param>
    /// <param name="enterAt">Node ID to start at.</param>
    /// <param name="voice">Voice key used for dialogue audio.</param>
    /// <returns><c>true</c> if the dialogue control was available and the dialogue was started.</returns>
    bool EnterDialogue(Dictionary<int, Dictionary<string, object>> dialogueData, int enterAt, string voice);
}
