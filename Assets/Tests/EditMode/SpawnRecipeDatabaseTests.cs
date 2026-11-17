using System;
using NUnit.Framework;
using UnityEditor;

namespace MyFreshMushroom.Tests
{
    public sealed class SpawnRecipeDatabaseTests
    {
        [Test]
        public void DatabasePreservesRecipesAndProductionUsesItsEntries()
        {
            var database = AssetDatabase.LoadAssetAtPath<SpawnRecipeDatabase>(
                "Assets/Data/SpawnRecipes/SpawnRecipeDatabase.asset");
            Assert.That(database, Is.Not.Null);
            Assert.That(database.Recipes.Count, Is.EqualTo(2));
            var oyster = database.Recipes[0];
            var shiitake = database.Recipes[1];
            Assert.That(oyster.Mushroom.Id, Is.EqualTo("oyster"));
            Assert.That(oyster.MushroomCost, Is.EqualTo(2));
            Assert.That(oyster.SpawnOutput, Is.EqualTo(3));
            Assert.That(shiitake.Mushroom.Id, Is.EqualTo("shiitake"));
            Assert.That(shiitake.MushroomCost, Is.EqualTo(3));
            Assert.That(shiitake.SpawnOutput, Is.EqualTo(3));

            var inventory = new InventoryService(new[] { "oyster", "shiitake" });
            var progress = new ProgressState();
            progress.TryDiscover("oyster");
            var production = new SpawnProductionService(inventory, progress, database.Recipes);
            Assert.That(production.Recipes[0], Is.SameAs(oyster));
            Assert.That(production.IsAvailable(shiitake), Is.False);
            inventory.TryAdd("oyster", ResourceKind.Mushroom, 2);
            Assert.That(production.TryProduce(oyster, 1), Is.True);
            Assert.That(inventory.State.GetAmounts("oyster").Mushrooms, Is.Zero);
            Assert.That(inventory.State.GetAmounts("oyster").Spawn, Is.EqualTo(3));
            progress.TryDiscover("shiitake");
            inventory.TryAdd("shiitake", ResourceKind.Mushroom, 3);
            Assert.That(production.TryProduce(shiitake, 1), Is.True);
            Assert.That(inventory.State.GetAmounts("shiitake").Mushrooms, Is.Zero);
            Assert.That(inventory.State.GetAmounts("shiitake").Spawn, Is.EqualTo(3));
            Assert.That(oyster.MushroomCost, Is.EqualTo(2));
            Assert.That(shiitake.MushroomCost, Is.EqualTo(3));
        }

        [Test]
        public void DuplicateMushroomRecipesAreRejected()
        {
            var database = AssetDatabase.LoadAssetAtPath<SpawnRecipeDatabase>(
                "Assets/Data/SpawnRecipes/SpawnRecipeDatabase.asset");
            Assert.That(database, Is.Not.Null);
            Assert.Throws<ArgumentException>(() => new SpawnProductionService(
                new InventoryService(new[] { "oyster" }), new ProgressState(),
                new[] { database.Recipes[0], database.Recipes[0] }));
        }
    }
}
