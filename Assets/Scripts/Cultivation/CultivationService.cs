using System;
using System.Collections.Generic;

namespace MyFreshMushroom
{
    public readonly struct HarvestResult
    {
        public FacilityState Facility { get; }
        public long CycleNumber { get; }
        public string MushroomId { get; }
        public int Quantity { get; }
        public bool WasWatered { get; }

        internal HarvestResult(FacilityState facility)
        {
            Facility = facility;
            CycleNumber = facility.Cultivation.CycleNumber;
            MushroomId = facility.Cultivation.Mushroom.Id;
            Quantity = facility.Cultivation.HarvestYield;
            WasWatered = facility.Cultivation.HasBeenWatered;
        }
    }

    public sealed class CultivationService
    {
        private readonly InventoryService inventory;
        private readonly HashSet<FacilityState> facilities;
        private bool processingAction;
        private readonly IGameplayActionRules actionRules;
        public event Action<FacilityState> Inoculated;
        public event Action<FacilityState> Watered;
        public event Action<HarvestResult> Harvested;

        public CultivationService(InventoryService inventory, IEnumerable<FacilityState> facilities, IGameplayActionRules actionRules = null)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.facilities = new HashSet<FacilityState>(facilities ?? throw new ArgumentNullException(nameof(facilities)));
            this.actionRules = actionRules;
        }

        public bool CanInoculate(FacilityState facility)
        {
            if (!OwnsCultivationFacility(facility) || facility.Cultivation.Stage != GrowthStage.Empty) return false;
            var mushroom = facility.Definition.SupportedMushroom;
            return mushroom != null && mushroom.HasValidCultivationSettings
                && inventory.CanAfford(mushroom.Id, ResourceKind.Spawn, 1)
                && (actionRules == null || actionRules.AllowsInoculation(facility));
        }

        public bool CanWater(FacilityState facility)
        {
            return OwnsCultivationFacility(facility) && facility.Cultivation.CanWater
                && (actionRules == null || actionRules.AllowsWatering(facility));
        }

        public bool CanHarvest(FacilityState facility)
        {
            return OwnsCultivationFacility(facility) && facility.Cultivation.Stage == GrowthStage.Ready
                && (actionRules == null || actionRules.AllowsHarvest(facility));
        }

        public bool TryInoculate(FacilityState facility)
        {
            if (processingAction || !CanInoculate(facility)) return false;
            var mushroom = facility.Definition.SupportedMushroom;
            if (mushroom == null || !mushroom.HasValidCultivationSettings) return false;

            // Inventory change notifications must not re-enter a partially committed crop action.
            processingAction = true;
            try
            {
                if (!inventory.TrySpend(mushroom.Id, ResourceKind.Spawn, 1)) return false;
                facility.Cultivation.Begin(mushroom);
                Inoculated?.Invoke(facility);
                return true;
            }
            finally { processingAction = false; }
        }

        public bool TryWater(FacilityState facility)
        {
            if (processingAction || !CanWater(facility))
                return false;
            processingAction = true;
            try
            {
                facility.Cultivation.Water();
                Watered?.Invoke(facility);
                return true;
            }
            finally { processingAction = false; }
        }

        public bool TryHarvest(FacilityState facility)
        {
            if (processingAction || !CanHarvest(facility)) return false;
            var crop = facility.Cultivation;
            var result = new HarvestResult(facility);
            processingAction = true;
            try
            {
                if (!inventory.TryAdd(crop.Mushroom.Id, ResourceKind.Mushroom, crop.HarvestYield))
                    return false;
                crop.Clear();
                Harvested?.Invoke(result);
                return true;
            }
            finally { processingAction = false; }
        }

        public void Tick(double seconds)
        {
            if (processingAction || !double.IsFinite(seconds) || seconds <= 0) return;
            foreach (var facility in facilities)
                if (facility != null) facility.Cultivation.Advance(seconds);
        }

        private bool OwnsCultivationFacility(FacilityState facility)
        {
            return facility != null && facilities.Contains(facility)
                && facility.Definition.Kind == FacilityKind.CultivationShelf;
        }
    }
}
