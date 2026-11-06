using System.Collections.Generic;

namespace MyFreshMushroom
{
    public sealed class ProgressState
    {
        public TutorialStep CurrentTutorialStep { get; internal set; } = TutorialStep.FirstInoculation;
        public bool FirstExplorationCollected { get; internal set; }
        public event System.Action DiscoveryChanged;
        private readonly HashSet<string> discoveredMushrooms = new HashSet<string>();

        public bool IsDiscovered(string mushroomId)
        {
            return mushroomId != null && discoveredMushrooms.Contains(mushroomId);
        }

        public bool TryDiscover(string mushroomId)
        {
            if (string.IsNullOrWhiteSpace(mushroomId) || !discoveredMushrooms.Add(mushroomId)) return false;
            DiscoveryChanged?.Invoke();
            return true;
        }
    }
}
