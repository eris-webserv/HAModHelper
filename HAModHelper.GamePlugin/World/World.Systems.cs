using HarmonyLib;
using HAModHelper.GamePlugin.Core;

namespace HAModHelper.GamePlugin.World.Systems;

// ---------------------------------------------------------------------------
// Hybrid Animals world generation (reverse-engineered from Assembly-CSharp):
//
//   ChunkControl.HostGetChunk loads a chunk from disk if it exists, otherwise calls
//   ChunkGeneratorOverworld.GenerateUnexplored(chunk_data, X, Z, zone, biome_id, ...) ONCE for the
//   new chunk and then SaveWholeChunkToDisk persists it. So anything added during generation is saved
//   like vanilla terrain and never re-rolled.
//
//   GenerateUnexplored looks the biome up by biome_id in ChunkControl.Instance.biomes[] and calls the
//   private GenerateBiomeObjects twice (scenic + item layer), passing the biome's
//   ChunkControl.biome_obj[] (biome_scenic). That method weights the candidates by spawn_commonness,
//   applies native clumping (additional_clump_min/max), checks empty tiles, and commits objects with
//   ChunkData.AddElement — attaching a unique basket_id (ConstructionControl.GetNewUniqueId) to each.
//
//   Each chunk is a 10x10 inner grid (inner coords 0..9). Biome ids (ChunkControl.GenBlobBiometype):
//   grass=0, snow=1, desert=2, evergreen=3, ocean=4, swamp=6, woodlands=8, sakura=9.
//
// IMPORTANT (why this file does NOT patch GenerateBiomeObjects): under BepInEx/Il2CppInterop, Harmony
// builds a native<->managed trampoline for every patched method, marshalling the full original
// signature. GenerateBiomeObjects takes a bool[,] and other types that can't be marshalled, and even
// patching GenerateUnexplored is sensitive — declaring a managed `string` parameter on the patch makes
// the trampoline fail to marshal the method's string args, which throws on EVERY chunk and leaves the
// whole world empty. So: the GenerateUnexplored patches below take ONLY blittable/safe parameters
// (no strings), and WEIGHTED injection is done by mutating the biome's native biome_scenic list once
// (a plain field/array operation, no method patch), letting vanilla generation do the weighting.
// ---------------------------------------------------------------------------

/// <summary>The overworld biomes, keyed by the game's internal biome_id.</summary>
public enum Biome
{
    /// <summary>Matches every biome. Only valid for <see cref="WorldGenInjectionType.Custom"/> injections.</summary>
    Any = -1,

    Grass = 0,
    Snow = 1,
    Desert = 2,
    Evergreen = 3,
    Ocean = 4,
    Swamp = 6,
    Woodlands = 8,
    Sakura = 9,
}

/// <summary>
/// How common a <see cref="WorldGenInjectionType.Weighted"/> object is in its biome. Mirrors the game's
/// <c>ChunkControl.spawn_commonness</c> exactly: higher rarity = picked less often; Double/Triple make
/// it appear more often than Average.
/// </summary>
public enum WeightedCommonness
{
    Average = 0,
    RareSlight = 1,
    RareMedium = 2,
    RareVery = 3,
    Double = 4,
    Triple = 5,
}

/// <summary>Which mechanism a world-gen registration uses.</summary>
public enum WorldGenInjectionType
{
    /// <summary>
    /// Added to the biome's native weighted object list, so the game spawns it with its own weighting,
    /// clumping, empty-tile checks and budget — exactly like a vanilla scenic/world object.
    /// </summary>
    Weighted,

    /// <summary>
    /// A rare, per-chunk guaranteed-style spawn placed in clusters (like the titanium chests), driven by
    /// <see cref="WorldGenRegistration.ChancePerChunk"/> rather than the biome's weighted budget.
    /// </summary>
    Special,

    /// <summary>
    /// No built-in placement — <see cref="WorldGenRegistration.OnGenerate"/> runs once per freshly
    /// generated chunk of the target biome with full control (use <see cref="WorldGenContext"/> to place).
    /// </summary>
    Custom,

    /// <summary>
    /// Removes <see cref="WorldGenRegistration.ItemName"/> (vanilla or modded) from the target biome's
    /// generated object list, so it stops spawning there.
    /// </summary>
    Remove,
}

