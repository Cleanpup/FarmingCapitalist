using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace HireSkilledHelpers.Workers;

/// <summary>Host-owned simulated expeditions with a visible, stationary worker at an entrance.</summary>
internal sealed class WorkerExplorationManager
{
    private readonly WorkerShellManager shell;
    private readonly WorkerNavigationManager navigation;
    private readonly IMonitor monitor;
    private readonly HashSet<string> stationed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> retryAfter = new(StringComparer.OrdinalIgnoreCase);

    public WorkerExplorationManager(WorkerShellManager shell, WorkerNavigationManager navigation, IMonitor monitor)
        => (this.shell, this.navigation, this.monitor) = (shell, navigation, monitor);

    public static string? AccessReason(string area) => WorkerExplorationPolicy.UnavailableReason(area,
        Game1.stats.DaysPlayed >= 5,
        Game1.MasterPlayer.mailReceived.Contains("ccVault"), Game1.MasterPlayer.hasSkullKey,
        Game1.MasterPlayer.hasOrWillReceiveMail("willyBoatFixed"),
        Game1.MasterPlayer.hasOrWillReceiveMail("Island_FirstParrot"));

    public void Reset()
    {
        this.stationed.Clear();
        this.retryAfter.Clear();
    }

    public void Stop(string workerId)
    {
        this.stationed.Remove(workerId);
        this.retryAfter.Remove(workerId);
        this.shell.CancelExploration(workerId);
    }

    public void Suspend(string workerId)
    {
        WorkerExplorationProgress progress = this.shell.GetExplorationProgress(workerId);
        if (WorkerExplorationPolicy.ObserveClock(progress, Game1.Date.TotalDays, Game1.timeOfDay, accrue: false))
            this.shell.RecordExplorationProgress(workerId, progress);
    }

