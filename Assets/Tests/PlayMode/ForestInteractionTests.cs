using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MyFreshMushroom.Tests
{
    public sealed class ForestInteractionTests
    {
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
            Assert.That(forest.Facilities.Count, Is.EqualTo(2));
            forest.Initialize();
            Assert.That(forest.Facilities.Count, Is.EqualTo(2));
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
