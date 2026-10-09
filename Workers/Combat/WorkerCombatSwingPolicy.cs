namespace FarmingCapitalist.Workers;

/// <summary>Visual-only sword poses and the single impact point of a worker swing.</summary>
internal static class WorkerCombatSwingPolicy
{
    public const int PoseCount = 6;
    public const int FirstSheetFrame = 16;
    public const double ImpactMilliseconds = 100;
    // Native sword poses last 55, 45, 25, 25, 25 ms, then twice
    // the Rusty Sword's 65 ms animation interval for recovery.
    public const int SwordAnimationIntervalMilliseconds = 65;
    public const double DurationMilliseconds = 305;

    public static int PoseAt(double elapsedMilliseconds) => elapsedMilliseconds switch
    {
        < 55 => 0,
        < 100 => 1,
        < 125 => 2,
        < 150 => 3,
        < 175 => 4,
        _ => 5,
    };

    public static int DirectionRow(int facing) => facing switch { 2 => 0, 1 => 1, 0 => 2, 3 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(facing)) };

    public static int SheetFrame(int facing, int pose) => FirstSheetFrame + DirectionRow(facing) * PoseCount + pose;

    public static int FacingTowards(int workerX, int workerY, int targetX, int targetY, int currentFacing)
    {
        int dx = targetX - workerX;
        int dy = targetY - workerY;
        if (dx == 0 && dy == 0)
            return currentFacing;
        return Math.Abs(dx) > Math.Abs(dy) ? (dx < 0 ? 3 : 1) : (dy < 0 ? 0 : 2);
    }

    // These are animation IDs, rather than texture-frame indices. Let the
    // game supply the full pose metadata (especially its sword arm overlay).
    public static int SwordAnimation(int facing) => facing switch { 2 => 232, 1 => 240, 0 => 248, 3 => 256,
        _ => throw new ArgumentOutOfRangeException(nameof(facing)) };

    public static bool CanApplyImpact(bool alreadyApplied, double elapsedMilliseconds,
        bool sameLocation, bool alive, bool present, bool inReach, bool revealed, bool invincible)
        => !alreadyApplied && elapsedMilliseconds >= ImpactMilliseconds && sameLocation
            && alive && present && inReach && revealed && !invincible;
}
