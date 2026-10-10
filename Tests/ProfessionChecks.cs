using System.Text.Json;
using FarmingCapitalist.Workers;

internal static class ProfessionChecks
{
    private static void Check(bool value, string scenario)
    {
        if (!value) throw new Exception(scenario);
    }

    public static void Run()
    {
        foreach (WorkerProfession original in Enum.GetValues<WorkerProfession>())
        foreach (WorkerProfession replacement in Enum.GetValues<WorkerProfession>())
        foreach (bool completed in new[] { false, true })
        {
            var worker = new WorkerRosterEntry
            {
                WorkerId = "same-worker", DisplayName = "Taylor", Profession = original,
                AssignedTask = WorkerTaskPolicy.GetTasks(original)[0],
                Experience = new() { Farming = 2150, Mining = 2300, Fishing = 2600, Foraging = 2900, Combat = 3200 },
                Stamina = new() { Day = 100, Current = 43.5f, Maximum = 540, Resting = true },
                CombatHealth = 0, CombatMaxHealth = 200, LastCombatHealthDay = 100, LastDefeatedDay = 100,
                DailyWage = 100, LastPaidDay = 100, LastWageAttemptDay = 100,
                HarvestDestination = new() { LocationName = "Farm", TileX = 12, TileY = 15 },
                Exploration = new() { PendingLoot = new() { new() { Area = "Mines", ItemId = "(O)684", Stack = 2 } } },
                Fishing = new() { PendingCatches = new() { new() { ItemId = "(O)145", Experience = 18 } } },
            };
            if (completed) WorkerTaskPolicy.AssignTask(worker, WorkerTaskKind.Idle, 100, completedDailyOrder: true);
            string before = JsonSerializer.Serialize(worker);
            Check(WorkerTaskPolicy.TryChangeProfession(worker, replacement, 100, true, true), "Host can select each profession");
            if (original == replacement)
            {
                Check(JsonSerializer.Serialize(worker) == before, "Selecting the current profession preserves its active or remembered job");
                continue;
            }
            Check(worker.Profession == replacement && worker.AssignedTask == WorkerTaskKind.Idle
                && worker.NextDayTask is null && worker.CompletedTaskDay == -1,
                "New profession cancels current and remembered daily orders");
            Check(!WorkerTaskPolicy.RestoreDailyOrder(worker, 101), "Old job cannot restart tomorrow after switching");
            Check(WorkerPerkPolicy.MaximumStamina(worker.Experience) == 540
                && WorkerPerkPolicy.MaximumHealth(worker.Experience) == 200,
                "Every profession keeps both earned perks");
            var saved = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(worker))!.Clone();
            Check(saved.Profession == replacement && saved.AssignedTask == WorkerTaskKind.Idle
                && saved.NextDayTask is null && saved.Stamina.Current == 43.5f && saved.Stamina.Maximum == 540
                && saved.CombatHealth == 0 && saved.LastDefeatedDay == 100
                && saved.Exploration.PendingLoot.Count == 1 && saved.Fishing.PendingCatches.Count == 1,
                "Save/reload retains role, depleted vitals, defeat and pending loot");
            // Compare every other saved field, catching accidental wage/identity/storage/stat changes.
            var baseline = JsonSerializer.Deserialize<WorkerRosterEntry>(before)!;
            baseline.Profession = replacement;
            baseline.AssignedTask = WorkerTaskKind.Idle;
            baseline.NextDayTask = null;
            baseline.CompletedTaskDay = -1;
            Check(JsonSerializer.Serialize(worker) == JsonSerializer.Serialize(baseline),
                "Only profession and daily-order fields may change");
        }
        foreach (var access in new[] { (Host: false, Ready: true), (Host: true, Ready: false), (Host: false, Ready: false) })
        {
            var worker = new WorkerRosterEntry { AssignedTask = WorkerTaskKind.WaterCrops };
            string before = JsonSerializer.Serialize(worker);
            Check(!WorkerTaskPolicy.TryChangeProfession(worker, WorkerProfession.Fisher, 100, access.Host, access.Ready)
                && JsonSerializer.Serialize(worker) == before, "Farmhands and unavailable worlds cannot change any state");
        }
        var invalid = new WorkerRosterEntry { AssignedTask = WorkerTaskKind.WaterCrops };
        string originalState = JsonSerializer.Serialize(invalid);
        Check(!WorkerTaskPolicy.TryChangeProfession(invalid, (WorkerProfession)999, 100, true, true)
            && JsonSerializer.Serialize(invalid) == originalState, "Invalid professions cannot cancel an order or mutate state");
        Console.WriteLine("Worker profession change checks passed (all 25 role combinations, orders, authority and persistence).");
    }
}
