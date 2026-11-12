using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    public sealed class GameplayActionMenu : MonoBehaviour
    {
        [SerializeField] private WorldInteractionController worldInput;
        [SerializeField] private GameManager session;
        [SerializeField] private ExplorationManager exploration;
        [SerializeField] private ForestGridView forest;
        [SerializeField] private FacilityPanel facilityPanel;
        [SerializeField] private SpawnProductionPanel productionPanel;
        [SerializeField] private bool showCooldownAsMinutesAndSeconds;

        private enum PopupPage { None, Facility, Area, Restoration, Catalogue, Production, Exploration, ExitExploration }

        private readonly KoreanUiFont font = new KoreanUiFont();
        [SerializeField] private RectTransform row;
        [SerializeField] private CanvasGroup actions;
        [SerializeField] private GameplayPopup popup;
        [SerializeField] private GameplayPopup productionPopup;
        private bool bound;
        private PopupPage page;
        private FacilityState selectedFacility;
        [SerializeField] private Button build;
        [SerializeField] private Button catalogue;
        [SerializeField] private Button harvest;
        [SerializeField] private Button production;
        [SerializeField] private Button restore;
        [SerializeField] private Button information;
        private int restorationStage;
        private bool hintRevealed;

        public RectTransform HarvestTarget => harvest != null ? (RectTransform)harvest.transform : null;
        public RectTransform ProductionTarget => production == null ? null
            : page == PopupPage.Production ? productionPopup.Window : (RectTransform)production.transform;

        private void OnEnable()
        {
            if (worldInput == null || session == null || exploration == null
                || forest == null || facilityPanel == null || productionPanel == null
                || row == null || actions == null || popup == null || productionPopup == null
                || build == null || catalogue == null || harvest == null || production == null
                || restore == null || information == null)
            {
                Debug.LogError("GameplayActionMenu requires its prefab UI, selection and gameplay panel references.", this);
                return;
            }
            if (!session.Initialize()) return;
            if (!popup.Initialize(font, ClosePopupFromButton, ConfirmRestoration)
                || !productionPopup.Initialize(font, ClosePopupFromButton, null)) return;
            foreach (var label in row.GetComponentsInChildren<Text>(true)) font.Apply(label);
            catalogue.onClick.AddListener(OpenCatalogue);
            harvest.onClick.AddListener(HarvestSelected);
            production.onClick.AddListener(OpenProduction);
            restore.onClick.AddListener(OpenRestoration);
            information.onClick.AddListener(OpenInformation);
            bound = true;
            row.gameObject.SetActive(true);
            forest.Selection.Changed += OnFacilityChanged;
            forest.RestorationPresentationChanged += RefreshActions;
            worldInput.AreaSelectionChanged += OnContextChanged;
            session.Exploration.Changed += OnContextChanged;
            session.Exploration.CooldownChanged += OnCooldownChanged;
            session.Progress.DiscoveryChanged += RefreshPopup;
            session.Inventory.Changed += RefreshPopup;
            facilityPanel.StatusChanged += RefreshFacilityPopup;
            if (session.Tutorial != null) session.Tutorial.Changed += OnTutorialChanged;
            OnFacilityChanged(forest.Selection.Selected);
        }

        private void OnDisable()
        {
            if (!bound) return;
            bound = false;
            catalogue.onClick.RemoveListener(OpenCatalogue);
            harvest.onClick.RemoveListener(HarvestSelected);
            production.onClick.RemoveListener(OpenProduction);
            restore.onClick.RemoveListener(OpenRestoration);
            information.onClick.RemoveListener(OpenInformation);
            forest.Selection.Changed -= OnFacilityChanged;
            forest.RestorationPresentationChanged -= RefreshActions;
            worldInput.AreaSelectionChanged -= OnContextChanged;
            session.Exploration.Changed -= OnContextChanged;
            session.Exploration.CooldownChanged -= OnCooldownChanged;
            session.Progress.DiscoveryChanged -= RefreshPopup;
            session.Inventory.Changed -= RefreshPopup;
            facilityPanel.StatusChanged -= RefreshFacilityPopup;
            if (session.Tutorial != null) session.Tutorial.Changed -= OnTutorialChanged;
            if (selectedFacility != null) selectedFacility.Cultivation.Changed -= RefreshActions;
            selectedFacility = null;
            ClosePopup();
            if (row != null) row.gameObject.SetActive(false);
        }

        private void OnDestroy() => font.Dispose();

        private void OpenCatalogue() => OpenText(PopupPage.Catalogue, "버섯 도감");
        private void HarvestSelected() => session.Cultivation.TryHarvest(selectedFacility);

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
            if (session.Exploration.IsExitConfirmationOpen)
            {
                page = PopupPage.ExitExploration;
                hintRevealed = false;
                popup.ShowExitConfirmation(session.Exploration.RemainingCount > 0, exploration.ConfirmReturn, ShowHint);
                RefreshPopup();
            }
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
            if (page == PopupPage.Restoration) RefreshPopup();
        }

        private void OnCooldownChanged()
        {
            RefreshActions();
            if (page == PopupPage.Area) RefreshPopup();
        }

        private string FormatCooldown(double remaining)
        {
            double seconds = System.Math.Ceiling(remaining);
            return showCooldownAsMinutesAndSeconds
                ? $"{System.Math.Floor(seconds / 60):0}:{seconds % 60:00}" : $"{seconds:0}초";
        }

        private void RefreshActions()
        {
            actions.interactable = page == PopupPage.None && !forest.IsPresentingRestoration;
            if (forest.IsPresentingRestoration) return;
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
            restore.gameObject.SetActive(areaSelected
                && area.CompletedRestorationStages < area.Definition.RestorationStages.Count);
            exploration.EnterButton.gameObject.SetActive(firstExploration || areaSelected && area.IsExplorationUnlocked);
            var entryArea = exploration.EntryArea;
            exploration.EnterButton.interactable = session.Exploration.CanEnter(entryArea)
                && entryArea.Definition.HasExplorationMaps;
            var enterLabel = exploration.EnterButton.GetComponentInChildren<Text>(true);
            if (enterLabel != null)
                enterLabel.text = entryArea != null && entryArea.RemainingCooldownSeconds > 0
                    ? $"탐험가기\n{FormatCooldown(entryArea.RemainingCooldownSeconds)}" : "탐험가기";
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
            productionPopup.ShowContent("종균 배양대", productionPanel.PopupHeight);
        }

        private void OpenRestoration()
        {
            var area = worldInput.SelectedArea;
            if (area == null || session.IsGameplayPaused || forest.IsPresentingRestoration) return;
            restorationStage = area.CompletedRestorationStages;
            page = PopupPage.Restoration;
            actions.interactable = false;
            popup.ShowConfirmation("구역 복원");
            RefreshPopup();
        }

        private void ConfirmRestoration()
        {
            if (page != PopupPage.Restoration) return;
            if (session.Restoration.TryRestore(worldInput.SelectedArea, restorationStage))
                OnContextChanged();
            else
                RefreshPopup();
        }

        private void ClosePopup()
        {
            page = PopupPage.None;
            if (popup != null) popup.Hide();
            if (productionPopup != null) productionPopup.Hide();
            if (actions != null) actions.interactable = !forest.IsPresentingRestoration;
        }

        private void ClosePopupFromButton()
        {
            if (page == PopupPage.ExitExploration) session.Exploration.ContinueExploration();
            else ClosePopup();
        }

        private void ShowHint()
        {
            if (page != PopupPage.ExitExploration || !session.Exploration.IsExitConfirmationOpen) return;
            hintRevealed = true;
            RefreshPopup();
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
                case PopupPage.ExitExploration: return DescribeExit();
                case PopupPage.Area: return DescribeArea(worldInput.SelectedArea);
                case PopupPage.Restoration: return DescribeRestoration(worldInput.SelectedArea);
                case PopupPage.Catalogue: return DescribeCatalogue();
                default: return "";
            }
        }

        private string DescribeExit()
        {
            var current = session.Exploration;
            string message = current.RemainingCount == 0
                ? "모든 버섯을 찾았습니다."
                : $"아직 찾지 못한 버섯이 있습니다.\n남은 채집 대상 {current.RemainingCount}개";
            message += "\n\n재배지로 돌아갈까요?\n이미 획득한 버섯은 그대로 보유합니다.";
            if (hintRevealed && current.TryGetRemainingHint(out var hint)) message += $"\n\n힌트\n{hint}";
            return message;
        }

        private string DescribeArea(ForestAreaState area)
        {
            if (area == null) return "";
            string access = area.IsExplorationUnlocked ? "탐험 개방" : "탐험 잠김";
            if (area.IsExplorationUnlocked && area.RemainingCooldownSeconds > 0)
                access = $"재탐험 대기 · {FormatCooldown(area.RemainingCooldownSeconds)}";
            var species = new Dictionary<string, MushroomData>();
            if (area.Definition == session.Exploration.FirstArea)
                species.Add(session.Exploration.MushroomId, session.Exploration.FirstMushroom);
            foreach (var prefab in area.Definition.ExplorationMapPrefabs ?? System.Array.Empty<GameObject>())
            {
                if (prefab == null || !prefab.TryGetComponent<ExplorationMap>(out var map) || map.Collectibles == null) continue;
                foreach (var target in map.Collectibles)
                    if (target?.Mushroom != null && !string.IsNullOrWhiteSpace(target.Mushroom.Id))
                        species[target.Mushroom.Id] = target.Mushroom;
            }
            int discovered = 0;
            var names = new StringBuilder();
            foreach (var mushroom in species.Values)
            {
                bool known = session.Progress.IsDiscovered(mushroom.Id);
                if (known) discovered++;
                names.AppendLine().Append(known ? mushroom.DisplayName : "?");
            }
            string progress = $"발견 {discovered}/{species.Count}{names}";
            int stages = area.Definition.RestorationStages.Count;
            string restoration = stages > 0 ? $"\n복원 {area.CompletedRestorationStages}/{stages}" : "";
            return $"{area.Definition.DisplayName}\n\n{access}{restoration}\n{progress}";
        }

        private string DescribeRestoration(ForestAreaState area)
        {
            if (!session.Restoration.TryGetCosts(area, restorationStage, out var costs))
            {
                popup.SetConfirmationInteractable(false);
                return "현재 복원할 수 있는 단계가 없습니다.";
            }

            bool sufficient = true;
            var text = new StringBuilder(area.Definition.DisplayName)
                .AppendLine().AppendLine($"복원 {restorationStage + 1}/{area.Definition.RestorationStages.Count}단계")
                .AppendLine().Append("필요 버섯 (보유 / 필요)");
            foreach (var mushroom in session.Mushrooms)
            {
                if (!costs.TryGetValue(mushroom.Id, out int required)) continue;
                int stock = session.Inventory.State.GetAmounts(mushroom.Id).Mushrooms;
                text.AppendLine().Append($"{mushroom.DisplayName}  {stock} / {required}");
                if (stock >= required) continue;
                sufficient = false;
                text.Append($"  ({required - stock}개 부족)");
            }
            popup.SetConfirmationInteractable(sufficient && !session.IsGameplayPaused);
            return text.ToString();
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
