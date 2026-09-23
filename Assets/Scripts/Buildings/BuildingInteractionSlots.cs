using System.Collections.Generic;
using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    // Per-building reservations keep concurrent builders on distinct reachable cells.
    public sealed class BuildingInteractionSlots : MonoBehaviour
    {
        private readonly Dictionary<PawnBuilder, Vector2Int> reservations = new();
        private BuildingSite site;
        private GridManager grid;
        private PathfindingSystem paths;

        public int ReservationCount => reservations.Count;

        public void Configure(BuildingSite owner, GridManager navigation)
        {
            site = owner;
            grid = navigation;
            paths = grid != null ? grid.GetComponent<PathfindingSystem>() : null;
        }

        public bool TryReserve(PawnBuilder builder, out Vector3 position)
        {
            position = default;
            if (builder == null || site == null || grid == null || paths == null)
                return false;
            if (reservations.TryGetValue(builder, out Vector2Int existing))
            {
                position = grid.GridToWorld(existing.x, existing.y);
                return true;
            }

            Vector2Int start = grid.WorldToGrid(builder.transform.position);
            GridCell footprintCell = grid.GetCell(site.Origin.x, site.Origin.y);
            if (footprintCell == null) return false;
            Vector2Int bestCell = default;
            float bestDistance = float.PositiveInfinity;
            bool found = false;
            Vector2Int size = site.Data.Footprint;
            for (int y = -1; y <= size.y; y++)
            for (int x = -1; x <= size.x; x++)
            {
                bool edge = (x == -1 || x == size.x) && y >= 0 && y < size.y
                    || (y == -1 || y == size.y) && x >= 0 && x < size.x;
                if (!edge) continue;
                Vector2Int candidate = site.Origin + new Vector2Int(x, y);
                if (reservations.ContainsValue(candidate)) continue;
                GridCell cell = grid.GetCell(candidate.x, candidate.y);
                if (cell == null || !cell.Walkable || cell.Occupied
                    || cell.HeightLevel != footprintCell.HeightLevel
                    || paths.FindPath(start, candidate).Count == 0) continue;
                float distance = ((Vector2)(cell.WorldPosition
                    - builder.transform.position)).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestCell = candidate;
                found = true;
            }
            if (!found) return false;
            reservations.Add(builder, bestCell);
            position = grid.GridToWorld(bestCell.x, bestCell.y);
            return true;
        }

        public void Release(PawnBuilder builder)
        {
            if (builder != null) reservations.Remove(builder);
        }

        public void ReleaseAll() => reservations.Clear();
    }
}
