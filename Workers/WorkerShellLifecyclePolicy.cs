namespace HireSkilledHelpers.Workers;

internal static class WorkerShellLifecyclePolicy
{
    public static bool ShouldRecoverCachedWorker(bool onGeneratedFloor, bool floorIsActive, bool attachedToLocation)
        => !attachedToLocation || (onGeneratedFloor && !floorIsActive);
}
