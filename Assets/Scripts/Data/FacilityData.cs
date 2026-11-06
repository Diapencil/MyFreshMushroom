using UnityEngine;

namespace MyFreshMushroom
{
    public enum FacilityKind { CultivationShelf, SpawnWorkbench }

    [CreateAssetMenu(menuName = "My Fresh Mushroom/Facility", fileName = "Facility")]
    public sealed class FacilityData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private FacilityKind kind;
        [SerializeField] private Vector2Int footprint = new Vector2Int(2, 2);
        [SerializeField] private MushroomData supportedMushroom;
        [SerializeField] private Sprite sprite;

        public string Id => id;
        public string DisplayName => displayName;
        public FacilityKind Kind => kind;
        public Vector2Int Footprint => footprint;
        public MushroomData SupportedMushroom => supportedMushroom;
        public Sprite Sprite => sprite;
    }
}
