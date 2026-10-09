using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist.Workers;

/// <summary>Host simulation at permanent shores, with read-only rod/line rendering on every peer.</summary>
internal sealed class WorkerFishingManager
{
    internal const string VisualKey = "Cleanpup.FarmingCapitalist/FishingCast";
    private readonly WorkerShellManager shell;
    private readonly WorkerNavigationManager navigation;
    private readonly IMonitor monitor;
    private readonly Dictionary<string, (GameLocation Location, Point Shore, Point Water)> stationed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> retryAfter = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<NPC> posed = new();
    private readonly Dictionary<string, int> castTicks = new(StringComparer.OrdinalIgnoreCase);

    public WorkerFishingManager(WorkerShellManager shell, WorkerNavigationManager navigation, IMonitor monitor)
        => (this.shell, this.navigation, this.monitor) = (shell, navigation, monitor);

    public void Reset()
    {
        if (Context.IsWorldReady && Context.IsMainPlayer)
            foreach (NPC worker in this.shell.GetSpawnedWorkers(recoverStale: false)) worker.modData.Remove(VisualKey);
        foreach (NPC worker in this.posed)
            if (worker.Sprite is not null) worker.Sprite.CurrentFrame = WorkerCombatSwingPolicy.DirectionRow(worker.FacingDirection) * 4;
        this.posed.Clear();
        this.stationed.Clear(); this.retryAfter.Clear(); this.castTicks.Clear();
    }

    public void Stop(string workerId)
    {
        this.stationed.Remove(workerId); this.retryAfter.Remove(workerId); this.castTicks.Remove(workerId);
        if (this.shell.TryGetWorker(workerId, out NPC? worker) && worker is not null && Context.IsMainPlayer)
        {
            worker.modData.Remove(VisualKey);
            if (this.posed.Remove(worker) && worker.Sprite is not null)
                worker.Sprite.CurrentFrame = WorkerCombatSwingPolicy.DirectionRow(worker.FacingDirection) * 4;
        }
        this.shell.CancelFishing(workerId);
    }

    public void Suspend(string workerId)
    {
        WorkerFishingProgress progress = this.shell.GetFishingProgress(workerId);
        if (WorkerFishingPolicy.ObserveClock(progress, Game1.Date.TotalDays, Game1.timeOfDay, accrue: false))
            this.shell.RecordFishingProgress(workerId, progress);
        if (Context.IsMainPlayer && this.shell.TryGetWorker(workerId, out NPC? worker) && worker is not null)
            worker.modData.Remove(VisualKey);
    }

