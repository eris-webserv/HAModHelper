namespace HAModHelper.GamePlugin.Entities.Interfaces;

/// <summary>
/// Defines public operations for registering mod-triggerable abilities on companions.
/// </summary>
public interface ICompanionAbilityManager
{
    /// <summary>
    /// Registers an ability that can later be triggered on a companion.
    /// </summary>
    /// <param name="abilityId">Unique ability identifier.</param>
    /// <param name="displayName">Human-readable name shown in logs/UI.</param>
    /// <param name="onTrigger">Callback invoked with the target companion when the ability fires.</param>
    void RegisterAbility(string abilityId, string displayName, Action<ActiveCompanion> onTrigger);

    /// <summary>Triggers a specific registered ability on a companion.</summary>
    /// <returns><c>true</c> if the ability was found and triggered; otherwise, <c>false</c>.</returns>
    bool TriggerAbility(string abilityId, ActiveCompanion companion);

    /// <summary>Gets the IDs of all currently registered abilities, in registration order.</summary>
    IReadOnlyList<string> RegisteredAbilityIds { get; }
}

/// <summary>
/// Defines public operations for registering custom merchant/vendor types that companions
/// can be converted into via <c>CompanionMerchantWindow</c>.
/// </summary>
public interface ICompanionShopManager
{
    /// <summary>
    /// Registers a custom vendor type to appear in the companion merchant picker.
    /// </summary>
    /// <param name="fileName">Internal vendor type identifier, as used by <c>CompanionMerchantWindow.AddVendor</c>.</param>
    /// <param name="visualName">Display name shown in the vendor picker.</param>
    /// <param name="gemCost">Gem cost to convert a companion into this vendor type.</param>
    /// <param name="item1">First item requirement for the conversion.</param>
    /// <param name="item2">Second item requirement for the conversion.</param>
    /// <param name="overwriteDescription">Optional description override; empty uses the game's generic description.</param>
    /// <param name="addMoreExpensiveStr">Whether to append the "more expensive" qualifier to the description.</param>
    void RegisterVendorType(string fileName, string visualName, int gemCost, ItemCountPair item1, ItemCountPair item2, string overwriteDescription = "", bool addMoreExpensiveStr = false);
}
