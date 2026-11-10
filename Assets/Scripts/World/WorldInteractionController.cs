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
        public ForestAreaState SelectedArea { get; private set; }
        public event Action AreaSelectionChanged;

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
            SelectArea(null);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (!ready || mouse == null || forest.IsPresentingRestoration) { pressed = dragged = false; return; }
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

            if (!pressed) return;
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

            var plane = new Plane(Vector3.forward, forest.transform.position);
            Ray ray = worldCamera.ScreenPointToRay(position);
            if (!plane.Raycast(ray, out float distance))
                return false;
            Vector2 worldPosition = ray.GetPoint(distance);
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
