using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace FarmingCapitalist
{
    /// <summary>
    /// Harmony glue for economy-related runtime bridges.
    /// - Explicitly registers postfixes for Object and base Item sellToStorePrice
    ///   so non-object sellable items can participate in live sell pricing.
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
            CropTraitService.Monitor = monitor;
            FishTraitService.Monitor = monitor;
            MineralTraitService.Monitor = monitor;
            AnimalProductTraitService.Monitor = monitor;
            ForageableTraitService.Monitor = monitor;
            PlantExtraTraitService.Monitor = monitor;
            CraftingExtraTraitService.Monitor = monitor;
            ArtisanGoodTraitService.Monitor = monitor;
            CookingFoodTraitService.Monitor = monitor;
            MonsterLootTraitService.Monitor = monitor;
            EquipmentTraitService.Monitor = monitor;
            _harmony = new Harmony(harmonyId);

            try
            {
                var sellToStorePriceTarget = AccessTools.Method(typeof(StardewValley.Object), "sellToStorePrice", new[] { typeof(long) });
                var sellToStorePricePostfix = AccessTools.Method(typeof(EconomyPatches), nameof(SellToStorePrice_Postfix));
                if (sellToStorePriceTarget == null || sellToStorePricePostfix == null)
                {
                    Monitor?.Log("Failed to find sellToStorePrice patch methods.", LogLevel.Error);
                    return;
                }

                _harmony.Patch(sellToStorePriceTarget, postfix: new HarmonyMethod(sellToStorePricePostfix));
                Monitor?.Log("Patched StardewValley.Object.sellToStorePrice (explicit postfix).", LogLevel.Trace);

                var baseItemSellToStorePriceTarget = AccessTools.Method(typeof(Item), "sellToStorePrice", new[] { typeof(long) });
                var baseItemSellToStorePricePostfix = AccessTools.Method(typeof(EconomyPatches), nameof(BaseItemSellToStorePrice_Postfix));
                if (baseItemSellToStorePriceTarget == null || baseItemSellToStorePricePostfix == null)
                {
                    Monitor?.Log("Failed to find base Item.sellToStorePrice patch methods.", LogLevel.Error);
                    return;
                }

                _harmony.Patch(baseItemSellToStorePriceTarget, postfix: new HarmonyMethod(baseItemSellToStorePricePostfix));
                Monitor?.Log("Patched StardewValley.Item.sellToStorePrice (base-item postfix).", LogLevel.Trace);

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
                Monitor?.Log("Patched ShopMenu.tryToPurchaseItem for purchase tracking.", LogLevel.Trace);

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
                Monitor?.Log("Patched ShopMenu.chargePlayer for purchase tracking.", LogLevel.Trace);

                var showEndOfNightTarget = AccessTools.Method(typeof(Game1), nameof(Game1.showEndOfNightStuff));
                var showEndOfNightPrefix = AccessTools.Method(typeof(EconomyPatches), nameof(Game1_ShowEndOfNightStuff_Prefix));
                if (showEndOfNightTarget == null || showEndOfNightPrefix == null)
                {
                    Monitor?.Log("Failed to find Game1.showEndOfNightStuff patch methods.", LogLevel.Error);
                    return;
                }

                _harmony.Patch(showEndOfNightTarget, prefix: new HarmonyMethod(showEndOfNightPrefix));
                Monitor?.Log("Patched Game1.showEndOfNightStuff for shipping tracking.", LogLevel.Trace);

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
                Monitor?.Log("Patched ShopMenu.receiveLeftClick for sell tracking.", LogLevel.Trace);

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
                Monitor?.Log("Patched ShopMenu.receiveRightClick for sell tracking.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed to apply economy patches: {ex}", LogLevel.Error);
            }
        }

        private static void SellToStorePrice_Postfix(StardewValley.Object __instance, long specificPlayerID, ref int __result)
        {
            _ = specificPlayerID;

            try
            {
                ApplySellPriceAdjustment(__instance, ref __result, "Object.sellToStorePrice");
            }
            catch (Exception ex)
            {
                Monitor?.Log($"SellToStorePrice_Postfix exception: {ex}", LogLevel.Error);
            }
        }

        private static void BaseItemSellToStorePrice_Postfix(Item __instance, long specificPlayerID, ref int __result)
        {
            _ = specificPlayerID;

            if (__instance is StardewValley.Object)
                return;

            try
            {
                ApplySellPriceAdjustment(__instance, ref __result, "Item.sellToStorePrice");
            }
            catch (Exception ex)
            {
                Monitor?.Log($"BaseItemSellToStorePrice_Postfix exception: {ex}", LogLevel.Error);
            }
        }

        private static void ShopMenu_TryToPurchaseItem_Prefix(ShopMenu __instance, ISalable item, ISalable held_item, int stockToBuy, int x, int y)
        {
            _ = held_item;
            _ = x;
            _ = y;

            try
            {
                ShopPriceRuntimeService.BeginPurchaseAttempt(__instance, item, stockToBuy);
            }
            catch (Exception ex)
            {
                Monitor?.Log($"ShopMenu_TryToPurchaseItem_Prefix exception: {ex}", LogLevel.Error);
            }
        }

        private static void ShopMenu_TryToPurchaseItem_Postfix(ShopMenu __instance, ISalable item, int stockToBuy)
        {
            _ = item;
            _ = stockToBuy;

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
                FishSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
                MineralSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
                AnimalProductSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
                ForageableSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
                PlantExtraSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
                CraftingExtraSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
                ArtisanGoodSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
                CookingFoodSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
                MonsterLootSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
                EquipmentSupplyTracker.TrackItems(Game1.player.displayedShippedItems, "shipping-bin");
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
                    return PendingShopSaleState.CreateSkipped();

                int inventoryIndex = shopMenu.inventory.getInventoryPositionOfClick(x, y);
                if (inventoryIndex < 0 || inventoryIndex >= shopMenu.inventory.actualInventory.Count)
                    return PendingShopSaleState.CreateSkipped();

                Item? clickedItem = shopMenu.inventory.actualInventory[inventoryIndex];
                if (clickedItem is null)
                    return PendingShopSaleState.CreateSkipped();

                bool shouldTrackCrop = CropSupplyTracker.TryGetCropProduceInfo(clickedItem, out string produceItemId, out string cropDisplayName);
                bool shouldTrackFish = FishSupplyTracker.TryGetFishInfo(clickedItem, out string fishItemId, out string fishDisplayName);
                bool shouldTrackMineral = MineralSupplyTracker.TryGetMineralInfo(clickedItem, out string mineralItemId, out string mineralDisplayName);
                bool shouldTrackAnimalProduct = AnimalProductSupplyTracker.TryGetAnimalProductInfo(clickedItem, out string animalProductItemId, out string animalProductDisplayName);
                bool shouldTrackForageable = ForageableSupplyTracker.TryGetForageableInfo(clickedItem, out string forageableItemId, out string forageableDisplayName);
                bool shouldTrackPlantExtra = PlantExtraSupplyTracker.TryGetPlantExtraInfo(clickedItem, out string plantExtraItemId, out string plantExtraDisplayName);
                bool shouldTrackCraftingExtra = CraftingExtraSupplyTracker.TryGetCraftingExtraInfo(clickedItem, out string craftingExtraItemId, out string craftingExtraDisplayName);
                bool shouldTrackArtisanGood = ArtisanGoodSupplyTracker.TryGetArtisanGoodInfo(clickedItem, out string artisanGoodItemId, out string artisanGoodDisplayName);
                bool shouldTrackCookingFood = CookingFoodSupplyTracker.TryGetCookingFoodInfo(clickedItem, out string cookingFoodItemId, out string cookingFoodDisplayName);
                bool shouldTrackMonsterLoot = MonsterLootSupplyTracker.TryGetMonsterLootInfo(clickedItem, out string monsterLootItemId, out string monsterLootDisplayName);
                bool shouldTrackEquipment = EquipmentSupplyTracker.TryGetEquipmentInfo(clickedItem, out string equipmentItemId, out string equipmentDisplayName);
                if (!shouldTrackCrop && !shouldTrackFish && !shouldTrackMineral && !shouldTrackAnimalProduct && !shouldTrackForageable && !shouldTrackPlantExtra && !shouldTrackCraftingExtra && !shouldTrackArtisanGood && !shouldTrackCookingFood && !shouldTrackMonsterLoot && !shouldTrackEquipment)
                    return PendingShopSaleState.CreateSkipped();

                return new PendingShopSaleState(
                    ShouldTrackCrop: shouldTrackCrop,
                    CropProduceItemId: produceItemId,
                    CropDisplayName: cropDisplayName,
                    ShouldTrackFish: shouldTrackFish,
                    FishItemId: fishItemId,
                    FishDisplayName: fishDisplayName,
                    ShouldTrackMineral: shouldTrackMineral,
                    MineralItemId: mineralItemId,
                    MineralDisplayName: mineralDisplayName,
                    ShouldTrackAnimalProduct: shouldTrackAnimalProduct,
                    AnimalProductItemId: animalProductItemId,
                    AnimalProductDisplayName: animalProductDisplayName,
                    ShouldTrackForageable: shouldTrackForageable,
                    ForageableItemId: forageableItemId,
                    ForageableDisplayName: forageableDisplayName,
                    ShouldTrackPlantExtra: shouldTrackPlantExtra,
                    PlantExtraItemId: plantExtraItemId,
                    PlantExtraDisplayName: plantExtraDisplayName,
                    ShouldTrackCraftingExtra: shouldTrackCraftingExtra,
                    CraftingExtraItemId: craftingExtraItemId,
                    CraftingExtraDisplayName: craftingExtraDisplayName,
                    ShouldTrackArtisanGood: shouldTrackArtisanGood,
                    ArtisanGoodItemId: artisanGoodItemId,
                    ArtisanGoodDisplayName: artisanGoodDisplayName,
                    ShouldTrackCookingFood: shouldTrackCookingFood,
                    CookingFoodItemId: cookingFoodItemId,
                    CookingFoodDisplayName: cookingFoodDisplayName,
                    ShouldTrackMonsterLoot: shouldTrackMonsterLoot,
                    MonsterLootItemId: monsterLootItemId,
                    MonsterLootDisplayName: monsterLootDisplayName,
                    ShouldTrackEquipment: shouldTrackEquipment,
                    EquipmentItemId: equipmentItemId,
                    EquipmentDisplayName: equipmentDisplayName,
                    SlotIndex: inventoryIndex,
                    QualifiedItemId: clickedItem.QualifiedItemId,
                    OriginalStack: clickedItem.Stack
                );
            }
            catch (Exception ex)
            {
                Monitor?.Log($"CapturePendingShopSale exception: {ex}", LogLevel.Error);
                return PendingShopSaleState.CreateSkipped();
            }
        }

        private static void CompletePendingShopSale(ShopMenu shopMenu, PendingShopSaleState state, string interactionKind)
        {
            _ = interactionKind;

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

                string source = $"shop:{shopId}:{interactionKind}";
                if (state.ShouldTrackCrop)
                {
                    CropSupplyTracker.TrackProduceSale(
                        state.CropProduceItemId,
                        state.CropDisplayName,
                        soldQuantity,
                        source
                    );
                }

                if (state.ShouldTrackFish)
                {
                    FishSupplyTracker.TrackFishSale(
                        state.FishItemId,
                        state.FishDisplayName,
                        soldQuantity,
                        source
                    );
                }

                if (state.ShouldTrackMineral)
                {
                    MineralSupplyTracker.TrackMineralSale(
                        state.MineralItemId,
                        state.MineralDisplayName,
                        soldQuantity,
                        source
                    );
                }

                if (state.ShouldTrackAnimalProduct)
                {
                    AnimalProductSupplyTracker.TrackAnimalProductSale(
                        state.AnimalProductItemId,
                        state.AnimalProductDisplayName,
                        soldQuantity,
                        source
                    );
                }

                if (state.ShouldTrackForageable)
                {
                    ForageableSupplyTracker.TrackForageableSale(
                        state.ForageableItemId,
                        state.ForageableDisplayName,
                        soldQuantity,
                        source
                    );
                }

                if (state.ShouldTrackPlantExtra)
                {
                    PlantExtraSupplyTracker.TrackPlantExtraSale(
                        state.PlantExtraItemId,
                        state.PlantExtraDisplayName,
                        soldQuantity,
                        source
                    );
                }

                if (state.ShouldTrackCraftingExtra)
                {
                    CraftingExtraSupplyTracker.TrackCraftingExtraSale(
                        state.CraftingExtraItemId,
                        state.CraftingExtraDisplayName,
                        soldQuantity,
                        source
                    );
                }

                if (state.ShouldTrackArtisanGood)
                {
                    ArtisanGoodSupplyTracker.TrackArtisanGoodSale(
                        state.ArtisanGoodItemId,
                        state.ArtisanGoodDisplayName,
                        soldQuantity,
                        source
                    );
                }

                if (state.ShouldTrackCookingFood)
                {
                    CookingFoodSupplyTracker.TrackCookingFoodSale(
                        state.CookingFoodItemId,
                        state.CookingFoodDisplayName,
                        soldQuantity,
                        source
                    );
                }

                if (state.ShouldTrackMonsterLoot)
                {
                    MonsterLootSupplyTracker.TrackMonsterLootSale(
                        state.MonsterLootItemId,
                        state.MonsterLootDisplayName,
                        soldQuantity,
                        source
                    );
                }

                if (state.ShouldTrackEquipment)
                {
                    EquipmentSupplyTracker.TrackEquipmentSale(
                        state.EquipmentItemId,
                        state.EquipmentDisplayName,
                        soldQuantity,
                        source
                    );
                }
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

                EconomyService.Monitor = null;
                EconomyContextBuilder.Monitor = null;
                DailyPurchaseTracker.Monitor = null;
                CropTraitService.Monitor = null;
                FishTraitService.Monitor = null;
                MineralTraitService.Monitor = null;
                AnimalProductTraitService.Monitor = null;
                ForageableTraitService.Monitor = null;
                PlantExtraTraitService.Monitor = null;
                CraftingExtraTraitService.Monitor = null;
                ArtisanGoodTraitService.Monitor = null;
                CookingFoodTraitService.Monitor = null;
                MonsterLootTraitService.Monitor = null;
                EquipmentTraitService.Monitor = null;
                ShopPriceRuntimeService.Clear();
                FrozenOvernightSellContext = null;
                CropSupplyDataService.ClearActiveData();
                FishSupplyDataService.ClearActiveData();
                MineralSupplyDataService.ClearActiveData();
                AnimalProductSupplyDataService.ClearActiveData();
                ForageableSupplyDataService.ClearActiveData();
                PlantExtraSupplyDataService.ClearActiveData();
                CraftingExtraSupplyDataService.ClearActiveData();
                ArtisanGoodSupplyDataService.ClearActiveData();
                CookingFoodSupplyDataService.ClearActiveData();
                MonsterLootSupplyDataService.ClearActiveData();
                EquipmentSupplyDataService.ClearActiveData();
                Monitor = null;
            }
            catch (Exception ex)
            {
                Monitor?.Log($"Failed to unpatch EconomyPatches: {ex}", LogLevel.Error);
            }
        }

        private readonly record struct PendingShopSaleState(
            bool ShouldTrackCrop,
            string CropProduceItemId,
            string CropDisplayName,
            bool ShouldTrackFish,
            string FishItemId,
            string FishDisplayName,
            bool ShouldTrackMineral,
            string MineralItemId,
            string MineralDisplayName,
            bool ShouldTrackAnimalProduct,
            string AnimalProductItemId,
            string AnimalProductDisplayName,
            bool ShouldTrackForageable,
            string ForageableItemId,
            string ForageableDisplayName,
            bool ShouldTrackPlantExtra,
            string PlantExtraItemId,
            string PlantExtraDisplayName,
            bool ShouldTrackCraftingExtra,
            string CraftingExtraItemId,
            string CraftingExtraDisplayName,
            bool ShouldTrackArtisanGood,
            string ArtisanGoodItemId,
            string ArtisanGoodDisplayName,
            bool ShouldTrackCookingFood,
            string CookingFoodItemId,
            string CookingFoodDisplayName,
            bool ShouldTrackMonsterLoot,
            string MonsterLootItemId,
            string MonsterLootDisplayName,
            bool ShouldTrackEquipment,
            string EquipmentItemId,
            string EquipmentDisplayName,
            int SlotIndex,
            string QualifiedItemId,
            int OriginalStack
        )
        {
            public static PendingShopSaleState CreateSkipped()
            {
                return new PendingShopSaleState(
                    ShouldTrackCrop: false,
                    CropProduceItemId: string.Empty,
                    CropDisplayName: string.Empty,
                    ShouldTrackFish: false,
                    FishItemId: string.Empty,
                    FishDisplayName: string.Empty,
                    ShouldTrackMineral: false,
                    MineralItemId: string.Empty,
                    MineralDisplayName: string.Empty,
                    ShouldTrackAnimalProduct: false,
                    AnimalProductItemId: string.Empty,
                    AnimalProductDisplayName: string.Empty,
                    ShouldTrackForageable: false,
                    ForageableItemId: string.Empty,
                    ForageableDisplayName: string.Empty,
                    ShouldTrackPlantExtra: false,
                    PlantExtraItemId: string.Empty,
                    PlantExtraDisplayName: string.Empty,
                    ShouldTrackCraftingExtra: false,
                    CraftingExtraItemId: string.Empty,
                    CraftingExtraDisplayName: string.Empty,
                    ShouldTrackArtisanGood: false,
                    ArtisanGoodItemId: string.Empty,
                    ArtisanGoodDisplayName: string.Empty,
                    ShouldTrackCookingFood: false,
                    CookingFoodItemId: string.Empty,
                    CookingFoodDisplayName: string.Empty,
                    ShouldTrackMonsterLoot: false,
                    MonsterLootItemId: string.Empty,
                    MonsterLootDisplayName: string.Empty,
                    ShouldTrackEquipment: false,
                    EquipmentItemId: string.Empty,
                    EquipmentDisplayName: string.Empty,
                    SlotIndex: -1,
                    QualifiedItemId: string.Empty,
                    OriginalStack: 0
                );
            }
        }

        private static void ApplySellPriceAdjustment(Item item, ref int result, string source)
        {
            int vanilla = result;
            EconomyContext context = FrozenOvernightSellContext
                ?? EconomyContextBuilder.Build(shopkeeperName: null, monitor: Monitor);

            int adjusted = EconomyService.AdjustSellPrice(vanilla, item, context);
            result = Math.Max(0, adjusted);
            VerbosePriceTraceLogger.Log($"{source}: {vanilla} -> {result} for {item?.Name}");
        }
    }
}
