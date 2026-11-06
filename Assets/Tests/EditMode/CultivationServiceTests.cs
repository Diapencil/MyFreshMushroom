using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MyFreshMushroom.Tests
{
    public sealed class CultivationServiceTests
    {
        private InventoryService inventory;
        private FacilityState shelf;
        private CultivationService cultivation;

        [SetUp]
        public void SetUp()
        {
            var data = AssetDatabase.LoadAssetAtPath<FacilityData>("Assets/Data/Facilities/OysterShelf.asset");
            Assert.That(data != null && data.SupportedMushroom.HasValidCultivationSettings, Is.True);
            inventory = new InventoryService(new[] { data.SupportedMushroom.Id });
            shelf = new FacilityState("test-shelf", data, Vector2Int.zero);
            cultivation = new CultivationService(inventory, new[] { shelf });
        }

        [Test]
        public void CropProgressesThroughFourStagesAndHarvestsOnlyOnce()
        {
            inventory.TryAdd("oyster", ResourceKind.Spawn, 3);
            Assert.That(cultivation.TryInoculate(shelf), Is.True);
            Assert.That(inventory.State.GetAmounts("oyster").Spawn, Is.EqualTo(2));
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Mycelium));
            cultivation.Tick(shelf.Definition.SupportedMushroom.GrowthDurationSeconds / 3.0);
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Pinning));
            cultivation.Tick(shelf.Definition.SupportedMushroom.GrowthDurationSeconds / 3.0);
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Growing));
            cultivation.Tick(shelf.Definition.SupportedMushroom.GrowthDurationSeconds / 3.0);
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Ready));
            cultivation.Tick(10000);
            Assert.That(inventory.State.GetAmounts("oyster").Mushrooms, Is.Zero);
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Ready));
            Assert.That(cultivation.TryHarvest(shelf), Is.True);
            Assert.That(inventory.State.GetAmounts("oyster").Mushrooms, Is.EqualTo(3));
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Empty));
            Assert.That(cultivation.TryHarvest(shelf), Is.False);
            Assert.That(cultivation.TryInoculate(shelf), Is.True);
            Assert.That(inventory.State.GetAmounts("oyster").Spawn, Is.EqualTo(1));
            Assert.That(shelf.Cultivation.ElapsedSeconds, Is.Zero);
        }

        [Test]
        public void InvalidActionsDoNotConsumeResourcesOrAdvanceGrowth()
        {
            Assert.That(cultivation.TryInoculate(shelf), Is.False);
            Assert.That(cultivation.TryInoculate(null), Is.False);
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Empty));
            inventory.TryAdd("oyster", ResourceKind.Spawn, 1);
            Assert.That(cultivation.TryInoculate(shelf), Is.True);
            Assert.That(cultivation.TryInoculate(shelf), Is.False);
            cultivation.Tick(-1);
            cultivation.Tick(double.NaN);
            cultivation.Tick(double.PositiveInfinity);
            Assert.That(shelf.Cultivation.ElapsedSeconds, Is.Zero);
            cultivation.Tick(shelf.Definition.SupportedMushroom.GrowthDurationSeconds - 0.01);
            Assert.That(cultivation.TryHarvest(shelf), Is.False);
            Assert.That(inventory.State.GetAmounts("oyster").Mushrooms, Is.Zero);
            Assert.That(inventory.State.GetAmounts("oyster").Spawn, Is.Zero);
        }

        [Test]
        public void FailedYieldDepositKeepsCropAndNotificationsCannotHarvestTwice()
        {
            inventory.TryAdd("oyster", ResourceKind.Spawn, 1);
            inventory.TryAdd("oyster", ResourceKind.Mushroom, int.MaxValue);
            cultivation.TryInoculate(shelf);
            cultivation.Tick(shelf.Definition.SupportedMushroom.GrowthDurationSeconds);
            Assert.That(cultivation.TryHarvest(shelf), Is.False);
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Ready));
            inventory.TrySpend("oyster", ResourceKind.Mushroom, 3);
            bool reentered = false;
            inventory.Changed += () => reentered |= cultivation.TryHarvest(shelf);
            Assert.That(cultivation.TryHarvest(shelf), Is.True);
            Assert.That(reentered, Is.False);
            Assert.That(inventory.State.GetAmounts("oyster").Mushrooms, Is.EqualTo(int.MaxValue));
            Assert.That(shelf.Cultivation.Stage, Is.EqualTo(GrowthStage.Empty));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void WateringIsOptionalOncePerCropDuringStagesTwoOrThree(int elapsedThirds)
        {
            double duration = shelf.Definition.SupportedMushroom.GrowthDurationSeconds;
            inventory.TryAdd("oyster", ResourceKind.Spawn, 2);
            Assert.That(cultivation.TryWater(shelf), Is.False);
            cultivation.TryInoculate(shelf);
            Assert.That(cultivation.TryWater(shelf), Is.False);
            cultivation.Tick(duration * elapsedThirds / 3.0);
            Assert.That(cultivation.TryWater(shelf), Is.True);
            Assert.That(cultivation.TryWater(shelf), Is.False);
            Assert.That(shelf.Cultivation.HasBeenWatered, Is.True);
            Assert.That(shelf.Cultivation.HarvestYield, Is.EqualTo(5));
            Assert.That(inventory.State.GetAmounts("oyster").Spawn, Is.EqualTo(1));
            cultivation.Tick(duration);
            Assert.That(cultivation.TryWater(shelf), Is.False);
            Assert.That(cultivation.TryHarvest(shelf), Is.True);
            Assert.That(inventory.State.GetAmounts("oyster").Mushrooms, Is.EqualTo(5));
            Assert.That(shelf.Cultivation.HasBeenWatered, Is.False);
            Assert.That(cultivation.TryInoculate(shelf), Is.True);
            Assert.That(shelf.Cultivation.HarvestYield, Is.EqualTo(3));
            cultivation.Tick(duration / 3.0);
            Assert.That(cultivation.TryWater(shelf), Is.True);
        }
    }
}
