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

        public void Configure(int radius) => cellRadius = Mathf.Max(1, radius);

        public bool TryReserve(UnitController worker, GridManager grid, PathfindingSystem paths,
            out Vector2Int destination)
        {
            destination = default;
            if (worker == null || grid == null || paths == null) return false;
            GetComponent<SheepPresentation>()?.StopForHarvest();
            if (reservations.TryGetValue(worker, out destination)) return true;

            Vector2Int center = grid.WorldToGrid(transform.position);
            Vector2Int start = grid.WorldToGrid(worker.transform.position);
            GridCell targetCell = grid.GetCell(center.x, center.y);
            if (targetCell == null) return false;
            float best = float.PositiveInfinity;
            int bestDiagonal = 2;
            bool found = false;
            for (int y = -cellRadius; y <= cellRadius; y++)
            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != cellRadius) continue;
                Vector2Int cell = center + new Vector2Int(x, y);
                GridCell candidate = grid.GetCell(cell.x, cell.y);
                if (candidate == null || !candidate.Walkable || candidate.Occupied
                    || candidate.HeightLevel != targetCell.HeightLevel) continue;
                bool reserved = false;
                foreach (Vector2Int used in reservations.Values)
                    if (used == cell) { reserved = true; break; }
                if (reserved || paths.FindPath(start, cell).Count == 0) continue;
                float distance = ((Vector2)grid.GridToWorld(cell.x, cell.y)
                    - (Vector2)worker.transform.position).sqrMagnitude;
                // Cardinal slots put the worker closer to the resource visual;
                // keep diagonal slots as a reachable fallback.
                int diagonal = x != 0 && y != 0 ? 1 : 0;
                if (diagonal > bestDiagonal || (diagonal == bestDiagonal && distance >= best))
                    continue;
                bestDiagonal = diagonal;
                best = distance;
                destination = cell;
                found = true;
            }
            if (found) reservations.Add(worker, destination);
            return found;
        }

        public void Release(UnitController worker)
        {
            if (!ReferenceEquals(worker, null)) reservations.Remove(worker);
        }

        public void ReleaseAll() => reservations.Clear();
    }
}
