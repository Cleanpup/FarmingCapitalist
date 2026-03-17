using StardewModdingAPI;
using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Defines the shared trait lookup and debug contract for category-owned trait systems.
    /// </summary>
    internal interface ICategoryTraitService<TTrait>
    {
        /// <summary>Get the traits for a concrete item instance.</summary>
        TTrait GetTraits(Item? item);

        /// <summary>Get the traits for a normalized category item ID.</summary>
        TTrait GetTraits(string? itemId);

        /// <summary>Get whether a concrete item instance has the requested trait.</summary>
        bool HasTrait(Item? item, TTrait trait);

        /// <summary>Format a trait mask into a concise human-readable string.</summary>
        string FormatTraits(TTrait traits);

        /// <summary>Build a concise debug summary for a concrete item instance.</summary>
        string GetDebugSummary(Item? item);

        /// <summary>Log the debug summary for a concrete item instance.</summary>
        void LogTraits(Item? item, LogLevel level = LogLevel.Trace);
    }
}
