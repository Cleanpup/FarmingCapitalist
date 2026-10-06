using FarmingCapitalist.Workers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using xTile;

if (args.Length != 1)
    throw new ArgumentException("Pass the installed Stardew Valley game directory as the first argument.");

using ContentManager content = new(new EmptyServices(), Path.Combine(args[0], "Content"));
Map farm = content.Load<Map>("Maps/Farm");
Map forest = content.Load<Map>("Maps/Forest");
bool IsNpcBarrier(int x, int y) => farm.GetLayer("Back").Tiles[x, y]?.Properties.ContainsKey("NPCBarrier") == true;

Assert(IsNpcBarrier(40, 62) && IsNpcBarrier(41, 62), "The Standard Farm south arrival is isolated by NPC barriers.");
Assert(IsNpcBarrier(78, 17) && IsNpcBarrier(78, 18), "The Standard Farm east arrival is isolated by NPC barriers.");
Assert(IsNpcBarrier(40, 6) && IsNpcBarrier(41, 6), "The Standard Farm north arrival is isolated by NPC barriers.");
Assert(forest.Properties["Warp"].ToString()!.Contains("67 -1 Farm 41 64"), "The Forest-to-Farm exit must be an authored warp.");

int fartherBuilds = 0;
WorkerWarpOption<Leg>[] options =
[
    new(new Point(-1, 7), () => null),
    new(new Point(10, 8), () => (new Leg("long walk"), 20)),
    new(new Point(12, 8), () => (new Leg("short walk"), 12)),
    new(new Point(120, 25), () => { fartherBuilds++; return (new Leg("far exit"), 130); }),
];
Leg? chosen = WorkerDirectWarpPlanner.ChooseNearest(new Point(1, 8), options);
Assert(chosen?.Name == "short walk", "The nearest reachable warp must win by walking distance.");
Assert(fartherBuilds == 0, "A warp farther than the best possible route should not start another path search.");

bool found = WorkerDirectWarpPlanner.TryChooseLanding(new Point(6, 7), 2,
    point => point == new Point(6, 7) || point == new Point(5, 7), out Point landing);
Assert(found && landing == new Point(6, 7), "An open requested destination should be used exactly.");

found = WorkerDirectWarpPlanner.TryChooseLanding(new Point(6, 7), 2,
    point => point == new Point(5, 7), out landing);
Assert(found && landing == new Point(5, 7), "A blocked destination should use the nearest allowed free tile.");

found = WorkerDirectWarpPlanner.TryChooseLanding(new Point(6, 7), 3,
    point => point == new Point(4, 5) || point == new Point(3, 7), out landing);
Assert(found && landing == new Point(3, 7), "Landing fallback should prioritize walking distance over square radius.");

found = WorkerDirectWarpPlanner.TryChooseLanding(new Point(6, 7), 2, _ => false, out landing);
Assert(!found, "Warp planning must fail when no safe landing exists.");

Point crop = new(64, 20);
found = WorkerDirectWarpPlanner.TryChooseLanding(crop, 2,
    point => WorkerCropApproachPolicy.IsNearbyLanding(point, crop, cropStillEligible: true)
        && point == new Point(63, 20), out landing);
Assert(found && landing == new Point(63, 20)
    && WorkerCropApproachPolicy.IsInWorkRange(landing, crop),
    "A blocked crop tile must allow a safe adjacent landing while preserving the crop as the job target.");
Assert(!WorkerCropApproachPolicy.IsNearbyLanding(new Point(63, 20), crop, cropStillEligible: false)
    && !WorkerCropApproachPolicy.IsInWorkRange(new Point(62, 20), crop),
    "A changed crop or distant landing must not authorize crop work.");

Console.WriteLine("Direct warp checks passed: authored exits, nearest reachable warp, safe landings, and adjacent crop arrival.");

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
