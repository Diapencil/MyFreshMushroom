using UnityEngine;

namespace MyFreshMushroom
{
    [CreateAssetMenu(menuName = "My Fresh Mushroom/Mushroom", fileName = "Mushroom")]
    public sealed class MushroomData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite spawnIcon;
        [SerializeField, Min(0.1f)] private float growthDurationSeconds = 120;
        [SerializeField, Min(1), Tooltip("물을 주지 않았을 때의 최종 수확 개수")]
        private int baseHarvestYield = 3;
        [SerializeField, Min(1), Tooltip("물을 줬을 때의 최종 수확 개수 (기본 수확량 이상)")]
        private int wateredHarvestYield = 5;
        [SerializeField] private Vector3 growthStageWeights = Vector3.one;
        [SerializeField] private Sprite[] growthStageSprites = new Sprite[4];

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Sprite SpawnIcon => spawnIcon;
        public float GrowthDurationSeconds => growthDurationSeconds;
        public int BaseHarvestYield => baseHarvestYield;
        public int WateredHarvestYield => wateredHarvestYield;
        public Vector3 GrowthStageWeights => growthStageWeights;

        public bool HasValidCultivationSettings
        {
            get
            {
                var weights = growthStageWeights;
                if (!float.IsFinite(growthDurationSeconds) || growthDurationSeconds <= 0 || baseHarvestYield <= 0
                    || wateredHarvestYield < baseHarvestYield
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
