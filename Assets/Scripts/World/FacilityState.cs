using System;
using UnityEngine;

namespace MyFreshMushroom
{
    public sealed class FacilityState
    {
        public string InstanceId { get; }
        public FacilityData Definition { get; }
        public Vector2Int Origin { get; }
        public CultivationState Cultivation { get; } = new CultivationState();

        public FacilityState(string instanceId, FacilityData definition, Vector2Int origin)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("An instance ID is required.", nameof(instanceId));
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id)
                || definition.Footprint.x <= 0 || definition.Footprint.y <= 0)
                throw new ArgumentException("A valid facility definition is required.", nameof(definition));
            InstanceId = instanceId;
            Definition = definition;
            Origin = origin;
        }

        public bool ContainsCell(Vector2Int cell)
        {
            Vector2Int size = Definition.Footprint;
            return cell.x >= Origin.x && cell.y >= Origin.y
                && cell.x < (long)Origin.x + size.x && cell.y < (long)Origin.y + size.y;
        }
    }

    public enum GrowthStage { Empty, Mycelium, Pinning, Growing, Ready }

    public sealed class CultivationState
    {
        private double firstStageEnd;
        private double secondStageEnd;
        private double duration;
        private int wateredHarvestYield;
        public MushroomData Mushroom { get; private set; }
        public long CycleNumber { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public double RemainingSeconds => Math.Max(0, duration - ElapsedSeconds);
        public double SecondsUntilWatering => Stage == GrowthStage.Mycelium
            ? Math.Max(0, firstStageEnd - ElapsedSeconds) : 0;
        public int HarvestYield { get; private set; }
        public bool HasBeenWatered { get; private set; }
        public bool CanWater => !HasBeenWatered && (Stage == GrowthStage.Pinning || Stage == GrowthStage.Growing);
        public GrowthStage Stage { get; private set; }
        public event Action Changed;

        internal void Begin(MushroomData mushroom)
        {
            CycleNumber++;
            Mushroom = mushroom;
            ElapsedSeconds = 0;
            duration = mushroom.GrowthDurationSeconds;
            HarvestYield = mushroom.BaseHarvestYield;
            wateredHarvestYield = mushroom.WateredHarvestYield;
            HasBeenWatered = false;
            var weights = mushroom.GrowthStageWeights;
            double totalWeight = (double)weights.x + weights.y + weights.z;
            firstStageEnd = duration * weights.x / totalWeight;
            secondStageEnd = duration * ((double)weights.x + weights.y) / totalWeight;
            Stage = GrowthStage.Mycelium;
            Changed?.Invoke();
        }

        internal void Advance(double seconds)
        {
            if (Stage == GrowthStage.Empty || Stage == GrowthStage.Ready) return;
            var previousStage = Stage;
            double previousRemaining = Math.Ceiling(RemainingSeconds);
            ElapsedSeconds = Math.Min(duration, ElapsedSeconds + seconds);
            Stage = ElapsedSeconds >= duration ? GrowthStage.Ready
                : ElapsedSeconds >= secondStageEnd ? GrowthStage.Growing
                : ElapsedSeconds >= firstStageEnd ? GrowthStage.Pinning
                : GrowthStage.Mycelium;
            // Views need updates at stage transitions and displayed second boundaries, not every frame.
            if (Stage != previousStage || Math.Ceiling(RemainingSeconds) != previousRemaining)
                Changed?.Invoke();
        }

        internal void Clear()
        {
            Mushroom = null;
            ElapsedSeconds = duration = firstStageEnd = secondStageEnd = 0;
            HarvestYield = 0;
            wateredHarvestYield = 0;
            HasBeenWatered = false;
            Stage = GrowthStage.Empty;
            Changed?.Invoke();
        }

        internal void Water()
        {
            HasBeenWatered = true;
            HarvestYield = wateredHarvestYield;
            Changed?.Invoke();
        }
    }
}
