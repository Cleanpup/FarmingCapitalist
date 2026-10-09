using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;

namespace FarmingCapitalist.Workers;

/// <summary>Host-owned, visual-only Farm activity for workers with no assigned job.</summary>
internal sealed class WorkerIdleMovementManager
{
    private const int MaximumFarmSearchTiles = 12000;
    private const int MinimumWanderDistance = 12;
    private const int MinimumWorkerSeparation = 7;
    private const int RetryDelayTicks = 180;
    private const int FarmRegionRefreshTicks = 1800;
    private readonly WorkerNavigationManager navigation;
    private readonly WorkerShellManager shells;
    private readonly IMonitor monitor;
    private readonly Random random = new();
    private readonly Dictionary<string, int> nextMoveTicks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Point> destinations = new(StringComparer.OrdinalIgnoreCase);
    private Farm? cachedFarm;
    private Point cachedDoor;
    private List<Point>? cachedFarmTiles;
    private int cachedDay = -1;
    private int cachedUntilTick;

    public WorkerIdleMovementManager(WorkerNavigationManager navigation, WorkerShellManager shells, IMonitor monitor)
    {
        this.navigation = navigation;
        this.shells = shells;
        this.monitor = monitor;
    }

    public void Reset()
    {
        this.nextMoveTicks.Clear();
        this.destinations.Clear();
        this.cachedFarm = null;
        this.cachedFarmTiles = null;
    }

    public void Stop(string workerId)
    {
        this.nextMoveTicks.Remove(workerId);
        this.destinations.Remove(workerId);
    }

    /// <summary>Each morning starts outdoors; saved farmhouse tiles remain the workers' home targets.</summary>
    public void PlaceWorkersOnFarmForMorning()
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
            return;

