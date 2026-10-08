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

    public WorkerSkillExperience Experience { get; set; } = new();

    /// <summary>Chest used for this worker's gathered items; null sends them to the shipping bin.</summary>
    public WorkerHarvestDestination? HarvestDestination { get; set; }

    public int DailyWage { get; set; } = WorkerEmploymentTerms.DailyWage;

    /// <summary>The absolute game date covered by the last payment; -1 means no payment yet.</summary>
    public int LastPaidDay { get; set; } = -1;

    /// <summary>Prevents duplicate day-start events from retrying an unaffordable payment.</summary>
    public int LastWageAttemptDay { get; set; } = -1;

    public WorkerObstacleReport? PendingObstacleReport { get; set; }

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
            Experience = this.Experience?.Clone() ?? new WorkerSkillExperience(),
            HarvestDestination = this.HarvestDestination?.Clone(),
            DailyWage = this.DailyWage,
            LastPaidDay = this.LastPaidDay,
            LastWageAttemptDay = this.LastWageAttemptDay,
            PendingObstacleReport = this.PendingObstacleReport?.Clone(),
        };
    }
}
