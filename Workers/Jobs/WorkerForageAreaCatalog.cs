using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist.Workers;

/// <summary>Outdoor mainland areas which vanilla NPC schedule pathing can reach on foot.</summary>
internal static class WorkerForageAreaCatalog
{
    public const string DefaultLocationName = "Farm";

    private static readonly WorkerForageArea[] SupportedAreas =
    {
        new("Farm", "Farm"),
        new("Forest", "Cindersap Forest"),
        new("Town", "Pelican Town"),
        new("Mountain", "The Mountain"),
        new("Beach", "The Beach"),
        new("BusStop", "Bus Stop"),
        new("Backwoods", "Backwoods"),
        new("Railroad", "Railroad"),
        new("Woods", "Secret Woods"),
    };

    public static IReadOnlyList<WorkerForageArea> GetAvailableAreas()
    {
        List<WorkerForageArea> result = new();
        if (!Context.IsWorldReady)
            return result;

        foreach (WorkerForageArea area in SupportedAreas)
        {
            GameLocation? location = Game1.getLocationFromName(area.LocationName);
            if (location is { IsOutdoors: true })
                result.Add(area);
        }

        return result;
    }

    public static bool IsValidLocation(string? locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
            return false;

        foreach (WorkerForageArea area in GetAvailableAreas())
        {
            if (string.Equals(area.LocationName, locationName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string GetDisplayName(string? locationName)
    {
        foreach (WorkerForageArea area in SupportedAreas)
        {
            if (string.Equals(area.LocationName, locationName, StringComparison.OrdinalIgnoreCase))
                return area.DisplayName;
        }

        return locationName ?? "Unknown area";
    }
}
