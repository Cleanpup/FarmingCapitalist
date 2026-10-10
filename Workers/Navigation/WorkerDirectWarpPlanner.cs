using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace HireSkilledHelpers.Workers;

internal sealed record WorkerWarpOption<TLeg>(Point WarpTile, Func<(TLeg Leg, int Steps)?> BuildLeg) where TLeg : class;

internal static class WorkerDirectWarpPlanner
{
    public static TLeg? ChooseNearest<TLeg>(Point start, IReadOnlyList<WorkerWarpOption<TLeg>> options)
        where TLeg : class
    {
        TLeg? best = null;
        int bestSteps = int.MaxValue;
        foreach (WorkerWarpOption<TLeg> option in options.OrderBy(option => LowerBound(start, option.WarpTile)))
        {
            if (best is not null && LowerBound(start, option.WarpTile) > bestSteps)
                break;

            (TLeg Leg, int Steps)? candidate = option.BuildLeg();
            if (candidate is null || candidate.Value.Steps >= bestSteps)
                continue;

            best = candidate.Value.Leg;
            bestSteps = candidate.Value.Steps;
        }

        return best;
    }

    public static bool TryChooseLanding(Point requested, int maxRadius, Func<Point, bool> isSafe, out Point landing)
    {
        for (int distance = 0; distance <= 2 * maxRadius; distance++)
        {
            for (int dx = -maxRadius; dx <= maxRadius; dx++)
            {
                for (int dy = -maxRadius; dy <= maxRadius; dy++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) != distance)
                        continue;

                    Point candidate = new(requested.X + dx, requested.Y + dy);
                    if (isSafe(candidate))
                    {
                        landing = candidate;
                        return true;
                    }
                }
            }
        }

        landing = Point.Zero;
        return false;
    }

    private static int LowerBound(Point start, Point warp)
        => Math.Max(0, Math.Abs(start.X - warp.X) - 1)
         + Math.Max(0, Math.Abs(start.Y - warp.Y) - 1);
}
