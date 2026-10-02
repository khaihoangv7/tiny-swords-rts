using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Input
{
    public static class DestinationDistributor
    {
        public static List<GridCell> FindNearbyDestinations(IGridCellProvider grid, Vector2Int center,
            int requestedCount, int maximumRadius = 8,
            bool searchAroundOccupiedCenter = false)
        {
            var result = new List<GridCell>();
            if (grid == null || requestedCount <= 0 || !grid.IsInsideGrid(center.x, center.y))
                return result;

            GridCell centerCell = grid.GetCell(center.x, center.y);
            if (centerCell == null || !centerCell.Walkable
                || (centerCell.Occupied && !searchAroundOccupiedCenter))
                return result;

            if (!centerCell.Occupied)
                AddIfValid(grid, center.x, center.y, centerCell.HeightLevel, result,
                    requestedCount);
            for (int radius = 1; radius <= maximumRadius && result.Count < requestedCount; radius++)
            {
                for (int y = -radius; y <= radius && result.Count < requestedCount; y++)
                for (int x = -radius; x <= radius && result.Count < requestedCount; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                        continue;
                    AddIfValid(grid, center.x + x, center.y + y, centerCell.HeightLevel,
                        result, requestedCount);
                }
            }

            return result;
        }

        private static void AddIfValid(IGridCellProvider grid, int x, int y, int requiredHeight,
            List<GridCell> result, int requestedCount)
        {
            if (result.Count >= requestedCount || !grid.IsInsideGrid(x, y))
                return;

            GridCell cell = grid.GetCell(x, y);
            if (cell != null && cell.Walkable && !cell.Occupied && cell.HeightLevel == requiredHeight)
                result.Add(cell);
        }
    }

    public static class FormationRolePriority
    {
        public const int Warrior = 0;
        public const int Lancer = 1;
        public const int Archer = 2;
        public const int Monk = 3;
        public const int Pawn = 4;
        public const int SafeDefault = Monk;

        public static int For(UnitType type)
        {
            return type switch
            {
                UnitType.Warrior => Warrior,
                UnitType.Lancer => Lancer,
                UnitType.Archer => Archer,
                UnitType.Monk => Monk,
                UnitType.Pawn => Pawn,
                _ => SafeDefault
            };
        }
    }

    public readonly struct FormationAssignment
    {
        public FormationAssignment(UnitSelection unit, GridCell cell, int rolePriority,
            int slotIndex, Vector3 desiredPosition)
        {
            Unit = unit;
            Cell = cell;
            RolePriority = rolePriority;
            SlotIndex = slotIndex;
            DesiredPosition = desiredPosition;
        }

        public UnitSelection Unit { get; }
        public GridCell Cell { get; }
        public int RolePriority { get; }
        public int SlotIndex { get; }
        public Vector3 DesiredPosition { get; }
    }

    public static class RoleFormationPlanner
    {
        private readonly struct Member
        {
            public Member(UnitSelection unit)
            {
                Unit = unit;
                UnitType type = unit != null
                    ? unit.GetComponent<HealthComponent>()?.CombatData?.UnitType
                        ?? UnitType.GoblinTorch
                    : UnitType.GoblinTorch;
                Priority = FormationRolePriority.For(type);
            }

            public UnitSelection Unit { get; }
            public int Priority { get; }
        }

        private readonly struct DesiredSlot
        {
            public DesiredSlot(int priority, int index, Vector3 position)
            {
                Priority = priority;
                Index = index;
                Position = position;
            }

            public int Priority { get; }
            public int Index { get; }
            public Vector3 Position { get; }
        }

        private readonly struct ResolvedSlot
        {
            public ResolvedSlot(DesiredSlot desired, GridCell cell)
            {
                Desired = desired;
                Cell = cell;
            }

            public DesiredSlot Desired { get; }
            public GridCell Cell { get; }
        }

        public const float DefaultSpacing = 1f;
        public const float MinimumSpacing = 0.5f;
        public const float DirectionEpsilon = 0.05f;

        public static bool RequiresFormation(int unitCount) => unitCount > 1;

        public static int ColumnsFor(int unitCount)
            => Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, unitCount))));

        public static Vector3 CalculateGroupOrigin(IReadOnlyList<UnitSelection> units)
        {
            if (units == null || units.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] == null) continue;
                sum += units[i].transform.position;
                count++;
            }
            return count > 0 ? sum / count : Vector3.zero;
        }

        public static Vector2 ResolveForward(Vector3 groupOrigin, Vector3 destination)
        {
            Vector2 direction = (Vector2)(destination - groupOrigin);
            return direction.sqrMagnitude >= DirectionEpsilon * DirectionEpsilon
                ? direction.normalized : Vector2.right;
        }

        public static bool TryPlan(GridManager grid, IReadOnlyList<UnitSelection> units,
            Vector3 destination, float spacing, int candidateMultiplier,
            int maximumSearchRadius, bool searchAroundOccupiedCenter,
            List<FormationAssignment> output)
        {
            output?.Clear();
            if (grid == null || units == null || output == null || units.Count < 2)
                return false;

            var members = new List<Member>(units.Count);
            for (int i = 0; i < units.Count; i++)
                if (units[i] != null) members.Add(new Member(units[i]));
            if (members.Count < 2) return false;
            members.Sort(CompareMembers);

            Vector3 origin = CalculateGroupOrigin(units);
            Vector2 forward = ResolveForward(origin, destination);
            Vector2 right = new(forward.y, -forward.x);
            float slotSpacing = Mathf.Max(MinimumSpacing, spacing);
            int columns = ColumnsFor(members.Count);
            List<DesiredSlot> desired = GenerateDesiredSlots(members, destination,
                forward, right, columns, slotSpacing);

            Vector2Int center = grid.WorldToGrid(destination);
            int multiplier = Mathf.Max(1, candidateMultiplier);
            int searchRadius = RequiredSearchRadius(grid, desired, destination,
                maximumSearchRadius);
            int diameter = searchRadius * 2 + 1;
            int fullNeighborhoodCount = diameter * diameter;
            List<GridCell> candidates = DestinationDistributor.FindNearbyDestinations(
                grid, center, Mathf.Max(members.Count * multiplier, fullNeighborhoodCount),
                searchRadius, searchAroundOccupiedCenter);
            if (candidates.Count == 0) return false;

            List<ResolvedSlot> resolved = ResolveSlots(desired, candidates);
            if (resolved.Count == 0) return false;
            AssignMembers(members, resolved, output);
            return output.Count > 0;
        }

        private static int RequiredSearchRadius(GridManager grid,
            List<DesiredSlot> desired, Vector3 destination, int maximumSearchRadius)
        {
            float farthest = 0f;
            for (int i = 0; i < desired.Count; i++)
                farthest = Mathf.Max(farthest,
                    Vector2.Distance(destination, desired[i].Position));
            float cellSize = grid.NavigationData != null
                ? Mathf.Max(0.01f, grid.NavigationData.CellSize) : 1f;
            int required = Mathf.CeilToInt(farthest / cellSize) + 1;
            return Mathf.Clamp(required, 1, Mathf.Max(1, maximumSearchRadius));
        }

        private static List<DesiredSlot> GenerateDesiredSlots(List<Member> members,
            Vector3 destination, Vector2 forward, Vector2 right, int columns, float spacing)
        {
            var result = new List<DesiredSlot>(members.Count);
            int memberIndex = 0;
            int row = 0;
            int slotIndex = 0;
            while (memberIndex < members.Count)
            {
                int priority = members[memberIndex].Priority;
                int roleEnd = memberIndex + 1;
                while (roleEnd < members.Count && members[roleEnd].Priority == priority)
                    roleEnd++;

                int remaining = roleEnd - memberIndex;
                while (remaining > 0)
                {
                    int rowCount = Mathf.Min(columns, remaining);
                    float lateralCenter = (rowCount - 1) * 0.5f;
                    for (int column = 0; column < rowCount; column++)
                    {
                        float lateral = (column - lateralCenter) * spacing;
                        Vector3 position = destination
                            - (Vector3)(forward * (row * spacing))
                            + (Vector3)(right * lateral);
                        result.Add(new DesiredSlot(priority, slotIndex++, position));
                    }
                    remaining -= rowCount;
                    row++;
                }
                memberIndex = roleEnd;
            }
            return result;
        }

        private static List<ResolvedSlot> ResolveSlots(List<DesiredSlot> desired,
            List<GridCell> candidates)
        {
            var result = new List<ResolvedSlot>(Mathf.Min(desired.Count, candidates.Count));
            var used = new bool[candidates.Count];
            for (int i = 0; i < desired.Count; i++)
            {
                int best = -1;
                float bestDistance = float.PositiveInfinity;
                for (int candidate = 0; candidate < candidates.Count; candidate++)
                {
                    if (used[candidate]) continue;
                    float distance = ((Vector2)(candidates[candidate].WorldPosition
                        - desired[i].Position)).sqrMagnitude;
                    if (distance < bestDistance - 0.0001f
                        || Mathf.Abs(distance - bestDistance) <= 0.0001f
                        && IsEarlierCell(candidates[candidate], best >= 0
                            ? candidates[best] : null))
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }
                if (best < 0) break;
                used[best] = true;
                result.Add(new ResolvedSlot(desired[i], candidates[best]));
            }
            return result;
        }

        private static void AssignMembers(List<Member> members, List<ResolvedSlot> slots,
            List<FormationAssignment> output)
        {
            int memberStart = 0;
            int slotStart = 0;
            while (memberStart < members.Count && slotStart < slots.Count)
            {
                int priority = members[memberStart].Priority;
                int memberEnd = memberStart + 1;
                while (memberEnd < members.Count && members[memberEnd].Priority == priority)
                    memberEnd++;
                int slotEnd = slotStart;
                while (slotEnd < slots.Count && slots[slotEnd].Desired.Priority == priority)
                    slotEnd++;
                AssignRole(members, memberStart, memberEnd, slots, slotStart, slotEnd, output);
                memberStart = memberEnd;
                slotStart = slotEnd;
            }
        }

        private static void AssignRole(List<Member> members, int memberStart, int memberEnd,
            List<ResolvedSlot> slots, int slotStart, int slotEnd,
            List<FormationAssignment> output)
        {
            int memberCount = memberEnd - memberStart;
            int slotCount = slotEnd - slotStart;
            var memberUsed = new bool[memberCount];
            var slotUsed = new bool[slotCount];
            int pairs = Mathf.Min(memberCount, slotCount);
            for (int pair = 0; pair < pairs; pair++)
            {
                int bestMember = -1;
                int bestSlot = -1;
                float bestDistance = float.PositiveInfinity;
                for (int member = 0; member < memberCount; member++)
                {
                    if (memberUsed[member]) continue;
                    UnitSelection unit = members[memberStart + member].Unit;
                    for (int slot = 0; slot < slotCount; slot++)
                    {
                        if (slotUsed[slot]) continue;
                        ResolvedSlot candidate = slots[slotStart + slot];
                        float distance = ((Vector2)(unit.transform.position
                            - candidate.Cell.WorldPosition)).sqrMagnitude;
                        if (distance < bestDistance - 0.0001f
                            || Mathf.Abs(distance - bestDistance) <= 0.0001f
                            && IsEarlierPair(unit, candidate, bestMember >= 0
                                ? members[memberStart + bestMember].Unit : null,
                                bestSlot >= 0 ? slots[slotStart + bestSlot] : default))
                        {
                            bestMember = member;
                            bestSlot = slot;
                            bestDistance = distance;
                        }
                    }
                }
                if (bestMember < 0 || bestSlot < 0) break;
                memberUsed[bestMember] = true;
                slotUsed[bestSlot] = true;
                Member assigned = members[memberStart + bestMember];
                ResolvedSlot destination = slots[slotStart + bestSlot];
                output.Add(new FormationAssignment(assigned.Unit, destination.Cell,
                    assigned.Priority, destination.Desired.Index,
                    destination.Desired.Position));
            }
        }

        private static int CompareMembers(Member first, Member second)
        {
            int priority = first.Priority.CompareTo(second.Priority);
            return priority != 0 ? priority
                : first.Unit.Controller.GetEntityId().CompareTo(
                    second.Unit.Controller.GetEntityId());
        }

        private static bool IsEarlierPair(UnitSelection unit, ResolvedSlot slot,
            UnitSelection currentUnit, ResolvedSlot currentSlot)
        {
            if (currentUnit == null) return true;
            int entity = unit.Controller.GetEntityId().CompareTo(
                currentUnit.Controller.GetEntityId());
            return entity < 0 || entity == 0
                && slot.Desired.Index < currentSlot.Desired.Index;
        }

        private static bool IsEarlierCell(GridCell candidate, GridCell current)
        {
            if (current == null) return true;
            int y = candidate.GridY.CompareTo(current.GridY);
            return y < 0 || y == 0 && candidate.GridX < current.GridX;
        }
    }
}
