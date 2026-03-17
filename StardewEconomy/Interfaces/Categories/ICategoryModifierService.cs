using StardewValley;

namespace FarmingCapitalist
{
    /// <summary>
    /// Defines the common runtime contract for converting tracked supply scores into sell-price modifiers.
    /// Category-specific item detection should stay outside this interface.
    /// </summary>
    internal interface ICategoryModifierService
    {
        /// <summary>Gets whether this modifier service is enabled for live sell-price adjustment.</summary>
        bool ApplyToLiveSellPricing { get; }

        /// <summary>Gets whether a debug override is currently replacing normal supply math.</summary>
        bool HasDebugSellModifierOverride { get; }

        /// <summary>Gets the minimum allowed sell modifier for debug override validation.</summary>
        float MinimumAllowedSellModifier { get; }

        /// <summary>Gets the maximum allowed sell modifier for debug override validation.</summary>
        float MaximumAllowedSellModifier { get; }

        /// <summary>Set a debug sell modifier override if the supplied value is valid.</summary>
        bool TrySetDebugSellModifierOverride(float modifier, out string error);

        /// <summary>Clear any active debug sell modifier override.</summary>
        void ClearDebugSellModifierOverride();

        /// <summary>Get the active debug override if one is set.</summary>
        bool TryGetDebugSellModifierOverride(out float modifier);

        /// <summary>Get the current sell modifier for a concrete item instance.</summary>
        float GetSellModifier(Item? item);

        /// <summary>Get the current sell modifier for a normalized item ID and optional display name.</summary>
        float GetSellModifier(string? itemId, string? displayName = null);

        /// <summary>Build a concise debug summary showing the current score and modifier for an item.</summary>
        string GetDebugSummary(string itemId, string? displayName = null);
    }
}
