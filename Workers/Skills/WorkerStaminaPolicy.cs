namespace FarmingCapitalist.Workers;

internal enum WorkerStaminaAction { Free, Water, Axe, Pickaxe, FishingCast }

/// <summary>Installed vanilla uncharged tool costs, using worker skills rather than player buffs.</summary>
internal static class WorkerStaminaPolicy
{
    // Farmer's starting maxStamina. Workers don't own stardrops or enchanted tools.
    public const float Maximum = 270;
    // Worker-specific idle recovery: 10 energy per 10 active game minutes.
    public const float RecoveryPerMinute = 1;
    public const float ResumeAt = Maximum * 0.2f;

    public static float Cost(WorkerStaminaAction action, WorkerSkillExperience skills)
        => action switch
        {
            // WateringCan.DoFunction(power=0), Axe.DoFunction(power=1),
            // Pickaxe.DoFunction(toolPower=0), FishingRod.DoFunction.
            WorkerStaminaAction.Water => 2f - WorkerExperiencePolicy.GetLevel(skills.Farming) * 0.1f,
            WorkerStaminaAction.Axe => 2f - WorkerExperiencePolicy.GetLevel(skills.Foraging) * 0.1f,
            WorkerStaminaAction.Pickaxe => 2f - WorkerExperiencePolicy.GetLevel(skills.Mining) * 0.1f,
            WorkerStaminaAction.FishingCast => 8f - WorkerExperiencePolicy.GetLevel(skills.Fishing) * 0.1f,
            _ => 0,
        };

    public static int Minute(int time) => Math.Clamp(time / 100 * 60 + time % 100, 360, 1560);

    public static bool Observe(WorkerStaminaState state, int day, int time, bool recovering)
    {
        int minute = Minute(time);
        if (state.Day != day)
        {
            state.Day = day;
            state.Current = Maximum;
            state.Resting = false;
            state.LastObservedMinute = minute;
            state.WasRecovering = recovering;
            return true;
        }
        bool changed = state.WasRecovering != recovering;
        if (minute > state.LastObservedMinute)
        {
            if (recovering && state.WasRecovering && state.LastObservedMinute >= 0)
                state.Current = Math.Min(Maximum, state.Current + (minute - state.LastObservedMinute) * RecoveryPerMinute);
            state.LastObservedMinute = minute;
            changed = true;
        }
        state.WasRecovering = recovering;
        if (state.Resting && state.Current >= ResumeAt)
        {
            state.Resting = false;
            changed = true;
        }
        return changed;
    }

    public static bool TryUse(WorkerRosterEntry worker, WorkerStaminaAction action,
        bool isHost, bool worldReady, bool spend)
    {
        if (!isHost || !worldReady) return false;
        worker.Stamina ??= new();
        float cost = Cost(action, worker.Experience ?? new());
        if (cost == 0) return true;
        if (worker.Stamina.Resting) return false;
        if (worker.Stamina.Current < cost)
        {
            worker.Stamina.Resting = true;
            return false;
        }
        if (spend) worker.Stamina.Current = Math.Max(0, worker.Stamina.Current - cost);
        return true;
    }
}
