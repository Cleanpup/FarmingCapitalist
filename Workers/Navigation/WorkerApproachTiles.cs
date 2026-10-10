using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HireSkilledHelpers.Workers;

internal static class WorkerApproachTiles
{
    public static IEnumerable<Point> AroundResource(Point resource, int width, int height, Func<Point, bool> canUse)
    {
        for (int x = resource.X - 1; x <= resource.X + width; x++)
        {
            foreach (int y in new[] { resource.Y - 1, resource.Y + height })
            {
                Point candidate = new(x, y);
                if (canUse(candidate))
                    yield return candidate;
            }
        }
        for (int y = resource.Y; y < resource.Y + height; y++)
        {
            foreach (int x in new[] { resource.X - 1, resource.X + width })
            {
                Point candidate = new(x, y);
                if (canUse(candidate))
                    yield return candidate;
            }
        }
    }
}
