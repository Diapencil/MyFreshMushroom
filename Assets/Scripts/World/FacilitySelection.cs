using System;
using System.Collections.Generic;

namespace MyFreshMushroom
{
    public sealed class FacilitySelection
    {
        private readonly HashSet<FacilityState> facilities;
        public FacilityState Selected { get; private set; }
        public event Action<FacilityState> Changed;

        public FacilitySelection(IEnumerable<FacilityState> facilities)
        {
            this.facilities = new HashSet<FacilityState>(facilities);
        }

        public bool Select(FacilityState facility)
        {
            if ((facility != null && !facilities.Contains(facility)) || ReferenceEquals(Selected, facility))
                return false;
            Selected = facility;
            Changed?.Invoke(Selected);
            return true;
        }
    }
}
