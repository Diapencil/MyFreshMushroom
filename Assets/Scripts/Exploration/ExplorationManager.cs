using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    public sealed class ExplorationManager : MonoBehaviour
    {
        [SerializeField] private GameManager session;
        [SerializeField] private ForestGridView forest;
        [SerializeField] private WorldInteractionController worldInput;
        [SerializeField] private FacilityPanel facilityPanel;
        [SerializeField] private SpawnProductionPanel productionPanel;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private ForestCamera forestCamera;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private Button enterButton;
        [SerializeField] private Button returnButton;
        [SerializeField] private GameObject explorationInfo;
        [SerializeField] private Text statusText;
        [SerializeField, Min(0.1f)] private float minimumZoom = 1.2f;
        [SerializeField, Min(0.1f)] private float maximumZoom = 5.5f;
        [SerializeField, Min(0.01f)] private float zoomSensitivity = 0.2f;

        private readonly KoreanUiFont uiFont = new KoreanUiFont();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private ExplorationService exploration;
        private ExplorationMap activeMap;
        private Vector3 savedPosition;
        private float savedSize;
        private Color savedBackground;
        private bool pressed;
        private bool dragged;
        private Vector2 pressPosition;
        private Vector2 previousPosition;

        public Button EnterButton => enterButton;
        public Button ReturnButton => returnButton;
        public string Status => statusText.text;

        public ForestAreaState EntryArea
        {
            get
            {
                if (worldInput.SelectedArea != null) return worldInput.SelectedArea;
                var firstArea = session.Exploration.FirstArea;
                return firstArea != null && session.Tutorial?.Step == TutorialStep.AwaitFirstExploration
                    && session.ForestAreas.TryGetValue(firstArea.Id, out var area) ? area : null;
            }
        }

        private void OnEnable()
        {
            if (session == null || forest == null || worldInput == null || facilityPanel == null || productionPanel == null
                || worldCamera == null || forestCamera == null || eventSystem == null || enterButton == null
                || returnButton == null || explorationInfo == null || statusText == null)
            {
                Debug.LogError("ExplorationManager requires its session, camera and UI references.", this);
                return;
            }
            if (!session.Initialize()) return;
            exploration = session.Exploration;
            uiFont.Apply(statusText);
            foreach (var button in new[] { enterButton, returnButton })
                uiFont.Apply(button.GetComponentInChildren<Text>(true));
            var returnLabel = returnButton.GetComponentInChildren<Text>(true);
            if (returnLabel != null) returnLabel.text = "돌아가기";
            enterButton.onClick.AddListener(Enter);
            returnButton.onClick.AddListener(Return);
            explorationInfo.SetActive(false);
            exploration.Changed += Refresh;
            if (session.Tutorial != null) session.Tutorial.Changed += Refresh;
            Refresh();
        }

        public void Enter()
        {
            if (exploration == null || activeMap != null || forest.IsPresentingRestoration) return;
            var area = EntryArea;
            if (!exploration.CanEnter(area)) return;
            if (!area.Definition.TryChooseExplorationMap(out var prefab)
                || !prefab.TryGetComponent<ExplorationMap>(out var mapPrefab))
            {
                Debug.LogError($"No usable exploration map prefab for {area.Definition.Id}.", this);
                return;
            }

            activeMap = Instantiate(mapPrefab, forest.transform.position, Quaternion.identity, transform);
            bool requiresFirstDiscovery = area.Definition == exploration.FirstArea && !exploration.IsCollected;
            if (!activeMap.Prepare(requiresFirstDiscovery))
            {
                Debug.LogError($"Invalid exploration map configuration: {prefab.name}.", prefab);
                RemoveMap();
                return;
            }
            if (!exploration.TryEnter(area))
            {
                RemoveMap();
                return;
            }

            savedPosition = worldCamera.transform.position;
            savedSize = worldCamera.orthographicSize;
            savedBackground = worldCamera.backgroundColor;
            forest.Selection.Select(null);
            forestCamera.enabled = false;
            worldInput.enabled = false;
            facilityPanel.gameObject.SetActive(false);
            productionPanel.gameObject.SetActive(false);
            forest.gameObject.SetActive(false);
            var centre = activeMap.CameraBounds.center;
            worldCamera.transform.position = new Vector3(centre.x, centre.y, savedPosition.z);
            worldCamera.orthographicSize = FitMapZoom();
            worldCamera.backgroundColor = new Color(0.07f, 0.13f, 0.10f);
            pressed = dragged = false;
            Refresh();
        }

        private void Return()
        {
            if (exploration?.IsExploring != true || session.IsGameplayPaused) return;
            ExitExploration();
        }

        private void ExitExploration()
        {
            RemoveMap();
            forest.gameObject.SetActive(true);
            worldCamera.transform.position = savedPosition;
            worldCamera.orthographicSize = savedSize;
            worldCamera.backgroundColor = savedBackground;
            forestCamera.enabled = true;
            facilityPanel.gameObject.SetActive(true);
            productionPanel.gameObject.SetActive(true);
            worldInput.enabled = true;
            pressed = dragged = false;
            exploration.Leave();
        }

        private void RemoveMap()
        {
            if (activeMap == null) return;
            activeMap.gameObject.SetActive(false);
            Destroy(activeMap.gameObject);
            activeMap = null;
        }

        private void Refresh()
        {
            returnButton.interactable = !session.IsGameplayPaused;
            var area = exploration.ActiveArea;
            if (area == null || activeMap == null) return;
            bool firstArea = area.Definition == exploration.FirstArea;
            var target = activeMap.FirstDiscoveryTarget;
            if (target != null) target.gameObject.SetActive(firstArea && !exploration.IsCollected);
            const string controls = "드래그: 화면 이동 / 휠: 확대·축소";
            statusText.text = !firstArea ? $"{area.Definition.DisplayName}\n{controls}"
                : exploration.IsCollected
                    ? $"표고버섯 {exploration.RewardQuantity}개를 발견했어요!\n숲으로 돌아가 재배를 이어가세요."
                    : $"첫 탐험 · 통나무 주변을 살펴보세요\n{controls}\n보이는 버섯을 누르면 바로 획득합니다.";
        }

        private bool IsOverUi(Vector2 position)
        {
            uiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, uiHits);
            return uiHits.Count > 0;
        }

        private Vector3 ScreenToWorld(Vector2 point) => worldCamera.ScreenToWorldPoint(
            new Vector3(point.x, point.y, -worldCamera.transform.position.z));

        private void Update()
        {
            if (activeMap == null || !exploration.IsExploring || Mouse.current == null) return;
            if (session.IsGameplayPaused) { pressed = dragged = false; return; }
            var mouse = Mouse.current;
            Vector2 point = mouse.position.ReadValue();
            bool overUi = IsOverUi(point);
            float scroll = mouse.scroll.ReadValue().y;
            if (!overUi && scroll != 0)
            {
                Vector3 before = ScreenToWorld(point);
                // Input System's default uniform scroll range uses one unit per wheel notch.
                worldCamera.orthographicSize = Mathf.Clamp(worldCamera.orthographicSize * Mathf.Exp(-scroll * zoomSensitivity),
                    minimumZoom, Mathf.Max(maximumZoom, FitMapZoom()));
                worldCamera.transform.position += before - ScreenToWorld(point);
                ClampCamera();
            }
            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                pressed = true;
                dragged = false;
                pressPosition = previousPosition = point;
            }
            if (pressed && mouse.leftButton.isPressed)
            {
                if ((point - pressPosition).sqrMagnitude > 25) dragged = true;
                if (dragged)
                {
                    worldCamera.transform.position += ScreenToWorld(previousPosition) - ScreenToWorld(point);
                    ClampCamera();
                }
                previousPosition = point;
            }
            var mushroom = activeMap.FirstDiscoveryTarget;
            bool hovering = false;
            if (mushroom != null && mushroom.gameObject.activeInHierarchy)
            {
                Bounds target = mushroom.bounds;
                target.size = Vector3.Max(target.size, new Vector3(0.7f, 0.6f, 0.1f));
                Vector3 worldPoint = ScreenToWorld(point);
                worldPoint.z = target.center.z;
                hovering = !overUi && target.Contains(worldPoint);
                mushroom.color = hovering && !dragged ? new Color(1, 0.88f, 0.65f) : Color.white;
            }
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if ((point - pressPosition).sqrMagnitude > 25) dragged = true;
                if (pressed && !dragged && hovering) exploration.TryCollect();
                pressed = dragged = false;
            }
        }

        private void ClampCamera()
        {
            Vector3 position = worldCamera.transform.position;
            Bounds bounds = activeMap.CameraBounds;
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
            worldCamera.transform.position = position;
        }

        private float FitMapZoom()
        {
            var extents = activeMap.CameraBounds.extents;
            return Mathf.Max(extents.y + 0.6f, (extents.x + 0.6f) / worldCamera.aspect);
        }

        private void OnDisable()
        {
            if (enterButton != null) enterButton.onClick.RemoveListener(Enter);
            if (returnButton != null) returnButton.onClick.RemoveListener(Return);
            if (exploration != null) exploration.Changed -= Refresh;
            if (session != null && session.Tutorial != null) session.Tutorial.Changed -= Refresh;
        }

        private void OnDestroy() => uiFont.Dispose();
    }
}
