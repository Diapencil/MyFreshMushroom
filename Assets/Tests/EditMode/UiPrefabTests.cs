using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MyFreshMushroom.Tests
{
    public sealed class UiPrefabTests
    {
        [TestCase("Assets/Prefabs/UI/HUD/ContextActions.prefab")]
        [TestCase("Assets/Prefabs/UI/HUD/InventoryPanel.prefab")]
        [TestCase("Assets/Prefabs/UI/Popups/InformationPopup.prefab")]
        [TestCase("Assets/Prefabs/UI/Popups/ProductionPopup.prefab")]
        [TestCase("Assets/Prefabs/UI/Elements/RecipeButton.prefab")]
        [TestCase("Assets/Prefabs/UI/Tutorial/TutorialPanel.prefab")]
        public void UiPrefabHasNoMissingScriptsOrBrokenInternalReferences(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, transform.name);
            foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                            Is.True, path + ": " + component.name + "." + property.propertyPath);
            }
        }

        [Test]
        public void ProductionContentAndRecipeTemplateAreOwnedByTheirPrefabs()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Popups/ProductionPopup.prefab");
            var production = prefab.GetComponent<SpawnProductionPanel>();
            var popup = prefab.GetComponent<GameplayPopup>();
            Assert.That(production, Is.Not.Null);
            Assert.That(production.Content.parent, Is.SameAs(popup.Window));
            var recipe = new SerializedObject(production).FindProperty("recipeButtonPrefab").objectReferenceValue;
            Assert.That(AssetDatabase.GetAssetPath(recipe), Is.EqualTo("Assets/Prefabs/UI/Elements/RecipeButton.prefab"));
        }
    }
}
