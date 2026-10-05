using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FarmingCapitalist.Workers;

internal sealed record WorkerRouteOption<TLeg>(Point ArrivalTile, Func<TLeg?> BuildLeg) where TLeg : class;

/// <summary>Choose a first warp only after every remaining leg can reach its next exit or final tile.</summary>
internal static class WorkerRouteSelector
{
    public static bool TrySelect<TLeg>(
        IReadOnlyList<string[]> routes,
        Point startTile,
        Point destinationTile,
        Func<string, Point, string, IReadOnlyList<WorkerRouteOption<TLeg>>> getLegOptions,
        Func<string, Point, Point, bool> canReachDestination,
        out TLeg? firstLeg,
        out string[]? selectedRoute)
        where TLeg : class
    {
        HashSet<(int RouteIndex, int LegIndex, Point Tile)> deadEnds = new();
        for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
        {
            string[] route = routes[routeIndex];
            if (route.Length < 2)
                continue;

            if (TryFollow(routeIndex, route, 0, startTile, destinationTile,
                    getLegOptions, canReachDestination, deadEnds, out firstLeg))
            {
                selectedRoute = route;
                return true;
            }
        }

        firstLeg = null;
        selectedRoute = null;
        return false;
    }

    private static bool TryFollow<TLeg>(
        int routeIndex,
        string[] route,
        int legIndex,
        Point startTile,
        Point destinationTile,
        Func<string, Point, string, IReadOnlyList<WorkerRouteOption<TLeg>>> getLegOptions,
        Func<string, Point, Point, bool> canReachDestination,
        HashSet<(int RouteIndex, int LegIndex, Point Tile)> deadEnds,
        out TLeg? selectedFirstLeg)
        where TLeg : class
    {
        if (legIndex == route.Length - 1)
        {
            selectedFirstLeg = null;
            return canReachDestination(route[legIndex], startTile, destinationTile);
        }

        if (deadEnds.Contains((routeIndex, legIndex, startTile)))
        {
            selectedFirstLeg = null;
            return false;
        }

        foreach (WorkerRouteOption<TLeg> option in getLegOptions(route[legIndex], startTile, route[legIndex + 1]))
        {
            // Validate the arrival's onward route before doing a potentially expensive path
            // search to the current warp. A dead-end landing should cost no first-leg search.
            if (!TryFollow(routeIndex, route, legIndex + 1, option.ArrivalTile,
                    destinationTile, getLegOptions, canReachDestination, deadEnds, out _))
                continue;

            TLeg? leg = option.BuildLeg();
            if (leg is not null)
            {
                selectedFirstLeg = leg;
                return true;
            }
        }

        deadEnds.Add((routeIndex, legIndex, startTile));
        selectedFirstLeg = null;
        return false;
    }
}
