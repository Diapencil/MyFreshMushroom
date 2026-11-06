using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MyFreshMushroom.Tests
{
    public sealed class InitialSessionTests
    {
        [UnityTest]
        public IEnumerator SceneStartsWithThreeSpawnAndPanelTracksSuccessfulChanges()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);

            var session = Object.FindFirstObjectByType<GameManager>();
            var panel = Object.FindFirstObjectByType<InventoryPanel>();
            Assert.That(session, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            var text = panel.GetComponentInChildren<Text>();
            Assert.That(text, Is.Not.Null);
            // Unity's missing references can have a non-null managed wrapper.
            Assert.That(text.font != null, Is.True, "The inventory font must resolve to a live Unity object.");
            Assert.That(text.font.HasCharacter('M'), Is.True);
            string id = session.StartingMushroom.Id;

            Assert.That(session.IsInitialized, Is.True);
            Assert.That(session.Progress.IsDiscovered(id), Is.True);
            Assert.That(session.Inventory.State.GetAmounts(id).Mushrooms, Is.Zero);
            Assert.That(session.Inventory.State.GetAmounts(id).Spawn, Is.EqualTo(3));
            StringAssert.Contains("버섯: 0개", text.text);
            StringAssert.Contains("종균: 3개", text.text);
            Canvas.ForceUpdateCanvases();
            Assert.That(text.preferredHeight, Is.GreaterThan(0));
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height));

            session.Initialize();
            session.gameObject.SetActive(false);
            session.gameObject.SetActive(true);
            Assert.That(session.Inventory.State.GetAmounts(id).Spawn, Is.EqualTo(3));

            Assert.That(session.Inventory.TrySpend(id, ResourceKind.Spawn, 1), Is.True);
            StringAssert.Contains("종균: 2개", text.text);
            Assert.That(session.Inventory.TryAdd(id, ResourceKind.Mushroom, 3), Is.True);
            StringAssert.Contains("버섯: 3개", text.text);

            panel.gameObject.SetActive(false);
            string previousText = text.text;
            session.Inventory.TrySpend(id, ResourceKind.Spawn, 1);
            Assert.That(text.text, Is.EqualTo(previousText));
            panel.gameObject.SetActive(true);
            StringAssert.Contains("종균: 1개", text.text);

            previousText = text.text;
            Assert.That(session.Inventory.TrySpend(id, ResourceKind.Spawn, 2), Is.False);
            Assert.That(text.text, Is.EqualTo(previousText));
        }

        [UnityTest]
        public IEnumerator ReloadingSceneCreatesFreshSessionWithoutMutatingDefinition()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            var first = Object.FindFirstObjectByType<GameManager>();
            var definition = first.StartingMushroom;
            string originalId = definition.Id;
            string originalName = definition.DisplayName;
            first.Inventory.TrySpend(originalId, ResourceKind.Spawn, 3);
            first.Inventory.TryAdd(originalId, ResourceKind.Mushroom, 10);

            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            var second = Object.FindFirstObjectByType<GameManager>();
            Assert.That(second.Inventory.State.GetAmounts(originalId).Mushrooms, Is.Zero);
            Assert.That(second.Inventory.State.GetAmounts(originalId).Spawn, Is.EqualTo(3));
            Assert.That(second.StartingMushroom.Id, Is.EqualTo(originalId));
            Assert.That(second.StartingMushroom.DisplayName, Is.EqualTo(originalName));
        }
    }
}
