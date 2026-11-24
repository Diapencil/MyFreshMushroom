using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MyFreshMushroom.Tests
{
    public sealed class ForestInteractionTests
    {
        [SetUp]
        public void UseUnrestrictedTestSession()
        {
            SaveManager.SaveDirectoryOverride = "";
            GameManager.TutorialEnabledOverride = false;
        }

        [TearDown]
        public void RestoreSessionSettings()
        {
            SaveManager.SaveDirectoryOverride = null;
            GameManager.TutorialEnabledOverride = null;
        }

        [UnityTest]
        public IEnumerator FacilitiesCanBeBuiltMovedFlippedAndDemolishedThroughTheActionMenu()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindFirstObjectByType<GameManager>();
            if (session.Tutorial != null) Assert.Ignore("This test exercises unrestricted facility management.");
            var forest = Object.FindFirstObjectByType<ForestGridView>();
            var input = Object.FindFirstObjectByType<WorldInteractionController>();
            var definition = session.BuildableFacilities.Single(data => data.SupportedMushroom == session.StartingMushroom);
            var mushroomId = definition.SupportedMushroom.Id;
            var occupiedCell = forest.Facilities.Single(view => view.State.InstanceId == "starting-oyster-shelf").State.Origin;
            var buildCell = new Vector2Int(-4, -2);
            var moveCell = new Vector2Int(-3, -2);
            int originalCount = forest.Facilities.Count;

            Vector2 ScreenPoint(Vector2Int cell) => Camera.main.WorldToScreenPoint(
                forest.transform.TransformPoint(forest.Grid.CellCenterToWorld(cell)));

            GameObject.Find("Build").GetComponent<Button>().onClick.Invoke();
            var buildOption = GameObject.Find("Confirm Action").GetComponent<Button>();
            Assert.That(buildOption.IsInteractable(), Is.False);
            Assert.That(GameObject.Find("Show Hint"), Is.Null, "The second build option requires discovery.");
            session.Inventory.TryAdd(mushroomId, ResourceKind.Mushroom, definition.ConstructionCost);
            Assert.That(buildOption.IsInteractable(), Is.True);
            buildOption.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That(input.IsPlacingFacility, Is.True);
            Assert.That(input.TrySelectAtScreenPosition(ScreenPoint(occupiedCell)), Is.False);
            Assert.That(session.TryBuildFacility(definition, new Vector2Int(7, 7), false, out _), Is.False);
            Assert.That(session.Inventory.State.GetAmounts(mushroomId).Mushrooms, Is.EqualTo(definition.ConstructionCost));
            Assert.That(input.TrySelectAtScreenPosition(ScreenPoint(buildCell)), Is.True);
            Assert.That(input.IsPlacingFacility, Is.False);
            Assert.That(forest.Facilities.Count, Is.EqualTo(originalCount + 1));
            Assert.That(session.Inventory.State.GetAmounts(mushroomId).Mushrooms, Is.Zero);

            var facility = forest.Selection.Selected;
            var view = forest.Facilities.Single(item => item.State == facility);
            Assert.That(facility.Origin, Is.EqualTo(buildCell));
            Assert.That(session.Cultivation.TryInoculate(facility), Is.True);
            session.Cultivation.Tick(facility.Cultivation.SecondsUntilWatering);
            Assert.That(session.Cultivation.TryWater(facility), Is.True);
            double elapsed = facility.Cultivation.ElapsedSeconds;

            AssertVisibleButtonLabel("Move Facility", "이동");
            AssertVisibleButtonLabel("Demolish Facility", "철거");
            Vector3 originalPosition = view.transform.position;
            Vector2 movePointer = Camera.main.WorldToScreenPoint(forest.transform.TransformPoint(
                forest.Grid.GridToWorld((Vector2)moveCell + (Vector2)facility.Footprint * 0.5f)));
            GameObject.Find("Move Facility").GetComponent<Button>().onClick.Invoke();
            var preview = forest.transform.Find("Facility Placement Preview");
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(preview.gameObject.activeSelf, Is.True);
            Assert.That(preview.position, Is.EqualTo(originalPosition));
            Assert.That(preview.GetComponent<SpriteRenderer>().color.a, Is.LessThan(1f));
            Assert.That(preview.Find("Growth").GetComponent<SpriteRenderer>().sprite,
                Is.SameAs(view.transform.Find("Growth").GetComponent<SpriteRenderer>().sprite));
            AssertVisibleButtonLabel("Flip Placement", "좌우 반전\n[R]");
            AssertVisibleButtonLabel("Cancel Placement", "취소\n[Esc]");
            GameObject.Find("Flip Placement").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Cancel Placement").GetComponent<Button>().onClick.Invoke();
            Assert.That(facility.Origin, Is.EqualTo(buildCell));
            Assert.That(facility.IsFlipped, Is.False);
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(view.transform.position, Is.EqualTo(originalPosition));
            Assert.That(preview.gameObject.activeSelf, Is.False);

            GameObject.Find("Move Facility").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Flip Placement").GetComponent<Button>().onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That(input.TrySelectAtScreenPosition(movePointer), Is.True);
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(preview.gameObject.activeSelf, Is.False);
            Assert.That(forest.Selection.Selected, Is.SameAs(facility));
            Assert.That(facility.Origin, Is.EqualTo(moveCell));
            Assert.That(facility.Footprint, Is.EqualTo(new Vector2Int(definition.Footprint.y, definition.Footprint.x)));
            Assert.That(view.GetComponent<SpriteRenderer>().flipX, Is.True);
            Assert.That(facility.Cultivation.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(facility.Cultivation.HasBeenWatered, Is.True);
            session.Cultivation.Tick(100);
            Assert.That(session.Cultivation.CanHarvest(facility), Is.True);

            GameObject.Find("Demolish Facility").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Cancel Confirmation").GetComponent<Button>().onClick.Invoke();
            Assert.That(forest.Selection.Selected, Is.SameAs(facility));
            GameObject.Find("Demolish Facility").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Confirm Action").GetComponent<Button>().onClick.Invoke();
            Assert.That(forest.Selection.Selected, Is.Null);
            Assert.That(forest.Facilities.Count, Is.EqualTo(originalCount));
            Assert.That(session.Cultivation.CanHarvest(facility), Is.False);
            Assert.That(session.Inventory.State.GetAmounts(mushroomId).Mushrooms, Is.Zero);
            session.Inventory.TryAdd(mushroomId, ResourceKind.Mushroom, definition.ConstructionCost);
            Assert.That(session.CanPlaceFacility(definition, moveCell, true), Is.True);
        }

        private static void AssertVisibleButtonLabel(string buttonName, string expectedText)
        {
            var label = GameObject.Find(buttonName).GetComponentInChildren<Text>();
            Assert.That(label, Is.Not.Null, buttonName);
            Assert.That(label.isActiveAndEnabled, Is.True, buttonName);
            Assert.That(label.text, Is.EqualTo(expectedText));
            Assert.That(label.font != null && label.font.HasCharacter(expectedText[0]), Is.True, buttonName);
        }

        [UnityTest]
        public IEnumerator SpawnWorkbenchCanBeMovedAndStillOpensProduction()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindFirstObjectByType<GameManager>();
            if (session.Tutorial != null) Assert.Ignore("This test exercises unrestricted facility management.");
            var forest = Object.FindFirstObjectByType<ForestGridView>();
            var input = Object.FindFirstObjectByType<WorldInteractionController>();
            var view = forest.Facilities.Single(item => item.State.Definition.Kind == FacilityKind.SpawnWorkbench);
            var workbench = view.State;
            var destination = workbench.Origin + Vector2Int.right;
            forest.Selection.Select(workbench);

            Assert.That(GameObject.Find("Demolish Facility"), Is.Null);
            Assert.That(session.TryDemolishFacility(workbench), Is.False);
            GameObject.Find("Move Facility").GetComponent<Button>().onClick.Invoke();
            Assert.That(input.IsPlacingFacility, Is.True);
            Assert.That(view.gameObject.activeSelf, Is.False);
            Vector2 pointer = Camera.main.WorldToScreenPoint(forest.transform.TransformPoint(
                forest.Grid.GridToWorld((Vector2)destination + (Vector2)workbench.Footprint * 0.5f)));
            Canvas.ForceUpdateCanvases();
            Assert.That(input.TrySelectAtScreenPosition(pointer), Is.True);
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(workbench.Origin, Is.EqualTo(destination));
            Assert.That(forest.Selection.Selected, Is.SameAs(workbench));

            GameObject.Find("Open Production").GetComponent<Button>().onClick.Invoke();
            Assert.That(Object.FindFirstObjectByType<SpawnProductionPanel>().Content.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator CultivationProductionAndExplorationUsePrefabUiWithoutReparenting()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var session = Object.FindFirstObjectByType<GameManager>();
            var forest = Object.FindFirstObjectByType<ForestGridView>();
            var input = Object.FindFirstObjectByType<WorldInteractionController>();
            var panel = Object.FindFirstObjectByType<SpawnProductionPanel>();
            var shelf = session.Tutorial?.GuidanceTarget
                ?? forest.Facilities.Single(view => view.State.InstanceId == "starting-oyster-shelf").State;
            forest.Selection.Select(shelf);

            GameObject.Find("Inoculate Button").GetComponent<Button>().onClick.Invoke();
            session.Tutorial?.ContinueMessage();
            session.Cultivation.Tick(100);
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Ready));
            GameObject.Find("Harvest").GetComponent<Button>().onClick.Invoke();
            if (session.Tutorial != null) Assert.That(session.Tutorial.Step, Is.EqualTo(TutorialStep.FirstSpawnProduction));
            forest.Selection.Select(session.Tutorial?.GuidanceTarget
                ?? forest.Facilities.Single(view => view.State.Definition.Kind == FacilityKind.SpawnWorkbench).State);
            Assert.That(panel.Content.gameObject.activeInHierarchy, Is.False);

            var contentParent = panel.Content.parent;
            var open = GameObject.Find("Open Production").GetComponent<Button>();
            open.onClick.Invoke();
            yield return null;
            Assert.That(panel.Content.parent.name, Is.EqualTo("Popup Window"));
            Assert.That(panel.Content.parent, Is.SameAs(contentParent), "Opening production must not reparent its content.");
            Assert.That(panel.Content.gameObject.activeInHierarchy, Is.True);
            Assert.That(open.IsInteractable(), Is.False);

            var backdrop = GameObject.Find("Popup Backdrop").GetComponent<Image>();
            Assert.That(backdrop.raycastTarget, Is.True);
            var bench = forest.Facilities.Single(view => view.State == forest.Selection.Selected);
            Vector2 pointer = Camera.main.WorldToScreenPoint(bench.GetComponent<PolygonCollider2D>().bounds.center);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pointer }, hits);
            Assert.That(hits.Any(hit => hit.gameObject == backdrop.gameObject), Is.True);
            Assert.That(input.TrySelectAtScreenPosition(pointer), Is.False);

            GameObject.Find("Close Popup").GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.Content.gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.Content.parent, Is.SameAs(contentParent));
            Assert.That(open.IsInteractable(), Is.True);
            open.onClick.Invoke();
            GameObject.Find("Produce").GetComponent<Button>().onClick.Invoke();
            if (session.Tutorial != null)
                Assert.That(session.Tutorial.Step, Is.EqualTo(TutorialStep.SecondInoculation));
            else
                GameObject.Find("Close Popup").GetComponent<Button>().onClick.Invoke();
            Assert.That(panel.Content.gameObject.activeInHierarchy, Is.False);

            // Exercise watering and the separate exploration confirmation popup with either tutorial setting.
            forest.Selection.Select(shelf);
            GameObject.Find("Inoculate Button").GetComponent<Button>().onClick.Invoke();
            session.Cultivation.Tick(shelf.Cultivation.SecondsUntilWatering);
            GameObject.Find("Water Button").GetComponent<Button>().onClick.Invoke();
            if (session.Tutorial != null) Assert.That(session.Tutorial.Step, Is.EqualTo(TutorialStep.WateringComplete));
            session.Tutorial?.ContinueMessage();
            session.Cultivation.Tick(100);
            GameObject.Find("Harvest").GetComponent<Button>().onClick.Invoke();
            if (session.Tutorial != null) Assert.That(session.Tutorial.Step, Is.EqualTo(TutorialStep.AwaitFirstExploration));
            session.Tutorial?.ContinueMessage();

            var exploration = Object.FindFirstObjectByType<ExplorationManager>();
            if (session.Tutorial == null)
            {
                forest.Selection.Select(null);
                var area = forest.transform.Find("Ground/" + session.Exploration.FirstArea.DisplayName);
                Canvas.ForceUpdateCanvases();
                Assert.That(input.TrySelectAtScreenPosition(Camera.main.WorldToScreenPoint(area.position)), Is.True);
            }
            var buttonParent = exploration.EnterButton.transform.parent;
            exploration.EnterButton.onClick.Invoke();
            Assert.That(session.Exploration.IsExploring, Is.True);
            session.Tutorial?.ContinueMessage();
            exploration.ReturnButton.onClick.Invoke();
            Assert.That(session.Exploration.IsExitConfirmationOpen, Is.True);
            Assert.That(panel.Content.gameObject.activeInHierarchy, Is.False);
            GameObject.Find("Cancel Confirmation").GetComponent<Button>().onClick.Invoke();
            Assert.That(session.Exploration.IsExitConfirmationOpen, Is.False);
            Assert.That(session.Exploration.IsExploring, Is.True);
            exploration.ReturnButton.onClick.Invoke();
            GameObject.Find("Confirm Action").GetComponent<Button>().onClick.Invoke();
            Assert.That(session.Exploration.IsExploring, Is.False);
            Assert.That(forest.gameObject.activeInHierarchy, Is.True);
            Assert.That(exploration.EnterButton.transform.parent, Is.SameAs(buttonParent));
            Assert.That(exploration.ReturnButton.transform.parent, Is.SameAs(buttonParent));
            Assert.That(panel.Content.parent, Is.SameAs(contentParent));
            yield return null;
            var menu = Object.FindFirstObjectByType<GameplayActionMenu>();
            int uiCount = buttonParent.root.GetComponentsInChildren<RectTransform>(true).Length;
            menu.enabled = false;
            menu.enabled = true;
            Assert.That(buttonParent.root.GetComponentsInChildren<RectTransform>(true).Length, Is.EqualTo(uiCount));
            Assert.That(Object.FindObjectsByType<GameplayPopup>(FindObjectsSortMode.None).Length, Is.EqualTo(2));
            GameObject.Find("Mushroom Catalogue").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("Popup Backdrop"), Is.Not.Null);
            GameObject.Find("Close Popup").GetComponent<Button>().onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator ProductionTabsShowSelectedRecipeAndLimitBatchesToOwnedMushrooms()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var session = Object.FindFirstObjectByType<GameManager>();
            if (session.Tutorial != null) Assert.Ignore("This test exercises unrestricted batch selection.");

            var forest = Object.FindFirstObjectByType<ForestGridView>();
            var panel = Object.FindFirstObjectByType<SpawnProductionPanel>();
            var first = session.SpawnProduction.Recipes.Single(recipe => recipe.Mushroom == session.StartingMushroom);
            var second = session.SpawnProduction.Recipes.First(recipe => recipe != first);
            Assert.That(session.Progress.TryDiscover(second.Mushroom.Id), Is.True);
            int owned = session.Inventory.State.GetAmounts(first.Mushroom.Id).Mushrooms;
            if (owned > 0)
                Assert.That(session.Inventory.TrySpend(first.Mushroom.Id, ResourceKind.Mushroom, owned), Is.True);
            int twoBatches = 2 * first.MushroomCost;
            Assert.That(session.Inventory.TryAdd(first.Mushroom.Id, ResourceKind.Mushroom, twoBatches), Is.True);

            forest.Selection.Select(forest.Facilities.Single(view => view.State.Definition.Kind == FacilityKind.SpawnWorkbench).State);
            GameObject.Find("Open Production").GetComponent<Button>().onClick.Invoke();
            yield return null;

            var tabs = panel.Content.Find("Recipe Tabs");
            var body = panel.Content.Find("Recipe Body");
            Assert.That(tabs.childCount, Is.EqualTo(2));
            var tabBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel.Content, tabs);
            var bodyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel.Content, body);
            Assert.That(tabBounds.min.y, Is.GreaterThanOrEqualTo(bodyBounds.max.y - 0.01f));

            var increase = body.Find("Increase").GetComponent<Button>();
            var decrease = body.Find("Decrease").GetComponent<Button>();
            var produce = body.Find("Produce").GetComponent<Button>();
            var cost = body.Find("Recipe Totals/Mushroom Cost").GetComponent<Text>();
            var output = body.Find("Recipe Totals/Spawn Output").GetComponent<Text>();
            var quantity = body.Find("Batch Count").GetComponent<Text>();
            var remaining = body.Find("Owned Mushrooms").GetComponent<Text>();
            var title = body.Find("Details").GetComponent<Text>();
            Assert.That(increase.interactable, Is.True);
            increase.onClick.Invoke();
            Assert.That(cost.text, Does.EndWith($"{twoBatches}개"));
            Assert.That(output.text, Does.EndWith($"{2 * first.SpawnOutput}개"));
            Assert.That(remaining.text, Does.EndWith($"{twoBatches}개"));
            Assert.That(increase.interactable, Is.False);
            increase.onClick.Invoke();
            Assert.That(quantity.text, Is.EqualTo("2회"));

            var secondTab = tabs.GetComponentsInChildren<Button>().Single(button => button.interactable);
            secondTab.onClick.Invoke();
            Assert.That(title.text, Is.EqualTo(second.Mushroom.DisplayName));
            Assert.That(quantity.text, Is.EqualTo("1회"));
            Assert.That(remaining.text, Does.EndWith("0개"));
            Assert.That(produce.interactable, Is.False);
            Assert.That(increase.interactable, Is.False);

            tabs.GetComponentsInChildren<Button>().Single(button => button.interactable).onClick.Invoke();
            increase.onClick.Invoke();
            decrease.onClick.Invoke();
            Assert.That(cost.text, Does.EndWith($"{first.MushroomCost}개"));
            increase.onClick.Invoke();
            produce.onClick.Invoke();
            Assert.That(remaining.text, Does.EndWith("0개"));
            Assert.That(quantity.text, Is.EqualTo("1회"));
            Assert.That(produce.interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator ForestHasExpectedLayoutAndBothFacilitiesCanBeSelected()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var forest = Object.FindFirstObjectByType<ForestGridView>();
            var input = Object.FindFirstObjectByType<WorldInteractionController>();
            var panel = Object.FindFirstObjectByType<FacilityPanel>();
            var camera = Camera.main;
            Assert.That(forest.Grid.Size, Is.EqualTo(16));
            Assert.That(forest.TileCount, Is.EqualTo(256));
            Assert.That(forest.Facilities.Count, Is.EqualTo(3));
            forest.Initialize();
            Assert.That(forest.Facilities.Count, Is.EqualTo(3));
            Assert.That(forest.TileCount, Is.EqualTo(256));
            var buttonLabel = panel.InoculateButtonTransform.GetComponentInChildren<Text>(true);
            Assert.That(buttonLabel.font != null && buttonLabel.font.HasCharacter('M'), Is.True);
            Physics2D.SyncTransforms();
            foreach (var view in forest.Facilities)
            {
                Assert.That(forest.Grid.ContainsFootprint(view.State.Origin, view.State.Definition.Footprint), Is.True);
                Vector3 centre = view.GetComponent<PolygonCollider2D>().bounds.center;
                Vector3 viewport = camera.WorldToViewportPoint(centre);
                Assert.That(viewport.x, Is.InRange(0f, 1f));
                Assert.That(viewport.y, Is.InRange(0f, 1f));
                Assert.That(input.TrySelectAtScreenPosition(camera.WorldToScreenPoint(centre)), Is.True);
                Assert.That(forest.Selection.Selected, Is.SameAs(view.State));
                StringAssert.Contains(view.State.Definition.DisplayName, panel.Status);
                Assert.That(view.GetComponent<LineRenderer>().enabled, Is.True);
            }
            var empty = forest.Grid.CellCenterToWorld(new Vector2Int(-8, -8));
            Assert.That(input.TrySelectAtScreenPosition(camera.WorldToScreenPoint(empty)), Is.True);
            Assert.That(forest.Selection.Selected, Is.Null);
            StringAssert.Contains("시설 선택", panel.Status);
        }

        [UnityTest]
        public IEnumerator TutorialSelectsTheHighlightedShelfThroughAGapInItsCollider()
        {
            GameManager.TutorialEnabledOverride = true;
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindFirstObjectByType<GameManager>();
            var forest = Object.FindFirstObjectByType<ForestGridView>();
            var input = Object.FindFirstObjectByType<WorldInteractionController>();
            var panel = Object.FindFirstObjectByType<FacilityPanel>();
            var actionMenu = Object.FindFirstObjectByType<GameplayActionMenu>();
            var shelf = forest.Facilities.Single(view => view.State == session.Tutorial.GuidanceTarget);
            var spriteBounds = shelf.GetComponent<SpriteRenderer>().bounds;
            Vector2 localMin = shelf.transform.InverseTransformPoint(spriteBounds.min);
            Vector2 localSize = shelf.transform.InverseTransformVector(spriteBounds.size);
            Vector2 leftEdge = localMin + new Vector2(localSize.x * 0.25f, 0);
            var collider = shelf.GetComponent<PolygonCollider2D>();
            collider.pathCount = 1;
            collider.SetPath(0, new[] { localMin, localMin + Vector2.up * localSize.y,
                leftEdge + Vector2.up * localSize.y, leftEdge });
            Physics2D.SyncTransforms();
            Canvas.ForceUpdateCanvases();

            var workbench = forest.Facilities.Single(view => view.State.Definition.Kind == FacilityKind.SpawnWorkbench);
            Assert.That(input.TrySelectAtScreenPosition(Camera.main.WorldToScreenPoint(workbench.transform.position)), Is.False);
            Assert.That(collider.OverlapPoint(spriteBounds.center), Is.False);
            Assert.That(input.TrySelectAtScreenPosition(Camera.main.WorldToScreenPoint(spriteBounds.center)), Is.True);
            Assert.That(forest.Selection.Selected, Is.SameAs(shelf.State));
            var inoculate = panel.InoculateButtonTransform.GetComponent<Button>();
            Assert.That(inoculate.IsInteractable(), Is.True);
            inoculate.onClick.Invoke();
            Assert.That(session.Tutorial.Step, Is.EqualTo(TutorialStep.FirstHarvest));

            Assert.That(GameObject.Find("Settings Button"), Is.Not.Null);
            session.Tutorial.ContinueMessage();
            session.Cultivation.Tick(session.StartingMushroom.GrowthDurationSeconds);
            var harvest = actionMenu.HarvestTarget.GetComponent<Button>();
            Assert.That(harvest.gameObject.activeInHierarchy && harvest.IsInteractable(), Is.True);
            harvest.onClick.Invoke();
            Assert.That(session.Tutorial.Step, Is.EqualTo(TutorialStep.FirstSpawnProduction));
        }

        [UnityTest]
        public IEnumerator UiOverlayBlocksWorldSelectionAtTheSamePointerPosition()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var forest = Object.FindFirstObjectByType<ForestGridView>();
            var input = Object.FindFirstObjectByType<WorldInteractionController>();
            var shelf = forest.Facilities.Single(v => v.State.InstanceId == "starting-oyster-shelf");
            var bench = forest.Facilities.Single(v => v.State.Definition.Kind == FacilityKind.SpawnWorkbench);
            forest.Selection.Select(shelf.State);
            var inventory = Object.FindFirstObjectByType<InventoryPanel>();
            var rect = (RectTransform)inventory.transform;
            Vector3 pointer = Camera.main.WorldToScreenPoint(bench.GetComponent<PolygonCollider2D>().bounds.center);
            rect.position = pointer + new Vector3(-30, 30, 0);
            Canvas.ForceUpdateCanvases();
            Physics2D.SyncTransforms();
            Assert.That(input.TrySelectAtScreenPosition(pointer), Is.False);
            Assert.That(forest.Selection.Selected, Is.SameAs(shelf.State));
            inventory.gameObject.SetActive(false);
            Assert.That(input.TrySelectAtScreenPosition(pointer), Is.True);
            Assert.That(forest.Selection.Selected, Is.SameAs(bench.State));
        }

        [UnityTest]
        public IEnumerator OverlappingClickRegionsSelectTheFrontmostFacilityWithAnOutlineHit()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var forest = Object.FindFirstObjectByType<ForestGridView>();
            var input = Object.FindFirstObjectByType<WorldInteractionController>();
            var back = forest.Facilities[0];
            var front = forest.Facilities[1];
            front.transform.position = back.transform.position;
            front.GetComponent<SpriteRenderer>().sortingOrder = back.SortingOrder + 10;
            Physics2D.SyncTransforms();
            Vector3 worldPoint = back.GetComponent<PolygonCollider2D>().bounds.center;
            Vector3 pointer = Camera.main.WorldToScreenPoint(worldPoint);
            Assert.That(input.TrySelectAtScreenPosition(pointer), Is.True);
            Assert.That(forest.Selection.Selected, Is.SameAs(front.State));

            // A concave outline can include the click in its bounds while leaving the point uncovered.
            var frontCollider = front.GetComponent<PolygonCollider2D>();
            Vector2 localPoint = front.transform.InverseTransformPoint(worldPoint);
            var outline = new Vector2[]
            {
                new Vector2(-2, -2), new Vector2(-2, 2), new Vector2(2, 2), new Vector2(2, 1),
                new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(2, -1), new Vector2(2, -2)
            };
            frontCollider.pathCount = 1;
            frontCollider.SetPath(0, outline.Select(point => point * 0.25f + localPoint).ToArray());
            Physics2D.SyncTransforms();
            Assert.That(frontCollider.bounds.Contains(worldPoint), Is.True);
            Assert.That(input.TrySelectAtScreenPosition(pointer), Is.True);
            Assert.That(forest.Selection.Selected, Is.SameAs(back.State));
        }
    }
}
