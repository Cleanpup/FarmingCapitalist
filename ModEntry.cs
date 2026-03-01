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

            // print button presses to the console window
            this.Monitor.Log($"{Game1.player.Name} pressed {e.Button}.", LogLevel.Debug);
        }
        
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // initialize Harmony patches for ShopMenu
            // Use the mod's UniqueID to form a unique Harmony instance ID to avoid collisions
            // with other mods. This supplies the required harmonyId parameter added
            // when refactoring ShopMenuPatches.Initialize.
            var harmonyId = this.ModManifest.UniqueID + ".shopmenu";
            ShopMenuPatches.Initialize(this.Monitor, harmonyId);
            this.Monitor.Log("Game launched with Farming Capitalist!", LogLevel.Info);
        }
    }
}