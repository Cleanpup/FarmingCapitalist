using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

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
        internal static EconomyContext? FrozenOvernightSellContext;
        private static Harmony? _harmony;

        internal static void Initialize(IMonitor monitor, string harmonyId)
        {
            Monitor = monitor;
            EconomyService.Monitor = monitor;
            EconomyContextBuilder.Monitor = monitor;
            DailyPurchaseTracker.Monitor = monitor;
            ShopPriceRuntimeService.Monitor = monitor;
            CropTraitService.Monitor = monitor;
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
                Monitor?.Log("Patched StardewValley.Object.sellToStorePrice (explicit postfix).", LogLevel.Trace);

                var shopPurchaseTarget = AccessTools.Method(
                    typeof(ShopMenu),
                    "tryToPurchaseItem",
                    new[] { typeof(ISalable), typeof(ISalable), typeof(int), typeof(int), typeof(int) }
                );
                var shopPurchasePrefix = AccessTools.Method(typeof(EconomyPatches), nameof(ShopMenu_TryToPurchaseItem_Prefix));
                var shopPurchasePostfix = AccessTools.Method(typeof(EconomyPatches), nameof(ShopMenu_TryToPurchaseItem_Postfix));

                if (shopPurchaseTarget == null || shopPurchasePrefix == null || shopPurchasePostfix == null)
                {
                    Monitor?.Log("Failed to find ShopMenu.tryToPurchaseItem patch methods.", LogLevel.Error);
                    return;
                }

                _harmony.Patch(
                    shopPurchaseTarget,
                    prefix: new HarmonyMethod(shopPurchasePrefix),
                    postfix: new HarmonyMethod(shopPurchasePostfix)
                );
                Monitor?.Log("Patched ShopMenu.tryToPurchaseItem for bulk-buy pricing preview.", LogLevel.Info);

                var chargePlayerTarget = AccessTools.Method(
                    typeof(ShopMenu),
                    "chargePlayer",
                    new[] { typeof(Farmer), typeof(int), typeof(int) }
                );
                var chargePlayerPostfix = AccessTools.Method(typeof(EconomyPatches), nameof(ShopMenu_ChargePlayer_Postfix));
                if (chargePlayerTarget == null || chargePlayerPostfix == null)
                {
                    Monitor?.Log("Failed to find ShopMenu.chargePlayer patch methods.", LogLevel.Error);
                    return;
                }

                _harmony.Patch(chargePlayerTarget, postfix: new HarmonyMethod(chargePlayerPostfix));
                Monitor?.Log("Patched ShopMenu.chargePlayer to confirm purchase completion.", LogLevel.Trace);
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
                EconomyContext context;
                if (FrozenOvernightSellContext != null)
                {
                    context = FrozenOvernightSellContext;
                    Monitor?.Log(
                        $"SellToStorePrice_Postfix: using frozen overnight context ({context.Season} {context.DayOfMonth}).",
                        LogLevel.Trace
                    );
                }
                else
                {
                    context = EconomyContextBuilder.Build(shopkeeperName: null, monitor: Monitor);
                }

                int adjusted = EconomyService.AdjustSellPrice(vanilla, __instance, context);

                // Clamp sell result to >= 0 (allow selling for 0 if needed).
                __result = Math.Max(0, adjusted);
                Monitor?.Log($"SellToStorePrice_Postfix: {vanilla} -> {__result} for {__instance?.Name}", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"SellToStorePrice_Postfix exception: {ex}", LogLevel.Error);
            }
        }

        private static void ShopMenu_TryToPurchaseItem_Prefix(ShopMenu __instance, ISalable item, int stockToBuy)
        {
            try
            {
                ShopPriceRuntimeService.BeginPurchaseAttempt(__instance, item, stockToBuy);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"ShopMenu_TryToPurchaseItem_Prefix exception: {ex}", LogLevel.Error);
            }
        }

        private static void ShopMenu_TryToPurchaseItem_Postfix(ShopMenu __instance)
        {
            try
            {
                ShopPriceRuntimeService.CompletePurchaseAttempt(__instance);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"ShopMenu_TryToPurchaseItem_Postfix exception: {ex}", LogLevel.Error);
            }
        }

        private static void ShopMenu_ChargePlayer_Postfix(Farmer who, int currencyType, int amount)
        {
            _ = who;
            _ = currencyType;

            try
            {
                ShopPriceRuntimeService.MarkPurchaseCharged(amount);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"ShopMenu_ChargePlayer_Postfix exception: {ex}", LogLevel.Error);
            }
        }

        internal static void Unload()
        {
            try
            {
                _harmony?.UnpatchAll(_harmony.Id);
                Monitor?.Log("Unpatched EconomyPatches.", LogLevel.Info);

                // Clear monitors to avoid holding references after unload
                EconomyService.Monitor = null;
                EconomyContextBuilder.Monitor = null;
                DailyPurchaseTracker.Monitor = null;
                ShopPriceRuntimeService.Monitor = null;
                CropTraitService.Monitor = null;
                ShopPriceRuntimeService.Clear();
                FrozenOvernightSellContext = null;
                Monitor = null;
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed to unpatch EconomyPatches: {ex}", LogLevel.Error);
            }
        }
    }
}
