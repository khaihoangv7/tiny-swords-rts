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
            Vector2Int size = site.Data.Footprint;
            int slotCount = size.x * 2 + size.y * 2;
            ulong tried = 0UL;
            for (int attempt = 0; attempt < slotCount && attempt < 64; attempt++)
            {
                int bestIndex = -1;
                Vector2Int bestCell = default;
                float bestDistance = float.PositiveInfinity;
                int index = 0;
                for (int y = -1; y <= size.y; y++)
                for (int x = -1; x <= size.x; x++)
                {
                    bool edge = (x == -1 || x == size.x) && y >= 0 && y < size.y
                        || (y == -1 || y == size.y) && x >= 0 && x < size.x;
                    if (!edge) continue;
                    int candidateIndex = index++;
                    if (candidateIndex >= 64) continue;
                    if ((tried & (1UL << candidateIndex)) != 0) continue;
                    Vector2Int candidate = site.Origin + new Vector2Int(x, y);
                    if (reservations.ContainsValue(candidate)) continue;
                    GridCell cell = grid.GetCell(candidate.x, candidate.y);
                    if (cell == null || !cell.Walkable || cell.Occupied
                        || cell.HeightLevel != footprintCell.HeightLevel) continue;
                    float distance = ((Vector2)(cell.WorldPosition
                        - builder.transform.position)).sqrMagnitude;
                    if (bestIndex >= 0 && distance >= bestDistance) continue;
                    bestIndex = candidateIndex;
                    bestCell = candidate;
                    bestDistance = distance;
                }
                if (bestIndex < 0) return false;
                tried |= 1UL << bestIndex;
                if (paths.FindPath(start, bestCell).Count == 0) continue;
                reservations.Add(builder, bestCell);
                position = grid.GridToWorld(bestCell.x, bestCell.y);
                return true;
            }
            return false;
        }

        public void Release(PawnBuilder builder)
        {
            if (builder != null) reservations.Remove(builder);
        }

        public void ReleaseAll() => reservations.Clear();
    }
}
