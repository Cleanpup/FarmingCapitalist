using Microsoft.Xna.Framework;

namespace HireSkilledHelpers.Workers;

internal sealed record WorkerNavigationTarget(string LocationName, Point Tile, int FacingDirection);
