using System;
using UnityEngine;

namespace MyFreshMushroom
{
    [RequireComponent(typeof(Camera))]
    public sealed class ForestCamera : MonoBehaviour
    {
        [SerializeField] private ForestGridView forest;
        [SerializeField] private SpriteRenderer panBackground;
        [SerializeField, Min(0)] private float margin = 0.9f;
        [SerializeField, Min(0.1f)] private float minimumZoom = 1.5f;
        [SerializeField, Min(0.01f)] private float zoomSensitivity = 0.2f;
        [SerializeField, Min(0)] private float restorationFocusSeconds = 0.65f;
        private Camera targetCamera;
        private float previousAspect;
        private float maximumZoom;
        private Vector3 focusStart;
        private Vector3 focusTarget;
        private float focusElapsed;
        private Action onFocusFinished;

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            if (maximumZoom <= 0) ShowWholeMap();
        }

        // A single aspect check is needed to keep the map visible when Game view is resized.
        private void LateUpdate()
        {
            if (targetCamera.aspect != previousAspect) UpdateZoomLimit();
            if (onFocusFinished == null) return;
            focusElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(focusElapsed / restorationFocusSeconds);
            transform.position = ClampToPanBounds(Vector3.Lerp(focusStart, focusTarget, Mathf.SmoothStep(0, 1, progress)));
            if (progress >= 1) FinishFocus();
        }

        private void OnDisable() => FinishFocus();

        public void ShowWholeMap()
        {
            FinishFocus();
            UpdateZoomLimit();
            if (maximumZoom <= 0) return;
            targetCamera.orthographicSize = maximumZoom;
            Vector3 centre = forest.MapBounds.center;
            centre.z = transform.position.z;
            transform.position = ClampToPanBounds(centre);
        }

        internal bool TryFocus(Vector3 centre, Action finished)
        {
            if (!isActiveAndEnabled || onFocusFinished != null) return false;
            centre.z = transform.position.z;
            centre = ClampToPanBounds(centre);
            if (restorationFocusSeconds <= 0 || transform.position == centre)
            {
                transform.position = centre;
                return false;
            }

            focusStart = transform.position;
            focusTarget = centre;
            focusElapsed = 0;
            onFocusFinished = finished;
            return true;
        }

        internal void FinishFocus()
        {
            var finished = onFocusFinished;
            onFocusFinished = null;
            finished?.Invoke();
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
            if (panBackground != null)
            {
                Vector3 backgroundExtents = panBackground.bounds.extents;
                maximumZoom = Mathf.Min(maximumZoom, backgroundExtents.y, backgroundExtents.x / previousAspect);
            }

            targetCamera.orthographicSize = wasFullyZoomedOut
                ? maximumZoom
                : Mathf.Min(targetCamera.orthographicSize, maximumZoom);
            transform.position = ClampToPanBounds(transform.position);
        }

        public void Pan(Vector2 previous, Vector2 current)
        {
            Move(ScreenToWorld(previous) - ScreenToWorld(current));
        }

        public void Zoom(Vector2 pointer, float scroll)
        {
            Vector3 before = ScreenToWorld(pointer);
            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize * Mathf.Exp(-scroll * zoomSensitivity),
                Mathf.Min(minimumZoom, maximumZoom), maximumZoom);
            Move(before - ScreenToWorld(pointer));
        }

        private Vector3 ScreenToWorld(Vector2 pointer) =>
            targetCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, targetCamera.nearClipPlane));

        private void Move(Vector3 delta) => transform.position = ClampToPanBounds(transform.position + delta);

        private Vector3 ClampToPanBounds(Vector3 position)
        {
            Bounds bounds = panBackground != null ? panBackground.bounds : forest.MapBounds;
            Vector3 travel = bounds.extents;
            if (panBackground != null)
            {
                float halfHeight = targetCamera.orthographicSize;
                travel.x = Mathf.Max(0, travel.x - halfHeight * targetCamera.aspect);
                travel.y = Mathf.Max(0, travel.y - halfHeight);
            }

            position.x = Mathf.Clamp(position.x, bounds.center.x - travel.x, bounds.center.x + travel.x);
            position.y = Mathf.Clamp(position.y, bounds.center.y - travel.y, bounds.center.y + travel.y);
            return position;
        }
    }
}
