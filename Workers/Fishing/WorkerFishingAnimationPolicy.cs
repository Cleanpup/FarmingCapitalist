namespace HireSkilledHelpers.Workers;

/// <summary>Callback-free vanilla farmer cast frames, held pose, and reversed reel motion.</summary>
internal static class WorkerFishingAnimationPolicy
{
    public const int FirstSheetFrame = 100;
    public const int FramesPerDirection = 6;
    public static int SheetFrame(int facing, int phase)
    {
        int pose = phase < 45 ? Math.Clamp(phase / 9, 0, 4)
            : phase <= 315 ? 5 : Math.Clamp((360 - phase) / 9, 0, 4);
        return FirstSheetFrame + WorkerCombatSwingPolicy.DirectionRow(facing) * FramesPerDirection + pose;
    }
    public static int NativeFrame(int facing, int pose) => facing switch
    {
        0 => new[] { 76, 38, 63, 62, 63, 76 }[pose],
        2 => new[] { 66, 67, 68, 69, 70, 74 }[pose],
        _ => new[] { 48, 49, 50, 51, 52, 72 }[pose],
    };
}
