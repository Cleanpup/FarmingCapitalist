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
Equal(380, loaded.Workers[0].Experience.Farming, "saved XP round trip");
Equal(11, loaded.Workers[0].Experience.Mining, "mining XP round trip");
Equal(12, loaded.Workers[0].Experience.Fishing, "fishing XP round trip");
Equal(13, loaded.Workers[0].Experience.Foraging, "foraging XP round trip");
Equal(14, loaded.Workers[0].Experience.Combat, "combat XP round trip");
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
var corrupt = JsonSerializer.Deserialize<WorkerRosterEntry>("{\"WorkerId\":\"old\",\"Experience\":{\"Farming\":-4}}")!;
Equal(0, corrupt.Clone().Experience.Farming, "negative XP normalized on clone");
var missing = JsonSerializer.Deserialize<WorkerRosterEntry>("{\"WorkerId\":\"null\",\"Experience\":null}")!;
Equal(0, missing.Clone().Experience.Farming, "null XP normalized on clone");
Console.WriteLine("Worker experience tests passed.");
