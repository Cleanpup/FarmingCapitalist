namespace HireSkilledHelpers.Workers;

internal static class WorkerCombatTargetPolicy
{
    public static bool IsRevealedCrab(bool moving) => moving;

    public static bool CanTargetBug(bool armored) => !armored;

    // Duggy deals contact damage only during its exposed frames. Its invisible
    // underground and emerging frames must not become worker targets.
    public static bool IsExposedDuggy(bool invisible, int frame)
        => !invisible && frame is 8 or 9;

    public static bool ShouldTriggerDuggy(bool invisible, int frame, bool workerInRange, bool validEmergenceTile)
        => invisible && frame < 4 && workerInRange && validEmergenceTile;

    public static T? TargetAfterContact<T>(T? current, T attacker, bool attackerEligible)
        where T : class
        => attackerEligible ? attacker : current;

    public static bool ShouldOfferBlockedMonsterDialogue(bool onSlayOrder, bool waitingForClearRoute,
        bool monsterAlive, bool monsterPresent, bool monsterVisible)
        => onSlayOrder && waitingForClearRoute && monsterAlive && monsterPresent && monsterVisible;

    public static bool CanDamageCrabNow(bool isStickBug, bool shellGone, bool exposedAnimation)
        => isStickBug || shellGone || exposedAnimation;
}
