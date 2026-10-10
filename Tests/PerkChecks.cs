using System.Text.Json;
using FarmingCapitalist.Workers;

internal static class PerkChecks
{
    private static void Check(bool value, string scenario)
    {
        if (!value) throw new Exception(scenario);
    }

    public static void Run()
    {
        int threshold = WorkerExperiencePolicy.GetThreshold(5);
        Check(WorkerPerkPolicy.MaximumStamina(null) == 270 && WorkerPerkPolicy.MaximumHealth(null) == 100,
            "Missing/legacy XP has base stamina and health");
        foreach (var setSkill in new Action<WorkerSkillExperience, int>[] {
            (s, xp) => s.Farming = xp, (s, xp) => s.Mining = xp,
            (s, xp) => s.Fishing = xp, (s, xp) => s.Foraging = xp })
        {
            var skills = new WorkerSkillExperience();
            setSkill(skills, threshold - 1);
            Check(WorkerPerkPolicy.MaximumStamina(skills) == 270, "Level four has no stamina perk");
            setSkill(skills, threshold);
            Check(WorkerPerkPolicy.MaximumStamina(skills) == 540 && WorkerPerkPolicy.MaximumHealth(skills) == 100,
                "Each work skill unlocks doubled stamina at exactly level five, without health");
            setSkill(skills, int.MaxValue);
            Check(WorkerPerkPolicy.MaximumStamina(skills) == 540, "Higher levels retain the same stamina perk");
        }
        var all = new WorkerSkillExperience { Farming = threshold, Mining = threshold, Fishing = threshold, Foraging = threshold, Combat = threshold };
        Check(WorkerPerkPolicy.MaximumStamina(all) == 540 && WorkerPerkPolicy.MaximumHealth(all) == 200,
            "Multiple work skills do not repeatedly double the shared stamina pool");
        var combat = new WorkerSkillExperience { Combat = threshold - 1 };
        Check(WorkerPerkPolicy.MaximumHealth(combat) == 100, "Combat level four keeps base health");
        combat.Combat = threshold;
        Check(WorkerPerkPolicy.MaximumHealth(combat) == 200 && WorkerPerkPolicy.MaximumStamina(combat) == 270,
            "Combat level five doubles health, not stamina");
        Check(WorkerTaskPolicy.GetProfessionLabel(WorkerProfession.CombatWorker) == "Fighter"
            && (int)WorkerProfession.CombatWorker == 2, "Fighter label preserves saved profession identity");

        var worker = new WorkerRosterEntry { Experience = all, Stamina = new() { Day = 100, Current = 135 } };
        Check(WorkerStaminaPolicy.SynchronizeMaximum(worker.Stamina, WorkerPerkPolicy.MaximumStamina(all))
            && worker.Stamina.Current == 270 && worker.Stamina.Maximum == 540,
            "Unlock preserves the remaining stamina fraction instead of refilling");
        Check(!WorkerStaminaPolicy.SynchronizeMaximum(worker.Stamina, 540) && worker.Stamina.Current == 270,
            "Repeated unlock observation cannot grant extra energy");
        var saved = JsonSerializer.Deserialize<WorkerRosterEntry>(JsonSerializer.Serialize(worker))!;
        Check(saved.Clone().Stamina.Current == 270 && saved.Clone().Stamina.Maximum == 540,
            "Save and clone preserve boosted maximum");
        saved.Stamina.Current = 500;
        Check(saved.Clone().Stamina.Current == 500, "Cloning no longer truncates boosted stamina to 270");
        WorkerStaminaPolicy.SynchronizeMaximum(saved.Stamina, WorkerPerkPolicy.MaximumStamina(saved.Experience));
        WorkerStaminaPolicy.Observe(saved.Stamina, 100, 600, false);
        Check(saved.Stamina.Current == 500, "Same-day reload cannot refill boosted stamina");
        WorkerStaminaPolicy.Observe(saved.Stamina, 101, 600, false);
        Check(saved.Stamina.Current == 540, "New day refills the boosted pool");
        saved.Stamina.Current = 50; saved.Stamina.Resting = true; saved.Stamina.WasRecovering = true;
        WorkerStaminaPolicy.Observe(saved.Stamina, 101, 650, true);
        Check(saved.Stamina.Current == 100 && saved.Stamina.Resting, "Boosted pool rests below its 20-percent threshold");
        WorkerStaminaPolicy.Observe(saved.Stamina, 101, 700, true);
        Check(saved.Stamina.Current == 110 && !saved.Stamina.Resting, "Boosted pool resumes at 108 stamina");
        WorkerStaminaPolicy.Observe(saved.Stamina, 101, 2300, true);
        Check(saved.Stamina.Current == 540, "Idle recovery caps at boosted maximum");
        float before = saved.Stamina.Current;
        Check(WorkerStaminaPolicy.TryUse(saved, WorkerStaminaAction.Pickaxe, true, true, true)
            && Math.Abs(saved.Stamina.Current - (before - 1.5f)) < 0.0001f,
            "Stamina perk leaves vanilla level-five action cost unchanged");
        var client = new WorkerRosterEntry { Experience = all };
        Check(!WorkerStaminaPolicy.TryUse(client, WorkerStaminaAction.Axe, false, true, true)
            && client.Stamina.Maximum == 270 && client.Stamina.Current == 270,
            "Client execution cannot apply or spend a perk");
        worker.Stamina.Current = 0;
        WorkerStaminaPolicy.SynchronizeMaximum(worker.Stamina, 540);
        Check(worker.Stamina.Current == 0, "Stamina perk does not restore an exhausted pool");

        Check(WorkerCombatPolicy.NormalizeHealth(50, 100, 200) == 100, "HP unlock preserves missing-health fraction");
        Check(WorkerCombatPolicy.NormalizeHealth(0, 100, 200) == 0, "Health perk cannot revive a defeated worker");
        Check(WorkerCombatPolicy.NormalizeHealth(150, 200, 200) == 150, "Boosted health survives same-day normalization");
        Check(WorkerCombatPolicy.HealthAfterContact(200, 8, 200) == 192, "Damage starts from boosted health without clamping to 100");
        Check(WorkerCombatPolicy.ClampHealth(500, 200) == 200, "Boosted health still caps at maximum");
        var second = new WorkerRosterEntry();
        Check(WorkerPerkPolicy.MaximumStamina(second.Experience) == 270 && WorkerPerkPolicy.MaximumHealth(second.Experience) == 100,
            "Perks remain independent between workers");
        Console.WriteLine("Level-five worker perk checks passed.");
    }
}
