using HireSkilledHelpers.Workers;
using Microsoft.Xna.Framework;

string[] candidates = Enumerable.Range(0, 96).Select(index => index.ToString()).ToArray();
int cursor = 0;
int totalAttempts = 0;
string? selected = null;
for (int batch = 0; batch < 24 && selected is null; batch++)
{
    bool found = WorkerBoundedCandidateSearch.TryFind(candidates, cursor, 4, 1000,
        candidate => { totalAttempts++; return candidate == "40"; },
        out selected, out cursor, out int attempted, out bool exhausted, out _);
    Assert(attempted <= 4, "A route search update must test at most four approaches.");
    Assert(found == (selected is not null), "The search result must match its selected target.");
    Assert(!exhausted, "A reachable later candidate must be tried before declaring the sweep exhausted.");
}
Assert(selected == "40" && totalAttempts == 41, "A later reachable target must be found across bounded updates.");

string[] blocked = Enumerable.Range(0, 9).Select(index => index.ToString()).ToArray();
int blockedCursor = 0;
int blockedAttempts = 0;
for (int batch = 0; batch < 3; batch++)
{
    bool found = WorkerBoundedCandidateSearch.TryFind(blocked, blockedCursor, 4, 1000,
        _ => { blockedAttempts++; return false; },
        out _, out blockedCursor, out int attempted, out bool exhausted, out _);
    Assert(!found && attempted <= 4, "Blocked approaches must remain bounded per update.");
    Assert(exhausted == (batch == 2), "The search must signal return home only after every approach was attempted.");
}
Assert(blockedAttempts == blocked.Length && blockedCursor == 0,
    "An exhausted search must not silently restart the same blocked approaches.");

bool limited = WorkerBoundedCandidateSearch.TryFind(candidates, 7, 4, 0, _ => false,
    out _, out int nextIndex, out int limitedAttempts, out bool budgetExhausted, out _);
Assert(!limited && limitedAttempts == 1 && nextIndex == 8,
    "A spent time budget must still try one candidate and then resume at the next.");
Assert(!budgetExhausted, "A spent time budget must not be mistaken for exhausted work.");

var eligibleTargets = new[]
{
    new ForagerTargetCheck(new Point(4, 43), new Point(6, 43)),
    new ForagerTargetCheck(new Point(39, 20), new Point(38, 20)),
};
Assert(WorkerForagerApproachMatcher.TryFind(eligibleTargets, new Point(38, 20), target => target.Approach,
        out ForagerTargetCheck? matched)
    && matched?.Resource == new Point(39, 20),
    "A reachable fallback approach must be matched to its eligible hardwood target.");
Assert(!WorkerForagerApproachMatcher.TryFind(eligibleTargets, new Point(7, 47), target => target.Approach, out _),
    "A fallback endpoint that belongs only to a blocked or stale approach must not resume work.");

Console.WriteLine("Forager search checks passed: reachable alternative, all-blocked exhaustion, and bounded attempts.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

internal sealed record ForagerTargetCheck(Point Resource, Point Approach);
