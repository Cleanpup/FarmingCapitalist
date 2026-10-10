using System.Text.Json;
using FarmingCapitalist.Workers;

StaminaChecks.Run();
PerkChecks.Run();
ProfessionChecks.Run();

static void Equal<T>(T expected, T actual, string scenario)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{scenario}: expected {expected}, got {actual}");
}

static void Check(bool value, string scenario)
{
    if (!value) throw new Exception(scenario);
}

foreach (WorkerProfession profession in Enum.GetValues<WorkerProfession>())
{
    foreach (WorkerTaskKind task in WorkerTaskPolicy.GetTasks(profession))
    {
        Equal(task != WorkerTaskKind.Idle,
            WorkerTaskPolicy.ShouldIdleAfterWorkComplete(task, 1200, true, false, false),
            $"{profession} completed {task} switches to Idle");
        Check(!WorkerTaskPolicy.ShouldIdleAfterWorkComplete(task, 1200, true, true, false),
            $"{task} stamina break retains assignment");
        Check(!WorkerTaskPolicy.ShouldIdleAfterWorkComplete(task, 2200, true, false, false),
            $"{task} end-of-shift return retains daily assignment");
        Check(!WorkerTaskPolicy.ShouldIdleAfterWorkComplete(task, 2400, true, false, false),
            $"{task} midnight return retains assignment");
        Check(!WorkerTaskPolicy.ShouldIdleAfterWorkComplete(task, 1200, false, false, false),
            $"{task} unpaid worker remains off duty");
        Check(!WorkerTaskPolicy.ShouldIdleAfterWorkComplete(task, 1200, true, false, true),
            $"{task} defeated worker remains off duty");
    }
}
Check(!WorkerTaskPolicy.ShouldIdleAfterWorkComplete(WorkerTaskKind.WaterCrops, 550, true, false, false),
    "before-work return does not start daytime wandering");
Check(WorkerTaskPolicy.ShouldIdleAfterWorkComplete(WorkerTaskKind.WaterCrops, 600, true, false, false),
    "completed morning job starts idle movement");
Check(WorkerTaskPolicy.ShouldIdleAfterWorkComplete(WorkerTaskKind.WaterCrops, 2150, true, false, false),
    "last daytime completion still switches to Idle");

var dailyWorker = new WorkerRosterEntry { AssignedTask = WorkerTaskKind.WaterCrops };
WorkerTaskPolicy.AssignTask(dailyWorker, WorkerTaskKind.Idle, 100, completedDailyOrder: true);
Equal(WorkerTaskKind.Idle, dailyWorker.AssignedTask, "completed work becomes actual Idle");
Equal<WorkerTaskKind?>(WorkerTaskKind.WaterCrops, dailyWorker.NextDayTask, "daily watering job remembered");
Check(!WorkerTaskPolicy.RestoreDailyOrder(dailyWorker, 100), "same-day callback cannot restart completed job");
var savedDaily = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(dailyWorker))!;
Equal<WorkerTaskKind?>(WorkerTaskKind.WaterCrops, savedDaily.Clone().NextDayTask, "clone and save preserve remembered order");
Check(!WorkerTaskPolicy.RestoreDailyOrder(savedDaily, 100), "same-day reload remains Idle");
Check(WorkerTaskPolicy.RestoreDailyOrder(savedDaily, 101), "following morning restores daily job");
Equal(WorkerTaskKind.WaterCrops, savedDaily.AssignedTask, "original daily job restored");
Equal<WorkerTaskKind?>(null, savedDaily.NextDayTask, "restore clears completed-day marker");
Check(!WorkerTaskPolicy.RestoreDailyOrder(savedDaily, 101), "duplicate day-start callback does not restore again");
WorkerTaskPolicy.AssignTask(dailyWorker, WorkerTaskKind.Idle, 100);
Check(!WorkerTaskPolicy.RestoreDailyOrder(dailyWorker, 101), "explicit Idle cancels remembered daily job");
WorkerTaskPolicy.AssignTask(dailyWorker, WorkerTaskKind.WaterCrops, 101);
WorkerTaskPolicy.AssignTask(dailyWorker, WorkerTaskKind.Idle, 101, completedDailyOrder: true);
WorkerTaskPolicy.AssignTask(dailyWorker, WorkerTaskKind.HarvestCrops, 101);
Check(!WorkerTaskPolicy.RestoreDailyOrder(dailyWorker, 102), "new order replaces remembered daily job");
Equal(WorkerTaskKind.HarvestCrops, dailyWorker.AssignedTask, "new order is not overwritten tomorrow");
var invalidDaily = new WorkerRosterEntry { Profession = WorkerProfession.Farmer, NextDayTask = WorkerTaskKind.Fish, CompletedTaskDay = 100 };
Check(!WorkerTaskPolicy.RestoreDailyOrder(invalidDaily, 101), "invalid profession order cannot restore");
Equal<WorkerTaskKind?>(null, invalidDaily.NextDayTask, "invalid saved pending task cleared");
var legacyDaily = JsonSerializer.Deserialize<WorkerRosterEntry>("{\"AssignedTask\":1}")!;
Check(!WorkerTaskPolicy.RestoreDailyOrder(legacyDaily, 101), "legacy save has no pending completed order");

var first = new WorkerRosterEntry { WorkerId = "first" };
var second = new WorkerRosterEntry { WorkerId = "second" };
first.HarvestDestination = new WorkerHarvestDestination { LocationName = "Farm", TileX = 5, TileY = 7 };
Check(WorkerExperiencePolicy.IsConfirmedCropHarvest(true, false, 0, 0, 1), "ordinary harvest confirmed");
Check(WorkerExperiencePolicy.IsConfirmedCropHarvest(false, true, 0, 4, 1), "regrowing harvest confirmed");
Check(!WorkerExperiencePolicy.IsConfirmedCropHarvest(false, false, 0, 0, 1), "failed nonregrowing harvest rejected");
Check(!WorkerExperiencePolicy.IsConfirmedCropHarvest(false, true, 4, 4, 1), "retry without crop change rejected");
Check(!WorkerExperiencePolicy.IsConfirmedCropHarvest(true, false, 0, 0, 0), "empty collector rejected");

bool confirmed = WorkerExperiencePolicy.IsConfirmedCropHarvest(true, false, 0, 0, 1);
Check(WorkerExperiencePolicy.TryAwardFarmingHarvest(first, true, true, confirmed, 1), "successful harvest awards");
Equal(WorkerExperiencePolicy.FarmingExperiencePerCropHarvest, first.Experience.Farming, "single award");
Equal(0, second.Experience.Farming, "independent worker");
Check(WorkerExperiencePolicy.TryAwardCompletedAction(first, true, true, WorkerExperienceAction.PickupForage), "forage pickup awards");
Equal(WorkerExperiencePolicy.ForagingExperiencePerForage, first.Experience.Foraging, "forage XP");
Check(WorkerExperiencePolicy.TryAwardCompletedAction(first, true, true, WorkerExperienceAction.ChopTree), "tree awards");
Check(WorkerExperiencePolicy.TryAwardCompletedAction(first, true, true, WorkerExperienceAction.ChopHardwood), "hardwood awards");
Check(WorkerExperiencePolicy.TryAwardCompletedAction(first, true, true, WorkerExperienceAction.ClearWeeds), "weeds award");
Check(WorkerExperiencePolicy.TryAwardCompletedAction(first, true, true, WorkerExperienceAction.ClearTwig), "twig awards");
Equal(WorkerExperiencePolicy.ForagingExperiencePerForage + WorkerExperiencePolicy.ForagingExperiencePerTree
    + WorkerExperiencePolicy.ForagingExperiencePerHardwood + 2 * WorkerExperiencePolicy.ExperiencePerSmallDebris,
    first.Experience.Foraging, "foraging actions total");