/// <summary>
/// Context handed to a <see cref="WorldGenInjectionType.Custom"/> generator for one freshly generated
/// chunk. Place objects with <see cref="TryPlace"/>; probe free tiles with <see cref="EmptyAt"/>.
/// </summary>
public readonly struct WorldGenContext(ChunkData chunkData, int chunkX, int chunkZ, string zone, int biomeId, bool isQuestMiniworld)
{
    /// <summary>The chunk being generated.</summary>
    public ChunkData ChunkData { get; } = chunkData;

    /// <summary>Chunk X coordinate.</summary>
    public int ChunkX { get; } = chunkX;

    /// <summary>Chunk Z coordinate.</summary>
    public int ChunkZ { get; } = chunkZ;

    /// <summary>The zone (overworld dimension) the chunk belongs to.</summary>
    public string Zone { get; } = zone;

    /// <summary>The biome id of the chunk.</summary>
    public int BiomeId { get; } = biomeId;

    /// <summary>True if this is a hand-built quest mini-world (you usually want to skip those).</summary>
    public bool IsQuestMiniworld { get; } = isQuestMiniworld;

    /// <summary>True if the inner tile (x, z) is currently unoccupied. Inner coords are 0..9.</summary>
    public bool EmptyAt(int x, int z) => ChunkData != null && ChunkData.EmptyAt(x, z);

    /// <summary>
    /// Place <paramref name="itemName"/> at inner tile (x, z), mirroring how the game commits a generated
    /// object (a fresh InventoryItem carrying a unique basket_id). Returns false if the tile is occupied.
    /// </summary>
    public bool TryPlace(string itemName, int x, int z, int rot = 0)
        => WorldGenManager.PlaceObject(ChunkData, itemName, x, z, rot);
}

/// <summary>One registered world-gen entry: an item id (or callback) plus how/where it should generate.</summary>
public sealed class WorldGenRegistration
{
    /// <summary>Which mechanism this registration uses.</summary>
    public required WorldGenInjectionType Type { get; init; }

    /// <summary>The biome to act on (<see cref="Biome.Any"/> only for Custom).</summary>
    public required Biome Biome { get; init; }

    /// <summary>
    /// The full item id (e.g. <c>"MyMod:MyRock"</c>). Required for Weighted, Special and Remove;
    /// ignored for Custom.
    /// </summary>
    public string? ItemName { get; init; }

    // ── Weighted ────────────────────────────────────────────────────────────────
    /// <summary>Weighted: how common the object is. Defaults to <see cref="WeightedCommonness.Average"/>.</summary>
    public WeightedCommonness Commonness { get; init; } = WeightedCommonness.Average;

    /// <summary>
    /// Weighted: whether the object belongs to the item layer (true, the default — placeable world
    /// objects such as veins/chests) or the scenic layer (false — purely decorative props).
    /// </summary>
    public bool AsItem { get; init; } = true;

    /// <summary>Weighted/Special: minimum extra copies placed next to the first (a clump). Defaults to 0.</summary>
    public int ClumpMin { get; init; }

    /// <summary>Weighted/Special: maximum extra copies placed next to the first. Defaults to 0.</summary>
    public int ClumpMax { get; init; }

    /// <summary>Weighted: minimum world depth required for the object to appear. Defaults to 0.</summary>
    public float MinDepth { get; init; }

    /// <summary>Weighted/Special: whether the object may be randomly rotated. Defaults to true.</summary>
    public bool Rotate { get; init; } = true;

    // ── Special ─────────────────────────────────────────────────────────────────
    /// <summary>Special: probability (0..1) that a generated chunk of this biome gets a cluster.</summary>
    public double ChancePerChunk { get; init; } = 0.25;

    /// <summary>
    /// Special: the object's footprint in tiles (NxN). A cluster member is only placed where a full
    /// NxN block of inner tiles is free. Defaults to 1.
    /// </summary>
    public int Footprint { get; init; } = 1;

    // ── Special / Custom ─────────────────────────────────────────────────────────
    /// <summary>Special/Custom: skip hand-built quest mini-worlds. Defaults to true.</summary>
    public bool SkipQuestMiniworlds { get; init; } = true;

    /// <summary>Custom: the generator invoked once per freshly generated chunk of the target biome.</summary>
    public Action<WorldGenContext>? OnGenerate { get; init; }
}

