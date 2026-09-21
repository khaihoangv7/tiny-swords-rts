using System.Collections.Generic;
using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Units
{
    // Local transform steering only. Units never reserve navigation grid cells.
    public static class UnitSeparation
    {
        public readonly struct Neighbor
        {
            public readonly Vector2 Position;
            public readonly float Radius;
            public readonly int Id;

            public Neighbor(Vector2 position, float radius, int id)
            {
                Position = position;
                Radius = radius;
                Id = id;
            }
        }

        private static readonly List<Neighbor> Nearby = new();

        public static Vector3 Apply(UnitMovement self, Vector3 desired, float deltaTime,
            GridManager grid)
        {
            if (self == null || grid == null || deltaTime <= 0f) return desired;
            Nearby.Clear();
            foreach (UnitSelection selection in UnitSelection.ActiveUnits)
            {
                if (selection == null || !selection.Controller.IsAlive || selection.gameObject == self.gameObject)
                    continue;
                UnitMovement other = selection.Movement;
                Nearby.Add(new Neighbor(other.transform.position, other.SeparationRadius,
                    other.GetHashCode()));
            }
            return Resolve(desired, self.SeparationRadius, self.SeparationSpeed, deltaTime,
                self.GetHashCode(), Nearby, grid);
        }

        public static Vector3 Resolve(Vector3 desired, float radius, float speed, float deltaTime,
            int id, IReadOnlyList<Neighbor> neighbors, GridManager grid)
        {
            if (grid == null || neighbors == null || deltaTime <= 0f) return desired;
            Vector2 push = Vector2.zero;
            bool overlapping = false;
            foreach (Neighbor other in neighbors)
            {
                if (other.Id == id) continue;
                float required = radius + other.Radius;
                Vector2 delta = (Vector2)desired - other.Position;
                float distance = delta.magnitude;
                if (distance >= required) continue;
                overlapping = true;
                Vector2 direction = distance > 0.0001f ? delta / distance
                    : CoincidentDirection(id, other.Id);
                push += direction * (required - distance);
            }
            if (!overlapping) return desired;
            if (push.sqrMagnitude < 0.000001f)
                push = CoincidentDirection(id, 0);
            Vector2 displacement = Vector2.ClampMagnitude(push,
                speed * Mathf.Max(0f, deltaTime));
            Vector3 candidate = desired + new Vector3(displacement.x, displacement.y, 0f);
            if (IsSafeSegment(grid, desired, candidate)) return candidate;

            // Sliding along an edge is safer than pushing through a blocked tile.
            Vector3 slideX = desired + new Vector3(displacement.x, 0f, 0f);
            Vector3 slideY = desired + new Vector3(0f, displacement.y, 0f);
            bool validX = IsSafeSegment(grid, desired, slideX);
            bool validY = IsSafeSegment(grid, desired, slideY);
            if (validX && validY)
                return Mathf.Abs(displacement.x) >= Mathf.Abs(displacement.y) ? slideX : slideY;
            if (validX) return slideX;
            if (validY) return slideY;
            return desired;
        }

        public static bool IsSafeSegment(GridManager grid, Vector3 from, Vector3 to)
        {
            if (grid == null) return false;
            GridCell previous = CellAt(grid, from);
            if (previous == null) return false;
            float cellSize = grid.NavigationData.CellSize;
            int segments = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / (cellSize * 0.2f)));
            for (int i = 1; i <= segments; i++)
            {
                Vector3 sample = Vector3.Lerp(from, to, i / (float)segments);
                GridCell next = CellAt(grid, sample);
                if (next == null || (next != previous && !AStarPathfinder.CanTraverseStep(grid, previous, next)))
                    return false;
                previous = next;
            }
            return true;
        }

        private static GridCell CellAt(GridManager grid, Vector3 point)
        {
            Vector2Int location = grid.WorldToGrid(point);
            GridCell cell = grid.GetCell(location.x, location.y);
            return cell != null && cell.Walkable && !cell.Occupied ? cell : null;
        }

        private static Vector2 CoincidentDirection(int first, int second)
        {
            uint a = (uint)Mathf.Min(first, second);
            uint b = (uint)Mathf.Max(first, second);
            uint hash = (a * 73856093u) ^ (b * 19349663u);
            float angle = (hash & 1023u) * (2f * Mathf.PI / 1024f);
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            return first < second ? direction : -direction;
        }
    }
}
