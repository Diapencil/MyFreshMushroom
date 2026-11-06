using UnityEngine;

namespace MyFreshMushroom
{
    [CreateAssetMenu(menuName = "My Fresh Mushroom/Spawn Recipe", fileName = "SpawnRecipe")]
    public sealed class SpawnRecipeData : ScriptableObject
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
