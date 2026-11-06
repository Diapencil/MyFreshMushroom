using System;
using System.Collections.Generic;

namespace MyFreshMushroom
{
    public sealed class InventoryService
    {
        public InventoryState State { get; }
        public event Action Changed;

        public InventoryService(IEnumerable<string> mushroomIds)
        {
            State = new InventoryState(mushroomIds);
        }

        public bool CanAfford(string mushroomId, ResourceKind kind, int amount)
        {
            if (amount <= 0 || !IsValidKind(kind) || !State.TryGetAmounts(mushroomId, out var current))
                return false;

            return GetQuantity(current, kind) >= amount;
        }

        public bool TryAdd(string mushroomId, ResourceKind kind, int amount)
        {
            if (amount <= 0 || !IsValidKind(kind) || !State.TryGetAmounts(mushroomId, out var current))
                return false;

            int quantity = GetQuantity(current, kind);
            if (quantity > int.MaxValue - amount)
                return false;

            Apply(mushroomId, kind, current, quantity + amount);
            return true;
        }

        public bool TrySpend(string mushroomId, ResourceKind kind, int amount)
        {
            if (!CanAfford(mushroomId, kind, amount))
                return false;

            var current = State.GetAmounts(mushroomId);
            Apply(mushroomId, kind, current, GetQuantity(current, kind) - amount);
            return true;
        }

        public bool CanExchangeMushroomsForSpawn(string mushroomId, int mushroomCost, int spawnOutput)
        {
            return mushroomCost > 0 && spawnOutput > 0
                && State.TryGetAmounts(mushroomId, out var current)
                && current.Mushrooms >= mushroomCost && current.Spawn <= int.MaxValue - spawnOutput;
        }

        public bool TryExchangeMushroomsForSpawn(string mushroomId, int mushroomCost, int spawnOutput)
        {
            if (!CanExchangeMushroomsForSpawn(mushroomId, mushroomCost, spawnOutput)) return false;
            var current = State.GetAmounts(mushroomId);
            // Publish only after both sides of the transaction have committed.
            State.SetAmounts(mushroomId, new InventoryAmounts(current.Mushrooms - mushroomCost, current.Spawn + spawnOutput));
            Changed?.Invoke();
            return true;
        }

        private void Apply(string mushroomId, ResourceKind kind, InventoryAmounts current, int quantity)
        {
            var updated = kind == ResourceKind.Mushroom
                ? new InventoryAmounts(quantity, current.Spawn)
                : new InventoryAmounts(current.Mushrooms, quantity);

            State.SetAmounts(mushroomId, updated);
            Changed?.Invoke();
        }

        private static int GetQuantity(InventoryAmounts amounts, ResourceKind kind)
        {
            return kind == ResourceKind.Mushroom ? amounts.Mushrooms : amounts.Spawn;
        }

        private static bool IsValidKind(ResourceKind kind)
        {
            return kind == ResourceKind.Mushroom || kind == ResourceKind.Spawn;
        }
    }
}
