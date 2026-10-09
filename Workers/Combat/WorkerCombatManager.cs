using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace FarmingCapitalist.Workers;

/// <summary>Host-owned monster work. Never passes a farmer to the damage or kill path.</summary>
internal sealed class WorkerCombatManager
{
    private const string HealthDataKey = "Cleanpup.FarmingCapitalist/CombatHealth";
    private const string MaxHealthDataKey = "Cleanpup.FarmingCapitalist/CombatMaxHealth";
    internal const string BlockedMonsterDataKey = "Cleanpup.FarmingCapitalist/BlockedMonsterLocation";

    private sealed class CombatState
    {
        public Monster? Target;
        public readonly Dictionary<Monster, int> FailedTargets = new();
        public GameLocation? LastLocation;
        public int Health;
        public int MaxHealth;
        public int NextAttackTick;
        public Monster? SwingTarget;
        public double SwingElapsed;
        public bool SwingImpactApplied;
        public int NextContactTick;
        public int NextRouteTick;
        public int NextPatrolTick;
        public bool Patrolling;
        public bool DefeatedToday;
        public bool WaitingForBlockedMonsterRoute;
    }

    private readonly Dictionary<string, CombatState> states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Monster, int> lastAggroTick = new();
    private readonly WorkerShellManager shell;
    private readonly WorkerNavigationManager navigation;
    private readonly IMonitor monitor;
    private readonly Action<string> onConfirmedKill;
    private readonly WorkerCombatAnimationManager animations;

    public WorkerCombatManager(WorkerShellManager shell, WorkerNavigationManager navigation, IMonitor monitor,
        Action<string> onConfirmedKill)
    {
        this.shell = shell;
        this.navigation = navigation;
        this.monitor = monitor;
        this.onConfirmedKill = onConfirmedKill;
        this.animations = new WorkerCombatAnimationManager(shell);
    }

    public void RegisterAnimations(HarmonyLib.Harmony harmony) => this.animations.Register(harmony);

    public void UpdateClientAnimations() => this.animations.UpdateClients();

    public void Reset()
    {
        this.animations.Reset();
        if (Context.IsWorldReady)
        {
            foreach (NPC worker in this.shell.GetSpawnedWorkers(recoverStale: false))
                worker.modData.Remove(BlockedMonsterDataKey);
        }
        this.states.Clear();
        this.lastAggroTick.Clear();
    }

    public bool TryGetHealth(string workerId, out int health, out int maxHealth)
    {
        if (this.states.TryGetValue(workerId, out CombatState? state) && state.MaxHealth > 0)
        {
            health = WorkerCombatPolicy.NormalizeHealth(state.Health, state.MaxHealth);
            maxHealth = WorkerCombatPolicy.MaxHealth;
            return true;
        }
        if (this.shell.TryGetWorker(workerId, out NPC? worker) && worker is not null
            && worker.modData.TryGetValue(HealthDataKey, out string? storedHealth)
            && worker.modData.TryGetValue(MaxHealthDataKey, out string? storedMaxHealth)
            && int.TryParse(storedHealth, out health) && int.TryParse(storedMaxHealth, out maxHealth)
            && maxHealth > 0)
        {
            health = WorkerCombatPolicy.NormalizeHealth(health, maxHealth);
            maxHealth = WorkerCombatPolicy.MaxHealth;
            return true;
        }
        health = 0;
        maxHealth = 0;
        return false;
    }

