using System;
using UnityEngine;

namespace MyFreshMushroom
{
    public sealed class FacilityState
    {
        public string InstanceId { get; }
        public FacilityData Definition { get; }
        public Vector2Int Origin { get; }
        public CultivationState Cultivation { get; } = new CultivationState();

        public FacilityState(string instanceId, FacilityData definition, Vector2Int origin)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("An instance ID is required.", nameof(instanceId));
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id)
                || definition.Footprint.x <= 0 || definition.Footprint.y <= 0)
                throw new ArgumentException("A valid facility definition is required.", nameof(definition));
            InstanceId = instanceId;
            Definition = definition;
            Origin = origin;
        }

        public bool ContainsCell(Vector2Int cell)
        {
            Vector2Int size = Definition.Footprint;
            return cell.x >= Origin.x && cell.y >= Origin.y
                && cell.x < (long)Origin.x + size.x && cell.y < (long)Origin.y + size.y;
        }
    }
}