/// <summary>
/// Registers objects (or behaviours) into Hybrid Animals world generation. Populate it with the
/// <c>Inject*</c>/<c>Remove*</c> methods; the Harmony patches in this file (applied by the helper's
/// <c>Harmony.PatchAll</c>) consult it live, so registrations made at any time take effect. Reusable —
/// nothing here is specific to any one mod.
/// </summary>
public sealed class WorldGenManager
{
    public static WorldGenManager Instance { get; } = new();
    private WorldGenManager() { }

    private readonly List<WorldGenRegistration> _registrations = new();

    // Weighted injection is applied by mutating each biome's native biome_scenic list. We keep the
    // pristine original per biome so re-applying (e.g. after a late registration) rebuilds from scratch
    // instead of stacking, and a signature so we only re-mutate when the relevant registrations change.
    private readonly Dictionary<int, ChunkControl.biome_obj[]?> _originalScenic = new();
    private readonly Dictionary<int, int> _appliedSignature = new();

    public void Initialize() { }

    // TEST-ONLY: reset system state.
    public void Reset()
    {
        _registrations.Clear();
        _originalScenic.Clear();
        _appliedSignature.Clear();
    }

    /// <summary>Register (or add) a world-gen entry. Returns the registration so it can later be passed
    /// to <see cref="Unregister"/>. Safe to call at any time.</summary>
    public WorldGenRegistration Register(WorldGenRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        bool needsItem = registration.Type != WorldGenInjectionType.Custom;
        if (needsItem && string.IsNullOrEmpty(registration.ItemName))
            throw new ArgumentException($"{registration.Type} world-gen injections require an ItemName.", nameof(registration));
        if (registration.Type == WorldGenInjectionType.Custom && registration.OnGenerate is null)
            throw new ArgumentException("Custom world-gen injections require an OnGenerate handler.", nameof(registration));
        if (registration.Type != WorldGenInjectionType.Custom && registration.Biome == Biome.Any)
            throw new ArgumentException($"{registration.Type} world-gen injections require a specific Biome (not Biome.Any).", nameof(registration));

        _registrations.Add(registration);
        return registration;
    }

    /// <summary>Remove a registration previously returned by one of the <c>Inject*</c>/<c>Register</c> methods.</summary>
    public void Unregister(WorldGenRegistration registration)
    {
        if (registration != null) _registrations.Remove(registration);
    }

    /// <summary>
    /// Inject an object into a biome's native weighted generation. The game handles weighting,
    /// clumping, empty-tile checks, budget and saving — exactly like a vanilla world object.
    /// </summary>
    /// <param name="itemName">Full item id, e.g. <c>"MyMod:IceVein"</c>.</param>
    /// <param name="biome">Which biome to spawn it in.</param>
    /// <param name="commonness">How common it is relative to the biome's other objects.</param>
    /// <param name="clumpMin">Minimum extra copies clumped next to each placement (0 = single).</param>
    /// <param name="clumpMax">Maximum extra copies clumped next to each placement.</param>
    /// <param name="asItem">True for placeable world objects (default), false for decorative scenics.</param>
    /// <param name="minDepth">Minimum world depth required before it appears.</param>
    /// <param name="rotate">Whether it may be randomly rotated.</param>
    public WorldGenRegistration InjectWeighted(
        string itemName, Biome biome, WeightedCommonness commonness = WeightedCommonness.Average,
        int clumpMin = 0, int clumpMax = 0, bool asItem = true, float minDepth = 0f, bool rotate = true)
        => Register(new WorldGenRegistration
        {
            Type = WorldGenInjectionType.Weighted,
            Biome = biome,
            ItemName = itemName,
            Commonness = commonness,
            ClumpMin = clumpMin,
            ClumpMax = clumpMax,
            AsItem = asItem,
            MinDepth = minDepth,
            Rotate = rotate,
        });

    /// <summary>
    /// Inject an object as a rare, per-chunk special spawn (titanium-chest style): each generated chunk
    /// of <paramref name="biome"/> has a <paramref name="chancePerChunk"/> probability of receiving a
    /// cluster of <c>1 + [clumpMin..clumpMax]</c> copies, independent of the biome's weighted budget.
    /// </summary>
    public WorldGenRegistration InjectSpecial(
        string itemName, Biome biome, double chancePerChunk,
        int clumpMin = 0, int clumpMax = 0, int footprint = 1, bool rotate = true, bool skipQuestMiniworlds = true)
        => Register(new WorldGenRegistration
        {
            Type = WorldGenInjectionType.Special,
            Biome = biome,
            ItemName = itemName,
            ChancePerChunk = chancePerChunk,
            ClumpMin = clumpMin,
            ClumpMax = clumpMax,
            Footprint = footprint,
            Rotate = rotate,
            SkipQuestMiniworlds = skipQuestMiniworlds,
        });

