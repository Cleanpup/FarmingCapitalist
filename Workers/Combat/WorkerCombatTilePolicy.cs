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
}
