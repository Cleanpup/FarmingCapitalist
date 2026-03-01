using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace FarmingCapitalist
{
    /// <summary>
    /// Harmony patch registration and helpers for shop-related behavior.
    ///
    /// This refactor replaces reflection-based, argument-array patches with
    /// strongly-typed Harmony patches. Advantages:
    /// - Clear, explicit parameter names and types (safer than inspecting __args)
    /// - Easier to read and maintain
    /// - Business logic separated from Harmony glue for testability
    /// - Uses <c>ref</c> parameters to safely modify primitive arguments
    /// </summary>
    internal static class ShopMenuPatches
    {
        // Exposed as internal so other classes (patches, tests) can log via the mod's monitor.
        internal static IMonitor? Monitor;

        // Harmony instance to manage patch lifecycle (apply/unapply). Nullable to
        // reflect that Initialize may not have run yet or patching failed.
        private static Harmony? _harmony;

        /// <summary>
        /// Initialize and apply Harmony patches for this mod.
        /// Called from the mod entry point (ModEntry) during startup.
        /// </summary>
        /// <param name="monitor">SMAPI monitor for logging.</param>
        internal static void Initialize(IMonitor monitor, string harmonyId)
        {
            Monitor = monitor;
            _harmony = new Harmony(harmonyId);

            try
            {
                var target = AccessTools.Method(typeof(ShopMenu), "tryToPurchaseItem");
                var prefix = AccessTools.Method(typeof(TryToPurchasePatch), nameof(TryToPurchasePatch.Prefix));

                if (target == null || prefix == null)
                {
                    Monitor?.Log("Failed to find ShopMenu.tryToPurchaseItem or Prefix.", LogLevel.Error);
                    return;
                }

                _harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                Monitor?.Log("Patched ShopMenu.tryToPurchaseItem (explicit).", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed to apply Harmony patches: {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Unpatch any patches applied by this Harmony instance. Call when the mod is
        /// unloaded to avoid leaving hooks in the game.
        /// </summary>
        internal static void Unload()
        {
            try
            {
                _harmony?.UnpatchAll(_harmony.Id);
                Monitor?.Log("Removed Harmony patches for FarmingCapitalist.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed to unpatch Harmony patches: {ex}", LogLevel.Error);
            }
        }
    }

    /*
     * Strongly-typed Harmony patch
     *
     * Rationale:
     * - We declare the target by attribute which makes the intent explicit.
     * - The Prefix signature mirrors the original method where possible so parameters
     *   can be modified using 'ref' rather than editing a raw object[] array.
     * - Keep the prefix minimal: delegate to a business-logic helper to make the
     *   patch easy to read and unit test.
     */
    // Minimal explicit patch class. We locate this Prefix via AccessTools in Initialize
    // which avoids attribute scanning and keeps registration centralized.
    internal static class TryToPurchasePatch
    {
        internal static void Prefix(ref int stockToBuy)
        {
            try
            {
                PurchaseLogic.ForceSinglePurchase(ref stockToBuy, ShopMenuPatches.Monitor);
            }
            catch (Exception ex)
            {
                ShopMenuPatches.Monitor?.Log($"TryToPurchasePatch.Prefix exception: {ex}", LogLevel.Error);
            }
        }
    }

    /// <summary>
    /// Business logic for purchase manipulation. Kept separate from Harmony glue
    /// so it can be unit-tested and evolved without changing patch declarations.
    /// </summary>
    internal static class PurchaseLogic
    {
        /// <summary>
        /// Example policy: force purchases to a single item by mutating <paramref name="stockToBuy"/>.
        /// Replace or extend this method with any logic you need (player perms, item checks, price mods).
        /// </summary>
        /// <param name="stockToBuy">The requested purchase quantity; modified in-place.</param>
        /// <param name="monitor">Optional logger for diagnostics.</param>
        internal static void ForceSinglePurchase(ref int stockToBuy, IMonitor monitor = null)
        {
            // Defensive: only change when necessary to reduce log noise and side effects.
            if (stockToBuy != 1)
            {
                monitor?.Log($"Forcing stockToBuy {stockToBuy} -> 1", LogLevel.Trace);
                stockToBuy = 1;
            }
        }
    }
}
