using HarmonyLib;
using HAModHelper.GamePlugin.Core;
using HAModHelper.GamePlugin.Entities.Systems;

namespace HAModHelper.GamePlugin.Entities.Patches;

/// <summary>
/// Injects mod-registered vendor types into the companion merchant picker after the vanilla
/// vendor list is built, then replicates the pagination/redraw work <c>Start()</c> already did
/// for the vanilla-only count so the new entries actually show up.
/// </summary>
/// <remarks>
/// Start() calls AddVendor 24 times for the vanilla vendor types, then -- only after all of
/// them -- computes <c>final_page = (vendor_types.Count - 1) / 6</c> and calls RedrawPage()/
/// RedrawHeader() itself. A postfix that only calls AddVendor for custom vendors would leave
/// final_page and the on-screen page stuck at the vanilla count, so this replicates that
/// follow-up work too. vendor_types (a List of a private nested struct), AddVendor, final_page,
/// RedrawPage, and RedrawHeader are all private, so this all goes through reflection.
/// </remarks>
[HarmonyPatch(typeof(CompanionMerchantWindow), "Start")]
public static class CompanionMerchantWindow_Start_Patch
{
    [HarmonyPostfix]
    public static void Postfix(CompanionMerchantWindow __instance)
    {
        var customVendors = CompanionShopManager.Instance.GetRegisteredVendorTypes();
        if (customVendors.Count == 0) return;

        var addVendor = AccessTools.Method(typeof(CompanionMerchantWindow), "AddVendor");
        var vendorTypesField = AccessTools.Field(typeof(CompanionMerchantWindow), "vendor_types");
        var finalPageField = AccessTools.Field(typeof(CompanionMerchantWindow), "final_page");
        var redrawPage = AccessTools.Method(typeof(CompanionMerchantWindow), "RedrawPage");
        var redrawHeader = AccessTools.Method(typeof(CompanionMerchantWindow), "RedrawHeader");

        if (addVendor == null || vendorTypesField == null || finalPageField == null || redrawPage == null || redrawHeader == null)
        {
            try { HAMHMod.Logger.LogError("[HAMH] CompanionMerchantWindow's private members changed shape; custom vendor types were not injected."); } catch { }
            return;
        }

        foreach (var vendor in customVendors)
        {
            addVendor.Invoke(__instance, new object?[] { vendor.FileName, vendor.VisualName, vendor.GemCost, vendor.Item1, vendor.Item2, vendor.OverwriteDescription, vendor.AddMoreExpensiveStr });
        }

        var vendorTypesList = (System.Collections.ICollection)vendorTypesField.GetValue(__instance)!;
        finalPageField.SetValue(__instance, (vendorTypesList.Count - 1) / 6);

        redrawPage.Invoke(__instance, null);
        redrawHeader.Invoke(__instance, null);
    }
}

/// <summary>
/// Repurposes the companion panel's "coming soon" command button to trigger mod-registered
/// companion abilities, cycling through them on each press.
/// </summary>
/// <remarks>
/// PressCommandComingSoon is wired to 4 live buttons in COMPANION-commands.prefab and normally
/// shows a real "not implemented yet" popup via PopupControl.ShowMessage -- it's the game's own
/// placeholder for future companion commands, not dead code. When no abilities are registered,
/// or no companion is selected, this falls through to that vanilla behavior unchanged.
/// </remarks>
[HarmonyPatch(typeof(CompanionController), nameof(CompanionController.PressCommandComingSoon))]
public static class CompanionController_PressCommandComingSoon_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(CompanionController __instance)
    {
        var manager = CompanionAbilityManager.Instance;
        if (manager.RegisteredAbilityIds.Count == 0) return true;

        var companion = __instance.GetCurrSelectedCompanion();
        if (companion == null) return true;

        return !manager.TriggerNextAbility(companion);
    }
}
