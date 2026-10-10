using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HireSkilledHelpers.Workers;

internal static class WorkerForagerApproachMatcher
{
    public static bool TryFind<T>(IEnumerable<T> candidates, Point approach, Func<T, Point> getApproach, out T? candidate)
        where T : class
    {
        foreach (T item in candidates)
        {
            if (getApproach(item) != approach)
                continue;

            candidate = item;
            return true;
        }

        candidate = default;
        return false;
    }
}
