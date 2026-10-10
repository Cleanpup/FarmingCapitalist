using HireSkilledHelpers.Workers;
using Microsoft.Xna.Framework;

// Model the engine collision result, including terrain occupancy that does not
// block NPC movement. Exercise enumeration, routing, matching and landing together.
Point resource = new(14, 48);
Point grassApproach = new(13, 50);
Point start = new(7, 46);
Dictionary<Point, TileFacts> map = new();
for (int x = 7; x <= 12; x++) map[new Point(x, 46)] = new();
for (int y = 47; y <= 50; y++) map[new Point(12, y)] = new();
map[grassApproach] = new(Terrain: "Grass");
bool Safe(Point tile) => map.TryGetValue(tile, out TileFacts? facts)
    && WorkerWorkTilePolicy.CanStand(facts.OnMap, facts.MovementBlocked, facts.NpcBarrier, facts.Occupied, facts.Warp);
Point[] CurrentApproaches() => WorkerApproachTiles.AroundResource(resource, 2, 2, Safe).ToArray();
Target[] CurrentTargets() => CurrentApproaches().Select(tile => new Target(resource, tile)).ToArray();

Point[] perimeter = WorkerApproachTiles.AroundResource(resource, 2, 2, _ => true).ToArray();
Assert(perimeter.Length == 12 && perimeter.Distinct().Count() == 12
    && !perimeter.Any(tile => tile.X >= 14 && tile.X <= 15 && tile.Y >= 48 && tile.Y <= 49),
    "A two-by-two clump must expose its full perimeter, never a tile inside the resource.");

Assert(CurrentApproaches().SequenceEqual(new[] { grassApproach }),
    "The only NPC-walkable hardwood approach must survive enumeration even though grass occupies its tile.");
WorkerObstacleRoutePlanner planner = new(start, CurrentApproaches(),
    tile => Safe(tile) ? WorkerRouteTileKind.Open : WorkerRouteTileKind.Impassable);
WorkerObstaclePlanStatus status;
int batches = 0;
do
{
    status = planner.Advance(4, 128, out WorkerObstacleClearance? clearance);
    Assert(clearance is null, "A route across passable terrain must not clear grass or unrelated obstacles.");
    Assert(++batches <= 32, "The approach search must finish within its bounded grid.");
} while (status == WorkerObstaclePlanStatus.Searching);
Assert(status == WorkerObstaclePlanStatus.ReachableClearRoute && planner.ReachedTarget == grassApproach,
    "A grass-only work approach must be reachable by the obstacle fallback.");

Target[] initialSnapshot = Array.Empty<Target>();
Assert(!initialSnapshot.Any(target => target.Approach == grassApproach)
    && WorkerForagerApproachMatcher.TryFind(CurrentTargets(), planner.ReachedTarget!.Value,
        target => target.Approach, out Target? recovered) && recovered?.Resource == resource,
    "Recovery must map the reached tile to the current eligible stump even when absent from the initial snapshot.");
Assert(!WorkerForagerApproachMatcher.TryFind(Array.Empty<Target>(), grassApproach,
    target => target.Approach, out _), "A removed resource must not be recovered from a stale approach.");
Assert(WorkerDirectWarpPlanner.TryChooseLanding(grassApproach, 1, tile => tile == grassApproach && Safe(tile), out Point landing)
    && landing == grassApproach, "Safe warp arrival must permit passable grass at the intended work approach.");

foreach (TileFacts blocked in new[]
{
    new TileFacts(OnMap: false),
    new TileFacts(MovementBlocked: true, Terrain: "Wall"),
    new TileFacts(MovementBlocked: true, Terrain: "Placed object"),
    new TileFacts(MovementBlocked: true, Terrain: "Large clump"),
    new TileFacts(NpcBarrier: true),
    new TileFacts(Occupied: true, Terrain: "Grass"),
    new TileFacts(Warp: true),
})
{
    map[grassApproach] = blocked;
    Assert(CurrentApproaches().Length == 0, $"An unsafe approach must be excluded: {blocked}.");
    Assert(!WorkerDirectWarpPlanner.TryChooseLanding(grassApproach, 1,
        tile => tile == grassApproach && Safe(tile), out _), $"An unsafe landing must be rejected: {blocked}.");
    Assert(!WorkerForagerApproachMatcher.TryFind(CurrentTargets(), grassApproach,
        target => target.Approach, out _), "An approach occupied after planning must fail live recovery validation.");
}

Console.WriteLine("Work approach checks passed: grass-only hardwood route, live fallback recovery, safe grass landing, and blocked/occupied controls.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

internal sealed record Target(Point Resource, Point Approach);
internal sealed record TileFacts(bool OnMap = true, bool MovementBlocked = false, bool NpcBarrier = false,
    bool Occupied = false, bool Warp = false, string? Terrain = null);
