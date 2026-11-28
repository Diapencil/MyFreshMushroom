using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MyFreshMushroom.Tests
{
    public sealed class InitialSessionTests
    {
        [SetUp]
        public void IsolateStorage() => SaveManager.SaveDirectoryOverride = "";

        [TearDown]
        public void RestoreStorage() => SaveManager.SaveDirectoryOverride = null;

        [UnityTest]
        public IEnumerator SceneStartsWithThreeSpawnAndPanelTracksSuccessfulChanges()
        {
            yield return SceneManager.LoadSceneAsync(GameManager.MainScene, LoadSceneMode.Single);

            var session = Object.FindFirstObjectByType<GameManager>();
            var panel = Object.FindFirstObjectByType<InventoryPanel>();
            Assert.That(session, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            var oyster = panel.transform.Find("Oyster");
            var shiitake = panel.transform.Find("Shiitake");
            var background = panel.GetComponent<Image>().rectTransform;
            var mushrooms = oyster.Find("Mushroom Quantity").GetComponent<Text>();
            var spawn = oyster.Find("Spawn Quantity").GetComponent<Text>();
            // Unity's missing references can have a non-null managed wrapper.
            Assert.That(spawn.font != null, Is.True, "The inventory font must resolve to a live Unity object.");
            Assert.That(spawn.font.HasCharacter('0'), Is.True);
            string id = session.StartingMushroom.Id;

            Assert.That(session.IsInitialized, Is.True);
            Assert.That(session.Progress.IsDiscovered(id), Is.True);
            Assert.That(session.Inventory.State.GetAmounts(id).Mushrooms, Is.Zero);
            Assert.That(session.Inventory.State.GetAmounts(id).Spawn, Is.EqualTo(3));
            Assert.That(mushrooms.text, Is.EqualTo("0"));
            Assert.That(spawn.text, Is.EqualTo("3"));
            Assert.That(shiitake.gameObject.activeSelf, Is.False);
            Assert.That(background.rect.width, Is.EqualTo(96f).Within(0.01f));
            Vector2 originalPosition = background.anchoredPosition;
            Canvas.ForceUpdateCanvases();
            AssertInventoryIcon(oyster.Find("Mushroom Icon").GetComponent<Image>(), session.StartingMushroom.Icon);
            AssertInventoryIcon(oyster.Find("Spawn Icon").GetComponent<Image>(), session.StartingMushroom.SpawnIcon);
            Assert.That(spawn.preferredHeight, Is.GreaterThan(0));
            Assert.That(spawn.preferredHeight, Is.LessThanOrEqualTo(spawn.rectTransform.rect.height));

            session.Initialize();
            session.gameObject.SetActive(false);
            session.gameObject.SetActive(true);
            Assert.That(session.Inventory.State.GetAmounts(id).Spawn, Is.EqualTo(3));

            Assert.That(session.Inventory.TrySpend(id, ResourceKind.Spawn, 1), Is.True);
            Assert.That(spawn.text, Is.EqualTo("2"));
            Assert.That(session.Inventory.TryAdd(id, ResourceKind.Mushroom, 3), Is.True);
            Assert.That(mushrooms.text, Is.EqualTo("3"));

            panel.gameObject.SetActive(false);
            session.Inventory.TrySpend(id, ResourceKind.Spawn, 1);
            Assert.That(spawn.text, Is.EqualTo("2"));
            panel.gameObject.SetActive(true);
            Assert.That(spawn.text, Is.EqualTo("1"));

            Assert.That(session.Inventory.TrySpend(id, ResourceKind.Spawn, 2), Is.False);
            Assert.That(spawn.text, Is.EqualTo("1"));

            var shiitakeData = session.Mushrooms.Single(mushroom => mushroom.Id == "shiitake");
            Assert.That(session.Inventory.TryAdd(shiitakeData.Id, ResourceKind.Mushroom, 2), Is.True);
            Assert.That(shiitake.gameObject.activeSelf, Is.False);
            Assert.That(background.rect.width, Is.EqualTo(96f).Within(0.01f));
            Assert.That(session.Progress.TryDiscover(shiitakeData.Id), Is.True);
            Assert.That(shiitake.gameObject.activeInHierarchy, Is.True);
            Assert.That(background.rect.width, Is.EqualTo(184f).Within(0.01f));
            Assert.That(background.anchoredPosition, Is.EqualTo(originalPosition));
            AssertInventoryIcon(shiitake.Find("Mushroom Icon").GetComponent<Image>(), shiitakeData.Icon);
            AssertInventoryIcon(shiitake.Find("Spawn Icon").GetComponent<Image>(), shiitakeData.SpawnIcon);
            Assert.That(shiitake.Find("Mushroom Quantity").GetComponent<Text>().text, Is.EqualTo("2"));
            Assert.That(shiitake.Find("Spawn Quantity").GetComponent<Text>().text, Is.EqualTo("0"));
            Assert.That(mushrooms.text, Is.EqualTo("3"));
        }

        private static void AssertInventoryIcon(Image icon, Sprite sprite)
        {
            Assert.That(sprite, Is.Not.Null);
            Assert.That(icon.sprite, Is.SameAs(sprite));
            Assert.That(icon.enabled, Is.True);
            Vector2 nativeSize = icon.sprite.rect.size / icon.pixelsPerUnit;
            Assert.That(icon.rectTransform.sizeDelta.x, Is.EqualTo(nativeSize.x).Within(0.01f));
            Assert.That(icon.rectTransform.sizeDelta.y, Is.EqualTo(nativeSize.y).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator ReloadingSceneCreatesFreshSessionWithoutMutatingDefinition()
        {
            yield return SceneManager.LoadSceneAsync(GameManager.MainScene, LoadSceneMode.Single);
            var first = Object.FindFirstObjectByType<GameManager>();
            var definition = first.StartingMushroom;
            string originalId = definition.Id;
            string originalName = definition.DisplayName;
            first.Inventory.TrySpend(originalId, ResourceKind.Spawn, 3);
            first.Inventory.TryAdd(originalId, ResourceKind.Mushroom, 10);

            yield return SceneManager.LoadSceneAsync(GameManager.MainScene, LoadSceneMode.Single);
            var second = Object.FindFirstObjectByType<GameManager>();
            Assert.That(second.Inventory.State.GetAmounts(originalId).Mushrooms, Is.Zero);
            Assert.That(second.Inventory.State.GetAmounts(originalId).Spawn, Is.EqualTo(3));
            Assert.That(second.StartingMushroom.Id, Is.EqualTo(originalId));
            Assert.That(second.StartingMushroom.DisplayName, Is.EqualTo(originalName));
        }
    }
}
