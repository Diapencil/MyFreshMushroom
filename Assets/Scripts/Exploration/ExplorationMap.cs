using UnityEngine;
using UnityEngine.Tilemaps;

namespace MyFreshMushroom
{
    public sealed class ExplorationMap : MonoBehaviour
    {
        [SerializeField] private Tilemap ground;
        [SerializeField] private Vector2 cameraSize = new Vector2(10, 5);
        [SerializeField] private SpriteRenderer firstDiscoveryTarget;
        [SerializeField] private Sprite prototypeGroundSprite;
        [SerializeField, Min(0)] private int prototypeGridSize;

        private Tile prototypeTile;

        public SpriteRenderer FirstDiscoveryTarget => firstDiscoveryTarget;
        public Bounds CameraBounds => new Bounds(transform.position, cameraSize);

        internal bool Prepare(bool requiresFirstDiscovery)
        {
            if (!isActiveAndEnabled || ground == null || ground.layoutGrid == null
                || !float.IsFinite(cameraSize.x) || !float.IsFinite(cameraSize.y)
                || cameraSize.x <= 0 || cameraSize.y <= 0
                || requiresFirstDiscovery && (firstDiscoveryTarget == null || firstDiscoveryTarget.sprite == null))
                return false;

            if (ground.GetUsedTilesCount() > 0) return true;
            if (prototypeGroundSprite == null || prototypeGridSize <= 0) return false;

            prototypeTile = ScriptableObject.CreateInstance<Tile>();
            prototypeTile.sprite = prototypeGroundSprite;
            prototypeTile.colliderType = Tile.ColliderType.None;
            prototypeTile.flags = TileFlags.None;
            var last = new Vector3Int(prototypeGridSize - 1, prototypeGridSize - 1, 0);
            var centre = (ground.GetCellCenterWorld(Vector3Int.zero) + ground.GetCellCenterWorld(last)) * 0.5f;
            ground.transform.position += transform.position - centre;
            for (int x = 0; x < prototypeGridSize; x++)
            for (int y = 0; y < prototypeGridSize; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                ground.SetTile(cell, prototypeTile);
                ground.SetColor(cell, (x + y) % 2 == 0
                    ? new Color(0.24f, 0.38f, 0.25f) : new Color(0.27f, 0.42f, 0.28f));
            }
            return true;
        }

        private void OnDestroy()
        {
            if (prototypeTile != null) Destroy(prototypeTile);
        }
    }
}
