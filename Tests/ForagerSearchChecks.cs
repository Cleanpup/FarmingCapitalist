using FarmingCapitalist.Workers;

string[] candidates = Enumerable.Range(0, 96).Select(index => index.ToString()).ToArray();
int cursor = 0;
int totalAttempts = 0;
string? selected = null;
for (int batch = 0; batch < 24 && selected is null; batch++)
{
    bool found = WorkerBoundedCandidateSearch.TryFind(candidates, cursor, 4, 1000,
        candidate => { totalAttempts++; return candidate == "40"; },
        out selected, out cursor, out int attempted, out _);
    Assert(attempted <= 4, "A route search update must test at most four approaches.");
    Assert(found == (selected is not null), "The search result must match its selected target.");
}
Assert(selected == "40" && totalAttempts == 41, "A later reachable target must be found across bounded updates.");

bool limited = WorkerBoundedCandidateSearch.TryFind(candidates, 7, 4, 0, _ => false,
    out _, out int nextIndex, out int limitedAttempts, out _);
Assert(!limited && limitedAttempts == 1 && nextIndex == 8,
    "A spent time budget must still try one candidate and then resume at the next.");

Console.WriteLine("Forager search checks passed: bounded attempts, cursor progress, and time-budget behavior.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}
