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

        [Serializable]
        private sealed class AreaPlacement
        {
            public ForestAreaData definition;
            public Vector2 centre;
            public Vector2 size;
            public Color color;
            public GameObject initialAppearancePrefab;
            [NonSerialized] public Transform appearanceRoot;
            [NonSerialized] public GameObject appliedPrefab;
            [NonSerialized] public GameObject appearance;
        }

        [SerializeField, Range(1, 128)] private int size = 16;
        [SerializeField] private Vector2 tileSize = new Vector2(1, 0.5f);
        [SerializeField] private Sprite groundSprite;
        [SerializeField] private FacilityView facilityPrefab;
        [SerializeField] private InitialFacility[] initialFacilities;
        [SerializeField] private AreaPlacement[] areas = Array.Empty<AreaPlacement>();
        [SerializeField] private GameManager session;
        [SerializeField] private ForestCamera forestCamera;
        [SerializeField] private Color groundColor = new Color(0.29f, 0.42f, 0.27f);
        [SerializeField] private Color alternateGroundColor = new Color(0.32f, 0.46f, 0.30f);

        private readonly List<FacilityView> facilities = new List<FacilityView>();
        public IReadOnlyList<FacilityView> Facilities => facilities.AsReadOnly();
        public ForestGrid Grid { get; private set; }
        public FacilitySelection Selection { get; private set; }
        public int TileCount { get; private set; }
        public Bounds MapBounds { get; private set; }
        public bool IsPresentingRestoration => pendingArea != null;
        public event Action RestorationPresentationChanged;

        private ForestAreaState pendingArea;
        private ForestRestorationService restoration;

        private void Awake() => Initialize();

        private void OnEnable()
        {
            if (session == null || !session.Initialize()) return;
            restoration = session.Restoration;
            restoration.Restored += PresentRestoration;
            foreach (var area in session.ForestAreas.Values) ApplyAppearance(area);
        }

        private void OnDisable()
        {
            if (restoration != null) restoration.Restored -= PresentRestoration;
            restoration = null;
            if (pendingArea != null && forestCamera != null) forestCamera.FinishFocus();
            pendingArea = null;
        }

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
            MapBounds = new Bounds(transform.position, tileSize * size);
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
            foreach (var area in areas)
            {
                if (area.definition == null || area.size.x <= 0 || area.size.y <= 0) continue;
                area.appearanceRoot = new GameObject(area.definition.DisplayName).transform;
                area.appearanceRoot.SetParent(ground, false);
                area.appearanceRoot.localPosition = area.centre;
                var marker = new GameObject("Area Ground", typeof(SpriteRenderer));
                marker.transform.SetParent(area.appearanceRoot, false);
                marker.transform.localScale = new Vector3(area.size.x / groundSprite.bounds.size.x,
                    area.size.y / groundSprite.bounds.size.y, 1);
                var renderer = marker.GetComponent<SpriteRenderer>();
                renderer.sprite = groundSprite;
                renderer.color = area.color;
                renderer.sortingOrder = -9999;
                var bounds = MapBounds;
                bounds.Encapsulate(renderer.bounds);
                MapBounds = bounds;
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

        private void PresentRestoration(ForestAreaState area)
        {
            var placement = FindPlacement(area.Definition);
            if (placement == null) return;
            if (pendingArea != null) forestCamera.FinishFocus();
            pendingArea = area;
            if (forestCamera == null || !forestCamera.TryFocus(placement.appearanceRoot.position, FinishRestoration))
                FinishRestoration();
            else
                RestorationPresentationChanged?.Invoke();
        }

        private void FinishRestoration()
        {
            var area = pendingArea;
            pendingArea = null;
            if (area != null && isActiveAndEnabled) ApplyAppearance(area);
            RestorationPresentationChanged?.Invoke();
        }

        private void ApplyAppearance(ForestAreaState area)
        {
            var placement = FindPlacement(area.Definition);
            if (placement == null) return;
            var prefab = placement.initialAppearancePrefab;
            for (int i = 0; i < area.CompletedRestorationStages; i++)
            {
                var restored = area.Definition.RestorationStages[i]?.RestoredAppearancePrefab;
                if (restored != null) prefab = restored;
            }
            if (placement.appliedPrefab == prefab) return;

            if (placement.appearance != null)
            {
                placement.appearance.SetActive(false);
                Destroy(placement.appearance);
            }
            placement.appearance = prefab != null ? Instantiate(prefab, placement.appearanceRoot) : null;
            placement.appliedPrefab = prefab;
        }

        private AreaPlacement FindPlacement(ForestAreaData definition)
        {
            foreach (var area in areas)
                if (area.definition == definition && area.appearanceRoot != null) return area;
            return null;
        }

        public ForestAreaData FindAreaAt(Vector2 worldPosition)
        {
            Vector2 local = transform.InverseTransformPoint(worldPosition);
            foreach (var area in areas)
            {
                if (area.definition == null || area.size.x <= 0 || area.size.y <= 0) continue;
                Vector2 offset = local - area.centre;
                if (Mathf.Abs(offset.x) / area.size.x + Mathf.Abs(offset.y) / area.size.y <= 0.5f)
                    return area.definition;
            }
            return null;
        }

        private void RefreshSelection(FacilityState selected)
        {
            foreach (var view in facilities)
                view.SetSelected(ReferenceEquals(view.State, selected));
        }
    }
}