    public void DrawHealthBars(SpriteBatch batch)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null)
            return;
        foreach (NPC worker in this.shell.GetSpawnedWorkers(recoverStale: false))
        {
            if (worker.currentLocation != Game1.currentLocation
                || !this.shell.TryGetWorkerId(worker, out string workerId)
                || this.shell.GetAssignedTask(workerId) != WorkerTaskKind.SlayMonsters
                || !this.TryGetHealth(workerId, out int health, out int maxHealth))
                continue;
            Rectangle body = worker.GetBoundingBox();
            // The NPC collision box ends 16 px above its drawn sprite baseline.
            // Add that offset plus a small gap so the bar sits beneath the feet.
            Vector2 feet = Game1.GlobalToLocal(Game1.viewport,
                new Vector2(body.Center.X, body.Bottom + 16));
            Rectangle frame = new((int)feet.X - 26, (int)feet.Y + 4, 52, 8);
            batch.Draw(Game1.staminaRect, frame, Color.Black * 0.8f);
            batch.Draw(Game1.staminaRect, new Rectangle(frame.X + 2, frame.Y + 2, 48, 4), Color.DarkRed);
            int filled = (int)(48L * Math.Clamp(health, 0, maxHealth) / maxHealth);
            if (filled > 0)
                batch.Draw(Game1.staminaRect, new Rectangle(frame.X + 2, frame.Y + 2, filled, 4), Color.LimeGreen);
        }
    }

    public void Stop(string workerId)
    {
        if (Context.IsWorldReady)
        {
            foreach (NPC worker in this.shell.GetSpawnedWorkers(recoverStale: false))
            {
                if (this.shell.TryGetWorkerId(worker, out string spawnedId)
                    && string.Equals(spawnedId, workerId, StringComparison.OrdinalIgnoreCase))
                {
                    worker.modData.Remove(BlockedMonsterDataKey);
                    this.animations.Stop(worker);
                }
            }
        }
        if (this.states.TryGetValue(workerId, out CombatState? state))
        {
            state.Target = null;
            state.SwingTarget = null;
            state.Patrolling = false;
        }
    }

    public void EnsureHealth(NPC worker, string workerId)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
            return;
        CombatState state = this.states.TryGetValue(workerId, out CombatState? existing)
            ? existing : this.states[workerId] = new CombatState();
        const int newMaximum = WorkerCombatPolicy.MaxHealth;
        if (state.MaxHealth == 0)
        {
            if (this.shell.TryGetCombatHealthToday(workerId, out int savedHealth, out int savedMaximum))
            {
                state.Health = WorkerCombatPolicy.NormalizeHealth(savedHealth, savedMaximum);
                state.MaxHealth = newMaximum;
            }
            else
                state.Health = state.MaxHealth = newMaximum;
        }
        else if (newMaximum != state.MaxHealth)
        {
            state.Health = WorkerCombatPolicy.NormalizeHealth(state.Health, state.MaxHealth);
            state.MaxHealth = newMaximum;
        }
        else
            state.Health = WorkerCombatPolicy.ClampHealth(state.Health);
        string healthText = state.Health.ToString();
        string maxHealthText = state.MaxHealth.ToString();
        if (!worker.modData.TryGetValue(HealthDataKey, out string? publishedHealth) || publishedHealth != healthText)
            worker.modData[HealthDataKey] = healthText;
        if (!worker.modData.TryGetValue(MaxHealthDataKey, out string? publishedMax) || publishedMax != maxHealthText)
            worker.modData[MaxHealthDataKey] = maxHealthText;
        this.shell.RecordCombatHealth(workerId, state.Health);
    }

    public bool IsDefeatedToday(string workerId)
        => this.shell.WasDefeatedToday(workerId)
            || this.states.TryGetValue(workerId, out CombatState? state) && state.DefeatedToday;

    public void ExitGeneratedFloorForReturn(NPC worker, string area)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || worker.currentLocation is not MineShaft
            || !WorkerCombatAreaCatalog.IsDungeon(area))
            return;
        GameLocation? entrance = Game1.getLocationFromName(
            area == WorkerCombatAreaCatalog.Mines ? "Mine" : "SkullCave");
        if (entrance is not null)
            this.TryFollowTransition(worker, entrance, nearFarmer: false);
    }

    public void HandleHostWarp(Func<string, bool> mayFollow)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
            return;
        // Let this warp move a worker from the prior generated floor even if
        // vanilla has already unregistered that floor. Ordinary lookups recover
        // an unregistered shell home if this transition cannot place it safely.
        foreach (NPC worker in this.shell.GetSpawnedWorkers(recoverStale: false))
        {
            if (!this.shell.TryGetWorkerId(worker, out string workerId)
                || this.shell.GetAssignedTask(workerId) != WorkerTaskKind.SlayMonsters
                || this.IsDefeatedToday(workerId)
                || !mayFollow(workerId))
                continue;
            string area = this.shell.GetCombatArea(workerId);
            if (!WorkerCombatAreaCatalog.IsDungeon(area))
                continue;
            GameLocation? entrance = Game1.getLocationFromName(
                area == WorkerCombatAreaCatalog.Mines ? "Mine" : "SkullCave");
            if (entrance is null)
                continue;
            MineShaft? hostFloor = Game1.player.currentLocation as MineShaft;
            bool matchingActiveFloor = hostFloor is not null
                && WorkerCombatPolicy.IsMatchingMineFloor(area, hostFloor.mineLevel)
                && MineShaft.activeMines.Contains(hostFloor);
            bool stagedAtEntrance = worker.currentLocation == entrance;
            bool alreadyOnGeneratedFloor = worker.currentLocation is MineShaft;
            if (WorkerCombatPolicy.ShouldFollowOnHostWarp(stagedAtEntrance, alreadyOnGeneratedFloor,
                    matchingActiveFloor))
                this.TryFollowTransition(worker, hostFloor!, nearFarmer: true);
            else if (alreadyOnGeneratedFloor && !matchingActiveFloor)
                this.TryFollowTransition(worker, entrance, nearFarmer: false);
        }
    }

    public bool EnsureHomeByMidnight(NPC worker)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || !WorkerCombatPolicy.MustBeHomeNow(Game1.timeOfDay))
            return false;
        if (worker.currentLocation?.NameOrUniqueName == TestWorkerDefinition.LocationName)
            return true;
        WorkerNavigationTarget? home = this.shell.GetWorkerReturnTargets(worker, new HashSet<Point>()).FirstOrDefault();
        if (home is null)
        {
            this.monitor.Log($"{worker.displayName} could not meet the midnight return deadline: no safe home tile.", LogLevel.Warn);
            return false;
        }
        this.navigation.StopWorker(worker);
        this.animations.Stop(worker);
        try
        {
            Game1.warpCharacter(worker, home.LocationName, home.Tile);
            this.navigation.ApplyIdlePose(worker);
            this.monitor.Log($"{worker.displayName} arrived home at the midnight return deadline.", LogLevel.Info);
            return true;
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Could not place {worker.displayName} at home by midnight; will retry: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    /// <summary>Returns true when combat should hand control to the existing normal home route.</summary>
    public bool Update(NPC worker, string workerId, out WorkerRuntimeSnapshot snapshot)
    {
        snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Selecting", "Looking for monsters", 0, null);
        if (!Context.IsWorldReady || !Context.IsMainPlayer || !Game1.shouldTimePass())
            return false;

        bool returnHome = this.UpdateCore(worker, workerId, out snapshot);
        if (this.states.TryGetValue(workerId, out CombatState? state)
            && state.WaitingForBlockedMonsterRoute && worker.currentLocation is GameLocation location)
        {
            if (!worker.modData.TryGetValue(BlockedMonsterDataKey, out string? publishedLocation)
                || publishedLocation != location.NameOrUniqueName)
                worker.modData[BlockedMonsterDataKey] = location.NameOrUniqueName;
        }
        else
            worker.modData.Remove(BlockedMonsterDataKey);
        return returnHome;
    }

    private bool UpdateCore(NPC worker, string workerId, out WorkerRuntimeSnapshot snapshot)
    {
        snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Selecting", "Looking for monsters", 0, null);
        this.EnsureHealth(worker, workerId);
        CombatState state = this.states[workerId];
        state.WaitingForBlockedMonsterRoute = false;
        if (this.IsDefeatedToday(workerId))
        {
            if (worker.currentLocation?.NameOrUniqueName != TestWorkerDefinition.LocationName)
                this.TryPlaceDefeatedAtHome(worker);
            snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Idle", "Defeated; off duty today", 0, null);
            return false;
        }

        // Leave a generous margin for the ordinary visible route home, including mine exits.
        if (WorkerCombatPolicy.ShouldBeginReturn(Game1.timeOfDay))
        {
            this.Stop(workerId);
            return true;
        }

        string area = this.shell.GetCombatArea(workerId);
        GameLocation? location = this.ResolveWorkLocation(area, worker);
        if (location is null)
        {
            snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Waiting", "Area unavailable", 0, null);
            return false;
        }

        if (worker.currentLocation != location)
        {
            if (worker.controller is null && !this.navigation.HasActiveRoute(workerId)
                && Game1.ticks >= state.NextRouteTick)
            {
                state.NextRouteTick = Game1.ticks + 120;
                if (this.TryChooseLanding(location, worker, out Point landing))
                    this.navigation.TryStartTravel(worker, new WorkerNavigationTarget(location.NameOrUniqueName,
                        landing, TestWorkerDefinition.FacingDirection), "combat destination", out _);
            }
            snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Traveling",
                $"Going to {WorkerCombatAreaCatalog.GetLabel(area)}", 0, null);
            return false;
        }

        if (state.LastLocation != location)
        {
            this.animations.Stop(worker);
            state.SwingTarget = null;
            state.Target = null;
            state.FailedTargets.Clear();
            state.Patrolling = false;
            state.NextPatrolTick = Game1.ticks;
            state.LastLocation = location;
        }

        if (WorkerCombatAreaCatalog.IsDungeon(area) && location is not MineShaft)
        {
            this.navigation.StopTravel(worker);
            snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Waiting",
                "Waiting in the cave entrance for the farmer to descend", 0, null);
            return false;
        }

        this.AttractMonsters(worker, location);
        this.ApplyMonsterContact(worker, workerId, state, location);
        if (state.DefeatedToday)
        {
            snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Idle", "Defeated; off duty today", 0, null);
            return false;
        }

        if (state.SwingTarget is Monster swingTarget)
        {
            state.SwingElapsed += Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;
            this.animations.SetElapsed(worker, state.SwingElapsed);
            bool impactTime = !state.SwingImpactApplied
                && state.SwingElapsed >= WorkerCombatSwingPolicy.ImpactMilliseconds;
            if (impactTime)
            {
                bool canHit = WorkerCombatSwingPolicy.CanApplyImpact(state.SwingImpactApplied, state.SwingElapsed,
                        worker.currentLocation == location, swingTarget.Health > 0,
                        location.characters.Contains(swingTarget),
                        Math.Abs(worker.TilePoint.X - swingTarget.TilePoint.X)
                            + Math.Abs(worker.TilePoint.Y - swingTarget.TilePoint.Y) <= 1,
                        this.IsRevealedTarget(swingTarget), swingTarget.isInvincible());
                state.SwingImpactApplied = true;
                if (canHit)
                    this.Strike(worker, workerId, state, location, swingTarget);
            }
            if (state.SwingElapsed < WorkerCombatSwingPolicy.DurationMilliseconds)
            {
                snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Fighting",
                    $"Swinging at {swingTarget.displayName} ({state.Health} HP)", 0, swingTarget.TilePoint);
                return false;
            }
            state.SwingTarget = null;
            this.animations.Stop(worker);
        }

        // A monster can be briefly invincible after a hit. Keep pursuing the
        // current target through that window instead of starting a patrol.
        if (state.Target is null || state.Target.Health <= 0 || !location.characters.Contains(state.Target)
            || !this.IsRevealedTarget(state.Target))
            state.Target = this.FindTarget(location, worker, workerId, state);

        Monster? target = state.Target;
        if (target is null)
        {
            // A moving monster can temporarily block every adjacent approach tile.
            // Keep the worker nearby for a short retry instead of patrolling away.
            if (state.FailedTargets.Any(pair => WorkerCombatTargetPolicy.ShouldOfferBlockedMonsterDialogue(
                    this.shell.GetAssignedTask(workerId) == WorkerTaskKind.SlayMonsters,
                    waitingForClearRoute: true, pair.Key.Health > 0,
                    location.characters.Contains(pair.Key), this.IsRevealedTarget(pair.Key))
                    && pair.Value > Game1.ticks))
            {
                this.navigation.StopTravel(worker);
                state.Patrolling = false;
                state.WaitingForBlockedMonsterRoute = true;
                snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Searching",
                    "Waiting for a clear path to a monster", 0, null);
                return false;
            }
            if (state.Patrolling && (worker.controller is not null || this.navigation.HasActiveRoute(workerId)))
            {
                snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Patrolling",
                    "Walking the area for monsters", 0, null);
                return false;
            }
            if (state.Patrolling)
            {
                state.Patrolling = false;
                state.NextPatrolTick = Game1.ticks + 60;
            }
            if (worker.controller is null && !this.navigation.HasActiveRoute(workerId)
                && Game1.ticks >= state.NextPatrolTick)
            {
                state.NextPatrolTick = Game1.ticks + 120;
                state.Patrolling = this.TryStartPatrol(location, worker);
            }
            snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters,
                state.Patrolling ? "Patrolling" : "Searching",
                state.Patrolling ? "Walking the area for monsters" : "No reachable monster in this area", 0, null);
            return false;
        }

        if (state.Patrolling)
        {
            this.navigation.StopTravel(worker);
            state.Patrolling = false;
        }

        int distance = Math.Abs(worker.TilePoint.X - target.TilePoint.X) + Math.Abs(worker.TilePoint.Y - target.TilePoint.Y);
        if (distance <= 1)
        {
            this.navigation.StopTravel(worker);
            if (!target.isInvincible() && Game1.ticks >= state.NextAttackTick)
            {
                state.NextAttackTick = Game1.ticks + 40;
                Point workerCenter = worker.StandingPixel;
                Point targetCenter = target.StandingPixel;
                int facing = WorkerCombatSwingPolicy.FacingTowards(workerCenter.X, workerCenter.Y,
                    targetCenter.X, targetCenter.Y, worker.FacingDirection);
                state.SwingTarget = target;
                state.SwingElapsed = 0;
                state.SwingImpactApplied = false;
                this.animations.Start(worker, facing);
                this.monitor.Log($"{worker.displayName} started sword swing facing {facing} "
                    + $"(0=up, 1=right, 2=down, 3=left) at {target.displayName}: "
                    + $"worker center={workerCenter}, target center={targetCenter}.", LogLevel.Trace);
            }
            snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Fighting",
                $"Fighting {target.displayName} ({state.Health} HP)", 0, target.TilePoint);
            return false;
        }

        if (worker.controller is null && !this.navigation.HasActiveRoute(workerId)
            && Game1.ticks >= state.NextRouteTick)
        {
            state.NextRouteTick = Game1.ticks + 60;
            if (!this.TryStartApproach(location, worker, target.TilePoint))
            {
                state.FailedTargets[target] = Game1.ticks + 60;
                state.Target = null;
                state.WaitingForBlockedMonsterRoute = true;
                snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Searching",
                    "Waiting for a clear path to a monster", 0, null);
                return false;
            }
            else
                state.FailedTargets.Remove(target);
        }
        snapshot = new WorkerRuntimeSnapshot(WorkerTaskKind.SlayMonsters, "Hunting",
            $"Approaching {target.displayName} ({state.Health} HP)", 0, target.TilePoint);
        return false;
    }

    private GameLocation? ResolveWorkLocation(string area, NPC worker)
    {
        if (WorkerCombatAreaCatalog.IsDungeon(area))
        {
            GameLocation? entrance = Game1.getLocationFromName(area == WorkerCombatAreaCatalog.Mines ? "Mine" : "SkullCave");
            if (entrance is null)
                return null;
            MineShaft? hostFloor = Game1.player.currentLocation as MineShaft;
            bool matchingFloor = hostFloor is not null
                && WorkerCombatPolicy.IsMatchingMineFloor(area, hostFloor.mineLevel);
            bool hostAtEntrance = Game1.player.currentLocation == entrance;
            // Follow mode never unlocks or creates a dungeon for a worker. The host
            // farmer must already be at its entrance or on an active matching floor.
            if (!hostAtEntrance && !matchingFloor)
            {
                if (worker.currentLocation is MineShaft)
                    this.TryFollowTransition(worker, entrance, nearFarmer: false);
                return worker.currentLocation == entrance ? entrance : null;
            }
            if (worker.currentLocation is MineShaft)
            {
                if (matchingFloor && hostFloor is not null && MineShaft.activeMines.Contains(hostFloor))
                {
                    // The host's warp event can precede the generated floor
                    // becoming active. Retry the transition during the normal
                    // update, but only for a worker already inside the dungeon.
                    if (worker.currentLocation != hostFloor)
                        this.TryFollowTransition(worker, hostFloor, nearFarmer: true);
                    if (worker.currentLocation == hostFloor)
                        return hostFloor;
                }
                this.TryFollowTransition(worker, entrance, nearFarmer: false);
            }
            // A worker always stages in the entrance cave first. Only a later host
            // floor warp event may move it from that station into a generated floor.
            return entrance;
        }
        if (area is WorkerCombatAreaCatalog.IslandFarm or WorkerCombatAreaCatalog.VolcanoEntrance
            && !Game1.MasterPlayer.hasOrWillReceiveMail("willyBoatFixed"))
            return null;
        return Game1.getLocationFromName(area);
    }

    private void TryFollowTransition(NPC worker, GameLocation destination, bool nearFarmer)
    {
        Point landing;
        if (nearFarmer)
        {
            if (!this.TryChooseFloorLanding(destination, worker, out landing))
            {
                this.monitor.Log($"No safe landing with room to move was available for {worker.displayName} anywhere in {destination.NameOrUniqueName}; staying on the previous floor.", LogLevel.Info);
                return;
            }
            this.monitor.Log($"{worker.displayName} will follow into {destination.NameOrUniqueName} at {landing} "
                + $"({Math.Abs(landing.X - Game1.player.TilePoint.X) + Math.Abs(landing.Y - Game1.player.TilePoint.Y)} tiles from the farmer).", LogLevel.Trace);
        }
        else if (!this.TryChooseLanding(destination, worker, out landing))
            return;

        this.navigation.StopWorker(worker);
        this.animations.Stop(worker);
        GameLocation? previous = worker.currentLocation;
        try
        {
            Game1.warpCharacter(worker, destination, landing.ToVector2());
            if (worker.currentLocation == destination && previous != destination)
                previous?.characters.Remove(worker);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Could not safely move {worker.displayName} into {destination.NameOrUniqueName}: {ex.Message}", LogLevel.Warn);
        }
    }

    private Monster? FindTarget(GameLocation location, NPC worker, string workerId, CombatState state)
    {
        HashSet<Monster> reserved = this.states
            .Where(pair => !string.Equals(pair.Key, workerId, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value.Target).OfType<Monster>().ToHashSet();
        return location.characters.OfType<Monster>()
            .Where(monster => monster.Health > 0 && !monster.isInvincible() && !reserved.Contains(monster)
                && this.IsRevealedTarget(monster)
                && (!state.FailedTargets.TryGetValue(monster, out int retryAt) || Game1.ticks >= retryAt))
            .OrderBy(monster => Math.Abs(worker.TilePoint.X - monster.TilePoint.X)
                + Math.Abs(worker.TilePoint.Y - monster.TilePoint.Y))
            .FirstOrDefault();
    }

    private bool IsRevealedTarget(Monster monster)
    {
        if (!this.CanDealContactDamage(monster))
            return false;
        if (monster is Bug bug)
            return WorkerCombatTargetPolicy.CanTargetBug(bug.isArmoredBug.Value);
        return true;
    }

    private bool CanDealContactDamage(Monster monster)
    {
        if (monster is RockCrab crab)
            return WorkerCombatTargetPolicy.IsRevealedCrab(crab.isMoving());
        if (monster is Duggy duggy)
            return WorkerCombatTargetPolicy.IsExposedDuggy(duggy.IsInvisible,
                duggy.Sprite?.CurrentFrame ?? -1);
        return !monster.IsInvisible;
    }

    private bool TryStartApproach(GameLocation location, NPC worker, Point target)
    {
        Point[] candidates = { new(target.X + 1, target.Y), new(target.X - 1, target.Y),
            new(target.X, target.Y + 1), new(target.X, target.Y - 1) };
        foreach (Point tile in candidates.OrderBy(tile => Math.Abs(tile.X - worker.TilePoint.X)
            + Math.Abs(tile.Y - worker.TilePoint.Y)))
        {
            if (this.IsSafeCombatMovementTile(location, worker, tile))
            {
                if (this.navigation.TryStartTravel(worker,
                        new WorkerNavigationTarget(location.NameOrUniqueName, tile, TestWorkerDefinition.FacingDirection),
                        "approach monster", out _, stepValidator: step => this.IsSafeCombatRouteStep(location, step),
                        retryBlockedRoute: false))
                    return true;
            }
        }
        return false;
    }

    private bool TryStartPatrol(GameLocation location, NPC worker)
    {
        Point origin = worker.TilePoint;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            int dx = Game1.random.Next(-6, 7);
            int dy = Game1.random.Next(-6, 7);
            if (!WorkerCombatPolicy.IsUsefulPatrolOffset(dx, dy))
                continue;
            Point candidate = new(origin.X + dx, origin.Y + dy);
            if (!this.IsSafeCombatMovementTile(location, worker, candidate))
                continue;
            if (this.navigation.TryStartTravel(worker,
                    new WorkerNavigationTarget(location.NameOrUniqueName, candidate, TestWorkerDefinition.FacingDirection),
                    "combat patrol", out _, stepValidator: step => this.IsSafeCombatRouteStep(location, step),
                    retryBlockedRoute: false))
                return true;
        }
        return false;
    }

    private bool TryChooseLanding(GameLocation location, NPC worker, out Point landing)
    {
        bool caveEntrance = location.NameOrUniqueName is "Mine" or "SkullCave";
        if (caveEntrance && Game1.player.currentLocation == location)
            return this.TryChooseConnectedLandingNearFarmer(location, worker, 4, out landing);

        foreach (Warp warp in location.warps)
        {
            if (this.TryFindSafeTileNear(location, worker, new Point(warp.X, warp.Y), caveEntrance ? 4 : 8, out landing))
                return true;
        }
        // Interior cave entrances must be reached from their authored exit area;
        // scanning the whole map can select invisible border tiles as landings.
        if (caveEntrance)
        {
            landing = Point.Zero;
            return false;
        }
        for (int y = 1; y < 80; y++)
        for (int x = 1; x < 100; x++)
        {
            Point candidate = new(x, y);
            if (this.IsSafeCombatLandingTile(location, worker, candidate))
            {
                landing = candidate;
                return true;
            }
        }
        landing = Point.Zero;
        return false;
    }

    private bool TryFindSafeTileNear(GameLocation location, NPC worker, Point anchor, int maxRadius, out Point tile)
    {
        for (int radius = 1; radius <= maxRadius; radius++)
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                continue;
            Point candidate = new(anchor.X + dx, anchor.Y + dy);
            if (this.IsSafeCombatLandingTile(location, worker, candidate))
            {
                tile = candidate;
                return true;
            }
        }
        tile = Point.Zero;
        return false;
    }

    private bool IsSafeCombatLandingTile(GameLocation location, NPC worker, Point tile)
    {
        // Initial placement must avoid decorative wall/edge tiles. This mine-specific
        // test is too strict for ordinary movement across passable mine overlays.
        bool onMap = this.IsSafeCombatRouteStep(location, tile);
        bool hasGround = onMap && location.hasTileAt(tile.X, tile.Y, "Back");
        if (hasGround && location is MineShaft mine)
            hasGround = mine.isTileOnClearAndSolidGround(tile.ToVector2());
        bool mapPassable = hasGround && location.isTilePassable(tile.ToVector2());
        bool navigationWalkable = mapPassable && this.navigation.IsWalkableWorkTile(location, worker, tile);
        return WorkerCombatTilePolicy.IsSafeTile(onMap, hasGround, mapPassable, navigationWalkable);
    }

    private bool IsSafeCombatMovementTile(GameLocation location, NPC worker, Point tile)
        => this.IsSafeCombatRouteStep(location, tile)
            && this.navigation.IsWalkableWorkTile(location, worker, tile);

    private bool IsSafeCombatRouteStep(GameLocation location, Point tile)
    {
        if (!location.isTileOnMap(tile.ToVector2()))
            return false;
        bool hasGround = location.hasTileAt(tile.X, tile.Y, "Back");
        bool passable = hasGround && location.isTilePassable(tile.ToVector2());
        return WorkerCombatTilePolicy.IsSafeRouteStep(true, hasGround, passable,
            location.doesTileHaveProperty(tile.X, tile.Y, "NPCBarrier", "Back") is not null,
            location.warps.Any(warp => warp.X == tile.X && warp.Y == tile.Y));
    }

    private bool TryChooseConnectedLandingNearFarmer(GameLocation location, NPC worker, int maxRadius, out Point landing)
    {
        Point origin = Game1.player.TilePoint;
        bool found = WorkerCombatTilePolicy.TryChooseConnectedLanding(origin.X, origin.Y, maxRadius,
            (x, y) => this.IsSafeCombatLandingTile(location, worker, new Point(x, y)), out (int X, int Y) tile);
        landing = found ? new Point(tile.X, tile.Y) : Point.Zero;
        return found;
    }

    private bool TryChooseFloorLanding(GameLocation location, NPC worker, out Point landing)
    {
        var ground = location.map.RequireLayer("Back");
        Point origin = Game1.player.TilePoint;
        bool found = WorkerCombatTilePolicy.TryChooseFloorLanding(ground.LayerWidth, ground.LayerHeight,
            origin.X, origin.Y,
            (x, y) => this.IsSafeCombatRouteStep(location, new Point(x, y))
                && this.navigation.IsTraversableWorkTile(location, worker, new Point(x, y)),
            (x, y) => this.IsSafeCombatLandingTile(location, worker, new Point(x, y)), out var tile);
        landing = found ? new Point(tile.X, tile.Y) : Point.Zero;
        return found;
    }

    private void AttractMonsters(NPC worker, GameLocation location)
    {
        Point workerTile = worker.TilePoint;
        foreach (Monster monster in location.characters.OfType<Monster>())
        {
            if (monster.Health <= 0
                || this.lastAggroTick.GetValueOrDefault(monster) == Game1.ticks)
                continue;

            // Vanilla Duggies only watch the nearest Farmer. Give an on-duty
            // worker the same proximity trigger, then let the vanilla animation
            // and damage frames run normally. Emerging Duggies are not moved or
            // targeted until their exposed frames.
            if (monster is Duggy duggy && this.TryTriggerDuggyForWorker(duggy, worker, location))
            {
                this.lastAggroTick[monster] = Game1.ticks;
                continue;
            }

            // Don't pull a monster which the worker cannot actually fight.
            // In particular, armored bugs require a player's Bug Killer
            // enchantment and reject this worker's farmer-free damage call.
            if (!this.IsRevealedTarget(monster))
                continue;

            Point monsterTile = monster.TilePoint;
            int distance = DistanceSquared(monsterTile, workerTile);
            int nearestFarmer = location.farmers
                .Where(farmer => !farmer.hidden.Value)
                .Select(farmer => DistanceSquared(monsterTile, farmer.TilePoint))
                .DefaultIfEmpty(int.MaxValue).Min();
            if (!WorkerCombatPolicy.ShouldAggroWorker(distance, nearestFarmer))
                continue;

            // The closest on-duty worker owns this monster's attention for this tick.
            // This prevents two workers from pulling it in opposite directions.
            bool closerWorker = this.shell.GetSpawnedWorkers(recoverStale: false).Any(other => other != worker
                && other.currentLocation == location
                && this.shell.TryGetWorkerId(other, out string otherId)
                && this.shell.GetAssignedTask(otherId) == WorkerTaskKind.SlayMonsters
                && !this.IsDefeatedToday(otherId)
                && DistanceSquared(monsterTile, other.TilePoint) < distance);
            if (closerWorker)
                continue;

            this.lastAggroTick[monster] = Game1.ticks;
            Vector2 before = monster.Position;
            int dx = workerTile.X - monsterTile.X;
            int dy = workerTile.Y - monsterTile.Y;
            // MovePosition uses the game's collision rules. Try the other axis if
            // the direct approach is blocked by a wall or map obstacle.
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                this.MoveMonsterToward(monster, location, dx, horizontal: true);
                if (monster.Position == before && dy != 0)
                    this.MoveMonsterToward(monster, location, dy, horizontal: false);
            }
            else
            {
                this.MoveMonsterToward(monster, location, dy, horizontal: false);
                if (monster.Position == before && dx != 0)
                    this.MoveMonsterToward(monster, location, dx, horizontal: true);
            }
        }
    }

    private bool TryTriggerDuggyForWorker(Duggy duggy, NPC worker, GameLocation location)
    {
        if (duggy.Sprite is null)
            return false;

        int frame = duggy.Sprite.CurrentFrame;
        Rectangle triggerArea = duggy.GetBoundingBox();
        triggerArea.Inflate(128, 128);
        bool workerInRange = triggerArea.Contains(worker.StandingPixel);

        Point workerTile = worker.TilePoint;
        bool validTile = false;
        if (location.isTileOnMap(workerTile.ToVector2()))
        {
            xTile.Tiles.Tile? tile = location.map.RequireLayer("Back").Tiles[workerTile.X, workerTile.Y];
            validTile = tile is not null
                && !tile.Properties.ContainsKey("NPCBarrier")
                && (tile.TileIndex == 0 || tile.TileIndexProperties.ContainsKey("Diggable"));
        }

        if (!WorkerCombatTargetPolicy.ShouldTriggerDuggy(
                duggy.IsInvisible, frame, workerInRange, validTile))
            return false;

        duggy.Position = worker.Tile * 64f;
        duggy.IsInvisible = false;
        duggy.Sprite.interval = 100f;
        location.localSound("Duggy");
        return true;
    }

    private static int DistanceSquared(Point a, Point b)
    {
        int dx = a.X - b.X;
        int dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private void MoveMonsterToward(Monster monster, GameLocation location, int delta, bool horizontal)
    {
        if (delta == 0)
            return;
        if (horizontal)
        {
            if (delta < 0) monster.SetMovingOnlyLeft();
            else monster.SetMovingOnlyRight();
        }
        else
        {
            if (delta < 0) monster.SetMovingOnlyUp();
            else monster.SetMovingOnlyDown();
        }
        monster.MovePosition(Game1.currentGameTime, Game1.viewport, location);
    }

    private void ApplyMonsterContact(NPC worker, string workerId, CombatState state, GameLocation location)
    {
        if (Game1.ticks < state.NextContactTick)
            return;
        Rectangle workerBox = worker.GetBoundingBox();
        workerBox.Inflate(12, 12);
        Monster? contact = location.characters.OfType<Monster>()
            .FirstOrDefault(monster => monster.Health > 0 && this.CanDealContactDamage(monster)
                && monster.GetBoundingBox().Intersects(workerBox));
        if (contact is null)
            return;
        state.NextContactTick = Game1.ticks + 90;
        state.Health = WorkerCombatPolicy.HealthAfterContact(state.Health, contact.DamageToFarmer);
        worker.modData[HealthDataKey] = state.Health.ToString();
        this.shell.RecordCombatHealth(workerId, state.Health);
        if (state.Health > 0)
        {
            this.PrioritizeAttacker(worker, workerId, state, location, contact);
            return;
        }

        state.DefeatedToday = true;
        this.shell.MarkDefeatedToday(workerId);
        state.Target = null;
        state.SwingTarget = null;
        this.navigation.StopWorker(worker);
        this.animations.Stop(worker);
        this.monitor.Log($"Defeat details: worker={workerId}, location={location.NameOrUniqueName}, tile={worker.TilePoint}, attacker={contact.displayName}.", LogLevel.Trace);
        this.TryPlaceDefeatedAtHome(worker);
        string message = $"{worker.displayName} was defeated and is off duty until tomorrow.";
        this.monitor.Log(message, LogLevel.Info);
        Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
    }

    private void PrioritizeAttacker(NPC worker, string workerId, CombatState state,
        GameLocation location, Monster attacker)
    {
        if (ReferenceEquals(state.Target, attacker))
            return;

        bool eligible = attacker.Health > 0 && location.characters.Contains(attacker)
            && this.IsRevealedTarget(attacker) && !attacker.isInvincible()
            && (attacker is not RockCrab crab || WorkerCombatTargetPolicy.CanDamageCrabNow(
                crab.isStickBug.Value, crab.shellGone.Value,
                crab.Sprite is not null && crab.Sprite.CurrentFrame % 4 != 0));
        Monster? next = WorkerCombatTargetPolicy.TargetAfterContact(state.Target, attacker, eligible);
        if (ReferenceEquals(next, state.Target))
            return;

        // Retaliation transfers this monster's reservation to the worker it hit.
        // The old owner must give up both its target and its route to that target.
        foreach ((string otherId, CombatState otherState) in this.states)
        {
            if (string.Equals(otherId, workerId, StringComparison.OrdinalIgnoreCase)
                || !ReferenceEquals(otherState.Target, attacker))
                continue;
            otherState.Target = null;
            otherState.NextRouteTick = Game1.ticks;
            if (this.shell.TryGetWorker(otherId, out NPC? otherWorker) && otherWorker is not null)
                this.navigation.StopTravel(otherWorker);
        }

        this.navigation.StopTravel(worker);
        state.Target = next;
        state.FailedTargets.Remove(attacker);
        state.Patrolling = false;
        state.NextRouteTick = Game1.ticks;
        // Keep NextAttackTick: receiving a hit does not grant an extra swing.
    }

    private void TryPlaceDefeatedAtHome(NPC worker)
    {
        WorkerNavigationTarget? home = this.shell.GetWorkerReturnTargets(worker, new HashSet<Point>()).FirstOrDefault();
        if (home is null)
        {
            this.monitor.Log($"{worker.displayName} was defeated, but no safe home tile was available.", LogLevel.Warn);
            return;
        }
        try
        {
            Game1.warpCharacter(worker, home.LocationName, home.Tile);
            this.navigation.ApplyIdlePose(worker);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Could not place defeated {worker.displayName} at home; will retry: {ex.Message}", LogLevel.Warn);
        }
    }

    private void Strike(NPC worker, string workerId, CombatState state, GameLocation location, Monster target)
    {
        if (target.Health <= 0 || !location.characters.Contains(target) || target.isInvincible()
            || !this.IsRevealedTarget(target))
            return;
        // The farmer-free Monster overload bypasses RockCrab.takeDamage's
        // shell immunity. Match its visible vulnerable state before striking.
        if (target is RockCrab crab && !WorkerCombatTargetPolicy.CanDamageCrabNow(
                crab.isStickBug.Value, crab.shellGone.Value,
                crab.Sprite is not null && crab.Sprite.CurrentFrame % 4 != 0))
            return;
        // Keep the direction captured at windup; damage must not redirect the
        // visual slash halfway through when the monster moves or overlaps us.
        int dealt = target.takeDamage(10, 0, 0, false, 0d, "hitEnemy");
        this.monitor.Log($"{worker.displayName} struck {target.displayName} at {target.TilePoint}: "
            + $"damage={dealt}, remaining health={target.Health}.", LogLevel.Trace);
        if (dealt <= 0 || target.Health > 0)
            return;

        // Vanilla onMonsterKilled credits a Farmer's quests, stats and XP. Worker kills use
        // the monster's own loot list and the existing shared worker storage destination.
        Vector2 origin = target.getStandingPosition();
        foreach (string raw in target.objectsToDrop.ToArray())
        {
            try
            {
                Debris generated = raw.StartsWith('-') && int.TryParse(raw, out int resource)
                    ? new Debris(Math.Abs(resource), Game1.random.Next(1, 4), origin, origin)
                    : new Debris(raw, origin, origin);
                this.StoreGeneratedLoot(location, target.ModifyMonsterLoot(generated));
            }
            catch (Exception ex)
            {
                this.monitor.Log($"Could not create monster drop '{raw}'; preserving it as debris: {ex.Message}", LogLevel.Warn);
                try
                {
                    location.debris.Add(new Debris(raw, origin, origin));
                }
                catch (Exception preserveError)
                {
                    this.monitor.Log($"Monster drop '{raw}' could not be represented as world debris: {preserveError}", LogLevel.Error);
                }
            }
        }
        foreach (Item item in target.getExtraDropItems())
        {
            try
            {
                this.StoreGeneratedLoot(location, target.ModifyMonsterLoot(new Debris(item, origin, origin)));
            }
            catch (Exception ex)
            {
                this.monitor.Log($"Could not store an extra monster drop; leaving it in the world: {ex.Message}", LogLevel.Warn);
                Game1.createItemDebris(item, origin, -1, location);
            }
        }
        if (target.ShouldMonsterBeRemoved())
            location.characters.Remove(target);
        this.shell.TryAwardCombatKillExperience(workerId, target.ExperienceGained);
        this.onConfirmedKill(workerId);
        state.Target = null;
    }

    private void StoreGeneratedLoot(GameLocation location, Debris debris)
    {
        // Put the drop in the world first. If an item type or destination is unavailable,
        // the physical drop remains rather than being swallowed by a failed storage call.
        location.debris.Add(debris);
        try
        {
            Item? item = debris.item;
            if (item is null && !string.IsNullOrWhiteSpace(debris.itemId.Value)
                && debris.debrisType.Value is Debris.DebrisType.OBJECT or Debris.DebrisType.RESOURCE or Debris.DebrisType.ARCHAEOLOGY)
                item = ItemRegistry.Create(debris.itemId.Value, Math.Max(1, debris.Chunks.Count), debris.itemQuality);
            if (item is null)
                return;
            Item forStorage = item.getOne();
            forStorage.Stack = item.Stack;
            WorkerItemStorage.Store(forStorage, this.shell.GetHarvestDestination(), this.monitor);
            location.debris.Remove(debris);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Could not store monster loot in shared storage; left it on the ground: {ex.Message}", LogLevel.Warn);
        }
    }
}
