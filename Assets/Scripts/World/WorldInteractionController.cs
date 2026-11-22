using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace MyFreshMushroom
{
    public sealed class WorldInteractionController : MonoBehaviour
    {
        [SerializeField] private ForestGridView forest;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private ForestCamera forestCamera;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private GameManager session;
        private bool ready;
        private bool pressed;
        private bool dragged;
        private Vector2 pressPosition;
        private Vector2 previousPosition;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private FacilityData placementDefinition;
        private FacilityState movingFacility;
        private bool placementFlipped;
        private Vector2Int previewCell;
        private Vector2? pointerBeforePickup;
        public ForestAreaState SelectedArea { get; private set; }
        public event Action AreaSelectionChanged;
        public bool IsPlacingFacility => placementDefinition != null;
        public event Action PlacementChanged;

        private void OnEnable()
        {
            if (forest == null || worldCamera == null || forestCamera == null || eventSystem == null || session == null)
            {
                Debug.LogError("WorldInteractionController requires forest, camera, EventSystem and GameManager references.", this);
                return;
            }
            forest.Initialize();
            if (!session.Initialize()) return;
            ready = true;
            forest.Selection.Changed += OnFacilitySelected;
            if (session.Tutorial != null) session.Tutorial.Changed += OnTutorialChanged;
        }

        private void OnDisable()
        {
            if (!ready) return;
            forest.Selection.Changed -= OnFacilitySelected;
            if (session.Tutorial != null) session.Tutorial.Changed -= OnTutorialChanged;
            ready = pressed = dragged = false;
            CancelPlacement();
            SelectArea(null);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (!ready) return;
            if (IsPlacingFacility && (!session.CanManageFacilities || Keyboard.current?.escapeKey.wasPressedThisFrame == true))
            {
                CancelPlacement();
                return;
            }
            if (mouse == null || forest.IsPresentingRestoration)
            {
                pressed = dragged = false;
                if (movingFacility == null) forest.HidePlacementPreview();
                return;
            }
            if (IsPlacingFacility && Keyboard.current?.rKey.wasPressedThisFrame == true) FlipPlacement();
            Vector2 pointer = mouse.position.ReadValue();
            bool canMoveCamera = !session.IsGameplayPaused;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressed = CanPointAtWorld(pointer);
                dragged = false;
                pressPosition = previousPosition = pointer;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0 && canMoveCamera && CanPointAtWorld(pointer)) forestCamera.Zoom(pointer, scroll);

            if (pressed)
            {
                if (!dragged && (pointer - pressPosition).sqrMagnitude > 25)
                {
                    dragged = true;
                    previousPosition = pressPosition;
                }
                if (dragged && canMoveCamera) forestCamera.Pan(previousPosition, pointer);
                previousPosition = pointer;
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    if (!dragged) TrySelectAtScreenPosition(pointer);
                    pressed = dragged = false;
                }
            }
            if (!IsPlacingFacility) return;
            // The OS moves the cursor before the Input System reports its new position.
            if (pointerBeforePickup.HasValue && pointer == pointerBeforePickup.Value) return;
            pointerBeforePickup = null;
            if (!dragged && CanPointAtWorld(pointer) && TryGetWorldPoint(pointer, out var worldPosition)
                && TryGetPlacementCell(worldPosition, out var cell))
                RefreshPlacementPreview(cell);
            else if (movingFacility == null) forest.HidePlacementPreview();
        }

        public void BeginConstruction(FacilityData definition)
        {
            if (!ready || IsPlacingFacility || !session.CanManageFacilities || !session.CanAffordFacility(definition)) return;
            BeginPlacement(definition, null);
        }

        public void BeginFacilityMove(FacilityState facility)
        {
            if (!ready || IsPlacingFacility || !session.CanManageFacility(facility)) return;
            BeginPlacement(facility.Definition, facility);
            forest.SetFacilityVisible(facility, false);
            RefreshPlacementPreview(facility.Origin);

            var mouse = Mouse.current;
            pointerBeforePickup = mouse?.position.ReadValue();
            if (mouse == null || !Application.isFocused || Application.isBatchMode) return;

            Vector3 centre = forest.transform.TransformPoint(
                forest.Grid.GridToWorld((Vector2)facility.Origin + (Vector2)facility.Footprint * 0.5f));
            Vector2 screenPoint = worldCamera.WorldToScreenPoint(centre);
            if (!CanPointAtWorld(screenPoint))
            {
                forestCamera.Pan(screenPoint, worldCamera.pixelRect.center);
                screenPoint = worldCamera.WorldToScreenPoint(centre);
            }
            if (CanPointAtWorld(screenPoint)) mouse.WarpCursorPosition(screenPoint);
        }

        private void BeginPlacement(FacilityData definition, FacilityState facility)
        {
            pressed = dragged = false;
            placementDefinition = definition;
            movingFacility = facility;
            placementFlipped = facility != null && facility.IsFlipped;
            PlacementChanged?.Invoke();
        }

        public void FlipPlacement()
        {
            if (!IsPlacingFacility) return;
            placementFlipped = !placementFlipped;
            if (movingFacility != null) RefreshPlacementPreview(previewCell);
        }

        private void RefreshPlacementPreview(Vector2Int cell)
        {
            previewCell = cell;
            var crop = movingFacility?.Cultivation;
            var growthSprite = crop?.Mushroom != null ? crop.Mushroom.GetGrowthStageSprite(crop.Stage) : null;
            bool valid = session.CanPlaceFacility(placementDefinition, cell, placementFlipped, movingFacility);
            forest.ShowPlacementPreview(placementDefinition, cell, placementFlipped, valid, growthSprite);
        }

        public void CancelPlacement()
        {
            if (!IsPlacingFacility) return;
            if (movingFacility != null) forest.SetFacilityVisible(movingFacility, true);
            placementDefinition = null;
            movingFacility = null;
            pointerBeforePickup = null;
            pressed = dragged = false;
            forest.HidePlacementPreview();
            PlacementChanged?.Invoke();
        }

        private bool TryGetPlacementCell(Vector2 worldPosition, out Vector2Int cell)
        {
            Vector2Int footprint = placementDefinition.GetFootprint(placementFlipped);
            int margin = Mathf.Max(1, Mathf.Max(footprint.x, footprint.y));
            Vector2 localPosition = forest.transform.InverseTransformPoint(worldPosition);
            if (movingFacility != null)
            {
                // Keep the cursor over the same part of the facility as when it was picked up.
                localPosition -= forest.Grid.GridToWorld(((Vector2)movingFacility.Footprint - Vector2.one) * 0.5f);
            }
            return forest.Grid.TryWorldToCell(localPosition, out cell, margin);
        }

        private bool TryCompletePlacement(Vector2 worldPosition)
        {
            if (!TryGetPlacementCell(worldPosition, out var cell)) return false;
            var facility = movingFacility;
            bool placed = facility != null
                ? session.TryMoveFacility(facility, cell, placementFlipped)
                : session.TryBuildFacility(placementDefinition, cell, placementFlipped, out facility);
            if (!placed) return false;
            CancelPlacement();
            forest.Selection.Select(facility);
            return true;
        }

        private bool TryGetWorldPoint(Vector2 position, out Vector2 worldPosition)
        {
            var plane = new Plane(Vector3.forward, forest.transform.position);
            Ray ray = worldCamera.ScreenPointToRay(position);
            worldPosition = default;
            if (!plane.Raycast(ray, out float distance)) return false;
            worldPosition = ray.GetPoint(distance);
            return true;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) pressed = dragged = false;
        }

        private void OnTutorialChanged()
        {
            if (session.Tutorial.IsMessageOpen && session.Tutorial.GuidanceTarget != null)
                forestCamera.ShowWholeMap();
        }

        private void OnFacilitySelected(FacilityState facility)
        {
            if (facility != null) SelectArea(null);
        }

        private void SelectArea(ForestAreaState area)
        {
            if (ReferenceEquals(SelectedArea, area)) return;
            SelectedArea = area;
            AreaSelectionChanged?.Invoke();
        }

        public bool TrySelectAtScreenPosition(Vector2 position)
        {
            if (!ready || !isActiveAndEnabled || forest.IsPresentingRestoration || !CanPointAtWorld(position)) return false;

            if (!TryGetWorldPoint(position, out var worldPosition)) return false;
            if (IsPlacingFacility) return TryCompletePlacement(worldPosition);
            FacilityView candidate = null;
            foreach (var hit in Physics2D.OverlapPointAll(worldPosition))
            {
                var view = hit.GetComponent<FacilityView>();
                if (view == null || view.State == null || view.transform.parent != forest.transform)
                    continue;
                if (candidate == null || view.SortingOrder > candidate.SortingOrder)
                    candidate = view;
            }
            var tutorial = session != null ? session.Tutorial : null;
            if (tutorial != null && tutorial.IsMessageOpen
                && !ReferenceEquals(candidate != null ? candidate.State : null, tutorial.GuidanceTarget))
                return false;
            ForestAreaState selectedArea = null;
            if (candidate == null && !session.IsGameplayPaused)
            {
                var definition = forest.FindAreaAt(worldPosition);
                if (definition != null) session.ForestAreas.TryGetValue(definition.Id, out selectedArea);
            }
            forest.Selection.Select(candidate != null ? candidate.State : null);
            SelectArea(selectedArea);
            return true;
        }

        private bool CanPointAtWorld(Vector2 position)
        {
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !worldCamera.pixelRect.Contains(position))
                return false;
            // Raycast at this pointer position, even before the UI module updates its cache.
            uiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, uiHits);
            return uiHits.Count == 0;
        }
    }
}