Check(WorkerExperiencePolicy.TryAwardCompletedAction(first, true, true, WorkerExperienceAction.ClearStone), "stone awards");
Equal(WorkerExperiencePolicy.ExperiencePerSmallDebris, first.Experience.Mining, "stone gives mining only");
Equal(0, first.Experience.Fishing, "no fishing task XP");
Equal(0, first.Experience.Combat, "no combat task XP");
Equal(0, second.Experience.Foraging, "other worker foraging independent");
Equal(0, second.Experience.Mining, "other worker mining independent");
Check(WorkerExperiencePolicy.TryAwardCombatKill(first, true, true, true, 17), "confirmed worker kill awards vanilla combat XP");
Equal(17, first.Experience.Combat, "combat XP only to killer");
Equal(0, second.Experience.Combat, "other worker combat XP independent");
Check(!WorkerExperiencePolicy.TryAwardCombatKill(first, false, true, true, 17), "client kill cannot award XP");
Check(!WorkerExperiencePolicy.TryAwardCombatKill(first, true, true, false, 17), "unconfirmed kill cannot award XP");
Check(!WorkerExperiencePolicy.TryAwardCombatKill(first, true, true, true, 0), "zero XP kill cannot award XP");
Equal(17, first.Experience.Combat, "rejected combat awards leave XP unchanged");
Equal(2, (int)WorkerProfession.CombatWorker, "profession save enum stable");
Equal(8, (int)WorkerTaskKind.SlayMonsters, "combat task save enum stable");
Check(WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.CombatWorker, WorkerTaskKind.SlayMonsters), "combat job allowed");
Check(!WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.Farmer, WorkerTaskKind.SlayMonsters), "farmer cannot slay");
Check(!WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.CombatWorker, WorkerTaskKind.ClearDebris), "combat worker cannot clear debris");
Equal(9, (int)WorkerTaskKind.ExploreArea, "exploration appended without changing existing task values");
Check(WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.CombatWorker, WorkerTaskKind.ExploreArea), "combat worker can explore independently");
Check(!WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.Farmer, WorkerTaskKind.ExploreArea), "farmer cannot explore");
Check(!WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.Forager, WorkerTaskKind.ExploreArea), "forager cannot explore");
Equal("Mine", WorkerExplorationAreaCatalog.Entrance(WorkerExplorationAreaCatalog.Mines), "exploration uses permanent mine entrance");
Equal("SkullCave", WorkerExplorationAreaCatalog.Entrance(WorkerExplorationAreaCatalog.SkullCavern), "exploration uses permanent cavern entrance");
Equal("IslandNorth", WorkerExplorationAreaCatalog.Entrance(WorkerExplorationAreaCatalog.Volcano), "volcano exploration stays outside dungeon floors");
Check(WorkerExplorationPolicy.UnavailableReason("Volcano", true, true, true, false, true)!.Contains("boat"), "debug island arrival cannot bypass boat unlock");
Check(WorkerExplorationPolicy.UnavailableReason("Volcano", true, true, true, true, false)!.Contains("north"), "volcano requires opening north island path");
Check(WorkerExplorationPolicy.UnavailableReason("SkullCavern", true, false, true, true, true)!.Contains("bus"), "cavern exploration requires bus access");
Check(WorkerExplorationPolicy.UnavailableReason("SkullCavern", true, true, false, true, true)!.Contains("Key"), "cavern exploration requires skull key");
Check(WorkerExplorationPolicy.UnavailableReason("Mines", false, true, true, true, true) is not null, "early-game mine landslide blocks exploration");
Check(WorkerExplorationPolicy.UnavailableReason("Volcano", true, true, true, true, true) is null, "unlocked volcano can be explored");

var expedition = new WorkerRosterEntry { WorkerId = "explorer", Profession = WorkerProfession.CombatWorker, AssignedTask = WorkerTaskKind.ExploreArea };
var progress = expedition.Exploration;
Check(WorkerExplorationPolicy.ObserveClock(progress, 100, 600, false), "first arrival establishes time baseline");
Equal(0, progress.Minutes, "no immediate arrival reward");
WorkerExplorationPolicy.ObserveClock(progress, 100, 610, true);
Equal(10, progress.Minutes, "ten minutes of active exploration accrue");
Check(!WorkerExplorationPolicy.ObserveClock(progress, 100, 610, true), "repeated ticks do not add progress");
WorkerExplorationPolicy.ObserveClock(progress, 100, 650, true);
Equal(50, progress.Minutes, "HHMM minutes are converted without inventing forty minutes at hour boundary");
WorkerExplorationPolicy.ObserveClock(progress, 100, 700, true);
Equal(60, progress.Minutes, "one hour is ready for one batch");
var staleProgress = progress.Clone();
var initialLoot = new List<WorkerExplorationLoot> { new() { ItemId = "(O)766", Stack = 2 } };
Check(WorkerExplorationPolicy.TryQueueRun(expedition, progress, initialLoot, 100, true, true, true, false), "host commits completed run and pending loot");
Equal(1, expedition.Exploration.CompletedRuns, "one run completed");
Equal(0, expedition.Exploration.Minutes, "completed run consumes its hour");
Equal(0, expedition.Experience.Mining, "monster exploration never simulates mining or awards mining XP");
Equal(5, expedition.Experience.Combat, "explorer gets five combat XP without a player kill");
Check(!WorkerExplorationPolicy.TryQueueRun(expedition, staleProgress, initialLoot, 100, true, true, true, false), "stale completion cannot duplicate loot or XP");
initialLoot[0].Stack = 99;
Equal(2, expedition.Exploration.PendingLoot[0].Stack, "saved rewards do not alias rolled loot");
progress = expedition.Exploration.Clone();
WorkerExplorationPolicy.ObserveClock(progress, 100, 720, false);
Equal(0, progress.Minutes, "paused or unpaid time grants no progress");
WorkerExplorationPolicy.ObserveClock(progress, 100, 730, true);
Equal(10, progress.Minutes, "only newly active time progresses");
WorkerExplorationPolicy.ObserveClock(progress, 100, 700, true);
WorkerExplorationPolicy.ObserveClock(progress, 100, 730, true);
Equal(10, progress.Minutes, "rewinding time cannot re-credit an interval");
WorkerExplorationPolicy.ObserveClock(progress, 100, 2400, false);
Equal(1320, progress.LastObservedMinute, "nighttime is capped at the 22:00 work cutoff");
WorkerExplorationPolicy.ObserveClock(progress, 101, 600, false);
Equal(0, progress.CompletedRuns, "new day resets the run count");
Equal(0, progress.Minutes, "unfinished previous-day progress does not carry over");
Equal(1, progress.PendingLoot.Count, "new day preserves undelivered completed rewards");
progress.Minutes = 60;
expedition.Exploration = progress.Clone();
Check(!WorkerExplorationPolicy.TryQueueRun(expedition, progress, initialLoot, 101, false, true, true, false), "clients cannot simulate reward batches");
Check(!WorkerExplorationPolicy.TryQueueRun(expedition, progress, initialLoot, 101, true, false, true, false), "title-screen simulation cannot award");
Check(!WorkerExplorationPolicy.TryQueueRun(expedition, progress, initialLoot, 101, true, true, false, false), "unpaid workers cannot earn simulated loot");
Check(!WorkerExplorationPolicy.TryQueueRun(expedition, progress, initialLoot, 101, true, true, true, true), "defeated workers cannot explore");
expedition.AssignedTask = WorkerTaskKind.Idle;
Check(!WorkerExplorationPolicy.TryQueueRun(expedition, progress, initialLoot, 101, true, true, true, false), "Idle cancels pending completion");
expedition.AssignedTask = WorkerTaskKind.ExploreArea;
progress.CompletedRuns = WorkerExplorationPolicy.MaximumRunsPerDay;
expedition.Exploration = progress.Clone();
Check(!WorkerExplorationPolicy.TryQueueRun(expedition, progress, initialLoot, 101, true, true, true, false), "daily run limit prevents runaway clock rewards");

