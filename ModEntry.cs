using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;

        }
        
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // initialize Harmony patches for Economy and ShopMenu
            var harmonyId = this.ModManifest.UniqueID + ".economy";
            EconomyPatches.Initialize(this.Monitor, harmonyId);

            this.Monitor.Log("Game launched with Farming Capitalist!", LogLevel.Info);
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            // Only act when a new menu opened and it's a ShopMenu (and currency is money 0).
            if (e.NewMenu is not StardewValley.Menus.ShopMenu shop)
                return;

            try
            {
                if (shop.currency != 0)
                    return; // only money-priced shops

                var keys = shop.itemPriceAndStock.Keys.ToList();
                foreach (var item in keys)
                {
                    var stock = shop.itemPriceAndStock[item]; // struct copy
                    int vanillaPrice = stock.Price;
                    int adjusted = EconomyService.AdjustBuyPrice(vanillaPrice);

                    // Clamp buy price to at least 1 to avoid free purchases.
                    adjusted = Math.Max(1, adjusted);

                    stock.Price = adjusted;
                    shop.itemPriceAndStock[item] = stock; // assign back

                    this.Monitor.Log($"Adjusted shop price: {GetItemName(item)} {vanillaPrice} -> {adjusted}", LogLevel.Trace);
                }

                this.Monitor.Log($"Adjusted buy prices for shop {shop.ShopId}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed to adjust shop prices: {ex}", LogLevel.Error);
            }
        }

        private string GetItemName(StardewValley.ISalable s)
        {
            if (s is StardewValley.Object obj) return obj.Name;
            if (s is StardewValley.Item it) return it.Name;
            return s?.ToString() ?? "<unknown>";
        }
    }
}