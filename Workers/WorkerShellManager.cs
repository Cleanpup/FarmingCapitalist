using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerShellManager
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly string legacyAppearanceSaveDataKey;
    private readonly string rosterSaveDataKey;
    private readonly string workerIdDataKey;
    private readonly WorkerSpriteSheetBuilder spriteSheetBuilder;
    private readonly List<WorkerRosterEntry> savedWorkers = new();
    private readonly Dictionary<string, Texture2D> generatedSpriteSheets = new(StringComparer.OrdinalIgnoreCase);

    public WorkerShellManager(IModHelper helper, IManifest manifest, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.legacyAppearanceSaveDataKey = $"{manifest.UniqueID}.TestWorkerAppearance";
        this.rosterSaveDataKey = $"{manifest.UniqueID}.WorkerRoster";
        this.workerIdDataKey = $"{manifest.UniqueID}/WorkerId";
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

    public void ReloadWorkerAppearance()
    {
        this.savedWorkers.Clear();
        this.savedWorkers.AddRange(this.LoadRosterEntries());

        if (Context.IsWorldReady && this.savedWorkers.Count == 0)
        {
            this.RemoveAllWorkerShells();
        }

        this.RebuildAllGeneratedSpriteSheets();
    }

    public NPC? SpawnConfiguredWorker(WorkerAppearanceData appearance)
    {
        if (!Context.IsWorldReady)
        {
            return null;
        }

        GameLocation? targetLocation = Game1.getLocationFromName(TestWorkerDefinition.LocationName);
        if (targetLocation is null)
        {
            this.monitor.Log($"Could not find worker location '{TestWorkerDefinition.LocationName}'.", LogLevel.Warn);
            return null;
        }

        Point spawnTile = this.FindNextAvailableSpawnTile(targetLocation, reservedWorkerId: null);
        WorkerRosterEntry entry = new()
        {
            WorkerId = this.CreateNextWorkerId(),
            DisplayName = this.CreateNextDisplayName(),
            SpawnTileX = spawnTile.X,
            SpawnTileY = spawnTile.Y,
            Appearance = appearance.Clone(),
        };

        this.savedWorkers.Add(entry);
        this.PersistRoster();
        this.RebuildGeneratedSpriteSheet(entry);

        NPC? worker = this.EnsureWorkerPresent(entry, respawnAtSpawn: true);
        if (worker is not null)
        {
            this.monitor.Log(
                $"Spawned {entry.DisplayName} at {targetLocation.NameOrUniqueName} tile {spawnTile}. Total configured workers: {this.savedWorkers.Count}.",
                LogLevel.Info);
        }

        return worker;
    }

    public bool DeleteConfiguredWorker()
    {
        bool removedWorkers = this.RemoveAllWorkerShells();
        bool hadRosterEntries = this.savedWorkers.Count > 0;

        this.savedWorkers.Clear();
        this.PersistRoster();
        this.helper.Data.WriteSaveData<WorkerAppearanceData>(this.legacyAppearanceSaveDataKey, null);
        this.DisposeAllGeneratedSpriteSheets();

        return removedWorkers || hadRosterEntries;
    }

    public NPC? EnsureConfiguredWorkerPresent(bool respawnAtSpawn = true)
    {
        if (!Context.IsWorldReady || this.savedWorkers.Count == 0)
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
        return worker is not null;
    }

    public IReadOnlyList<NPC> GetSpawnedWorkers()
    {
        if (!Context.IsWorldReady || this.savedWorkers.Count == 0)
        {
            return Array.Empty<NPC>();
        }

        List<NPC> workers = new(this.savedWorkers.Count);
        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            NPC? worker = this.FindWorkerById(entry.WorkerId);
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
        target = new WorkerNavigationTarget(TestWorkerDefinition.LocationName, TestWorkerDefinition.SpawnTile, TestWorkerDefinition.FacingDirection);

        if (!this.TryGetWorkerId(worker, out string workerId))
        {
            return false;
        }

        WorkerRosterEntry? entry = this.GetWorkerEntry(workerId);
        if (entry is null)
        {
            return false;
        }

        target = new WorkerNavigationTarget(
            TestWorkerDefinition.LocationName,
            new Point(entry.SpawnTileX, entry.SpawnTileY),
            TestWorkerDefinition.FacingDirection);
        return true;
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
        this.DisposeAllGeneratedSpriteSheets();
    }

    private List<WorkerRosterEntry> LoadRosterEntries()
    {
        WorkerRosterSaveData? rosterData = this.helper.Data.ReadSaveData<WorkerRosterSaveData>(this.rosterSaveDataKey);
        if (rosterData?.Workers is { Count: > 0 })
        {
            return this.NormalizeRosterEntries(rosterData.Workers);
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
            },
        };

        this.helper.Data.WriteSaveData(this.rosterSaveDataKey, new WorkerRosterSaveData { Workers = migratedEntries });
        this.helper.Data.WriteSaveData<WorkerAppearanceData>(this.legacyAppearanceSaveDataKey, null);
        this.monitor.Log("Migrated the legacy single-worker save data into the worker roster.", LogLevel.Info);
        return migratedEntries;
    }

    private List<WorkerRosterEntry> NormalizeRosterEntries(IEnumerable<WorkerRosterEntry> entries)
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
                ? this.CreateFallbackWorkerId(normalizedEntries.Count)
                : entry.WorkerId.Trim();
            if (!seenWorkerIds.Add(workerId))
            {
                continue;
            }

            string displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? this.CreateDisplayNameForIndex(normalizedEntries.Count)
                : entry.DisplayName.Trim();

            normalizedEntries.Add(new WorkerRosterEntry
            {
                WorkerId = workerId,
                DisplayName = displayName,
                SpawnTileX = entry.SpawnTileX,
                SpawnTileY = entry.SpawnTileY,
                Appearance = entry.Appearance?.Clone() ?? WorkerAppearanceData.CreateDefault(),
            });
        }

        return normalizedEntries;
    }

    private void PersistRoster()
    {
        WorkerRosterSaveData saveData = new()
        {
            Workers = new List<WorkerRosterEntry>(this.savedWorkers.Count),
        };

        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            saveData.Workers.Add(new WorkerRosterEntry
            {
                WorkerId = entry.WorkerId,
                DisplayName = entry.DisplayName,
                SpawnTileX = entry.SpawnTileX,
                SpawnTileY = entry.SpawnTileY,
                Appearance = entry.Appearance.Clone(),
            });
        }

        this.helper.Data.WriteSaveData(this.rosterSaveDataKey, saveData);
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
                Point alternateSpawnTile = this.FindNextAvailableSpawnTile(targetLocation, entry.WorkerId);
                entry.SpawnTileX = alternateSpawnTile.X;
                entry.SpawnTileY = alternateSpawnTile.Y;
                spawnTile = alternateSpawnTile;
                this.PersistRoster();
            }

            worker = this.CreateWorkerShell(entry, targetLocation, spawnTile.ToVector2());
            targetLocation.addCharacter(worker);
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
            this.monitor.Log(
                $"{entry.DisplayName} spawn tile {spawnTile} is currently blocked. Leaving the worker at tile {worker.Tile}.",
                LogLevel.Warn);
            return worker;
        }

        if (worker.currentLocation != targetLocation)
        {
            worker.currentLocation?.characters.Remove(worker);
            if (!targetLocation.characters.Contains(worker))
            {
                targetLocation.addCharacter(worker);
            }
        }

        this.ApplyWorkerShellState(worker, entry, targetLocation, spawnTile.ToVector2(), moveToSpawn: true);
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

        this.ApplyWorkerShellState(worker, entry, location, spawnTile, moveToSpawn: true);
        return worker;
    }

    private void ApplyWorkerShellState(NPC worker, WorkerRosterEntry entry, GameLocation location, Vector2 spawnTile, bool moveToSpawn)
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

        if (moveToSpawn)
        {
            worker.Position = spawnTile * Game1.tileSize;
        }

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

    private NPC? FindWorkerById(string workerId)
    {
        foreach (GameLocation location in Game1.locations)
        {
            NPC? worker = this.FindWorkerInLocation(location, workerId);
            if (worker is not null)
            {
                return worker;
            }
        }

        NPC? namedWorker = Game1.getCharacterFromName(this.GetInternalName(workerId), mustBeVillager: false);
        if (namedWorker is not null && this.IsManagedWorker(namedWorker, workerId))
        {
            return namedWorker;
        }

        return null;
    }

    private NPC? FindWorkerInLocation(GameLocation location, string workerId)
    {
        foreach (NPC npc in location.characters)
        {
            if (this.IsManagedWorker(npc, workerId))
            {
                return npc;
            }
        }

        return null;
    }

    private bool IsManagedWorker(NPC npc, string? workerId = null)
    {
        if (npc.modData.TryGetValue(this.workerIdDataKey, out string? storedWorkerId))
        {
            return workerId is null
                ? !string.IsNullOrWhiteSpace(storedWorkerId)
                : string.Equals(storedWorkerId, workerId, StringComparison.OrdinalIgnoreCase);
        }

        return workerId == TestWorkerDefinition.WorkerId && npc.Name == TestWorkerDefinition.InternalName;
    }

    private bool RemoveAllWorkerShells()
    {
        bool removedAny = false;

        foreach (GameLocation location in Game1.locations)
        {
            for (int i = location.characters.Count - 1; i >= 0; i--)
            {
                if (!this.IsManagedWorker(location.characters[i]))
                {
                    continue;
                }

                location.characters.RemoveAt(i);
                removedAny = true;
            }
        }

        return removedAny;
    }

    private AnimatedSprite CreateWorkerSprite(string workerId)
    {
        if (!this.generatedSpriteSheets.TryGetValue(workerId, out Texture2D? spriteSheet))
        {
            return new AnimatedSprite(TestWorkerDefinition.ShellSpriteAssetName, 0, 16, 32);
        }

        AnimatedSprite sprite = new()
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
        this.DisposeAllGeneratedSpriteSheets();

        if (!Context.IsWorldReady)
        {
            return;
        }

        foreach (WorkerRosterEntry entry in this.savedWorkers)
        {
            this.RebuildGeneratedSpriteSheet(entry);
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
            newSheet = this.spriteSheetBuilder.BuildSheet(entry.Appearance);
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
            int currentFrame = existingWorker.Sprite?.CurrentFrame ?? 0;
            existingWorker.Sprite = this.CreateWorkerSprite(entry.WorkerId);
            existingWorker.Sprite.CurrentFrame = currentFrame;
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

    private string CreateNextWorkerId()
    {
        if (this.savedWorkers.Count == 0)
        {
            return TestWorkerDefinition.WorkerId;
        }

        int nextIndex = 2;
        while (this.GetWorkerEntry($"worker-{nextIndex}") is not null)
        {
            nextIndex++;
        }

        return $"worker-{nextIndex}";
    }

    private string CreateNextDisplayName()
    {
        return this.savedWorkers.Count == 0
            ? TestWorkerDefinition.DisplayName
            : $"{TestWorkerDefinition.DisplayName} {this.savedWorkers.Count + 1}";
    }

    private string CreateFallbackWorkerId(int index)
    {
        return index == 0 ? TestWorkerDefinition.WorkerId : $"worker-{index + 1}";
    }

    private string CreateDisplayNameForIndex(int index)
    {
        return index == 0 ? TestWorkerDefinition.DisplayName : $"{TestWorkerDefinition.DisplayName} {index + 1}";
    }

    private string GetInternalName(string workerId)
    {
        return workerId == TestWorkerDefinition.WorkerId
            ? TestWorkerDefinition.InternalName
            : $"{TestWorkerDefinition.InternalName}.{workerId}";
    }

    private Point FindNextAvailableSpawnTile(GameLocation location, string? reservedWorkerId)
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
                return candidate;
            }
        }

        return origin;
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

    private bool IsSpawnTileAvailable(GameLocation location, Vector2 spawnTile, NPC? workerToIgnore)
    {
        if (!location.isTileOnMap(spawnTile))
        {
            return false;
        }

        if (location.IsTileBlockedBy(spawnTile, CollisionMask.All, CollisionMask.Characters, useFarmerTile: true))
        {
            return false;
        }

        NPC? occupant = location.isCharacterAtTile(spawnTile);
        return occupant is null || occupant == workerToIgnore;
    }
}
