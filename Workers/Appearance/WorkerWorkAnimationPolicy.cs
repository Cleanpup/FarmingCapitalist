namespace HireSkilledHelpers.Workers;

internal enum WorkerWorkAnimationKind { Water, Gather, Axe, Pickaxe, Scythe }

/// <summary>Work poses use the native farmer tool/pickup metadata, with scythes sharing the combat sword poses.</summary>
internal static class WorkerWorkAnimationPolicy
{
    public const int FirstSheetFrame = 40; // leaves existing walking and sword frames unchanged
    public const int FramesPerDirection = 5;
    public const int BodyKinds = 3;
    public const double DurationMilliseconds = 400; // existing 24-tick chopping cadence

    public static int BodyKind(WorkerWorkAnimationKind kind) => kind switch
    {
        WorkerWorkAnimationKind.Water => 0,
        WorkerWorkAnimationKind.Gather or WorkerWorkAnimationKind.Scythe => 1,
        WorkerWorkAnimationKind.Axe or WorkerWorkAnimationKind.Pickaxe => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public static int PoseCount(WorkerWorkAnimationKind kind) => kind == WorkerWorkAnimationKind.Scythe ? WorkerCombatSwingPolicy.PoseCount : BodyKind(kind) == 2 ? 5 : 4;
    public static int PoseAt(WorkerWorkAnimationKind kind, double elapsed)
        => kind == WorkerWorkAnimationKind.Scythe ? WorkerCombatSwingPolicy.PoseAt(elapsed) : Math.Clamp((int)(elapsed / DurationMilliseconds * PoseCount(kind)), 0, PoseCount(kind) - 1);
    public static double ImpactMilliseconds(WorkerWorkAnimationKind kind)
        => kind == WorkerWorkAnimationKind.Scythe ? WorkerCombatSwingPolicy.ImpactMilliseconds : DurationMilliseconds / PoseCount(kind) * (BodyKind(kind) == 2 || kind == WorkerWorkAnimationKind.Water ? 2 : 1);
    public static int SheetFrame(WorkerWorkAnimationKind kind, int facing, int pose)
        => kind == WorkerWorkAnimationKind.Scythe ? WorkerCombatSwingPolicy.SheetFrame(facing, pose) : FirstSheetFrame + (BodyKind(kind) * 4 + WorkerCombatSwingPolicy.DirectionRow(facing)) * FramesPerDirection + pose;
    public static int NativeAnimation(WorkerWorkAnimationKind kind, int facing)
        => kind == WorkerWorkAnimationKind.Scythe ? WorkerCombatSwingPolicy.SwordAnimation(facing) : BodyKind(kind) switch
        {
            0 => facing switch { 2 => 164, 1 => 172, 0 => 180, 3 => 188, _ => throw new ArgumentOutOfRangeException(nameof(facing)) },
            1 => facing switch { 2 => 281, 1 => 280, 0 => 279, 3 => 282, _ => throw new ArgumentOutOfRangeException(nameof(facing)) },
            _ => facing switch { 2 => 160, 1 => 168, 0 => 176, 3 => 184, _ => throw new ArgumentOutOfRangeException(nameof(facing)) },
        };
    public static bool CanApplyImpact(bool applied, WorkerWorkAnimationKind kind, double elapsed,
        bool host, bool worldReady, bool valid)
        => !applied && host && worldReady && valid && elapsed >= ImpactMilliseconds(kind);
}
