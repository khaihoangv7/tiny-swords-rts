using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public interface IUnitStationHost
    {
        Transform StationTransform { get; }
        void CancelPending(UnitController unit);
        void OnUnitEnteringInternal(UnitController unit);
    }
}
