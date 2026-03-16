using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>
    /// High-level lifecycle contract for a market simulation subsystem.
    /// Ordered to read top-to-bottom the same way the game drives the services.
    /// </summary>
    internal interface IMarketSimulationLifecycle
    {
        /// <summary>Initialize the simulation with helper access, logging, and debug configuration.</summary>
        void Initialize(IModHelper helper, IMonitor monitor, bool debugLoggingEnabled);

        /// <summary>Load the current save's simulation state, creating default state when none exists yet.</summary>
        void LoadOrCreateForCurrentSave();

        /// <summary>Clear any active in-memory simulation state when leaving the current save.</summary>
        void ClearActiveData();

        /// <summary>Run the once-per-day simulation pass when the current save and day require it.</summary>
        bool RunDailyUpdateIfNeeded();

        /// <summary>Run an explicit debug update pass when the simulation exposes one; otherwise report no change.</summary>
        bool ApplyDebugDailyUpdate(int elapsedDays);

        /// <summary>Return concise human-readable status lines when the simulation exposes them.</summary>
        IEnumerable<string> GetDebugStatusLines();
    }
}
