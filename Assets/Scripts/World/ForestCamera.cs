using UnityEngine;

namespace MyFreshMushroom
{
    [RequireComponent(typeof(Camera))]
    public sealed class ForestCamera : MonoBehaviour
    {
        [SerializeField] private ForestGridView forest;
        [SerializeField, Min(0)] private float margin = 0.9f;
        private Camera targetCamera;
        private float previousAspect;

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            previousAspect = 0;
            Fit();
        }

        // A single aspect check is needed to keep the map visible when Game view is resized.
        private void LateUpdate()
        {
            if (targetCamera.aspect != previousAspect) Fit();
        }

        private void Fit()
        {
            if (forest == null || targetCamera.aspect <= 0) return;
            forest.Initialize();
            previousAspect = targetCamera.aspect;
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = Mathf.Max(
                forest.Grid.Size * forest.Grid.TileSize.y * 0.5f + margin,
                (forest.Grid.Size * forest.Grid.TileSize.x * 0.5f + margin) / previousAspect);
        }
    }
}
