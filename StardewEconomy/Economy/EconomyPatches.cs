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

                var showEndOfNightTarget = AccessTools.Method(typeof(Game1), nameof(Game1.showEndOfNightStuff));
                var showEndOfNightPrefix = AccessTools.Method(typeof(EconomyPatches), nameof(Game1_ShowEndOfNightStuff_Prefix));
                if (showEndOfNightTarget == null || showEndOfNightPrefix == null)
                {
                    Monitor?.Log("Failed to find Game1.showEndOfNightStuff patch methods.", LogLevel.Error);
                    return;
                }

                _harmony.Patch(showEndOfNightTarget, prefix: new HarmonyMethod(showEndOfNightPrefix));
                Monitor?.Log("Patched Game1.showEndOfNightStuff for completed shipping tracking.", LogLevel.Trace);

                var receiveLeftClickTarget = AccessTools.Method(
                    typeof(ShopMenu),
                    nameof(ShopMenu.receiveLeftClick),
                    new[] { typeof(int), typeof(int), typeof(bool) }
                );
                var receiveLeftClickPrefix = AccessTools.Method(typeof(EconomyPatches), nameof(ShopMenu_ReceiveLeftClick_Prefix));
                var receiveLeftClickPostfix = AccessTools.Method(typeof(EconomyPatches), nameof(ShopMenu_ReceiveLeftClick_Postfix));
                if (receiveLeftClickTarget == null || receiveLeftClickPrefix == null || receiveLeftClickPostfix == null)
                {
                    Monitor?.Log("Failed to find ShopMenu.receiveLeftClick patch methods.", LogLevel.Error);
                    return;
                }

                _harmony.Patch(
                    receiveLeftClickTarget,
                    prefix: new HarmonyMethod(receiveLeftClickPrefix),
                    postfix: new HarmonyMethod(receiveLeftClickPostfix)
                );
                Monitor?.Log("Patched ShopMenu.receiveLeftClick for completed sell tracking.", LogLevel.Trace);

                var receiveRightClickTarget = AccessTools.Method(
                    typeof(ShopMenu),
                    nameof(ShopMenu.receiveRightClick),
                    new[] { typeof(int), typeof(int), typeof(bool) }
                );
                var receiveRightClickPrefix = AccessTools.Method(typeof(EconomyPatches), nameof(ShopMenu_ReceiveRightClick_Prefix));
                var receiveRightClickPostfix = AccessTools.Method(typeof(EconomyPatches), nameof(ShopMenu_ReceiveRightClick_Postfix));
                if (receiveRightClickTarget == null || receiveRightClickPrefix == null || receiveRightClickPostfix == null)
                {
                    Monitor?.Log("Failed to find ShopMenu.receiveRightClick patch methods.", LogLevel.Error);
                    return;
                }

                _harmony.Patch(
                    receiveRightClickTarget,
                    prefix: new HarmonyMethod(receiveRightClickPrefix),
                    postfix: new HarmonyMethod(receiveRightClickPostfix)
                );
                Monitor?.Log("Patched ShopMenu.receiveRightClick for partial-stack sell tracking.", LogLevel.Trace);
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
                VerbosePriceTraceLogger.Log($"SellToStorePrice_Postfix: {vanilla} -> {__result} for {__instance?.Name}");
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

        private static void Game1_ShowEndOfNightStuff_Prefix()
        {
            try
            {
                if (!Context.IsWorldReady || Game1.player?.displayedShippedItems is null || Game1.player.displayedShippedItems.Count == 0)
                    return;

                CropSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Game1_ShowEndOfNightStuff_Prefix exception: {ex}", LogLevel.Error);
            }
        }

        private static void ShopMenu_ReceiveLeftClick_Prefix(ShopMenu __instance, int x, int y, bool playSound, out PendingShopSaleState __state)
        {
            _ = playSound;
            __state = CapturePendingShopSale(__instance, x, y);
        }

        private static void ShopMenu_ReceiveLeftClick_Postfix(ShopMenu __instance, PendingShopSaleState __state)
        {
            CompletePendingShopSale(__instance, __state, "sell-left-click");
        }

        private static void ShopMenu_ReceiveRightClick_Prefix(ShopMenu __instance, int x, int y, bool playSound, out PendingShopSaleState __state)
        {
            _ = playSound;
            __state = CapturePendingShopSale(__instance, x, y);
        }

        private static void ShopMenu_ReceiveRightClick_Postfix(ShopMenu __instance, PendingShopSaleState __state)
        {
            CompletePendingShopSale(__instance, __state, "sell-right-click");
        }

        private static PendingShopSaleState CapturePendingShopSale(ShopMenu shopMenu, int x, int y)
        {
            try
            {
                if (shopMenu.readOnly || shopMenu.heldItem != null || shopMenu.onSell != null || shopMenu.currency != 0)
                    return PendingShopSaleState.None;

                int inventoryIndex = shopMenu.inventory.getInventoryPositionOfClick(x, y);
                if (inventoryIndex < 0 || inventoryIndex >= shopMenu.inventory.actualInventory.Count)
                    return PendingShopSaleState.None;

                Item? clickedItem = shopMenu.inventory.actualInventory[inventoryIndex];
                if (!CropSupplyTracker.TryGetCropProduceInfo(clickedItem, out string produceItemId, out string displayName))
                    return PendingShopSaleState.None;

                return new PendingShopSaleState(
                    ShouldTrack: true,
                    SlotIndex: inventoryIndex,
                    QualifiedItemId: clickedItem!.QualifiedItemId,
                    ProduceItemId: produceItemId,
                    DisplayName: displayName,
                    OriginalStack: clickedItem.Stack
                );
            }
            catch (Exception ex)
            {
                Monitor?.Log($"CapturePendingShopSale exception: {ex}", LogLevel.Error);
                return PendingShopSaleState.None;
            }
        }

        private static void CompletePendingShopSale(ShopMenu shopMenu, PendingShopSaleState state, string interactionKind)
        {
            if (!state.ShouldTrack)
                return;

            try
            {
                Item? remainingItem = state.SlotIndex >= 0 && state.SlotIndex < shopMenu.inventory.actualInventory.Count
                    ? shopMenu.inventory.actualInventory[state.SlotIndex]
                    : null;
                int remainingStack = remainingItem is not null
                    && string.Equals(remainingItem.QualifiedItemId, state.QualifiedItemId, StringComparison.OrdinalIgnoreCase)
                        ? remainingItem.Stack
                        : 0;
                int soldQuantity = Math.Max(0, state.OriginalStack - remainingStack);

                if (soldQuantity <= 0)
                    return;

                string shopId = string.IsNullOrWhiteSpace(shopMenu.ShopId)
                    ? "<unknown>"
                    : shopMenu.ShopId;
                CropSupplyTracker.TrackProduceSale(
                    state.ProduceItemId,
                    state.DisplayName,
                    soldQuantity,
                    $"shop:{shopId}:{interactionKind}"
                );
            }
            catch (Exception ex)
            {
                Monitor?.Log($"CompletePendingShopSale exception: {ex}", LogLevel.Error);
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
                CropSupplyDataService.ClearActiveData();
                Monitor = null;
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed to unpatch EconomyPatches: {ex}", LogLevel.Error);
            }
        }

        private readonly record struct PendingShopSaleState(
            bool ShouldTrack,
            int SlotIndex,
            string QualifiedItemId,
            string ProduceItemId,
            string DisplayName,
            int OriginalStack
        )
        {
            public static PendingShopSaleState None { get; } = new(
                ShouldTrack: false,
                SlotIndex: -1,
                QualifiedItemId: string.Empty,
                ProduceItemId: string.Empty,
                DisplayName: string.Empty,
                OriginalStack: 0
            );
        }
    }
}
