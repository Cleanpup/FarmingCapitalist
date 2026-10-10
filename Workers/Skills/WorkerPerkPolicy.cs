namespace HireSkilledHelpers.Workers;

/// <summary>Automatic level-five perks derived from each worker's saved XP, without duplicate unlock state.</summary>
internal static class WorkerPerkPolicy
{
    public const int UnlockLevel = 5;

    public static bool HasStaminaPerk(WorkerSkillExperience? skills)
        => skills is not null && (Unlocked(skills.Farming) || Unlocked(skills.Mining)
            || Unlocked(skills.Fishing) || Unlocked(skills.Foraging));

    public static bool HasHealthPerk(WorkerSkillExperience? skills)
        => skills is not null && Unlocked(skills.Combat);

    public static float MaximumStamina(WorkerSkillExperience? skills)
        => WorkerStaminaPolicy.Maximum * (HasStaminaPerk(skills) ? 2 : 1);

    public static int MaximumHealth(WorkerSkillExperience? skills)
        => WorkerCombatPolicy.MaxHealth * (HasHealthPerk(skills) ? 2 : 1);

    private static bool Unlocked(int experience)
        => WorkerExperiencePolicy.GetLevel(experience) >= UnlockLevel;
}
