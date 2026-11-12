using System;
using System.Collections.Generic;

namespace MyFreshMushroom
{
    public sealed class ExplorationService
    {
        private readonly InventoryService inventory;
        private readonly ProgressState progress;
        private readonly IGameplayActionRules actionRules;
        private readonly IReadOnlyDictionary<string, ForestAreaState> areas;
        private bool collecting;
        private readonly Dictionary<string, ExplorationTarget> targets = new Dictionary<string, ExplorationTarget>();
        private readonly HashSet<string> collectedTargets = new HashSet<string>();
        private readonly Dictionary<string, MushroomData> mushrooms = new Dictionary<string, MushroomData>();
        public string MushroomId { get; }
        public MushroomData FirstMushroom => mushrooms[MushroomId];
        public ForestAreaData FirstArea { get; }
        public ForestAreaState ActiveArea { get; private set; }
        public bool IsExploring => ActiveArea != null;
        public bool IsCollected => progress.FirstExplorationCollected;
        public int TargetCount => targets.Count;
        public int CollectedCount => collectedTargets.Count;
        public int RemainingCount => TargetCount - CollectedCount;
        public bool IsExitConfirmationOpen { get; private set; }
        public event Action Entered;
        public event Action Changed;
        public event Action CooldownChanged;
        public event Action<string, int> Collected;

        public ExplorationService(InventoryService inventory, ProgressState progress, ForestAreaData firstArea,
            string mushroomId, IEnumerable<MushroomData> catalogue, IReadOnlyDictionary<string, ForestAreaState> areas,
            IGameplayActionRules actionRules = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            if (string.IsNullOrWhiteSpace(mushroomId)) throw new ArgumentException("Invalid first exploration mushroom.");
            MushroomId = mushroomId;
            FirstArea = firstArea;
            this.actionRules = actionRules;
            this.areas = areas ?? throw new ArgumentNullException(nameof(areas));
            foreach (var mushroom in catalogue)
                mushrooms.Add(mushroom.Id, mushroom);
        }

        public bool CanEnter(ForestAreaState area) => !collecting && !IsExploring && area != null
            && !string.IsNullOrWhiteSpace(area.Definition.Id)
            && areas.TryGetValue(area.Definition.Id, out var owned) && ReferenceEquals(area, owned)
            && area.IsExplorationUnlocked && area.RemainingCooldownSeconds <= 0
            && float.IsFinite(area.Definition.ExplorationCooldownSeconds) && area.Definition.ExplorationCooldownSeconds >= 0
            && (actionRules == null || actionRules.AllowsEnteringExploration()
                && (IsCollected || area.Definition == FirstArea));

        public bool TryEnter(ForestAreaState area, IReadOnlyList<ExplorationTarget> mapTargets)
        {
            if (!CanEnter(area) || mapTargets == null || mapTargets.Count == 0) return false;
            var pending = new Dictionary<string, ExplorationTarget>();
            bool hasFirstMushroom = false;
            foreach (var target in mapTargets)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.Id) || target.Mushroom == null
                    || !mushrooms.TryGetValue(target.Mushroom.Id, out var mushroom) || mushroom != target.Mushroom
                    || target.FirstDiscoveryYield <= 0 || target.RepeatExplorationYield <= 0
                    || !pending.TryAdd(target.Id, target)) return false;
                hasFirstMushroom |= target.Mushroom.Id == MushroomId;
            }
            if (area.Definition == FirstArea && !IsCollected && !hasFirstMushroom) return false;
            targets.Clear();
            foreach (var pair in pending) targets.Add(pair.Key, pair.Value);
            collectedTargets.Clear();
            ActiveArea = area;
            if (area.Definition == FirstArea) Entered?.Invoke();
            Changed?.Invoke();
            return true;
        }

        public void Leave()
        {
            if (!IsExploring || collecting) return;
            ActiveArea.RemainingCooldownSeconds = ActiveArea.Definition.ExplorationCooldownSeconds;
            ActiveArea = null;
            IsExitConfirmationOpen = false;
            targets.Clear();
            collectedTargets.Clear();
            Changed?.Invoke();
        }

        public void Tick(double seconds)
        {
            if (collecting || !double.IsFinite(seconds) || seconds <= 0 || actionRules?.IsGameplayPaused == true) return;
            bool displayChanged = false;
            foreach (var area in areas.Values)
            {
                double previous = area.RemainingCooldownSeconds;
                if (previous <= 0) continue;
                area.RemainingCooldownSeconds = Math.Max(0, previous - seconds);
                displayChanged |= Math.Ceiling(previous) != Math.Ceiling(area.RemainingCooldownSeconds);
            }
            if (displayChanged) CooldownChanged?.Invoke();
        }

        public bool RequestLeave()
        {
            if (!IsExploring || collecting || IsExitConfirmationOpen || actionRules?.IsGameplayPaused == true) return false;
            IsExitConfirmationOpen = true;
            Changed?.Invoke();
            return true;
        }

        public void ContinueExploration()
        {
            if (!IsExitConfirmationOpen) return;
            IsExitConfirmationOpen = false;
            Changed?.Invoke();
        }

        public bool TryConfirmLeave()
        {
            if (!IsExploring || !IsExitConfirmationOpen || collecting || actionRules?.IsGameplayPaused == true) return false;
            Leave();
            return true;
        }

        public bool TryGetRemainingHint(out string hint)
        {
            hint = null;
            if (!IsExitConfirmationOpen || RemainingCount == 0) return false;
            foreach (var target in targets.Values)
            {
                if (IsTargetCollected(target.Id)) continue;
                hint = string.IsNullOrWhiteSpace(target.Hint)
                    ? "풍경 속에 일부 드러난 버섯이 있는지 살펴보세요." : target.Hint;
                return true;
            }
            return false;
        }

        public bool IsTargetCollected(string targetId) => targetId != null && collectedTargets.Contains(targetId);

        public bool TryCollect(string targetId)
        {
            if (collecting || ActiveArea == null || IsExitConfirmationOpen || targetId == null || IsTargetCollected(targetId)
                || !targets.TryGetValue(targetId, out var target)
                || actionRules != null && !actionRules.AllowsCollectingExplorationMushroom()) return false;
            collecting = true;
            try
            {
                var mushroom = target.Mushroom;
                bool firstDiscovery = !progress.IsDiscovered(mushroom.Id);
                bool firstExploration = ActiveArea.Definition == FirstArea && mushroom.Id == MushroomId && !IsCollected;
                int quantity = firstDiscovery ? target.FirstDiscoveryYield : target.RepeatExplorationYield;
                if (!inventory.TryAddSilently(mushroom.Id, ResourceKind.Mushroom, quantity)) return false;
                collectedTargets.Add(targetId);
                if (firstExploration) progress.FirstExplorationCollected = true;
                progress.TryDiscover(mushroom.Id);
                inventory.NotifyChanged();
                Collected?.Invoke(mushroom.Id, quantity);
                Changed?.Invoke();
                return true;
            }
            finally { collecting = false; }
        }
    }
}
