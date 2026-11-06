namespace MyFreshMushroom
{
    public interface IGameplayActionRules
    {
        bool AllowsInoculation(FacilityState facility);
        bool AllowsWatering(FacilityState facility);
        bool AllowsHarvest(FacilityState facility);
        bool AllowsProduction(SpawnRecipeData recipe, int batches);
    }
}
