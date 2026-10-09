namespace FarmingCapitalist.Workers;

/// <summary>Short, cardinal casts cross only water, including nonfishable decorative shore borders.</summary>
internal static class WorkerFishingShorePolicy
{
    public const int MaximumCastTiles = 4;

    public static bool TryFindCast(int shoreX, int shoreY, int dx, int dy,
        Func<int, int, bool> isWater, Func<int, int, bool> eligibleFishableWater, out (int X, int Y) cast)
    {
        cast = default;
        if (Math.Abs(dx) + Math.Abs(dy) != 1) return false;
        for (int distance = 1; distance <= MaximumCastTiles; distance++)
        {
            int x = shoreX + dx * distance, y = shoreY + dy * distance;
            if (!isWater(x, y)) break;
            if (!eligibleFishableWater(x, y)) continue;
            cast = (x, y);
            return true;
        }
        return false;
    }

    public static bool IsContinuousWaterCast(int shoreX, int shoreY, int waterX, int waterY,
        Func<int, int, bool> isWater)
    {
        int dx = waterX - shoreX, dy = waterY - shoreY;
        int distance = Math.Abs(dx) + Math.Abs(dy);
        if (distance is < 1 or > MaximumCastTiles || (dx != 0 && dy != 0)) return false;
        for (int step = 1; step <= distance; step++)
            if (!isWater(shoreX + Math.Sign(dx) * step, shoreY + Math.Sign(dy) * step)) return false;
        return true;
    }
}
