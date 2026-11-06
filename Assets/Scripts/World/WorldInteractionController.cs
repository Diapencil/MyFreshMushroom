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
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private GameManager session;
        private InputAction click;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private void OnEnable()
        {
            if (forest == null || worldCamera == null || eventSystem == null || session == null)
            {
                Debug.LogError("WorldInteractionController requires forest, camera, EventSystem and GameManager references.", this);
                return;
            }
            forest.Initialize();
            if (!session.Initialize()) return;
            click = new InputAction("Select facility", InputActionType.Button, "<Mouse>/leftButton");
            click.performed += OnClick;
            click.Enable();
        }

        private void OnDisable()
        {
            if (click == null) return;
            click.performed -= OnClick;
            click.Dispose();
            click = null;
        }

        private void OnClick(InputAction.CallbackContext context)
        {
            if (Mouse.current != null)
                TryInteractAtScreenPosition(Mouse.current.position.ReadValue());
        }

        public bool TryInteractAtScreenPosition(Vector2 position)
        {
            if (!TrySelectAtScreenPosition(position) || session == null || !session.IsInitialized)
                return false;
            var selected = forest.Selection.Selected;
            if (selected == null || selected.Definition.Kind != FacilityKind.CultivationShelf) return true;
            if (selected.Cultivation.Stage == GrowthStage.Ready)
                session.Cultivation.TryHarvest(selected);
            return true;
        }

        public bool TrySelectAtScreenPosition(Vector2 position)
        {
            if (!isActiveAndEnabled || forest == null || worldCamera == null || eventSystem == null)
                return false;
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !worldCamera.pixelRect.Contains(position))
                return false;
            // Raycast at this click's position, even before the UI module updates its pointer cache.
            uiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, uiHits);
            if (uiHits.Count > 0)
                return false;

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
            forest.Selection.Select(candidate != null ? candidate.State : null);
            return true;
        }
    }
}
