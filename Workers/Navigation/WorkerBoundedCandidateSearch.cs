using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HireSkilledHelpers.Workers;

internal static class WorkerBoundedCandidateSearch
{
    public static bool TryFind<T>(IReadOnlyList<T> candidates, int startIndex, int maxAttempts, long budgetMilliseconds,
        Func<T, bool> tryCandidate, out T? selected, out int nextIndex, out int attempted,
        out bool exhausted, out long elapsedMilliseconds)
        where T : class
    {
        selected = null;
        nextIndex = 0;
        attempted = 0;
        exhausted = false;
        elapsedMilliseconds = 0;
        if (candidates.Count == 0 || maxAttempts <= 0)
            return false;

        int first = Math.Max(0, startIndex) % candidates.Count;
        Stopwatch timer = Stopwatch.StartNew();
        while (attempted < Math.Min(candidates.Count - first, maxAttempts)
            && (attempted == 0 || timer.ElapsedMilliseconds < budgetMilliseconds))
        {
            T candidate = candidates[(first + attempted) % candidates.Count];
            attempted++;
            if (tryCandidate(candidate))
            {
                selected = candidate;
                break;
            }
        }

        nextIndex = (first + attempted) % candidates.Count;
        exhausted = selected is null && first + attempted >= candidates.Count;
        elapsedMilliseconds = timer.ElapsedMilliseconds;
        return selected is not null;
    }
}
