using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// Harmony glue for economy-related runtime bridges.
    /// - Explicitly registers a postfix for Object.sellToStorePrice to allow
    ///   mods to adjust the sell price before the game uses it.
    /// </summary>
    internal static class EconomyPatches
    {
        internal static IMonitor? Monitor;
        private static Harmony? _harmony;

        internal static void Initialize(IMonitor monitor, string harmonyId)
        {
            Monitor = monitor;
            EconomyService.Monitor = monitor;
            _harmony = new Harmony(harmonyId);

            try
            {
                // Locate target: Object.sellToStorePrice(long specificPlayerID = -1L)
                var target = AccessTools.Method(typeof(StardewValley.Object), "sellToStorePrice", new Type[] { typeof(long) });
                var postfix = AccessTools.Method(typeof(EconomyPatches), nameof(SellToStorePrice_Postfix));

                if (target == null || postfix == null)
                {
                    Monitor?.Log("Failed to find sellToStorePrice or postfix.", LogLevel.Error);
                    return;
                }

                _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                Monitor?.Log("Patched StardewValley.Object.sellToStorePrice (explicit postfix).", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed to apply sellToStorePrice patch: {ex}", LogLevel.Error);
            }
        }

        // Postfix bridge: receives vanilla computed value, calls EconomyService, writes back.
        // Signature includes 'long specificPlayerID' to match the original parameter.
        private static void SellToStorePrice_Postfix(StardewValley.Object __instance, long specificPlayerID, ref int __result)
        {
            try
            {
                int vanilla = __result;
                int adjusted = EconomyService.AdjustSellPrice(vanilla);

                // Clamp sell result to >= 0 (allow selling for 0 if needed).
                __result = Math.Max(0, adjusted);
                Monitor?.Log($"SellToStorePrice_Postfix: {vanilla} -> {__result} for {__instance?.Name}", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"SellToStorePrice_Postfix exception: {ex}", LogLevel.Error);
            }
        }

        internal static void Unload()
        {
            try
            {
                _harmony?.UnpatchAll(_harmony.Id);
                Monitor?.Log("Unpatched EconomyPatches.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed to unpatch EconomyPatches: {ex}", LogLevel.Error);
            }
        }
    }
}
