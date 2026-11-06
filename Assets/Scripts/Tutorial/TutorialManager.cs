using System;

namespace MyFreshMushroom
{
    public enum TutorialStep
    {
        FirstInoculation,
        FirstHarvest,
        FirstSpawnProduction,
        SecondInoculation,
        WaterSecondCrop,
        SecondHarvest,
        AwaitFirstExploration,
        Completed
    }

    public sealed class TutorialManager : IGameplayActionRules, IDisposable
    {
        private readonly ProgressState progress;
        private readonly InventoryService inventory;
        private readonly FacilityState shelf;
        private readonly FacilityState workbench;
        private readonly SpawnRecipeData recipe;
        private CultivationService cultivation;
        private SpawnProductionService production;
        private long expectedCycle;
        private int expectedYield;

        public TutorialStep Step => progress.CurrentTutorialStep;
        public bool RestrictsActions => Step < TutorialStep.AwaitFirstExploration;
        public event Action Changed;

        public TutorialManager(ProgressState progress, InventoryService inventory, FacilityState shelf,
            FacilityState workbench, SpawnRecipeData recipe)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.shelf = shelf ?? throw new ArgumentNullException(nameof(shelf));
            this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
            this.recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        }

        public void Bind(CultivationService cultivation, SpawnProductionService production)
        {
            if (this.cultivation != null) throw new InvalidOperationException("Tutorial is already bound.");
            this.cultivation = cultivation;
            this.production = production;
            cultivation.Inoculated += OnInoculated;
            cultivation.Watered += OnWatered;
            cultivation.Harvested += OnHarvested;
            production.Produced += OnProduced;
            shelf.Cultivation.Changed += NotifyChanged;
        }

        private bool NeedsRetrySpawn => Step == TutorialStep.WaterSecondCrop
            && shelf.Cultivation.Stage == GrowthStage.Empty
            && inventory.State.GetAmounts(recipe.Mushroom.Id).Spawn == 0;

        public FacilityState GuidanceTarget => !RestrictsActions ? null
            : Step == TutorialStep.FirstSpawnProduction || NeedsRetrySpawn ? workbench : shelf;

        public bool AllowsInoculation(FacilityState facility)
        {
            return !RestrictsActions || ReferenceEquals(facility, shelf)
                && (Step == TutorialStep.FirstInoculation || Step == TutorialStep.SecondInoculation
                    || Step == TutorialStep.WaterSecondCrop);
        }

        public bool AllowsWatering(FacilityState facility)
        {
            return !RestrictsActions || ReferenceEquals(facility, shelf)
                && Step == TutorialStep.WaterSecondCrop && facility.Cultivation.CycleNumber == expectedCycle;
        }

        public bool AllowsHarvest(FacilityState facility)
        {
            if (!RestrictsActions) return true;
            if (!ReferenceEquals(facility, shelf) || facility.Cultivation.CycleNumber != expectedCycle) return false;
            return (Step == TutorialStep.FirstHarvest || Step == TutorialStep.WaterSecondCrop)
                && !facility.Cultivation.HasBeenWatered
                || Step == TutorialStep.SecondHarvest && facility.Cultivation.HasBeenWatered;
        }

        public bool AllowsProduction(SpawnRecipeData selectedRecipe, int batches)
        {
            return !RestrictsActions || ReferenceEquals(recipe, selectedRecipe) && batches == 1
                && (Step == TutorialStep.FirstSpawnProduction || NeedsRetrySpawn);
        }

        private void OnInoculated(FacilityState facility)
        {
            if (!ReferenceEquals(facility, shelf)) return;
            if (Step != TutorialStep.FirstInoculation && Step != TutorialStep.SecondInoculation
                && Step != TutorialStep.WaterSecondCrop) return;
            expectedCycle = facility.Cultivation.CycleNumber;
            expectedYield = facility.Cultivation.HarvestYield;
            SetStep(Step == TutorialStep.FirstInoculation ? TutorialStep.FirstHarvest : TutorialStep.WaterSecondCrop);
        }

