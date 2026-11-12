using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace MyFreshMushroom
{
    public sealed class ExplorationMap : MonoBehaviour
    {
        [Serializable]
        public sealed class Collectible
        {
            [SerializeField] private string id;
            [SerializeField] private MushroomData mushroom;
            [SerializeField] private SpriteRenderer sprite;
            [SerializeField, Min(1)] private int firstDiscoveryYield = 10;
            [SerializeField, Min(1)] private int repeatExplorationYield = 1;
            [SerializeField, TextArea] private string hint;

            public string Id => id;
            public SpriteRenderer Sprite => sprite;
            internal MushroomData Mushroom => mushroom;

            internal Collectible(string id, MushroomData mushroom, SpriteRenderer sprite, int firstDiscoveryYield)
            {
                this.id = id;
                this.mushroom = mushroom;
                this.sprite = sprite;
                this.firstDiscoveryYield = firstDiscoveryYield;
                hint = "통나무 주변에 일부 드러난 버섯을 살펴보세요.";
            }

            internal ExplorationTarget CreateTarget() =>
                new ExplorationTarget(id, mushroom, firstDiscoveryYield, repeatExplorationYield, hint);
        }

        [SerializeField] private Tilemap ground;
        [SerializeField] private Vector2 cameraSize = new Vector2(10, 5);
        [SerializeField] private SpriteRenderer firstDiscoveryTarget;
        [SerializeField] private Collectible[] collectibles = Array.Empty<Collectible>();
        [SerializeField] private Sprite prototypeGroundSprite;
        [SerializeField, Min(0)] private int prototypeGridSize;

        private Tile prototypeTile;
        private readonly List<ExplorationTarget> targets = new List<ExplorationTarget>();

        public IReadOnlyList<Collectible> Collectibles => collectibles;
        public IReadOnlyList<ExplorationTarget> Targets => targets;
        public Bounds CameraBounds => new Bounds(transform.position, cameraSize);

        internal bool Prepare(MushroomData legacyFirstMushroom, int legacyFirstReward)
        {
            if (!isActiveAndEnabled || ground == null || ground.layoutGrid == null
                || !float.IsFinite(cameraSize.x) || !float.IsFinite(cameraSize.y)
                || cameraSize.x <= 0 || cameraSize.y <= 0)
                return false;

            // Preserve maps authored before collectible definitions were introduced.
            if ((collectibles == null || collectibles.Length == 0) && firstDiscoveryTarget != null)
                collectibles = new[] { new Collectible("first-mushroom", legacyFirstMushroom, firstDiscoveryTarget, legacyFirstReward) };
            if (collectibles == null || collectibles.Length == 0) return false;
            targets.Clear();
            var ids = new HashSet<string>();
            var renderers = new HashSet<SpriteRenderer>();
            foreach (var item in collectibles)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id) || item.Mushroom == null
                    || item.Sprite == null || item.Sprite.sprite == null || !item.Sprite.enabled
                    || !item.Sprite.gameObject.activeInHierarchy
                    || item.Sprite.transform == transform || !item.Sprite.transform.IsChildOf(transform)
                    || !renderers.Add(item.Sprite)) return false;
                targets.Add(item.CreateTarget());
            }

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
