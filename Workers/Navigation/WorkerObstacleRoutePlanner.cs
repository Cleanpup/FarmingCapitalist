using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FarmingCapitalist.Workers;

internal enum WorkerRouteTileKind
{
    Open,
    SmallDebris,
    LargeObstacle,
    Impassable,
}

internal enum WorkerObstaclePlanStatus
{
    Searching,
    ClearSmallDebris,
    ReachableClearRoute,
    BlockedByLargeObstacle,
    NoRoute,
    SearchLimitReached,
}

internal sealed record WorkerObstacleClearance(Point ObstacleTile, Point ApproachTile);

/// <summary>Finds a useful small blocker without changing the world. Searches are advanced in bounded updates.</summary>
internal sealed class WorkerObstacleRoutePlanner
{
    private static readonly Point[] Directions = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };
    private readonly Point start;
    private readonly HashSet<Point> targets;
    private readonly Func<Point, WorkerRouteTileKind> classify;
    private readonly Dictionary<Point, WorkerRouteTileKind> tileKinds = new();
    private readonly Dictionary<Point, int> distances = new();
    private readonly Dictionary<Point, Point> parents = new();
    private readonly HashSet<Point> visited = new();
    private PriorityQueue<Point, int> frontier = new();
    private bool allowLarge;

    public Point? ReachedTarget { get; private set; }

    public WorkerObstacleRoutePlanner(Point start, IEnumerable<Point> targets, Func<Point, WorkerRouteTileKind> classify)
    {
        this.start = start;
        this.targets = new HashSet<Point>(targets);
        this.classify = classify;
        this.StartPhase(allowLarge: false);
    }

    public WorkerObstaclePlanStatus Advance(int maxExpandedNodes, int maxTotalNodes, out WorkerObstacleClearance? clearance)
    {
        clearance = null;
        if (this.targets.Count == 0)
            return WorkerObstaclePlanStatus.NoRoute;

        int expanded = 0;
        while (expanded < maxExpandedNodes)
        {
            if (!this.frontier.TryDequeue(out Point point, out int cost))
            {
                if (this.allowLarge)
                    return WorkerObstaclePlanStatus.NoRoute;
                this.StartPhase(allowLarge: true);
                continue;
            }

            if (!this.distances.TryGetValue(point, out int best) || cost != best || !this.visited.Add(point))
                continue;

            expanded++;
            if (this.visited.Count > maxTotalNodes)
                return WorkerObstaclePlanStatus.SearchLimitReached;

            if (this.targets.Contains(point))
            {
                this.ReachedTarget = point;
                if (this.allowLarge)
                    return WorkerObstaclePlanStatus.BlockedByLargeObstacle;

                clearance = this.FindFirstSmallDebris(point);
                return clearance is null ? WorkerObstaclePlanStatus.ReachableClearRoute : WorkerObstaclePlanStatus.ClearSmallDebris;
            }

            foreach (Point direction in Directions)
            {
                Point next = new(point.X + direction.X, point.Y + direction.Y);
                WorkerRouteTileKind kind = this.GetKind(next);
                if (kind == WorkerRouteTileKind.Impassable || kind == WorkerRouteTileKind.LargeObstacle && !this.allowLarge)
                    continue;

                int nextCost = cost + (kind == WorkerRouteTileKind.Open ? 1 : kind == WorkerRouteTileKind.SmallDebris ? 16 : 32);
                if (this.distances.TryGetValue(next, out int previous) && previous <= nextCost)
                    continue;

                this.distances[next] = nextCost;
                this.parents[next] = point;
                this.frontier.Enqueue(next, nextCost);
            }
        }

        return WorkerObstaclePlanStatus.Searching;
    }

    private WorkerRouteTileKind GetKind(Point point)
    {
        if (!this.tileKinds.TryGetValue(point, out WorkerRouteTileKind kind))
            this.tileKinds[point] = kind = this.classify(point);
        return kind;
    }

    private void StartPhase(bool allowLarge)
    {
        this.allowLarge = allowLarge;
        this.ReachedTarget = null;
        this.frontier = new PriorityQueue<Point, int>();
        this.distances.Clear();
        this.parents.Clear();
        this.visited.Clear();
        this.distances[this.start] = 0;
        this.frontier.Enqueue(this.start, 0);
    }

    private WorkerObstacleClearance? FindFirstSmallDebris(Point goal)
    {
        List<Point> path = new() { goal };
        while (this.parents.TryGetValue(path[^1], out Point previous))
            path.Add(previous);
        path.Reverse();

        for (int i = 1; i < path.Count; i++)
        {
            if (this.GetKind(path[i]) == WorkerRouteTileKind.SmallDebris)
                return new WorkerObstacleClearance(path[i], path[i - 1]);
        }

        return null;
    }
}
