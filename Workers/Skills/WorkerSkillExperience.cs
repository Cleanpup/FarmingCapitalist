namespace FarmingCapitalist.Workers;

/// <summary>Mod-owned experience for one worker across the five standard skills.</summary>
internal sealed class WorkerSkillExperience
{
    public int Farming { get; set; }

    public int Mining { get; set; }

    public int Fishing { get; set; }

    public int Foraging { get; set; }

    public int Combat { get; set; }

    public WorkerSkillExperience Clone() => new()
    {
        Farming = Math.Max(0, this.Farming),
        Mining = Math.Max(0, this.Mining),
        Fishing = Math.Max(0, this.Fishing),
        Foraging = Math.Max(0, this.Foraging),
        Combat = Math.Max(0, this.Combat),
    };
}
