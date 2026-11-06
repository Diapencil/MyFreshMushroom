using UnityEngine;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    public sealed class TutorialPanel : MonoBehaviour
    {
        [SerializeField] private GameManager session;
        [SerializeField] private ForestGridView forest;
        [SerializeField] private Text guidanceText;
        [SerializeField] private GameObject overlayContent;
        [SerializeField] private RectTransform dimLeft;
        [SerializeField] private RectTransform dimRight;
        [SerializeField] private RectTransform dimTop;
        [SerializeField] private RectTransform dimBottom;
        [SerializeField] private Button continueButton;
        [SerializeField] private FacilityPanel facilityPanel;
        [SerializeField] private SpawnProductionPanel productionPanel;
        [SerializeField] private Camera worldCamera;
        [SerializeField, Min(0)] private float spotlightPadding = 12;
        private TutorialManager tutorial;
        private readonly KoreanUiFont uiFont = new KoreanUiFont();
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform root;

        private void OnEnable()
        {
            if (session == null || forest == null || guidanceText == null || overlayContent == null
                || dimLeft == null || dimRight == null || dimTop == null || dimBottom == null
                || continueButton == null || facilityPanel == null || productionPanel == null || worldCamera == null)
            {
                Debug.LogError("TutorialPanel requires its session, forest, spotlight, message and action references.", this);
                return;
            }
            if (!session.Initialize()) return;
            tutorial = session.Tutorial;
            root = (RectTransform)transform;
            if (tutorial == null)
            {
                overlayContent.SetActive(false);
                return;
            }
            uiFont.Apply(guidanceText);
            var label = continueButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                uiFont.Apply(label);
                label.text = "계속";
            }
            continueButton.onClick.AddListener(ContinueMessage);
            tutorial.Changed += Refresh;
            forest.Selection.Changed += OnSelectionChanged;
            Refresh();
        }

        private void ContinueMessage() => tutorial?.ContinueMessage();
        private void OnSelectionChanged(FacilityState facility) => Refresh();

        private void Refresh()
        {
            guidanceText.text = tutorial.Guidance;
            overlayContent.SetActive(tutorial.IsMessageOpen);
            continueButton.gameObject.SetActive(tutorial.CanContinueMessage);
            foreach (var view in forest.Facilities)
                view.SetTutorialTarget(tutorial.IsMessageOpen && ReferenceEquals(view.State, tutorial.GuidanceTarget));
            if (tutorial.IsMessageOpen)
            {
                transform.SetAsLastSibling();
                Canvas.ForceUpdateCanvases();
                RefreshSpotlight();
            }
        }

        private void LateUpdate()
        {
            if (tutorial != null && tutorial.IsMessageOpen) RefreshSpotlight();
        }

        private void RefreshSpotlight()
        {
            RectTransform button = null;
            var target = tutorial.GuidanceTarget;
            if (tutorial.CanContinueMessage) button = (RectTransform)continueButton.transform;
            else if (ReferenceEquals(forest.Selection.Selected, target))
            {
                if (tutorial.Step == TutorialStep.FirstInoculation || tutorial.Step == TutorialStep.SecondInoculation)
                    button = facilityPanel.InoculateButtonTransform;
                else if (tutorial.Step == TutorialStep.WaterSecondCrop)
                    button = facilityPanel.WaterButtonTransform;
                else if (tutorial.Step == TutorialStep.FirstSpawnProduction)
                    button = productionPanel.ProduceButtonTransform;
            }

            bool found = false;
            if (button != null && button.gameObject.activeInHierarchy)
            {
                button.GetWorldCorners(corners);
                for (int i = 0; i < corners.Length; i++)
                    corners[i] = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                found = true;
            }
            else if (target != null)
            {
                foreach (var view in forest.Facilities)
                {
                    if (!ReferenceEquals(view.State, target)) continue;
                    Bounds bounds = view.GetComponent<SpriteRenderer>().bounds;
                    corners[0] = worldCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
                    corners[1] = worldCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z));
                    corners[2] = worldCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
                    corners[3] = worldCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z));
                    found = true;
                    break;
                }
            }

            Rect full = root.rect;
            if (!found)
            {
                SetMask(dimLeft, full);
                SetMask(dimRight, new Rect());
                SetMask(dimTop, new Rect());
                SetMask(dimBottom, new Rect());
                return;
            }
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < corners.Length; i++)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, corners[i], null, out Vector2 point);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            float left = Mathf.Clamp(min.x - spotlightPadding, full.xMin, full.xMax);
            float right = Mathf.Clamp(max.x + spotlightPadding, full.xMin, full.xMax);
            float bottom = Mathf.Clamp(min.y - spotlightPadding, full.yMin, full.yMax);
            float top = Mathf.Clamp(max.y + spotlightPadding, full.yMin, full.yMax);
            SetMask(dimLeft, Rect.MinMaxRect(full.xMin, full.yMin, left, full.yMax));
            SetMask(dimRight, Rect.MinMaxRect(right, full.yMin, full.xMax, full.yMax));
            SetMask(dimBottom, Rect.MinMaxRect(left, full.yMin, right, bottom));
            SetMask(dimTop, Rect.MinMaxRect(left, top, right, full.yMax));
        }

        private void SetMask(RectTransform mask, Rect area)
        {
            mask.anchoredPosition = area.min - root.rect.min;
            mask.sizeDelta = area.size;
        }

        private void OnDisable()
        {
            if (tutorial != null) tutorial.Changed -= Refresh;
            if (continueButton != null) continueButton.onClick.RemoveListener(ContinueMessage);
            if (overlayContent != null) overlayContent.SetActive(false);
            tutorial = null;
            if (forest != null)
            {
                if (forest.Selection != null) forest.Selection.Changed -= OnSelectionChanged;
                foreach (var view in forest.Facilities) view.SetTutorialTarget(false);
            }
        }

        private void OnDestroy() => uiFont.Dispose();
    }
}
