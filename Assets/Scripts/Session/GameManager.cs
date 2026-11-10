using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
        [SerializeField] private MushroomData firstExplorationMushroom;
        [SerializeField, Min(1)] private int firstExplorationReward = 3;
        [SerializeField] private ForestAreaData[] forestAreas = System.Array.Empty<ForestAreaData>();
        [SerializeField] private MushroomData[] additionalMushrooms = System.Array.Empty<MushroomData>();

        public MushroomData StartingMushroom => startingMushroom;
        public InventoryService Inventory { get; private set; }
        public ProgressState Progress { get; private set; }
        public CultivationService Cultivation { get; private set; }
        public SpawnProductionService SpawnProduction { get; private set; }
        public TutorialManager Tutorial { get; private set; }
        public ExplorationService Exploration { get; private set; }
        public IReadOnlyList<MushroomData> Mushrooms { get; private set; }
        public IReadOnlyDictionary<string, ForestAreaState> ForestAreas { get; private set; }
        public bool IsInitialized => Inventory != null;
        public bool IsGameplayPaused => Tutorial != null && Tutorial.IsGameplayPaused;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            // Menus and future exploration screens share this running-session clock.
            if (IsInitialized && !IsGameplayPaused) Cultivation.Tick(Time.unscaledDeltaTime);
        }

        // Views may initialize first, so initialization must not depend on Awake order.
        public bool Initialize()
        {
            if (IsInitialized)
                return true;

            if (forest == null || startingMushroom == null || firstExplorationMushroom == null
                || string.IsNullOrWhiteSpace(firstExplorationMushroom.Id) || firstExplorationReward <= 0
                || firstExplorationMushroom.Id == startingMushroom.Id || string.IsNullOrWhiteSpace(startingMushroom.Id)
                || string.IsNullOrWhiteSpace(startingMushroom.DisplayName)
                || startingMushroomCount < 0 || startingSpawnCount < 0)
            {
                Debug.LogError("GameManager requires forest, valid starting/exploration mushrooms and valid quantities.", this);
                return false;
            }

            var mushroomIds = new HashSet<string> { startingMushroom.Id };
            var mushrooms = new List<MushroomData> { startingMushroom };
            foreach (var recipe in spawnRecipes ?? System.Array.Empty<SpawnRecipeData>())
                if (recipe != null && recipe.IsValid && mushroomIds.Add(recipe.Mushroom.Id)) mushrooms.Add(recipe.Mushroom);
            if (mushroomIds.Add(firstExplorationMushroom.Id)) mushrooms.Add(firstExplorationMushroom);
            foreach (var mushroom in additionalMushrooms ?? System.Array.Empty<MushroomData>())
                if (mushroom != null && !string.IsNullOrWhiteSpace(mushroom.Id) && mushroomIds.Add(mushroom.Id))
                    mushrooms.Add(mushroom);

            var areaStates = new Dictionary<string, ForestAreaState>();
            foreach (var area in forestAreas ?? System.Array.Empty<ForestAreaData>())
            {
                if (area == null || string.IsNullOrWhiteSpace(area.Id) || areaStates.ContainsKey(area.Id))
                {
                    Debug.LogError("GameManager requires valid forest areas with unique IDs.", this);
                    return false;
                }
                areaStates.Add(area.Id, new ForestAreaState(area));
            }

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
                Tutorial = new TutorialManager(progress, shelf, workbench, tutorialRecipe);
            }
            Cultivation = new CultivationService(inventory, facilities, Tutorial);
            SpawnProduction = new SpawnProductionService(inventory, progress, spawnRecipes ?? System.Array.Empty<SpawnRecipeData>(), Tutorial);
            Tutorial?.Bind(Cultivation, SpawnProduction);
            Exploration = new ExplorationService(inventory, progress, firstExplorationMushroom.Id, firstExplorationReward, Tutorial);
            Tutorial?.BindExploration(Exploration);
            Mushrooms = mushrooms.AsReadOnly();
            ForestAreas = new ReadOnlyDictionary<string, ForestAreaState>(areaStates);
            Progress = progress;
            Inventory = inventory;
            return true;
        }

        private void OnDestroy() => Tutorial?.Dispose();
    }
}
