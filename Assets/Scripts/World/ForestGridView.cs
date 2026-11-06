using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyFreshMushroom
{
    public sealed class ForestGridView : MonoBehaviour
    {
        [Serializable]
        private sealed class InitialFacility
        {
            public string instanceId;
            public FacilityData definition;
            public Vector2Int origin;
        }

        [SerializeField, Range(1, 128)] private int size = 16;
        [SerializeField] private Vector2 tileSize = new Vector2(1, 0.5f);
        [SerializeField] private Sprite groundSprite;
        [SerializeField] private FacilityView facilityPrefab;
        [SerializeField] private InitialFacility[] initialFacilities;
        [SerializeField] private Color groundColor = new Color(0.29f, 0.42f, 0.27f);
        [SerializeField] private Color alternateGroundColor = new Color(0.32f, 0.46f, 0.30f);

        private readonly List<FacilityView> facilities = new List<FacilityView>();
        public IReadOnlyList<FacilityView> Facilities => facilities.AsReadOnly();
        public ForestGrid Grid { get; private set; }
        public FacilitySelection Selection { get; private set; }
        public int TileCount { get; private set; }

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (Selection != null)
                return;
            if (groundSprite == null || facilityPrefab == null || initialFacilities == null)
                throw new InvalidOperationException("Forest requires a ground sprite, facility prefab and initial placements.");

            var grid = new ForestGrid(size, tileSize);
            var states = new List<FacilityState>();
            var ids = new HashSet<string>();
            var occupied = new HashSet<Vector2Int>();
            // Validate the full layout before creating any objects.
            foreach (var entry in initialFacilities)
            {
                if (entry == null || entry.definition == null || entry.definition.Sprite == null
                    || (entry.definition.Kind == FacilityKind.CultivationShelf && entry.definition.SupportedMushroom == null)
                    || !ids.Add(entry.instanceId) || !grid.ContainsFootprint(entry.origin, entry.definition.Footprint))
                    throw new InvalidOperationException("Initial facility data or footprint is invalid.");
                var state = new FacilityState(entry.instanceId, entry.definition, entry.origin);
                states.Add(state);
                for (int x = 0; x < entry.definition.Footprint.x; x++)
                for (int y = 0; y < entry.definition.Footprint.y; y++)
                    if (!occupied.Add(entry.origin + new Vector2Int(x, y)))
                        throw new InvalidOperationException("Initial facilities overlap.");
            }

            Grid = grid;
            var ground = new GameObject("Ground").transform;
            ground.SetParent(transform, false);
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                var tile = new GameObject($"Tile {x},{y}", typeof(SpriteRenderer));
                tile.transform.SetParent(ground, false);
                tile.transform.localPosition = grid.CellCenterToWorld(new Vector2Int(x, y));
                var renderer = tile.GetComponent<SpriteRenderer>();
                renderer.sprite = groundSprite;
                renderer.color = (x + y) % 2 == 0 ? groundColor : alternateGroundColor;
                renderer.sortingOrder = -10000;
                tile.transform.localScale = new Vector3(tileSize.x / groundSprite.bounds.size.x,
                    tileSize.y / groundSprite.bounds.size.y, 1) * 0.97f;
                TileCount++;
            }
            foreach (var state in states)
            {
                var view = Instantiate(facilityPrefab, transform);
                view.Initialize(state, grid);
                facilities.Add(view);
            }
            Selection = new FacilitySelection(states);
            Selection.Changed += RefreshSelection;
        }

        private void RefreshSelection(FacilityState selected)
        {
            foreach (var view in facilities)
                view.SetSelected(ReferenceEquals(view.State, selected));
        }
    }
}
