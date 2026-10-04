namespace FarmingCapitalist.Workers;

internal sealed class WorkerRosterEntry
{
    public WorkerRosterEntry()
    {
    }

    public string WorkerId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int SpawnTileX { get; set; }

    public int SpawnTileY { get; set; }

    public WorkerAppearanceData Appearance { get; set; } = WorkerAppearanceData.CreateDefault();

    public WorkerProfession Profession { get; set; } = WorkerProfession.Farmer;

    public string ForageLocationName { get; set; } = WorkerForageAreaCatalog.DefaultLocationName;

    public WorkerTaskKind AssignedTask { get; set; } = WorkerTaskKind.Idle;

    public int DailyWage { get; set; } = WorkerEmploymentTerms.DailyWage;

    /// <summary>The absolute game date covered by the last payment; -1 means no payment yet.</summary>
    public int LastPaidDay { get; set; } = -1;

    /// <summary>Prevents duplicate day-start events from retrying an unaffordable payment.</summary>
    public int LastWageAttemptDay { get; set; } = -1;

    public WorkerRosterEntry Clone()
    {
        return new WorkerRosterEntry
        {
            WorkerId = this.WorkerId,
            DisplayName = this.DisplayName,
            SpawnTileX = this.SpawnTileX,
            SpawnTileY = this.SpawnTileY,
            Appearance = this.Appearance?.Clone() ?? WorkerAppearanceData.CreateDefault(),
            Profession = this.Profession,
            ForageLocationName = this.ForageLocationName,
            AssignedTask = this.AssignedTask,
            DailyWage = this.DailyWage,
            LastPaidDay = this.LastPaidDay,
            LastWageAttemptDay = this.LastWageAttemptDay,
        };
    }
}
