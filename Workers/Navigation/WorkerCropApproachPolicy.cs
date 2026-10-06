using System;
using Microsoft.Xna.Framework;

namespace FarmingCapitalist.Workers;

internal static class WorkerCropApproachPolicy
{
    public static bool IsInWorkRange(Point workerTile, Point cropTile)
        => Math.Abs(workerTile.X - cropTile.X) + Math.Abs(workerTile.Y - cropTile.Y) <= 1;

    public static bool IsNearbyLanding(Point landingTile, Point cropTile, bool cropStillEligible)
        => cropStillEligible && IsInWorkRange(landingTile, cropTile);

    public static Point[] GetApproaches(Point cropTile) =>
    [
        cropTile,
        new(cropTile.X, cropTile.Y - 1),
        new(cropTile.X + 1, cropTile.Y),
        new(cropTile.X, cropTile.Y + 1),
        new(cropTile.X - 1, cropTile.Y),
    ];
}
