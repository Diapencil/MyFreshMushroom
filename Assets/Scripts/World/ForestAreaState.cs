using System;

namespace MyFreshMushroom
{
    public sealed class ForestAreaState
    {
        public ForestAreaData Definition { get; }
        public int CompletedRestorationStages { get; internal set; }
        public double RemainingCooldownSeconds { get; internal set; }

        public bool IsExplorationUnlocked
        {
            get
            {
                if (Definition.StartsOpenForExploration) return true;
                for (int i = 0; i < CompletedRestorationStages; i++)
                    if (Definition.RestorationStages[i].OpensExploration) return true;
                return false;
            }
        }

        internal ForestAreaState(ForestAreaData definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
    }
}
