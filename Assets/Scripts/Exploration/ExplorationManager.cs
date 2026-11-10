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
        [SerializeField] private Sprite groundSprite;
        [SerializeField] private Sprite logSprite;
        [SerializeField] private Sprite mushroomSprite;
        [SerializeField, Range(4, 32)] private int mapSize = 10;
        [SerializeField] private Vector2Int logCell = new Vector2Int(6, 6);
        [SerializeField, Min(0.1f)] private float minimumZoom = 1.2f;
        [SerializeField, Min(0.1f)] private float maximumZoom = 5.5f;
        [SerializeField, Min(0.01f)] private float zoomSensitivity = 0.2f;

        private readonly KoreanUiFont uiFont = new KoreanUiFont();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private ExplorationService exploration;
        private GameObject viewRoot;
        private SpriteRenderer mushroom;
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

        private void OnEnable()
        {
            if (session == null || forest == null || worldInput == null || facilityPanel == null || productionPanel == null
                || worldCamera == null || forestCamera == null || eventSystem == null || enterButton == null
                || returnButton == null || explorationInfo == null || statusText == null || groundSprite == null
                || logSprite == null || mushroomSprite == null)
            {
                Debug.LogError("ExplorationManager requires its session, camera, UI and prototype art references.", this);
                return;
            }
            if (!session.Initialize()) return;
            exploration = session.Exploration;
            BuildView();
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

        private void BuildView()
        {
            if (viewRoot != null) return;
            var grid = new ForestGrid(mapSize, new Vector2(1, 0.5f));
            if (!grid.ContainsFootprint(logCell, Vector2Int.one))
                throw new System.InvalidOperationException("Exploration log must be inside the map.");
            viewRoot = new GameObject("First Exploration Forest");
            viewRoot.transform.SetParent(transform, false);
            viewRoot.transform.position = forest.transform.position;
            for (int x = 0; x < mapSize; x++)
            for (int y = 0; y < mapSize; y++)
            {
                var tile = CreateSprite($"Tile {x},{y}", groundSprite, grid.CellCenterToWorld(new Vector2Int(x, y)), -10000);
                tile.transform.localScale = new Vector3(1f / groundSprite.bounds.size.x,
                    0.5f / groundSprite.bounds.size.y, 1) * 0.97f;
                tile.color = (x + y) % 2 == 0 ? new Color(0.24f, 0.38f, 0.25f) : new Color(0.27f, 0.42f, 0.28f);
            }
            Vector2 logPosition = grid.CellCenterToWorld(logCell);
            CreateSprite("Fallen Log", logSprite, logPosition, 100);
            mushroom = CreateSprite("Shiitake", mushroomSprite, logPosition + new Vector2(0.65f, -0.12f), 101);
            mushroom.transform.localScale = Vector3.one * 0.75f;
            viewRoot.SetActive(false);
        }

        private SpriteRenderer CreateSprite(string name, Sprite sprite, Vector2 position, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(viewRoot.transform, false);
            go.transform.localPosition = position;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        public void Enter()
        {
            if (exploration == null || !exploration.TryEnter()) return;
            savedPosition = worldCamera.transform.position;
            savedSize = worldCamera.orthographicSize;
            savedBackground = worldCamera.backgroundColor;
            forest.Selection.Select(null);
            forestCamera.enabled = false;
            worldInput.enabled = false;
            facilityPanel.gameObject.SetActive(false);
            productionPanel.gameObject.SetActive(false);
            forest.gameObject.SetActive(false);
            viewRoot.SetActive(true);
            worldCamera.transform.position = new Vector3(viewRoot.transform.position.x, viewRoot.transform.position.y, savedPosition.z);
            worldCamera.orthographicSize = Mathf.Max(mapSize * 0.25f + 0.6f, (mapSize * 0.5f + 0.6f) / worldCamera.aspect);
            worldCamera.backgroundColor = new Color(0.07f, 0.13f, 0.10f);
            pressed = dragged = false;
            Refresh();
        }

        private void Return()
        {
            if (!exploration.IsExploring || session.IsGameplayPaused) return;
            viewRoot.SetActive(false);
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

        private void Refresh()
        {
            bool active = exploration.IsExploring;
            enterButton.interactable = exploration.CanEnter;
            returnButton.interactable = !session.IsGameplayPaused;
            mushroom.gameObject.SetActive(!exploration.IsCollected);
            statusText.text = exploration.IsCollected
                ? $"표고버섯 {exploration.RewardQuantity}개를 발견했어요!\n숲으로 돌아가 재배를 이어가세요."
                : "첫 탐험 · 통나무 주변을 살펴보세요\n드래그: 화면 이동 / 휠: 확대·축소\n보이는 버섯을 누르면 바로 획득합니다.";
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
            if (exploration == null || !exploration.IsExploring || Mouse.current == null) return;
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
                    minimumZoom, Mathf.Max(minimumZoom, maximumZoom));
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
            Bounds target = mushroom.bounds;
            target.size = Vector3.Max(target.size, new Vector3(0.7f, 0.6f, 0.1f));
            Vector3 worldPoint = ScreenToWorld(point);
            worldPoint.z = target.center.z;
            bool hovering = !overUi && !exploration.IsCollected && target.Contains(worldPoint);
            mushroom.color = hovering && !dragged ? new Color(1, 0.88f, 0.65f) : Color.white;
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (pressed && !dragged && hovering) exploration.TryCollect();
                pressed = dragged = false;
            }
        }

        private void ClampCamera()
        {
            Vector3 position = worldCamera.transform.position;
            Vector3 centre = viewRoot.transform.position;
            position.x = Mathf.Clamp(position.x, centre.x - mapSize * 0.5f, centre.x + mapSize * 0.5f);
            position.y = Mathf.Clamp(position.y, centre.y - mapSize * 0.25f, centre.y + mapSize * 0.25f);
            worldCamera.transform.position = position;
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
