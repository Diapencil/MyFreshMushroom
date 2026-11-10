using System;
using System.Collections.Generic;

namespace MyFreshMushroom
{
    public sealed class ForestRestorationService
    {
        private readonly InventoryService inventory;
        private readonly IReadOnlyDictionary<string, ForestAreaState> areas;
        private readonly IGameplayActionRules actionRules;
        public event Action<ForestAreaState> Restored;

        internal ForestRestorationService(InventoryService inventory, IReadOnlyDictionary<string, ForestAreaState> areas,
            IGameplayActionRules actionRules)
        {
            this.inventory = inventory;
            this.areas = areas;
            this.actionRules = actionRules;
        }

        public bool TryGetCosts(ForestAreaState area, int expectedStage, out IReadOnlyDictionary<string, int> costs)
        {
            costs = null;
            if (area == null || !areas.TryGetValue(area.Definition.Id, out var owned) || owned != area
                || expectedStage < 0 || area.CompletedRestorationStages != expectedStage
                || expectedStage >= area.Definition.RestorationStages.Count)
                return false;

            var entries = area.Definition.RestorationStages[expectedStage]?.Costs;
            if (entries == null || entries.Count == 0) return false;

            var totals = new Dictionary<string, int>();
            foreach (var entry in entries)
            {
                if (entry?.Mushroom == null || entry.Quantity <= 0
                    || !inventory.State.TryGetAmounts(entry.Mushroom.Id, out _)) return false;
                string id = entry.Mushroom.Id;
                totals.TryGetValue(id, out int total);
                if (total > int.MaxValue - entry.Quantity) return false;
                totals[id] = total + entry.Quantity;
            }
            costs = totals;
            return true;
        }

        public bool TryRestore(ForestAreaState area, int expectedStage)
        {
            if (actionRules?.IsGameplayPaused == true || !TryGetCosts(area, expectedStage, out var costs)
                || !inventory.TryDebitMushrooms(costs))
                return false;

            // Observers must see both the new stage and all debited balances.
            area.CompletedRestorationStages++;
            inventory.NotifyChanged();
            Restored?.Invoke(area);
            return true;
        }
    }
}
