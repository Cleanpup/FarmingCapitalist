namespace FarmingCapitalist
{
    /// <summary>
    /// Defines the shared actor-state contract for category market simulation persistence.
    /// </summary>
    internal interface ICategoryMarketActorState
    {
        /// <summary>Get or set the stable actor identifier.</summary>
        string ActorId { get; set; }

        /// <summary>Get or set the actor's influence scale.</summary>
        float InfluenceScale { get; set; }

        /// <summary>Get or set the actor's demand bias.</summary>
        float DemandBias { get; set; }

        /// <summary>Get or set the remaining duration of the current trend.</summary>
        int TrendDaysRemaining { get; set; }

        /// <summary>Get or set whether the current trend drives demand instead of supply.</summary>
        bool TrendDrivesDemand { get; set; }

        /// <summary>Get or set the focused normalized item IDs for the active trend.</summary>
        IList<string> FocusItemIds { get; set; }
    }
}
