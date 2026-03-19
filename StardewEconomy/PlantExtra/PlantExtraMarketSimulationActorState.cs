namespace FarmingCapitalist
{
    public sealed class PlantExtraMarketSimulationActorState : ICategoryMarketActorState
    {
        public string ActorId { get; set; } = string.Empty;
        public float InfluenceScale { get; set; } = 1f;
        public float DemandBias { get; set; } = 0f;
        public int TrendDaysRemaining { get; set; } = 0;
        public bool TrendDrivesDemand { get; set; } = true;
        public List<string> FocusPlantExtraItemIds { get; set; } = new();

        IList<string> ICategoryMarketActorState.FocusItemIds
        {
            get => FocusPlantExtraItemIds;
            set => FocusPlantExtraItemIds = value is List<string> focusItemIds
                ? focusItemIds
                : value?.ToList() ?? new List<string>();
        }
    }
}
