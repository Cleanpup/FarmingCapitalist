using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Menus;

namespace FarmingCapitalist
{
    /// <summary>
    /// Contains Harmony patches that intercept behavior in <see cref="ShopMenu"/>.
    ///
    /// Purpose & approach:
    /// - We create a single static class to register Harmony patches at runtime.
    /// - The class keeps a reference to the mod's <see cref="IMonitor"/> so it can log
    ///   useful diagnostics (finding methods, errors while patching, runtime events).
    /// - Patches are applied via reflection so the code tolerates method visibility
    ///   (public/non-public) changes between game versions.
    ///
    /// Notes about divergence from the reference example:
    /// - The provided example shows a pattern for patching a specific instance method
    ///   (e.g. a static prefix method with typed parameters). Here we use reflection to
    ///   locate the target method and a small, generic prefix that inspects the
    ///   original method's parameter metadata and argument array. This gives us a
    ///   flexible way to modify arguments without tightly coupling to a particular
    ///   method signature. That flexibility is useful when modding across Stardew
    ///   Valley versions where signatures sometimes change.
    /// - Using a generic argument array approach means we must be careful with
    ///   argument types and indexes; the code below documents how we find and change
    ///   the first integer parameter we encounter (commonly the purchase amount).
    /// </summary>
    internal static class ShopMenuPatches
    {
        /*
         * Fields
         */
        // Monitor: used for logging (IMonitor is provided by SMAPI). We store it
        // so all methods in this static class can write diagnostic messages to the
        // SMAPI console / log file.
        private static IMonitor Monitor;

        // harmony: the Harmony instance used to apply/undo patches. Keeping a
        // reference can be useful if we later want to unpatch.
        private static Harmony harmony;

        /*
         * Initialization
         */
        /// <summary>
        /// Called from the mod entry point to register Harmony patches for <see cref="ShopMenu"/>.
        /// </summary>
        /// <param name="monitor">The mod monitor used for logging.</param>
        internal static void Initialize(IMonitor monitor)
        {
            // Save the monitor so the patch methods can log.
            Monitor = monitor;

            try
            {
                // Create a Harmony instance. The ID should be unique to avoid collisions
                // with other mods that may also patch the same methods.
                harmony = new Harmony("cleanpup.farmingcapitalist.shopmenu");

                // Locate the method we want to patch: ShopMenu.tryToPurchaseItem.
                // We search for instance methods and include non-public members so
                // the patch works even if the method is internal/private in some builds.
                var original = typeof(ShopMenu).GetMethod(
                    "tryToPurchaseItem",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                // If the method can't be found, log an error and don't attempt to patch.
                if (original == null)
                {
                    Monitor.Log("Failed to find ShopMenu.tryToPurchaseItem method.", LogLevel.Error);
                    return;
                }

                // Find the prefix method declared below. We mark it non-public because
                // it doesn't need to be part of the public API — Harmony only needs a MethodInfo.
                var prefix = typeof(ShopMenuPatches).GetMethod(
                    nameof(TryToPurchase_Prefix),
                    BindingFlags.Static | BindingFlags.NonPublic);

                // Apply the prefix patch. After this, TryToPurchase_Prefix will run before
                // the original method and can inspect/modify arguments.
                harmony.Patch(original, prefix: new HarmonyMethod(prefix));

                Monitor.Log("Patched ShopMenu.tryToPurchaseItem", LogLevel.Info);
            }
            catch (Exception ex)
            {
                // Catch any reflection/Harmony errors and log them to help debugging.
                Monitor.Log($"Failed to apply ShopMenu patches: {ex}", LogLevel.Error);
            }
        }

        /*
         * Prefix
         */
        /// <summary>
        /// A Harmony prefix that runs before <see cref="ShopMenu.tryToPurchaseItem"/>.
        ///
        /// Why this signature?
        /// - Harmony supports several prefix signatures. Using <c>MethodBase __originalMethod</c>
        ///   gives us access to metadata about the method being executed (its name and
        ///   parameters). Using <c>object[] __args</c> lets us inspect and modify the
        ///   runtime argument values before the original method sees them.
        ///
        /// What it does in this mod:
        /// - The example implementation locates the first integer parameter of the
        ///   original method and sets that argument to 1. In Stardew's shop code this
        ///   commonly corresponds to the purchase amount. This is intentionally simple
        ///   and is intended as a clear demonstration of how to intercept and change
        ///   arguments; you can replace the logic to modify price, cancel purchases,
        ///   or change return values as needed.
        ///
        /// Safety notes:
        /// - Because we rely on the first int parameter we find, if the method
        ///   signature changes in a future game version this may modify the wrong
        ///   value. When targeting production mods, prefer matching parameters by
        ///   name and type or patching with a strongly-typed prefix where possible.
        /// - Modifying __args directly is powerful but risky — ensure the replacement
        ///   values are the correct type and within expected ranges.
        /// </summary>
        /// <param name="__originalMethod">Reflection metadata for the method being called.</param>
        /// <param name="__args">The runtime arguments for the method; modifying this array
        /// changes what the original method receives.</param>
        private static void TryToPurchase_Prefix(MethodBase __originalMethod, object[] __args)
        {
            try
            {
                // Quick sanity checks: if we don't have method metadata or no arguments,
                // there's nothing for us to modify.
                if (__originalMethod == null || __args == null || __args.Length == 0)
                    return;

                // Get parameter metadata so we can correlate __args indexes with types/names.
                var parms = __originalMethod.GetParameters();

                // Iterate parameters and find the first integer parameter. This is a
                // conservative approach: it won't try to modify multiple parameters and
                // avoids assumptions about parameter ordering beyond "first int".
                for (int i = 0; i < parms.Length; i++)
                {
                    // If the parameter type is exactly int, we'll change the corresponding argument.
                    if (parms[i].ParameterType == typeof(int))
                    {
                        // Example modification: set the int argument to 1.
                        // In practice you might compute a different value based on the
                        // player, item, or other game state.
                        __args[i] = 1;

                        // Log at Trace level so normal users don't see this unless they
                        // enable verbose logging; it helps while developing or debugging.
                        Monitor.Log($"Modified argument '{parms[i].Name}' (int) to 1 in {__originalMethod.Name}.", LogLevel.Trace);
                        break; // only modify the first int parameter we find
                    }
                }
            }
            catch (Exception ex)
            {
                // If anything goes wrong while inspecting/modifying args, log the
                // error and allow the original method to continue unaffected.
                Monitor.Log($"Failed in {nameof(TryToPurchase_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }
    }
}
