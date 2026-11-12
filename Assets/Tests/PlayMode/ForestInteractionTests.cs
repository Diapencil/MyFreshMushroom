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
            Vector2 pointer = Camera.main.WorldToScreenPoint(bench.GetComponent<BoxCollider2D>().bounds.center);
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
                Vector3 centre = view.GetComponent<BoxCollider2D>().bounds.center;
                Vector3 viewport = camera.WorldToViewportPoint(centre);
                Assert.That(viewport.x, Is.InRange(0f, 1f));
                Assert.That(viewport.y, Is.InRange(0f, 1f));
                Assert.That(input.TrySelectAtScreenPosition(camera.WorldToScreenPoint(centre)), Is.True);
                Assert.That(forest.Selection.Selected, Is.SameAs(view.State));
                StringAssert.Contains(view.State.Definition.DisplayName, panel.Status);
                Assert.That(view.GetComponent<LineRenderer>().enabled, Is.True);
            }
            var empty = forest.Grid.CellCenterToWorld(Vector2Int.zero);
            Assert.That(input.TrySelectAtScreenPosition(camera.WorldToScreenPoint(empty)), Is.True);
            Assert.That(forest.Selection.Selected, Is.Null);
            StringAssert.Contains("시설 선택", panel.Status);
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
            Vector3 pointer = Camera.main.WorldToScreenPoint(bench.GetComponent<BoxCollider2D>().bounds.center);
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
        public IEnumerator OverlappingClickRegionsSelectTheFrontmostRenderedFacility()
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
            Vector3 pointer = Camera.main.WorldToScreenPoint(back.GetComponent<BoxCollider2D>().bounds.center);
            Assert.That(input.TrySelectAtScreenPosition(pointer), Is.True);
            Assert.That(forest.Selection.Selected, Is.SameAs(front.State));
        }
    }
}
