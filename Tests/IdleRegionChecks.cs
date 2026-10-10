using HireSkilledHelpers.Workers;
using Microsoft.Xna.Framework;

Point door = new(5, 2);
HashSet<Point> walkable = new()
{
    new(5, 3), new(6, 3), new(5, 4), // isolated porch
};
for (int x = 0; x <= 12; x++)
for (int y = 6; y <= 12; y++)
    walkable.Add(new Point(x, y));

bool found = WorkerIdleRegionPolicy.TryFindLargest(door, walkable.Contains, 1000,
    out Point start, out List<Point> region);
Assert(found && region.Count == 91 && start.Y >= 6,
    "The Farm search must choose the open Farm instead of the tiny porch component.");
Assert(!region.Contains(new Point(5, 3)) && region.Contains(new Point(12, 12)),
    "The chosen region must contain distant Farm tiles and exclude the isolated porch.");

found = WorkerIdleRegionPolicy.TryFindLargest(door, tile => tile == new Point(5, 3), 1000,
    out _, out region);
Assert(!found && region.Count == 0,
    "A door-adjacent porch tile alone must not count as a Farm wandering area.");

Console.WriteLine("Idle region checks passed: open Farm selected over isolated porch.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}