    /// <summary>
    /// Inject a fully custom generator: <paramref name="onGenerate"/> runs once per freshly generated
    /// chunk of <paramref name="biome"/> (or every biome when <see cref="Biome.Any"/>), with full control.
    /// </summary>
    public WorldGenRegistration InjectCustom(
        Biome biome, Action<WorldGenContext> onGenerate, bool skipQuestMiniworlds = true)
        => Register(new WorldGenRegistration
        {
            Type = WorldGenInjectionType.Custom,
            Biome = biome,
            OnGenerate = onGenerate,
            SkipQuestMiniworlds = skipQuestMiniworlds,
        });

    /// <summary>
    /// Stop <paramref name="itemName"/> (vanilla or modded) from generating in <paramref name="biome"/>.
    /// </summary>
    public WorldGenRegistration RemoveFromBiome(string itemName, Biome biome)
        => Register(new WorldGenRegistration
        {
            Type = WorldGenInjectionType.Remove,
            Biome = biome,
            ItemName = itemName,
        });

    // ── consulted by the patches ────────────────────────────────────────────────

    private IEnumerable<WorldGenRegistration> WeightedFor(int biomeId)
        => _registrations.Where(r => r.Type == WorldGenInjectionType.Weighted && (int)r.Biome == biomeId);

