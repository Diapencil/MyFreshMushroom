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
        [SerializeField] private SpawnRecipeDatabase spawnRecipeDatabase;
        [SerializeField] private bool enableTutorial = true;
        [SerializeField] private string tutorialShelfId = "starting-oyster-shelf";
        [SerializeField] private MushroomData firstExplorationMushroom;
        [SerializeField] private ForestAreaData firstExplorationArea;
        // Used only when loading an old map without explicit collectible settings.
        [SerializeField, HideInInspector, Min(1)] private int firstExplorationReward = 3;
        [SerializeField] private ForestAreaData[] forestAreas = System.Array.Empty<ForestAreaData>();
        [SerializeField] private MushroomData[] additionalMushrooms = System.Array.Empty<MushroomData>();
        [SerializeField] private FacilityData[] buildableFacilities = System.Array.Empty<FacilityData>();

        public MushroomData StartingMushroom => startingMushroom;
        public InventoryService Inventory { get; private set; }
        public ProgressState Progress { get; private set; }
        public CultivationService Cultivation { get; private set; }
        public SpawnProductionService SpawnProduction { get; private set; }
        public TutorialManager Tutorial { get; private set; }
        public ExplorationService Exploration { get; private set; }
        public ForestRestorationService Restoration { get; private set; }
        public IReadOnlyList<MushroomData> Mushrooms { get; private set; }
        public IReadOnlyDictionary<string, ForestAreaState> ForestAreas { get; private set; }
        public bool IsInitialized => Inventory != null;
        public bool IsGameplayPaused => Tutorial != null && Tutorial.IsGameplayPaused;
        public IReadOnlyList<FacilityData> BuildableFacilities => buildableFacilities;
        public bool CanManageFacilities => IsInitialized && !IsGameplayPaused && !Exploration.IsExploring && !forest.IsPresentingRestoration
            && (Tutorial == null || Tutorial.Step == TutorialStep.Completed);
        internal int LegacyFirstExplorationReward => firstExplorationReward;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            // Menus and future exploration screens share this running-session clock.
            if (!IsInitialized || IsGameplayPaused) return;
            Exploration.Tick(Time.unscaledDeltaTime);
            Cultivation.Tick(Time.unscaledDeltaTime);
        }

        // Views may initialize first, so initialization must not depend on Awake order.
        public bool Initialize()
        {
            if (IsInitialized)
                return true;

            if (spawnRecipeDatabase == null)
            {
                Debug.LogError("GameManager requires a spawn recipe database.", this);
                return false;
            }
            var spawnRecipes = spawnRecipeDatabase.Recipes;

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
            foreach (var recipe in spawnRecipes)
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
                foreach (var recipe in spawnRecipes)
                    if (recipe != null && recipe.Mushroom == startingMushroom) tutorialRecipe = recipe;
                if (shelf == null || workbench == null || tutorialRecipe == null)
                {
                    Debug.LogError("Tutorial requires its starting shelf, spawn workbench and starting mushroom recipe.", this);
                    return false;
                }
                Tutorial = new TutorialManager(progress, shelf, workbench, tutorialRecipe);
            }
            Cultivation = new CultivationService(inventory, forest.Selection.Registered, Tutorial);
            SpawnProduction = new SpawnProductionService(inventory, progress, spawnRecipes, Tutorial);
            Tutorial?.Bind(Cultivation, SpawnProduction);
            Exploration = new ExplorationService(inventory, progress, firstExplorationArea,
                firstExplorationMushroom.Id, mushrooms, areaStates, Tutorial);
            Tutorial?.BindExploration(Exploration);
            Mushrooms = mushrooms.AsReadOnly();
            ForestAreas = new ReadOnlyDictionary<string, ForestAreaState>(areaStates);
            Restoration = new ForestRestorationService(inventory, ForestAreas, Tutorial);
            Progress = progress;
            Inventory = inventory;
            return true;
        }

        public bool IsFacilityUnlocked(FacilityData definition)
        {
            return IsInitialized && definition != null && definition.Kind == FacilityKind.CultivationShelf
                && definition.Sprite != null && definition.SupportedMushroom != null && definition.ConstructionCost > 0
                && System.Array.IndexOf(buildableFacilities, definition) >= 0
                && Progress.IsDiscovered(definition.SupportedMushroom.Id);
        }

        public bool CanAffordFacility(FacilityData definition)
        {
            return IsFacilityUnlocked(definition) && Inventory.CanAfford(definition.SupportedMushroom.Id,
                ResourceKind.Mushroom, definition.ConstructionCost);
        }

        public bool CanManageFacility(FacilityState facility)
        {
            return CanManageFacilities && facility != null && facility.Definition.Kind == FacilityKind.CultivationShelf
                && forest.Selection.Registered.Contains(facility);
        }

        public bool CanPlaceFacility(FacilityData definition, Vector2Int origin, bool flipped, FacilityState moving = null)
        {
            return CanManageFacilities
                && (moving == null ? CanAffordFacility(definition) : CanManageFacility(moving) && moving.Definition == definition)
                && forest.CanPlace(definition, origin, flipped, moving);
        }

        public bool TryBuildFacility(FacilityData definition, Vector2Int origin, bool flipped, out FacilityState facility)
        {
            facility = null;
            if (!CanPlaceFacility(definition, origin, flipped) || Cultivation.IsProcessingAction) return false;

            var state = new FacilityState(System.Guid.NewGuid().ToString("N"), definition, origin, flipped);
            forest.AddFacility(state);
            // Publish the inventory change only once the new facility is registered.
            if (!Inventory.TrySpendSilently(definition.SupportedMushroom.Id, ResourceKind.Mushroom,
                definition.ConstructionCost))
            {
                forest.RemoveFacility(state);
                return false;
            }
            facility = state;
            Inventory.NotifyChanged();
            return true;
        }

        public bool TryMoveFacility(FacilityState facility, Vector2Int origin, bool flipped)
        {
            if (facility == null || !CanPlaceFacility(facility.Definition, origin, flipped, facility)
                || Cultivation.IsProcessingAction) return false;
            forest.MoveFacility(facility, origin, flipped);
            return true;
        }

        public bool TryDemolishFacility(FacilityState facility)
        {
            if (!CanManageFacility(facility) || Cultivation.IsProcessingAction) return false;
            forest.RemoveFacility(facility);
            return true;
        }

        private void OnDestroy() => Tutorial?.Dispose();
    }
}
