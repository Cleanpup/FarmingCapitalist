using System.Text.Json;
using FarmingCapitalist.Workers;

internal static class StaminaChecks
{
    private static void Check(bool condition, string scenario)
    {
        if (!condition) throw new Exception(scenario);
    }

    public static void Run()
    {
        var worker = new WorkerRosterEntry();
        Check(worker.Stamina.Current == 270, "Default player energy must be the worker's starting pool");
        foreach (WorkerStaminaAction action in new[] { WorkerStaminaAction.Water, WorkerStaminaAction.Axe, WorkerStaminaAction.Pickaxe })
            Check(WorkerStaminaPolicy.Cost(action, worker.Experience) == 2, "Level-zero uncharged tool cost");
        worker.Experience = new() { Farming = 15000, Mining = 15000, Foraging = 15000, Fishing = 15000 };
        foreach (WorkerStaminaAction action in new[] { WorkerStaminaAction.Water, WorkerStaminaAction.Axe, WorkerStaminaAction.Pickaxe })
            Check(WorkerStaminaPolicy.Cost(action, worker.Experience) == 1, "Level-ten proficiency must halve tool cost");
        Check(WorkerStaminaPolicy.Cost(WorkerStaminaAction.FishingCast, worker.Experience) == 7, "Level-ten fishing cast cost");
        Check(WorkerStaminaPolicy.Cost(WorkerStaminaAction.Free, worker.Experience) == 0, "Swords/scythes/gathering are free");
        worker.Experience = new();
        Check(!WorkerStaminaPolicy.TryUse(worker, WorkerStaminaAction.Axe, false, true, true), "Clients cannot spend energy");
        Check(!WorkerStaminaPolicy.TryUse(worker, WorkerStaminaAction.Axe, true, false, true), "Unloaded worlds cannot spend energy");
        Check(WorkerStaminaPolicy.TryUse(worker, WorkerStaminaAction.Axe, true, true, false)
            && worker.Stamina.Current == 270, "Windup affordability must not charge a cancelled action");
        for (int hit = 0; hit < 135; hit++)
            Check(WorkerStaminaPolicy.TryUse(worker, WorkerStaminaAction.Pickaxe, true, true, true), "Each durable-rock hit spends separately");
        Check(worker.Stamina.Current == 0, "135 basic hits exhaust starting energy");
        Check(!WorkerStaminaPolicy.TryUse(worker, WorkerStaminaAction.Pickaxe, true, true, true)
            && worker.Stamina.Resting, "An unaffordable hit must rest without changing the world");
        Check(WorkerStaminaPolicy.TryUse(worker, WorkerStaminaAction.Free, true, true, true)
            && worker.Stamina.Current == 0, "Free actions remain free even at zero energy");
        Check(worker.Experience.Mining == 0, "Energy accounting cannot invent XP");
        foreach (WorkerProfession profession in Enum.GetValues<WorkerProfession>())
        {
            var member = new WorkerRosterEntry { Profession = profession };
            Check(member.Stamina.Current == 270, "Every profession starts with a default player pool");
            Check(WorkerStaminaPolicy.TryUse(member, WorkerStaminaAction.Free, true, true, true)
                && member.Stamina.Current == 270, "Free work must not drain any profession");
        }
        var proficient = new WorkerRosterEntry { Experience = new() { Foraging = 100, Mining = 380, Farming = 770, Fishing = 1300 } };
        Check(Math.Abs(WorkerStaminaPolicy.Cost(WorkerStaminaAction.Axe, proficient.Experience) - 1.9f) < 0.00001,
            "Axe cost uses worker Foraging level, not the host's profession skill");
        Check(Math.Abs(WorkerStaminaPolicy.Cost(WorkerStaminaAction.Pickaxe, proficient.Experience) - 1.8f) < 0.00001,
            "Pickaxe cost uses Mining proficiency");
        Check(Math.Abs(WorkerStaminaPolicy.Cost(WorkerStaminaAction.Water, proficient.Experience) - 1.7f) < 0.00001,
            "Watering cost uses Farming proficiency");
        Check(Math.Abs(WorkerStaminaPolicy.Cost(WorkerStaminaAction.FishingCast, proficient.Experience) - 7.6f) < 0.00001,
            "Fishing cost uses Fishing proficiency");

        worker.Stamina = new() { Day = 100, Current = 1, Resting = true };
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 650, true);
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 700, true);
        Check(worker.Stamina.Current == 11, "Recovery must use real minutes across the hour boundary");
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 700, true);
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 650, true);
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 700, true);
        Check(worker.Stamina.Current == 11, "Repeat ticks or rewinds cannot duplicate recovery");
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 710, false);
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 800, false);
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 800, true);
        Check(worker.Stamina.Current == 11, "Paused/menu/event gaps must not recover energy");
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 840, true);
        Check(worker.Stamina.Current == 51 && worker.Stamina.Resting, "Rest must continue below resume threshold");
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 850, true);
        Check(worker.Stamina.Current == 61 && !worker.Stamina.Resting, "Recovered workers resume their saved assignment");
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 1200, true);
        Check(worker.Stamina.Current == 251, "Idle recovery has a bounded rate");
        WorkerStaminaPolicy.Observe(worker.Stamina, 100, 1300, true);
        Check(worker.Stamina.Current == 270, "Recovery cannot exceed max stamina");

        worker.Stamina.Current = 12.5f;
        worker.Stamina.Resting = true;
        worker.AssignedTask = WorkerTaskKind.WaterCrops;
        var reloaded = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(worker))!;
        Check(reloaded.Stamina.Current == 12.5f && reloaded.Stamina.Resting && reloaded.AssignedTask == WorkerTaskKind.WaterCrops,
            "Save/reload must preserve fraction, rest, and assigned job");
        WorkerStaminaPolicy.Observe(reloaded.Stamina, 100, 1300, false);
        Check(reloaded.Stamina.Current == 12.5f, "Same-day reload cannot refill energy");
        WorkerStaminaPolicy.Observe(reloaded.Stamina, 101, 600, false);
        Check(reloaded.Stamina.Current == 270 && !reloaded.Stamina.Resting, "New day refills energy");
        reloaded.Stamina.Current = 42;
        WorkerStaminaPolicy.Observe(reloaded.Stamina, 101, 600, false);
        Check(reloaded.Stamina.Current == 42, "Repeated day-start cannot refill twice");
        var clone = worker.Clone();
        clone.Stamina.Current = 200;
        Check(worker.Stamina.Current == 12.5f, "Roster snapshots must not alias stamina");
        var legacy = JsonSerializer.Deserialize<WorkerRosterEntry>("{\"WorkerId\":\"old\"}")!;
        Check(legacy.Stamina.Current == 270, "Legacy roster starts with full default stamina");
        worker.Stamina.Current = float.NaN;
        Check(worker.Clone().Stamina.Current == 0, "Corrupt energy must normalize conservatively");

        var fisher = new WorkerRosterEntry { Profession = WorkerProfession.Fisher, AssignedTask = WorkerTaskKind.Fish };
        fisher.Fishing.Day = 2;
        fisher.Stamina.Current = 7;
        Check(!WorkerFishingPolicy.TryBeginCast(fisher, 2, true, true, true) && fisher.Stamina.Resting
            && !fisher.Fishing.CastStarted, "Unaffordable cast must not begin or roll fish");
        fisher.Stamina.Current = 270;
        fisher.Stamina.Resting = false;
        Check(WorkerFishingPolicy.TryBeginCast(fisher, 2, true, true, true), "Affordable cast begins");
        var savedCast = fisher.Clone();
        Check(WorkerFishingPolicy.TryBeginCast(savedCast, 2, true, true, true) && savedCast.Stamina.Current == 262,
            "Reloading an active cast cannot charge it twice");
        savedCast.Fishing.CastStarted = false;
        Check(WorkerFishingPolicy.TryBeginCast(savedCast, 2, true, true, true) && savedCast.Stamina.Current == 254,
            "Cancelling an already-started cast must not refund it; next cast spends separately");
        Console.WriteLine("Worker stamina checks passed.");
    }
}
