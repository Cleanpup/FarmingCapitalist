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
    private readonly HashSet<string> reportedBlockedShores = new(StringComparer.OrdinalIgnoreCase);
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
        this.stationed.Clear(); this.retryAfter.Clear(); this.castTicks.Clear(); this.reportedBlockedShores.Clear();
    }

    public void Stop(string workerId)
    {
        this.stationed.Remove(workerId); this.retryAfter.Remove(workerId); this.castTicks.Remove(workerId); this.reportedBlockedShores.Remove(workerId);
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
            && this.IsSafeShore(location, worker, area, station.Shore, station.Water, checkActors: false);
        if (!validStation)
        {
            worker.modData.Remove(VisualKey);
            if (WorkerFishingPolicy.ObserveClock(progress, Game1.Date.TotalDays, Game1.timeOfDay, accrue: false))
                this.shell.RecordFishingProgress(workerId, progress);
            // Preserve the reason throughout the retry cooldown instead of replacing it with Preparing.
            snapshot = snapshot with { Status = "Waiting for clear fishable shoreline" };
            if (this.retryAfter.GetValueOrDefault(workerId) > Game1.ticks) return false;
            this.retryAfter[workerId] = Game1.ticks + 120;
            if (!this.TryChooseShore(location, worker, area, out Point shore, out Point water))
            {
                if (this.reportedBlockedShores.Add(workerId))
                    this.monitor.Log($"{worker.displayName} is waiting for clear fishable shoreline in {location.NameOrUniqueName} near {WorkerFishingAreaCatalog.Anchor(area)}; no safe shore/cast pair within 12 tiles.", LogLevel.Info);
                return false;
            }
            this.reportedBlockedShores.Remove(workerId);
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

    private bool IsSafeShore(GameLocation location, NPC worker, string area, Point shore, Point water, bool checkActors = true)
        => WorkerFishingShorePolicy.IsAllowedShore(area, shore.X, shore.Y)
            && location.hasTileAt(shore.X, shore.Y, "Back")
            && (checkActors ? this.navigation.IsWalkableWorkTile(location, worker, shore) : this.navigation.IsTraversableWorkTile(location, worker, shore))
            && !location.isWaterTile(shore.X, shore.Y)
            && (area != WorkerFishingAreaCatalog.Beach
                || (!location.hasTileAt(shore.X, shore.Y, "Front") && !location.hasTileAt(shore.X, shore.Y, "AlwaysFront")))
            && location.isTileFishable(water.X, water.Y)
            && WorkerFishingShorePolicy.IsContinuousWaterCast(shore.X, shore.Y, water.X, water.Y,
                (x, y) => IsWaterForCast(location, x, y))
            && !location.warps.Any(warp => Math.Abs(warp.X - shore.X) + Math.Abs(warp.Y - shore.Y) <= 1)
            && location.doesTileHaveProperty(shore.X, shore.Y, "TouchAction", "Back") is null
            && new[] { new Point(0, -1), new Point(1, 0), new Point(0, 1), new Point(-1, 0) }
                .Any(offset => checkActors ? this.navigation.IsWalkableWorkTile(location, worker, shore + offset)
                    : this.navigation.IsTraversableWorkTile(location, worker, shore + offset));

    private static bool IsWaterForCast(GameLocation location, int x, int y)
        => location.hasTileAt(x, y, "Back") && (location.isWaterTile(x, y) || location.isTileFishable(x, y));

    private static bool IsEligibleFishableWater(GameLocation location, string area, int x, int y)
        => location.isTileFishable(x, y) && (area != WorkerFishingAreaCatalog.Forest
            || (location.TryGetFishAreaForTile(new Vector2(x, y), out string fishArea, out _) && fishArea == "River"));

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
                // Vanilla often draws a nonfishable border in the Buildings layer over
                // the first water tile. Cast beyond that border without crossing land.
                if (!WorkerFishingShorePolicy.TryFindCast(candidate.X, candidate.Y, offset.X, offset.Y,
                    (x, y) => IsWaterForCast(location, x, y),
                    (x, y) => IsEligibleFishableWater(location, area, x, y), out var castTile)) continue;
                Point cast = new(castTile.X, castTile.Y);
                if (!this.IsSafeShore(location, worker, area, candidate, cast)) continue;
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
                || !int.TryParse(fields[2], out int phase) || phase is < 0 or >= 360) continue;
            Vector2 bobber = new(x * 64 + 32, y * 64 + 32);
            Point shore = worker.TilePoint;
            int facing = y < shore.Y ? 0 : x > shore.X ? 1 : y > shore.Y ? 2 : 3;
            float cast = Math.Clamp(phase / 45f, 0f, 1f);
            if (phase > 315) cast = Math.Clamp((360 - phase) / 45f, 0f, 1f);
            DrawNativeRod(batch, worker, facing, phase);
            if (cast <= 0f) continue;

            // FishingRod.draw's bobber asset and curved line, rendered from saved
            // visual state only. Its draw method also plays sounds and adds sprites.
            Vector2 tip = worker.Position + (facing switch
            {
                0 => new Vector2(28, -76),
                1 => new Vector2(120, -48),
                2 => new Vector2(28, 52),
                _ => new Vector2(-56, -48),
            });
            Vector2 floatPosition = Vector2.Lerp(tip, bobber, cast);
            if (cast > 0.9f && phase % 24 >= 12) floatPosition.Y += 2f;
            Vector2 lineStart = Game1.GlobalToLocal(Game1.viewport, tip);
            Vector2 lineEnd = Game1.GlobalToLocal(Game1.viewport, floatPosition + new Vector2(0, -10));
            DrawNativeLine(batch, lineStart, lineEnd, cast > 0.9f,
                floatPosition.Y / 10000f + 0.005f);

            Rectangle bobberSource = Game1.getSourceRectForStandardTileSheet(Game1.bobbersTexture, 0, 16, 32);
            bobberSource.Y += 16;
            bobberSource.Height = 16;
            batch.Draw(Game1.bobbersTexture, Game1.GlobalToLocal(Game1.viewport, floatPosition), bobberSource,
                Color.White, 0f, new Vector2(8, 8), 4f,
                facing == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, floatPosition.Y / 10000f);
        }
    }

    private static void DrawNativeRod(SpriteBatch batch, NPC worker, int facing, int phase)
    {
        // Game1.drawTool uses these 48x48 frames from the rod's tool texture.
        // We draw the asset directly so no FishingRod callbacks can alter gameplay.
        int frame = phase < 45 ? Math.Clamp(phase / 9, 0, 4) : phase <= 315 ? 5 : 6;
        int sourceY = facing is 1 or 3 ? (phase > 315 ? 240 : 288) : 336;
        Rectangle source = new(frame * 48, sourceY, 48, 48);
        Texture2D texture = ItemRegistry.GetData("(T)BambooPole")?.GetTexture() ?? Game1.toolSpriteSheet;
        if (source.Right > texture.Width || source.Bottom > texture.Height) return;
        Vector2 position = worker.Position + (facing is 1 or 3
            ? new Vector2(-64, -160) : new Vector2(facing == 0 ? -48 : -64, -124));
        batch.Draw(texture, Game1.GlobalToLocal(Game1.viewport, position), source,
            Color.Goldenrod, 0f, Vector2.Zero, 4f,
            facing == 3 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            worker.StandingPixel.Y / 10000f + 0.003f);
    }

    private static void DrawNativeLine(SpriteBatch batch, Vector2 start, Vector2 end, bool held, float depth)
    {
        Vector2 p1 = new(start.X + (end.X - start.X) / 3f, start.Y + (end.Y - start.Y) * 2f / 3f);
        Vector2 p2 = new(start.X + (end.X - start.X) * 2f / 3f,
            start.Y + (end.Y - start.Y) * (held ? 6f : 2f) / 5f);
        Vector2 previous = start;
        for (int i = 1; i <= 40; i++)
        {
            Vector2 next = Utility.GetCurvePoint(i / 40f, start, p1, p2, end);
            Utility.drawLineWithScreenCoordinates((int)previous.X, (int)previous.Y,
                (int)next.X, (int)next.Y, batch, Color.White * 0.5f, depth);
            previous = next;
        }
    }
}
