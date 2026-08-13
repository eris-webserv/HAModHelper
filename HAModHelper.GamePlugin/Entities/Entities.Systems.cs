using HAModHelper.GamePlugin.Base.Events;
using HAModHelper.GamePlugin.Entities.Events;
using HAModHelper.GamePlugin.Entities.Interfaces;

namespace HAModHelper.GamePlugin.Entities.Systems;

/// <summary>
/// Lets mods register abilities that can be triggered on a player's companion, and trigger them
/// programmatically. Since the vanilla UI has no picker for multiple mod-registered abilities,
/// they're also wired into the companion panel's unused "coming soon" command button, which
/// cycles through registered abilities on the currently selected companion each press.
/// </summary>
public sealed class CompanionAbilityManager : ICompanionAbilityManager
{
    public static CompanionAbilityManager Instance { get; } = new CompanionAbilityManager();

    private readonly Dictionary<string, (string DisplayName, Action<ActiveCompanion> OnTrigger)> _abilities = new();
    private readonly List<string> _abilityOrder = new();
    private int _nextAbilityIndex;

    private CompanionAbilityManager() { }

    /// <summary>TEST-ONLY: Reset system state.</summary>
    public void Reset()
    {
        _abilities.Clear();
        _abilityOrder.Clear();
        _nextAbilityIndex = 0;
    }

    /// <summary>Initialize the companion ability manager (called on game start).</summary>
    public void Initialize()
    {
    }

    /// <inheritdoc />
    public void RegisterAbility(string abilityId, string displayName, Action<ActiveCompanion> onTrigger)
    {
        if (!_abilities.ContainsKey(abilityId))
        {
            _abilityOrder.Add(abilityId);
        }
        _abilities[abilityId] = (displayName, onTrigger);
    }

    /// <inheritdoc />
    public bool TriggerAbility(string abilityId, ActiveCompanion companion)
    {
        if (companion == null) return false;
        if (!_abilities.TryGetValue(abilityId, out var ability)) return false;

        ability.OnTrigger(companion);
        EventBus.Instance.Fire(new AbilityTriggeredEvent(abilityId, companion));
        return true;
    }

    /// <summary>
    /// Triggers the next registered ability in rotation on a companion. Used by the
    /// companion panel's "coming soon" button hook.
    /// </summary>
    public bool TriggerNextAbility(ActiveCompanion companion)
    {
        if (_abilityOrder.Count == 0) return false;

        var abilityId = _abilityOrder[_nextAbilityIndex % _abilityOrder.Count];
        _nextAbilityIndex++;
        return TriggerAbility(abilityId, companion);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> RegisteredAbilityIds => _abilityOrder;

    /// <summary>Gets the display name registered for an ability ID, or <c>null</c> if unregistered.</summary>
    public string? GetDisplayName(string abilityId)
        => _abilities.TryGetValue(abilityId, out var ability) ? ability.DisplayName : null;
}

/// <summary>A custom merchant/vendor type to be injected into <c>CompanionMerchantWindow</c>'s picker.</summary>
public readonly record struct VendorType(
    string FileName,
    string VisualName,
    int GemCost,
    ItemCountPair Item1,
    ItemCountPair Item2,
    string OverwriteDescription,
    bool AddMoreExpensiveStr);

/// <summary>
/// Lets mods register custom merchant/vendor types that companions can be converted into.
/// Thin registry wrapper around <c>CompanionMerchantWindow</c>'s vendor-type list.
/// </summary>
public sealed class CompanionShopManager : ICompanionShopManager
{
    public static CompanionShopManager Instance { get; } = new CompanionShopManager();

    private readonly List<VendorType> _customVendorTypes = new();

    private CompanionShopManager() { }

    /// <summary>TEST-ONLY: Reset system state.</summary>
    public void Reset()
    {
        _customVendorTypes.Clear();
    }

    /// <summary>Initialize the companion shop manager (called on game start).</summary>
    public void Initialize()
    {
    }

    /// <inheritdoc />
    public void RegisterVendorType(string fileName, string visualName, int gemCost, ItemCountPair item1, ItemCountPair item2, string overwriteDescription = "", bool addMoreExpensiveStr = false)
    {
        _customVendorTypes.Add(new VendorType(fileName, visualName, gemCost, item1, item2, overwriteDescription, addMoreExpensiveStr));
    }

    /// <summary>Gets all custom vendor types registered so far.</summary>
    public IReadOnlyList<VendorType> GetRegisteredVendorTypes() => _customVendorTypes;
}
