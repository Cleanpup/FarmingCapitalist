using Microsoft.Xna.Framework;

namespace HireSkilledHelpers.Workers;

/// <summary>Find the main walkable Farm area near the farmhouse without mistaking its porch for the whole Farm.</summary>
internal static class WorkerIdleRegionPolicy
{
    public static bool TryFindLargest(Point door, Func<Point, bool> canStand, int maxTiles,
        out Point start, out List<Point> tiles)
    {
        start = Point.Zero;
        tiles = new List<Point>();
        HashSet<Point> examined = new();
        for (int y = door.Y - 2; y <= door.Y + 10; y++)
        for (int x = door.X - 8; x <= door.X + 8; x++)
        {
            Point candidate = new(x, y);
            if (Math.Abs(candidate.X - door.X) + Math.Abs(candidate.Y - door.Y) < 3
                || examined.Contains(candidate) || !canStand(candidate))
                continue;

            List<Point> region = GetConnected(candidate, canStand, maxTiles);
            examined.UnionWith(region);
            if (region.Count <= tiles.Count)
                continue;
            start = candidate;
            tiles = region;
        }
        return tiles.Count > 0;
    }

    private static List<Point> GetConnected(Point start, Func<Point, bool> canStand, int maxTiles)
    {
        Queue<Point> pending = new();
        HashSet<Point> seen = new() { start };
        List<Point> connected = new();
        pending.Enqueue(start);
        while (pending.Count > 0 && connected.Count < maxTiles)
        {
            Point tile = pending.Dequeue();
            connected.Add(tile);
            foreach (Point neighbor in new[]
            {
                new Point(tile.X, tile.Y - 1), new Point(tile.X + 1, tile.Y),
                new Point(tile.X, tile.Y + 1), new Point(tile.X - 1, tile.Y),
            })
            {
                if (!seen.Add(neighbor) || !canStand(neighbor))
                    continue;
                pending.Enqueue(neighbor);
            }
        }
        return connected;
    }
}
