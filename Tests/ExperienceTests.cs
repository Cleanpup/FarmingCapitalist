using System.Text.Json;
using FarmingCapitalist.Workers;

static void Equal<T>(T expected, T actual, string scenario)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{scenario}: expected {expected}, got {actual}");
}

static void Check(bool value, string scenario)
{
    if (!value) throw new Exception(scenario);
}

var first = new WorkerRosterEntry { WorkerId = "first" };
var second = new WorkerRosterEntry { WorkerId = "second" };
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
var initialLoot = new List<WorkerExplorationLoot> { new() { ItemId = "(O)378", Stack = 2 } };
Check(WorkerExplorationPolicy.TryQueueRun(expedition, progress, initialLoot, 100, true, true, true, false), "host commits completed run and pending loot");
Equal(1, expedition.Exploration.CompletedRuns, "one run completed");
Equal(0, expedition.Exploration.Minutes, "completed run consumes its hour");
Equal(5, expedition.Experience.Mining, "explorer gets five mining XP");
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
    ["Mines"] = new() { "390", "378", "380", "384", "684", "766", "767", "769", "535", "536", "537", "382" },
    ["SkullCavern"] = new() { "390", "384", "386", "768", "769", "749", "382" },
    ["Volcano"] = new() { "390", "848", "384", "768", "537", "382" },
};
foreach (string area in WorkerExplorationAreaCatalog.GetAreas())
for (int seed = 0; seed < 1000; seed++)
{
    var loot = WorkerExplorationPolicy.RollLoot(area, new Random(seed), 120);
    Check(loot.All(drop => areaLootIds[area].Contains(drop.ItemId[3..]) && drop.Stack is >= 1 and <= 4), "every rolled reward belongs to its area's capped pool");
    if (area == "Volcano") Check(loot.Any(drop => drop.ItemId == "(O)848"), "volcano batches always include cinder shards");
    if (area == "SkullCavern") Check(loot.Any(drop => drop.ItemId == "(O)384"), "cavern batches include gold ore");
    var shallowLoot = WorkerExplorationPolicy.RollLoot("Mines", new Random(seed), 39);
    Check(shallowLoot.Any(drop => drop.ItemId == "(O)378") && !shallowLoot.Any(drop => drop.ItemId is "(O)380" or "(O)384" or "(O)386"), "shallow unlocked Mines give copper rather than advanced ores");
}
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
Equal(100, WorkerCombatPolicy.MaxHealth, "combat worker maximum is fixed at 100 HP");
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