var areaLootIds = new Dictionary<string, HashSet<string>>
{
    ["Mines"] = new() { "766", "684", "767", "768", "769", "382", "378", "380", "286", "717", "86", "157", "273", "203", "96", "97", "98", "99", "105", "114", "336", "74" },
    ["SkullCavern"] = new() { "766", "767", "768", "769", "386", "382", "428", "226", "287", "749", "732", "107", "580", "583", "584", "72", "337", "99", "485", "74" },
    ["Volcano"] = new() { "848", "881", "768", "766", "378", "380", "382", "851", "831", "833", "829", "852", "60", "62", "64", "66", "68", "70", "72", "835", "857", "98", "99" },
};
var forbiddenExplorationIds = new[] { "390", "388", "92", "384", "535", "536", "537", "73", "890", "875", "876", "930" };
var mineCoreIds = new HashSet<string> { "766", "684", "767", "768", "769" };
var mineRareIds = new HashSet<string> { "96", "97", "98", "99", "105", "114", "336" };
foreach (var scenario in new[] { ("Mines", 0, false), ("Mines", 30, false), ("Mines", 31, false),
    ("Mines", 40, false), ("Mines", 50, false), ("Mines", 51, false), ("Mines", 79, false),
    ("Mines", 80, false), ("Mines", 119, false), ("Mines", 120, false),
    ("SkullCavern", 119, false), ("SkullCavern", 120, false), ("Volcano", 120, false), ("Volcano", 120, true) })
{
    var (area, depth, hutch) = scenario;
    int commonUnits = 0, totalUnits = 0, rareEvents = 0, shards = 0, eggs = 0, solar = 0, voids = 0;
    const int samples = 100000;
    var random = new Random(9467);
    for (int run = 0; run < samples; run++)
    {
        var loot = WorkerExplorationPolicy.RollLoot(area, random, depth, hutch);
        Check(loot.Count > 0 && loot.All(drop => drop.Area == area && areaLootIds[area].Contains(drop.ItemId[3..])
            && WorkerExplorationPolicy.IsAllowedMonsterLoot(area, drop.ItemId) && drop.Stack is >= 1 and <= 5), "only capped source-backed monster drops from the selected area");
        Check(!loot.Any(drop => forbiddenExplorationIds.Contains(drop.ItemId[3..])), "stone and excluded mining/quest rewards never roll");
        shards += loot.Count(drop => drop.ItemId == "(O)74");
        eggs += loot.Count(drop => drop.ItemId == "(O)857");
        if (area != "Mines") continue;
        var core = loot.Where(drop => mineCoreIds.Contains(drop.ItemId[3..])).ToList();
        Check(core.Count == 1 && core[0].Stack is >= 3 and <= 5, "every hourly run guarantees three to five core monster units");
        commonUnits += core.Sum(drop => drop.Stack);
        totalUnits += loot.Sum(drop => drop.Stack);
        rareEvents += loot.Count(drop => mineRareIds.Contains(drop.ItemId[3..]));
        solar += loot.Count(drop => drop.ItemId == "(O)768");
        voids += loot.Count(drop => drop.ItemId == "(O)769");
        Check(depth >= 31 || !loot.Any(drop => drop.ItemId is "(O)767" or "(O)382"), "pre-Golem/Bat progression excludes their coal and wings");
        Check(depth >= 40 || !loot.Any(drop => drop.ItemId is "(O)97" or "(O)336"), "Dust Spirit extras wait for the frozen band");
        Check(depth >= 80 || !loot.Any(drop => drop.ItemId is "(O)378" or "(O)380" or "(O)203" or "(O)98"), "Metal Head/Shadow Brute and conservative Scroll III gates");
        Check(depth >= 120 || !loot.Any(drop => drop.ItemId is "(O)99"), "conservative worker Scroll IV gate waits for mine bottom");
    }
    if (area == "Mines")
    {
        double commonShare = (double)commonUnits / totalUnits;
        Check(commonShare is > .795 and < .805, "Mines container units average approximately eighty percent core and twenty percent extras at every progression band");
        Check(rareEvents is > 800 and < 1120, "rare tier remains near 0.96 percent hourly rather than filling an extra-loot quota");
        Check(depth < 51 ? solar == 0 : solar > 0, "solar essence starts at actual Ghost floor 51");
        Check(depth < 80 ? voids == 0 : voids > 0, "void essence starts with Shadow monsters");
    }
    if (area != "Volcano")
        Check(depth < 120 ? shards == 0 : area == "Mines" ? shards is > 10 and < 75 : shards is > 50 and < 155,
            "prismatic hourly rarity and bottom gate preserve conservative worker progression");
    else
    {
        Equal(0, shards, "Volcano does not inherit global mine-monster prismatic rewards");
        Check(!hutch ? eggs == 0 : eggs is > 200 and < 400, "Tiger egg requires the host's slime hutch progression and remains rare");
    }
}
foreach (string area in WorkerExplorationAreaCatalog.GetAreas())
    foreach (string id in forbiddenExplorationIds)
        Check(!WorkerExplorationPolicy.IsAllowedMonsterLoot(area, "(O)" + id), "queued validation rejects stone and items outside the curated monster subset");
Check(WorkerExplorationPolicy.IsAllowedMonsterLoot("Mines", "(O)382"), "Dust Sprite coal remains legitimate monster loot");
Check(WorkerExplorationPolicy.IsAllowedMonsterLoot("Mines", "(O)380"), "Metal Head iron remains legitimate monster loot");
Check(WorkerExplorationPolicy.IsAllowedMonsterLoot("SkullCavern", "(O)386"), "Iridium Crab/slime ore remains legitimate monster loot");
Check(WorkerExplorationPolicy.IsAllowedMonsterLoot("SkullCavern", "(O)749"), "Carbon Ghost omni geodes are genuine monster drops");
Check(WorkerExplorationPolicy.IsAllowedMonsterLoot("Volcano", "(O)851"), "False Magma Cap drops remain legitimate monster loot");
Check(WorkerExplorationPolicy.IsAllowedMonsterLoot("Volcano", "(O)378"), "Hot Head copper remains legitimate monster loot");
Check(!WorkerExplorationPolicy.IsAllowedMonsterLoot("Volcano", "390"), "unqualified stone is also forbidden");
var legacyQueue = new WorkerExplorationProgress { PendingLoot = new() {
    new() { ItemId = "(O)684", Stack = 1 }, new() { ItemId = "(O)749", Stack = 1 },
    new() { Area = "Volcano", ItemId = "(O)852", Stack = 1 } } };
WorkerExplorationPolicy.TagLegacyPendingLoot(legacyQueue, "Mines");
Check(WorkerExplorationPolicy.IsAllowedPendingLoot(legacyQueue.PendingLoot[0]), "legacy source defaults to its saved destination");
Check(!WorkerExplorationPolicy.IsAllowedPendingLoot(legacyQueue.PendingLoot[1]), "legacy Mines queue cannot borrow Skull-only Carbon Ghost rewards");
Equal("Volcano", legacyQueue.PendingLoot[2].Area, "migration never retags known source areas after a destination change");
Check(!WorkerExplorationPolicy.IsAllowedPendingLoot(new() { Area = "Mines", ItemId = "(O)390", Stack = 1 }), "old queued stone cannot be delivered");
Check(WorkerExplorationPolicy.IsAllowedPendingLoot(new() { Area = "Mines", ItemId = "(O)684", Stack = 1 }), "saved loot retains its source area after changing destinations");
Check(!WorkerExplorationPolicy.IsAllowedPendingLoot(new() { Area = "Volcano", ItemId = "(O)684", Stack = 1 }), "invalid cross-area loot is rejected");
var rejectedRun = new WorkerRosterEntry { Profession = WorkerProfession.CombatWorker, AssignedTask = WorkerTaskKind.ExploreArea,
    Exploration = new() { Day = 55, Minutes = 60 } };
Check(!WorkerExplorationPolicy.TryQueueRun(rejectedRun, rejectedRun.Exploration.Clone(),
    new() { new() { ItemId = "(O)390", Stack = 1 } }, 55, true, true, true, false), "a prohibited reward cannot commit a run or XP");
