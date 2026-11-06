namespace MyFreshMushroom
{
    public interface IGameplayActionRules
    {
        bool IsGameplayPaused { get; }
        double GetGrowthDelta(double requestedSeconds);
        bool AllowsEnteringExploration();
        bool AllowsCollectingExplorationMushroom();
        bool AllowsInoculation(FacilityState facility);
        bool AllowsWatering(FacilityState facility);
        bool AllowsHarvest(FacilityState facility);
        bool AllowsProduction(SpawnRecipeData recipe, int batches);
    }
}
