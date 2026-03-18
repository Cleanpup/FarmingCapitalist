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

    /// <summary>Thin lifecycle wrapper around the existing animal product market simulation service.</summary>
    internal sealed class AnimalProductMarketSimulationLifecycleAdapter : IMarketSimulationLifecycle
    {
        public void Initialize(IModHelper helper, IMonitor monitor, bool debugLoggingEnabled)
        {
            AnimalProductMarketSimulationService.Initialize(helper, monitor, debugLoggingEnabled);
        }

        public void LoadOrCreateForCurrentSave()
        {
            AnimalProductMarketSimulationService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            AnimalProductMarketSimulationService.ClearActiveData();
        }

        public bool RunDailyUpdateIfNeeded()
        {
            return AnimalProductMarketSimulationService.RunDailyUpdateIfNeeded();
        }

        public bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            return AnimalProductMarketSimulationService.ApplyDebugDailyUpdate(elapsedDays);
        }

        public IEnumerable<string> GetDebugStatusLines()
        {
            return AnimalProductMarketSimulationService.GetDebugStatusLines();
        }
    }

    /// <summary>Thin lifecycle wrapper around the existing forageable market simulation service.</summary>
    internal sealed class ForageableMarketSimulationLifecycleAdapter : IMarketSimulationLifecycle
    {
        public void Initialize(IModHelper helper, IMonitor monitor, bool debugLoggingEnabled)
        {
            ForageableMarketSimulationService.Initialize(helper, monitor, debugLoggingEnabled);
        }

        public void LoadOrCreateForCurrentSave()
        {
            ForageableMarketSimulationService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            ForageableMarketSimulationService.ClearActiveData();
        }

        public bool RunDailyUpdateIfNeeded()
        {
            return ForageableMarketSimulationService.RunDailyUpdateIfNeeded();
        }

        public bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            return ForageableMarketSimulationService.ApplyDebugDailyUpdate(elapsedDays);
        }

        public IEnumerable<string> GetDebugStatusLines()
        {
            return ForageableMarketSimulationService.GetDebugStatusLines();
        }
    }

    /// <summary>Thin lifecycle wrapper around the existing artisan-good market simulation service.</summary>
    internal sealed class ArtisanGoodMarketSimulationLifecycleAdapter : IMarketSimulationLifecycle
    {
        public void Initialize(IModHelper helper, IMonitor monitor, bool debugLoggingEnabled)
        {
            ArtisanGoodMarketSimulationService.Initialize(helper, monitor, debugLoggingEnabled);
        }

        public void LoadOrCreateForCurrentSave()
        {
            ArtisanGoodMarketSimulationService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            ArtisanGoodMarketSimulationService.ClearActiveData();
        }

        public bool RunDailyUpdateIfNeeded()
        {
            return ArtisanGoodMarketSimulationService.RunDailyUpdateIfNeeded();
        }

        public bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            return ArtisanGoodMarketSimulationService.ApplyDebugDailyUpdate(elapsedDays);
        }

        public IEnumerable<string> GetDebugStatusLines()
        {
            return ArtisanGoodMarketSimulationService.GetDebugStatusLines();
        }
    }

    /// <summary>Thin lifecycle wrapper around the existing monster-loot market simulation service.</summary>
    internal sealed class MonsterLootMarketSimulationLifecycleAdapter : IMarketSimulationLifecycle
    {
        public void Initialize(IModHelper helper, IMonitor monitor, bool debugLoggingEnabled)
        {
            MonsterLootMarketSimulationService.Initialize(helper, monitor, debugLoggingEnabled);
        }

        public void LoadOrCreateForCurrentSave()
        {
            MonsterLootMarketSimulationService.LoadOrCreateForCurrentSave();
        }

        public void ClearActiveData()
        {
            MonsterLootMarketSimulationService.ClearActiveData();
        }

        public bool RunDailyUpdateIfNeeded()
        {
            return MonsterLootMarketSimulationService.RunDailyUpdateIfNeeded();
        }

        public bool ApplyDebugDailyUpdate(int elapsedDays)
        {
            return MonsterLootMarketSimulationService.ApplyDebugDailyUpdate(elapsedDays);
        }

        public IEnumerable<string> GetDebugStatusLines()
        {
            return MonsterLootMarketSimulationService.GetDebugStatusLines();
        }
    }
}
