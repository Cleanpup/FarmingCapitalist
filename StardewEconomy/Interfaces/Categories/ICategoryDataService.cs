namespace FarmingCapitalist
{
    /// <summary>
    /// Defines the common runtime contract for reading and mutating tracked supply scores.
    /// Category-specific daily simulation or decay should stay outside this interface.
    /// </summary>
    internal interface ICategoryDataService
    {
        /// <summary>Gets the neutral score that represents an unchanged market state.</summary>
        float NeutralSupplyScore { get; }

        /// <summary>Load the active save data for the current save, creating a default snapshot if needed.</summary>
        void LoadOrCreateForCurrentSave();

        /// <summary>Clear any active in-memory state when leaving the save or returning to title.</summary>
        void ClearActiveData();

        /// <summary>Reset all tracked supply scores back to an empty, neutral state.</summary>
        void ResetTrackedSupply();

        /// <summary>Return a safe snapshot of all currently tracked normalized item IDs and scores.</summary>
        IReadOnlyDictionary<string, float> GetSnapshot();

        /// <summary>Replace the full tracked score set with the provided normalized values.</summary>
        bool ReplaceTrackedSupplyScores(IEnumerable<KeyValuePair<string, float>> supplyScores);

        /// <summary>Get the current score for a normalized item ID, or the neutral score if it is not tracked.</summary>
        float GetSupplyScore(string? itemId);

        /// <summary>Add supply pressure for a normalized item ID and return the updated score.</summary>
        float AddSupply(string itemId, float amount, string displayName, string source);
    }
}
