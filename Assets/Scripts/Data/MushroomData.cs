using UnityEngine;

namespace MyFreshMushroom
{
    [CreateAssetMenu(menuName = "My Fresh Mushroom/Mushroom", fileName = "Mushroom")]
    public sealed class MushroomData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0.1f)] private float growthDurationSeconds = 120;
        [SerializeField, Min(1)] private int baseHarvestYield = 3;
        [SerializeField, Min(1)] private float wateringYieldMultiplier = 1.5f;
        [SerializeField] private Vector3 growthStageWeights = Vector3.one;
        [SerializeField] private Sprite[] growthStageSprites = new Sprite[4];

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public float GrowthDurationSeconds => growthDurationSeconds;
        public int BaseHarvestYield => baseHarvestYield;
        public float WateringYieldMultiplier => wateringYieldMultiplier;
        public Vector3 GrowthStageWeights => growthStageWeights;

        public bool HasValidCultivationSettings
        {
            get
            {
                var weights = growthStageWeights;
                if (!float.IsFinite(growthDurationSeconds) || growthDurationSeconds <= 0 || baseHarvestYield <= 0
                    || !float.IsFinite(wateringYieldMultiplier) || wateringYieldMultiplier < 1
                    || System.Math.Ceiling((double)baseHarvestYield * wateringYieldMultiplier) > int.MaxValue
                    || !float.IsFinite(weights.x) || !float.IsFinite(weights.y) || !float.IsFinite(weights.z)
                    || weights.x <= 0 || weights.y <= 0 || weights.z <= 0
                    || growthStageSprites == null || growthStageSprites.Length != 4)
                    return false;
                foreach (var sprite in growthStageSprites)
                    if (sprite == null) return false;
                return true;
            }
        }

        public Sprite GetGrowthStageSprite(GrowthStage stage)
        {
            int index = (int)stage - 1;
            return growthStageSprites != null && index >= 0 && index < growthStageSprites.Length
                ? growthStageSprites[index] : null;
        }
    }
}