    private HashSet<string> RemovalsFor(int biomeId)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in _registrations)
            if (r.Type == WorldGenInjectionType.Remove && (int)r.Biome == biomeId && r.ItemName != null)
                set.Add(r.ItemName);
        return set;
    }

    private IEnumerable<WorldGenRegistration> SpecialFor(int biomeId)
        => _registrations.Where(r => r.Type == WorldGenInjectionType.Special && (int)r.Biome == biomeId);

    private IEnumerable<WorldGenRegistration> CustomFor(int biomeId)
        => _registrations.Where(r => r.Type == WorldGenInjectionType.Custom &&
                                     (r.Biome == Biome.Any || (int)r.Biome == biomeId));

    internal bool HasUnexploredWork(int biomeId)
        => _registrations.Any(r =>
            (r.Type == WorldGenInjectionType.Special && (int)r.Biome == biomeId) ||
            (r.Type == WorldGenInjectionType.Custom && (r.Biome == Biome.Any || (int)r.Biome == biomeId)));

    private int WeightedSignature(int biomeId)
        => _registrations.Count(r =>
            (r.Type == WorldGenInjectionType.Weighted || r.Type == WorldGenInjectionType.Remove) &&
            (int)r.Biome == biomeId);

    // Apply (or refresh) WEIGHTED + REMOVE registrations for a biome by mutating its native biome_scenic
    // list, so vanilla GenerateBiomeObjects does the weighting/clumping for us. Idempotent and cheap; it
    // only rebuilds when the relevant registrations change. Runs from the GenerateUnexplored prefix,
    // before the chunk generates. Never throws out — a failure here must not break generation.
    internal void ApplyWeightedFor(int biomeId)
    {
        int sig = WeightedSignature(biomeId);
        if (_appliedSignature.TryGetValue(biomeId, out var applied) && applied == sig) return;
        if (sig == 0) { _appliedSignature[biomeId] = 0; return; }

        var cc = ChunkControl.Instance;
        if (cc == null) return;                 // not ready yet — retry on a later chunk
        var biomes = cc.biomes;
        if (biomes == null || biomeId < 0 || biomeId >= biomes.Length) { _appliedSignature[biomeId] = sig; return; }

        var biome = biomes[biomeId];
        if (!_originalScenic.ContainsKey(biomeId))
            _originalScenic[biomeId] = biome.biome_scenic;

        var rebuilt = BuildObjectList(_originalScenic[biomeId], biomeId);
        biome.biome_scenic = rebuilt ?? _originalScenic[biomeId];
        biomes[biomeId] = biome;                // struct write-back into the il2cpp array

        _appliedSignature[biomeId] = sig;
        HAMHMod.Logger?.LogInfo($"[HAMH] World-gen weighted list applied for biome {biomeId} ({biome.biome_scenic?.Length ?? 0} entries).");
    }

    // Build the object list for a biome: the originals (minus removals) plus our weighted entries as real
    // biome_obj structs. Returns null when nothing changes.
    private ChunkControl.biome_obj[]? BuildObjectList(ChunkControl.biome_obj[]? original, int biomeId)
    {
        var removals = RemovalsFor(biomeId);
        var additions = WeightedFor(biomeId).ToList();
        if (removals.Count == 0 && additions.Count == 0) return null;

        var kept = new List<ChunkControl.biome_obj>();
        if (original != null)
        {
            for (int i = 0; i < original.Length; i++)
            {
                var e = original[i];
                if (removals.Count > 0 && e.item_name != null && removals.Contains(e.item_name)) continue;
                kept.Add(e);
            }
        }

        foreach (var a in additions)
        {
            kept.Add(new ChunkControl.biome_obj
            {
                item_name = a.ItemName,
                is_item = a.AsItem,
                spawn_rate = (ChunkControl.spawn_commonness)(int)a.Commonness,
                additional_clump_min = a.ClumpMin,
                additional_clump_max = a.ClumpMax,
                clump_overwrite_obj = null,
                dont_rotate = !a.Rotate,
                min_depth = a.MinDepth,
                hide_from_mimicry_perk = false,
            });
        }

        var result = new ChunkControl.biome_obj[kept.Count];
        for (int i = 0; i < kept.Count; i++) result[i] = kept[i];
        return result;
    }

    // Run SPECIAL + CUSTOM registrations for a freshly generated chunk (called from the postfix).
    internal void RunUnexplored(ChunkData chunkData, int X, int Z, string zone, int biomeId, bool isQuestMiniworld)
    {
        foreach (var reg in SpecialFor(biomeId))
        {
            try
            {
                if (isQuestMiniworld && reg.SkipQuestMiniworlds) continue;
                if (string.IsNullOrEmpty(reg.ItemName)) continue;

                // Deterministic per-chunk-per-item RNG: stable across reloads/regeneration.
                int seed = unchecked((X * 73856093) ^ (Z * 19349663) ^ reg.ItemName!.GetHashCode());
                var rng = new System.Random(seed);
                if (rng.NextDouble() >= reg.ChancePerChunk) continue;

                int footprint = Math.Max(1, reg.Footprint);
                int extra = reg.ClumpMax > reg.ClumpMin ? rng.Next(reg.ClumpMin, reg.ClumpMax + 1) : reg.ClumpMin;
                int total = 1 + Math.Max(0, extra);

                int placed = 0, fx = 0, fz = 0;
                for (int i = 0; i < total; i++)
                {
                    bool anchored = placed > 0;
                    if (!TryFindSpot(chunkData, rng, footprint, anchored, fx, fz, out int vx, out int vz))
                        break;

                    int rot = reg.Rotate ? rng.Next(0, 4) : 0;
                    if (!PlaceObject(chunkData, reg.ItemName!, vx, vz, rot)) continue;

                    if (placed == 0) { fx = vx; fz = vz; }
                    placed++;
                    HAMHMod.Logger?.LogInfo(
                        $"[HAMH] World-gen special spawned '{reg.ItemName}' in biome {biomeId} — chunk ({X},{Z}) inner ({vx},{vz}) [{placed}/{total}]");
                }
            }
            catch (Exception ex)
            {
                HAMHMod.Logger?.LogError($"[HAMH] World-gen special injection '{reg.ItemName}' failed at chunk ({X},{Z}): {ex}");
            }
        }

        foreach (var reg in CustomFor(biomeId))
        {
            try
            {
                if (isQuestMiniworld && reg.SkipQuestMiniworlds) continue;
                reg.OnGenerate?.Invoke(new WorldGenContext(chunkData, X, Z, zone, biomeId, isQuestMiniworld));
            }
            catch (Exception ex)
            {
                HAMHMod.Logger?.LogError($"[HAMH] World-gen custom injection failed at chunk ({X},{Z}): {ex}");
            }
        }
    }

    // Commit one object at inner tile (x, z) the way the game does: a fresh InventoryItem carrying a
    // unique basket_id. Returns false if the tile is occupied.
    internal static bool PlaceObject(ChunkData chunk, string itemName, int x, int z, int rot)
    {
        if (chunk == null || string.IsNullOrEmpty(itemName)) return false;
        if (!chunk.EmptyAt(x, z)) return false;

        var cc = ConstructionControl.Instance;
        if (cc != null)
        {
            var extra = new ExtraInventoryData();
            extra.SetLong("basket_id", cc.GetNewUniqueId());
            chunk.AddElement(x, z, new ChunkElement(new InventoryItem(itemName, extra), rot));
        }
        else
        {
            chunk.AddElement(x, z, new ChunkElement(itemName, rot));
        }
        return true;
    }

    // Find an origin tile whose footprint*footprint centred block is empty; keep the footprint inside
    // the 0..9 grid. When anchored, restrict to a few tiles from the anchor so a cluster reads together.
    private static bool TryFindSpot(
        ChunkData chunk, System.Random rng, int footprint, bool anchored, int anchorX, int anchorZ,
        out int ox, out int oz)
    {
        int half = Math.Max(0, footprint / 2);
        int lo = half;
        int hi = 9 - half;
        if (hi < lo) { ox = oz = 0; return false; }

        for (int attempt = 0; attempt < 40; attempt++)
        {
            int x = rng.Next(lo, hi + 1);
            int z = rng.Next(lo, hi + 1);

            if (anchored)
            {
                int cheb = Math.Max(Math.Abs(x - anchorX), Math.Abs(z - anchorZ));
                if (cheb < footprint || cheb > footprint + 2) continue;
            }

            if (FootprintEmpty(chunk, x, z, footprint)) { ox = x; oz = z; return true; }
        }

        ox = oz = 0;
        return false;
    }

    private static bool FootprintEmpty(ChunkData chunk, int cx, int cz, int footprint)
    {
        int half = footprint / 2;
        for (int dx = -half; dx <= half; dx++)
            for (int dz = -half; dz <= half; dz++)
                if (!chunk.EmptyAt(cx + dx, cz + dz)) return false;
        return true;
    }
}

