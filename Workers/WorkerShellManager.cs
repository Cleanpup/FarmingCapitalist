using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerShellManager
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly string legacyAppearanceSaveDataKey;
    private readonly string rosterSaveDataKey;
    private readonly string workerIdDataKey;
    private readonly string rosterMirrorDataKey;
    private readonly WorkerSpriteSheetBuilder spriteSheetBuilder;
    private readonly List<WorkerRosterEntry> savedWorkers = new();
    private readonly Dictionary<string, Texture2D> generatedSpriteSheets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NPC> runtimeWorkers = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<(NPC Worker, GameLocation Location)> detachedWorkers = new();
    private readonly Dictionary<string, NPC> clientAppearanceWorkers = new(StringComparer.OrdinalIgnoreCase);
    private string? lastMirrorPayload;
    private int nextWorkerNumber = 1;

    public WorkerShellManager(IModHelper helper, IManifest manifest, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.legacyAppearanceSaveDataKey = $"{manifest.UniqueID}.TestWorkerAppearance";
        this.rosterSaveDataKey = $"{manifest.UniqueID}.WorkerRoster";
        this.workerIdDataKey = $"{manifest.UniqueID}/WorkerId";
        this.rosterMirrorDataKey = $"{manifest.UniqueID}/WorkerRoster";
        this.spriteSheetBuilder = new WorkerSpriteSheetBuilder(monitor);
    }

    public Vector2 GetExpectedSpawnTile()
    {
        return new Vector2(TestWorkerDefinition.SpawnTile.X, TestWorkerDefinition.SpawnTile.Y);
    }

    public WorkerAppearanceData? GetSavedWorkerAppearance()
    {
        return this.savedWorkers.Count > 0
            ? this.savedWorkers[this.savedWorkers.Count - 1].Appearance.Clone()
            : null;
    }

    public bool HasSavedWorkerAppearance()
    {
        return this.savedWorkers.Count > 0;
    }

    public int GetConfiguredWorkerCount()
    {
        return this.savedWorkers.Count;
    }

    public WorkerHarvestDestination? GetHarvestDestination(string workerId)
        => this.GetWorkerEntry(workerId)?.HarvestDestination?.Clone();

    public bool TrySetHarvestDestination(string workerId, WorkerHarvestDestination? destination, out string message)
    {
        if (!this.CanManageWorkers(out message))
            return false;

        WorkerRosterEntry? worker = this.GetWorkerEntry(workerId);
        if (worker is null)
        {
            message = "That worker is no longer employed.";
            return false;
        }

        if (destination is not null && !WorkerChestCatalog.TryGetChest(destination, out _))
        {
            message = "That chest is no longer available. Choose another chest or the shipping bin.";
            return false;
        }

        worker.HarvestDestination = destination?.Clone();
        this.PersistRoster();
        WorkerChestOption? selected = destination is null
            ? null
            : WorkerChestCatalog.GetAvailableChests().FirstOrDefault(option => option.LocationName == destination.LocationName && option.Tile == destination.Tile);
        message = selected is null ? $"{worker.DisplayName} will place gathered items in the shipping bin."
            : $"{worker.DisplayName} will place gathered items in the chest at {selected.Label}.";
        return true;
    }

    public void ReloadWorkerAppearance()
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        if (!Context.IsMainPlayer)
        {
            this.RefreshClientRoster();
            return;
        }

        this.savedWorkers.Clear();
        this.savedWorkers.AddRange(this.LoadRosterEntries());
        this.RemoveOrphanedWorkerShells();
        this.RebuildAllGeneratedSpriteSheets();
        this.PersistRoster();
    }

    /// <summary>Compatibility alias for the old spawn command; all hires use the same paid contract.</summary>
    public NPC? SpawnConfiguredWorker(WorkerAppearanceData appearance)
    {
        this.TryHireWorker(appearance, out NPC? worker, out string message);
        this.monitor.Log(message, worker is null ? LogLevel.Warn : LogLevel.Info);
        return worker;
    }

    public bool TryHireWorker(WorkerAppearanceData appearance, out NPC? worker, out string message)
        => this.TryHireWorker(appearance, WorkerProfession.Farmer, out worker, out message);

    public bool TryHireWorker(WorkerAppearanceData appearance, WorkerProfession profession, out NPC? worker, out string message)
    {
        worker = null;
        if (!this.CanManageWorkers(out message))
        {
            return false;
        }

        if (this.savedWorkers.Count >= WorkerEmploymentTerms.MaximumWorkers)
        {
            message = $"You can hire up to {WorkerEmploymentTerms.MaximumWorkers} workers.";
            return false;
        }

        if (Game1.player.Money < WorkerEmploymentTerms.HiringCost)
        {
            message = $"Hiring costs {WorkerEmploymentTerms.HiringCost}g, including today's wages.";
            return false;
        }

        GameLocation? targetLocation = Game1.getLocationFromName(TestWorkerDefinition.LocationName);
        if (targetLocation is null || !this.TryFindNextAvailableSpawnTile(targetLocation, null, out Point spawnTile))
        {
            message = "There is no clear space for a worker in the farmhouse. Clear some floor space and try again.";
            return false;
        }

        int workerNumber = this.nextWorkerNumber++;
        WorkerRosterEntry entry = new()
        {
            WorkerId = workerNumber == 1 ? TestWorkerDefinition.WorkerId : $"worker-{workerNumber}",
            DisplayName = $"Worker {workerNumber}",
            SpawnTileX = spawnTile.X,
            SpawnTileY = spawnTile.Y,
            Appearance = appearance.Clone(),
            Profession = Enum.IsDefined(typeof(WorkerProfession), profession) ? profession : WorkerProfession.Farmer,
            ForageLocationName = WorkerForageAreaCatalog.DefaultLocationName,
            CombatArea = WorkerCombatAreaCatalog.Farm,
            LastPaidDay = Game1.Date.TotalDays,
            LastWageAttemptDay = Game1.Date.TotalDays,
        };

        this.savedWorkers.Add(entry);
        try
        {
            worker = this.EnsureWorkerPresent(entry, respawnAtSpawn: true);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Could not create {entry.DisplayName}: {ex}", LogLevel.Error);
        }

        if (worker is null)
        {
            this.savedWorkers.Remove(entry);
            this.RemoveWorkerShells(entry.WorkerId);
            this.DisposeGeneratedSpriteSheet(entry.WorkerId);
            message = "The worker could not arrive. No gold was charged; see the SMAPI log for details.";
            return false;
        }

        Game1.player.Money -= WorkerEmploymentTerms.HiringCost;
        this.PersistRoster();
        message = $"Hired {entry.DisplayName} for {WorkerEmploymentTerms.HiringCost}g. Today's wages are included; assign a task in Workers.";
        return true;
    }

    public bool TryDismissWorker(string workerId, out string message)
    {
        if (!this.CanManageWorkers(out message))
        {
            return false;
        }

        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is null)
        {
            message = "That worker is no longer employed.";
            return false;
        }
        if (!this.TryStorePendingExploration(entry, out message))
            return false;

        this.RemoveWorkerShells(workerId);
        this.detachedWorkers.RemoveAll(saved => this.IsManagedWorker(saved.Worker, workerId));
        this.savedWorkers.Remove(entry);
        this.DisposeGeneratedSpriteSheet(workerId);
        this.PersistRoster();
        message = $"Dismissed {entry.DisplayName}. Paid hiring fees and wages are not refunded.";
        return true;
    }

    public bool DeleteConfiguredWorker()
    {
        if (!this.CanManageWorkers(out _))
        {
            return false;
        }

        foreach (WorkerRosterEntry entry in this.savedWorkers)
            if (!this.TryStorePendingExploration(entry, out string message))
            {
                this.monitor.Log(message, LogLevel.Warn);
                return false;
            }
        bool removedWorkers = this.RemoveAllWorkerShells();
        bool hadRosterEntries = this.savedWorkers.Count > 0;
        this.savedWorkers.Clear();
        this.detachedWorkers.Clear();
        this.runtimeWorkers.Clear();
        this.PersistRoster();
        this.helper.Data.WriteSaveData<WorkerAppearanceData>(this.legacyAppearanceSaveDataKey, null);
        this.DisposeAllGeneratedSpriteSheets();
        return removedWorkers || hadRosterEntries;
    }

    private bool TryStorePendingExploration(WorkerRosterEntry entry, out string message)
    {
        message = string.Empty;
        bool delivered = WorkerExplorationLootStorage.TryDeliver(entry.Exploration, this.GetHarvestDestination(entry.WorkerId),
            this.monitor, this.PersistRoster, out string error);
        if (!delivered)
            message = $"{entry.DisplayName} still has saved exploration loot; dismissal deferred: {error}";
        return delivered;
    }

    public IReadOnlyList<WorkerRosterEntry> GetRosterEntries()
    {
        return this.savedWorkers.Select(entry => entry.Clone()).ToArray();
    }

    public WorkerSkillExperience GetWorkerExperience(string workerId)
        => this.GetWorkerEntry(workerId)?.Experience?.Clone() ?? new WorkerSkillExperience();

    /// <summary>Only the host can change persistent skill progress.</summary>
    public bool TryAwardFarmingHarvestExperience(string workerId, bool confirmedCropChange, int itemsCollected)
    {
        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is null || !WorkerExperiencePolicy.TryAwardFarmingHarvest(entry, Context.IsMainPlayer,
                Context.IsWorldReady, confirmedCropChange, itemsCollected))
            return false;

        this.PersistRoster();
        return true;
    }

    public bool TryAwardCompletedActionExperience(string workerId, WorkerExperienceAction action)
    {
        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is null || !WorkerExperiencePolicy.TryAwardCompletedAction(entry, Context.IsMainPlayer,
                Context.IsWorldReady, action))
            return false;

        this.PersistRoster();
        return true;
    }

    public bool TryAwardCombatKillExperience(string workerId, int monsterExperience)
    {
        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is null || !WorkerExperiencePolicy.TryAwardCombatKill(entry, Context.IsMainPlayer,
                Context.IsWorldReady, confirmedWorkerKill: true, monsterExperience))
            return false;
        this.PersistRoster();
        return true;
    }

    public WorkerTaskKind GetAssignedTask(string workerId)
    {
        return this.GetWorkerEntry(workerId)?.AssignedTask ?? WorkerTaskKind.Idle;
    }

    public WorkerProfession GetWorkerProfession(string workerId)
    {
        return this.GetWorkerEntry(workerId)?.Profession ?? WorkerProfession.Farmer;
    }

    public string GetForageLocationName(string workerId)
    {
        return this.GetWorkerEntry(workerId)?.ForageLocationName ?? WorkerForageAreaCatalog.DefaultLocationName;
    }

    public string GetCombatArea(string workerId)
        => this.GetWorkerEntry(workerId)?.CombatArea ?? WorkerCombatAreaCatalog.Farm;

    public string GetExplorationArea(string workerId)
        => this.GetWorkerEntry(workerId)?.ExplorationArea ?? WorkerExplorationAreaCatalog.Mines;

    public WorkerExplorationProgress GetExplorationProgress(string workerId)
        => this.GetWorkerEntry(workerId)?.Exploration?.Clone() ?? new();

    public void RecordExplorationProgress(string workerId, WorkerExplorationProgress progress)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || this.GetWorkerEntry(workerId) is not WorkerRosterEntry entry)
            return;
        entry.Exploration = progress.Clone();
        this.PersistRoster();
    }

    public bool TryCompleteExploration(string workerId, WorkerExplorationProgress progress, List<WorkerExplorationLoot> loot)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || this.GetWorkerEntry(workerId) is not WorkerRosterEntry entry
            || !WorkerExplorationPolicy.TryQueueRun(entry, progress, loot, Game1.Date.TotalDays,
                Context.IsMainPlayer, Context.IsWorldReady, this.CanWorkerWorkToday(workerId), this.WasDefeatedToday(workerId)))
            return false;
        // Persist completion and its undelivered items together before delivery;
        // saving/reloading can't re-roll or credit the same simulated run twice.
        this.PersistRoster();
        return true;
    }

    public void CancelExploration(string workerId)
    {
        WorkerExplorationProgress progress = this.GetExplorationProgress(workerId);
        if (progress.Minutes == 0 && progress.LastObservedMinute == -1)
            return;
        progress.Minutes = 0;
        progress.LastObservedMinute = -1;
        this.RecordExplorationProgress(workerId, progress);
    }

    public bool TrySetExplorationArea(string workerId, string area, out string message)
    {
        if (!this.CanManageWorkers(out message))
            return false;
        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is null || entry.Profession != WorkerProfession.CombatWorker || !WorkerExplorationAreaCatalog.IsValid(area))
        {
            message = "That exploration area is not available for this worker.";
            return false;
        }
        this.CancelExploration(workerId);
        entry.ExplorationArea = area;
        entry.AssignedTask = WorkerTaskKind.Idle;
        this.PersistRoster();
        message = $"{entry.DisplayName} will explore {WorkerExplorationAreaCatalog.GetLabel(area)}. Choose Explore Area to begin.";
        return true;
    }

    public bool WasDefeatedToday(string workerId)
        => Context.IsWorldReady && this.GetWorkerEntry(workerId)?.LastDefeatedDay == Game1.Date.TotalDays;

    public bool TryGetCombatHealthToday(string workerId, out int health, out int maxHealth)
    {
        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (Context.IsWorldReady && entry is not null && entry.LastCombatHealthDay == Game1.Date.TotalDays
            && entry.CombatMaxHealth > 0 && entry.CombatHealth >= 0)
        {
            health = entry.CombatHealth;
            maxHealth = entry.CombatMaxHealth;
            return true;
        }
        health = maxHealth = 0;
        return false;
    }

    public void RecordCombatHealth(string workerId, int health)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || this.GetWorkerEntry(workerId) is not WorkerRosterEntry entry)
            return;
        entry.CombatHealth = WorkerCombatPolicy.ClampHealth(health);
        entry.CombatMaxHealth = WorkerCombatPolicy.MaxHealth;
        entry.LastCombatHealthDay = Game1.Date.TotalDays;
    }

    public void MarkDefeatedToday(string workerId)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || this.GetWorkerEntry(workerId) is not WorkerRosterEntry entry)
            return;
        entry.LastDefeatedDay = Game1.Date.TotalDays;
        this.PersistRoster();
    }

    public bool TrySetCombatArea(string workerId, string area, out string message)
    {
        if (!this.CanManageWorkers(out message))
            return false;
        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is null || entry.Profession != WorkerProfession.CombatWorker || !WorkerCombatAreaCatalog.IsValid(area))
        {
            message = "That combat area is not available for this worker.";
            return false;
        }
        entry.CombatArea = area;
        entry.AssignedTask = WorkerTaskKind.Idle;
        this.PersistRoster();
        message = $"{entry.DisplayName} will work in {WorkerCombatAreaCatalog.GetLabel(area)}. Choose a new order to begin.";
        return true;
    }

    public WorkerObstacleReport? GetPendingObstacleReport(string workerId)
        => this.GetWorkerEntry(workerId)?.PendingObstacleReport?.Clone();

    public void ReportLargeObstacle(string workerId, WorkerTaskKind task, string locationName)
        => this.ReportBlockedRoute(workerId, task, locationName,
            $"I couldn't finish {WorkerTaskPolicy.GetTaskLabel(task).ToLowerInvariant()} in "
            + $"{WorkerForageAreaCatalog.GetDisplayName(locationName)}. Big trees, logs, or boulders blocked my way. Please clear a path.");

    public void ReportBlockedRoute(string workerId, WorkerTaskKind task, string locationName)
        => this.ReportBlockedRoute(workerId, task, locationName,
            $"I couldn't finish {(task == WorkerTaskKind.Idle ? "returning home" : WorkerTaskPolicy.GetTaskLabel(task).ToLowerInvariant())} in "
            + $"{WorkerForageAreaCatalog.GetDisplayName(locationName)}. I couldn't find a clear way home. Please clear a path.");

    private void ReportBlockedRoute(string workerId, WorkerTaskKind task, string locationName, string message)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || this.GetWorkerEntry(workerId) is not WorkerRosterEntry entry)
            return;

        if (entry.PendingObstacleReport is { } previous
            && previous.Task == task
            && string.Equals(previous.LocationName, locationName, StringComparison.OrdinalIgnoreCase))
            return;

        entry.PendingObstacleReport = new WorkerObstacleReport
        {
            Id = Guid.NewGuid().ToString("N"),
            Task = task,
            LocationName = locationName,
            Message = message,
        };
        this.PersistRoster();
    }

    public bool TryAcknowledgeObstacleReport(string workerId, string reportId)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || this.GetWorkerEntry(workerId) is not WorkerRosterEntry entry
            || !WorkerObstacleReportPolicy.MatchesAcknowledgment(entry.PendingObstacleReport, reportId))
            return false;

        entry.PendingObstacleReport = null;
        this.PersistRoster();
        return true;
    }

    public bool TrySetForageLocation(string workerId, string locationName, out string message)
    {
        if (!this.CanManageWorkers(out message))
            return false;

        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is null)
        {
            message = "That worker is no longer employed.";
            return false;
        }

        if (entry.Profession != WorkerProfession.Forager)
        {
            message = "Only foragers have a forage area.";
            return false;
        }

        if (!WorkerForageAreaCatalog.IsValidLocation(locationName))
        {
            message = "That area isn't available for foraging.";
            return false;
        }

        entry.ForageLocationName = locationName;
        entry.AssignedTask = WorkerTaskKind.Idle;
        this.PersistRoster();
        message = $"{entry.DisplayName} will now work in {WorkerForageAreaCatalog.GetDisplayName(locationName)}. Choose a new order to begin.";
        return true;
    }

    public bool TrySetAssignedTask(string workerId, WorkerTaskKind task)
    {
        if (!this.CanManageWorkers(out _) || !Enum.IsDefined(typeof(WorkerTaskKind), task))
        {
            return false;
        }

        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is null)
        {
            return false;
        }

        if (!WorkerTaskPolicy.IsTaskAllowed(entry.Profession, task))
            return false;

        entry.AssignedTask = task;
        this.PersistRoster();
        return true;
    }

    public bool CanWorkerWorkToday(string workerId)
    {
        return Context.IsWorldReady && this.GetWorkerEntry(workerId)?.LastPaidDay == Game1.Date.TotalDays;
    }

    /// <summary>Charge each employee at most once per day, including repeated DayStarted callbacks.</summary>
    public void ProcessDailyWages(bool retryUnpaid = false)
    {
        if (!this.CanManageWorkers(out _))
        {
            return;
        }

        int today = Game1.Date.TotalDays;
        int unpaidCount = 0;
        int totalPaid = 0;
        bool changed = false;
        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            if (!WorkerWagePolicy.ShouldAttemptPayment(entry.LastPaidDay, entry.LastWageAttemptDay, today, retryUnpaid))
            {
                continue;
            }

            changed = true;
            entry.LastWageAttemptDay = today;
            if (Game1.player.Money < entry.DailyWage)
            {
                unpaidCount++;
                continue;
            }

            Game1.player.Money -= entry.DailyWage;
            entry.LastPaidDay = today;
            totalPaid += entry.DailyWage;
        }

        if (changed)
        {
            this.PersistRoster();
        }

        if (totalPaid > 0)
        {
            this.monitor.Log($"Paid {totalPaid}g in daily worker wages.", LogLevel.Info);
        }

        if (unpaidCount > 0)
        {
            string message = $"{unpaidCount} worker(s) are unpaid. Open Workers to pay today's wages and resume work.";
            this.monitor.Log(message, LogLevel.Info);
            Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
        }
    }

    public bool TryPayWorkerForToday(string workerId, out string message)
    {
        if (!this.CanManageWorkers(out message))
        {
            return false;
        }

        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is null)
        {
            message = "That worker is no longer employed.";
            return false;
        }

        if (entry.LastPaidDay == Game1.Date.TotalDays)
        {
            message = $"{entry.DisplayName} is already paid for today.";
            return true;
        }

        if (Game1.player.Money < entry.DailyWage)
        {
            message = $"You need {entry.DailyWage}g to pay {entry.DisplayName} for today.";
            return false;
        }

        Game1.player.Money -= entry.DailyWage;
        entry.LastPaidDay = Game1.Date.TotalDays;
        entry.LastWageAttemptDay = Game1.Date.TotalDays;
        this.PersistRoster();
        message = $"Paid {entry.DisplayName} {entry.DailyWage}g for today.";
        return true;
    }

    public void SaveRoster()
    {
        this.PersistRoster();
    }

    /// <summary>Transient generated sprites must never be written into vanilla NPC save data.</summary>
    public void RemoveWorkersForSaving()
    {
        if (!Context.IsMainPlayer || this.detachedWorkers.Count > 0)
        {
            return;
        }

        foreach (WorkerRosterEntry entry in this.savedWorkers)
            this.FindWorkerById(entry.WorkerId);

        Utility.ForEachLocation(location =>
        {
            for (int i = location.characters.Count - 1; i >= 0; i--)
            {
                NPC worker = location.characters[i];
                if (this.IsManagedWorker(worker))
                {
                    this.detachedWorkers.Add((worker, location));
                    location.characters.RemoveAt(i);
                }
            }

            return true;
        }, includeGenerated: true);
    }

    public void RestoreWorkersAfterSaving()
    {
        if (!Context.IsMainPlayer)
        {
            return;
        }

        foreach ((NPC worker, GameLocation location) in this.detachedWorkers)
        {
            if (this.TryGetWorkerId(worker, out string workerId) && this.GetWorkerEntry(workerId) is not null
                && this.FindWorkerById(workerId) is null)
            {
                if (location is MineShaft mine && !MineShaft.activeMines.Contains(mine))
                    this.TryRecoverInactiveMineWorker(worker);
                else
                {
                    worker.currentLocation = location;
                    location.addCharacter(worker);
                }
            }
        }

        this.detachedWorkers.Clear();
    }

    public WorkerRosterSaveData GetRosterSnapshot()
    {
        return new WorkerRosterSaveData
        {
            SchemaVersion = WorkerEmploymentTerms.RosterSchemaVersion,
            NextWorkerNumber = this.nextWorkerNumber,
            HarvestDestination = null,
            Workers = this.savedWorkers.Select(entry => entry.Clone()).ToList(),
        };
    }

    public void ApplyRosterSnapshot(WorkerRosterSaveData snapshot)
    {
        if (!Context.IsWorldReady || Context.IsMainPlayer)
        {
            return;
        }

        this.savedWorkers.Clear();
        this.savedWorkers.AddRange(this.NormalizeRosterEntries(snapshot.Workers ?? new List<WorkerRosterEntry>(), migrateEmployment: false));
        WorkerRosterMigration.ApplySharedHarvestDestination(new WorkerRosterSaveData
        {
            SchemaVersion = snapshot.SchemaVersion,
            HarvestDestination = snapshot.HarvestDestination?.Clone(),
            Workers = this.savedWorkers,
        });
        this.clientAppearanceWorkers.Clear();
        this.RebuildAllGeneratedSpriteSheets();
        this.RefreshClientAppearances();
    }

    /// <summary>Farm modData is replicated by vanilla; farmhands only rebuild local display textures.</summary>
    public void RefreshClientRoster()
    {
        if (!Context.IsWorldReady || Context.IsMainPlayer)
        {
            return;
        }

        Game1.getFarm().modData.TryGetValue(this.rosterMirrorDataKey, out string? payload);
        if (payload != this.lastMirrorPayload)
        {
            this.lastMirrorPayload = payload;
            try
            {
                WorkerRosterSaveData? snapshot = string.IsNullOrWhiteSpace(payload)
                    ? new WorkerRosterSaveData()
                    : JsonSerializer.Deserialize<WorkerRosterSaveData>(payload);
                if (snapshot is not null)
                {
                    this.ApplyRosterSnapshot(snapshot);
                }
            }
            catch (JsonException ex)
            {
                this.monitor.Log($"Could not read the host's worker roster: {ex.Message}", LogLevel.Warn);
            }
        }

        this.RefreshClientAppearances();
    }

    public NPC? EnsureConfiguredWorkerPresent(bool respawnAtSpawn = true)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || this.savedWorkers.Count == 0)
        {
            return null;
        }

        NPC? primaryWorker = null;
        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            NPC? worker = this.EnsureWorkerPresent(entry, respawnAtSpawn);
            if (primaryWorker is null && worker is not null)
            {
                primaryWorker = worker;
            }
        }

        return primaryWorker;
    }

    public bool TryGetTestWorker(out NPC? worker)
    {
        worker = null;

        if (!Context.IsWorldReady)
        {
            return false;
        }

        string primaryWorkerId = this.savedWorkers.Count > 0
            ? this.savedWorkers[0].WorkerId
            : TestWorkerDefinition.WorkerId;
        worker = this.FindWorkerById(primaryWorkerId);
        return worker is not null;
    }

    public bool TryGetWorker(string workerId, out NPC? worker)
    {
        worker = null;

        if (!Context.IsWorldReady)
        {
            return false;
        }

        worker = this.FindWorkerById(workerId);
        if (worker is null && Context.IsMainPlayer && this.GetWorkerEntry(workerId) is WorkerRosterEntry entry)
            worker = this.EnsureWorkerPresent(entry, respawnAtSpawn: false);
        return worker is not null;
    }

    public IReadOnlyList<NPC> GetSpawnedWorkers(bool recoverStale = true)
    {
        if (!Context.IsWorldReady || this.savedWorkers.Count == 0)
        {
            return Array.Empty<NPC>();
        }

        List<NPC> workers = new(this.savedWorkers.Count);
        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            NPC? worker = this.FindWorkerById(entry.WorkerId, recoverStale);
            if (worker is not null)
            {
                workers.Add(worker);
            }
        }

        return workers;
    }

    public bool TryGetWorkerId(NPC worker, out string workerId)
    {
        workerId = string.Empty;

        if (worker.modData.TryGetValue(this.workerIdDataKey, out string? storedWorkerId)
            && !string.IsNullOrWhiteSpace(storedWorkerId))
        {
            workerId = storedWorkerId;
            return true;
        }

        if (worker.Name == TestWorkerDefinition.InternalName)
        {
            workerId = TestWorkerDefinition.WorkerId;
            return true;
        }

        return false;
    }

    public bool TryGetWorkerReturnTarget(NPC worker, out WorkerNavigationTarget target)
    {
        WorkerNavigationTarget? firstTarget = this.GetWorkerReturnTargets(worker).FirstOrDefault();
        if (firstTarget is null)
        {
            target = new WorkerNavigationTarget(TestWorkerDefinition.LocationName, TestWorkerDefinition.SpawnTile, TestWorkerDefinition.FacingDirection);
            return false;
        }

        target = firstTarget;
        return true;
    }

    /// <summary>Get safe, ordered farmhouse targets without changing the saved roster.</summary>
    public IReadOnlyList<WorkerNavigationTarget> GetWorkerReturnTargets(NPC worker, IEnumerable<Point>? reservedIdleTargets = null)
    {
        if (!this.TryGetWorkerId(worker, out string workerId))
        {
            return Array.Empty<WorkerNavigationTarget>();
        }

        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        GameLocation? farmhouse = Game1.getLocationFromName(TestWorkerDefinition.LocationName);
        if (entry is null || farmhouse is null)
        {
            return Array.Empty<WorkerNavigationTarget>();
        }

        HashSet<Point> reservedTiles = new();
        foreach (WorkerRosterEntry otherEntry in this.savedWorkers)
        {
            if (!string.Equals(otherEntry.WorkerId, workerId, StringComparison.OrdinalIgnoreCase))
            {
                reservedTiles.Add(new Point(otherEntry.SpawnTileX, otherEntry.SpawnTileY));
            }
        }

        if (reservedIdleTargets is not null)
        {
            reservedTiles.UnionWith(reservedIdleTargets);
        }

        HashSet<Point> entranceTiles = this.GetFarmhouseEntranceTiles(farmhouse, worker);
        List<WorkerNavigationTarget> targets = new();
        Point savedTile = new(entry.SpawnTileX, entry.SpawnTileY);
        foreach (Point candidate in this.GetPreferredSpawnCandidates(savedTile, maxRadius: 8))
        {
            if (reservedTiles.Contains(candidate)
                || entranceTiles.Contains(candidate)
                || !this.IsSpawnTileAvailable(farmhouse, candidate.ToVector2(), worker))
            {
                continue;
            }

            targets.Add(new WorkerNavigationTarget(
                TestWorkerDefinition.LocationName,
                candidate,
                TestWorkerDefinition.FacingDirection));
        }

        return targets;
    }

    public IReadOnlyList<WorkerSummarySnapshot> GetWorkerSummaries()
    {
        if (!Context.IsWorldReady || this.savedWorkers.Count == 0)
        {
            return Array.Empty<WorkerSummarySnapshot>();
        }

        List<WorkerSummarySnapshot> summaries = new(this.savedWorkers.Count);
        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            NPC? worker = this.FindWorkerById(entry.WorkerId);
            summaries.Add(new WorkerSummarySnapshot(
                entry.WorkerId,
                entry.DisplayName,
                entry.Profession,
                entry.ForageLocationName,
                entry.CombatArea,
                entry.ExplorationArea,
                IsConfigured: true,
                IsSpawned: worker is not null,
                CurrentLocationName: worker?.currentLocation?.NameOrUniqueName,
                CurrentTile: worker?.TilePoint));
        }

        return summaries;
    }

    public bool TryGetWorkerMenuFace(string workerId, out Texture2D? texture, out Rectangle sourceRect)
    {
        texture = null;
        sourceRect = Rectangle.Empty;

        NPC? worker = this.FindWorkerById(workerId);
        if (worker?.Sprite?.spriteTexture is not null)
        {
            texture = worker.Sprite.spriteTexture;
            sourceRect = new Rectangle(0, 0, 16, 16);
            return true;
        }

        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is not null)
        {
            this.EnsureGeneratedSpriteSheet(entry);
            if (this.generatedSpriteSheets.TryGetValue(workerId, out Texture2D? spriteSheet))
            {
                texture = spriteSheet;
                sourceRect = new Rectangle(0, 0, 16, 16);
                return true;
            }
        }

        texture = Game1.content.Load<Texture2D>(TestWorkerDefinition.ShellSpriteAssetName);
        sourceRect = new Rectangle(0, 0, 16, 16);
        return true;
    }

    public void Reset()
    {
        this.savedWorkers.Clear();
        this.detachedWorkers.Clear();
        this.clientAppearanceWorkers.Clear();
        this.runtimeWorkers.Clear();
        this.lastMirrorPayload = null;
        this.nextWorkerNumber = 1;
        this.DisposeAllGeneratedSpriteSheets();
    }

    private List<WorkerRosterEntry> LoadRosterEntries()
    {
        WorkerRosterSaveData? rosterData = this.helper.Data.ReadSaveData<WorkerRosterSaveData>(this.rosterSaveDataKey);
        // An explicitly empty roster wins over any stale legacy appearance so dismissed workers stay dismissed.
        if (rosterData is not null)
        {
            List<WorkerRosterEntry> entries = this.NormalizeRosterEntries(rosterData.Workers ?? new List<WorkerRosterEntry>(),
                migrateEmployment: rosterData.SchemaVersion <= 0);
            WorkerRosterMigration.ApplySharedHarvestDestination(new WorkerRosterSaveData
            {
                SchemaVersion = rosterData.SchemaVersion,
                HarvestDestination = rosterData.HarvestDestination?.Clone(),
                Workers = entries,
            });
            this.nextWorkerNumber = Math.Max(Math.Max(1, rosterData.NextWorkerNumber), this.GetNextWorkerNumber(entries));
            this.helper.Data.WriteSaveData<WorkerAppearanceData>(this.legacyAppearanceSaveDataKey, null);
            return entries;
        }

        WorkerAppearanceData? legacyAppearance = this.helper.Data.ReadSaveData<WorkerAppearanceData>(this.legacyAppearanceSaveDataKey);
        if (legacyAppearance is null)
        {
            return new List<WorkerRosterEntry>();
        }

        List<WorkerRosterEntry> migratedEntries = new()
        {
            new WorkerRosterEntry
            {
                WorkerId = TestWorkerDefinition.WorkerId,
                DisplayName = TestWorkerDefinition.DisplayName,
                SpawnTileX = TestWorkerDefinition.SpawnTile.X,
                SpawnTileY = TestWorkerDefinition.SpawnTile.Y,
                Appearance = legacyAppearance.Clone(),
                LastPaidDay = Game1.Date.TotalDays,
                LastWageAttemptDay = Game1.Date.TotalDays,
            },
        };

        this.nextWorkerNumber = 2;
        this.helper.Data.WriteSaveData<WorkerAppearanceData>(this.legacyAppearanceSaveDataKey, null);
        this.monitor.Log("Migrated the legacy worker appearance into the roster; today's wages are included.", LogLevel.Info);
        return migratedEntries;
    }

    private List<WorkerRosterEntry> NormalizeRosterEntries(IEnumerable<WorkerRosterEntry> entries, bool migrateEmployment)
    {
        List<WorkerRosterEntry> normalizedEntries = new();
        HashSet<string> seenWorkerIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (WorkerRosterEntry entry in entries)
        {
            if (entry is null)
            {
                continue;
            }

            string workerId = string.IsNullOrWhiteSpace(entry.WorkerId)
                ? $"worker-recovered-{normalizedEntries.Count + 1}"
                : entry.WorkerId.Trim();
            if (!seenWorkerIds.Add(workerId))
            {
                this.monitor.Log($"Ignored duplicate worker roster ID '{workerId}'.", LogLevel.Warn);
                continue;
            }

            WorkerRosterEntry normalized = entry.Clone();
            normalized.Experience = entry.Experience?.Clone() ?? new WorkerSkillExperience();
            normalized.WorkerId = workerId;
            normalized.DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? $"Worker {normalizedEntries.Count + 1}"
                : entry.DisplayName.Trim();
            if (!Enum.IsDefined(typeof(WorkerTaskKind), normalized.AssignedTask))
            {
                normalized.AssignedTask = WorkerTaskKind.Idle;
            }
            if (!Enum.IsDefined(typeof(WorkerProfession), normalized.Profession))
            {
                normalized.Profession = WorkerProfession.Farmer;
            }
            if (!WorkerTaskPolicy.IsTaskAllowed(normalized.Profession, normalized.AssignedTask))
            {
                normalized.AssignedTask = WorkerTaskKind.Idle;
            }
            if (!WorkerForageAreaCatalog.IsValidLocation(normalized.ForageLocationName))
            {
                normalized.ForageLocationName = WorkerForageAreaCatalog.DefaultLocationName;
            }
            if (!WorkerCombatAreaCatalog.IsValid(normalized.CombatArea))
                normalized.CombatArea = WorkerCombatAreaCatalog.Farm;
            if (!WorkerExplorationAreaCatalog.IsValid(normalized.ExplorationArea))
                normalized.ExplorationArea = WorkerExplorationAreaCatalog.Mines;
            normalized.Exploration ??= new();
            WorkerExplorationPolicy.TagLegacyPendingLoot(normalized.Exploration, normalized.ExplorationArea);
            normalized.Exploration.Day = Math.Clamp(normalized.Exploration.Day, -1, Game1.Date.TotalDays);
            normalized.Exploration.Minutes = Math.Clamp(normalized.Exploration.Minutes, 0, 960);
            normalized.Exploration.LastObservedMinute = normalized.Exploration.LastObservedMinute is >= 360 and <= 1320
                ? normalized.Exploration.LastObservedMinute : -1;
            normalized.Exploration.CompletedRuns = Math.Clamp(normalized.Exploration.CompletedRuns, 0, WorkerExplorationPolicy.MaximumRunsPerDay);
            normalized.LastDefeatedDay = Math.Clamp(normalized.LastDefeatedDay, -1, Game1.Date.TotalDays);

            // Employment prices aren't save-editable contracts yet; normalize corrupt/older values.
            normalized.DailyWage = WorkerEmploymentTerms.DailyWage;
            if (migrateEmployment)
            {
                normalized.LastPaidDay = Game1.Date.TotalDays;
                normalized.LastWageAttemptDay = Game1.Date.TotalDays;
            }
            else
            {
                normalized.LastPaidDay = Math.Clamp(normalized.LastPaidDay, -1, Game1.Date.TotalDays);
                normalized.LastWageAttemptDay = Math.Clamp(normalized.LastWageAttemptDay, -1, Game1.Date.TotalDays);
            }

            normalizedEntries.Add(normalized);
        }

        return normalizedEntries;
    }

    private void PersistRoster()
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
        {
            return;
        }

        WorkerRosterSaveData saveData = this.GetRosterSnapshot();
        this.helper.Data.WriteSaveData(this.rosterSaveDataKey, saveData);
        string payload = JsonSerializer.Serialize(saveData);
        Game1.getFarm().modData[this.rosterMirrorDataKey] = payload;
    }

    private bool CanManageWorkers(out string message)
    {
        if (!Context.IsWorldReady)
        {
            message = "Load a save before managing workers.";
            return false;
        }

        if (!Context.IsMainPlayer)
        {
            message = "Only the farm host can hire, pay, dismiss, assign, or choose worker storage.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void RefreshClientAppearances()
    {
        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            NPC? worker = this.FindWorkerById(entry.WorkerId);
            if (worker is null || (this.clientAppearanceWorkers.TryGetValue(entry.WorkerId, out NPC? previous) && previous == worker
                && this.generatedSpriteSheets.TryGetValue(entry.WorkerId, out Texture2D? expected) && worker.Sprite?.spriteTexture == expected))
            {
                continue;
            }

            this.EnsureGeneratedSpriteSheet(entry);
            this.ApplyLocalWorkerTexture(worker, entry.WorkerId);
            this.clientAppearanceWorkers[entry.WorkerId] = worker;
        }
    }

    private NPC? EnsureWorkerPresent(WorkerRosterEntry entry, bool respawnAtSpawn)
    {
        GameLocation? targetLocation = Game1.getLocationFromName(TestWorkerDefinition.LocationName);
        if (targetLocation is null)
        {
            this.monitor.Log($"Could not find worker location '{TestWorkerDefinition.LocationName}'.", LogLevel.Warn);
            return null;
        }

        this.EnsureGeneratedSpriteSheet(entry);

        Point spawnTile = new(entry.SpawnTileX, entry.SpawnTileY);
        NPC? worker = this.FindWorkerById(entry.WorkerId);
        if (worker is null)
        {
            if (!this.IsSpawnTileAvailable(targetLocation, spawnTile.ToVector2(), workerToIgnore: null))
            {
                if (!this.TryFindNextAvailableSpawnTile(targetLocation, entry.WorkerId, out Point alternateSpawnTile))
                {
                    this.monitor.Log($"No clear farmhouse tile is available for {entry.DisplayName}; clear floor space and reopen Workers.", LogLevel.Trace);
                    return null;
                }

                entry.SpawnTileX = alternateSpawnTile.X;
                entry.SpawnTileY = alternateSpawnTile.Y;
                spawnTile = alternateSpawnTile;
                this.PersistRoster();
            }

            worker = this.CreateWorkerShell(entry, targetLocation, spawnTile.ToVector2());
            targetLocation.addCharacter(worker);
            this.runtimeWorkers[entry.WorkerId] = worker;
            return worker;
        }

        if (!respawnAtSpawn)
        {
            return worker;
        }

        bool canUseSpawnTile = this.IsSpawnTileAvailable(
            targetLocation,
            spawnTile.ToVector2(),
            worker.currentLocation == targetLocation ? worker : null);
        if (!canUseSpawnTile)
        {
            if (!this.TryFindNextAvailableSpawnTile(targetLocation, entry.WorkerId, out Point alternateTile))
            {
                return worker;
            }

            spawnTile = alternateTile;
            entry.SpawnTileX = spawnTile.X;
            entry.SpawnTileY = spawnTile.Y;
            this.PersistRoster();
        }

        if (worker.currentLocation != targetLocation)
        {
            worker.currentLocation?.characters.Remove(worker);
            if (!targetLocation.characters.Contains(worker))
            {
                targetLocation.addCharacter(worker);
            }
        }

        this.ApplyWorkerShellState(worker, entry, targetLocation, spawnTile.ToVector2());
        return worker;
    }

    private NPC CreateWorkerShell(WorkerRosterEntry entry, GameLocation location, Vector2 spawnTile)
    {
        Texture2D portrait = Game1.content.Load<Texture2D>(TestWorkerDefinition.ShellPortraitAssetName);
        NPC worker = new(
            this.CreateWorkerSprite(entry.WorkerId),
            spawnTile * Game1.tileSize,
            location.NameOrUniqueName,
            TestWorkerDefinition.FacingDirection,
            this.GetInternalName(entry.WorkerId),
            portrait,
            eventActor: false);

        this.ApplyWorkerShellState(worker, entry, location, spawnTile);
        return worker;
    }

    private void ApplyWorkerShellState(NPC worker, WorkerRosterEntry entry, GameLocation location, Vector2 spawnTile)
    {
        this.ApplyWorkerIdentity(worker, entry);

        worker.currentLocation = location;
        worker.DefaultMap = location.NameOrUniqueName;
        worker.DefaultPosition = spawnTile * Game1.tileSize;
        worker.DefaultFacingDirection = TestWorkerDefinition.FacingDirection;
        worker.followSchedule = false;
        worker.ignoreScheduleToday = true;
        worker.willDestroyObjectsUnderfoot = false;
        worker.IsInvisible = false;
        worker.EventActor = false;
        worker.controller = null;
        worker.temporaryController = null;
        worker.Halt();

        worker.Position = spawnTile * Game1.tileSize;

        worker.FacingDirection = TestWorkerDefinition.FacingDirection;
        worker.Sprite?.faceDirection(TestWorkerDefinition.FacingDirection);
    }

    private void ApplyWorkerIdentity(NPC worker, WorkerRosterEntry entry)
    {
        worker.Name = this.GetInternalName(entry.WorkerId);
        worker.displayName = entry.DisplayName;
        worker.SimpleNonVillagerNPC = true;
        worker.Sprite = this.CreateWorkerSprite(entry.WorkerId);
        worker.Portrait = Game1.content.Load<Texture2D>(TestWorkerDefinition.ShellPortraitAssetName);
        worker.modData[this.workerIdDataKey] = entry.WorkerId;

        if (worker.Sprite is not null)
        {
            worker.Sprite.SpriteHeight = 32;
            worker.Sprite.UpdateSourceRect();
        }
    }

    private NPC? FindWorkerById(string workerId, bool recoverStale = true)
    {
        NPC? found = null;
        Utility.ForEachLocation(location =>
        {
            foreach (NPC npc in location.characters)
            {
                if (this.IsManagedWorker(npc, workerId))
                {
                    found = npc;
                    return false;
                }
            }

            return true;
        }, includeGenerated: true);
        if (found is not null)
        {
            this.runtimeWorkers[workerId] = found;
            return found;
        }

        if (!Context.IsMainPlayer || !this.runtimeWorkers.TryGetValue(workerId, out NPC? cached)
            || this.detachedWorkers.Any(item => item.Worker == cached))
            return null;

        // Generated floors are discarded after the farmer leaves. Keep the same
        // NPC shell and move it back into the live world before it can disappear
        // from menu commands or cause a duplicate shell to be created.
        bool onGeneratedFloor = cached.currentLocation is MineShaft;
        bool floorIsActive = cached.currentLocation is MineShaft currentMine && MineShaft.activeMines.Contains(currentMine);
        bool attachedToLocation = cached.currentLocation?.characters.Contains(cached) == true;
        if (recoverStale && WorkerShellLifecyclePolicy.ShouldRecoverCachedWorker(onGeneratedFloor, floorIsActive, attachedToLocation))
            this.TryRecoverInactiveMineWorker(cached);
        return cached;
    }

    private bool TryRecoverInactiveMineWorker(NPC worker)
    {
        WorkerNavigationTarget? home = this.GetWorkerReturnTargets(worker).FirstOrDefault();
        if (home is null)
            return false;
        worker.controller = null;
        worker.temporaryController = null;
        worker.Halt();
        try
        {
            Game1.warpCharacter(worker, home.LocationName, home.Tile);
            this.monitor.Log($"Recovered {worker.displayName} from an unloaded mine floor at home.", LogLevel.Info);
            return true;
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Could not recover {worker.displayName} from an unloaded mine floor: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private bool IsManagedWorker(NPC npc, string? workerId = null)
    {
        if (npc.modData.TryGetValue(this.workerIdDataKey, out string? storedWorkerId) && !string.IsNullOrWhiteSpace(storedWorkerId))
        {
            return workerId is null || string.Equals(storedWorkerId, workerId, StringComparison.OrdinalIgnoreCase);
        }

        return npc.Name == TestWorkerDefinition.InternalName
            && (workerId is null || string.Equals(workerId, TestWorkerDefinition.WorkerId, StringComparison.OrdinalIgnoreCase));
    }

    private bool RemoveAllWorkerShells()
    {
        return this.RemoveWorkerShells(workerId: null);
    }

    private bool RemoveWorkerShells(string? workerId)
    {
        bool removedAny = false;
        Utility.ForEachLocation(location =>
        {
            for (int i = location.characters.Count - 1; i >= 0; i--)
            {
                NPC worker = location.characters[i];
                if (!this.IsManagedWorker(worker, workerId))
                {
                    continue;
                }

                worker.controller = null;
                worker.temporaryController = null;
                worker.Halt();
                location.characters.RemoveAt(i);
                removedAny = true;
            }

            return true;
        }, includeGenerated: true);
        if (workerId is null)
            this.runtimeWorkers.Clear();
        else
            this.runtimeWorkers.Remove(workerId);
        return removedAny;
    }

    private void RemoveOrphanedWorkerShells()
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        Utility.ForEachLocation(location =>
        {
            for (int i = location.characters.Count - 1; i >= 0; i--)
            {
                NPC worker = location.characters[i];
                if (!this.TryGetWorkerId(worker, out string id))
                {
                    continue;
                }

                if (this.GetWorkerEntry(id) is null || !seen.Add(id))
                {
                    worker.controller = null;
                    worker.temporaryController = null;
                    worker.Halt();
                    location.characters.RemoveAt(i);
                }
            }

            return true;
        }, includeGenerated: true);
    }

    private AnimatedSprite CreateWorkerSprite(string workerId)
    {
        if (!this.generatedSpriteSheets.TryGetValue(workerId, out Texture2D? spriteSheet))
        {
            return new AnimatedSprite(TestWorkerDefinition.ShellSpriteAssetName, 0, 16, 32);
        }

        // Replicate a real asset name; generated texture data is rebuilt locally on each peer.
        AnimatedSprite sprite = new(TestWorkerDefinition.ShellSpriteAssetName, 0, 16, 32)
        {
            SpriteWidth = 16,
            SpriteHeight = 32,
            framesPerAnimation = 4,
            spriteTexture = spriteSheet,
            overrideTextureName = $"{TestWorkerDefinition.InternalName}.{workerId}.GeneratedSheet",
            loadedTexture = $"{TestWorkerDefinition.InternalName}.{workerId}.GeneratedSheet",
            textureUsesFlippedRightForLeft = false,
        };

        sprite.CurrentFrame = 0;
        return sprite;
    }

    private void EnsureGeneratedSpriteSheet(WorkerRosterEntry entry)
    {
        if (!this.generatedSpriteSheets.ContainsKey(entry.WorkerId))
        {
            this.RebuildGeneratedSpriteSheet(entry);
        }
    }

    private void RebuildAllGeneratedSpriteSheets()
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            this.RebuildGeneratedSpriteSheet(entry);
        }

        foreach (string obsoleteId in this.generatedSpriteSheets.Keys.Where(id => this.GetWorkerEntry(id) is null).ToArray())
        {
            this.DisposeGeneratedSpriteSheet(obsoleteId);
        }
    }

    private void RebuildGeneratedSpriteSheet(WorkerRosterEntry entry)
    {
        if (!Context.IsWorldReady)
        {
            this.DisposeGeneratedSpriteSheet(entry.WorkerId);
            return;
        }

        Texture2D newSheet;
        try
        {
            newSheet = this.spriteSheetBuilder.BuildSheet(entry.Appearance,
                includeCombatFrames: entry.Profession == WorkerProfession.CombatWorker);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Failed to rebuild the generated sprite sheet for {entry.DisplayName}: {ex.Message}", LogLevel.Warn);
            return;
        }

        Texture2D? previousSheet = null;
        if (this.generatedSpriteSheets.TryGetValue(entry.WorkerId, out Texture2D? existingSheet))
        {
            previousSheet = existingSheet;
        }

        this.generatedSpriteSheets[entry.WorkerId] = newSheet;

        NPC? existingWorker = this.FindWorkerById(entry.WorkerId);
        if (existingWorker is not null)
        {
            if (Context.IsMainPlayer)
            {
                int currentFrame = existingWorker.Sprite?.CurrentFrame ?? 0;
                existingWorker.Sprite = this.CreateWorkerSprite(entry.WorkerId);
                existingWorker.Sprite.CurrentFrame = currentFrame;
            }
            else
            {
                this.ApplyLocalWorkerTexture(existingWorker, entry.WorkerId);
            }
        }

        previousSheet?.Dispose();
    }

    private void DisposeAllGeneratedSpriteSheets()
    {
        foreach (Texture2D texture in this.generatedSpriteSheets.Values)
        {
            texture.Dispose();
        }

        this.generatedSpriteSheets.Clear();
    }

    private void DisposeGeneratedSpriteSheet(string workerId)
    {
        if (!this.generatedSpriteSheets.Remove(workerId, out Texture2D? texture))
        {
            return;
        }

        // A farmhand may receive the roster removal before the corresponding character removal.
        // Keep that briefly visible NPC on a valid local fallback until vanilla replication removes it.
        NPC? worker = Context.IsWorldReady ? this.FindWorkerById(workerId) : null;
        if (worker?.Sprite is not null && worker.Sprite.spriteTexture == texture)
        {
            worker.Sprite.overrideTextureName = TestWorkerDefinition.ShellSpriteAssetName;
            worker.Sprite.loadedTexture = TestWorkerDefinition.ShellSpriteAssetName;
            worker.Sprite.spriteTexture = Game1.content.Load<Texture2D>(TestWorkerDefinition.ShellSpriteAssetName);
            worker.Sprite.UpdateSourceRect();
        }

        texture.Dispose();
    }

    private WorkerRosterEntry? GetWorkerEntry(string workerId)
    {
        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            if (string.Equals(entry.WorkerId, workerId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private void ApplyLocalWorkerTexture(NPC worker, string workerId)
    {
        if (worker.Sprite is null || !this.generatedSpriteSheets.TryGetValue(workerId, out Texture2D? sheet))
        {
            return;
        }

        // These fields are local-only. Never replace the host-owned NetRef Sprite on a farmhand.
        worker.Sprite.overrideTextureName = $"{TestWorkerDefinition.InternalName}.{workerId}.GeneratedSheet";
        worker.Sprite.loadedTexture = worker.Sprite.overrideTextureName;
        worker.Sprite.spriteTexture = sheet;
        worker.Sprite.textureUsesFlippedRightForLeft = false;
        worker.Sprite.UpdateSourceRect();
    }

    private int GetNextWorkerNumber(IEnumerable<WorkerRosterEntry> entries)
    {
        int next = 1;
        foreach (WorkerRosterEntry entry in entries)
        {
            if (entry.WorkerId == TestWorkerDefinition.WorkerId)
            {
                next = Math.Max(next, 2);
            }
            else if (entry.WorkerId.StartsWith("worker-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(entry.WorkerId.AsSpan("worker-".Length), out int number)
                && number > 0 && number < int.MaxValue)
            {
                next = Math.Max(next, number + 1);
            }
        }

        return Math.Max(next, entries.Count() + 1);
    }

    private string GetInternalName(string workerId)
    {
        return workerId == TestWorkerDefinition.WorkerId
            ? TestWorkerDefinition.InternalName
            : $"{TestWorkerDefinition.InternalName}.{workerId}";
    }

    private bool TryFindNextAvailableSpawnTile(GameLocation location, string? reservedWorkerId, out Point spawnTile)
    {
        HashSet<Point> reservedTiles = new();
        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            if (reservedWorkerId is not null && string.Equals(entry.WorkerId, reservedWorkerId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            reservedTiles.Add(new Point(entry.SpawnTileX, entry.SpawnTileY));
        }

        Point origin = TestWorkerDefinition.SpawnTile;
        foreach (Point candidate in this.GetPreferredSpawnCandidates(origin, maxRadius: 8))
        {
            if (reservedTiles.Contains(candidate))
            {
                continue;
            }

            if (this.IsSpawnTileAvailable(location, candidate.ToVector2(), workerToIgnore: null))
            {
                spawnTile = candidate;
                return true;
            }
        }

        spawnTile = Point.Zero;
        return false;
    }

    private IEnumerable<Point> GetPreferredSpawnCandidates(Point origin, int maxRadius)
    {
        yield return origin;

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            foreach (int yOffset in this.GetPreferredAxisOffsets(radius, preferPositiveFirst: true))
            {
                foreach (int xOffset in this.GetPreferredAxisOffsets(radius, preferPositiveFirst: false))
                {
                    if (Math.Max(Math.Abs(xOffset), Math.Abs(yOffset)) != radius)
                    {
                        continue;
                    }

                    yield return new Point(origin.X + xOffset, origin.Y + yOffset);
                }
            }
        }
    }

    private IEnumerable<int> GetPreferredAxisOffsets(int radius, bool preferPositiveFirst)
    {
        yield return 0;

        for (int offset = 1; offset <= radius; offset++)
        {
            if (preferPositiveFirst)
            {
                yield return offset;
                yield return -offset;
            }
            else
            {
                yield return -offset;
                yield return offset;
            }
        }
    }

    private HashSet<Point> GetFarmhouseEntranceTiles(GameLocation farmhouse, NPC worker)
    {
        HashSet<Point> entranceTiles = new();

        foreach (Warp warp in farmhouse.warps)
        {
            this.AddEntranceArea(entranceTiles, new Point(warp.X, warp.Y));
        }

        try
        {
            Point farmExit = farmhouse.getWarpPointTo("Farm", worker);
            if (farmExit != Point.Zero)
            {
                this.AddEntranceArea(entranceTiles, farmExit);
            }

            if (farmhouse.map is not null && farmhouse.map.Layers.Count > 0)
            {
                xTile.Layers.Layer mapLayer = farmhouse.map.Layers[0];
                for (int x = 0; x < mapLayer.LayerWidth; x++)
                {
                    for (int y = 0; y < mapLayer.LayerHeight; y++)
                    {
                        if (farmhouse.getWarpFromDoor(new Point(x, y), worker) is not null)
                        {
                            this.AddEntranceArea(entranceTiles, new Point(x, y));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Could not inspect the farmhouse exit while selecting a worker idle tile: {ex.Message}", LogLevel.Trace);
        }

        return entranceTiles;
    }

    private void AddEntranceArea(HashSet<Point> entranceTiles, Point entrance)
    {
        entranceTiles.Add(entrance);
        foreach (Point offset in new[]
        {
            new Point(0, -1),
            new Point(0, 1),
            new Point(-1, 0),
            new Point(1, 0),
        })
        {
            entranceTiles.Add(new Point(entrance.X + offset.X, entrance.Y + offset.Y));
        }
    }

    private bool IsSpawnTileAvailable(GameLocation location, Vector2 spawnTile, NPC? workerToIgnore)
    {
        if (!location.isTileOnMap(spawnTile))
        {
            return false;
        }

        if (location.IsTileBlockedBy(spawnTile, CollisionMask.All & ~CollisionMask.Characters,
            CollisionMask.All & ~CollisionMask.Farmers, useFarmerTile: true))
        {
            return false;
        }

        Rectangle tileBounds = new((int)spawnTile.X * Game1.tileSize, (int)spawnTile.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);
        foreach (NPC occupant in location.characters)
        {
            if (occupant != workerToIgnore && !occupant.IsInvisible && occupant.GetBoundingBox().Intersects(tileBounds))
            {
                return false;
            }
        }

        return true;
    }
}
