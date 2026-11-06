using System;
using NUnit.Framework;

namespace MyFreshMushroom.Tests
{
    public sealed class InventoryServiceTests
    {
        private InventoryService inventory;
        private int changeCount;

        [SetUp]
        public void SetUp()
        {
            inventory = new InventoryService(new[] { "oyster", "shiitake" });
            changeCount = 0;
            inventory.Changed += () => changeCount++;
        }

        [Test]
        public void NewInventoryStartsEmptyAndSeparatesSpeciesAndResourceKinds()
        {
            Assert.That(inventory.State.GetAmounts("oyster").Mushrooms, Is.Zero);
            Assert.That(inventory.State.GetAmounts("oyster").Spawn, Is.Zero);
            Assert.That(inventory.TryAdd("oyster", ResourceKind.Spawn, 3), Is.True);
            Assert.That(inventory.TryAdd("shiitake", ResourceKind.Mushroom, 5), Is.True);
            Assert.That(inventory.State.GetAmounts("oyster").Mushrooms, Is.Zero);
            Assert.That(inventory.State.GetAmounts("oyster").Spawn, Is.EqualTo(3));
            Assert.That(inventory.State.GetAmounts("shiitake").Mushrooms, Is.EqualTo(5));
            Assert.That(inventory.State.GetAmounts("shiitake").Spawn, Is.Zero);
        }

        [TestCase(ResourceKind.Mushroom)]
        [TestCase(ResourceKind.Spawn)]
        public void SpendChecksBalanceWithoutMutatingAndCanConsumeExactBalance(ResourceKind kind)
        {
            Assert.That(inventory.TryAdd("oyster", kind, 3), Is.True);
            Assert.That(inventory.CanAfford("oyster", kind, 4), Is.False);
            Assert.That(inventory.TrySpend("oyster", kind, 4), Is.False);
            Assert.That(changeCount, Is.EqualTo(1));
            Assert.That(inventory.CanAfford("oyster", kind, 3), Is.True);
            Assert.That(inventory.TrySpend("oyster", kind, 3), Is.True);
            Assert.That(inventory.TrySpend("oyster", kind, 1), Is.False);
            Assert.That(inventory.State.GetAmounts("oyster").Mushrooms, Is.Zero);
            Assert.That(inventory.State.GetAmounts("oyster").Spawn, Is.Zero);
            Assert.That(changeCount, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void NonPositiveAmountsAreRejectedWithoutNotifications(int amount)
        {
            Assert.That(inventory.TryAdd("oyster", ResourceKind.Spawn, 3), Is.True);
            changeCount = 0;
            Assert.That(inventory.TryAdd("oyster", ResourceKind.Spawn, amount), Is.False);
            Assert.That(inventory.CanAfford("oyster", ResourceKind.Spawn, amount), Is.False);
            Assert.That(inventory.TrySpend("oyster", ResourceKind.Spawn, amount), Is.False);
            Assert.That(inventory.State.GetAmounts("oyster").Spawn, Is.EqualTo(3));
            Assert.That(changeCount, Is.Zero);
        }

        [TestCase(ResourceKind.Mushroom)]
        [TestCase(ResourceKind.Spawn)]
        public void OverflowIsRejectedAndMaximumBalanceRemainsSpendable(ResourceKind kind)
        {
            Assert.That(inventory.TryAdd("oyster", kind, int.MaxValue), Is.True);
            Assert.That(inventory.TryAdd("oyster", kind, 1), Is.False);
            Assert.That(inventory.TryAdd("oyster", kind, int.MaxValue), Is.False);
            Assert.That(inventory.CanAfford("oyster", kind, int.MaxValue), Is.True);
            Assert.That(changeCount, Is.EqualTo(1));
            Assert.That(inventory.TrySpend("oyster", kind, int.MaxValue), Is.True);
            Assert.That(inventory.State.GetAmounts("oyster").Mushrooms, Is.Zero);
            Assert.That(inventory.State.GetAmounts("oyster").Spawn, Is.Zero);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("unknown")]
        public void UnknownIdsDoNotCreateInventoryEntries(string id)
        {
            Assert.That(inventory.TryAdd(id, ResourceKind.Spawn, 1), Is.False);
            Assert.That(inventory.CanAfford(id, ResourceKind.Spawn, 1), Is.False);
            Assert.That(inventory.TrySpend(id, ResourceKind.Spawn, 1), Is.False);
            Assert.That(changeCount, Is.Zero);
        }

        [Test]
        public void InvalidResourceKindIsRejected()
        {
            var invalid = (ResourceKind)99;
            Assert.That(inventory.TryAdd("oyster", invalid, 1), Is.False);
            Assert.That(inventory.CanAfford("oyster", invalid, 1), Is.False);
            Assert.That(inventory.TrySpend("oyster", invalid, 1), Is.False);
            Assert.That(changeCount, Is.Zero);
        }

        [Test]
        public void InvalidCatalogIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new InventoryService(null));
            Assert.Throws<ArgumentException>(() => new InventoryService(new[] { "oyster", "oyster" }));
            Assert.Throws<ArgumentException>(() => new InventoryService(new[] { " " }));
            Assert.Throws<ArgumentException>(() => new InventoryService(new string[] { null }));
        }

        [Test]
        public void NotificationObservesCommittedBalance()
        {
            int observedQuantity = -1;
            inventory.Changed += () => observedQuantity = inventory.State.GetAmounts("oyster").Spawn;
            inventory.TryAdd("oyster", ResourceKind.Spawn, 3);
            Assert.That(observedQuantity, Is.EqualTo(3));
        }

        [Test]
        public void SessionsHaveIndependentInventoryAndDiscoveryState()
        {
            var other = new InventoryService(new[] { "oyster" });
            inventory.TryAdd("oyster", ResourceKind.Spawn, 3);
            Assert.That(other.State.GetAmounts("oyster").Spawn, Is.Zero);

            var progress = new ProgressState();
            var otherProgress = new ProgressState();
            Assert.That(progress.TryDiscover(null), Is.False);
            Assert.That(progress.TryDiscover(" "), Is.False);
            Assert.That(progress.TryDiscover("oyster"), Is.True);
            Assert.That(progress.TryDiscover("oyster"), Is.False);
            Assert.That(progress.IsDiscovered("oyster"), Is.True);
            Assert.That(otherProgress.IsDiscovered("oyster"), Is.False);
        }
    }
}
