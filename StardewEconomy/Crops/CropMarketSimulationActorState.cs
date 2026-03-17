using System;

namespace FarmingCapitalist
{
    public sealed class CropMarketSimulationActorState : ICategoryMarketActorState
    {
        public string ActorId { get; set; } = string.Empty;
        public float InfluenceScale { get; set; } = 1f;
        public float DemandBias { get; set; } = 0f;
        public int TrendDaysRemaining { get; set; } = 0;
        public bool TrendDrivesDemand { get; set; } = true;
        public List<string> FocusCropProduceItemIds { get; set; } = new();

        // Maps the shared category focus list onto the existing save-compatible crop property.
        IList<string> ICategoryMarketActorState.FocusItemIds
        {
            get => FocusCropProduceItemIds;
            set => FocusCropProduceItemIds = value is List<string> focusItemIds
                ? focusItemIds
                : value?.ToList() ?? new List<string>();
        }
    }
}
