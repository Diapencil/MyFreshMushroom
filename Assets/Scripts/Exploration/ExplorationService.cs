using System;

namespace MyFreshMushroom
{
    public sealed class ExplorationService
    {
        private readonly InventoryService inventory;
        private readonly ProgressState progress;
        private readonly IGameplayActionRules actionRules;
        private bool collecting;
        public string MushroomId { get; }
        public int RewardQuantity { get; }
        public ForestAreaData FirstArea { get; }
        public ForestAreaState ActiveArea { get; private set; }
        public bool IsExploring => ActiveArea != null;
        public bool IsCollected => progress.FirstExplorationCollected;
        public event Action Entered;
        public event Action Changed;
        public event Action<string, int> Collected;

        public ExplorationService(InventoryService inventory, ProgressState progress, ForestAreaData firstArea,
            string mushroomId, int rewardQuantity, IGameplayActionRules actionRules = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            if (string.IsNullOrWhiteSpace(mushroomId) || rewardQuantity <= 0) throw new ArgumentException("Invalid exploration reward.");
            MushroomId = mushroomId;
            RewardQuantity = rewardQuantity;
            FirstArea = firstArea;
            this.actionRules = actionRules;
        }

        public bool CanEnter(ForestAreaState area) => !IsExploring && area != null && area.IsExplorationUnlocked
            && (actionRules == null || actionRules.AllowsEnteringExploration()
                && (IsCollected || area.Definition == FirstArea));

        public bool TryEnter(ForestAreaState area)
        {
            if (!CanEnter(area)) return false;
            ActiveArea = area;
            if (area.Definition == FirstArea) Entered?.Invoke();
            Changed?.Invoke();
            return true;
        }

        public void Leave()
        {
            if (!IsExploring) return;
            ActiveArea = null;
            Changed?.Invoke();
        }

        public bool TryCollect()
        {
            if (collecting || ActiveArea == null || ActiveArea.Definition != FirstArea || IsCollected
                || actionRules != null && !actionRules.AllowsCollectingExplorationMushroom()) return false;
            collecting = true;
            try
            {
                if (!inventory.TryAdd(MushroomId, ResourceKind.Mushroom, RewardQuantity)) return false;
                progress.FirstExplorationCollected = true;
                progress.TryDiscover(MushroomId);
                Collected?.Invoke(MushroomId, RewardQuantity);
                Changed?.Invoke();
                return true;
            }
            finally { collecting = false; }
        }
    }
}