Equal(0, rejectedRun.Experience.Combat, "prohibited loot rejection is side-effect free");
expedition.ExplorationArea = WorkerExplorationAreaCatalog.Volcano;
var reloadedExpedition = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(expedition))!;
Equal(WorkerExplorationAreaCatalog.Volcano, reloadedExpedition.ExplorationArea, "independent exploration selection survives save/load");
Equal(60, reloadedExpedition.Exploration.Minutes, "simulation progress survives save/load");
Equal(1, reloadedExpedition.Exploration.PendingLoot.Count, "undelivered rewards survive save/load");
Equal(5, reloadedExpedition.Experience.Combat, "failed completions never grant extra XP");
var expeditionClone = reloadedExpedition.Clone();
expeditionClone.Exploration.PendingLoot[0].Stack = 12;
Equal(2, reloadedExpedition.Exploration.PendingLoot[0].Stack, "pending loot clone is independent");
Check(WorkerCombatAreaCatalog.IsValid(WorkerCombatAreaCatalog.IslandFarm), "island farm allowed");
Check(WorkerCombatAreaCatalog.IsValid(WorkerCombatAreaCatalog.VolcanoEntrance), "volcano entrance area allowed");
Check(!WorkerCombatAreaCatalog.IsValid("VolcanoDungeon"), "volcano dungeon not offered");
Check(!WorkerCombatPolicy.ShouldBeginReturn(2150), "combat can work before return buffer");
Check(WorkerCombatPolicy.ShouldBeginReturn(2200), "return starts before midnight");
Check(!WorkerCombatPolicy.MustBeHomeNow(2350), "deadline is not 11:50 PM");
Check(WorkerCombatPolicy.MustBeHomeNow(2400), "midnight is home deadline");
Check(WorkerCombatPolicy.IsMatchingMineFloor(WorkerCombatAreaCatalog.Mines, 1), "regular mine floor follows");
Check(!WorkerCombatPolicy.IsMatchingMineFloor(WorkerCombatAreaCatalog.Mines, 121), "Skull Cavern not regular mine");
Check(WorkerCombatPolicy.IsMatchingMineFloor(WorkerCombatAreaCatalog.SkullCavern, 121), "Skull Cavern floor follows");
Check(!WorkerCombatPolicy.IsMatchingMineFloor(WorkerCombatAreaCatalog.SkullCavern, 77377), "quarry side branch excluded");
Check(!WorkerCombatPolicy.ShouldFollowOnHostWarp(false, false, true), "worker first stages in entrance cave");
Check(WorkerCombatPolicy.ShouldFollowOnHostWarp(true, false, true), "staged worker follows later host floor warp");
Check(WorkerCombatPolicy.ShouldFollowOnHostWarp(false, true, true), "worker follows host floor transition");
Check(!WorkerCombatPolicy.ShouldFollowOnHostWarp(true, false, false), "unmatched floor does not pull worker in");
Check(!WorkerShellLifecyclePolicy.ShouldRecoverCachedWorker(true, true, true), "worker attached to active generated floor remains available for floor transition");
Check(WorkerShellLifecyclePolicy.ShouldRecoverCachedWorker(true, false, true), "worker on unloaded generated floor must be recovered");
Check(WorkerShellLifecyclePolicy.ShouldRecoverCachedWorker(true, true, false), "worker removed from an active floor must be recovered");
Check(!WorkerShellLifecyclePolicy.ShouldRecoverCachedWorker(false, false, true), "ordinary attached worker is retained");
Equal(0, WorkerCombatPolicy.HealthAfterContact(3, 4), "contact damage depletes HP");
Equal(9, WorkerCombatPolicy.HealthAfterContact(10, 0), "contact damage has minimum one");
Equal(100, WorkerCombatPolicy.MaxHealth, "worker base maximum is 100 HP");
Equal(100, WorkerCombatPolicy.ClampHealth(200), "combat health cannot exceed 100");
Equal(0, WorkerCombatPolicy.ClampHealth(-2), "negative combat health clamps to zero");
Equal(99, WorkerCombatPolicy.HealthAfterContact(200, 1), "contact clamps legacy overfull HP before damage");
Equal(100, WorkerCombatPolicy.NormalizeHealth(200, 200), "legacy full health becomes 100");
Equal(75, WorkerCombatPolicy.NormalizeHealth(150, 200), "legacy missing health retains its fraction");
Equal(37, WorkerCombatPolicy.NormalizeHealth(73, 200), "legacy partial health rounds up safely");
Equal(1, WorkerCombatPolicy.NormalizeHealth(1, 200), "living worker is not killed by migration rounding");
Equal(0, WorkerCombatPolicy.NormalizeHealth(0, 200), "defeated worker remains at zero");
Equal(100, WorkerCombatPolicy.NormalizeHealth(300, 200), "legacy overfull save clamps to 100");
Equal(100, WorkerCombatPolicy.NormalizeHealth(200, 0), "invalid legacy maximum cannot exceed 100");
first.CombatHealth = 73;
first.CombatMaxHealth = 200;
first.LastCombatHealthDay = 4;
WorkerRosterEntry persistedCombatHealth = first.Clone();
Equal(73, persistedCombatHealth.CombatHealth, "worker current HP survives roster cloning");
Equal(200, persistedCombatHealth.CombatMaxHealth, "worker maximum HP survives roster cloning");
Equal(4, persistedCombatHealth.LastCombatHealthDay, "worker HP day survives roster cloning");
Check(WorkerCombatPolicy.ShouldAggroWorker(16, 25), "nearer worker attracts monster");
Check(!WorkerCombatPolicy.ShouldAggroWorker(25, 16), "closer farmer keeps monster attention");
Check(!WorkerCombatPolicy.ShouldAggroWorker(81, int.MaxValue), "distant worker does not attract monster");
Check(!WorkerCombatPolicy.IsUsefulPatrolOffset(0, 0), "patrol does not choose current tile");
Check(!WorkerCombatPolicy.IsUsefulPatrolOffset(1, 1), "patrol step is visibly longer than one tile");
Check(WorkerCombatPolicy.IsUsefulPatrolOffset(4, -2), "nearby bounded patrol offset allowed");
Check(!WorkerCombatPolicy.IsUsefulPatrolOffset(9, 0), "patrol does not range too far");
Check(WorkerCombatTilePolicy.IsSafeTile(true, true, true, true), "mapped passable cave floor may be used");
Check(!WorkerCombatTilePolicy.IsSafeTile(true, false, true, true), "blank map border is not a combat route tile");
Check(!WorkerCombatTilePolicy.IsSafeTile(true, true, false, true), "building wall is not a combat route tile");
Check(!WorkerCombatTilePolicy.IsSafeTile(true, true, true, false), "occupied or collision-blocked tile is rejected");
Check(WorkerCombatTilePolicy.IsSafeRouteStep(true, true, true, false, false),
    "a map-passable grounded mine step stays valid even if a moving actor occupies it");
Check(!WorkerCombatTilePolicy.IsSafeRouteStep(false, true, true, false, false), "route cannot leave the map");
Check(!WorkerCombatTilePolicy.IsSafeRouteStep(true, false, true, false, false), "route cannot cross blank ground");
Check(!WorkerCombatTilePolicy.IsSafeRouteStep(true, true, false, false, false), "route cannot cross a solid wall");
Check(!WorkerCombatTilePolicy.IsSafeRouteStep(true, true, true, true, false), "route respects NPC barriers");
Check(!WorkerCombatTilePolicy.IsSafeRouteStep(true, true, true, false, true), "route cannot activate a warp");
Check(!WorkerCombatTargetPolicy.IsRevealedCrab(false),
    "stationary stone crab is ignored");
Check(WorkerCombatTargetPolicy.IsRevealedCrab(true),
    "moving stone crab can be targeted");
