using Microsoft.Xna.Framework;

namespace HireSkilledHelpers.Workers;

/// <summary>A saved chest address. Null means the farm shipping bin.</summary>
internal sealed class WorkerHarvestDestination
{
    public WorkerHarvestDestination()
    {
    }

    public string LocationName { get; set; } = string.Empty;

    public int TileX { get; set; }

    public int TileY { get; set; }

    internal Point Tile => new(this.TileX, this.TileY);

    public WorkerHarvestDestination Clone() => new()
    {
        LocationName = this.LocationName,
        TileX = this.TileX,
        TileY = this.TileY,
    };
}
