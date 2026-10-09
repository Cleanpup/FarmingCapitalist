namespace FarmingCapitalist.Workers;

/// <summary>Curated ordinary-monster rewards, normalized for an hourly worker expedition.
/// Chances here are worker balance, NOT vanilla per-kill chances. Each bonus rolls independently.</summary>
internal static class WorkerExplorationLootCatalog
{
    private readonly record struct Drop(string Id, int Weight, int MinimumDepth = 0);

    // Provenance is installed Data/Monsters plus species getExtraDropItems.
    // No rock/container/forage rewards, invulnerable Spikers, armored Bugs,
    // dangerous variants, quest items, or player equipment/mastery modifiers.
    private static readonly Drop[] EarlyCore = { new("766", 50), new("684", 35), new("767", 15, 31) };
    private static readonly Drop[] MiddleBeforeGhostCore = { new("766", 45), new("684", 25), new("767", 30) };
    private static readonly Drop[] MiddleCore = { new("766", 40), new("684", 20), new("767", 25), new("768", 15, 51) };
    private static readonly Drop[] DeepCore = { new("766", 35), new("684", 15), new("767", 20), new("768", 15), new("769", 15) };
    private static readonly Drop[] MineResources = { new("382", 60), new("378", 20, 80), new("380", 20, 80) }; // Golem/Dust Spirit, Metal Head
    private static readonly Drop[] ShallowBonus = { new("286", 60), new("717", 40) }; // Duggy/Rock Crab, before Golems at 31
    private static readonly Drop[] MineUncommon =
    {
        new("286", 25), new("717", 20), new("86", 20), new("157", 15), new("273", 10), new("203", 10, 80),
    }; // Duggy, Rock Crab, Grub, Shadow Brute (203 = Strange Bun)
    private static readonly Drop[] MineRareGroups = { new("scroll", 65), new("artifact", 25), new("336", 10, 40) }; // Dust Spirit gold bar
    private static readonly Drop[] MineScrolls = { new("96", 40), new("97", 30, 40), new("98", 20, 80), new("99", 10, 120) };
    private static readonly Drop[] MineArtifacts = { new("105", 70), new("114", 30) }; // Duggy, Bug/Grub/Fly
    private static readonly Drop[] SkullCore = { new("766", 35), new("769", 30), new("768", 25), new("767", 10) };
    private static readonly Drop[] SkullBones = { new("580", 1), new("583", 1), new("584", 1) }; // infrequent Pepper Rex encounter
    private static readonly Drop[] SkullRare = { new("72", 40), new("337", 25), new("99", 20), new("485", 15) }; // Sludge, bat/slime, Mummy/Serpent
    private static readonly Drop[] VolcanoCore = { new("848", 55), new("881", 25), new("768", 15), new("766", 5) };
    private static readonly Drop[] HotHeadOres = { new("378", 1), new("380", 1) };
    private static readonly Drop[] SentryGems = { new("60", 1), new("62", 1), new("64", 1), new("66", 1), new("68", 1), new("70", 1), new("72", 1) };
    private static readonly Drop[] VolcanoScrolls = { new("98", 3), new("99", 1) }; // Hot Head/Tiger Slime

    // Area allowlists also protect saved queues. Progression affects new rolls,
    // not delivery of already earned loot; changing destination preserves Area.
    public static bool IsAllowed(string area, string itemId)
    {
        string id = itemId.StartsWith("(O)", StringComparison.Ordinal) ? itemId[3..] : itemId;
        if (id == "390") return false; // stone, even when a monster supplies it
        return area switch
        {
            WorkerExplorationAreaCatalog.Mines => id is "766" or "684" or "767" or "768" or "769"
                or "382" or "378" or "380" or "286" or "717" or "86" or "157" or "273" or "203"
                or "96" or "97" or "98" or "99" or "105" or "114" or "336" or "74",
            WorkerExplorationAreaCatalog.SkullCavern => id is "766" or "767" or "768" or "769" or "386" or "382"
                or "428" or "226" or "287" or "749" or "732" or "107" or "580" or "583" or "584"
                or "72" or "337" or "99" or "485" or "74",
            WorkerExplorationAreaCatalog.Volcano => id is "848" or "881" or "768" or "766" or "378" or "380"
                or "382" or "851" or "831" or "833" or "829" or "852" or "60" or "62" or "64"
                or "66" or "68" or "70" or "72" or "835" or "857" or "98" or "99",
            _ => false,
        };
    }

