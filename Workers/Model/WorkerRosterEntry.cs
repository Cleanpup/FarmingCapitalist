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

    public string CombatArea { get; set; } = WorkerCombatAreaCatalog.Farm;
    public string MiningArea { get; set; } = WorkerMiningPolicy.DefaultArea;
    public string ExplorationArea { get; set; } = WorkerExplorationAreaCatalog.Mines;
    public WorkerExplorationProgress Exploration { get; set; } = new();
    public string FishingArea { get; set; } = WorkerFishingAreaCatalog.Forest;
    public WorkerFishingProgress Fishing { get; set; } = new();

    public WorkerTaskKind AssignedTask { get; set; } = WorkerTaskKind.Idle;

    public WorkerSkillExperience Experience { get; set; } = new();

    public WorkerStaminaState Stamina { get; set; } = new();

    /// <summary>Chest used for this worker's gathered items; null sends them to the shipping bin.</summary>
    public WorkerHarvestDestination? HarvestDestination { get; set; }

    public int DailyWage { get; set; } = WorkerEmploymentTerms.DailyWage;

    /// <summary>The absolute game date covered by the last payment; -1 means no payment yet.</summary>
    public int LastPaidDay { get; set; } = -1;

    /// <summary>Prevents duplicate day-start events from retrying an unaffordable payment.</summary>
    public int LastWageAttemptDay { get; set; } = -1;

    /// <summary>Absolute game day when this worker lost all combat HP.</summary>
    public int LastDefeatedDay { get; set; } = -1;

    /// <summary>Current combat HP is saved so a same-day reload cannot heal a worker.</summary>
    public int CombatHealth { get; set; } = -1;

    public int CombatMaxHealth { get; set; }

    public int LastCombatHealthDay { get; set; } = -1;

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
            CombatArea = this.CombatArea,
            MiningArea = this.MiningArea,
            ExplorationArea = this.ExplorationArea,
            Exploration = this.Exploration?.Clone() ?? new(),
            FishingArea = this.FishingArea,
            Fishing = this.Fishing?.Clone() ?? new(),
            AssignedTask = this.AssignedTask,
            Experience = this.Experience?.Clone() ?? new WorkerSkillExperience(),
            Stamina = this.Stamina?.Clone() ?? new(),
            HarvestDestination = this.HarvestDestination?.Clone(),
            DailyWage = this.DailyWage,
            LastPaidDay = this.LastPaidDay,
            LastWageAttemptDay = this.LastWageAttemptDay,
            LastDefeatedDay = this.LastDefeatedDay,
            CombatHealth = this.CombatHealth,
            CombatMaxHealth = this.CombatMaxHealth,
            LastCombatHealthDay = this.LastCombatHealthDay,
            PendingObstacleReport = this.PendingObstacleReport?.Clone(),
        };
    }
}
