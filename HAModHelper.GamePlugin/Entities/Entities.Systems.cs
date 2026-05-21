using HarmonyLib;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using HAModHelper.GamePlugin.Core;

namespace HAModHelper.GamePlugin.Entities.Systems;

public class Creature
{
    public required string Name { get; set; }
    public required List<string> DataLines { get; set; }
    public byte[]? SpriteBytes { get; set; }
}

public sealed class CreatureManager
{
    public static CreatureManager Instance { get; } = new();
    private CreatureManager() { }

    private readonly Dictionary<string, Creature> _creatures = new();
    private readonly Dictionary<string, Sprite> _spriteCache = new();

    public void Initialize() { }

    public void AddCreature(Creature creature)
    {
        _creatures[creature.Name] = creature;
    }

    public void RegisterFromEmbeddedResources(Assembly assembly, string resourcePrefix)
    {
        var imageBytes = new Dictionary<string, byte[]>();

        foreach (string res in assembly.GetManifestResourceNames())
        {
            if (!res.StartsWith(resourcePrefix) || !res.EndsWith(".png"))
                continue;

            string name = res.Substring(resourcePrefix.Length, res.Length - resourcePrefix.Length - 4);
            try
            {
                using var stream = assembly.GetManifestResourceStream(res)!;
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                imageBytes[name] = ms.ToArray();
            }
            catch (Exception ex)
            {
                try { HAMHMod.Logger.LogError($"[HAMH] Failed to load creature image {name}: {ex.Message}"); } catch { }
            }
        }

        foreach (string res in assembly.GetManifestResourceNames())
        {
            if (!res.StartsWith(resourcePrefix) || !res.EndsWith(".txt"))
                continue;

            string name = res.Substring(resourcePrefix.Length, res.Length - resourcePrefix.Length - 4);
            try
            {
                using var stream = assembly.GetManifestResourceStream(res)!;
                using var reader = new StreamReader(stream);
                var lines = new List<string>(Regex.Split(reader.ReadToEnd(), @"\r\n|\r|\n"));
                AddCreature(new Creature
                {
                    Name = name,
                    DataLines = lines,
                    SpriteBytes = imageBytes.TryGetValue(name, out var bytes) ? bytes : null,
                });
                try { HAMHMod.Logger.LogInfo($"[HAMH] Registered creature: {name}"); } catch { }
            }
            catch (Exception ex)
            {
                try { HAMHMod.Logger.LogError($"[HAMH] Failed to load creature {name}: {ex.Message}"); } catch { }
            }
        }
    }

    internal bool TryGetDataLines(string name, out List<string> lines)
    {
        if (_creatures.TryGetValue(name, out var c))
        {
            lines = c.DataLines;
            return true;
        }
        lines = null!;
        return false;
    }

    internal bool IsCustomCreature(string name) => _creatures.ContainsKey(name);

    internal IReadOnlyCollection<string> AllNames => _creatures.Keys;

    internal void EnsureInLists()
    {
        try
        {
            var morpher = CreatureMorpher.Instance;
            if (morpher == null || _creatures.Count == 0) return;

            foreach (string name in _creatures.Keys)
            {
                if (morpher.all_creature_names != null && !morpher.all_creature_names.Contains(name))
                    morpher.all_creature_names.Add(name);

                if (morpher.default_creature_names != null && !morpher.default_creature_names.Contains(name))
                    morpher.default_creature_names.Add(name);
            }
        }
        catch (Exception ex)
        {
            try { HAMHMod.Logger.LogError($"[HAMH] EnsureInLists failed: {ex}"); } catch { }
        }
    }

    internal Sprite? GetOrCreateSprite(string name)
    {
        if (_spriteCache.TryGetValue(name, out var cached))
            return cached;

        if (!_creatures.TryGetValue(name, out var creature) || creature.SpriteBytes == null)
            return null;

        var tex = new Texture2D(2, 2);
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
        ImageConversion.LoadImage(tex, creature.SpriteBytes);
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        _spriteCache[name] = sprite;
        return sprite;
    }

    private static string ExtractCreatureName(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        int slash = path.LastIndexOf('/');
        if (slash >= 0) path = path.Substring(slash + 1);
        int backslash = path.LastIndexOf('\\');
        if (backslash >= 0) path = path.Substring(backslash + 1);
        return path;
    }

