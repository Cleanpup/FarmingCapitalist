using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace HireSkilledHelpers.Workers;

internal sealed record WorkerChestOption(string LocationName, string LocationLabel, Point Tile)
{
    public string Label => $"{this.LocationLabel} ({this.Tile.X}, {this.Tile.Y})";

    public WorkerHarvestDestination ToDestination() => new()
    {
        LocationName = this.LocationName,
        TileX = this.Tile.X,
        TileY = this.Tile.Y,
    };
}

/// <summary>Finds placed storage chests in loaded locations, including building interiors.</summary>
internal static class WorkerChestCatalog
{
    public static IReadOnlyList<WorkerChestOption> GetAvailableChests()
    {
        List<WorkerChestOption> options = new();
        if (!Context.IsWorldReady)
            return options;

        Utility.ForEachLocation(location =>
        {
            string locationName = location.NameOrUniqueName;
            string label = location.GetDisplayName();
            if (string.IsNullOrWhiteSpace(label))
                label = locationName;

            foreach (KeyValuePair<Vector2, StardewValley.Object> pair in location.Objects.Pairs)
            {
                if (pair.Value is Chest chest && IsUsableChest(chest))
                    options.Add(new WorkerChestOption(locationName, label, pair.Key.ToPoint()));
            }
            return true;
        }, includeInteriors: true, includeGenerated: true);

        List<WorkerChestOption> distinct = options
            .DistinctBy(option => (option.LocationName, option.Tile))
            .ToList();
        HashSet<(string Label, Point Tile)> ambiguousLabels = distinct
            .GroupBy(option => (option.LocationLabel, option.Tile))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        return distinct
            .Select(option => ambiguousLabels.Contains((option.LocationLabel, option.Tile))
                ? option with { LocationLabel = $"{option.LocationLabel} [{option.LocationName}]" }
                : option)
            .OrderBy(option => option.LocationLabel)
            .ThenBy(option => option.Tile.Y)
            .ThenBy(option => option.Tile.X)
            .ToList();
    }

    public static bool TryGetChest(WorkerHarvestDestination destination, out Chest? chest)
    {
        chest = null;
        if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(destination.LocationName))
            return false;

        Chest? foundChest = null;
        Utility.ForEachLocation(location =>
        {
            if (location.NameOrUniqueName != destination.LocationName)
                return true;

            if (location.Objects.TryGetValue(destination.Tile.ToVector2(), out StardewValley.Object? placed)
                && placed is Chest found
                && IsUsableChest(found))
            {
                foundChest = found;
                return false;
            }
            return true;
        }, includeInteriors: true, includeGenerated: true);

        chest = foundChest;
        return chest is not null;
    }

    private static bool IsUsableChest(Chest chest)
    {
        if (!chest.playerChest.Value)
            return false;

        return chest.SpecialChestType is Chest.SpecialChestTypes.None
            or Chest.SpecialChestTypes.BigChest
            or Chest.SpecialChestTypes.JunimoChest;
    }
}