Check(!WorkerCombatTargetPolicy.CanTargetBug(true), "armored bug is not a worker target");
Check(WorkerCombatTargetPolicy.CanTargetBug(false), "unarmored bug can be a worker target");
Check(!WorkerCombatTargetPolicy.IsExposedDuggy(true, 8),
    "invisible Duggy cannot be targeted during retaliation");
Check(!WorkerCombatTargetPolicy.IsExposedDuggy(false, 4),
    "emerging Duggy cannot be targeted before its damaging frames");
Check(WorkerCombatTargetPolicy.IsExposedDuggy(false, 8),
    "exposed Duggy can be targeted");
Check(!WorkerCombatTargetPolicy.ShouldTriggerDuggy(false, 0, true, true),
    "visible Duggy is not retriggered");
Check(!WorkerCombatTargetPolicy.ShouldTriggerDuggy(true, 4, true, true),
    "emerging Duggy is not reset to its first frame");
Check(!WorkerCombatTargetPolicy.ShouldTriggerDuggy(true, 0, false, true),
    "distant worker does not trigger Duggy");
Check(!WorkerCombatTargetPolicy.ShouldTriggerDuggy(true, 0, true, false),
    "Duggy does not emerge on an invalid tile");
Check(WorkerCombatTargetPolicy.ShouldTriggerDuggy(true, 0, true, true),
    "nearby worker triggers an underground Duggy on valid ground");
object currentMonster = new();
object attackingMonster = new();
Check(ReferenceEquals(attackingMonster, WorkerCombatTargetPolicy.TargetAfterContact(
    currentMonster, attackingMonster, true)), "damage source takes priority over current target");
Check(ReferenceEquals(currentMonster, WorkerCombatTargetPolicy.TargetAfterContact(
    currentMonster, attackingMonster, false)), "hidden or untargetable attacker keeps current target");
Check(ReferenceEquals(currentMonster, WorkerCombatTargetPolicy.TargetAfterContact(
    currentMonster, currentMonster, true)), "current attacker remains the target");
Check(WorkerCombatTargetPolicy.ShouldOfferBlockedMonsterDialogue(true, true, true, true, true),
    "live unreachable monster can prompt dialogue");
Check(!WorkerCombatTargetPolicy.ShouldOfferBlockedMonsterDialogue(true, true, false, true, true),
    "dead monster cannot leave stale blocked dialogue");
Check(!WorkerCombatTargetPolicy.ShouldOfferBlockedMonsterDialogue(true, true, true, false, true),
    "unloaded monster cannot leave stale blocked dialogue");
Check(!WorkerCombatTargetPolicy.ShouldOfferBlockedMonsterDialogue(true, false, true, true, true),
    "restored route clears blocked dialogue");
Check(!WorkerCombatTargetPolicy.ShouldOfferBlockedMonsterDialogue(false, true, true, true, true),
    "idle order clears blocked dialogue");
Check(!WorkerCombatTargetPolicy.CanDamageCrabNow(false, false, false),
    "worker cannot bypass a disguised stone crab's shell immunity");
HashSet<(int, int)> isolatedLadderPockets = new() { (3, 2), (5, 2), (5, 3) };
Check(!WorkerCombatTilePolicy.TryChooseConnectedLanding(2, 2, 8,
    (x, y) => isolatedLadderPockets.Contains((x, y)), out _), "isolated ladder pocket with no escape is rejected");
HashSet<(int, int)> connectedFloor = new() { (3, 2), (4, 2), (5, 2), (5, 3) };
Check(WorkerCombatTilePolicy.TryChooseConnectedLanding(2, 2, 8,
    (x, y) => connectedFloor.Contains((x, y)), out var connectedLanding), "connected floor with an exit is accepted");
Equal((3, 2), connectedLanding, "landing stays beside the farmer on connected floor");
HashSet<(int, int)> separatedFloor = new() { (4, 2), (5, 2), (5, 3) };
Check(!WorkerCombatTilePolicy.TryChooseConnectedLanding(2, 2, 8,
    (x, y) => separatedFloor.Contains((x, y)), out _), "floor across a wall is not a landing");

HashSet<(int, int)> longHall = Enumerable.Range(1, 18).Select(x => (x, 2)).ToHashSet();
Check(WorkerCombatTilePolicy.TryChooseFloorLanding(22, 5, 1, 2,
    (x, y) => longHall.Contains((x, y)),
    (x, y) => longHall.Contains((x, y)) && x >= 14, out var farLanding),
    "floor search crosses safe route overlays to a landing beyond eight tiles");
Equal((14, 2), farLanding, "far landing uses nearest clear tile in farmer's component");
HashSet<(int, int)> distantRoom = new() { (15, 2), (16, 2), (17, 2), (17, 3) };
Check(WorkerCombatTilePolicy.TryChooseFloorLanding(20, 6, 2, 2,
    (x, y) => distantRoom.Contains((x, y)), (x, y) => distantRoom.Contains((x, y)), out var roomLanding),
    "no adjacent floor tile still allows a safe distant room on the generated floor");
Equal((15, 2), roomLanding, "fallback stays in the valid distant room");
Check(!WorkerCombatTilePolicy.TryChooseFloorLanding(8, 8, 2, 2,
    (x, y) => x == 5 && y == 5, (_, _) => true, out _),
    "single isolated passable tile is never a floor landing");
Check(!WorkerCombatTilePolicy.TryChooseFloorLanding(20, 6, 2, 2,
    (x, y) => distantRoom.Contains((x, y)), (_, _) => false, out _),
    "occupied room with no safe standing tile is rejected");
Check(!WorkerCombatTilePolicy.TryChooseFloorLanding(5, 5, 2, 2,
    (_, _) => false, (_, _) => true, out _), "blank/walled floor cannot produce a landing");
HashSet<(int, int)> rooms = new(distantRoom) { (4, 2), (5, 2), (6, 2) };
Check(WorkerCombatTilePolicy.TryChooseFloorLanding(20, 6, 2, 2,
    (x, y) => rooms.Contains((x, y)), (x, y) => rooms.Contains((x, y)), out var roomyLanding),
    "fallback selects a connected area with room to move");
Equal((15, 2), roomyLanding, "larger room is preferred over a tiny pocket beside the ladder");

Equal(0, WorkerCombatSwingPolicy.PoseAt(0), "swing begins in the windup pose");
Equal(1, WorkerCombatSwingPolicy.PoseAt(55), "second windup pose starts at 55 ms");
Equal(2, WorkerCombatSwingPolicy.PoseAt(100), "impact matches the visible third pose");
Equal(5, WorkerCombatSwingPolicy.PoseAt(250), "swing ends in recovery");
for (int facing = 0; facing < 4; facing++)
for (int pose = 0; pose < WorkerCombatSwingPolicy.PoseCount; pose++)
{
    int frame = WorkerCombatSwingPolicy.SheetFrame(facing, pose);
    Check(frame >= 16 && frame < 40, "all sword poses lie after walking frames and inside sheet bounds");
}
Equal(232, WorkerCombatSwingPolicy.SwordAnimation(2), "down uses the native sword animation ID");
Equal(240, WorkerCombatSwingPolicy.SwordAnimation(1), "right uses the native sword animation ID");
Equal(248, WorkerCombatSwingPolicy.SwordAnimation(0), "up uses the native sword animation ID");
Equal(256, WorkerCombatSwingPolicy.SwordAnimation(3), "left uses the native mirrored sword animation ID");
Equal(0, WorkerCombatSwingPolicy.FacingTowards(96, 100, 96, 90, 2), "enemy above within the same tile starts an upward swing even when worker was facing down");
Equal(2, WorkerCombatSwingPolicy.FacingTowards(96, 100, 96, 110, 0), "enemy below preserves downward sword attacks");
Equal(1, WorkerCombatSwingPolicy.FacingTowards(96, 100, 106, 101, 2), "enemy predominantly right starts a right swing");
Equal(3, WorkerCombatSwingPolicy.FacingTowards(96, 100, 86, 101, 2), "enemy predominantly left starts a left swing");
Equal(0, WorkerCombatSwingPolicy.FacingTowards(96, 100, 99, 90, 2), "enemy above with small horizontal offset still gets an upward swing");
Equal(3, WorkerCombatSwingPolicy.FacingTowards(96, 100, 96, 100, 3), "exactly overlapping centers retain the prior facing");
Equal(2, WorkerCombatSwingPolicy.PoseAt(124), "first slash pose lasts the native 25 ms");
Equal(3, WorkerCombatSwingPolicy.PoseAt(125), "second slash pose follows immediately");
Equal(4, WorkerCombatSwingPolicy.PoseAt(150), "third slash pose follows at 150 ms");
Equal(5, WorkerCombatSwingPolicy.PoseAt(175), "sword recovery starts after the native slash poses");
Equal(305d, WorkerCombatSwingPolicy.DurationMilliseconds, "native Rusty Sword swing and recovery duration");
bool Impact(double elapsed, bool applied = false, bool sameLocation = true, bool alive = true,
    bool present = true, bool inReach = true, bool revealed = true, bool invincible = false)
    => WorkerCombatSwingPolicy.CanApplyImpact(applied, elapsed, sameLocation, alive, present, inReach, revealed, invincible);