        private void OnWatered(FacilityState facility)
        {
            if (Step != TutorialStep.WaterSecondCrop || !ReferenceEquals(facility, shelf)
                || facility.Cultivation.CycleNumber != expectedCycle) return;
            expectedYield = facility.Cultivation.HarvestYield;
            SetStep(TutorialStep.SecondHarvest);
        }

        private void OnHarvested(HarvestResult result)
        {
            if (!ReferenceEquals(result.Facility, shelf) || result.CycleNumber != expectedCycle
                || result.MushroomId != recipe.Mushroom.Id) return;
            if (Step == TutorialStep.FirstHarvest && !result.WasWatered && result.Quantity == expectedYield)
                SetStep(TutorialStep.FirstSpawnProduction);
            else if (Step == TutorialStep.SecondHarvest && result.WasWatered && result.Quantity == expectedYield)
                SetStep(TutorialStep.AwaitFirstExploration);
            else NotifyChanged();
        }

        private void OnProduced(SpawnRecipeData producedRecipe, int batches)
        {
            if (!ReferenceEquals(producedRecipe, recipe) || batches != 1) return;
            if (Step == TutorialStep.FirstSpawnProduction) SetStep(TutorialStep.SecondInoculation);
            else NotifyChanged();
        }

        private void SetStep(TutorialStep step)
        {
            progress.CurrentTutorialStep = step;
            NotifyChanged();
        }

        private void NotifyChanged() => Changed?.Invoke();

        public string Guidance
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.FirstInoculation:
                        return "재배 안내 1/6 · 첫 접종\n강조된 선반을 선택하고 ‘종균 심기’를 눌러 주세요.";
                    case TutorialStep.FirstHarvest:
                        return shelf.Cultivation.Stage == GrowthStage.Ready
                            ? $"재배 안내 2/6 · 첫 수확\n선반을 눌러 버섯 {expectedYield}개를 수확해 주세요."
                            : "재배 안내 2/6 · 첫 성장\n버섯은 시간이 지나면 성장해요. 다 자라면 선반을 눌러 수확해 주세요.";
                    case TutorialStep.FirstSpawnProduction:
                        return $"재배 안내 3/6 · 종균 만들기\n배양대에서 {recipe.Mushroom.DisplayName} {recipe.MushroomCost}개로 종균 {recipe.SpawnOutput}개를 1회 만들어 주세요.";
                    case TutorialStep.SecondInoculation:
                        return "재배 안내 4/6 · 두 번째 접종\n선반을 선택하고 다시 ‘종균 심기’를 눌러 주세요.";
                    case TutorialStep.WaterSecondCrop:
                        if (NeedsRetrySpawn)
                            return "재배 안내 5/6 · 물 주기 재시도\n종균을 모두 사용했어요. 배양대에서 1회 제작한 뒤 다시 심어 주세요.";
                        if (shelf.Cultivation.Stage == GrowthStage.Ready)
                            return "재배 안내 5/6 · 물 주기 재시도\n물 주기 시기를 놓쳤어요. 먼저 수확한 뒤 다시 심어 주세요.";
                        return "재배 안내 5/6 · 물 주기\n선반의 성장 2~3단계에 ‘물 주기’를 눌러 주세요. 시기를 놓쳐도 다시 시도할 수 있어요.";
                    case TutorialStep.SecondHarvest:
                        return $"재배 안내 6/6 · 보너스 수확\n물 주기를 완료했어요. 다 자란 선반을 눌러 버섯 {expectedYield}개를 수확해 주세요.";
                    case TutorialStep.AwaitFirstExploration:
                        return "재배 안내 완료 · 첫 탐험 대기\n탐험 기능은 다음 개발 단계에서 연결합니다. 그동안 재배와 종균 제작을 자유롭게 계속할 수 있어요.";
                    default:
                        return "튜토리얼 완료";
                }
            }
        }

        public void Dispose()
        {
            if (cultivation != null)
            {
                cultivation.Inoculated -= OnInoculated;
                cultivation.Watered -= OnWatered;
                cultivation.Harvested -= OnHarvested;
            }
            if (production != null) production.Produced -= OnProduced;
            shelf.Cultivation.Changed -= NotifyChanged;
            cultivation = null;
            production = null;
        }
    }
}
