namespace HireSkilledHelpers.Workers;

internal sealed class WorkerPermitState
{
    public WorkerPermitState() { }

    public bool PermissionGranted { get; set; }
}

/// <summary>The host's permit or bypass controls the farm; client settings cannot unlock it.</summary>
internal static class WorkerUnlockPolicy
{
    public const string EventId = "Cleanpup.FarmingCapitalist.FarmhandPermit";
    public const int FirstEligibleDay = 7; // WorldDate.TotalDays is zero-based: Spring 8, year 1.
    public const string LockedMessage = "Farm Crew unlocks after Mayor Lewis's visit, available from Spring 8. The host can skip the visit in the mod settings.";

    public static bool IsAvailable(bool worldReady, bool isHost, bool permissionGranted,
        bool bypassEvent, bool hostMirrorUnlocked)
        => worldReady && (isHost ? permissionGranted || bypassEvent : hostMirrorUnlocked);

    public static bool TryGrantPermission(WorkerPermitState state, bool worldReady, bool isHost, bool eventSeen)
    {
        if (!worldReady || !isHost || !eventSeen || state.PermissionGranted) return false;
        state.PermissionGranted = true;
        return true;
    }

    public static bool MayOfferEvent(int elapsedDays, bool isHost, bool unlocked,
        bool onFarm, bool playerFree, bool festivalDay)
        => elapsedDays >= FirstEligibleDay && isHost && !unlocked && onFarm && playerFree && !festivalDay;
}
