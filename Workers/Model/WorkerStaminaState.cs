namespace HireSkilledHelpers.Workers;

/// <summary>Saved energy and recovery clock for one worker, independent of the player.</summary>
internal sealed class WorkerStaminaState
{
    public float Current { get; set; } = WorkerStaminaPolicy.Maximum;
    public float Maximum { get; set; } = WorkerStaminaPolicy.Maximum;
    public int Day { get; set; } = -1;
    public int LastObservedMinute { get; set; } = -1;
    public bool WasRecovering { get; set; }
    public bool Resting { get; set; }

    public WorkerStaminaState Clone() => new()
    {
        Current = float.IsFinite(this.Current) ? Math.Clamp(this.Current, 0, WorkerStaminaPolicy.ValidMaximum(this.Maximum)) : 0,
        Maximum = WorkerStaminaPolicy.ValidMaximum(this.Maximum),
        Day = this.Day,
        LastObservedMinute = this.LastObservedMinute,
        WasRecovering = this.WasRecovering,
        Resting = this.Resting,
    };
}
