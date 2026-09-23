using System.Collections.Generic;
using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Units
{
    // Reservations are angles, so each unit retains its direction as the target moves.
    public static class MeleeEngagementSlots
    {
        public const int SlotCount = 8;
        private const float EngagementFraction = 0.85f;
        private static readonly Dictionary<UnitController, List<Reservation>> ByTarget = new();

        private readonly struct Reservation
        {
            public readonly UnitController Attacker;
            public readonly int Index;

            public Reservation(UnitController attacker, int index)
            {
                Attacker = attacker;
                Index = index;
            }
        }

        public static float Distance(float attackRange) => Mathf.Max(0.01f, attackRange * EngagementFraction);

        public static Vector3 Position(Vector3 targetPosition, int index, float attackRange)
        {
            float angle = index * Mathf.PI * 2f / SlotCount;
            float distance = Distance(attackRange);
            return targetPosition + new Vector3(Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance, 0f);
        }

        public static bool TryGet(UnitController attacker, UnitController target, float attackRange,
            GridManager grid, PathfindingSystem pathfinding, out Vector3 position)
        {
            position = default;
            if (attacker == null || target == null || !ByTarget.TryGetValue(target, out var reservations))
                return false;
            Prune(reservations);
            foreach (Reservation reservation in reservations)
            {
                if (reservation.Attacker != attacker) continue;
                position = Position(target.transform.position, reservation.Index, attackRange);
                return IsUsable(attacker, target, position, grid, pathfinding, false);
            }
            return false;
        }

        public static bool TryReserve(UnitController attacker, UnitController target, float attackRange,
            GridManager grid, PathfindingSystem pathfinding, out Vector3 position)
        {
            if (TryGet(attacker, target, attackRange, grid, pathfinding, out position))
                return true;
            Release(attacker);
            position = default;
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive
                || grid == null || pathfinding == null)
                return false;

            if (!ByTarget.TryGetValue(target, out var reservations))
            {
                reservations = new List<Reservation>();
                ByTarget.Add(target, reservations);
            }
            Prune(reservations);
            int chosen = -1;
            float best = float.PositiveInfinity;
            for (int index = 0; index < SlotCount; index++)
            {
                if (reservations.Exists(reservation => reservation.Index == index)) continue;
                Vector3 candidate = Position(target.transform.position, index, attackRange);
                float cost = ((Vector2)(candidate - attacker.transform.position)).sqrMagnitude;
                if (cost >= best || !IsUsable(attacker, target, candidate, grid, pathfinding, true)) continue;
                best = cost;
                chosen = index;
                position = candidate;
            }
            if (chosen < 0)
            {
                if (reservations.Count == 0) ByTarget.Remove(target);
                return false;
            }
            reservations.Add(new Reservation(attacker, chosen));
            return true;
        }

        public static int SlotIndex(UnitController attacker, UnitController target)
        {
            if (target == null || !ByTarget.TryGetValue(target, out var reservations)) return -1;
            Prune(reservations);
            foreach (Reservation reservation in reservations)
                if (reservation.Attacker == attacker) return reservation.Index;
            return -1;
        }

        public static int ReservationCount(UnitController target)
        {
            if (target == null || !ByTarget.TryGetValue(target, out var reservations)) return 0;
            Prune(reservations);
            return reservations.Count;
        }

        public static void Release(UnitController attacker)
        {
            if (attacker == null) return;
            var empty = new List<UnitController>();
            foreach (var entry in ByTarget)
            {
                entry.Value.RemoveAll(reservation => reservation.Attacker == attacker
                    || reservation.Attacker == null || !reservation.Attacker.IsAlive);
                if (entry.Value.Count == 0) empty.Add(entry.Key);
            }
            foreach (UnitController target in empty) ByTarget.Remove(target);
        }

        public static void ReleaseTarget(UnitController target)
        {
            if (target != null) ByTarget.Remove(target);
        }

        private static void Prune(List<Reservation> reservations)
        {
            reservations.RemoveAll(reservation => reservation.Attacker == null
                || !reservation.Attacker.IsAlive);
        }

        private static bool IsUsable(UnitController attacker, UnitController target,
            Vector3 position, GridManager grid, PathfindingSystem pathfinding, bool requirePath)
        {
            if (grid == null || pathfinding == null || attacker == null || target == null
                || !attacker.IsAlive || !target.IsAlive) return false;
            Vector2Int start = grid.WorldToGrid(attacker.transform.position);
            Vector2Int destination = grid.WorldToGrid(position);
            Vector2Int targetCell = grid.WorldToGrid(target.transform.position);
            GridCell from = grid.GetCell(start.x, start.y);
            GridCell cell = grid.GetCell(destination.x, destination.y);
            GridCell enemy = grid.GetCell(targetCell.x, targetCell.y);
            return from != null && cell != null && enemy != null && cell.Walkable
                && !cell.Occupied && cell.HeightLevel == enemy.HeightLevel
                && UnitSeparation.IsSafeSegment(grid, cell.WorldPosition, position)
                && (!requirePath || pathfinding.FindPath(start, destination).Count > 0);
        }
    }
}