    public bool Deliver(string workerId)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer) return false;
        WorkerFishingProgress progress = this.shell.GetFishingProgress(workerId);
        bool delivered = WorkerFishingStorage.TryDeliver(progress, this.shell.GetHarvestDestination(workerId), this.monitor,
            () => this.shell.RecordFishingProgress(workerId, progress), out string error);
        if (!delivered) this.monitor.Log($"{workerId} fishing delivery deferred; catches remain saved: {error}", LogLevel.Warn);
        return delivered;
    }

    public bool Update(NPC worker, string workerId, out WorkerRuntimeSnapshot snapshot)
    {
        string area = this.shell.GetFishingArea(workerId);
        snapshot = new(WorkerTaskKind.Fish, "Waiting", $"Preparing to fish at {WorkerFishingAreaCatalog.Label(area)}", 0, null);
        if (!Context.IsWorldReady || !Context.IsMainPlayer) return false;
        if (!Game1.shouldTimePass() || Game1.activeClickableMenu is not null || Game1.CurrentEvent is not null)
        { this.Suspend(workerId); return false; }
        // Midnight is a strict cutoff; no catch may complete on or beyond it.
        if (!WorkerFishingPolicy.IsWithinWorkHours(Game1.timeOfDay)) { this.Stop(workerId); return true; }
        WorkerFishingProgress progress = this.shell.GetFishingProgress(workerId);
        string? reason = WorkerFishingAreaCatalog.AccessReason(area);
        GameLocation? location = reason is null ? Game1.getLocationFromName(area) : null;
        if (location is null)
        {
            this.Suspend(workerId);
            snapshot = snapshot with { State = "Unavailable", Status = reason ?? "The fishing area is unavailable" };
            return false;
        }
        bool validStation = this.stationed.TryGetValue(workerId, out var station) && station.Location == location
            && worker.currentLocation == location && worker.TilePoint == station.Shore
            && this.IsSafeShore(location, worker, station.Shore, station.Water, checkActors: false);
        if (!validStation)
        {
            worker.modData.Remove(VisualKey);
            if (WorkerFishingPolicy.ObserveClock(progress, Game1.Date.TotalDays, Game1.timeOfDay, accrue: false))
                this.shell.RecordFishingProgress(workerId, progress);
            if (this.retryAfter.GetValueOrDefault(workerId) > Game1.ticks) return false;
            this.retryAfter[workerId] = Game1.ticks + 120;
            if (!this.TryChooseShore(location, worker, area, out Point shore, out Point water))
            {
                snapshot = snapshot with { Status = "Waiting for clear fishable shoreline" };
                return false;
            }
            this.navigation.StopWorker(worker);
            Game1.warpCharacter(worker, location.NameOrUniqueName, shore);
            this.navigation.ApplyIdlePose(worker);
            this.stationed[workerId] = station = (location, shore, water);
            this.castTicks[workerId] = 0;
            worker.FacingDirection = water.Y < shore.Y ? 0 : water.X > shore.X ? 1 : water.Y > shore.Y ? 2 : 3;
            this.monitor.Log($"{worker.displayName} stationed for simulated fishing at {location.NameOrUniqueName} {shore}, casting toward {water}.", LogLevel.Info);
        }
        else if (WorkerFishingPolicy.ObserveClock(progress, Game1.Date.TotalDays, Game1.timeOfDay, accrue: true))
            this.shell.RecordFishingProgress(workerId, progress);

        int ticks = this.castTicks.GetValueOrDefault(workerId) + 1;
        this.castTicks[workerId] = ticks;
        // State is mirrored through NPC modData. Draw hooks never advance time or roll catches.
        if (ticks % 12 == 1 || !worker.modData.ContainsKey(VisualKey))
            worker.modData[VisualKey] = $"{station.Water.X},{station.Water.Y},{ticks % 360}";
        while (progress.Minutes >= WorkerFishingPolicy.MinutesPerCatch && progress.CompletedAttempts < WorkerFishingPolicy.MaximumAttemptsPerDay)
        {
            if (!this.Deliver(workerId)) break;
            progress = this.shell.GetFishingProgress(workerId);
            int seed = unchecked((int)Game1.uniqueIDForThisGame + progress.Day * 397 + progress.CompletedAttempts * 7919);
            foreach (char c in workerId + area) seed = unchecked(seed * 31 + c);
            WorkerFishingCatch? caught = WorkerFishingCatchCatalog.Roll(area, location.GetSeason().ToString(), Game1.timeOfDay,
                location.IsRainingHere(), WorkerExperiencePolicy.GetLevel(this.shell.GetWorkerExperience(workerId).Fishing), new Random(seed));
            if (!this.shell.TryCompleteFishing(workerId, progress, caught)) break;
            if (!this.Deliver(workerId)) break;
            progress = this.shell.GetFishingProgress(workerId);
        }
        snapshot = new(WorkerTaskKind.Fish, "Fishing", $"Fishing at {WorkerFishingAreaCatalog.Label(area)}: {Math.Min(progress.Minutes, WorkerFishingPolicy.MinutesPerCatch)}/{WorkerFishingPolicy.MinutesPerCatch} minutes", progress.CompletedCatches, station.Shore);
        return false;
    }

    private bool IsSafeShore(GameLocation location, NPC worker, Point shore, Point water, bool checkActors = true)
        => location.hasTileAt(shore.X, shore.Y, "Back")
            && (checkActors ? this.navigation.IsWalkableWorkTile(location, worker, shore) : this.navigation.IsTraversableWorkTile(location, worker, shore))
            && !location.isWaterTile(shore.X, shore.Y)
            && location.isTileFishable(water.X, water.Y)
            && !location.warps.Any(warp => Math.Abs(warp.X - shore.X) + Math.Abs(warp.Y - shore.Y) <= 1)
            && location.doesTileHaveProperty(shore.X, shore.Y, "TouchAction", "Back") is null
            && new[] { new Point(0, -1), new Point(1, 0), new Point(0, 1), new Point(-1, 0) }
                .Any(offset => checkActors ? this.navigation.IsWalkableWorkTile(location, worker, shore + offset)
                    : this.navigation.IsTraversableWorkTile(location, worker, shore + offset));

    private bool TryChooseShore(GameLocation location, NPC worker, string area, out Point shore, out Point water)
    {
        Point anchor = WorkerFishingAreaCatalog.Anchor(area);
        Point[] offsets = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };
        // Bounded scan supports edited maps without assuming an authored tile remains safe.
        for (int radius = 0; radius <= 12; radius++)
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
            Point candidate = new(anchor.X + dx, anchor.Y + dy);
            foreach (Point offset in offsets)
            {
                Point cast = candidate + offset;
                if (!this.IsSafeShore(location, worker, candidate, cast)) continue;
                // Use Forest river, never the pond's distinct fish pool.
                if (area == WorkerFishingAreaCatalog.Forest
                    && (!location.TryGetFishAreaForTile(cast.ToVector2(), out string fishArea, out _) || fishArea != "River")) continue;
                shore = candidate; water = cast; return true;
            }
        }
        shore = water = Point.Zero; return false;
    }

    public void UpdateVisuals()
    {
        if (!Context.IsWorldReady) return;
        HashSet<NPC> present = this.shell.GetSpawnedWorkers(recoverStale: false).ToHashSet();
        foreach (NPC worker in this.posed.Where(worker => !present.Contains(worker)
            || !worker.modData.ContainsKey(VisualKey) || !WorkerFishingPolicy.IsWithinWorkHours(Game1.timeOfDay)).ToArray())
        {
            this.posed.Remove(worker);
            if (worker.Sprite is not null) worker.Sprite.CurrentFrame = WorkerCombatSwingPolicy.DirectionRow(worker.FacingDirection) * 4;
        }
        foreach (NPC worker in present)
        {
            if (!worker.modData.TryGetValue(VisualKey, out string? value) || worker.Sprite is null
                || !this.shell.TryGetWorkerId(worker, out string id) || this.shell.GetAssignedTask(id) != WorkerTaskKind.Fish
                || !WorkerFishingPolicy.IsWithinWorkHours(Game1.timeOfDay)) continue;
            string[] fields = value.Split(',');
            if (fields.Length != 3 || !int.TryParse(fields[0], out int x) || !int.TryParse(fields[1], out int y)
                || !int.TryParse(fields[2], out int phase) || phase is < 0 or >= 360) continue;
            Point shore = worker.TilePoint;
            int facing = y < shore.Y ? 0 : x > shore.X ? 1 : y > shore.Y ? 2 : 3;
            worker.FacingDirection = facing;
            worker.Sprite.StopAnimation();
            worker.Sprite.CurrentFrame = WorkerFishingAnimationPolicy.SheetFrame(facing, phase);
            this.posed.Add(worker);
        }
    }

    public void Draw(SpriteBatch batch)
    {
        if (!Context.IsWorldReady || Game1.currentLocation is null) return;
        foreach (NPC worker in Game1.currentLocation.characters)
        {
            if (worker.IsInvisible || !this.shell.TryGetWorkerId(worker, out string id) || this.shell.GetAssignedTask(id) != WorkerTaskKind.Fish
                || !WorkerFishingPolicy.IsWithinWorkHours(Game1.timeOfDay) || !worker.modData.TryGetValue(VisualKey, out string? value)) continue;
            string[] fields = value.Split(',');
            if (fields.Length != 3 || !int.TryParse(fields[0], out int x) || !int.TryParse(fields[1], out int y)
                || !int.TryParse(fields[2], out int phase)) continue;
            Vector2 hand = worker.Position + new Vector2(32, -4);
            Vector2 bobber = new Vector2(x * 64 + 32, y * 64 + 32);
            Vector2 direction = Vector2.Normalize(bobber - (worker.Position + new Vector2(32, 32)));
            float cast = Math.Clamp(phase / 45f, 0f, 1f);
            if (phase > 315) cast = Math.Clamp((360 - phase) / 45f, 0f, 1f);
            Vector2 tip = hand + direction * 48 + new Vector2(0, -24);
            hand = Game1.GlobalToLocal(Game1.viewport, hand);
            tip = Game1.GlobalToLocal(Game1.viewport, tip);
            bobber = Game1.GlobalToLocal(Game1.viewport, bobber);
            DrawLine(batch, hand, tip, new Color(139, 90, 43), 4);
            Vector2 end = Vector2.Lerp(tip, bobber, cast);
            DrawLine(batch, tip, end, Color.White * 0.85f, 1);
            if (cast > 0.9f)
            {
                float bob = phase % 24 < 12 ? 0 : 2;
                batch.Draw(Game1.staminaRect, new Rectangle((int)end.X - 3, (int)(end.Y + bob) - 4, 6, 8), Color.Red);
                batch.Draw(Game1.staminaRect, new Rectangle((int)end.X - 3, (int)(end.Y + bob), 6, 4), Color.White);
            }
        }
    }

    private static void DrawLine(SpriteBatch batch, Vector2 start, Vector2 end, Color color, int width)
    {
        Vector2 delta = end - start;
        batch.Draw(Game1.staminaRect, start, null, color, MathF.Atan2(delta.Y, delta.X), Vector2.Zero,
            new Vector2(delta.Length(), width), SpriteEffects.None, 1f);
    }
}
