namespace FarmingCapitalist.Workers;

// Game-facing appearance and storage values are irrelevant to these pure roster tests.
internal sealed class WorkerAppearanceData
{
    public static WorkerAppearanceData CreateDefault() => new();
    public WorkerAppearanceData Clone() => new();
}

internal static class WorkerForageAreaCatalog
{
    public const string DefaultLocationName = "Forest";
}

internal sealed class WorkerHarvestDestination
{
    public string LocationName { get; set; } = string.Empty;
    public int TileX { get; set; }
    public int TileY { get; set; }

    public WorkerHarvestDestination Clone() => new()
    {
        LocationName = this.LocationName,
        TileX = this.TileX,
        TileY = this.TileY,
    };
}
