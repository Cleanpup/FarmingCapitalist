using FarmingCapitalist.Workers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using xTile;

if (args.Length != 1)
    throw new ArgumentException("Pass the installed Stardew Valley game directory as the first argument.");

using ContentManager content = new(new EmptyServices(), Path.Combine(args[0], "Content"));
Map farm = content.Load<Map>("Maps/Farm");
bool IsNpcBarrier(int x, int y) => farm.GetLayer("Back").Tiles[x, y]?.Properties.ContainsKey("NPCBarrier") == true;

Assert(IsNpcBarrier(40, 62) && IsNpcBarrier(41, 62), "The Standard Farm south lane must have both NPC barriers.");
Assert(!IsNpcBarrier(40, 63) && !IsNpcBarrier(41, 63), "The Forest arrival lane should be open below the barrier.");
Assert(!IsNpcBarrier(40, 61) && !IsNpcBarrier(41, 61), "The farm interior should be open above the barrier.");
Assert(IsNpcBarrier(78, 17) && IsNpcBarrier(78, 18), "The Bus Stop arrival must be outside the east NPC barrier.");
Assert(IsNpcBarrier(40, 6) && IsNpcBarrier(41, 6), "The Backwoods arrival must be outside the north NPC barrier.");

Point southArrival = new(41, 64);
Point eastArrival = new(79, 17);
Point interiorArrival = new(64, 15);
Point houseArrival = new(3, 11);
Point houseTarget = new(6, 7);
int trappedWarpBuilds = 0;

IReadOnlyList<WorkerRouteOption<Leg>> GetOptions(string from, Point start, string to)
{
    return (from, to) switch
    {
        ("Forest", "Farm") => [new(southArrival, () => { trappedWarpBuilds++; return new Leg("south Forest exit"); })],
        ("Forest", "Town") => [new(new Point(0, 90), () => new Leg("east Forest exit"))],
        ("Town", "BusStop") => [new(new Point(44, 24), () => new Leg("Town to Bus Stop"))],
        ("BusStop", "Farm") => [new(eastArrival, () => { trappedWarpBuilds++; return new Leg("east Farm entrance"); })],
        ("Farm", "FarmHouse") when start == interiorArrival => [new(houseArrival, () => new Leg("farmhouse door"))],
        _ => [],
    };
}

bool CanReachTarget(string location, Point start, Point destination)
    => location == "FarmHouse" && start == houseArrival && destination == houseTarget;

string[][] routes =
[
    ["Forest", "Farm", "FarmHouse"],
    ["Forest", "BusStop", "Farm", "FarmHouse"],
    ["Forest", "Town", "BusStop", "Farm", "FarmHouse"],
];

bool found = WorkerRouteSelector.TrySelect(routes, new Point(1, 8), houseTarget,
    GetOptions, CanReachTarget, out Leg? firstLeg, out string[]? selectedRoute);
Assert(!found && firstLeg is null && selectedRoute is null, "All authored Standard Farm outdoor arrivals are trapped by NPC barriers.");
Assert(trappedWarpBuilds == 0, "A warp into a known dead end must not start an expensive first-leg path search.");

// On a map with a reachable east arrival, the same planner must use the alternate route.
IReadOnlyList<WorkerRouteOption<Leg>> WithReachableEastArrival(string from, Point start, string to)
    => (from, to) == ("BusStop", "Farm")
        ? [new(interiorArrival, () => new Leg("reachable Farm entrance"))]
        : GetOptions(from, start, to);

found = WorkerRouteSelector.TrySelect(routes, new Point(1, 8), houseTarget,
    WithReachableEastArrival, CanReachTarget, out firstLeg, out selectedRoute);
Assert(found && firstLeg?.Name == "east Forest exit", "A trapped south arrival must be rejected in favor of a reachable alternate route.");
Assert(selectedRoute is not null && string.Join(" -> ", selectedRoute) == "Forest -> Town -> BusStop -> Farm -> FarmHouse",
    "The selected alternate route must reach the farmhouse.");

// A shorter first warp can land on the wrong side of the same map. Try its alternate arrival.
IReadOnlyList<WorkerRouteOption<Leg>> WithAlternateFarmArrival(string from, Point start, string to)
    => (from, to) == ("Forest", "Farm")
        ? [new(southArrival, () => new Leg("short trapped warp")), new(interiorArrival, () => new Leg("long reachable warp"))]
        : GetOptions(from, start, to);

found = WorkerRouteSelector.TrySelect([routes[0]], new Point(1, 8), houseTarget,
    WithAlternateFarmArrival, CanReachTarget, out firstLeg, out selectedRoute);
Assert(found && firstLeg?.Name == "long reachable warp", "A second warp into the same location must remain eligible.");

found = WorkerRouteSelector.TrySelect([routes[0]], new Point(1, 8), houseTarget,
    GetOptions, CanReachTarget, out firstLeg, out selectedRoute);
Assert(!found && firstLeg is null && selectedRoute is null, "A route ending below the NPC barrier must fail safely.");

found = WorkerRouteSelector.TrySelect(routes, new Point(1, 8), houseTarget,
    WithReachableEastArrival, (_, _, _) => false, out firstLeg, out selectedRoute);
Assert(!found && firstLeg is null && selectedRoute is null, "A route whose final tile is unreachable must fail safely.");

Console.WriteLine("Route selector checks passed: all Standard Farm outdoor barriers, lazy rejection, alternate map route, alternate arrival, and unreachable destination cases.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

internal sealed record Leg(string Name);
internal sealed class EmptyServices : IServiceProvider
{
    public object? GetService(Type serviceType) => null;
}
