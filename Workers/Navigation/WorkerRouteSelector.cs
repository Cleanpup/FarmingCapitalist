using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FarmingCapitalist.Workers;

internal sealed record WorkerRouteOption<TLeg>(TLeg Leg, Point ArrivalTile) where TLeg : class;

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

            if (TryFollow(routeIndex, route, 0, startTile, null, destinationTile,
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
        TLeg? firstLeg,
        Point destinationTile,
        Func<string, Point, string, IReadOnlyList<WorkerRouteOption<TLeg>>> getLegOptions,
        Func<string, Point, Point, bool> canReachDestination,
        HashSet<(int RouteIndex, int LegIndex, Point Tile)> deadEnds,
        out TLeg? selectedFirstLeg)
        where TLeg : class
    {
        if (legIndex == route.Length - 1)
        {
            selectedFirstLeg = canReachDestination(route[legIndex], startTile, destinationTile) ? firstLeg : null;
            return selectedFirstLeg is not null;
        }

        if (deadEnds.Contains((routeIndex, legIndex, startTile)))
        {
            selectedFirstLeg = null;
            return false;
        }

        foreach (WorkerRouteOption<TLeg> option in getLegOptions(route[legIndex], startTile, route[legIndex + 1]))
        {
            if (TryFollow(routeIndex, route, legIndex + 1, option.ArrivalTile, firstLeg ?? option.Leg,
                    destinationTile, getLegOptions, canReachDestination, deadEnds, out selectedFirstLeg))
                return true;
        }

        deadEnds.Add((routeIndex, legIndex, startTile));
        selectedFirstLeg = null;
        return false;
    }
}
