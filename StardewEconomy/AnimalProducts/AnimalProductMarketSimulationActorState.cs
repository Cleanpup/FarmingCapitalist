namespace FarmingCapitalist
{
    public sealed class AnimalProductMarketSimulationActorState : ICategoryMarketActorState
    {
        public string ActorId { get; set; } = string.Empty;
        public float InfluenceScale { get; set; } = 1f;
        public float DemandBias { get; set; } = 0f;
        public int TrendDaysRemaining { get; set; } = 0;
        public bool TrendDrivesDemand { get; set; } = true;
        public List<string> FocusAnimalProductItemIds { get; set; } = new();

        IList<string> ICategoryMarketActorState.FocusItemIds
        {
            get => FocusAnimalProductItemIds;
            set => FocusAnimalProductItemIds = value is List<string> focusItemIds
                ? focusItemIds
                : value?.ToList() ?? new List<string>();
        }
    }
}
