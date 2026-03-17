using StardewModdingAPI;

namespace FarmingCapitalist
{
    /// <summary>Thin lifecycle wrapper around the existing crop market simulation service.</summary>
    internal sealed class CropMarketSimulationLifecycleAdapter : IMarketSimulationLifecycle
    {
        public void Initialize(IModHelper helper, IMonitor monitor, bool debugLoggingEnabled)
        {
            CropMarketSimulationService.Initialize(helper, monitor, debugLoggingEnabled);
        }

        public void LoadOrCreateForCurrentSave()
        {
            CropMarketSimulationService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            CropMarketSimulationService.ClearActiveData();
        }

        public bool RunDailyUpdateIfNeeded()
        {
            return CropMarketSimulationService.RunDailyUpdateIfNeeded();
        }

        public bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            _ = elapsedDays;
            return false;
        }

        public IEnumerable<string> GetDebugStatusLines()
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Thin lifecycle wrapper around the existing fish market simulation service.</summary>
    internal sealed class FishMarketSimulationLifecycleAdapter : IMarketSimulationLifecycle
    {
        public void Initialize(IModHelper helper, IMonitor monitor, bool debugLoggingEnabled)
        {
            FishMarketSimulationService.Initialize(helper, monitor, debugLoggingEnabled);
        }

        public void LoadOrCreateForCurrentSave()
        {
            FishMarketSimulationService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            FishMarketSimulationService.ClearActiveData();
        }

        public bool RunDailyUpdateIfNeeded()
        {
            return FishMarketSimulationService.RunDailyUpdateIfNeeded();
        }

        public bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            return FishMarketSimulationService.ApplyDebugDailyUpdate(elapsedDays);
        }

        public IEnumerable<string> GetDebugStatusLines()
        {
            return FishMarketSimulationService.GetDebugStatusLines();
        }
    }

    /// <summary>Thin lifecycle wrapper around the existing mineral market simulation service.</summary>
    internal sealed class MineralMarketSimulationLifecycleAdapter : IMarketSimulationLifecycle
    {
        public void Initialize(IModHelper helper, IMonitor monitor, bool debugLoggingEnabled)
        {
            MineralMarketSimulationService.Initialize(helper, monitor, debugLoggingEnabled);
        }

        public void LoadOrCreateForCurrentSave()
        {
            MineralMarketSimulationService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            MineralMarketSimulationService.ClearActiveData();
        }

        public bool RunDailyUpdateIfNeeded()
        {
            return MineralMarketSimulationService.RunDailyUpdateIfNeeded();
        }

        public bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            return MineralMarketSimulationService.ApplyDebugDailyUpdate(elapsedDays);
        }

        public IEnumerable<string> GetDebugStatusLines()
        {
            return MineralMarketSimulationService.GetDebugStatusLines();
        }
    }
}
