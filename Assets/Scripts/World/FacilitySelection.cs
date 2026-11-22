using System;
using System.Collections.Generic;

namespace MyFreshMushroom
{
    public sealed class FacilitySelection
    {
        internal HashSet<FacilityState> Registered { get; }
        public FacilityState Selected { get; private set; }
        public event Action<FacilityState> Changed;

        public FacilitySelection(IEnumerable<FacilityState> facilities)
        {
            Registered = new HashSet<FacilityState>(facilities);
        }

        public bool Select(FacilityState facility)
        {
            if ((facility != null && !Registered.Contains(facility)) || ReferenceEquals(Selected, facility))
                return false;
            Selected = facility;
            Changed?.Invoke(Selected);
            return true;
        }
    }
}
