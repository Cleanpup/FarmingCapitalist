namespace FarmingCapitalist.Workers;

internal sealed class WorkerRosterEntry
{
    public string WorkerId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int SpawnTileX { get; set; }

    public int SpawnTileY { get; set; }

    public WorkerAppearanceData Appearance { get; set; } = WorkerAppearanceData.CreateDefault();
}
