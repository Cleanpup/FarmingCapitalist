using System;

namespace HireSkilledHelpers.Workers;

internal enum WorkerObstacleToolActionStatus
{
    Continue,
    Completed,
    NoProgress,
    ActionLimitReached,
}

/// <summary>Bounds debris tool work by its actual vanilla durability and detects stalled actions.</summary>
internal static class WorkerObstacleToolProgressPolicy
{
    public static int GetMaximumActions(int initialDurability, int damagePerToolAction, int hardLimit)
    {
        int damage = Math.Max(1, damagePerToolAction);
        int needed = Math.Max(1, (Math.Max(0, initialDurability) + damage - 1) / damage);
        return Math.Min(Math.Max(1, hardLimit), needed);
    }

    public static WorkerObstacleToolActionStatus Evaluate(
        bool toolActionCompleted,
        bool targetRemains,
        int durabilityBefore,
        int durabilityAfter,
        int actionCount,
        int maximumActions)
    {
        if (toolActionCompleted || !targetRemains)
            return WorkerObstacleToolActionStatus.Completed;

        if (durabilityAfter >= durabilityBefore)
            return WorkerObstacleToolActionStatus.NoProgress;

        if (actionCount >= Math.Max(1, maximumActions))
            return WorkerObstacleToolActionStatus.ActionLimitReached;

        return WorkerObstacleToolActionStatus.Continue;
    }
}
