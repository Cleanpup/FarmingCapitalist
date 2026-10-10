namespace HireSkilledHelpers.Workers;

/// <summary>Vanilla 1.6 location spawn seasons and Data/Fish bite gates; excludes boss/special fish.</summary>
internal static class WorkerFishingCatchCatalog
{
    private sealed record Fish(int Id, int Start, int End, string Weather, double Chance, int Depth, double DepthPenalty, int Experience,
        int SecondStart = -1, int SecondEnd = -1);
    // Normal-quality Fishing XP is max(1, 3 + difficulty / 3), matching FishingRod.
    private static readonly Fish[] FishData =
    {
        new(128,1200,1600,"sun",.3,4,.5,29), new(129,600,2600,"any",.25,1,.3,13),
        new(130,600,1900,"any",.15,3,.55,26), new(131,600,1900,"any",.65,1,.1,13),
        new(132,1800,2600,"any",.45,1,.1,14), new(136,600,1900,"any",.4,3,.2,19),
        new(137,600,2600,"any",.45,1,.1,12), new(138,600,1900,"sun",.35,2,.3,18),
        new(139,600,1900,"any",.4,3,.2,19), new(140,1200,2600,"rain",.4,2,.15,18),
        new(141,600,2600,"any",.45,1,.1,14), new(142,600,2600,"any",.45,1,.1,8),
        new(143,600,2400,"rain",.4,4,.1,28), new(144,600,2600,"any",.4,3,.15,23),
        new(145,600,1900,"sun",.45,1,.1,13), new(146,600,1900,"any",.4,2,.15,21),
        new(147,600,2600,"any",.45,1,.1,11), new(148,1600,2600,"rain",.55,3,.1,26),
        new(149,600,1300,"any",.1,5,.08,34), new(150,600,1900,"rain",.45,2,.1,16),
        new(151,1800,2600,"any",.35,3,.3,28), new(154,600,1900,"any",.25,3,.25,16),
        new(155,1800,2600,"any",.1,4,.25,29), new(267,600,2000,"any",.15,2,.05,19),
        new(269,2200,2600,"any",.33,1,.1,21), new(698,600,1900,"any",.35,3,.2,29),
        new(699,600,1900,"any",.2,3,.1,23), new(700,600,2600,"any",.35,2,.2,18),
        new(701,600,1400,"any",.4,3,.2,19), new(702,600,2600,"any",.45,1,.1,14),
        new(704,600,1900,"any",.15,3,.1,29), new(705,600,1100,"any",.3,3,.15,23,1800,2600),
        new(706,900,2600,"rain",.35,2,.2,18), new(707,600,2600,"any",.3,3,.05,31),
        new(708,600,1100,"any",.4,3,.2,19,1900,2600), new(837,600,2600,"any",.4,3,.1,19),
    };
    // 1=spring, 2=summer, 4=fall, 8=winter. These are location-spawn seasons;
    // Data/Fish's legacy season field is not used by vanilla generic getFish.
    private static readonly Dictionary<string, (int Id, int Seasons)[]> Spawns = new()
    {
        ["Forest"] = new[] { (145,3),(143,13),(132,15),(706,7),(702,15),(144,10),(138,2),(704,2),(140,12),(139,4),(699,12),(141,8),(707,8) },
        ["Mountain"] = new[] { (136,15),(142,7),(702,15),(700,15),(138,2),(698,10),(140,12),(269,12),(141,8),(707,8) },
        ["Town"] = new[] { (137,5),(132,15),(143,13),(145,3),(706,7),(138,2),(144,10),(139,4),(140,12),(699,12),(141,8),(707,8) },
        ["Beach"] = new[] { (129,5),(131,13),(147,9),(148,5),(708,11),(267,3),(128,2),(130,10),(146,10),(149,2),(150,14),(155,6),(701,6),(154,12),(705,12),(151,8) },
        ["IslandSouth"] = new[] { (155,15),(130,15),(128,15),(267,15),(837,15) },
    };
    private static readonly HashSet<string> AllowedIds = FishData.Select(fish => $"(O){fish.Id}").ToHashSet(StringComparer.Ordinal);
    public static bool IsAllowed(string id) => AllowedIds.Contains(id);
    public static int Experience(string id) => FishData.FirstOrDefault(fish => $"(O){fish.Id}" == id)?.Experience ?? 0;

    public static IReadOnlyList<string> Eligible(string area, string season, int time, bool raining)
        => EligibleFish(area, season, time, raining).Select(fish => $"(O){fish.Id}").ToArray();

    private static Fish[] EligibleFish(string area, string season, int time, bool raining)
    {
        int mask = season.ToLowerInvariant() switch { "spring" => 1, "summer" => 2, "fall" => 4, "winter" => 8, _ => 0 };
        if (!Spawns.TryGetValue(area, out var rules)) return Array.Empty<Fish>();
        return rules.Where(rule => (rule.Seasons & mask) != 0).Select(rule => FishData.First(fish => fish.Id == rule.Id))
            .Where(fish => ((time >= fish.Start && time < fish.End) || (fish.SecondStart >= 0 && time >= fish.SecondStart && time < fish.SecondEnd))
                && (fish.Weather == "any" || (fish.Weather == "rain") == raining)).ToArray();
    }

    public static WorkerFishingCatch? Roll(string area, string season, int timeOfDay, bool raining, int fishingLevel, Random random)
    {
        Fish[] eligible = EligibleFish(area, season, timeOfDay, raining);
        // Vanilla shuffles entries at the same precedence and stops on the first
        // successful bite roll. A failed attempt consumes time without a reward.
        for (int i = eligible.Length - 1; i > 0; i--)
        { int j = random.Next(i + 1); (eligible[i], eligible[j]) = (eligible[j], eligible[i]); }
        foreach (Fish fish in eligible)
        {
            double chance = Math.Min(.9, fish.Chance * (1 - Math.Max(0, fish.Depth - 4) * fish.DepthPenalty)
                + Math.Clamp(fishingLevel, 0, 10) / 50d);
            if (random.NextDouble() < chance) return new() { ItemId = $"(O){fish.Id}", Experience = fish.Experience };
        }
        return null;
    }
}
