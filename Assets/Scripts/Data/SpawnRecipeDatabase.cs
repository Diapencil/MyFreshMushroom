using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyFreshMushroom
{
    [CreateAssetMenu(menuName = "My Fresh Mushroom/Spawn Recipe Database", fileName = "SpawnRecipeDatabase")]
    public sealed class SpawnRecipeDatabase : ScriptableObject
    {
        [SerializeField] private SpawnRecipeData[] recipes = Array.Empty<SpawnRecipeData>();

        public IReadOnlyList<SpawnRecipeData> Recipes => recipes ?? Array.Empty<SpawnRecipeData>();
    }

    [Serializable]
    public sealed class SpawnRecipeData
    {
        [SerializeField] private MushroomData mushroom;
        [SerializeField, Min(1)] private int mushroomCost = 2;
        [SerializeField, Min(1)] private int spawnOutput = 3;

        public MushroomData Mushroom => mushroom;
        public int MushroomCost => mushroomCost;
        public int SpawnOutput => spawnOutput;
        public bool IsValid => mushroom != null && !string.IsNullOrWhiteSpace(mushroom.Id)
            && mushroomCost > 0 && spawnOutput > 0;
    }
}
