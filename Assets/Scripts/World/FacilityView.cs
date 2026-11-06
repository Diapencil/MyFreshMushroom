using UnityEngine;

namespace MyFreshMushroom
{
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(LineRenderer))]
    public sealed class FacilityView : MonoBehaviour
    {
        public FacilityState State { get; private set; }
        public int SortingOrder => GetComponent<SpriteRenderer>().sortingOrder;
        private LineRenderer outline;
        private SpriteRenderer growthOverlay;
        private GrowthStage displayedStage;
        private bool selected;
        private bool tutorialTarget;

        public void Initialize(FacilityState state, ForestGrid grid)
        {
            State = state;
            name = state.Definition.DisplayName;
            Vector2 centre = grid.GridToWorld((Vector2)state.Origin + (Vector2)state.Definition.Footprint * 0.5f);
            transform.localPosition = centre;

            var body = GetComponent<SpriteRenderer>();
            body.sprite = state.Definition.Sprite;
            // Lower ground anchors draw in front; picking uses the same order.
            body.sortingOrder = 1000 - Mathf.RoundToInt(centre.y * 100);
            var collider = GetComponent<BoxCollider2D>();
            collider.offset = body.sprite.bounds.center;
            collider.size = body.sprite.bounds.size;

            outline = GetComponent<LineRenderer>();
            outline.sharedMaterial = body.sharedMaterial;
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.widthMultiplier = 0.035f;
            outline.startColor = outline.endColor = new Color(1, 0.87f, 0.35f);
            outline.sortingOrder = body.sortingOrder - 1;
            Vector2 origin = state.Origin;
            Vector2 size = state.Definition.Footprint;
            outline.positionCount = 4;
            outline.SetPositions(new[] {
                (Vector3)(grid.GridToWorld(origin) - centre),
                (Vector3)(grid.GridToWorld(origin + new Vector2(size.x, 0)) - centre),
                (Vector3)(grid.GridToWorld(origin + size) - centre),
                (Vector3)(grid.GridToWorld(origin + new Vector2(0, size.y)) - centre)
            });
            SetSelected(false);
            var overlay = new GameObject("Growth", typeof(SpriteRenderer));
            overlay.transform.SetParent(transform, false);
            growthOverlay = overlay.GetComponent<SpriteRenderer>();
            growthOverlay.sortingOrder = body.sortingOrder + 1;
            State.Cultivation.Changed += RefreshGrowth;
            RefreshGrowth();
        }

        public void SetSelected(bool selected)
        {
            this.selected = selected;
            RefreshOutline();
        }

        public void SetTutorialTarget(bool target)
        {
            tutorialTarget = target;
            RefreshOutline();
        }

        private void RefreshOutline()
        {
            if (outline == null) return;
            outline.enabled = selected || tutorialTarget;
            outline.startColor = outline.endColor = selected ? new Color(1, 0.87f, 0.35f)
                : new Color(0.35f, 0.88f, 0.95f);
        }

        private void RefreshGrowth()
        {
            var crop = State.Cultivation;
            if (displayedStage == crop.Stage && growthOverlay.sprite != null) return;
            displayedStage = crop.Stage;
            growthOverlay.sprite = crop.Mushroom != null ? crop.Mushroom.GetGrowthStageSprite(crop.Stage) : null;
            growthOverlay.enabled = growthOverlay.sprite != null;
        }

        private void OnDestroy()
        {
            if (State != null) State.Cultivation.Changed -= RefreshGrowth;
        }
    }
}
