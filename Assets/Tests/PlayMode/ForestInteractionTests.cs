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
        public IEnumerator TutorialHarvestAndProductionUseTheActionMenuAndModal()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var session = Object.FindFirstObjectByType<GameManager>();
            var forest = Object.FindFirstObjectByType<ForestGridView>();
            var input = Object.FindFirstObjectByType<WorldInteractionController>();
            var panel = Object.FindFirstObjectByType<SpawnProductionPanel>();
            var shelf = session.Tutorial.GuidanceTarget;
            forest.Selection.Select(shelf);

            GameObject.Find("Inoculate Button").GetComponent<Button>().onClick.Invoke();
            session.Tutorial.ContinueMessage();
            session.Cultivation.Tick(100);
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Ready));
            GameObject.Find("Harvest").GetComponent<Button>().onClick.Invoke();
            Assert.That(session.Tutorial.Step, Is.EqualTo(TutorialStep.FirstSpawnProduction));
            forest.Selection.Select(session.Tutorial.GuidanceTarget);
            Assert.That(panel.Content.gameObject.activeInHierarchy, Is.False);

            var open = GameObject.Find("Open Production").GetComponent<Button>();
            open.onClick.Invoke();
            yield return null;
            Assert.That(panel.Content.parent.name, Is.EqualTo("Popup Window"));
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
            Assert.That(open.IsInteractable(), Is.True);
            open.onClick.Invoke();
            GameObject.Find("Produce").GetComponent<Button>().onClick.Invoke();
            Assert.That(session.Tutorial.Step, Is.EqualTo(TutorialStep.SecondInoculation));
            Assert.That(panel.Content.gameObject.activeInHierarchy, Is.False);
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
            var text = panel.GetComponentInChildren<Text>();
            Assert.That(text.font != null && text.font.HasCharacter('M'), Is.True);
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
                StringAssert.Contains(view.State.Definition.DisplayName, text.text);
                Assert.That(view.GetComponent<LineRenderer>().enabled, Is.True);
            }
            var empty = forest.Grid.CellCenterToWorld(Vector2Int.zero);
            Assert.That(input.TrySelectAtScreenPosition(camera.WorldToScreenPoint(empty)), Is.True);
            Assert.That(forest.Selection.Selected, Is.Null);
            StringAssert.Contains("시설 선택", text.text);
        }

        [UnityTest]
        public IEnumerator UiOverlayBlocksWorldSelectionAtTheSamePointerPosition()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var forest = Object.FindFirstObjectByType<ForestGridView>();
            var input = Object.FindFirstObjectByType<WorldInteractionController>();
            var shelf = forest.Facilities.Single(v => v.State.Definition.Kind == FacilityKind.CultivationShelf);
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
