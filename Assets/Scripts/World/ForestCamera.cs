using UnityEngine;

namespace MyFreshMushroom
{
    [RequireComponent(typeof(Camera))]
    public sealed class ForestCamera : MonoBehaviour
    {
        [SerializeField] private ForestGridView forest;
        [SerializeField, Min(0)] private float margin = 0.9f;
        [SerializeField, Min(0.1f)] private float minimumZoom = 1.5f;
        [SerializeField, Min(0.01f)] private float zoomSensitivity = 0.2f;
        private Camera targetCamera;
        private float previousAspect;
        private float maximumZoom;

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            if (maximumZoom <= 0) ShowWholeMap();
        }

        // A single aspect check is needed to keep the map visible when Game view is resized.
        private void LateUpdate()
        {
            if (targetCamera.aspect != previousAspect) UpdateZoomLimit();
        }

        public void ShowWholeMap()
        {
            UpdateZoomLimit();
            if (maximumZoom <= 0) return;
            targetCamera.orthographicSize = maximumZoom;
            Vector3 centre = forest.MapBounds.center;
            centre.z = transform.position.z;
            transform.position = centre;
        }

        private void UpdateZoomLimit()
        {
            if (forest == null || targetCamera.aspect <= 0) return;
            forest.Initialize();
            previousAspect = targetCamera.aspect;
            targetCamera.orthographic = true;
            bool wasFullyZoomedOut = targetCamera.orthographicSize >= maximumZoom;
            Vector3 extents = forest.MapBounds.extents;
            maximumZoom = Mathf.Max(minimumZoom, extents.y + margin, (extents.x + margin) / previousAspect);
            if (wasFullyZoomedOut) targetCamera.orthographicSize = maximumZoom;
        }

        public void Pan(Vector2 previous, Vector2 current)
        {
            Move(ScreenToWorld(previous) - ScreenToWorld(current));
        }

        public void Zoom(Vector2 pointer, float scroll)
        {
            Vector3 before = ScreenToWorld(pointer);
            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize * Mathf.Exp(-scroll * zoomSensitivity), minimumZoom, maximumZoom);
            Move(before - ScreenToWorld(pointer));
        }

        private Vector3 ScreenToWorld(Vector2 pointer) =>
            targetCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, targetCamera.nearClipPlane));

        private void Move(Vector3 delta)
        {
            Vector3 position = transform.position + delta;
            var bounds = forest.MapBounds;
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
            transform.position = position;
        }
    }
}
