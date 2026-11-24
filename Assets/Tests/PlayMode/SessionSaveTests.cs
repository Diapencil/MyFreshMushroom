using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MyFreshMushroom.Tests
{
    public sealed class SessionSaveTests
    {
        private string directory;
        private string SavePath => Path.Combine(directory, "save.json");
        private static GameManager Session => Object.FindFirstObjectByType<GameManager>();
        private static ForestGridView Forest => Object.FindFirstObjectByType<ForestGridView>(FindObjectsInactive.Include);
        private static FacilityState StartingShelf => Forest.Facilities.Single(view =>
            view.State.InstanceId == "starting-oyster-shelf").State;

        [SetUp]
        public void UseTemporarySlot()
        {
            directory = Path.Combine(Application.temporaryCachePath, "save-test-" + System.Guid.NewGuid().ToString("N"));
            SaveManager.SaveDirectoryOverride = directory;
        }

        [UnityTearDown]
        public IEnumerator CleanTemporarySlot()
        {
            var scene = Session != null ? Session.gameObject.scene : default;
            if (scene.IsValid() && scene.isLoaded)
            {
                var empty = SceneManager.CreateScene("Save Test Cleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
                // The test runner or the next single-scene load will remove the active scene.
            }
            SaveManager.SaveDirectoryOverride = null;
            GameManager.TutorialEnabledOverride = null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator CheckpointBatchesChangesAndAFailedWritePreservesTheLastSave()
        {
            yield return Reload();
            string id = Session.StartingMushroom.Id;
            string initial = File.ReadAllText(SavePath);
            Assert.That(Session.Inventory.TryAdd(id, ResourceKind.Mushroom, 9), Is.True);
            Assert.That(Session.Inventory.TrySpend(id, ResourceKind.Mushroom, 2), Is.True);
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(initial));
            yield return new WaitForSecondsRealtime(5.1f);
            var lastSaved = File.ReadAllText(SavePath);
            Assert.That(JsonUtility.FromJson<SessionSaveData>(lastSaved).inventory
                .Single(entry => entry.mushroomId == id).mushrooms, Is.EqualTo(7));

            var blockedPath = SavePath + ".tmp";
            Directory.CreateDirectory(blockedPath);
            Assert.That(Session.Inventory.TryAdd(id, ResourceKind.Mushroom, 3), Is.True);
            Assert.That(Session.Saves.Flush(), Is.False);
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(lastSaved));
            Assert.That(Session.Inventory.State.GetAmounts(id).Mushrooms, Is.EqualTo(10));
            Directory.Delete(blockedPath);
            Assert.That(Session.Saves.Flush(), Is.True);
        }

        [UnityTest]
        public IEnumerator ReloadResumesWateredCropAndTutorialWithoutOfflineGrowthOrExtraResources()
        {
            yield return Reload();
            Assert.That(Session.Tutorial.Step, Is.EqualTo(TutorialStep.FirstInoculation));
            var mushroom = Session.StartingMushroom;
            Assert.That(Session.Inventory.State.GetAmounts(mushroom.Id).Spawn, Is.EqualTo(3));
            Assert.That(Session.Saves.Flush(), Is.True);
            Assert.That(File.Exists(SavePath), Is.True);

            Assert.That(Session.Cultivation.TryInoculate(StartingShelf), Is.True);
            Session.Tutorial.ContinueMessage();
            Session.Cultivation.Tick(mushroom.GrowthDurationSeconds);
            Assert.That(Session.Cultivation.TryHarvest(StartingShelf), Is.True);
            var recipe = Session.SpawnProduction.Recipes.Single(entry => entry.Mushroom == mushroom);
            Assert.That(Session.SpawnProduction.TryProduce(recipe, 1), Is.True);
            Assert.That(Session.Cultivation.TryInoculate(StartingShelf), Is.True);
            Session.Cultivation.Tick(StartingShelf.Cultivation.SecondsUntilWatering + 1);
            Assert.That(Session.Cultivation.TryWater(StartingShelf), Is.True);

            double elapsed = StartingShelf.Cultivation.ElapsedSeconds;
            var amounts = Session.Inventory.State.GetAmounts(mushroom.Id);
            Assert.That(Session.Saves.Flush(), Is.True);
            var save = JsonUtility.FromJson<SessionSaveData>(File.ReadAllText(SavePath));
            save.savedAtUtc = System.DateTime.UtcNow.AddHours(-3).ToString("O");
            File.WriteAllText(SavePath, JsonUtility.ToJson(save));
            yield return Reload();

            Assert.That(Session.Tutorial.Step, Is.EqualTo(TutorialStep.WateringComplete));
            Assert.That(Session.Tutorial.IsMessageOpen, Is.True);
            Assert.That(StartingShelf.Cultivation.ElapsedSeconds, Is.EqualTo(elapsed).Within(0.001));
            Assert.That(StartingShelf.Cultivation.HasBeenWatered, Is.True);
            Assert.That(Session.Inventory.State.GetAmounts(mushroom.Id).Spawn, Is.EqualTo(amounts.Spawn));
            Assert.That(Session.Inventory.State.GetAmounts(mushroom.Id).Mushrooms, Is.EqualTo(amounts.Mushrooms));
            Session.Tutorial.ContinueMessage();
            Session.Cultivation.Tick(mushroom.GrowthDurationSeconds);
            Assert.That(Session.Cultivation.TryHarvest(StartingShelf), Is.True);
            Assert.That(Session.Cultivation.TryHarvest(StartingShelf), Is.False);
            Assert.That(Session.Tutorial.Step, Is.EqualTo(TutorialStep.AwaitFirstExploration));

            Session.Tutorial.ContinueMessage();
            Object.FindFirstObjectByType<ExplorationManager>().Enter();
            Assert.That(Session.Exploration.IsExploring, Is.True);
            Session.Tutorial.ContinueMessage();
            var target = Object.FindFirstObjectByType<ExplorationMap>().Targets.First(entry =>
                entry.Mushroom.Id == Session.Exploration.MushroomId);
            Assert.That(Session.Exploration.TryCollect(target.Id), Is.True);
            int firstReward = Session.Inventory.State.GetAmounts(target.Mushroom.Id).Mushrooms;
            Assert.That(Session.Saves.Flush(), Is.True);
            yield return Reload();
            Assert.That(Session.Tutorial.Step, Is.EqualTo(TutorialStep.Completed));
            Assert.That(Session.Progress.FirstExplorationCollected, Is.True);
            Assert.That(Session.Exploration.IsTargetCollected(target.Id), Is.True);
            Session.Tutorial.ContinueMessage();
            Assert.That(Session.Exploration.TryCollect(target.Id), Is.False);
            Assert.That(Session.Inventory.State.GetAmounts(target.Mushroom.Id).Mushrooms, Is.EqualTo(firstReward));
        }

        [UnityTest]
        public IEnumerator ReloadRestoresFacilityRosterAreaUnlockAndTimers()
        {
            GameManager.TutorialEnabledOverride = false;
            yield return Reload();
            var definition = StartingShelf.Definition;
            var mushroomId = definition.SupportedMushroom.Id;
            Assert.That(Session.Inventory.TryAdd(mushroomId, ResourceKind.Mushroom, definition.ConstructionCost), Is.True);
            Vector2Int buildCell = FindPlacement(definition, false);
            Assert.That(Session.TryBuildFacility(definition, buildCell, false, out var built), Is.True);
            Vector2Int moveCell = FindPlacement(definition, true);
            Assert.That(Session.TryMoveFacility(built, moveCell, true), Is.True);
            Assert.That(Session.TryDemolishFacility(StartingShelf), Is.True);
            var workbench = Forest.Facilities.Single(view => view.State.Definition.Kind == FacilityKind.SpawnWorkbench).State;
            Vector2Int workbenchCell = FindPlacement(workbench.Definition, true);
            Assert.That(Session.TryMoveFacility(workbench, workbenchCell, true), Is.True);
            string builtId = built.InstanceId;
            string workbenchId = workbench.InstanceId;
            int facilityCount = Forest.Facilities.Count;

            var area = Session.ForestAreas["stump-forest"];
            Assert.That(Session.Restoration.TryGetCosts(area, 0, out var costs), Is.True);
            foreach (var cost in costs)
                Assert.That(Session.Inventory.TryAdd(cost.Key, ResourceKind.Mushroom, cost.Value), Is.True);
            Assert.That(Session.Restoration.TryRestore(area, 0), Is.True);
            var firstArea = Session.ForestAreas[Session.Exploration.FirstArea.Id];
            var map = firstArea.Definition.ExplorationMapPrefabs[0].GetComponent<ExplorationMap>();
            var target = new ExplorationTarget("test-target", Session.Exploration.FirstMushroom, 1, 1, "");
            Assert.That(Session.Exploration.TryEnter(firstArea, new[] { target }, map.SaveId), Is.True);
            Session.Exploration.Leave();
            Session.Exploration.Tick(10);
            double cooldown = firstArea.RemainingCooldownSeconds;
            Assert.That(Session.Saves.Flush(), Is.True);
            yield return Reload();

            Assert.That(Forest.Facilities.Count, Is.EqualTo(facilityCount));
            Assert.That(Forest.Facilities.Any(view => view.State.InstanceId == "starting-oyster-shelf"), Is.False);
            var restored = Forest.Facilities.Single(view => view.State.InstanceId == builtId).State;
            Assert.That(restored.Origin, Is.EqualTo(moveCell));
            Assert.That(restored.IsFlipped, Is.True);
            var restoredWorkbench = Forest.Facilities.Single(view => view.State.InstanceId == workbenchId).State;
            Assert.That(restoredWorkbench.Origin, Is.EqualTo(workbenchCell));
            Assert.That(restoredWorkbench.IsFlipped, Is.True);
            Assert.That(Session.ForestAreas[area.Definition.Id].CompletedRestorationStages, Is.EqualTo(1));
            Assert.That(Session.ForestAreas[area.Definition.Id].IsExplorationUnlocked, Is.True);
            Assert.That(Session.ForestAreas[firstArea.Definition.Id].RemainingCooldownSeconds,
                Is.InRange(cooldown - 1, cooldown));
            Assert.That(Session.Inventory.State.GetAmounts(mushroomId).Mushrooms, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TitleFlushesAndResetClearsBothPrimaryAndBackup()
        {
            yield return Reload();
            Assert.That(Session.Saves.Flush(), Is.True);
            string id = Session.StartingMushroom.Id;
            Session.Inventory.TryAdd(id, ResourceKind.Mushroom, 21);
            GameObject.Find("Settings Button").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Show Hint").GetComponent<Button>().onClick.Invoke();
            Assert.That(JsonUtility.FromJson<SessionSaveData>(File.ReadAllText(SavePath))
                .inventory.Single(entry => entry.mushroomId == id).mushrooms, Is.EqualTo(21));
            ClickConfirm();

            int oldScene = SceneManager.GetActiveScene().handle;
            GameObject.Find("Settings Button").GetComponent<Button>().onClick.Invoke();
            ClickConfirm();
            ClickConfirm();
            yield return new WaitUntil(() => SceneManager.GetActiveScene().handle != oldScene);
            Assert.That(Session.Inventory.State.GetAmounts(id).Mushrooms, Is.Zero);
            Assert.That(Session.Tutorial.Step, Is.EqualTo(TutorialStep.FirstInoculation));
            File.WriteAllText(SavePath, "{ invalid");
            yield return Reload();
            Assert.That(Session.Inventory.State.GetAmounts(id).Mushrooms, Is.Zero);
            Assert.That(Session.Inventory.State.GetAmounts(id).Spawn, Is.EqualTo(3));
            Assert.That(Session.Tutorial.IsMessageOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator InvalidPrimaryRecoversThePreviousSnapshotAndRepairsThePrimary()
        {
            yield return Reload();
            string id = Session.StartingMushroom.Id;
            Session.Inventory.TryAdd(id, ResourceKind.Mushroom, 8);
            Assert.That(Session.Saves.Flush(), Is.True);
            Session.Inventory.TryAdd(id, ResourceKind.Mushroom, 7);
            Assert.That(Session.Saves.Flush(), Is.True);
            var primary = JsonUtility.FromJson<SessionSaveData>(File.ReadAllText(SavePath));
            primary.facilities[0].x = int.MaxValue;
            File.WriteAllText(SavePath, JsonUtility.ToJson(primary));
            yield return Reload();
            Assert.That(Session.Inventory.State.GetAmounts(id).Mushrooms, Is.EqualTo(8));
            Assert.That(Session.Saves.Flush(), Is.True);
            Assert.That(JsonUtility.FromJson<SessionSaveData>(File.ReadAllText(SavePath))
                .inventory.Single(entry => entry.mushroomId == id).mushrooms, Is.EqualTo(8));
            yield return Reload();
            Assert.That(Session.Inventory.State.GetAmounts(id).Mushrooms, Is.EqualTo(8));
        }

        private static IEnumerator Reload()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
        }

        private static void ClickConfirm() => GameObject.Find("Confirm Action").GetComponent<Button>().onClick.Invoke();

        private static Vector2Int FindPlacement(FacilityData definition, bool flipped)
        {
            for (int x = Forest.Grid.MinCellCoordinate; x < Forest.Grid.MaxCellCoordinateExclusive; x++)
            for (int y = Forest.Grid.MinCellCoordinate; y < Forest.Grid.MaxCellCoordinateExclusive; y++)
            {
                var cell = new Vector2Int(x, y);
                if (Forest.CanPlace(definition, cell, flipped, null)) return cell;
            }
            throw new AssertionException("Expected a free facility placement.");
        }
    }
}
