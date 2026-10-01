using HarmonyLib;
using HAModHelper.GamePlugin.Assets.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace HAModHelper.GamePlugin.Assets.Patches;

/// <summary>
/// Harmony prefixes over the concrete <c>ResourceControl</c> loader entry points. Each builds the
/// Addressables key exactly as the game would, and if the key belongs to a registered mod bundle,
/// serves the asset from that bundle and skips the original (which would otherwise fail to resolve
/// the key against the game's catalog).
///
/// <para>
/// Every prefix is wrapped so that a registered mod asset can never throw into — or otherwise break
/// — these loaders for unrelated base-game items, which share the same methods.
/// </para>
/// </summary>
internal static class AssetLoaderPatches
{
    private const string PrefabPrefix = "Assets/Prefabs/";
    private const string InteriorPrefix = "Assets/Prefabs/Interiors/";
    private const string PrefabSuffix = ".prefab";

    // Returns false (skip original) only when the manager owns the key.
    private static bool TryServePrefab(string key, Il2CppSystem.Action<GameObject>? cb)
    {
        try
        {
            return AssetBundleManager.Instance.TryServePrefab(key, cb) ? false : true;
        }
        catch (Exception ex)
        {
            AssetBundleManager.LogError($"TryServePrefab('{key}') failed, deferring to game: {ex}");
            return true;
        }
    }

    // Held items (LiteModel.ApplyWeapon), hats (ApplyHat), armor, weapon displays.
    // equipment_path == the item's "World_obj_path" field.
    [HarmonyPatch(typeof(ResourceControl), nameof(ResourceControl.AsyncInstantiateEquipment))]
    internal static class EquipmentPatch
    {
        static bool Prefix(string equipment_path, Il2CppSystem.Action<GameObject> on_asset_ready)
            => TryServePrefab(PrefabPrefix + equipment_path + PrefabSuffix, on_asset_ready);
    }

    // Furniture / house interiors.
    [HarmonyPatch(typeof(ResourceControl), nameof(ResourceControl.AsyncInstantiateHouseInterior))]
    internal static class HouseInteriorPatch
    {
        static bool Prefix(string interior_path, Il2CppSystem.Action<GameObject> on_asset_ready)
            => TryServePrefab(InteriorPrefix + interior_path + PrefabSuffix, on_asset_ready);
    }

    // World objects placed/dropped in the world (string overload).
    [HarmonyPatch(typeof(ResourceControl), nameof(ResourceControl.AsyncInstantiateWorldObjectPrefab),
        new Type[] { typeof(string), typeof(Chunk), typeof(Il2CppSystem.Action<GameObject>) })]
    internal static class WorldObjectStringPatch
    {
        static bool Prefix(string obj_path, Il2CppSystem.Action<GameObject> on_asset_ready)
            => TryServePrefab(PrefabPrefix + obj_path + PrefabSuffix, on_asset_ready);
    }

    // World objects resolved from an InventoryItem. Also drives 3D inventory-icon previews
    // (ItemScreenshotTaker). Matches our items by name (robust) and falls back to World_obj_path.
    [HarmonyPatch(typeof(ResourceControl), nameof(ResourceControl.AsyncInstantiateWorldObjectPrefab),
        new Type[] { typeof(InventoryItem), typeof(Chunk), typeof(Il2CppSystem.Action<GameObject>) })]
    internal static class WorldObjectItemPatch
    {
        static bool Prefix(InventoryItem item, Il2CppSystem.Action<GameObject> on_asset_ready)
        {
            try
            {
                if (item == null) return true;

                if (AssetBundleManager.Instance.TryGetKeyForItemName(item.item_name, out var mappedKey))
                    return TryServePrefab(mappedKey, on_asset_ready);

                // Fallback: replicate the game's own resolution from the item file.
                if (item.GetString("custom_type") == "furniture") return true;
                string path = item.GetString("World_obj_path");
                if (string.IsNullOrEmpty(path)) return true;

                string key = PrefabPrefix + path + PrefabSuffix;
                if (!AssetBundleManager.Instance.IsPrefabKey(key)) return true;

                return TryServePrefab(key, on_asset_ready);
            }
            catch (Exception ex)
            {
                AssetBundleManager.LogError($"WorldObjectItemPatch failed, deferring to game: {ex}");
                return true;
            }
        }
    }

    // 2D inventory icons / sprites. sprite_path is already the full key (built by AssignItemSprite,
    // AssignCreatureSprite, AssignPerkSprite, ...).
    [HarmonyPatch(typeof(ResourceControl), "LoadAndAssignSprite",
        new Type[] { typeof(string), typeof(Image), typeof(Il2CppSystem.Action) })]
    internal static class LoadAndAssignSpritePatch
    {
        static bool Prefix(string sprite_path, Image img, Il2CppSystem.Action on_asset_ready)
        {
            try
            {
                return AssetBundleManager.Instance.TryServeSprite(sprite_path, img, on_asset_ready) ? false : true;
            }
            catch (Exception ex)
            {
                AssetBundleManager.LogError($"LoadAndAssignSpritePatch failed, deferring to game: {ex}");
                return true;
            }
        }
    }
}
