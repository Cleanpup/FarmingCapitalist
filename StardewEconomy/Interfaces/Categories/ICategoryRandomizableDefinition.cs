namespace FarmingCapitalist
{
    /// <summary>
    /// Defines shared metadata for save-profile-randomizable category definitions.
    /// </summary>
    internal interface ICategoryRandomizableDefinition<TTrait>
    {
        /// <summary>Get the stable category key used by save-profile data.</summary>
        string Key { get; }

        /// <summary>Get the category trait represented by this definition.</summary>
        TTrait Trait { get; }

        /// <summary>Get whether the category supports randomized buy modifiers.</summary>
        bool SupportsBuy { get; }

        /// <summary>Get whether the category supports randomized sell modifiers.</summary>
        bool SupportsSell { get; }

        /// <summary>Get whether the supplied trait mask matches this category definition.</summary>
        bool MatchesTraits(TTrait traits);
    }
}
