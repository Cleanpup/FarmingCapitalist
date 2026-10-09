using FarmingCapitalist.Workers;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
WorkerRosterEntry worker = new() { Profession = WorkerProfession.Fisher, AssignedTask = WorkerTaskKind.Fish };
WorkerFishingProgress progress = new();
Check(WorkerFishingPolicy.ObserveClock(progress, 12, 600, true) && progress.Minutes == 0, "Arrival must not grant time");
WorkerFishingPolicy.ObserveClock(progress, 12, 630, true);
worker.Fishing = progress.Clone();
Check(!WorkerFishingPolicy.TryQueueCatch(worker, progress, new() { ItemId = "(O)145" }, 12, false, true, true), "Farmhand awarded catch");
Check(!WorkerFishingPolicy.TryQueueCatch(worker, progress, new() { ItemId = "(O)145" }, 12, true, true, false), "Unpaid worker awarded catch");
Check(!WorkerFishingPolicy.TryQueueCatch(worker, progress, new() { ItemId = "(O)159" }, 12, true, true, true), "Legendary catch accepted");
Check(WorkerFishingPolicy.TryQueueCatch(worker, progress, null, 12, true, true, true), "No-catch attempt rejected");
Check(progress.Minutes == 0 && progress.CompletedAttempts == 1 && progress.CompletedCatches == 0 && worker.Experience.Fishing == 0, "Failed attempt reroll/XP bug");
Check(!WorkerFishingPolicy.TryQueueCatch(worker, progress, null, 12, true, true, true), "Attempt repeated without elapsed time");
WorkerFishingPolicy.ObserveClock(progress, 12, 700, true);
worker.Fishing = progress.Clone();
WorkerFishingProgress before = progress.Clone();
Check(WorkerFishingPolicy.TryQueueCatch(worker, progress, new() { ItemId = "(O)145", Experience = 9999 }, 12, true, true, true), "Valid catch rejected");
Check(progress.PendingCatches.Count == 1 && progress.CompletedCatches == 1 && worker.Experience.Fishing == 13, "Catch didn't use validated species XP");
Check(!WorkerFishingPolicy.TryQueueCatch(worker, before, new() { ItemId = "(O)145" }, 12, true, true, true), "Stale completion duplicated");
WorkerFishingProgress reloaded = worker.Clone().Fishing;
Check(reloaded.PendingCatches.Count == 1 && reloaded.CompletedCatches == 1 && reloaded.Minutes == 0, "Save clone lost pending catches");
WorkerFishingPolicy.ObserveClock(reloaded, 12, 800, false);
WorkerFishingPolicy.ObserveClock(reloaded, 12, 810, true);
Check(reloaded.Minutes == 10, "Paused clock gap awarded time");
WorkerFishingPolicy.ObserveClock(reloaded, 12, 700, true);
Check(reloaded.Minutes == 10, "Rewinding clock awarded time");
WorkerFishingPolicy.ObserveClock(reloaded, 13, 600, false);
Check(reloaded.CompletedAttempts == 0 && reloaded.CompletedCatches == 0 && reloaded.Minutes == 0 && reloaded.PendingCatches.Count == 1, "Day reset lost completed loot");
Check(WorkerFishingPolicy.IsWithinWorkHours(2350) && !WorkerFishingPolicy.IsWithinWorkHours(2400), "Midnight hours wrong");
Check(WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.Fisher, WorkerTaskKind.Fish) && !WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.Farmer, WorkerTaskKind.Fish), "Profession task rule wrong");
string[] legendary = { "159", "160", "163", "682", "775", "898", "899", "900", "901", "902" };
foreach (string id in legendary) Check(!WorkerFishingCatchCatalog.IsAllowed("(O)" + id), "Legendary pending item admitted " + id);
Check(WorkerFishingCatchCatalog.Eligible("Forest", "spring", 1200, false).Contains("(O)145"), "Spring sunny sunfish absent");
Check(!WorkerFishingCatchCatalog.Eligible("Forest", "winter", 1200, false).Contains("(O)145"), "Winter sunfish admitted");
Check(WorkerFishingCatchCatalog.Eligible("Forest", "spring", 1200, true).Contains("(O)143"), "Rainy spring catfish absent");
Check(!WorkerFishingCatchCatalog.Eligible("Forest", "spring", 1200, false).Contains("(O)143"), "Sunny catfish admitted");
Check(!WorkerFishingCatchCatalog.Eligible("Forest", "spring", 1200, true).Contains("(O)137"), "Forest pond species leaked into river");
Check(WorkerFishingCatchCatalog.Eligible("Mountain", "winter", 2230, false).Contains("(O)269"), "Night midnight carp absent");
Check(!WorkerFishingCatchCatalog.Eligible("Mountain", "winter", 2100, false).Contains("(O)269"), "Early midnight carp admitted");
Check(!WorkerFishingCatchCatalog.Eligible("Beach", "winter", 1300, false).Contains("(O)708") && WorkerFishingCatchCatalog.Eligible("Beach", "winter", 1900, false).Contains("(O)708"), "Halibut split window wrong");
Check(!WorkerFishingCatchCatalog.Eligible("Beach", "fall", 1300, false).Contains("(O)705") && WorkerFishingCatchCatalog.Eligible("Beach", "fall", 1800, false).Contains("(O)705"), "Albacore split window wrong");
Check(WorkerFishingCatchCatalog.Eligible("IslandSouth", "winter", 1200, false).Contains("(O)128"), "Island all-season gate wrong");
foreach (string area in new[] { "Forest", "Mountain", "Town", "Beach", "IslandSouth" })
foreach (string season in new[] { "spring", "summer", "fall", "winter" })
foreach (bool rain in new[] { false, true })
foreach (int time in new[] { 600, 1100, 1600, 1900, 2350 })
{
    var eligible = WorkerFishingCatchCatalog.Eligible(area, season, time, rain);
    for (int i = 0; i < 30; i++)
    {
        var caught = WorkerFishingCatchCatalog.Roll(area, season, time, rain, 5, new Random(i));
        Check(caught is null || eligible.Contains(caught.ItemId), "Roll leaked species gate");
    }
}
Check(WorkerFishingShorePolicy.TryFindCast(66, 50, 0, 1, (x,y) => x == 66 && y >= 51 && y <= 54,
    (x,y) => x == 66 && y >= 52, out var forestCast) && forestCast == (66, 52), "Forest decorative border blocked valid two-tile cast");
