using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MyFreshMushroom
{
    [Serializable]
    internal sealed class SessionSaveData
    {
        internal const int CurrentVersion = 1;
        public int version;
        public string savedAtUtc;
        public InventorySaveEntry[] inventory;
        public string[] discoveredMushroomIds;
        public bool firstExplorationCollected;
        public FacilitySaveEntry[] facilities;
        public AreaSaveEntry[] areas;
        public TutorialSaveData tutorial;
        public JourneySaveData journey;

        internal static SessionSaveData Capture(InventoryService inventory, ProgressState progress,
            IEnumerable<FacilityState> facilities, IEnumerable<MushroomData> mushrooms,
            IEnumerable<ForestAreaState> areas, TutorialSaveData tutorial, ExplorationService exploration = null)
        {
            return new SessionSaveData
            {
                version = CurrentVersion,
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                inventory = mushrooms.Select(mushroom =>
                {
                    var amounts = inventory.State.GetAmounts(mushroom.Id);
                    return new InventorySaveEntry
                    {
                        mushroomId = mushroom.Id, mushrooms = amounts.Mushrooms, spawn = amounts.Spawn
                    };
                }).ToArray(),
                discoveredMushroomIds = mushrooms.Where(mushroom => progress.IsDiscovered(mushroom.Id))
                    .Select(mushroom => mushroom.Id).ToArray(),
                firstExplorationCollected = progress.FirstExplorationCollected,
                facilities = facilities.Select(facility => new FacilitySaveEntry
                {
                    instanceId = facility.InstanceId,
                    definitionId = facility.Definition.Id,
                    x = facility.Origin.x,
                    y = facility.Origin.y,
                    flipped = facility.IsFlipped,
                    cropMushroomId = facility.Cultivation.Mushroom?.Id ?? "",
                    cycle = facility.Cultivation.CycleNumber,
                    elapsedSeconds = facility.Cultivation.ElapsedSeconds,
                    watered = facility.Cultivation.HasBeenWatered
                }).ToArray(),
                areas = areas.Select(area => new AreaSaveEntry
                {
                    areaId = area.Definition.Id,
                    restorationStages = area.CompletedRestorationStages,
                    remainingCooldownSeconds = area.RemainingCooldownSeconds
                }).ToArray(),
                tutorial = tutorial,
                journey = exploration?.ActiveArea != null && !string.IsNullOrWhiteSpace(exploration.ActiveMapId)
                    ? new JourneySaveData
                    {
                        active = true,
                        areaId = exploration.ActiveArea.Definition.Id,
                        mapId = exploration.ActiveMapId,
                        collectedTargetIds = exploration.CollectedTargetIds.ToArray()
                    }
                    : new JourneySaveData()
            };
        }

        // Validate against current definitions before touching the live scene or invoking gameplay actions.
        internal bool TryRestore(IReadOnlyDictionary<string, MushroomData> mushrooms,
            IReadOnlyDictionary<string, FacilityData> definitions, IEnumerable<ForestAreaState> defaultAreas,
            ForestGridView forest, string startingMushroomId, string firstExplorationMushroomId,
            string tutorialShelfId, bool tutorialEnabled, out RestoredSession restored)
        {
            restored = null;
            if (version != CurrentVersion || !DateTimeOffset.TryParse(savedAtUtc, out _)
                || inventory == null || discoveredMushroomIds == null || facilities == null || areas == null
                || tutorial == null || journey == null || !Enum.IsDefined(typeof(TutorialStep), tutorial.step)
                || tutorial.expectedCycle < 0 || tutorial.expectedYield < 0) return false;

            var balances = new InventoryService(mushrooms.Keys);
            var resourceIds = new HashSet<string>();
            foreach (var entry in inventory)
            {
                if (entry == null || entry.mushroomId == null || !mushrooms.ContainsKey(entry.mushroomId)
                    || !resourceIds.Add(entry.mushroomId) || entry.mushrooms < 0 || entry.spawn < 0) return false;
                balances.State.SetAmounts(entry.mushroomId, new InventoryAmounts(entry.mushrooms, entry.spawn));
            }
            var progress = new ProgressState
            {
                FirstExplorationCollected = firstExplorationCollected, CurrentTutorialStep = tutorial.step
            };
            foreach (var id in discoveredMushroomIds)
                if (id == null || !mushrooms.ContainsKey(id) || !progress.TryDiscover(id)) return false;
            if (!resourceIds.Contains(startingMushroomId) || !progress.IsDiscovered(startingMushroomId)
                || firstExplorationCollected && !progress.IsDiscovered(firstExplorationMushroomId)
                || tutorial.step == TutorialStep.Completed && !firstExplorationCollected) return false;

            var layout = new List<FacilityState>();
            foreach (var entry in facilities)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.instanceId) || entry.definitionId == null
                    || !definitions.TryGetValue(entry.definitionId, out var definition)
                    || definition.Footprint.x <= 0 || definition.Footprint.y <= 0
                    || entry.cycle < 0 || entry.cycle == long.MaxValue
                    || !double.IsFinite(entry.elapsedSeconds) || entry.elapsedSeconds < 0) return false;
                MushroomData crop = null;
                if (!string.IsNullOrEmpty(entry.cropMushroomId))
                {
                    if (!mushrooms.TryGetValue(entry.cropMushroomId, out crop) || crop != definition.SupportedMushroom
                        || definition.Kind != FacilityKind.CultivationShelf || !crop.HasValidCultivationSettings
                        || entry.cycle == 0) return false;
                }
                else if (entry.watered || entry.elapsedSeconds != 0) return false;
                var state = new FacilityState(entry.instanceId, definition, new Vector2Int(entry.x, entry.y), entry.flipped);
                state.Cultivation.Restore(crop, entry.cycle, entry.elapsedSeconds, entry.watered);
                layout.Add(state);
            }
            if (!forest.IsValidLayout(layout)) return false;
            if (tutorialEnabled && !layout.Exists(facility => facility.Definition.Kind == FacilityKind.SpawnWorkbench))
                return false;
            var shelf = layout.Find(facility => facility.InstanceId == tutorialShelfId
                && facility.Definition.Kind == FacilityKind.CultivationShelf
                && facility.Definition.SupportedMushroom == mushrooms[startingMushroomId]);
            if (tutorialEnabled && tutorial.step < TutorialStep.AwaitFirstExploration)
            {
                if (shelf == null || !IsTutorialCropValid(shelf.Cultivation)) return false;
            }

            var areaStates = defaultAreas.ToDictionary(area => area.Definition.Id, area => new ForestAreaState(area.Definition));
            var areaIds = new HashSet<string>();
            foreach (var entry in areas)
            {
                if (entry == null || entry.areaId == null || !areaStates.TryGetValue(entry.areaId, out var area)
                    || !areaIds.Add(entry.areaId) || entry.restorationStages < 0
                    || entry.restorationStages > area.Definition.RestorationStages.Count
                    || !double.IsFinite(entry.remainingCooldownSeconds) || entry.remainingCooldownSeconds < 0) return false;
                for (int stage = 0; stage < entry.restorationStages; stage++)
                    if (area.Definition.RestorationStages[stage] == null) return false;
                area.CompletedRestorationStages = entry.restorationStages;
                area.RemainingCooldownSeconds = entry.remainingCooldownSeconds;
            }
            if (journey.active)
            {
                if (journey.areaId == null || !areaStates.TryGetValue(journey.areaId, out var area)
                    || !area.IsExplorationUnlocked || area.RemainingCooldownSeconds != 0
                    || journey.collectedTargetIds == null) return false;
                var map = area.Definition.FindExplorationMap(journey.mapId);
                if (map == null) return false;
                var targetIds = new HashSet<string>();
                foreach (var id in journey.collectedTargetIds)
                    if (string.IsNullOrWhiteSpace(id) || !targetIds.Add(id) || !map.HasTargetId(id)) return false;
            }
            restored = new RestoredSession(balances, progress, layout, areaStates, tutorial, journey.active ? journey : null);
            return true;
        }

        private bool IsTutorialCropValid(CultivationState crop)
        {
            bool needsCrop = tutorial.step == TutorialStep.FirstHarvest || tutorial.step == TutorialStep.WaterSecondCrop
                || tutorial.step == TutorialStep.WateringComplete || tutorial.step == TutorialStep.SecondHarvest;
            if (!needsCrop) return crop.Stage == GrowthStage.Empty;
            bool needsWater = tutorial.step == TutorialStep.WateringComplete || tutorial.step == TutorialStep.SecondHarvest;
            return crop.Stage != GrowthStage.Empty && crop.CycleNumber == tutorial.expectedCycle
                && crop.HarvestYield == tutorial.expectedYield && crop.HasBeenWatered == needsWater;
        }
    }

    [Serializable]
    internal sealed class InventorySaveEntry
    {
        public string mushroomId;
        public int mushrooms;
        public int spawn;
    }

    [Serializable]
    internal sealed class FacilitySaveEntry
    {
        public string instanceId;
        public string definitionId;
        public int x;
        public int y;
        public bool flipped;
        public string cropMushroomId;
        public long cycle;
        public double elapsedSeconds;
        public bool watered;
    }

    [Serializable]
    internal sealed class AreaSaveEntry
    {
        public string areaId;
        public int restorationStages;
        public double remainingCooldownSeconds;
    }

    [Serializable]
    internal sealed class TutorialSaveData
    {
        public TutorialStep step;
        public bool messageOpen;
        public bool waterMessageShown;
        public bool harvestMessageShown;
        public long expectedCycle;
        public int expectedYield;
    }

    [Serializable]
    internal sealed class JourneySaveData
    {
        public bool active;
        public string areaId = "";
        public string mapId = "";
        public string[] collectedTargetIds = Array.Empty<string>();
    }

    internal sealed class RestoredSession
    {
        internal readonly InventoryService Inventory;
        internal readonly ProgressState Progress;
        internal readonly List<FacilityState> Facilities;
        internal readonly Dictionary<string, ForestAreaState> Areas;
        internal readonly TutorialSaveData Tutorial;
        internal readonly JourneySaveData Journey;

        internal RestoredSession(InventoryService inventory, ProgressState progress, List<FacilityState> facilities,
            Dictionary<string, ForestAreaState> areas, TutorialSaveData tutorial, JourneySaveData journey)
        {
            Inventory = inventory;
            Progress = progress;
            Facilities = facilities;
            Areas = areas;
            Tutorial = tutorial;
            Journey = journey;
        }
    }
}
