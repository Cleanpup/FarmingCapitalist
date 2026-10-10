namespace HireSkilledHelpers.Workers;

internal static class WorkerReturnFallbackPolicy
{
    public static bool MayUseEmergencyWarp(bool visibleRouteAvailable, bool clearableDebrisRouteAvailable, bool safeLandingAvailable)
        => !visibleRouteAvailable && !clearableDebrisRouteAvailable && safeLandingAvailable;
}