Check(!Impact(99), "windup cannot deal damage");
Check(Impact(100), "valid target takes damage at impact");
Check(!Impact(140, applied: true), "swing cannot damage twice");
Check(!Impact(100, sameLocation: false), "floor transition cancels the hit");
Check(!Impact(100, alive: false), "dead target cannot be hit again");
Check(!Impact(100, present: false), "removed target cannot be hit");
Check(!Impact(100, inReach: false), "target moving away during windup is not hit remotely");
Check(!Impact(100, revealed: false), "hidden/reburrowed target is revalidated at impact");
Check(!Impact(100, invincible: true), "invincible target does not take a delayed hit");
Check(!WorkerExperiencePolicy.TryAwardCompletedAction(first, true, true, WorkerExperienceAction.None), "route clearance gives no award");
Check(!WorkerExperiencePolicy.TryAwardCompletedAction(first, false, true, WorkerExperienceAction.ChopTree), "client cannot award");
Check(!WorkerExperiencePolicy.TryAwardCompletedAction(first, true, false, WorkerExperienceAction.ClearStone), "unloaded world cannot award");
Check(!WorkerExperiencePolicy.TryAwardFarmingHarvest(first, true, true, false, 1), "watering or failed attempt rejected");
Check(!WorkerExperiencePolicy.TryAwardFarmingHarvest(first, true, true, true, 0), "no produce rejected");
Check(!WorkerExperiencePolicy.TryAwardFarmingHarvest(first, false, true, true, 1), "farmhand rejected");
Check(!WorkerExperiencePolicy.TryAwardFarmingHarvest(first, true, false, true, 1), "unloaded world rejected");
Equal(WorkerExperiencePolicy.FarmingExperiencePerCropHarvest, first.Experience.Farming, "no duplicate award");

int[] thresholds = { 0, 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000 };
for (int level = 1; level <= WorkerExperiencePolicy.MaximumLevel; level++)
{
    Equal(level - 1, WorkerExperiencePolicy.GetFarmingLevel(thresholds[level] - 1), $"below level {level}");
    Equal(level, WorkerExperiencePolicy.GetFarmingLevel(thresholds[level]), $"at level {level}");
}
Equal(10, WorkerExperiencePolicy.GetFarmingLevel(int.MaxValue), "level cap");
first.Experience.Farming = int.MaxValue - 1;
Check(WorkerExperiencePolicy.TryAwardFarmingHarvest(first, true, true, true, 1), "high XP awards safely");
Equal(int.MaxValue, first.Experience.Farming, "XP saturates");

first.Experience.Farming = 380;
first.Experience.Mining = 11;
first.Experience.Fishing = 12;
first.Experience.Foraging = 13;
first.Experience.Combat = 14;
first.Profession = WorkerProfession.CombatWorker;
first.CombatArea = WorkerCombatAreaCatalog.IslandFarm;
first.LastDefeatedDay = 42;
var save = new WorkerRosterSaveData { Workers = new() { first, second } };
var loaded = JsonSerializer.Deserialize<WorkerRosterSaveData>(JsonSerializer.Serialize(save))!;
Equal("Farm", loaded.Workers[0].HarvestDestination?.LocationName, "worker destination round trip");
Equal(5, loaded.Workers[0].HarvestDestination?.TileX, "worker destination X round trip");
Check(loaded.Workers[1].HarvestDestination is null, "unassigned worker keeps shipping-bin default");
var legacyRoster = JsonSerializer.Deserialize<WorkerRosterSaveData>("""
    {"SchemaVersion":2,"HarvestDestination":{"LocationName":"Farm","TileX":5,"TileY":7},
     "Workers":[{"WorkerId":"one"},{"WorkerId":"two"}]}
    """)!;
WorkerRosterMigration.ApplySharedHarvestDestination(legacyRoster);
Check(legacyRoster.Workers.All(worker => worker.HarvestDestination?.LocationName == "Farm"),
    "legacy shared chest choice migrates to every worker");
// Combat 1.3.1 also wrote schema 3 with the shared chest field.
legacyRoster.SchemaVersion = 3;
legacyRoster.Workers[0].HarvestDestination = null;
legacyRoster.Workers[1].HarvestDestination = new() { LocationName = "Forest", TileX = 9, TileY = 8 };
WorkerRosterMigration.ApplySharedHarvestDestination(legacyRoster);
Equal("Farm", legacyRoster.Workers[0].HarvestDestination!.LocationName, "combat schema-three shared choice migrates");
Equal("Forest", legacyRoster.Workers[1].HarvestDestination!.LocationName, "existing individual chest survives branch integration");
legacyRoster.Workers[0].HarvestDestination!.TileX = 77;
Equal(5, legacyRoster.HarvestDestination!.TileX, "migration clones the shared destination");
Equal(9, legacyRoster.Workers[1].HarvestDestination!.TileX, "migration never aliases another worker's chest");
legacyRoster.SchemaVersion = 4;
legacyRoster.Workers[0].HarvestDestination = null;
WorkerRosterMigration.ApplySharedHarvestDestination(legacyRoster);
Check(legacyRoster.Workers[0].HarvestDestination is null, "schema-four shipping choice is not replaced by a stale global chest");
Equal(380, loaded.Workers[0].Experience.Farming, "saved XP round trip");
Equal(11, loaded.Workers[0].Experience.Mining, "mining XP round trip");
Equal(12, loaded.Workers[0].Experience.Fishing, "fishing XP round trip");
Equal(13, loaded.Workers[0].Experience.Foraging, "foraging XP round trip");
Equal(14, loaded.Workers[0].Experience.Combat, "combat XP round trip");
Equal(WorkerProfession.CombatWorker, loaded.Workers[0].Profession, "combat profession round trip");
Equal(WorkerCombatAreaCatalog.IslandFarm, loaded.Workers[0].CombatArea, "combat area round trip");
Equal(42, loaded.Workers[0].LastDefeatedDay, "defeat day survives save");
Equal(0, loaded.Workers[1].Experience.Farming, "independent saved XP");
var clone = loaded.Workers[0].Clone();
clone.HarvestDestination!.TileX = 99;
Equal(5, loaded.Workers[0].HarvestDestination!.TileX, "worker destination clone does not alias another worker");
clone.Experience.Farming = 500;
clone.Experience.Mining = 500;
Equal(380, loaded.Workers[0].Experience.Farming, "clone does not alias XP");
Equal(11, loaded.Workers[0].Experience.Mining, "clone does not alias other skills");
var legacy = JsonSerializer.Deserialize<WorkerRosterEntry>("{\"WorkerId\":\"legacy\"}")!;
Equal(0, legacy.Experience.Farming, "legacy missing XP defaults zero");
Equal(0, legacy.Experience.Mining, "legacy mining defaults zero");
Equal(0, legacy.Experience.Fishing, "legacy fishing defaults zero");
Equal(0, legacy.Experience.Foraging, "legacy foraging defaults zero");
Equal(0, legacy.Experience.Combat, "legacy combat defaults zero");
Equal(WorkerCombatAreaCatalog.Farm, legacy.CombatArea, "legacy combat area defaults to farm");
Equal(WorkerExplorationAreaCatalog.Mines, legacy.ExplorationArea, "legacy exploration area defaults to Mines");
Equal(0, legacy.Exploration.PendingLoot.Count, "legacy workers have no synthetic pending rewards");
Equal(-1, legacy.LastDefeatedDay, "legacy worker not defeated");
var corrupt = JsonSerializer.Deserialize<WorkerRosterEntry>("{\"WorkerId\":\"old\",\"Experience\":{\"Farming\":-4}}")!;
Equal(0, corrupt.Clone().Experience.Farming, "negative XP normalized on clone");
var missing = JsonSerializer.Deserialize<WorkerRosterEntry>("{\"WorkerId\":\"null\",\"Experience\":null}")!;
Equal(0, missing.Clone().Experience.Farming, "null XP normalized on clone");
Console.WriteLine("Worker experience tests passed.");

