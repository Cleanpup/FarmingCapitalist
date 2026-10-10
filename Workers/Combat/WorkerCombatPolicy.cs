namespace FarmingCapitalist.Workers;

internal static class WorkerCombatPolicy
{
    public const int AggroRangeTiles = 8;

    public const int MaxHealth = 100;

    public static int ClampHealth(int health, int maximum = MaxHealth) => Math.Clamp(health, 0, maximum);

    /// <summary>Preserve the fraction of health left when loading a save from an older HP maximum.</summary>
    public static int NormalizeHealth(int health, int previousMaximum, int maximum = MaxHealth)
    {
        if (previousMaximum <= 0)
            return ClampHealth(health, maximum);
        long validHealth = Math.Clamp((long)health, 0, previousMaximum);
        return (int)((validHealth * maximum + previousMaximum - 1L) / previousMaximum);
    }

    public static bool ShouldAggroWorker(int workerDistanceSquared, int nearestFarmerDistanceSquared)
        => workerDistanceSquared <= AggroRangeTiles * AggroRangeTiles
            && workerDistanceSquared < nearestFarmerDistanceSquared;

    // Leave two in-game hours for normal travel; midnight is the arrival deadline.
    public const int BeginReturnAt = 2200;
    public const int MustBeHomeBy = 2400;

    public static bool ShouldBeginReturn(int timeOfDay) => timeOfDay >= BeginReturnAt;

    public static bool MustBeHomeNow(int timeOfDay) => timeOfDay >= MustBeHomeBy;

    public static bool IsMatchingMineFloor(string area, int level)
        => area == WorkerCombatAreaCatalog.Mines ? level is >= 1 and <= 120
            : area == WorkerCombatAreaCatalog.SkullCavern && level is > 120 and < 77377;

    public static bool ShouldFollowOnHostWarp(bool stagedAtEntrance, bool alreadyOnGeneratedFloor,
        bool matchingActiveFloor)
        => matchingActiveFloor && (stagedAtEntrance || alreadyOnGeneratedFloor);

    public static int HealthAfterContact(int health, int monsterDamage, int maximum = MaxHealth)
        => Math.Max(0, ClampHealth(health, maximum) - Math.Max(1, monsterDamage));

    public static bool IsUsefulPatrolOffset(int dx, int dy)
    {
        int distance = Math.Abs(dx) + Math.Abs(dy);
        return distance is >= 3 and <= 10 && Math.Abs(dx) <= 6 && Math.Abs(dy) <= 6;
    }
}
