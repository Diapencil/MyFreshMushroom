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
            outline = GetComponent<LineRenderer>();
            outline.sharedMaterial = GetComponent<SpriteRenderer>().sharedMaterial;
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.widthMultiplier = 0.035f;
            outline.startColor = outline.endColor = new Color(1, 0.87f, 0.35f);
            outline.positionCount = 4;
            var overlay = new GameObject("Growth", typeof(SpriteRenderer));
            overlay.transform.SetParent(transform, false);
            growthOverlay = overlay.GetComponent<SpriteRenderer>();
            RefreshPlacement(grid);
            RefreshOutline();
            State.Cultivation.Changed += RefreshGrowth;
            RefreshGrowth();
        }

        internal void RefreshPlacement(ForestGrid grid)
        {
            Vector2 origin = State.Origin;
            Vector2 size = State.Footprint;
            Vector2 centre = grid.GridToWorld(origin + size * 0.5f);
            transform.localPosition = centre;

            var body = GetComponent<SpriteRenderer>();
            body.sprite = State.Definition.Sprite;
            body.flipX = State.IsFlipped;
            // Lower ground anchors draw in front; picking uses the same order.
            body.sortingOrder = 1000 - Mathf.RoundToInt(centre.y * 100);
            var collider = GetComponent<BoxCollider2D>();
            Vector2 offset = body.sprite.bounds.center;
            if (body.flipX) offset.x = -offset.x;
            collider.offset = offset;
            collider.size = body.sprite.bounds.size;

            outline.sortingOrder = body.sortingOrder - 1;
            outline.SetPosition(0, grid.GridToWorld(origin) - centre);
            outline.SetPosition(1, grid.GridToWorld(origin + new Vector2(size.x, 0)) - centre);
            outline.SetPosition(2, grid.GridToWorld(origin + size) - centre);
            outline.SetPosition(3, grid.GridToWorld(origin + new Vector2(0, size.y)) - centre);
            growthOverlay.sortingOrder = body.sortingOrder + 1;
            growthOverlay.flipX = State.IsFlipped;
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
