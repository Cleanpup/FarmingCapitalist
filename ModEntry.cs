using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace FarmingCapitalist
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        /*********
        ** Fields
        *********/

        /// <summary>The mod configuration.</summary>
        private ModConfig Config = null!;


        /*********
        ** Public methods
        *********/

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        }


        /*********
        ** Private methods
        *********/

        /// <summary>Raised after the game is launched, right before the first update tick.</summary>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.Monitor.Log("FarmingCapitalist loaded!", LogLevel.Info);

            if (this.Config.EnableDebugLogging)
                this.Monitor.Log("Debug logging is enabled.", LogLevel.Debug);
        }

        /// <summary>Raised after the player loads a save (including when creating a new save).</summary>
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (this.Config.EnableDebugLogging)
                this.Monitor.Log("FarmingCapitalist: save loaded.", LogLevel.Debug);
        }

        /// <summary>Raised after a new day starts.</summary>
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            // Called each in-game day; add day-start logic here.
        }
    }
}