    public bool DeliverPendingLoot(string workerId)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
            return false;
        WorkerExplorationProgress progress = this.shell.GetExplorationProgress(workerId);
        bool delivered = WorkerExplorationLootStorage.TryDeliver(progress, this.shell.GetHarvestDestination(workerId), this.monitor,
            () => this.shell.RecordExplorationProgress(workerId, progress), out string error);
        if (!delivered)
            this.monitor.Log($"{workerId} exploration loot delivery deferred; items remain saved: {error}", LogLevel.Warn);
        return delivered;
    }

    /// <returns>True when the ordinary home-return lifecycle should take over.</returns>
    public bool Update(NPC worker, string workerId, out WorkerRuntimeSnapshot snapshot)
    {
        string area = this.shell.GetExplorationArea(workerId);
        string label = WorkerExplorationAreaCatalog.GetLabel(area);
        snapshot = new(WorkerTaskKind.ExploreArea, "Waiting", $"Preparing to explore {label}", 0, null);
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
            return false;
        if (!Game1.shouldTimePass() || Game1.activeClickableMenu is not null || Game1.CurrentEvent is not null)
        {
            WorkerExplorationProgress paused = this.shell.GetExplorationProgress(workerId);
            if (WorkerExplorationPolicy.ObserveClock(paused, Game1.Date.TotalDays, Game1.timeOfDay, accrue: false))
                this.shell.RecordExplorationProgress(workerId, paused);
            return false;
        }
        if (this.shell.WasDefeatedToday(workerId))
        {
            this.Stop(workerId);
            return true;
        }
        WorkerExplorationProgress progress = this.shell.GetExplorationProgress(workerId);
        if ((!this.stationed.Contains(workerId) || progress.Day != Game1.Date.TotalDays)
            && WorkerExplorationPolicy.ObserveClock(progress, Game1.Date.TotalDays, Game1.timeOfDay, accrue: false))
            this.shell.RecordExplorationProgress(workerId, progress);
        string? reason = AccessReason(area);
        GameLocation? entrance = reason is null ? Game1.getLocationFromName(WorkerExplorationAreaCatalog.Entrance(area)) : null;
        if (entrance is null)
        {
            if (WorkerCombatPolicy.ShouldBeginReturn(Game1.timeOfDay)) return true;
            if (WorkerExplorationPolicy.ObserveClock(progress, Game1.Date.TotalDays, Game1.timeOfDay, accrue: false))
                this.shell.RecordExplorationProgress(workerId, progress);
            snapshot = new(WorkerTaskKind.ExploreArea, "Unavailable", reason ?? "The dungeon entrance is unavailable", 0, null);
            return false;
        }

        if (!this.stationed.Contains(workerId) || worker.currentLocation != entrance)
        {
            if (!WorkerTaskPolicy.IsWithinWorkHours(Game1.timeOfDay))
                return true;
            if (this.retryAfter.GetValueOrDefault(workerId) > Game1.ticks)
                return false;
            this.retryAfter[workerId] = Game1.ticks + 120;
            if (!this.TryChooseEntranceTile(entrance, worker, area, out Point tile))
            {
                snapshot = new(WorkerTaskKind.ExploreArea, "Waiting", "No clear tile at the dungeon entrance", 0, null);
                return false;
            }
            this.navigation.StopWorker(worker);
            // This entrance teleport is specific to the simulated Explore Area order.
            // Never use it to recover a failed physical Slay monsters route.
            Game1.warpCharacter(worker, entrance.NameOrUniqueName, tile);
            this.navigation.ApplyIdlePose(worker);
            this.stationed.Add(workerId);
            if (WorkerExplorationPolicy.ObserveClock(progress, Game1.Date.TotalDays, Game1.timeOfDay, accrue: false))
                this.shell.RecordExplorationProgress(workerId, progress);
            this.monitor.Log($"{worker.displayName} stationed for simulated exploration of {label} at "
                + $"{entrance.NameOrUniqueName} {tile}; no dungeon floors entered.", LogLevel.Info);
        }
        else if (WorkerExplorationPolicy.ObserveClock(progress, Game1.Date.TotalDays, Game1.timeOfDay, accrue: true))
        {
            this.shell.RecordExplorationProgress(workerId, progress);
        }

        // Saving a queued batch before storage keeps completed expeditions and XP
        // independent of storage retries and save/load. At most 16 runs per day.
        while (progress.Minutes >= WorkerExplorationPolicy.MinutesPerRun
            && progress.CompletedRuns < WorkerExplorationPolicy.MaximumRunsPerDay)
        {
            if (!this.DeliverPendingLoot(workerId)) break;
            // Delivery can remove an older pending batch. Reload that queue before
            // recording the next batch, so it can't restore already stored items.
            progress = this.shell.GetExplorationProgress(workerId);
            int seed = unchecked((int)Game1.uniqueIDForThisGame + progress.Day * 397 + progress.CompletedRuns * 7919);
            foreach (char c in workerId + area) seed = unchecked(seed * 31 + c);
            List<WorkerExplorationLoot> loot = WorkerExplorationPolicy.RollLoot(area, new Random(seed), Game1.MasterPlayer.deepestMineLevel,
                Game1.MasterPlayer.mailReceived.Contains("slimeHutchBuilt"));
            if (!this.shell.TryCompleteExploration(workerId, progress, loot))
                break;
            this.monitor.Log($"{worker.displayName} completed simulated exploration #{progress.CompletedRuns} in {label}: "
                + string.Join(", ", loot.Select(item => $"{item.ItemId} x{item.Stack}")) + ".", LogLevel.Info);
            if (!this.DeliverPendingLoot(workerId))
                break;
            progress = this.shell.GetExplorationProgress(workerId);
        }
        if (WorkerCombatPolicy.ShouldBeginReturn(Game1.timeOfDay))
        {
            this.Stop(workerId);
            return true;
        }
        snapshot = new(WorkerTaskKind.ExploreArea, "Exploring",
            $"Exploring {label}: {Math.Min(progress.Minutes, 60)}/60 minutes", progress.CompletedRuns, null);
        return false;
    }

    private bool TryChooseEntranceTile(GameLocation location, NPC worker, string area, out Point tile)
    {
        int x = 0, y = 0;
        if (area == WorkerExplorationAreaCatalog.Volcano)
            (x, y) = (40, 24); // Vanilla Volcano exit arrives here on IslandNorth.
        else
            Utility.getDefaultWarpLocation(location.NameOrUniqueName, ref x, ref y);
        Point anchor = new(x, y);
        Point[] neighbors = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };
        for (int radius = 0; radius <= 8; radius++)
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
            Point candidate = new(anchor.X + dx, anchor.Y + dy);
            if (!location.hasTileAt(candidate.X, candidate.Y, "Back")
                || location.warps.Any(warp => Math.Abs(warp.X - candidate.X) + Math.Abs(warp.Y - candidate.Y) <= 1)
                || !this.navigation.IsWalkableWorkTile(location, worker, candidate)
                || location.doesTileHaveProperty(candidate.X, candidate.Y, "TouchAction", "Back") is not null)
                continue;
            if (!neighbors.Any(offset => this.navigation.IsWalkableWorkTile(location, worker, candidate + offset)))
                continue;
            tile = candidate;
            return true;
        }
        tile = Point.Zero;
        return false;
    }
}
