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
        [SerializeField, Tooltip("점유 칸 수: X는 왼쪽 위, Y는 오른쪽 위 방향")]
        private Vector2Int footprint = new Vector2Int(2, 2);
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
