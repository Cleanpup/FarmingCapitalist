namespace HireSkilledHelpers.Workers;

internal static class WorkerRosterMigration
{
    /// <summary>Copies the former roster-wide chest choice to each worker when reading a legacy roster.</summary>
    // Both the older shared-storage roster and combat 1.3.1 use schema 3.
    // Schema 4 unifies the branches; keep this boundary stable across future upgrades.
    private const int PerWorkerDestinationSchema = 4;

    public static void ApplySharedHarvestDestination(WorkerRosterSaveData roster)
    {
        if (roster.SchemaVersion >= PerWorkerDestinationSchema || roster.HarvestDestination is null)
            return;

        foreach (WorkerRosterEntry worker in roster.Workers ?? new())
            worker.HarvestDestination ??= roster.HarvestDestination.Clone();
    }
}