// Direction-specific native tool/hand poses retain their own frame ranges.
var overheadAnimations = new[] { 176, 168, 160, 184 };
var waterAnimations = new[] { 180, 172, 164, 188 };
var gatherAnimations = new[] { 279, 280, 281, 282 };
var workSheetFrames = new HashSet<int>();
foreach (WorkerWorkAnimationKind kind in new[] { WorkerWorkAnimationKind.Water, WorkerWorkAnimationKind.Gather, WorkerWorkAnimationKind.Axe })
    for (int facing = 0; facing < 4; facing++)
    {
        int expectedAnimation = kind == WorkerWorkAnimationKind.Water ? waterAnimations[facing]
            : kind == WorkerWorkAnimationKind.Gather ? gatherAnimations[facing] : overheadAnimations[facing];
        Equal(expectedAnimation, WorkerWorkAnimationPolicy.NativeAnimation(kind, facing), "native work animation matches actual facing");
        for (int pose = 0; pose < WorkerWorkAnimationPolicy.FramesPerDirection; pose++)
        {
            int frame = WorkerWorkAnimationPolicy.SheetFrame(kind, facing, pose);
            Check(frame >= 40 && frame < 100 && workSheetFrames.Add(frame), "work sheets never overlap another direction or existing walk/combat frames");
        }
    }
Equal(60, workSheetFrames.Count, "all water/gather/tool directions are generated");
foreach (WorkerWorkAnimationKind kind in Enum.GetValues<WorkerWorkAnimationKind>())
{
    double impact = WorkerWorkAnimationPolicy.ImpactMilliseconds(kind);
    Check(!WorkerWorkAnimationPolicy.CanApplyImpact(false, kind, impact - .01, true, true, true), "tool changes nothing before its impact pose");
    Check(WorkerWorkAnimationPolicy.CanApplyImpact(false, kind, impact, true, true, true), "host can apply exactly one action at impact");
    Check(!WorkerWorkAnimationPolicy.CanApplyImpact(true, kind, 400, true, true, true), "completed impact cannot apply another tool action");
    Check(!WorkerWorkAnimationPolicy.CanApplyImpact(false, kind, 400, false, true, true), "clients only render and cannot execute work");
    Check(!WorkerWorkAnimationPolicy.CanApplyImpact(false, kind, 400, true, false, true), "title-screen action is forbidden");
    Check(!WorkerWorkAnimationPolicy.CanApplyImpact(false, kind, 400, true, true, false), "cancelled or unavailable target cannot execute");
    Equal(0, WorkerWorkAnimationPolicy.PoseAt(kind, -1), "early pose clamps safely");
    Equal(WorkerWorkAnimationPolicy.PoseCount(kind) - 1, WorkerWorkAnimationPolicy.PoseAt(kind, 999), "late pose never walks outside generated sheet");
    for (int facing = 0; facing < 4; facing++)
        if (kind == WorkerWorkAnimationKind.Scythe)
            {
            Equal(WorkerCombatSwingPolicy.SwordAnimation(facing), WorkerWorkAnimationPolicy.NativeAnimation(kind, facing), "scythe shares sword body metadata");
            for (int pose = 0; pose < WorkerCombatSwingPolicy.PoseCount; pose++)
                Equal(WorkerCombatSwingPolicy.SheetFrame(facing, pose), WorkerWorkAnimationPolicy.SheetFrame(kind, facing, pose), "scythe uses exact sword sprites in every direction");
        }
        else if (kind == WorkerWorkAnimationKind.Pickaxe)
            Equal(overheadAnimations[facing], WorkerWorkAnimationPolicy.NativeAnimation(kind, facing), "pickaxe retains overhead direction independently of walking/swords");
}
Console.WriteLine("Worker work-animation policy tests passed.");

// Additive mining fields must not change old numeric assignments or chest migration.
Equal(0, (int)WorkerProfession.Farmer, "legacy Farmer profession remains stable");
Equal(1, (int)WorkerProfession.Forager, "legacy Forager profession remains stable");
Equal(3, (int)WorkerProfession.Miner, "new Miner uses appended profession value");
Equal(9, (int)WorkerTaskKind.ExploreArea, "legacy Explore Area assignment remains stable");
Equal(10, (int)WorkerTaskKind.MineRocks, "new mining assignment is appended");
Check(WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.Miner, WorkerTaskKind.MineRocks), "Miner can mine");
Check(WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.Miner, WorkerTaskKind.Idle), "Miner can recall");
foreach (WorkerTaskKind task in Enum.GetValues<WorkerTaskKind>())
    if (!WorkerMiningPolicy.IsMiningTask(task) && task != WorkerTaskKind.Idle)
        Check(!WorkerTaskPolicy.IsTaskAllowed(WorkerProfession.Miner, task), "Miner cannot receive another profession's job");
foreach (WorkerProfession profession in new[] { WorkerProfession.Farmer, WorkerProfession.Forager, WorkerProfession.CombatWorker })
    Check(!WorkerTaskPolicy.IsTaskAllowed(profession, WorkerTaskKind.MineRocks), "mining cannot change existing profession contracts");
var legacyMinerInput = JsonSerializer.Deserialize<WorkerRosterEntry>("{\"WorkerId\":\"old\",\"AssignedTask\":3}")!;
Equal(WorkerProfession.Farmer, legacyMinerInput.Profession, "legacy worker still defaults to Farmer");
Equal(WorkerMiningPolicy.Quarry, legacyMinerInput.MiningArea, "absent mining area defaults to the first selectable area");
Equal(WorkerTaskKind.TendCrops, legacyMinerInput.AssignedTask, "old assignment survives additive mining fields");
var minerSave = new WorkerRosterEntry { WorkerId = "miner", Profession = WorkerProfession.Miner,
    AssignedTask = WorkerTaskKind.MineRocks, MiningArea = WorkerMiningPolicy.Quarry,
    HarvestDestination = first.HarvestDestination!.Clone() };
var minerRoundtrip = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(minerSave.Clone()))!;
Equal(WorkerProfession.Miner, minerRoundtrip.Profession, "miner profession clones and roundtrips");
Equal(WorkerTaskKind.MineRocks, minerRoundtrip.AssignedTask, "mining assignment roundtrips");
Equal(WorkerMiningPolicy.Quarry, minerRoundtrip.MiningArea, "per-worker quarry choice roundtrips");
minerRoundtrip.HarvestDestination!.TileX = 99;
Equal(5, minerSave.HarvestDestination!.TileX, "mining storage remains independent after cloning");
Check(WorkerMiningPolicy.ContainsTile(WorkerMiningPolicy.Quarry, 106, 13)
    && WorkerMiningPolicy.ContainsTile(WorkerMiningPolicy.Quarry, 127, 34), "quarry boundary stones are eligible");