Check(WorkerFishingShorePolicy.TryFindCast(69, 81, 1, 0, (x,y) => y == 81 && x >= 70 && x <= 73,
    (x,y) => y == 81 && x >= 72, out var townCast) && townCast == (72, 81), "Town decorative borders blocked valid three-tile cast");
Check(!WorkerFishingShorePolicy.TryFindCast(0, 0, 1, 0, (x,y) => x != 2, (x,y) => x == 3, out _), "Cast crossed a dry gap");
Check(!WorkerFishingShorePolicy.TryFindCast(0, 0, 1, 0, (x,y) => true, (x,y) => x == 5, out _), "Cast exceeded bounded reach");
Check(!WorkerFishingShorePolicy.TryFindCast(0, 0, 1, 1, (x,y) => true, (x,y) => true, out _), "Diagonal cast accepted");
Check(WorkerFishingShorePolicy.IsContinuousWaterCast(69,81,72,81,(x,y) => y==81 && x>=70), "Town cast did not revalidate");
Check(!WorkerFishingShorePolicy.IsContinuousWaterCast(69,81,72,81,(x,y) => x!=71), "Interrupted water cast remained valid");
Check(!WorkerFishingShorePolicy.IsContinuousWaterCast(69,81,70,82,(x,y) => true), "Diagonal cast revalidated");
Console.WriteLine("Fishing checks passed: shoreline border casts, dry-gap/reach rejection, authority, timing, no-catch, stale completion, persistence, XP, legendary exclusions, season/time/weather/location gates.");