    [HarmonyPatch(typeof(ResourceControl), nameof(ResourceControl.GetTextFileLines))]
    internal static class GetTextFileLinesPatch
    {
        static bool Prefix(string __0, ref bool __1, ref Il2CppSystem.Collections.Generic.List<string> __result)
        {
            try
            {
                string name = ExtractCreatureName(__0);
                if (!Instance.TryGetDataLines(name, out var lines))
                    return true;

                var il2List = new Il2CppSystem.Collections.Generic.List<string>();
                foreach (string line in lines)
                    il2List.Add(line);

                __1 = true;
                __result = il2List;
                return false;
            }
            catch (Exception ex)
            {
                try { HAMHMod.Logger.LogError($"[HAMH] GetTextFileLines patch error: {ex}"); } catch { }
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(CreatureMorpher), nameof(CreatureMorpher.LoadPlainCreatureFromDisk))]
    internal static class LoadPlainCreatureFromDiskPatch
    {
        static void Prefix(string __0)
        {
            try
            {
                if (Instance.IsCustomCreature(__0))
                    Instance.EnsureInLists();
            }
            catch (Exception ex)
            {
                try { HAMHMod.Logger.LogError($"[HAMH] LoadPlainCreatureFromDisk patch error: {ex}"); } catch { }
            }
        }
    }

    [HarmonyPatch(typeof(CreatureMorpher), nameof(CreatureMorpher.GenerateCreatureLists))]
    internal static class GenerateCreatureListsPatch
    {
        static void Postfix()
        {
            try
            {
                Instance.EnsureInLists();
            }
            catch (Exception ex)
            {
                try { HAMHMod.Logger.LogError($"[HAMH] GenerateCreatureLists patch error: {ex}"); } catch { }
            }
        }
    }

    [HarmonyPatch(typeof(BreedControl), nameof(BreedControl.SetUpBreeder))]
    internal static class SetUpBreederPatch
    {
        static void Postfix(BreedControl __instance)
        {
            try
            {
                if (Instance._creatures.Count == 0) return;
                Instance.EnsureInLists();

                var customList = new Il2CppSystem.Collections.Generic.List<string>();
                foreach (string name in Instance.AllNames)
                    customList.Add(name);

                __instance.CreateButtons(customList, false);
            }
            catch (Exception ex)
            {
                try { HAMHMod.Logger.LogError($"[HAMH] SetUpBreeder patch error: {ex}"); } catch { }
            }
        }
    }

    [HarmonyPatch(typeof(BreedControl), nameof(BreedControl.LoadEverythingFromDisk))]
    internal static class LoadEverythingFromDiskPatch
    {
        static void Prefix()
        {
            try
            {
                Instance.EnsureInLists();
            }
            catch (Exception ex)
            {
                try { HAMHMod.Logger.LogError($"[HAMH] LoadEverythingFromDisk patch error: {ex}"); } catch { }
            }
        }
    }

    [HarmonyPatch(typeof(ResourceControl), nameof(ResourceControl.AssignCreatureSprite))]
    internal static class AssignCreatureSpritePatch
    {
        static bool Prefix(string __0, Image __1)
        {
            try
            {
                if (!Instance.IsCustomCreature(__0))
                    return true;

                var sprite = Instance.GetOrCreateSprite(__0);
                if (sprite != null && __1 != null)
                {
                    __1.sprite = sprite;
                    __1.enabled = true;
                }
                return false;
            }
            catch (Exception ex)
            {
                try { HAMHMod.Logger.LogError($"[HAMH] AssignCreatureSprite patch error: {ex}"); } catch { }
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(CreatureMorpher), nameof(CreatureMorpher.GetRandomCreature))]
    internal static class GetRandomCreaturePatch
    {
        static bool Prefix(ref string __result)
        {
            try
            {
                if (Instance._creatures.Count == 0) return true;

                if (UnityEngine.Random.Range(0f, 1f) < 0.15f)
                {
                    int idx = UnityEngine.Random.Range(0, Instance._creatures.Count);
                    int i = 0;
                    foreach (string name in Instance.AllNames)
                    {
                        if (i++ == idx)
                        {
                            __result = name;
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try { HAMHMod.Logger.LogError($"[HAMH] GetRandomCreature patch error: {ex}"); } catch { }
            }
            return true;
        }
    }
}
