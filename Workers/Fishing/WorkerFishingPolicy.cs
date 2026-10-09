namespace FarmingCapitalist.Workers;

internal static class WorkerFishingPolicy
{
    public const int MinutesPerCatch = 30;
    public const int MaximumAttemptsPerDay = 36;
    public static bool IsWithinWorkHours(int time) => time >= 600 && time < 2400;
    public static int WorkMinute(int time) => Math.Clamp(time / 100 * 60 + time % 100, 360, 1440);

    public static void Normalize(WorkerFishingProgress progress, int day)
    {
        progress.Day = Math.Clamp(progress.Day, -1, day);
        progress.Minutes = Math.Clamp(progress.Minutes, 0, 1080);
        progress.CompletedAttempts = Math.Clamp(progress.CompletedAttempts, 0, MaximumAttemptsPerDay);
        progress.CompletedCatches = Math.Clamp(progress.CompletedCatches, 0, progress.CompletedAttempts);
        progress.LastObservedMinute = progress.LastObservedMinute is >= 360 and <= 1440 ? progress.LastObservedMinute : -1;
        progress.PendingCatches ??= new();
        progress.PendingCatches.RemoveAll(item => item is null || !WorkerFishingCatchCatalog.IsAllowed(item.ItemId));
    }

    public static bool ObserveClock(WorkerFishingProgress progress, int day, int time, bool accrue)
    {
        bool changed = false;
        if (progress.Day != day)
        {
            progress.Day = day;
            progress.Minutes = progress.CompletedCatches = progress.CompletedAttempts = 0;
            progress.LastObservedMinute = -1;
            changed = true;
        }
        int minute = WorkMinute(time);
        if (minute <= progress.LastObservedMinute) return changed;
        if (accrue && progress.LastObservedMinute >= 0) progress.Minutes += minute - progress.LastObservedMinute;
        progress.LastObservedMinute = minute;
        return true;
    }

    public static bool TryQueueCatch(WorkerRosterEntry entry, WorkerFishingProgress progress, WorkerFishingCatch? catchItem,
        int day, bool isHost, bool worldReady, bool paid)
    {
        if (!isHost || !worldReady || !paid || entry.Profession != WorkerProfession.Fisher || entry.AssignedTask != WorkerTaskKind.Fish
            || progress.Day != day || progress.Minutes < MinutesPerCatch || progress.CompletedAttempts >= MaximumAttemptsPerDay
            || entry.Fishing.Day != day || entry.Fishing.Minutes != progress.Minutes
            || entry.Fishing.CompletedAttempts != progress.CompletedAttempts || entry.Fishing.CompletedCatches != progress.CompletedCatches
            || (catchItem is not null && !WorkerFishingCatchCatalog.IsAllowed(catchItem.ItemId))) return false;
        progress.Minutes -= MinutesPerCatch;
        progress.CompletedAttempts++;
        if (catchItem is not null)
        {
            progress.CompletedCatches++;
            progress.PendingCatches.Add(catchItem.Clone());
        }
        entry.Experience ??= new();
        entry.Experience.Fishing = (int)Math.Min(int.MaxValue,
            (long)Math.Max(0, entry.Experience.Fishing) + (catchItem is null ? 0 : WorkerFishingCatchCatalog.Experience(catchItem.ItemId)));
        entry.Fishing = progress.Clone();
        return true;
    }
}