        HashSet<Point> placed = new();
        foreach (NPC worker in this.shells.GetSpawnedWorkers())
        {
            if (!this.shells.TryGetWorkerId(worker, out string workerId)
                || !this.shells.CanWorkerWorkToday(workerId)
                || this.shells.WasDefeatedToday(workerId)
                || this.navigation.HasForeignController(worker)
                || !this.TryGetFarmEntranceRegion(worker, out Farm farm, out Point door, out List<Point> tiles))
                continue;

            Point[] choices = tiles.Where(tile => Distance(tile, door) >= 8)
                .OrderBy(_ => this.random.Next()).ToArray();
            Point landing = choices.FirstOrDefault(tile => placed.All(other => Distance(tile, other) >= 12)
                && this.IsAvailable(farm, worker, tile));
            if (landing == Point.Zero)
                landing = choices.FirstOrDefault(tile => placed.All(other => Distance(tile, other) >= 5)
                    && this.IsAvailable(farm, worker, tile));
            if (landing == Point.Zero)
                landing = choices.FirstOrDefault(tile => !placed.Contains(tile)
                    && this.IsAvailable(farm, worker, tile));
            if (landing == Point.Zero)
            {
                this.monitor.Log($"No clear Farm morning spawn point was available for {worker.displayName}; keeping the farmhouse position.", LogLevel.Trace);
                continue;
            }

            this.navigation.StopWorker(worker);
            try
            {
                Game1.warpCharacter(worker, farm.NameOrUniqueName, landing);
            }
            catch (Exception ex)
            {
                this.monitor.Log($"Could not place {worker.displayName} at a Farm morning spawn point: {ex.Message}", LogLevel.Warn);
                continue;
            }
            placed.Add(landing);
            this.nextMoveTicks[workerId] = Game1.ticks + this.random.Next(90, 210);
            this.monitor.Log($"{worker.displayName} started the morning on Farm tile {landing}.", LogLevel.Trace);
        }
    }

    public WorkerRuntimeSnapshot Update(NPC worker, string workerId)
    {
        if (worker.currentLocation is not Farm farm)
        {
            this.destinations.Remove(workerId);
            if (!this.navigation.HasActiveRoute(workerId) && worker.controller is null && this.Ready(workerId)
                && this.TryGetFarmEntranceRegion(worker, out Farm destination, out Point door, out List<Point> tiles))
            {
                Point[] choices = tiles.Where(tile => Distance(tile, door) >= 3 && Distance(tile, door) <= 8
                        && this.IsAvailable(destination, worker, tile))
                    .OrderBy(_ => this.random.Next()).Take(4).ToArray();
                HashSet<Point> allowed = choices.ToHashSet();
                bool started = false;
                foreach (Point choice in choices)
                {
                    if (!this.navigation.TryStartTravel(worker,
                            new WorkerNavigationTarget(destination.NameOrUniqueName, choice, 2),
                            "idle walk to Farm", out _, allowed.Contains, retryBlockedRoute: false))
                        continue;
                    started = true;
                    break;
                }
                this.nextMoveTicks[workerId] = Game1.ticks + (started ? 90 : RetryDelayTicks);
            }
            return new WorkerRuntimeSnapshot(WorkerTaskKind.Idle, "Traveling", "Heading out to the Farm", 0, null);
        }

        if (this.navigation.HasActiveRoute(workerId) || worker.controller is not null)
            return new WorkerRuntimeSnapshot(WorkerTaskKind.Idle, "Traveling", "Walking around the Farm", 0,
                this.destinations.TryGetValue(workerId, out Point destinationTile) ? destinationTile : null);

        if (this.destinations.Remove(workerId, out Point lastDestination))
        {
            if (worker.TilePoint == lastDestination)
            {
                this.FaceNearbyWork(worker, farm);
                this.nextMoveTicks[workerId] = Game1.ticks + this.random.Next(120, 300);
            }
            else
                this.nextMoveTicks[workerId] = Game1.ticks + RetryDelayTicks;
        }

        if (!this.Ready(workerId))
            return new WorkerRuntimeSnapshot(WorkerTaskKind.Idle, "Idle", "Looking around the Farm", 0, null);

        if (!this.TryGetFarmEntranceRegion(worker, out _, out Point farmDoor, out List<Point> reachable))
        {
            this.nextMoveTicks[workerId] = Game1.ticks + RetryDelayTicks;
            return new WorkerRuntimeSnapshot(WorkerTaskKind.Idle, "Idle", "Looking around the Farm", 0, null);
        }

        Point[] candidates = reachable.Where(tile => Distance(tile, worker.TilePoint) >= MinimumWanderDistance
                && Distance(tile, farmDoor) >= 3 && this.IsSeparated(farm, worker, workerId, tile))
            .OrderBy(_ => this.random.Next()).Take(30)
            .Where(tile => this.IsAvailable(farm, worker, tile)).Take(6).ToArray();
        if (candidates.Length == 0)
            candidates = reachable.Where(tile => Distance(tile, worker.TilePoint) >= 3
                    && Distance(tile, farmDoor) >= 3 && this.IsSeparated(farm, worker, workerId, tile, 3))
                .OrderBy(_ => this.random.Next()).Take(20)
                .Where(tile => this.IsAvailable(farm, worker, tile)).Take(4).ToArray();
        foreach (Point candidate in candidates)
        {
            if (!this.navigation.TryStartTravel(worker,
                    new WorkerNavigationTarget(farm.NameOrUniqueName, candidate, 2),
                    "idle wander", out WorkerNavigationTarget target, retryBlockedRoute: false))
                continue;
            this.destinations[workerId] = target.Tile;
            return new WorkerRuntimeSnapshot(WorkerTaskKind.Idle, "Traveling", "Walking around the Farm", 0, target.Tile);
        }

        this.nextMoveTicks[workerId] = Game1.ticks + RetryDelayTicks;
        return new WorkerRuntimeSnapshot(WorkerTaskKind.Idle, "Idle", "Looking around the Farm", 0, null);
    }

    private bool TryGetFarmEntranceRegion(NPC worker, out Farm farm, out Point door, out List<Point> tiles)
    {
        farm = Game1.getFarm();
        if (farm == this.cachedFarm && this.cachedFarmTiles is not null && this.cachedDay == Game1.Date.TotalDays
            && Game1.ticks < this.cachedUntilTick)
        {
            door = this.cachedDoor;
            tiles = this.cachedFarmTiles;
            return true;
        }
        try
        {
            door = farm.getWarpPointTo("FarmHouse", worker);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Could not find the Farmhouse entrance for idle movement: {ex.Message}", LogLevel.Trace);
            door = Point.Zero;
        }
        tiles = new List<Point>();
        if (door == Point.Zero)
            return false;

        Point start = Point.Zero;
        foreach (Point tile in new[]
        {
            new Point(door.X, door.Y + 2), new Point(door.X, door.Y + 1),
            new Point(door.X - 1, door.Y + 1), new Point(door.X + 1, door.Y + 1),
            new Point(door.X - 2, door.Y + 2), new Point(door.X + 2, door.Y + 2),
        })
        {
            if (!this.navigation.IsTraversableWorkTile(farm, worker, tile))
                continue;
            start = tile;
            break;
        }
        if (start == Point.Zero)
            return false;

        tiles = this.GetConnectedTiles(farm, worker, start);
        this.cachedFarm = farm;
        this.cachedDoor = door;
        this.cachedFarmTiles = tiles;
        this.cachedDay = Game1.Date.TotalDays;
        this.cachedUntilTick = Game1.ticks + FarmRegionRefreshTicks;
        this.monitor.Log($"Idle Farm reachability from {start}: {tiles.Count} connected tiles (Farmhouse door {door}).", LogLevel.Trace);
        return tiles.Count > 0;
    }

    private List<Point> GetConnectedTiles(Farm farm, NPC worker, Point start)
    {
        Queue<Point> pending = new();
        HashSet<Point> seen = new() { start };
        List<Point> connected = new();
        pending.Enqueue(start);
        while (pending.Count > 0 && connected.Count < MaximumFarmSearchTiles)
        {
            Point tile = pending.Dequeue();
            connected.Add(tile);
            foreach (Point neighbor in new[]
            {
                new Point(tile.X, tile.Y - 1), new Point(tile.X + 1, tile.Y),
                new Point(tile.X, tile.Y + 1), new Point(tile.X - 1, tile.Y),
            })
            {
                if (!seen.Add(neighbor) || !this.navigation.IsTraversableWorkTile(farm, worker, neighbor))
                    continue;
                pending.Enqueue(neighbor);
            }
        }
        return connected;
    }

    private bool IsAvailable(Farm farm, NPC worker, Point tile)
        => this.navigation.IsWalkableWorkTile(farm, worker, tile)
            && farm.getWarpFromDoor(tile, worker) is null
            && !farm.objects.ContainsKey(tile.ToVector2())
            && (!farm.terrainFeatures.TryGetValue(tile.ToVector2(), out TerrainFeature? feature)
                || feature is not HoeDirt);

    private bool IsSeparated(Farm farm, NPC worker, string workerId, Point tile, int minimumDistance = MinimumWorkerSeparation)
    {
        foreach (NPC other in farm.characters)
        {
            if (other == worker || other.IsInvisible || Distance(tile, other.TilePoint) >= minimumDistance)
                continue;
            return false;
        }
        foreach ((string otherId, Point destination) in this.destinations)
        {
            if (!string.Equals(otherId, workerId, StringComparison.OrdinalIgnoreCase)
                && Distance(tile, destination) < minimumDistance)
                return false;
        }
        return true;
    }

    private void FaceNearbyWork(NPC worker, Farm farm)
    {
        Point tile = worker.TilePoint;
        (Point Neighbor, int Facing)[] directions =
        {
            (new Point(tile.X, tile.Y - 1), 0), (new Point(tile.X + 1, tile.Y), 1),
            (new Point(tile.X, tile.Y + 1), 2), (new Point(tile.X - 1, tile.Y), 3),
        };
        int? facingWork = directions.Where(pair => farm.objects.ContainsKey(pair.Neighbor.ToVector2())
            || farm.terrainFeatures.ContainsKey(pair.Neighbor.ToVector2())).Select(pair => (int?)pair.Facing).FirstOrDefault();
        int facing = facingWork ?? this.random.Next(4);
        worker.FacingDirection = facing;
        worker.Sprite?.standAndFaceDirection(facing);
    }

    private bool Ready(string workerId)
        => !this.nextMoveTicks.TryGetValue(workerId, out int nextTick) || Game1.ticks >= nextTick;

    private static int Distance(Point first, Point second)
        => Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
}
