using UnityEngine;
using System.Collections.Generic;

namespace MyFreshMushroom
{
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private MushroomData startingMushroom;
        [SerializeField, Min(0)] private int startingMushroomCount;
        [SerializeField, Min(0)] private int startingSpawnCount = 3;
        [SerializeField] private ForestGridView forest;
        [SerializeField] private SpawnRecipeData[] spawnRecipes;
        [SerializeField] private bool enableTutorial = true;
        [SerializeField] private string tutorialShelfId = "starting-oyster-shelf";

        public MushroomData StartingMushroom => startingMushroom;
        public InventoryService Inventory { get; private set; }
        public ProgressState Progress { get; private set; }
        public CultivationService Cultivation { get; private set; }
        public SpawnProductionService SpawnProduction { get; private set; }
        public TutorialManager Tutorial { get; private set; }
        public bool IsInitialized => Inventory != null;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            // Menus and future exploration screens share this running-session clock.
            if (IsInitialized) Cultivation.Tick(Time.unscaledDeltaTime);
        }

        // Views may initialize first, so initialization must not depend on Awake order.
        public bool Initialize()
        {
            if (IsInitialized)
                return true;

            if (forest == null || startingMushroom == null || string.IsNullOrWhiteSpace(startingMushroom.Id)
                || string.IsNullOrWhiteSpace(startingMushroom.DisplayName)
                || startingMushroomCount < 0 || startingSpawnCount < 0)
            {
                Debug.LogError("GameManager requires a forest, valid starting mushroom and non-negative starting quantities.", this);
                return false;
            }

            var mushroomIds = new HashSet<string> { startingMushroom.Id };
            foreach (var recipe in spawnRecipes ?? System.Array.Empty<SpawnRecipeData>())
                if (recipe != null && recipe.IsValid) mushroomIds.Add(recipe.Mushroom.Id);
            var inventory = new InventoryService(mushroomIds);
            if (startingMushroomCount > 0)
                inventory.TryAdd(startingMushroom.Id, ResourceKind.Mushroom, startingMushroomCount);
            if (startingSpawnCount > 0)
                inventory.TryAdd(startingMushroom.Id, ResourceKind.Spawn, startingSpawnCount);

            var progress = new ProgressState();
            progress.TryDiscover(startingMushroom.Id);
            forest.Initialize();
            var facilities = new List<FacilityState>();
            foreach (var view in forest.Facilities) facilities.Add(view.State);
            if (enableTutorial)
            {
                FacilityState shelf = null;
                FacilityState workbench = null;
                SpawnRecipeData tutorialRecipe = null;
                foreach (var facility in facilities)
                {
                    if (facility.InstanceId == tutorialShelfId && facility.Definition.Kind == FacilityKind.CultivationShelf
                        && facility.Definition.SupportedMushroom == startingMushroom) shelf = facility;
                    if (facility.Definition.Kind == FacilityKind.SpawnWorkbench) workbench = facility;
                }
                foreach (var recipe in spawnRecipes ?? System.Array.Empty<SpawnRecipeData>())
                    if (recipe != null && recipe.Mushroom == startingMushroom) tutorialRecipe = recipe;
                if (shelf == null || workbench == null || tutorialRecipe == null)
                {
                    Debug.LogError("Tutorial requires its starting shelf, spawn workbench and starting mushroom recipe.", this);
                    return false;
                }
                Tutorial = new TutorialManager(progress, inventory, shelf, workbench, tutorialRecipe);
            }
            Cultivation = new CultivationService(inventory, facilities, Tutorial);
            SpawnProduction = new SpawnProductionService(inventory, progress, spawnRecipes ?? System.Array.Empty<SpawnRecipeData>(), Tutorial);
            Tutorial?.Bind(Cultivation, SpawnProduction);
            Progress = progress;
            Inventory = inventory;
            return true;
        }

        private void OnDestroy() => Tutorial?.Dispose();
    }
}
