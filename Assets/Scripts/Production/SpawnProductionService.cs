using System;
using System.Collections.Generic;

namespace MyFreshMushroom
{
    public sealed class SpawnProductionService
    {
        private readonly InventoryService inventory;
        private readonly ProgressState progress;
        private readonly List<SpawnRecipeData> recipes;
        private bool producing;
        private readonly IGameplayActionRules actionRules;
        public event Action<SpawnRecipeData, int> Produced;
        public IReadOnlyList<SpawnRecipeData> Recipes { get; }

        public SpawnProductionService(InventoryService inventory, ProgressState progress, IEnumerable<SpawnRecipeData> recipes,
            IGameplayActionRules actionRules = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.actionRules = actionRules;
            this.recipes = new List<SpawnRecipeData>();
            var ids = new HashSet<string>();
            foreach (var recipe in recipes ?? throw new ArgumentNullException(nameof(recipes)))
            {
                if (recipe == null || !recipe.IsValid || !ids.Add(recipe.Mushroom.Id))
                    throw new ArgumentException("Spawn recipes must be valid and have unique mushroom IDs.", nameof(recipes));
                this.recipes.Add(recipe);
            }
            Recipes = this.recipes.AsReadOnly();
        }

        public bool IsAvailable(SpawnRecipeData recipe)
        {
            return recipe != null && recipes.Contains(recipe) && recipe.IsValid
                && progress.IsDiscovered(recipe.Mushroom.Id);
        }

        public bool TryGetTotals(SpawnRecipeData recipe, int batches, out int mushroomCost, out int spawnOutput)
        {
            mushroomCost = spawnOutput = 0;
            if (batches <= 0 || !IsAvailable(recipe)) return false;
            long cost = (long)recipe.MushroomCost * batches;
            long output = (long)recipe.SpawnOutput * batches;
            if (cost > int.MaxValue || output > int.MaxValue) return false;
            mushroomCost = (int)cost;
            spawnOutput = (int)output;
            return true;
        }

        public bool CanProduce(SpawnRecipeData recipe, int batches)
        {
            return CanChooseBatchCount(recipe, batches) && TryGetTotals(recipe, batches, out int cost, out int output)
                && inventory.CanExchangeMushroomsForSpawn(recipe.Mushroom.Id, cost, output);
        }

        public bool CanChooseBatchCount(SpawnRecipeData recipe, int batches)
        {
            return TryGetTotals(recipe, batches, out _, out _)
                && (actionRules == null || actionRules.AllowsProduction(recipe, batches));
        }

        public bool TryProduce(SpawnRecipeData recipe, int batches)
        {
            if (producing || !CanChooseBatchCount(recipe, batches)
                || !TryGetTotals(recipe, batches, out int cost, out int output)) return false;
            producing = true;
            try
            {
                if (!inventory.TryExchangeMushroomsForSpawn(recipe.Mushroom.Id, cost, output)) return false;
                Produced?.Invoke(recipe, batches);
                return true;
            }
            finally { producing = false; }
        }
    }
}
