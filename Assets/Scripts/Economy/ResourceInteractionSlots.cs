using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Economy
{
    // Per-target reservations keep workers on separate reachable cells.
    public sealed class ResourceInteractionSlots : MonoBehaviour
    {
        [SerializeField, Min(1)] private int cellRadius = 1;
        private readonly Dictionary<UnitController, Vector2Int> reservations = new();
        public int ReservationCount => reservations.Count;

        public static bool IsValidGatherCell(GridCell targetCell, GridCell candidate)
            => targetCell != null && TerrainRules.IsStableStandingCell(candidate)
                && candidate.HeightLevel == targetCell.HeightLevel;

        public bool HasAvailableCapacity(GridManager grid)
        {
            if (grid == null) return false;
            Vector2Int center = grid.WorldToGrid(transform.position);
            GridCell targetCell = grid.GetCell(center.x, center.y);
            if (targetCell == null) return false;
            for (int y = -cellRadius; y <= cellRadius; y++)
            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != cellRadius) continue;
                Vector2Int candidate = center + new Vector2Int(x, y);
                GridCell cell = grid.GetCell(candidate.x, candidate.y);
                if (!IsValidGatherCell(targetCell, cell)
                    || reservations.ContainsValue(candidate)) continue;
                return true;
            }
            return false;
        }

        public void Configure(int radius) => cellRadius = Mathf.Max(1, radius);

        public bool TryReserve(UnitController worker, GridManager grid, PathfindingSystem paths,
            out Vector2Int destination)
            => TryReserve(worker, grid, paths, null, out destination);

        public bool TryReserve(UnitController worker, GridManager grid, PathfindingSystem paths,
            IPathTraversalPolicy traversalPolicy, out Vector2Int destination)
        {
            destination = default;
            if (worker == null || grid == null || paths == null) return false;
            GetComponent<SheepPresentation>()?.StopForHarvest();
            if (reservations.TryGetValue(worker, out destination)) return true;

            Vector2Int center = grid.WorldToGrid(transform.position);
            Vector2Int start = grid.WorldToGrid(worker.transform.position);
            GridCell targetCell = grid.GetCell(center.x, center.y);
            if (targetCell == null) return false;
            int diameter = cellRadius * 2 + 1;
            int slotCount = diameter * diameter - Mathf.Max(0, diameter - 2)
                * Mathf.Max(0, diameter - 2);
            ulong tried = 0UL;
            HashSet<GridCell> reachable = null;
            for (int attempt = 0; attempt < slotCount && attempt < 64; attempt++)
            {
                int bestIndex = -1;
                int bestDiagonal = 2;
                float bestDistance = float.PositiveInfinity;
                Vector2Int bestCell = default;
                int index = 0;
                for (int y = -cellRadius; y <= cellRadius; y++)
                for (int x = -cellRadius; x <= cellRadius; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != cellRadius) continue;
                    int candidateIndex = index++;
                    if (candidateIndex >= 64) continue;
                    if ((tried & (1UL << candidateIndex)) != 0) continue;
                    Vector2Int cell = center + new Vector2Int(x, y);
                    GridCell candidate = grid.GetCell(cell.x, cell.y);
                    if (!IsValidGatherCell(targetCell, candidate)) continue;
                    bool reserved = false;
                    foreach (Vector2Int used in reservations.Values)
                        if (used == cell) { reserved = true; break; }
                    if (reserved) continue;
                    float distance = ((Vector2)candidate.WorldPosition
                        - (Vector2)worker.transform.position).sqrMagnitude;
                    // Cardinal slots remain preferred over diagonal slots.
                    int diagonal = x != 0 && y != 0 ? 1 : 0;
                    if (bestIndex >= 0 && (diagonal > bestDiagonal
                        || (diagonal == bestDiagonal && distance >= bestDistance))) continue;
                    bestIndex = candidateIndex;
                    bestDiagonal = diagonal;
                    bestDistance = distance;
                    bestCell = cell;
                }
                if (bestIndex < 0) return false;
                tried |= 1UL << bestIndex;
                GridCell selected = grid.GetCell(bestCell.x, bestCell.y);
                if (reachable != null)
                {
                    if (!reachable.Contains(selected)) continue;
                }
                else if (paths.FindPath(start, bestCell, traversalPolicy,
                    out HashSet<GridCell> exhausted).Count == 0)
                {
                    // On a failed full search, every legal cell in this connected
                    // region is known. Other slots need only a membership check.
                    reachable = exhausted;
                    continue;
                }
                destination = bestCell;
                reservations.Add(worker, destination);
                return true;
            }
            return false;
        }

        public void Release(UnitController worker)
        {
            if (!ReferenceEquals(worker, null)) reservations.Remove(worker);
        }

        public void ReleaseAll() => reservations.Clear();
    }
}