// ── Harmony patches (applied by the helper's Harmony.PatchAll) ──────────────────
//
// Both patches target ChunkGeneratorOverworld.GenerateUnexplored and take ONLY safe (non-string)
// parameters, so Harmony's trampoline for the method marshals cleanly. The zone string is read off the
// ChunkData field instead of taken as a patch parameter (a string patch parameter breaks marshalling
// and empties the whole world).

/// <summary>
/// Prefix: applies WEIGHTED/REMOVE registrations to the biome's native object list before the chunk
/// generates, so vanilla generation produces them with its own weighting and clumping.
/// </summary>
[HarmonyPatch(typeof(ChunkGeneratorOverworld), nameof(ChunkGeneratorOverworld.GenerateUnexplored))]
internal static class GenerateUnexploredWeightedPatch
{
    [HarmonyPrefix]
    static void Prefix(int biome_id)
    {
        try
        {
            WorldGenManager.Instance.ApplyWeightedFor(biome_id);
        }
        catch (Exception ex)
        {
            HAMHMod.Logger?.LogError($"[HAMH] World-gen weighted apply failed (biome {biome_id}): {ex}");
        }
    }
}

/// <summary>
/// Postfix: runs SPECIAL (rare per-chunk clustered spawns) and CUSTOM (modder callbacks) for the freshly
/// generated chunk, after vanilla has populated it so empty-tile checks see the real layout.
/// </summary>
[HarmonyPatch(typeof(ChunkGeneratorOverworld), nameof(ChunkGeneratorOverworld.GenerateUnexplored))]
internal static class GenerateUnexploredSpecialCustomPatch
{
    [HarmonyPostfix]
    static void Postfix(ChunkData chunk_data, int X, int Z, int biome_id, bool is_quest_miniworld)
    {
        if (chunk_data == null) return;
        var mgr = WorldGenManager.Instance;
        if (!mgr.HasUnexploredWork(biome_id)) return;

        string zone;
        try { zone = chunk_data.zone; } catch { zone = string.Empty; }

        mgr.RunUnexplored(chunk_data, X, Z, zone, biome_id, is_quest_miniworld);
    }
}
