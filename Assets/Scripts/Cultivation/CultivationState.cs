using System;

namespace MyFreshMushroom
{
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
            wateredHarvestYield = (int)Math.Ceiling((double)HarvestYield * mushroom.WateringYieldMultiplier);
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
