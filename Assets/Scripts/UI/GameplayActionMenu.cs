using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    public sealed class GameplayActionMenu : MonoBehaviour
    {
        [SerializeField] private WorldInteractionController worldInput;
        [SerializeField] private GameManager session;
        [SerializeField] private RectTransform canvas;
        [SerializeField] private ExplorationManager exploration;
        [SerializeField] private ForestAreaData smallForestPath;
        [SerializeField] private ForestGridView forest;
        [SerializeField] private FacilityPanel facilityPanel;
        [SerializeField] private SpawnProductionPanel productionPanel;

        private enum PopupPage { None, Facility, Area, Restoration, Catalogue, Production, Exploration }

        private readonly KoreanUiFont font = new KoreanUiFont();
        private RectTransform row;
        private CanvasGroup actions;
        private GameplayPopup popup;
        private PopupPage page;
        private FacilityState selectedFacility;
        private Button build;
        private Button catalogue;
        private Button harvest;
        private Button production;
        private Button restore;
        private Button information;

        public RectTransform HarvestTarget => harvest != null ? (RectTransform)harvest.transform : null;
        public RectTransform ProductionTarget => production == null ? null
            : page == PopupPage.Production ? popup.Window : (RectTransform)production.transform;

        private void OnEnable()
        {
            if (worldInput == null || session == null || canvas == null || exploration == null || smallForestPath == null
                || forest == null || facilityPanel == null || productionPanel == null)
            {
                Debug.LogError("GameplayActionMenu requires its canvas, selection and gameplay panel references.", this);
                return;
            }
            if (!session.Initialize()) return;
            if (row == null) Build();
            row.gameObject.SetActive(true);
            forest.Selection.Changed += OnFacilityChanged;
            worldInput.AreaSelectionChanged += OnContextChanged;
            session.Exploration.Changed += OnContextChanged;
            session.Progress.DiscoveryChanged += RefreshPopup;
            session.Inventory.Changed += RefreshPopup;
            facilityPanel.StatusChanged += RefreshFacilityPopup;
            if (session.Tutorial != null) session.Tutorial.Changed += OnTutorialChanged;
            OnFacilityChanged(forest.Selection.Selected);
        }

        private void OnDisable()
        {
            if (row == null) return;
            forest.Selection.Changed -= OnFacilityChanged;
            worldInput.AreaSelectionChanged -= OnContextChanged;
            session.Exploration.Changed -= OnContextChanged;
            session.Progress.DiscoveryChanged -= RefreshPopup;
            session.Inventory.Changed -= RefreshPopup;
            facilityPanel.StatusChanged -= RefreshFacilityPopup;
            if (session.Tutorial != null) session.Tutorial.Changed -= OnTutorialChanged;
            if (selectedFacility != null) selectedFacility.Cultivation.Changed -= RefreshActions;
            selectedFacility = null;
            ClosePopup();
            row.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            popup?.Destroy();
            if (row != null) Destroy(row.gameObject);
            font.Dispose();
        }

        private void Build()
        {
            row = GameplayPopup.CreateRect("Context Actions", canvas);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(1, 0);
            row.anchoredPosition = new Vector2(-24, 24);
            actions = row.gameObject.AddComponent<CanvasGroup>();
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            build = AddButton("Build", "건설", null, 84);
            build.interactable = false;
            AddIcon(build, true);
            catalogue = AddButton("Mushroom Catalogue", "도감", () => OpenText(PopupPage.Catalogue, "버섯 도감"), 84);
            AddIcon(catalogue, false);
            AddToRow(facilityPanel.InoculateButtonTransform);
            AddToRow(facilityPanel.WaterButtonTransform);
            harvest = AddButton("Harvest", "수확", () => session.Cultivation.TryHarvest(selectedFacility));
            production = AddButton("Open Production", "종균 배양", OpenProduction);
            restore = AddButton("Restoration", "복원하기", () => OpenText(PopupPage.Restoration, "구역 복원"));
            AddToRow((RectTransform)exploration.EnterButton.transform);
            AddToRow((RectTransform)exploration.ReturnButton.transform);
            information = AddButton("Information", "정보 보기", OpenInformation);
            popup = new GameplayPopup(canvas, font, ClosePopup);
        }

        private Button AddButton(string name, string caption, System.Action action, int width = 128)
        {
            var button = GameplayPopup.CreateButton(name, caption, row, font, action);
            AddToRow((RectTransform)button.transform, width);
            return button;
        }

        private void AddToRow(RectTransform rect, int width = 128)
        {
            rect.SetParent(row, false);
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = width;
            size.preferredHeight = 52;
        }

        private static void AddIcon(Button button, bool house)
        {
            var label = button.GetComponentInChildren<Text>();
            label.fontSize = 16;
            label.rectTransform.offsetMin = new Vector2(0, 1);
            label.rectTransform.offsetMax = new Vector2(0, -26);
            var icon = GameplayPopup.CreateRect("Icon", button.transform);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1);
            icon.anchoredPosition = new Vector2(0, -14);
            if (house)
            {
                AddIconLine(icon, new Vector2(-4, 3), new Vector2(11, 2), 42);
                AddIconLine(icon, new Vector2(4, 3), new Vector2(11, 2), -42);
                AddIconLine(icon, new Vector2(0, -4), new Vector2(12, 6), 0);
            }
            else
            {
                AddIconLine(icon, new Vector2(-4.5f, 0), new Vector2(8, 13), 0);
                AddIconLine(icon, new Vector2(4.5f, 0), new Vector2(8, 13), 0);
            }
        }

        private static void AddIconLine(RectTransform icon, Vector2 position, Vector2 size, float angle)
        {
            var image = GameplayPopup.CreateImage("Mark", icon, new Color(0.84f, 0.91f, 0.74f));
            image.raycastTarget = false;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            image.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
        }

        private void OnFacilityChanged(FacilityState facility)
        {
            if (selectedFacility != null) selectedFacility.Cultivation.Changed -= RefreshActions;
            selectedFacility = facility;
            if (selectedFacility != null) selectedFacility.Cultivation.Changed += RefreshActions;
            OnContextChanged();
        }

        private void OnContextChanged()
        {
            ClosePopup();
            RefreshActions();
        }

        private void OnTutorialChanged()
        {
            var tutorial = session.Tutorial;
            if (page == PopupPage.Production && tutorial.IsMessageOpen
                && tutorial.Step != TutorialStep.FirstSpawnProduction) ClosePopup();
            if (tutorial.Step == TutorialStep.AwaitFirstExploration && tutorial.IsMessageOpen)
                forest.Selection.Select(null);
            RefreshActions();
        }

        private void RefreshActions()
        {
            bool exploring = session.Exploration.IsExploring;
            var area = worldInput.SelectedArea;
            bool facilitySelected = !exploring && selectedFacility != null;
            bool areaSelected = !exploring && !facilitySelected && area != null;
            bool defaultMenu = !exploring && !facilitySelected && !areaSelected;
            bool firstExploration = defaultMenu && session.Tutorial?.Step == TutorialStep.AwaitFirstExploration;

            build.gameObject.SetActive(defaultMenu);
            catalogue.gameObject.SetActive(defaultMenu);
            harvest.gameObject.SetActive(facilitySelected && selectedFacility.Cultivation.Stage == GrowthStage.Ready);
            harvest.interactable = facilitySelected && session.Cultivation.CanHarvest(selectedFacility);
            production.gameObject.SetActive(facilitySelected && selectedFacility.Definition.Kind == FacilityKind.SpawnWorkbench);
            restore.gameObject.SetActive(areaSelected && !area.IsExplorationUnlocked);
            exploration.EnterButton.gameObject.SetActive(firstExploration
                || areaSelected && area.Definition == smallForestPath && area.IsExplorationUnlocked);
            exploration.ReturnButton.gameObject.SetActive(exploring);
            information.gameObject.SetActive(facilitySelected || areaSelected || exploring);
        }

        private void OpenInformation()
        {
            if (session.Exploration.IsExploring) OpenText(PopupPage.Exploration, "탐험 정보");
            else if (selectedFacility != null) OpenText(PopupPage.Facility, "시설 정보");
            else if (worldInput.SelectedArea != null) OpenText(PopupPage.Area, "구역 정보");
        }

        private void OpenText(PopupPage target, string title)
        {
            page = target;
            actions.interactable = false;
            popup.ShowText(title, DescribePage());
        }

        private void OpenProduction()
        {
            if (selectedFacility?.Definition.Kind != FacilityKind.SpawnWorkbench) return;
            page = PopupPage.Production;
            actions.interactable = false;
            popup.ShowContent("종균 배양대", productionPanel.Content, productionPanel.PopupHeight);
        }

        private void ClosePopup()
        {
            page = PopupPage.None;
            popup.Hide();
            actions.interactable = true;
        }

        private void RefreshFacilityPopup()
        {
            if (page == PopupPage.Facility) popup.SetText(facilityPanel.Status);
        }

        private void RefreshPopup()
        {
            if (page != PopupPage.None && page != PopupPage.Production) popup.SetText(DescribePage());
        }

        private string DescribePage()
        {
            switch (page)
            {
                case PopupPage.Facility: return facilityPanel.Status;
                case PopupPage.Exploration: return exploration.Status;
                case PopupPage.Area: return DescribeArea(worldInput.SelectedArea);
                case PopupPage.Restoration: return DescribeRestoration(worldInput.SelectedArea);
                case PopupPage.Catalogue: return DescribeCatalogue();
                default: return "";
            }
        }

        private string DescribeArea(ForestAreaState area)
        {
            if (area == null) return "";
            string access = area.IsExplorationUnlocked ? "탐험 개방" : "탐험 잠김";
            string progress = area.Definition != smallForestPath ? "발견 정보 미등록"
                : session.Progress.FirstExplorationCollected ? "발견 1/1 · 표고버섯" : "발견 0/1 · ?";
            int stages = area.Definition.RestorationStages.Count;
            string restoration = stages > 0 ? $"\n복원 {area.CompletedRestorationStages}/{stages}" : "";
            return $"{area.Definition.DisplayName}\n\n{access}{restoration}\n{progress}";
        }

        private string DescribeRestoration(ForestAreaState area)
        {
            if (area == null) return "";
            var text = new StringBuilder(area.Definition.DisplayName);
            var stages = area.Definition.RestorationStages;
            if (area.CompletedRestorationStages < stages.Count)
            {
                text.AppendLine().AppendLine().Append("필요 버섯 (보유 / 필요)");
                foreach (var cost in stages[area.CompletedRestorationStages].Costs)
                {
                    if (cost?.Mushroom == null) continue;
                    var stock = session.Inventory.State.GetAmounts(cost.Mushroom.Id);
                    text.AppendLine().Append($"{cost.Mushroom.DisplayName}  {stock.Mushrooms} / {cost.Quantity}");
                }
            }
            return text.AppendLine().AppendLine().Append("복원 실행은 준비 중입니다.").ToString();
        }

        private string DescribeCatalogue()
        {
            var text = new StringBuilder();
            int discovered = 0;
            foreach (var mushroom in session.Mushrooms)
            {
                if (!session.Progress.IsDiscovered(mushroom.Id))
                {
                    text.AppendLine("?  ·  미발견").AppendLine();
                    continue;
                }
                discovered++;
                text.AppendLine(mushroom.DisplayName);
                if (mushroom.HasValidCultivationSettings)
                    text.AppendLine($"성장 {mushroom.GrowthDurationSeconds:0.#}초  ·  기본 수확 {mushroom.BaseHarvestYield}개");
                text.AppendLine();
            }
            return text.Insert(0, $"발견 {discovered}/{session.Mushrooms.Count}\n\n").ToString();
        }
    }
}
