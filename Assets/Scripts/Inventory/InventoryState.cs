using System;
using System.Collections.Generic;

namespace MyFreshMushroom
{
    public enum ResourceKind
    {
        Mushroom,
        Spawn
    }

    public readonly struct InventoryAmounts
    {
        public int Mushrooms { get; }
        public int Spawn { get; }

        internal InventoryAmounts(int mushrooms, int spawn)
        {
            Mushrooms = mushrooms;
            Spawn = spawn;
        }
    }

    // Mutation stays inside InventoryService; views can only read balances.
    public sealed class InventoryState
    {
        private readonly Dictionary<string, InventoryAmounts> amounts =
            new Dictionary<string, InventoryAmounts>(StringComparer.Ordinal);

        internal InventoryState(IEnumerable<string> mushroomIds)
        {
            if (mushroomIds == null)
                throw new ArgumentNullException(nameof(mushroomIds));

            foreach (string id in mushroomIds)
            {
                if (string.IsNullOrWhiteSpace(id) || amounts.ContainsKey(id))
                    throw new ArgumentException("Mushroom IDs must be non-empty and unique.", nameof(mushroomIds));

                amounts.Add(id, new InventoryAmounts(0, 0));
            }
        }

        public InventoryAmounts GetAmounts(string mushroomId)
        {
            if (mushroomId == null)
                throw new ArgumentNullException(nameof(mushroomId));

            return amounts[mushroomId];
        }

        internal bool TryGetAmounts(string mushroomId, out InventoryAmounts value)
        {
            value = default;
            return mushroomId != null && amounts.TryGetValue(mushroomId, out value);
        }

        internal void SetAmounts(string mushroomId, InventoryAmounts value)
        {
            amounts[mushroomId] = value;
        }
    }
}
