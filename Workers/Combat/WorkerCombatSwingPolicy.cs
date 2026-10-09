namespace FarmingCapitalist.Workers;

/// <summary>Visual-only sword poses and the single impact point of a worker swing.</summary>
internal static class WorkerCombatSwingPolicy
{
    public const int PoseCount = 6;
    public const int FirstSheetFrame = 16;
    public const double ImpactMilliseconds = 100;
    public const double DurationMilliseconds = 320;

    public static int PoseAt(double elapsedMilliseconds) => elapsedMilliseconds switch
    {
        < 55 => 0,
        < 100 => 1,
        < 140 => 2,
        < 180 => 3,
        < 220 => 4,
        _ => 5,
    };

    public static int DirectionRow(int facing) => facing switch { 2 => 0, 1 => 1, 0 => 2, 3 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(facing)) };

    public static int SheetFrame(int facing, int pose) => FirstSheetFrame + DirectionRow(facing) * PoseCount + pose;

    public static int FarmerFrame(int facing, int pose) => (facing switch { 2 => 24, 0 => 36, _ => 30 }) + pose;

    public static bool CanApplyImpact(bool alreadyApplied, double elapsedMilliseconds,
        bool sameLocation, bool alive, bool present, bool inReach, bool revealed, bool invincible)
        => !alreadyApplied && elapsedMilliseconds >= ImpactMilliseconds && sameLocation
            && alive && present && inReach && revealed && !invincible;
}
