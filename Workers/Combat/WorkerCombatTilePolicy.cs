namespace FarmingCapitalist.Workers;

internal static class WorkerCombatTilePolicy
{
    public static bool IsSafeTile(bool onMap, bool hasGround, bool mapPassable, bool navigationWalkable)
        => onMap && hasGround && mapPassable && navigationWalkable;

    // Planned route steps need static map safety. Moving actors and decorative
    // Front/Buildings tiles can change or be traversable after path planning.
    public static bool IsSafeRouteStep(bool onMap, bool hasGround, bool mapPassable,
        bool npcBarrier, bool warp)
        => onMap && hasGround && mapPassable && !npcBarrier && !warp;

    public static bool TryChooseConnectedLanding(int originX, int originY, int maxDistance,
        Func<int, int, bool> canStand, out (int X, int Y) landing)
    {
        (int X, int Y)[] directions = { (0, 1), (1, 0), (-1, 0), (0, -1) };
        Queue<(int X, int Y)> queue = new();
        HashSet<(int X, int Y)> visited = new();
        foreach ((int dx, int dy) in directions)
        {
            (int X, int Y) next = (originX + dx, originY + dy);
            if (canStand(next.X, next.Y) && visited.Add(next))
                queue.Enqueue(next);
        }

        while (queue.Count > 0)
        {
            (int X, int Y) candidate = queue.Dequeue();
            if (directions.Any(direction => canStand(candidate.X + direction.X, candidate.Y + direction.Y)))
            {
                landing = candidate;
                return true;
            }

            foreach ((int dx, int dy) in directions)
            {
                (int X, int Y) next = (candidate.X + dx, candidate.Y + dy);
                if (Math.Abs(next.X - originX) + Math.Abs(next.Y - originY) <= maxDistance
                    && visited.Add(next) && canStand(next.X, next.Y))
                    queue.Enqueue(next);
            }
        }
        landing = default;
        return false;
    }

    /// <summary>Prefer the farmer's walkable component, then a roomy area anywhere on this actual floor.</summary>
    public static bool TryChooseFloorLanding(int width, int height, int originX, int originY,
        Func<int, int, bool> canTraverse, Func<int, int, bool> canStand, out (int X, int Y) landing)
    {
        landing = default;
        if (width <= 0 || height <= 0)
            return false;
        (int X, int Y)[] directions = { (0, 1), (1, 0), (-1, 0), (0, -1) };
        bool[,] traversable = new bool[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            traversable[x, y] = canTraverse(x, y);
        HashSet<(int X, int Y)> visited = new();

        bool OnMap(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
        int Distance((int X, int Y) p) => Math.Abs(p.X - originX) + Math.Abs(p.Y - originY);

        List<(int X, int Y)> Component(int x, int y)
        {
            List<(int X, int Y)> result = new();
            Queue<(int X, int Y)> queue = new();
            if (!OnMap(x, y) || !traversable[x, y] || !visited.Add((x, y)))
                return result;
            queue.Enqueue((x, y));
            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                result.Add(tile);
                foreach (var direction in directions)
                {
                    var next = (X: tile.X + direction.X, Y: tile.Y + direction.Y);
                    if (OnMap(next.X, next.Y) && traversable[next.X, next.Y] && visited.Add(next))
                        queue.Enqueue(next);
                }
            }
            return result;
        }

        bool Choose(List<(int X, int Y)> component, out (int X, int Y) chosen)
        {
            chosen = default;
            if (component.Count < 3)
                return false;
            foreach (var tile in component.OrderBy(Distance))
            {
                if ((tile.X == originX && tile.Y == originY) || !canStand(tile.X, tile.Y))
                    continue;
                if (directions.Any(d => OnMap(tile.X + d.X, tile.Y + d.Y)
                    && traversable[tile.X + d.X, tile.Y + d.Y] && canStand(tile.X + d.X, tile.Y + d.Y)))
                {
                    chosen = tile;
                    return true;
                }
            }
            return false;
        }

        if (Choose(Component(originX, originY), out landing))
            return true;
        int bestSize = 0;
        int bestDistance = int.MaxValue;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var component = Component(x, y);
            if (Choose(component, out var candidate)
                && (component.Count > bestSize || component.Count == bestSize && Distance(candidate) < bestDistance))
            {
                bestSize = component.Count;
                bestDistance = Distance(candidate);
                landing = candidate;
            }
        }
        return bestSize > 0;
    }
}
