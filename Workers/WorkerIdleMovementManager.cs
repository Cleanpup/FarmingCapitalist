using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;

namespace FarmingCapitalist.Workers;

/// <summary>Host-owned, visual-only Farm activity for workers with no assigned job.</summary>
internal sealed class WorkerIdleMovementManager
{
    private const int FarmSearchRadius = 14;
    private const int WanderRadius = 8;
    private const int RetryDelayTicks = 180;
    private readonly WorkerNavigationManager navigation;
    private readonly WorkerShellManager shells;
    private readonly IMonitor monitor;
    private readonly Random random = new();
    private readonly Dictionary<string, int> nextMoveTicks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Point> destinations = new(StringComparer.OrdinalIgnoreCase);

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

            Point[] choices = tiles.Where(tile => Distance(tile, door) >= 3 && Distance(tile, door) <= 10
                    && this.IsAvailable(farm, worker, tile))
                .OrderBy(_ => this.random.Next()).ToArray();
            Point landing = choices.FirstOrDefault(tile => placed.All(other => Distance(tile, other) >= 3));
            if (landing == Point.Zero)
                landing = choices.FirstOrDefault(tile => !placed.Contains(tile));
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
                Point[] choices = tiles.Where(tile => Distance(tile, door) >= 3 && Distance(tile, door) <= 7
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

        List<Point> nearby = this.GetConnectedTiles(farm, worker, worker.TilePoint, WanderRadius);
        Point[] candidates = nearby.Where(tile => Distance(tile, worker.TilePoint) >= 3
                && this.IsAvailable(farm, worker, tile))
            .OrderBy(_ => this.random.Next()).Take(6).ToArray();
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

        tiles = this.GetConnectedTiles(farm, worker, start, FarmSearchRadius);
        return tiles.Count > 0;
    }

    private List<Point> GetConnectedTiles(Farm farm, NPC worker, Point start, int radius)
    {
        Queue<Point> pending = new();
        HashSet<Point> seen = new() { start };
        List<Point> connected = new();
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            Point tile = pending.Dequeue();
            connected.Add(tile);
            foreach (Point neighbor in new[]
            {
                new Point(tile.X, tile.Y - 1), new Point(tile.X + 1, tile.Y),
                new Point(tile.X, tile.Y + 1), new Point(tile.X - 1, tile.Y),
            })
            {
                if (Distance(neighbor, start) > radius || !seen.Add(neighbor)
                    || !this.navigation.IsTraversableWorkTile(farm, worker, neighbor))
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
