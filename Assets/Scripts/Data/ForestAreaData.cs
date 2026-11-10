using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyFreshMushroom
{
    [CreateAssetMenu(menuName = "My Fresh Mushroom/Forest Area", fileName = "ForestArea")]
    public sealed class ForestAreaData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private bool startsOpenForExploration;
        [SerializeField] private RestorationStage[] restorationStages = Array.Empty<RestorationStage>();
        [SerializeField] private GameObject[] explorationMapPrefabs = Array.Empty<GameObject>();
        [SerializeField, Min(0)] private float explorationCooldownSeconds = 60;

        public string Id => id;
        public string DisplayName => displayName;
        public bool StartsOpenForExploration => startsOpenForExploration;
        public IReadOnlyList<RestorationStage> RestorationStages => restorationStages;
        public IReadOnlyList<GameObject> ExplorationMapPrefabs => explorationMapPrefabs;
        public float ExplorationCooldownSeconds => explorationCooldownSeconds;

        public bool HasExplorationMaps
        {
            get
            {
                if (explorationMapPrefabs == null || explorationMapPrefabs.Length == 0) return false;
                foreach (var candidate in explorationMapPrefabs)
                    if (candidate == null) return false;
                return true;
            }
        }

        public bool TryChooseExplorationMap(out GameObject prefab)
        {
            prefab = HasExplorationMaps
                ? explorationMapPrefabs[UnityEngine.Random.Range(0, explorationMapPrefabs.Length)] : null;
            return prefab != null;
        }

        [Serializable]
        public sealed class RestorationStage
        {
            [SerializeField] private MushroomCost[] costs = Array.Empty<MushroomCost>();
            [SerializeField] private GameObject restoredAppearancePrefab;
            [SerializeField] private bool opensExploration;

            public IReadOnlyList<MushroomCost> Costs => costs;
            public GameObject RestoredAppearancePrefab => restoredAppearancePrefab;
            public bool OpensExploration => opensExploration;
        }

        [Serializable]
        public sealed class MushroomCost
        {
            [SerializeField] private MushroomData mushroom;
            [SerializeField, Min(1)] private int quantity = 1;

            public MushroomData Mushroom => mushroom;
            public int Quantity => quantity;
        }
    }
}
