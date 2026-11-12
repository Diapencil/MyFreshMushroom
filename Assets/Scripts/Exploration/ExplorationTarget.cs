namespace MyFreshMushroom
{
    public sealed class ExplorationTarget
    {
        public string Id { get; }
        public MushroomData Mushroom { get; }
        public int FirstDiscoveryYield { get; }
        public int RepeatExplorationYield { get; }
        public string Hint { get; }

        public ExplorationTarget(string id, MushroomData mushroom, int firstDiscoveryYield, int repeatExplorationYield,
            string hint = null)
        {
            Id = id;
            Mushroom = mushroom;
            FirstDiscoveryYield = firstDiscoveryYield;
            RepeatExplorationYield = repeatExplorationYield;
            Hint = hint;
        }
    }
}
