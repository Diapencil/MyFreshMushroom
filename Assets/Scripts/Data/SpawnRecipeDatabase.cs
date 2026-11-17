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
}
