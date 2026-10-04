using Microsoft.Xna.Framework;

namespace FarmingCapitalist.Workers;

internal sealed record WorkerNavigationTarget(string LocationName, Point Tile, int FacingDirection);