foreach (var tile in new[] { (105, 13), (128, 13), (106, 12), (106, 35), (50, 20) })
    Check(!WorkerMiningPolicy.ContainsTile(WorkerMiningPolicy.Quarry, tile.Item1, tile.Item2), "quarry never selects other Mountain rocks");
Check(WorkerMiningPolicy.IsValid("Mines") && !WorkerMiningPolicy.IsValid("UndergroundMine1")
    && !WorkerMiningPolicy.IsValid(null), "saved mining choices name supported areas, never generated floors");
Console.WriteLine("Miner task, legacy save, independent storage, and quarry boundary policy checks passed.");

Equal(11, (int)WorkerTaskKind.MineOreGems, "ores task appends save enum values");
Equal(12, (int)WorkerTaskKind.FindLadder, "ladder task appends save enum values");
Check(WorkerMiningPolicy.Areas.SequenceEqual(new[] { "Quarry", "Mines", "SkullCavern", "Volcano" }), "mining area order matches the user's progression");
Check(WorkerTaskPolicy.GetTasks(WorkerProfession.Miner).SequenceEqual(new[] {
    WorkerTaskKind.MineOreGems, WorkerTaskKind.FindLadder, WorkerTaskKind.MineRocks, WorkerTaskKind.Idle }), "three mining jobs and recall appear in order");
foreach (WorkerProfession profession in new[] { WorkerProfession.Farmer, WorkerProfession.Forager, WorkerProfession.CombatWorker })
    foreach (WorkerTaskKind task in new[] { WorkerTaskKind.MineOreGems, WorkerTaskKind.FindLadder })
        Check(!WorkerTaskPolicy.IsTaskAllowed(profession, task), "new jobs don't broaden existing professions");
foreach (string area in WorkerMiningPolicy.Areas)
{
    minerSave.MiningArea = area;
    minerSave.AssignedTask = WorkerTaskKind.MineOreGems;
    var savedMiner = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(minerSave))!;
    Equal(area, savedMiner.MiningArea, "each mining destination persists independently");
    Equal(WorkerTaskKind.MineOreGems, savedMiner.AssignedTask, "new ore job persists");
}
var olderMiner = JsonSerializer.Deserialize<WorkerRosterEntry>("{\"Profession\":3,\"AssignedTask\":10,\"MiningArea\":\"Farm\"}")!;
Check(WorkerTaskPolicy.IsTaskAllowed(olderMiner.Profession, olderMiner.AssignedTask)
    && WorkerMiningPolicy.IsValid(olderMiner.MiningArea), "first mining implementation saves retain Farm/all-stone support");
foreach (string node in new[] { "751", "290", "764", "765", "95", "849", "850", "2", "4", "6", "8", "10", "12", "14", "44", "46",
    "75", "76", "77", "819", "843", "844", "BasicCoalNode0", "BasicCoalNode1", "VolcanoCoalNode0", "VolcanoCoalNode1", "VolcanoGoldNode" })
    Check(WorkerMiningPolicy.IsOreGemOrCoal(node) && WorkerMiningPolicy.CanTarget(WorkerMiningWorkMode.Ores, node), "mineral/coal nodes are included");
foreach (string node in new[] { "32", "38", "40", "42", "668", "670", "845", "846", "847", "343", "450", "816", "817", "818", "25" })
{
    Check(!WorkerMiningPolicy.CanTarget(WorkerMiningWorkMode.Ores, node), "ordinary rock/other resources are excluded from ore-first work");
    Check(WorkerMiningPolicy.CanTarget(WorkerMiningWorkMode.AllStone, node), "all-stone work still selects every breakable stone");
}
foreach (string gem in new[] { "80", "82", "84", "86" })
{
    Check(WorkerMiningPolicy.IsLooseGem(gem), "quartz and other loose mine crystals are recognized");
    Check(WorkerMiningPolicy.CanCollectLooseGem(WorkerMiningWorkMode.Ores, gem, false), "ore job gathers loose mine crystals");
    Check(!WorkerMiningPolicy.CanCollectLooseGem(WorkerMiningWorkMode.FindLadder, gem, false), "ladder search does not gather loose crystals");
    Check(!WorkerMiningPolicy.CanCollectLooseGem(WorkerMiningWorkMode.Ores, gem, true), "breakable nodes still use the pickaxe path");
}
Check(!WorkerMiningPolicy.IsLooseGem("78") && !WorkerMiningPolicy.IsLooseGem("81"), "unrelated ground objects are not miner gem targets");
Equal(WorkerMiningWorkMode.Ores, WorkerMiningPolicy.SelectMode(WorkerTaskKind.MineOreGems, true, true, false, false, true),
    "loose crystal presence keeps the ore job ahead of ladder search");
var crystalMiner = new WorkerRosterEntry { Profession = WorkerProfession.Miner, AssignedTask = WorkerTaskKind.MineOreGems };
Check(WorkerExperiencePolicy.TryAwardCompletedAction(crystalMiner, true, true, WorkerExperienceAction.PickupLooseGem),
    "gathering a mine crystal awards its miner");
Equal(WorkerExperiencePolicy.MiningExperiencePerLooseGem, crystalMiner.Experience.Mining, "loose crystal grants Mining XP");
Equal(0, crystalMiner.Experience.Foraging, "loose crystal does not grant Foraging XP");
Equal(WorkerMiningWorkMode.Ores, WorkerMiningPolicy.SelectMode(WorkerTaskKind.MineOreGems, true, true, true, false, true), "available ore takes priority even if an exit exists");
Equal(WorkerMiningWorkMode.FindLadder, WorkerMiningPolicy.SelectMode(WorkerTaskKind.MineOreGems, false, true, false, false, true), "exhausted ore falls back to finding the ladder");
Equal(WorkerMiningWorkMode.None, WorkerMiningPolicy.SelectMode(WorkerTaskKind.MineOreGems, false, true, true, false, true), "exhausted ore with an exit waits instead of breaking more rock");
Equal(WorkerMiningWorkMode.FindLadder, WorkerMiningPolicy.SelectMode(WorkerTaskKind.FindLadder, true, true, false, false, true), "ladder order works before ore exhaustion");
foreach (WorkerTaskKind task in new[] { WorkerTaskKind.FindLadder, WorkerTaskKind.MineOreGems })
{
    Equal(WorkerMiningWorkMode.None, WorkerMiningPolicy.SelectMode(task, false, false, false, false, false), "Quarry/Volcano never invent mine ladders");
    Equal(WorkerMiningWorkMode.None, WorkerMiningPolicy.SelectMode(task, false, true, true, false, true), "ladder or shaft stops further ladder search");
    Equal(WorkerMiningWorkMode.None, WorkerMiningPolicy.SelectMode(task, false, true, false, true, true), "monster-gated floor requires vanilla combat progression");
    Equal(WorkerMiningWorkMode.None, WorkerMiningPolicy.SelectMode(task, false, true, false, false, false), "terminal floors have no fabricated ladder");
}
Equal(WorkerMiningWorkMode.AllStone, WorkerMiningPolicy.SelectMode(WorkerTaskKind.MineRocks, false, true, true, true, false), "all-stone order continues on resource floors even when exit finding is unavailable");
foreach (string item in new[] { "(O)73", "(O)79", "(O)842" })
    Check(WorkerMiningPolicy.LeaveDropForPlayer(item), "walnuts and readable notes retain player pickup handling");
foreach (string item in new[] { "(O)390", "(O)382", "(O)378", "(O)380", "(O)384", "(O)386", "(O)72", "(O)848", "(O)535" })
    Check(!WorkerMiningPolicy.LeaveDropForPlayer(item), "normal mining loot reaches worker storage");
Console.WriteLine("Dungeon Miner progression checks passed: ore/coal selection, ladder fallback, exits, monster gates, Volcano, terminal floors, saves, and special drops.");
