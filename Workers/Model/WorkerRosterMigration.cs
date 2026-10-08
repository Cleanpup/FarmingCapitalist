namespace FarmingCapitalist.Workers;

internal static class WorkerRosterMigration
{
    /// <summary>Copies the former roster-wide chest choice to each worker when reading a legacy roster.</summary>
    public static void ApplySharedHarvestDestination(WorkerRosterSaveData roster)
    {
        if (roster.SchemaVersion >= WorkerEmploymentTerms.RosterSchemaVersion || roster.HarvestDestination is null)
            return;

        foreach (WorkerRosterEntry worker in roster.Workers ?? new())
            worker.HarvestDestination ??= roster.HarvestDestination.Clone();
    }
}
