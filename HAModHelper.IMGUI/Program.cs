
using MelonLoader;
using HAModHelper.GamePlugin.Core; // The namespace of your mod class

[assembly: HarmonyDontPatchAll]  // manual patching handled during init
[assembly: MelonInfo(typeof(HAMHMod), "HAModHelper.IMGUI", "0.0.1", "Eris (erisws)")]
[assembly: MelonGame("Abstract Software Inc", "Hybrid Animals")]