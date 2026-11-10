using UnityEngine;
using UnityEngine.UI;
using System;

namespace MyFreshMushroom
{
    public sealed class FacilityPanel : MonoBehaviour
    {
        [SerializeField] private ForestGridView forest;
        [SerializeField] private Text statusText;
        [SerializeField] private GameManager session;
        [SerializeField] private Button inoculateButton;
        [SerializeField] private Button waterButton;
        private FacilitySelection selection;
        private FacilityState observedFacility;
        private InventoryService inventory;
        private TutorialManager tutorial;
        private readonly KoreanUiFont uiFont = new KoreanUiFont();
        public RectTransform InoculateButtonTransform => (RectTransform)inoculateButton.transform;
        public RectTransform WaterButtonTransform => (RectTransform)waterButton.transform;
        public string Status => statusText.text;
        public event Action StatusChanged;

        private void OnEnable()
        {
            if (forest == null || statusText == null || session == null || inoculateButton == null || waterButton == null)
            {
                Debug.LogError("FacilityPanel requires forest, status text, GameManager, inoculation and watering buttons.", this);
                return;
            }
            forest.Initialize();
            if (!session.Initialize()) return;
            inventory = session.Inventory;
            if (TryGetComponent<Image>(out var background)) background.enabled = false;
            statusText.enabled = false;
            tutorial = session.Tutorial;
            if (tutorial != null) tutorial.Changed += RefreshCurrent;
            inoculateButton.onClick.AddListener(InoculateSelected);
            waterButton.onClick.AddListener(WaterSelected);
            uiFont.Apply(statusText);
            ConfigureButtonLabel();
            var waterLabel = waterButton.GetComponentInChildren<Text>(true);
            if (waterLabel != null)
            {
                uiFont.Apply(waterLabel);
                waterLabel.text = "물 주기";
            }
            inventory.Changed += RefreshCurrent;
            selection = forest.Selection;
            selection.Changed += Observe;
            Observe(selection.Selected);
        }

        private void OnDisable()
        {
            if (selection != null) selection.Changed -= Observe;
            if (observedFacility != null) observedFacility.Cultivation.Changed -= RefreshCurrent;
            if (inventory != null) inventory.Changed -= RefreshCurrent;
            if (tutorial != null) tutorial.Changed -= RefreshCurrent;
            tutorial = null;
            if (inoculateButton != null) inoculateButton.onClick.RemoveListener(InoculateSelected);
            if (waterButton != null) waterButton.onClick.RemoveListener(WaterSelected);
            inventory = null;
            observedFacility = null;
            selection = null;
        }

        private void Observe(FacilityState state)
        {
            if (observedFacility != null) observedFacility.Cultivation.Changed -= RefreshCurrent;
            observedFacility = state;
            if (observedFacility != null) observedFacility.Cultivation.Changed += RefreshCurrent;
            Refresh(state);
        }

        private void RefreshCurrent() => Refresh(observedFacility);

        private void InoculateSelected()
        {
            if (observedFacility == null || observedFacility.Definition.Kind != FacilityKind.CultivationShelf
                || observedFacility.Cultivation.Stage != GrowthStage.Empty || !session.IsInitialized)
                return;
            session.Cultivation.TryInoculate(observedFacility);
            RefreshCurrent();
        }

        private void ConfigureButtonLabel()
        {
            var label = inoculateButton.GetComponentInChildren<Text>(true);
            if (label == null) return;
            uiFont.Apply(label);
            label.text = "종균 심기";
        }

        private void WaterSelected()
        {
            if (observedFacility == null || !session.IsInitialized) return;
            session.Cultivation.TryWater(observedFacility);
            RefreshCurrent();
        }

        private void OnDestroy()
        {
            uiFont.Dispose();
        }

        private void Refresh(FacilityState state)
        {
            bool canWater = session.Cultivation.CanWater(state);
            waterButton.gameObject.SetActive(canWater);
            waterButton.interactable = canWater;
            bool emptyShelf = state != null && state.Definition.Kind == FacilityKind.CultivationShelf
                && state.Cultivation.Stage == GrowthStage.Empty;
            inoculateButton.gameObject.SetActive(emptyShelf);
            inoculateButton.interactable = emptyShelf && session.Cultivation.CanInoculate(state);
            statusText.text = Describe(state);
            StatusChanged?.Invoke();
        }

        private string Describe(FacilityState state)
        {
            if (state == null)
                return "시설 선택\n재배 시설이나 종균 배양대를 눌러 주세요.";
            var data = state.Definition;
            if (data.Kind == FacilityKind.SpawnWorkbench)
                return $"{data.DisplayName}\n공용 시설";
            var crop = state.Cultivation;
            if (crop.Stage == GrowthStage.Empty)
            {
                var mushroom = data.SupportedMushroom;
                string action = !session.Progress.IsDiscovered(mushroom.Id) ? "탐험에서 먼저 발견해 주세요"
                    : !mushroom.HasValidCultivationSettings ? "재배 설정을 확인해 주세요"
                    : !inventory.CanAfford(mushroom.Id, ResourceKind.Spawn, 1) ? "종균이 부족합니다"
                    : !session.Cultivation.CanInoculate(state) ? "튜토리얼 안내를 진행해 주세요" : "종균 1개 필요";
                return $"{data.DisplayName}\n접종 대기 | {action}";
            }
            if (crop.Stage == GrowthStage.Ready)
                return $"{data.DisplayName}\n수확 가능 | 버섯 {crop.HarvestYield}개";
            string watered = crop.HasBeenWatered ? " | 물 주기 완료" : "";
            return $"{data.DisplayName}\n성장 {(int)crop.Stage}/4단계 | 남은 시간 {Math.Ceiling(crop.RemainingSeconds):0}초\n예상 수확량: {crop.HarvestYield}개{watered}";
        }
    }
}
