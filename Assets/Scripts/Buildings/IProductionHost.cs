using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public interface IProductionHost
    {
        BuildingType Type { get; }
        Faction Faction { get; }
        Vector2Int FootprintOrigin { get; }
        Vector2Int Footprint { get; }
        bool IsProductionReady { get; }
    }
}
