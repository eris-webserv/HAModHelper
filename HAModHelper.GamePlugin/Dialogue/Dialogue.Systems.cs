using HAModHelper.GamePlugin.Dialogue.Interfaces;

namespace HAModHelper.GamePlugin.Dialogue.Systems;

/// <summary>
/// Lets mods register custom NPC dialogue trees and start dialogues directly. Thin wrapper
/// around <c>DialogueControl</c>.
/// </summary>
public sealed class DialogueManager : IDialogueManager
{
    public static DialogueManager Instance { get; } = new DialogueManager();

    private readonly Dictionary<string, FullNPC> _customNpcs = new();

    private DialogueManager() { }

    /// <summary>TEST-ONLY: Reset system state.</summary>
    public void Reset()
    {
        _customNpcs.Clear();
    }

    /// <summary>Initialize the dialogue manager (called on game start).</summary>
    public void Initialize()
    {
    }

    /// <inheritdoc />
    public void RegisterNpcDialogue(string npcFileName, string translatedDisplayName, Dictionary<int, Dictionary<string, object>> dialogueData)
    {
        _customNpcs[npcFileName] = new FullNPC
        {
            translated_display_name = translatedDisplayName,
            dialogue_data = ToNativeDialogueData(dialogueData)
        };
    }

    /// <summary>Used by the Harmony patches to look up mod-registered NPC dialogue by npc_file.</summary>
    internal bool TryGetRegisteredNpc(string? npcFileName, out FullNPC? npc)
    {
        if (npcFileName == null)
        {
            npc = null;
            return false;
        }
        return _customNpcs.TryGetValue(npcFileName, out npc);
    }

    /// <inheritdoc />
    public bool EnterDialogue(Dictionary<int, Dictionary<string, object>> dialogueData, int enterAt, string voice, string npcDisplayName = "")
    {
        var dc = DialogueControl.Instance;
        if (dc == null) return false;

        // EnterDialogue dereferences GameController.Instance.player (among other singletons)
        // without a null-check -- confirmed via native decompile -- so it crashes if called
        // before the player has spawned, regardless of dialogue data. Every vanilla call site
        // also primes focus with SetFocusNpc first; focus_type_t.none is the only variant that
        // tolerates a null npc_obj, so it's safe to call standalone without a real NPC GameObject.
        if (GameController.Instance?.player == null) return false;

        dc.SetFocusNpc(null, npcDisplayName, "", DialogueControl.focus_type_t.none);
        dc.EnterDialogue(ToNativeDialogueData(dialogueData), enterAt, voice);
        return true;
    }

    // FullNPC.dialogue_data and EnterDialogue's parameter are both Il2Cpp-native collections
    // (Il2CppSystem.Collections.Generic.Dictionary<int, Dictionary<string, Il2CppSystem.Object>>),
    // not plain .NET Dictionary<int, Dictionary<string, object>> -- the doubly-nested, dynamically
    // typed shape doesn't fit DictHelper's single-level generic converter, so it's done by hand here.
    private static Il2CppSystem.Collections.Generic.Dictionary<int, Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Object>> ToNativeDialogueData(Dictionary<int, Dictionary<string, object>> data)
    {
        var outer = new Il2CppSystem.Collections.Generic.Dictionary<int, Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Object>>();
        foreach (var (nodeId, fields) in data)
        {
            var inner = new Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Object>();
            foreach (var (key, value) in fields)
            {
                inner[key] = BoxFieldValue(value)!;
            }
            outer[nodeId] = inner;
        }
        return outer;
    }

    /// <summary>
    /// Boxes a dialogue field value into the Il2Cpp object type EnterDialogue/FullNPC expect.
    /// Covers the scalar types (string/bool/numeric) real dialogue field values use -- the
    /// vanilla format is a flat key-value bag per node, not nested structures.
    /// </summary>
    private static Il2CppSystem.Object? BoxFieldValue(object? value) => value switch
    {
        null => null,
        string s => s,
        bool b => b,
        int i => i,
        long l => l,
        float f => f,
        double d => d,
        char c => c,
        byte by => by,
        sbyte sb => sb,
        short sh => sh,
        ushort ush => ush,
        uint ui => ui,
        ulong ul => ul,
        _ => throw new NotSupportedException($"Unsupported dialogue field value type: {value.GetType().FullName}")
    };
}
