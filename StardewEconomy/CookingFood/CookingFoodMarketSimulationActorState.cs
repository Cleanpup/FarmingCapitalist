namespace FarmingCapitalist
{
    public sealed class CookingFoodMarketSimulationActorState : ICategoryMarketActorState
    {
        public string ActorId { get; set; } = string.Empty;
        public float InfluenceScale { get; set; } = 1f;
        public float DemandBias { get; set; } = 0f;
        public int TrendDaysRemaining { get; set; } = 0;
        public bool TrendDrivesDemand { get; set; } = true;
        public List<string> FocusCookingFoodItemIds { get; set; } = new();

        IList<string> ICategoryMarketActorState.FocusItemIds
        {
            get => FocusCookingFoodItemIds;
            set => FocusCookingFoodItemIds = value is List<string> focusItemIds
                ? focusItemIds
                : value?.ToList() ?? new List<string>();
        }
    }
}
