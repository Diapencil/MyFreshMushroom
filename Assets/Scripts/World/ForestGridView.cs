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
        [SerializeField] private Sprite groundTileOutline;
        [SerializeField] private Color groundTileOutlineColor = new Color32(64, 64, 64, 255);
        [SerializeField] private bool showGroundTiles;
        [SerializeField] private FacilityView facilityPrefab;
        [SerializeField] private InitialFacility[] initialFacilities;
        [SerializeField] private Vector2Int[] unavailableCells = Array.Empty<Vector2Int>();
        [SerializeField] private AreaPlacement[] areas = Array.Empty<AreaPlacement>();
        [SerializeField] private GameManager session;
        [SerializeField] private ForestCamera forestCamera;

        private readonly List<FacilityView> facilities = new List<FacilityView>();
        public IReadOnlyList<FacilityView> Facilities => facilities.AsReadOnly();
        private HashSet<Vector2Int> blockedCells;
        private GameObject groundTiles;
        private GameObject placementPreview;
        private SpriteRenderer previewBody;
        private SpriteRenderer previewGrowth;
        private readonly List<SpriteRenderer> previewTiles = new List<SpriteRenderer>();
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
            if (Selection != null) return;
            // Camera and HUD initialization can ask for the grid before this component's Awake.
            if (session != null) session.Initialize();
            else Initialize(CreateInitialFacilities());
        }

        internal List<FacilityState> CreateInitialFacilities()
        {
            if (groundSprite == null || facilityPrefab == null || initialFacilities == null)
                throw new InvalidOperationException("Forest requires a ground sprite, facility prefab and initial placements.");

            var states = new List<FacilityState>();
            foreach (var entry in initialFacilities)
            {
                if (entry == null) throw new InvalidOperationException("Initial facility data is missing.");
                states.Add(new FacilityState(entry.instanceId, entry.definition, entry.origin));
            }
            if (!IsValidLayout(states)) throw new InvalidOperationException("Initial facility layout is invalid.");
            return states;
        }

        internal bool IsValidLayout(List<FacilityState> states)
        {
            var grid = new ForestGrid(size, tileSize);
            var ids = new HashSet<string>();
            var occupied = new HashSet<Vector2Int>(unavailableCells);
            foreach (var state in states)
            {
                var definition = state.Definition;
                var footprint = state.Footprint;
                if (definition.Sprite == null
                    || definition.Kind == FacilityKind.CultivationShelf && definition.SupportedMushroom == null
                    || !ids.Add(state.InstanceId) || !grid.ContainsFootprint(state.Origin, footprint))
                    return false;
                for (int x = 0; x < footprint.x; x++)
                for (int y = 0; y < footprint.y; y++)
                    if (!occupied.Add(state.Origin + new Vector2Int(x, y))) return false;
            }
            return true;
        }

        internal void Initialize(List<FacilityState> states)
        {
            if (Selection != null) return;
            var grid = new ForestGrid(size, tileSize);
            blockedCells = new HashSet<Vector2Int>(unavailableCells);
            Grid = grid;
            float centreCoordinate = grid.MinCellCoordinate + size * 0.5f;
            MapBounds = new Bounds(transform.TransformPoint(grid.GridToWorld(Vector2.one * centreCoordinate)), tileSize * size);
            var ground = new GameObject("Ground").transform;
            ground.SetParent(transform, false);
            TileCount = grid.Size * grid.Size;
            if (showGroundTiles) SetGroundTilesVisible(true);
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

        internal void SetGroundTilesVisible(bool visible)
        {
            showGroundTiles = visible;
            if (showGroundTiles && groundTiles == null && groundTileOutline != null)
            {
                groundTiles = new GameObject("Ground Tiles");
                groundTiles.transform.SetParent(transform, false);
                Vector3 scale = new Vector3(tileSize.x / groundTileOutline.bounds.size.x,
                    tileSize.y / groundTileOutline.bounds.size.y, 1);
                for (int x = Grid.MinCellCoordinate; x < Grid.MaxCellCoordinateExclusive; x++)
                for (int y = Grid.MinCellCoordinate; y < Grid.MaxCellCoordinateExclusive; y++)
                {
                    var tile = new GameObject($"Tile {x},{y}", typeof(SpriteRenderer));
                    tile.transform.SetParent(groundTiles.transform, false);
                    tile.transform.localPosition = Grid.CellCenterToWorld(new Vector2Int(x, y));
                    tile.transform.localScale = scale;
                    var renderer = tile.GetComponent<SpriteRenderer>();
                    renderer.sprite = groundTileOutline;
                    renderer.color = groundTileOutlineColor;
                    renderer.sortingOrder = -10000;
                }
            }

            if (groundTiles != null) groundTiles.SetActive(showGroundTiles);
        }

        internal bool CanPlace(FacilityData definition, Vector2Int origin, bool flipped, FacilityState moving)
        {
            Vector2Int footprint = definition.GetFootprint(flipped);
            if (!Grid.ContainsFootprint(origin, footprint)) return false;

            for (int x = 0; x < footprint.x; x++)
            for (int y = 0; y < footprint.y; y++)
            {
                var cell = origin + new Vector2Int(x, y);
                if (blockedCells.Contains(cell)) return false;
                foreach (var view in facilities)
                    if (view.State != moving && view.State.ContainsCell(cell)) return false;
            }
            return true;
        }

        internal void AddFacility(FacilityState state)
        {
            var view = Instantiate(facilityPrefab, transform);
            view.Initialize(state, Grid);
            facilities.Add(view);
            Selection.Registered.Add(state);
        }

        internal void MoveFacility(FacilityState state, Vector2Int origin, bool flipped)
        {
            var view = facilities.Find(facility => facility.State == state);
            state.MoveTo(origin, flipped);
            view.RefreshPlacement(Grid);
        }

        internal void SetFacilityVisible(FacilityState state, bool visible)
        {
            var view = facilities.Find(facility => facility.State == state);
            if (view != null) view.gameObject.SetActive(visible);
        }

        internal void RemoveFacility(FacilityState state)
        {
            var view = facilities.Find(facility => facility.State == state);
            facilities.Remove(view);
            Selection.Registered.Remove(state);
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
            if (Selection.Selected == state) Selection.Select(null);
        }

        internal void ShowPlacementPreview(FacilityData definition, Vector2Int origin, bool flipped, bool valid,
            Sprite growthSprite)
        {
            if (placementPreview == null)
            {
                placementPreview = new GameObject("Facility Placement Preview");
                placementPreview.transform.SetParent(transform, false);
                previewBody = placementPreview.AddComponent<SpriteRenderer>();
                previewBody.sortingOrder = 20001;
                previewBody.color = new Color(1, 1, 1, 0.55f);
                var growth = new GameObject("Growth", typeof(SpriteRenderer));
                growth.transform.SetParent(placementPreview.transform, false);
                previewGrowth = growth.GetComponent<SpriteRenderer>();
                previewGrowth.sortingOrder = previewBody.sortingOrder + 1;
                previewGrowth.color = previewBody.color;
            }
            placementPreview.SetActive(true);
            Vector2Int footprint = definition.GetFootprint(flipped);
            previewBody.sprite = definition.Sprite;
            previewBody.flipX = flipped;
            previewGrowth.sprite = growthSprite;
            previewGrowth.flipX = flipped;
            previewGrowth.enabled = growthSprite != null;
            placementPreview.transform.localPosition = Grid.GridToWorld((Vector2)origin + (Vector2)footprint * 0.5f);

            Color tint = valid ? new Color(0.28f, 0.95f, 0.35f, 0.7f) : new Color(1, 0.25f, 0.22f, 0.7f);
            int index = 0;
            for (int x = 0; x < footprint.x; x++)
            for (int y = 0; y < footprint.y; y++)
            {
                if (index == previewTiles.Count)
                {
                    var tile = new GameObject("Preview Tile", typeof(SpriteRenderer));
                    tile.transform.SetParent(placementPreview.transform, false);
                    tile.transform.localScale = new Vector3(tileSize.x / groundSprite.bounds.size.x,
                        tileSize.y / groundSprite.bounds.size.y, 1) * 0.97f;
                    var renderer = tile.GetComponent<SpriteRenderer>();
                    renderer.sprite = groundSprite;
                    renderer.sortingOrder = 20000;
                    previewTiles.Add(renderer);
                }
                var previewTile = previewTiles[index++];
                previewTile.transform.localPosition = (Vector3)Grid.CellCenterToWorld(origin + new Vector2Int(x, y))
                    - placementPreview.transform.localPosition;
                previewTile.color = tint;
                previewTile.enabled = true;
            }
            for (; index < previewTiles.Count; index++) previewTiles[index].enabled = false;
        }

        internal void HidePlacementPreview()
        {
            if (placementPreview != null) placementPreview.SetActive(false);
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