    private static string Choose(Drop[] pool, Random random, int depth)
    {
        int weight = random.Next(pool.Where(item => item.MinimumDepth <= depth).Sum(item => item.Weight));
        foreach (Drop item in pool)
        {
            if (item.MinimumDepth > depth) continue;
            weight -= item.Weight;
            if (weight < 0) return item.Id;
        }
        throw new InvalidOperationException("Exploration loot pool has no eligible entry.");
    }

    public static List<WorkerExplorationLoot> Roll(string area, Random random, int depth, bool slimeHutchBuilt)
    {
        if (!WorkerExplorationAreaCatalog.IsValid(area)) throw new ArgumentOutOfRangeException(nameof(area));
        List<WorkerExplorationLoot> loot = new();
        void Add(string id, int count = 1) => loot.Add(new() { Area = area, ItemId = "(O)" + id, Stack = count });
        void Bonus(double chance, string id, bool twoUnits = false)
        {
            if (random.NextDouble() < chance) Add(id, twoUnits ? random.Next(1, 3) : 1);
        }
        Drop[] core = area switch
        {
            WorkerExplorationAreaCatalog.Mines => depth >= 80 ? DeepCore : depth >= 51 ? MiddleCore : depth >= 40 ? MiddleBeforeGhostCore : EarlyCore,
            WorkerExplorationAreaCatalog.SkullCavern => SkullCore,
            _ => VolcanoCore,
        };
        Add(Choose(core, random, depth), random.Next(3, 6));
        switch (area)
        {
            case WorkerExplorationAreaCatalog.Mines:
                // Mean 4 core + .75 routine + .24 uncommon + .0096 rare +
                // .0004 gated shard = 5 units/hour: approx 80/20 by ITEM UNITS.
                if (random.NextDouble() < .5)
                    Add(Choose(depth < 31 ? ShallowBonus : MineResources, random, depth), random.Next(1, 3));
                if (random.NextDouble() < .24) Add(Choose(MineUncommon, random, depth));
                if (random.NextDouble() < .0096)
                {
                    string rare = Choose(MineRareGroups, random, depth);
                    Add(rare == "scroll" ? Choose(MineScrolls, random, depth)
                        : rare == "artifact" ? Choose(MineArtifacts, random, depth) : rare);
                }
                // Conservative worker gates: Scroll IV and global shard after bottom.
                if (depth >= 120) Bonus(.0004, "74");
                break;
            case WorkerExplorationAreaCatalog.SkullCavern:
                Bonus(.25, "386", true); // purple slimes/Iridium Crabs (no deep-bat assumption)
                Bonus(.12, "382", true); // Sludge
                Bonus(.10, "428"); // Mummy
                Bonus(.05, "226"); Bonus(.05, "287"); // Serpent
                Bonus(.04, "749"); Bonus(.015, "732"); // Carbon Ghost, Iridium Crab
                // Abstract 1.5% Pepper Rex encounter: egg/bones preserve 1:9 ratio.
                if (random.NextDouble() < .015)
                    Add(random.NextDouble() < .1 ? "107" : Choose(SkullBones, random, depth));
                if (random.NextDouble() < .008) Add(Choose(SkullRare, random, depth));
                if (depth >= 120) Bonus(.001, "74");
                break;
            case WorkerExplorationAreaCatalog.Volcano:
                if (random.NextDouble() < .10) Add(Choose(HotHeadOres, random, depth), random.Next(1, 3));
                Bonus(.10, "382", true); // Hot Head/Tiger
                Bonus(.075, "851"); // killed False Magma Cap, not forage
                Bonus(.10, "831", true); Bonus(.04, "833", true); // Magma Duggy/Hot Head/Tiger
                Bonus(.025, "829"); Bonus(.03, "852"); // Tiger, Lava Lurk
                if (random.NextDouble() < .015) Add(Choose(SentryGems, random, depth));
                Bonus(.002, "835"); // Tiger
                if (slimeHutchBuilt) Bonus(.003, "857");
                if (random.NextDouble() < .004) Add(Choose(VolcanoScrolls, random, depth));
                break;
        }
        return loot;
    }
}
