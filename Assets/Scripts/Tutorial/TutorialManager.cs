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
        WateringComplete,
        SecondHarvest,
        AwaitFirstExploration,
        FirstExploration,
        FindFirstMushroom,
        Completed
    }

    public sealed class TutorialManager : IGameplayActionRules, IDisposable
    {
        private readonly ProgressState progress;
        private readonly FacilityState shelf;
        private readonly FacilityState workbench;
        private readonly SpawnRecipeData recipe;
        private CultivationService cultivation;
        private SpawnProductionService production;
        private ExplorationService exploration;
        private long expectedCycle;
        private int expectedYield;
        private bool waterMessageShown;
        private bool harvestMessageShown;

        public TutorialStep Step => progress.CurrentTutorialStep;
        public bool RestrictsActions => Step < TutorialStep.AwaitFirstExploration;
        public bool IsMessageOpen { get; private set; } = true;
        public bool IsGameplayPaused => IsMessageOpen;
        public bool CanContinueMessage => IsMessageOpen && (Step == TutorialStep.FirstHarvest
            && shelf.Cultivation.Stage != GrowthStage.Ready || Step == TutorialStep.WateringComplete
            || Step >= TutorialStep.AwaitFirstExploration);
        public event Action Changed;

        public TutorialManager(ProgressState progress, FacilityState shelf,
            FacilityState workbench, SpawnRecipeData recipe)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
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
            shelf.Cultivation.Changed += OnCropChanged;
        }

        public FacilityState GuidanceTarget => !RestrictsActions ? null
            : Step == TutorialStep.FirstSpawnProduction ? workbench : shelf;

        public void ContinueMessage()
        {
            if (!CanContinueMessage) return;
            if (Step == TutorialStep.WateringComplete)
            {
                SetStep(TutorialStep.SecondHarvest);
                return;
            }
            if (Step == TutorialStep.FirstExploration)
            {
                SetStep(TutorialStep.FindFirstMushroom);
                return;
            }
            IsMessageOpen = false;
            NotifyChanged();
        }

        public bool AllowsEnteringExploration() => !IsMessageOpen && Step >= TutorialStep.AwaitFirstExploration;
        public bool AllowsCollectingExplorationMushroom() => !IsMessageOpen
            && (Step == TutorialStep.FindFirstMushroom || Step == TutorialStep.Completed);

        public void BindExploration(ExplorationService exploration)
        {
            this.exploration = exploration;
            exploration.Entered += OnExplorationEntered;
            exploration.Collected += OnExplorationCollected;
        }

        private void OnExplorationEntered()
        {
            if (Step == TutorialStep.AwaitFirstExploration) SetStep(TutorialStep.FirstExploration);
        }

        private void OnExplorationCollected(string mushroomId, int quantity)
        {
            if (Step == TutorialStep.FindFirstMushroom && mushroomId == exploration.MushroomId
                && quantity > 0 && progress.FirstExplorationCollected
                && progress.IsDiscovered(mushroomId)) SetStep(TutorialStep.Completed);
        }

        public double GetGrowthDelta(double requestedSeconds)
        {
            if (IsGameplayPaused) return 0;
            // Stop exactly when watering becomes possible, even after a long frame.
            if (Step == TutorialStep.WaterSecondCrop && !waterMessageShown
                && shelf.Cultivation.Stage == GrowthStage.Mycelium)
                return Math.Min(requestedSeconds, shelf.Cultivation.SecondsUntilWatering);
            return requestedSeconds;
        }

        public bool AllowsInoculation(FacilityState facility)
        {
            if (CanContinueMessage) return false;
            return !RestrictsActions || ReferenceEquals(facility, shelf)
                && (Step == TutorialStep.FirstInoculation || Step == TutorialStep.SecondInoculation);
        }

        public bool AllowsWatering(FacilityState facility)
        {
            if (CanContinueMessage) return false;
            return !RestrictsActions || ReferenceEquals(facility, shelf)
                && Step == TutorialStep.WaterSecondCrop && facility.Cultivation.CycleNumber == expectedCycle;
        }

        public bool AllowsHarvest(FacilityState facility)
        {
            if (CanContinueMessage) return false;
            if (!RestrictsActions) return true;
            if (!ReferenceEquals(facility, shelf) || facility.Cultivation.CycleNumber != expectedCycle) return false;
            return Step == TutorialStep.FirstHarvest
                && !facility.Cultivation.HasBeenWatered
                || Step == TutorialStep.SecondHarvest && facility.Cultivation.HasBeenWatered;
        }

        public bool AllowsProduction(SpawnRecipeData selectedRecipe, int batches)
        {
            if (CanContinueMessage) return false;
            return !RestrictsActions || ReferenceEquals(recipe, selectedRecipe) && batches == 1
                && Step == TutorialStep.FirstSpawnProduction;
        }

        private void OnInoculated(FacilityState facility)
        {
            if (!ReferenceEquals(facility, shelf)) return;
            if (Step != TutorialStep.FirstInoculation && Step != TutorialStep.SecondInoculation) return;
            expectedCycle = facility.Cultivation.CycleNumber;
            expectedYield = facility.Cultivation.HarvestYield;
            SetStep(Step == TutorialStep.FirstInoculation ? TutorialStep.FirstHarvest : TutorialStep.WaterSecondCrop);
        }

        private void OnWatered(FacilityState facility)
        {
            if (Step != TutorialStep.WaterSecondCrop || !ReferenceEquals(facility, shelf)
                || facility.Cultivation.CycleNumber != expectedCycle) return;
            expectedYield = facility.Cultivation.HarvestYield;
            SetStep(TutorialStep.WateringComplete);
        }

        private void OnHarvested(HarvestResult result)
        {
            if (!ReferenceEquals(result.Facility, shelf) || result.CycleNumber != expectedCycle
                || result.MushroomId != recipe.Mushroom.Id) return;
            if (Step == TutorialStep.FirstHarvest && !result.WasWatered && result.Quantity == expectedYield)
                SetStep(TutorialStep.FirstSpawnProduction);
            else if (Step == TutorialStep.SecondHarvest && result.WasWatered && result.Quantity == expectedYield)
                SetStep(TutorialStep.AwaitFirstExploration);
        }

        private void OnProduced(SpawnRecipeData producedRecipe, int batches)
        {
            if (!ReferenceEquals(producedRecipe, recipe) || batches != 1) return;
            if (Step == TutorialStep.FirstSpawnProduction) SetStep(TutorialStep.SecondInoculation);
        }

        private void SetStep(TutorialStep step)
        {
            progress.CurrentTutorialStep = step;
            waterMessageShown = false;
            harvestMessageShown = false;
            IsMessageOpen = step != TutorialStep.WaterSecondCrop && step != TutorialStep.FindFirstMushroom
                && (step != TutorialStep.SecondHarvest || shelf.Cultivation.Stage == GrowthStage.Ready);
            NotifyChanged();
        }

        private void OnCropChanged()
        {
            if (Step == TutorialStep.WaterSecondCrop && !waterMessageShown && shelf.Cultivation.CanWater)
            {
                waterMessageShown = true;
                IsMessageOpen = true;
            }
            else if ((Step == TutorialStep.FirstHarvest || Step == TutorialStep.SecondHarvest)
                && !harvestMessageShown && shelf.Cultivation.Stage == GrowthStage.Ready)
            {
                harvestMessageShown = true;
                IsMessageOpen = true;
            }
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
                        return "재배 안내 1/7 · 첫 접종\n선반을 선택하고 ‘종균 심기’를 눌러 주세요.";
                    case TutorialStep.FirstHarvest:
                        return shelf.Cultivation.Stage == GrowthStage.Ready
                            ? $"재배 안내 2/7 · 첫 수확\n선반을 선택하고 ‘수확’을 눌러 버섯 {expectedYield}개를 얻어 주세요."
                            : "재배 안내 2/7 · 첫 성장\n버섯이 다 자라면 선반을 선택하고 ‘수확’을 눌러 주세요.";
                    case TutorialStep.FirstSpawnProduction:
                        return $"재배 안내 3/7 · 종균 만들기\n배양대를 선택하고 ‘종균 배양’을 열어 {recipe.Mushroom.DisplayName} {recipe.MushroomCost}개로 종균 {recipe.SpawnOutput}개를 만들어 주세요.";
                    case TutorialStep.SecondInoculation:
                        return "재배 안내 4/7 · 두 번째 접종\n선반을 선택하고 다시 ‘종균 심기’를 눌러 주세요.";
                    case TutorialStep.WaterSecondCrop:
                        return "재배 안내 5/7 · 물 주기\n선반을 선택하고 ‘물 주기’를 눌러 주세요. 물을 주면 수확량이 늘어납니다.";
                    case TutorialStep.WateringComplete:
                        return $"재배 안내 6/7 · 물 주기 완료\n물 주기로 예상 수확량이 {expectedYield}개로 늘었어요. ‘계속’을 누르고 버섯이 다 자랄 때까지 기다려 주세요.";
                    case TutorialStep.SecondHarvest:
                        return $"재배 안내 7/7 · 보너스 수확\n선반을 선택하고 ‘수확’을 눌러 버섯 {expectedYield}개를 얻어 주세요.";
                    case TutorialStep.AwaitFirstExploration:
                        return "재배 안내 완료 · 첫 탐험\n‘계속’을 누른 뒤 오른쪽 아래 ‘탐험하기’를 눌러 주세요.";
                    case TutorialStep.FirstExploration:
                        return "첫 탐험 · 숲 둘러보기\n마우스를 누른 채 끌면 화면이 이동하고, 휠을 굴리면 확대·축소됩니다. 통나무 옆 버섯을 찾아 한 번 눌러 주세요.";
                    case TutorialStep.FindFirstMushroom:
                        return "첫 탐험 · 버섯 발견\n통나무 주변을 살펴보고 보이는 버섯을 눌러 주세요.";
                    default:
                        return "튜토리얼 완료 · 표고버섯 발견!\n표고버섯을 얻었어요. ‘계속’을 누른 뒤 숲으로 돌아가 재배를 이어가세요.";
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
            if (exploration != null)
            {
                exploration.Entered -= OnExplorationEntered;
                exploration.Collected -= OnExplorationCollected;
            }
            shelf.Cultivation.Changed -= OnCropChanged;
            cultivation = null;
            production = null;
            exploration = null;
        }
    }
}
