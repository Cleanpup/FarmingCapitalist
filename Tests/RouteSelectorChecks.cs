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

Point southArrival = new(41, 64);
Point northArrival = new(79, 18);
Point houseArrival = new(3, 11);
Point houseTarget = new(6, 7);

bool FarmArrivalCanReachHouse(Point arrival)
{
    if (arrival != southArrival)
        return true;

    // Both tiles across this two-wide lane are blocked in the installed map.
    return !IsNpcBarrier(40, 62) || !IsNpcBarrier(41, 62);
}

IReadOnlyList<WorkerRouteOption<Leg>> GetOptions(string from, Point start, string to)
{
    return (from, to) switch
    {
        ("Forest", "Farm") => [new(new Leg("south Forest exit"), southArrival)],
        ("Forest", "Town") => [new(new Leg("east Forest exit"), new Point(0, 90))],
        ("Town", "BusStop") => [new(new Leg("Town to Bus Stop"), new Point(44, 24))],
        ("BusStop", "Farm") => [new(new Leg("north Farm entrance"), northArrival)],
        ("Farm", "FarmHouse") when FarmArrivalCanReachHouse(start) => [new(new Leg("farmhouse door"), houseArrival)],
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
Assert(found && firstLeg?.Name == "east Forest exit", "A trapped south Farm arrival must be rejected in favor of the reachable first warp.");
Assert(selectedRoute is not null && string.Join(" -> ", selectedRoute) == "Forest -> Town -> BusStop -> Farm -> FarmHouse",
    "The selected route must reach the farmhouse through a usable Farm entrance.");

// A shorter first warp can land on the wrong side of the same map. Try its alternate arrival.
IReadOnlyList<WorkerRouteOption<Leg>> WithAlternateFarmArrival(string from, Point start, string to)
    => (from, to) == ("Forest", "Farm")
        ? [new(new Leg("short trapped warp"), southArrival), new(new Leg("long reachable warp"), northArrival)]
        : GetOptions(from, start, to);

found = WorkerRouteSelector.TrySelect([routes[0]], new Point(1, 8), houseTarget,
    WithAlternateFarmArrival, CanReachTarget, out firstLeg, out selectedRoute);
Assert(found && firstLeg?.Name == "long reachable warp", "A second warp into the same location must remain eligible.");

found = WorkerRouteSelector.TrySelect([routes[0]], new Point(1, 8), houseTarget,
    GetOptions, CanReachTarget, out firstLeg, out selectedRoute);
Assert(!found && firstLeg is null && selectedRoute is null, "A route ending below the NPC barrier must fail safely.");

found = WorkerRouteSelector.TrySelect(routes, new Point(1, 8), houseTarget,
    GetOptions, (_, _, _) => false, out firstLeg, out selectedRoute);
Assert(!found && firstLeg is null && selectedRoute is null, "A route whose final tile is unreachable must fail safely.");

Console.WriteLine("Route selector checks passed: Standard Farm barrier, alternate map route, alternate arrival, and unreachable destination cases.");

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
