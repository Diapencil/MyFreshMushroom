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
        public bool IsExploring { get; private set; }
        public bool IsCollected => progress.FirstExplorationCollected;
        public bool CanEnter => !IsExploring && (actionRules == null || actionRules.AllowsEnteringExploration());
        public event Action Entered;
        public event Action Changed;
        public event Action<string, int> Collected;

        public ExplorationService(InventoryService inventory, ProgressState progress, string mushroomId, int rewardQuantity,
            IGameplayActionRules actionRules = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            if (string.IsNullOrWhiteSpace(mushroomId) || rewardQuantity <= 0) throw new ArgumentException("Invalid exploration reward.");
            MushroomId = mushroomId;
            RewardQuantity = rewardQuantity;
            this.actionRules = actionRules;
        }

        public bool TryEnter()
        {
            if (!CanEnter) return false;
            IsExploring = true;
            Entered?.Invoke();
            Changed?.Invoke();
            return true;
        }

        public void Leave()
        {
            if (!IsExploring) return;
            IsExploring = false;
            Changed?.Invoke();
        }

        public bool TryCollect()
        {
            if (collecting || !IsExploring || IsCollected
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
