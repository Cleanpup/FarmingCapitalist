using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist.Workers;

/// <summary>Offers the host a native, skippable mayor visit once the farm is eligible.</summary>
internal sealed class WorkerPermitEventManager
{
    private const int FreeTicksBeforeVisit = 30;
    private const int RetryDelayTicks = 300;
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly Func<bool> isUnlocked;
    private readonly Action onPermissionGranted;
    private Event? pendingEvent;
    private GameLocation? pendingLocation;
    private string? scriptTemplate;
    private int freeTicks;
    private int retryTicks;
    private bool loggedFailure;
    private bool manualVisitRequested;

    public WorkerPermitEventManager(IModHelper helper, IMonitor monitor,
        Func<bool> isUnlocked, Action onPermissionGranted)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.isUnlocked = isUnlocked;
        this.onPermissionGranted = onPermissionGranted;
    }

    /// <summary>Queue the same standalone scene early, including a replay of a completed visit.</summary>
    public bool TryStartEarly(out string message)
    {
        if (!Context.IsWorldReady)
        {
            message = "Load a save before requesting Mayor Lewis's visit.";
            return false;
        }
        if (!Context.IsMainPlayer)
        {
            message = "Only the farm host can request Mayor Lewis's visit.";
            return false;
        }
        if (Game1.currentLocation is not Farm)
        {
            message = "Go outside onto the Farm, then run workerpermit again.";
            return false;
        }
        if (this.pendingEvent is not null)
        {
            message = "Mayor Lewis's farmhand visit is already playing or finishing.";
            return false;
        }
        if (this.manualVisitRequested)
        {
            message = "Mayor Lewis's visit is already queued. Stay on the Farm and stand still on clear ground when the game is unpaused.";
            return true;
        }

        // Explicit requests bypass only date/permit eligibility. Update still enforces
        // menus, events, tools, warps, fades, festivals and safe actor placement.
        this.manualVisitRequested = true;
        this.freeTicks = 0;
        this.retryTicks = 0;
        message = "Mayor Lewis's visit is queued, even before Spring 8. Return to the game, close any menu, and stand still on clear ground on the Farm. Existing permission is preserved; leaving the Farm cancels the request.";
        return true;
    }

    public void Update()
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
            return;

        if (this.pendingEvent is not null)
        {
            if (!ReferenceEquals(this.pendingLocation?.currentEvent, this.pendingEvent))
            {
                // An interrupted/replaced event is not completion. Leave the permit locked
                // and allow a later visit; the native completion callback is the only grant.
                this.ClearPendingEvent();
                this.retryTicks = RetryDelayTicks;
            }
            return;
        }

        if (Game1.currentLocation is not Farm farm)
        {
            this.freeTicks = 0;
            if (this.manualVisitRequested)
            {
                this.manualVisitRequested = false;
                this.monitor.Log("Mayor Lewis's queued visit was cancelled because you left the Farm. Run workerpermit on the Farm to request it again.", LogLevel.Info);
            }
            return;
        }

        if (this.retryTicks > 0)
        {
            this.retryTicks--;
            return;
        }

        bool playerFree = Context.IsPlayerFree && Game1.player.CanMove
            && !Game1.player.UsingTool && !Game1.player.isEating && !Game1.player.isMoving()
            && Game1.player.mount is null && Game1.currentMinigame is null
            && Game1.currentLocation?.currentEvent is null && !Game1.eventUp && !Game1.eventOver
            && Game1.locationRequest is null && !Game1.fadeToBlack && !Game1.fadeIn
            && Game1.fadeToBlackAlpha <= 0f && Game1.shouldTimePass();

        bool festivalDay = Utility.isFestivalDay();
        bool mayOffer = this.manualVisitRequested
            ? playerFree && !festivalDay
            : WorkerUnlockPolicy.MayOfferEvent(Game1.Date.TotalDays, Context.IsMainPlayer,
                this.isUnlocked(), true, playerFree, festivalDay);
        if (!mayOffer)
        {
            this.freeTicks = 0;
            return;
        }

        if (++this.freeTicks < FreeTicksBeforeVisit)
            return;

        this.freeTicks = 0;
        Point farmerTile = Game1.player.TilePoint;
        if (!TryFindMayorTile(farm, farmerTile, out Point mayorTile, out int farmerFacing))
        {
            // Never clear farm objects or place Lewis inside a building/crop to stage a visit.
            this.retryTicks = RetryDelayTicks;
            return;
        }

        try
        {
            this.scriptTemplate ??= this.helper.ModContent
                .Load<Dictionary<string, string>>("assets/events/farmhand-permit.json")["script"];
            string script = this.scriptTemplate
                .Replace("{{farmerX}}", Format(farmerTile.X))
                .Replace("{{farmerY}}", Format(farmerTile.Y))
                .Replace("{{mayorX}}", Format(mayorTile.X))
                .Replace("{{mayorY}}", Format(mayorTile.Y))
                .Replace("{{farmerFacing}}", Format(farmerFacing))
                .Replace("{{mayorFacing}}", Format((farmerFacing + 2) % 4))
                .Replace("{{hiringCost}}", Format(WorkerEmploymentTerms.HiringCost))
                .Replace("{{dailyWage}}", Format(WorkerEmploymentTerms.DailyWage));

            Farmer host = Game1.player;
            // Generated script: no Data/Events asset registration or entry-only trigger.
            // Positive Lewis coordinates create a separate native event actor, not the NPC.
            Event visit = new(script, null, WorkerUnlockPolicy.EventId, host)
            {
                ignoreTileOffsets = true
            };
            this.pendingEvent = visit;
            this.pendingLocation = farm;
            visit.onEventFinished = () => this.OnVisitFinished(visit, host);
            farm.startEvent(visit);
            if (ReferenceEquals(farm.currentEvent, visit))
                this.manualVisitRequested = false;
        }
        catch (Exception ex)
        {
            if (!this.loggedFailure)
            {
                this.monitor.Log($"Couldn't start Mayor Lewis's farmhand visit; it will retry when possible. {ex}", LogLevel.Warn);
                this.loggedFailure = true;
            }
            if (!ReferenceEquals(this.pendingLocation?.currentEvent, this.pendingEvent))
                this.ClearPendingEvent();
            this.retryTicks = RetryDelayTicks;
        }
    }

    public void Reset()
    {
        this.ClearPendingEvent();
        this.freeTicks = 0;
        this.retryTicks = 0;
        this.loggedFailure = false;
        this.scriptTemplate = null;
        this.manualVisitRequested = false;
    }

    private void OnVisitFinished(Event visit, Farmer host)
    {
        if (!ReferenceEquals(this.pendingEvent, visit))
            return;

        this.ClearPendingEvent();
        this.retryTicks = RetryDelayTicks;
        // Native end and skip both record eventsSeen before invoking onEventFinished.
        // A forced interruption can invoke that callback without recording completion.
        if (Context.IsWorldReady && Context.IsMainPlayer && ReferenceEquals(Game1.player, host)
            && host.eventsSeen.Contains(WorkerUnlockPolicy.EventId))
            this.onPermissionGranted();
    }

    private void ClearPendingEvent()
    {
        if (this.pendingEvent is not null)
            this.pendingEvent.onEventFinished = null;
        this.pendingEvent = null;
        this.pendingLocation = null;
    }

    private static bool TryFindMayorTile(Farm farm, Point farmerTile, out Point mayorTile, out int farmerFacing)
    {
        // Stage beside the host's actual position on any farm layout, leaving the farmer
        // in place. Native end/skip restores their recorded position and facing.
        Point[] directions = { new(0, 1), new(1, 0), new(-1, 0), new(0, -1) };
        int[] facings = { 2, 1, 3, 0 };
        for (int distance = 2; distance >= 1; distance--)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                Point step = directions[i];
                Point candidate = new(farmerTile.X + step.X * distance, farmerTile.Y + step.Y * distance);
                Vector2 tile = new(candidate.X, candidate.Y);
                Vector2 between = new(farmerTile.X + step.X, farmerTile.Y + step.Y);
                if (candidate.X >= 0 && candidate.Y >= 0 && IsClearStageTile(farm, tile)
                    && IsClearStageTile(farm, between))
                {
                    mayorTile = candidate;
                    farmerFacing = facings[i];
                    return true;
                }
            }
        }
        mayorTile = default;
        farmerFacing = 2;
        return false;
    }

    private static bool IsClearStageTile(Farm farm, Vector2 tile)
        => farm.isTileOnMap(tile) && farm.isTilePassable(tile)
            && farm.CanItemBePlacedHere(tile,
                ignorePassables: CollisionMask.Flooring | CollisionMask.TerrainFeatures);

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
