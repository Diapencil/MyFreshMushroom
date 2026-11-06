using System.Collections.Generic;

namespace MyFreshMushroom
{
    public sealed class ProgressState
    {
        public TutorialStep CurrentTutorialStep { get; internal set; } = TutorialStep.FirstInoculation;
        private readonly HashSet<string> discoveredMushrooms = new HashSet<string>();

        public bool IsDiscovered(string mushroomId)
        {
            return mushroomId != null && discoveredMushrooms.Contains(mushroomId);
        }

        public bool TryDiscover(string mushroomId)
        {
            return !string.IsNullOrWhiteSpace(mushroomId) && discoveredMushrooms.Add(mushroomId);
        }
    }
}
