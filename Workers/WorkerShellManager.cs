using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerShellManager
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly string saveDataKey;
    private readonly string workerIdDataKey;
    private readonly WorkerSpriteSheetBuilder spriteSheetBuilder;
    private NPC? cachedWorker;
    private Texture2D? generatedSpriteSheet;
    private WorkerAppearanceData? savedAppearance;

    public WorkerShellManager(IModHelper helper, IManifest manifest, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.saveDataKey = $"{manifest.UniqueID}.TestWorkerAppearance";
        this.workerIdDataKey = $"{manifest.UniqueID}/WorkerId";
        this.spriteSheetBuilder = new WorkerSpriteSheetBuilder(monitor);
    }

    public Vector2 GetExpectedSpawnTile()
    {
        return new Vector2(TestWorkerDefinition.SpawnTile.X, TestWorkerDefinition.SpawnTile.Y);
    }

    public WorkerAppearanceData? GetSavedWorkerAppearance()
    {
        return this.savedAppearance;
    }

    public bool HasSavedWorkerAppearance()
    {
        return this.savedAppearance is not null;
    }

    public void ReloadWorkerAppearance()
    {
        this.savedAppearance = Context.IsWorldReady
            ? this.helper.Data.ReadSaveData<WorkerAppearanceData>(this.saveDataKey)
            : null;

        if (Context.IsWorldReady && this.savedAppearance is null)
        {
            this.RemoveExistingWorkerShell();
        }

        this.cachedWorker = null;
        this.RebuildGeneratedSpriteSheet();
    }

    public void SaveWorkerAppearance(WorkerAppearanceData appearance)
    {
        this.savedAppearance = appearance;
        this.helper.Data.WriteSaveData(this.saveDataKey, appearance);
        this.RebuildGeneratedSpriteSheet();
    }

    public bool DeleteConfiguredWorker()
    {
        bool removedWorker = this.RemoveExistingWorkerShell();
        bool removedAppearance = this.savedAppearance is not null;

        this.savedAppearance = null;
        this.cachedWorker = null;
        this.helper.Data.WriteSaveData<WorkerAppearanceData>(this.saveDataKey, null);
        this.DisposeGeneratedSpriteSheet();

        return removedWorker || removedAppearance;
    }

    public NPC? EnsureConfiguredWorkerPresent(bool respawnAtSpawn = true)
    {
        if (!Context.IsWorldReady || this.savedAppearance is null)
        {
            return null;
        }

        this.EnsureGeneratedSpriteSheet();

        GameLocation? targetLocation = Game1.getLocationFromName(TestWorkerDefinition.LocationName);
        if (targetLocation is null)
        {
            this.monitor.Log($"Could not find test worker location '{TestWorkerDefinition.LocationName}'.", LogLevel.Warn);
            return null;
        }

        Vector2 spawnTile = this.GetExpectedSpawnTile();
        NPC? worker = this.FindWorkerInLocation(targetLocation) ?? this.FindWorkerAnywhere();

        if (worker is null && !this.IsSpawnTileAvailable(targetLocation, spawnTile, workerToIgnore: null))
        {
            this.monitor.Log(
                $"Test worker spawn tile {spawnTile} in {targetLocation.NameOrUniqueName} is blocked, so the worker shell was not spawned.",
                LogLevel.Warn);
            return null;
        }

        if (worker is null)
        {
            worker = this.CreateWorkerShell(targetLocation, spawnTile);
            targetLocation.addCharacter(worker);
            this.monitor.Log($"Spawned test worker shell at {targetLocation.NameOrUniqueName} tile {spawnTile}.", LogLevel.Info);
            this.cachedWorker = worker;
            return worker;
        }

        if (!respawnAtSpawn)
        {
            this.cachedWorker = worker;
            return worker;
        }

        bool canUseSpawnTile = this.IsSpawnTileAvailable(
            targetLocation,
            spawnTile,
            worker.currentLocation == targetLocation ? worker : null);
        if (!canUseSpawnTile)
        {
            this.monitor.Log(
                $"Test worker shell was found, but spawn tile {spawnTile} is currently blocked. Leaving the worker at tile {worker.Tile}.",
                LogLevel.Warn);
            this.cachedWorker = worker;
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

        this.ApplyWorkerShellState(worker, targetLocation, spawnTile, moveToSpawn: true);
        this.cachedWorker = worker;
        return worker;
    }

    public bool TryGetTestWorker(out NPC? worker)
    {
        worker = null;

        if (!Context.IsWorldReady)
        {
            return false;
        }

        if (this.cachedWorker is not null && this.IsTestWorker(this.cachedWorker))
        {
            worker = this.cachedWorker;
            return true;
        }

        worker = this.FindWorkerInLocation(Game1.getLocationFromName(TestWorkerDefinition.LocationName) ?? Game1.player.currentLocation);
        if (worker is not null)
        {
            this.cachedWorker = worker;
            return true;
        }

        NPC? globalWorker = Game1.getCharacterFromName(TestWorkerDefinition.InternalName, mustBeVillager: false);
        if (globalWorker is not null && this.IsTestWorker(globalWorker))
        {
            this.cachedWorker = globalWorker;
            worker = globalWorker;
            return true;
        }

        return false;
    }

    public void Reset()
    {
        this.cachedWorker = null;
        this.savedAppearance = null;
        this.DisposeGeneratedSpriteSheet();
    }

    private NPC CreateWorkerShell(GameLocation location, Vector2 spawnTile)
    {
        Texture2D portrait = Game1.content.Load<Texture2D>(TestWorkerDefinition.ShellPortraitAssetName);
        NPC worker = new(
            this.CreateWorkerSprite(),
            spawnTile * Game1.tileSize,
            location.NameOrUniqueName,
            TestWorkerDefinition.FacingDirection,
            TestWorkerDefinition.InternalName,
            portrait,
            eventActor: false);

        this.ApplyWorkerShellState(worker, location, spawnTile, moveToSpawn: true);
        return worker;
    }

    private void ApplyWorkerShellState(NPC worker, GameLocation location, Vector2 spawnTile, bool moveToSpawn)
    {
        this.ApplyWorkerIdentity(worker);

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

    private void ApplyWorkerIdentity(NPC worker)
    {
        worker.Name = TestWorkerDefinition.InternalName;
        worker.displayName = TestWorkerDefinition.DisplayName;
        worker.SimpleNonVillagerNPC = true;
        worker.Sprite = this.CreateWorkerSprite();
        worker.Portrait = Game1.content.Load<Texture2D>(TestWorkerDefinition.ShellPortraitAssetName);
        worker.modData[this.workerIdDataKey] = TestWorkerDefinition.WorkerId;

        if (worker.Sprite is not null)
        {
            worker.Sprite.SpriteHeight = 32;
            worker.Sprite.UpdateSourceRect();
        }
    }

    private NPC? FindWorkerInLocation(GameLocation location)
    {
        NPC? namedWorker = location.getCharacterFromName(TestWorkerDefinition.InternalName);
        if (namedWorker is not null && this.IsTestWorker(namedWorker))
        {
            return namedWorker;
        }

        foreach (NPC npc in location.characters)
        {
            if (this.IsTestWorker(npc))
            {
                return npc;
            }
        }

        return null;
    }

    private NPC? FindWorkerAnywhere()
    {
        if (this.cachedWorker is not null && this.IsTestWorker(this.cachedWorker))
        {
            return this.cachedWorker;
        }

        NPC? namedWorker = Game1.getCharacterFromName(TestWorkerDefinition.InternalName, mustBeVillager: false);
        if (namedWorker is not null && this.IsTestWorker(namedWorker))
        {
            return namedWorker;
        }

        foreach (GameLocation location in Game1.locations)
        {
            NPC? worker = this.FindWorkerInLocation(location);
            if (worker is not null)
            {
                return worker;
            }
        }

        return null;
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

    private bool IsTestWorker(NPC npc)
    {
        if (npc.modData.TryGetValue(this.workerIdDataKey, out string? workerId))
        {
            return workerId == TestWorkerDefinition.WorkerId;
        }

        return npc.Name == TestWorkerDefinition.InternalName;
    }

    private bool RemoveExistingWorkerShell()
    {
        NPC? worker = this.FindWorkerAnywhere();
        if (worker is null)
        {
            return false;
        }

        worker.currentLocation?.characters.Remove(worker);
        if (this.cachedWorker == worker)
        {
            this.cachedWorker = null;
        }

        return true;
    }

    private AnimatedSprite CreateWorkerSprite()
    {
        if (this.generatedSpriteSheet is null)
        {
            return new AnimatedSprite(TestWorkerDefinition.ShellSpriteAssetName, 0, 16, 32);
        }

        AnimatedSprite sprite = new()
        {
            SpriteWidth = 16,
            SpriteHeight = 32,
            framesPerAnimation = 4,
            spriteTexture = this.generatedSpriteSheet,
            overrideTextureName = $"{TestWorkerDefinition.InternalName}.GeneratedSheet",
            loadedTexture = $"{TestWorkerDefinition.InternalName}.GeneratedSheet",
            textureUsesFlippedRightForLeft = false,
        };

        sprite.CurrentFrame = 0;
        return sprite;
    }

    private void EnsureGeneratedSpriteSheet()
    {
        if (this.generatedSpriteSheet is null && this.savedAppearance is not null)
        {
            this.RebuildGeneratedSpriteSheet();
        }
    }

    private void RebuildGeneratedSpriteSheet()
    {
        if (!Context.IsWorldReady || this.savedAppearance is null)
        {
            this.DisposeGeneratedSpriteSheet();
            return;
        }

        Texture2D newSheet;
        try
        {
            newSheet = this.spriteSheetBuilder.BuildSheet(this.savedAppearance);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Failed to rebuild the generated worker sprite sheet: {ex.Message}", LogLevel.Warn);
            return;
        }

        Texture2D? previousSheet = this.generatedSpriteSheet;
        this.generatedSpriteSheet = newSheet;

        NPC? existingWorker = this.FindWorkerAnywhere();
        if (existingWorker is not null)
        {
            int currentFrame = existingWorker.Sprite?.CurrentFrame ?? 0;
            existingWorker.Sprite = this.CreateWorkerSprite();
            existingWorker.Sprite.CurrentFrame = currentFrame;
        }

        previousSheet?.Dispose();
    }

    private void DisposeGeneratedSpriteSheet()
    {
        this.generatedSpriteSheet?.Dispose();
        this.generatedSpriteSheet = null;
    }
}
