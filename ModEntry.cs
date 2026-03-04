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
        private ShopEditor _shopEditor = null!;
        public override void Entry(IModHelper helper)
        {
            _shopEditor = new ShopEditor(helper, this.Monitor);
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
        }
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
            if (e.NewMenu is not StardewValley.Menus.ShopMenu shop)
                return;

            DeferOneTick(() => _shopEditor.Apply(shop));
        }

        private void DeferOneTick(Action action)
        {
            void ApplyNextTick(object? s, StardewModdingAPI.Events.UpdateTickedEventArgs args)
            {
                this.Helper.Events.GameLoop.UpdateTicked -= ApplyNextTick;

                try
                {
                    action();
                }
                catch (Exception e)
                {
                    this.Monitor.Log($"Failed to apply shop changes: {e}", LogLevel.Error);
                }
            }

            this.Helper.Events.GameLoop.UpdateTicked += ApplyNextTick;
        }

        private string GetItemName(StardewValley.ISalable s)
        {
            if (s is StardewValley.Object obj) return obj.Name;
            if (s is StardewValley.Item it) return it.Name;
            return s?.ToString() ?? "<unknown>";
        }

    }
}
