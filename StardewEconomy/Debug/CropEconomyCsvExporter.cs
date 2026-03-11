using System.Globalization;
using System.Text;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Debug exporter for crop economy balancing data.
    /// </summary>
    internal sealed class CropEconomyCsvExporter
    {
        private const int SeasonLengthDays = 28;
        private const int TileCount = 50;
        private const string SeedShopId = "SeedShop";

        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;

        private readonly record struct CropEconomyCsvRow(
            string CropName,
            int ModifiedSeedPrice,
            int ModifiedSellPrice,
            double ProfitUsing50Tiles
        );

        public CropEconomyCsvExporter(IModHelper helper, IMonitor monitor)
        {
            _helper = helper;
            _monitor = monitor;
        }

        public bool TryExport(out string outputPath)
        {
            outputPath = string.Empty;

            try
            {
                List<CropEconomyCsvRow> rows = BuildRows();
                rows = rows
                    .OrderBy(row => row.CropName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string outputDirectory = Path.Combine(_helper.DirectoryPath, "debug");
                Directory.CreateDirectory(outputDirectory);

                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                outputPath = Path.Combine(outputDirectory, $"starecon_dump_{timestamp}.csv");

                using StreamWriter writer = new(outputPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.WriteLine("CropName,ModifiedSeedPrice,ModifiedSellPrice,ProfitUsing50Tiles");

                foreach (CropEconomyCsvRow row in rows)
                {
                    writer.WriteLine(
                        string.Join(
                            ",",
                            EscapeCsv(row.CropName),
                            row.ModifiedSeedPrice.ToString(CultureInfo.InvariantCulture),
                            row.ModifiedSellPrice.ToString(CultureInfo.InvariantCulture),
                            row.ProfitUsing50Tiles.ToString("0.##", CultureInfo.InvariantCulture)
                        )
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                _monitor.Log($"Failed to export crop economy CSV: {ex}", LogLevel.Error);
                outputPath = string.Empty;
                return false;
            }
        }

        private List<CropEconomyCsvRow> BuildRows()
        {
            List<CropEconomyCsvRow> rows = new();
            int farmingLevel = Math.Max(0, Game1.player?.FarmingLevel ?? 0);

            foreach ((string seedItemId, CropData cropData) in Game1.cropData)
            {
                if (!TryResolveCrop(seedItemId, cropData, out SObject seedItem, out SObject harvestedItem, out string cropName))
                    continue;

                int modifiedSeedPrice = GetModifiedSeedPrice(seedItem, dayOfMonth: 1);
                int modifiedSellPrice = GetModifiedSellPrice(harvestedItem, dayOfMonth: 1);
                double expectedYieldPerHarvest = GetExpectedYieldPerHarvest(cropData, farmingLevel);
                double profitUsing50Tiles = CalculateProfitUsing50Tiles(seedItem, harvestedItem, cropData, expectedYieldPerHarvest);

                rows.Add(new CropEconomyCsvRow(cropName, modifiedSeedPrice, modifiedSellPrice, profitUsing50Tiles));
            }

            return rows;
        }

        private bool TryResolveCrop(
            string seedItemId,
            CropData cropData,
            out SObject seedItem,
            out SObject harvestedItem,
            out string cropName
        )
        {
            seedItem = null!;
            harvestedItem = null!;
            cropName = string.Empty;

            if (cropData is null)
                return false;

            if (cropData.Seasons is null || cropData.Seasons.Count == 0)
                return false;

            if (!cropData.CountForMonoculture && !cropData.CountForPolyculture)
                return false;

            if (GetDaysToFirstHarvest(cropData) <= 0)
                return false;

            SObject? createdSeed = ItemRegistry.Create<SObject>("(O)" + seedItemId, allowNull: true);
            if (createdSeed is null || createdSeed.Category != SObject.SeedsCategory)
                return false;

            string harvestItemId = NormalizeObjectItemId(cropData.HarvestItemId);
            if (string.IsNullOrWhiteSpace(harvestItemId))
                return false;

            // Special vanilla handling: item 421 resolves to item 431 on harvest.
            if (string.Equals(harvestItemId, "421", StringComparison.OrdinalIgnoreCase))
                harvestItemId = "431";

            SObject? createdHarvest = ItemRegistry.Create<SObject>("(O)" + harvestItemId, allowNull: true);
            if (createdHarvest is null)
                return false;

            seedItem = createdSeed;
            harvestedItem = createdHarvest;
            cropName = string.IsNullOrWhiteSpace(createdHarvest.DisplayName)
                ? createdHarvest.Name
                : createdHarvest.DisplayName;
            return !string.IsNullOrWhiteSpace(cropName);
        }

        private double CalculateProfitUsing50Tiles(
            SObject seedItem,
            SObject harvestedItem,
            CropData cropData,
            double expectedYieldPerHarvest
        )
        {
            int daysToFirstHarvest = GetDaysToFirstHarvest(cropData);
            if (daysToFirstHarvest <= 0)
                return 0d;

            double seedCostPerTile = 0d;
            double revenuePerTile = 0d;

            seedCostPerTile += GetModifiedSeedPrice(seedItem, dayOfMonth: 1);
            int firstHarvestDay = 1 + daysToFirstHarvest;

            if (cropData.RegrowDays > 0)
            {
                if (firstHarvestDay <= SeasonLengthDays)
                {
                    for (int harvestDay = firstHarvestDay; harvestDay <= SeasonLengthDays; harvestDay += cropData.RegrowDays)
                    {
                        revenuePerTile += expectedYieldPerHarvest * GetModifiedSellPrice(harvestedItem, harvestDay);
                    }
                }
            }
            else
            {
                if (firstHarvestDay <= SeasonLengthDays)
                {
                    revenuePerTile += expectedYieldPerHarvest * GetModifiedSellPrice(harvestedItem, firstHarvestDay);

                    int replantDay = firstHarvestDay;
                    while (replantDay + daysToFirstHarvest <= SeasonLengthDays)
                    {
                        seedCostPerTile += GetModifiedSeedPrice(seedItem, replantDay);

                        int harvestDay = replantDay + daysToFirstHarvest;
                        revenuePerTile += expectedYieldPerHarvest * GetModifiedSellPrice(harvestedItem, harvestDay);
                        replantDay = harvestDay;
                    }
                }
            }

            return (revenuePerTile - seedCostPerTile) * TileCount;
        }

        private int GetModifiedSeedPrice(SObject seedItem, int dayOfMonth)
        {
            int vanillaPrice = Math.Max(1, seedItem.Price);
            EconomyContext context = BuildNeutralEconomyContext(dayOfMonth);
            return EconomyService.AdjustBuyPrice(
                vanillaPrice: vanillaPrice,
                item: seedItem,
                shopId: SeedShopId,
                context: context,
                cumulativePurchasedToday: 0,
                purchaseQuantity: 1
            );
        }

        private int GetModifiedSellPrice(SObject harvestedItem, int dayOfMonth)
        {
            int vanillaPrice = Math.Max(0, harvestedItem.Price);
            EconomyContext context = BuildNeutralEconomyContext(dayOfMonth);
            return EconomyService.AdjustSellPrice(vanillaPrice, harvestedItem, context);
        }

        private static int GetDaysToFirstHarvest(CropData cropData)
        {
            if (cropData.DaysInPhase is null || cropData.DaysInPhase.Count == 0)
                return 0;

            int totalDays = 0;
            foreach (int phaseDays in cropData.DaysInPhase)
            {
                if (phaseDays > 0)
                    totalDays += phaseDays;
            }

            return Math.Max(0, totalDays);
        }

        private static double GetExpectedYieldPerHarvest(CropData cropData, int farmingLevel)
        {
            string harvestItemId = NormalizeObjectItemId(cropData.HarvestItemId);
            if (string.Equals(harvestItemId, "421", StringComparison.OrdinalIgnoreCase))
                return 2d;

            int minStack = Math.Max(1, cropData.HarvestMinStack);
            int maxStack = Math.Max(minStack, cropData.HarvestMaxStack);

            if (cropData.HarvestMaxIncreasePerFarmingLevel > 0f)
                maxStack += (int)(Math.Max(0, farmingLevel) * cropData.HarvestMaxIncreasePerFarmingLevel);

            double expectedYield = (minStack + maxStack) / 2d;

            double extraHarvestChance = Math.Clamp(cropData.ExtraHarvestChance, 0d, 0.9d);
            if (extraHarvestChance > 0d)
                expectedYield += extraHarvestChance / (1d - extraHarvestChance);

            return Math.Max(1d, expectedYield);
        }

        private EconomyContext BuildNeutralEconomyContext(int dayOfMonth)
        {
            Farmer? player = Game1.player;
            return new EconomyContext
            {
                Season = Game1.currentSeason ?? string.Empty,
                DayOfMonth = Math.Clamp(dayOfMonth, 1, SeasonLengthDays),
                IsFestivalToday = false,
                FestivalTomorrow = false,
                FestivalTomorrowName = null,
                FarmingLevel = player?.FarmingLevel ?? 0,
                FishingLevel = player?.FishingLevel ?? 0,
                MiningLevel = player?.MiningLevel ?? 0,
                HeartsWithShopkeeper = 5
            };
        }

        private static string NormalizeObjectItemId(string? itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return string.Empty;

            string normalized = itemId.Trim();
            if (normalized.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(3);

            return normalized;
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool requiresQuotes = value.Contains(',')
                || value.Contains('"')
                || value.Contains('\n')
                || value.Contains('\r');

            if (!requiresQuotes)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
