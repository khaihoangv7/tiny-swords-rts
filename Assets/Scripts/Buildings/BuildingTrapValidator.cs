using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    // Tests only the topology a prospective footprint can change. A component is
    // considered locally open when it can traverse beyond the bounded analysis
    // region. Existing sealed areas are ignored: the pre-placement component must
    // have been open for the candidate to be rejected.
    public sealed class BuildingTrapValidator
    {
        public const int ConnectivityMargin = 6;

        private static readonly ProfilerMarker ValidationMarker =
            new("TinyKingdom.Buildings.TrapValidation");
        private static readonly Vector2Int[] Neighbours =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up,
            new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)
        };

        private int[] componentByCell = Array.Empty<int>();
        private bool[] componentEscapes = Array.Empty<bool>();
        private bool[] componentSealed = Array.Empty<bool>();
        private int[] componentSeed = Array.Empty<int>();
        private int[] componentCellCount = Array.Empty<int>();
        private int[] visitStampByCell = Array.Empty<int>();
        private int[] queue = Array.Empty<int>();
        private int visitStamp;

        public int LastTrappedUnitCount { get; private set; }
        public int LastSealedCellCount { get; private set; }

        public void Configure(GridManager grid)
        {
            if (grid != null && grid.Width > 0 && grid.Height > 0)
                EnsureBuffers(grid.Width * grid.Height);
        }

        public bool WouldTrapFriendlyUnits(GridManager grid, Faction faction,
            Vector2Int footprintOrigin, Vector2Int footprintSize,
            out int trappedUnitCount)
            => WouldTrapFriendlyUnits(grid, faction, footprintOrigin, footprintSize,
                out trappedUnitCount, out _);

        // Empty pockets matter too: a producer can later choose one as its first
        // spawn cell even though no friendly unit stood there during placement.
        public bool WouldTrapFriendlyUnits(GridManager grid, Faction faction,
            Vector2Int footprintOrigin, Vector2Int footprintSize,
            out int trappedUnitCount, out int sealedCellCount)
        {
            using var marker = ValidationMarker.Auto();
            trappedUnitCount = 0;
            sealedCellCount = 0;
            LastTrappedUnitCount = 0;
            LastSealedCellCount = 0;
            if (grid == null || grid.Width <= 0 || grid.Height <= 0) return false;

            EnsureBuffers(grid.Width * grid.Height);
            Array.Clear(componentByCell, 0, componentByCell.Length);
            Array.Clear(componentEscapes, 0, componentEscapes.Length);
            Array.Clear(componentSealed, 0, componentSealed.Length);
            Array.Clear(componentCellCount, 0, componentCellCount.Length);

            int minX = Mathf.Max(0, footprintOrigin.x - ConnectivityMargin);
            int minY = Mathf.Max(0, footprintOrigin.y - ConnectivityMargin);
            int maxX = Mathf.Min(grid.Width - 1,
                footprintOrigin.x + footprintSize.x - 1 + ConnectivityMargin);
            int maxY = Mathf.Min(grid.Height - 1,
                footprintOrigin.y + footprintSize.y - 1 + ConnectivityMargin);

            int componentCount = LabelPostPlacementComponents(grid, footprintOrigin,
                footprintSize, minX, minY, maxX, maxY);
            for (int component = 1; component <= componentCount; component++)
            {
                if (componentEscapes[component]) continue;
                int seed = componentSeed[component];
                Vector2Int start = new(seed % grid.Width, seed / grid.Width);
                // Existing sealed terrain stays legal. Reject only a pocket whose
                // route to surrounding space is closed by this new footprint.
                if (!CanReachSurroundingSpace(grid, start, footprintOrigin,
                        footprintSize, minX, minY, maxX, maxY, false)) continue;
                componentSealed[component] = true;
                sealedCellCount += componentCellCount[component];
            }
            IReadOnlyList<UnitController> units = UnitController.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit == null || !unit.IsAlive || !unit.IsWorldParticipant
                    || unit.Faction != faction) continue;
                Vector2Int cell = grid.WorldToGrid(unit.transform.position);
                if (cell.x < minX || cell.x > maxX || cell.y < minY || cell.y > maxY
                    || Contains(footprintOrigin, footprintSize, cell)) continue;
                int component = componentByCell[ToIndex(cell.x, cell.y, grid.Width)];
                if (component <= 0 || component > componentCount
                    || !componentSealed[component]) continue;
                trappedUnitCount++;
            }
            LastTrappedUnitCount = trappedUnitCount;
            LastSealedCellCount = sealedCellCount;
            return trappedUnitCount > 0;
        }

        private int LabelPostPlacementComponents(GridManager grid, Vector2Int origin,
            Vector2Int size, int minX, int minY, int maxX, int maxY)
        {
            int component = 0;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int index = ToIndex(x, y, grid.Width);
                if (componentByCell[index] != 0
                    || !IsOpen(grid, x, y, origin, size, true)) continue;
                component++;
                componentSeed[component] = index;
                componentEscapes[component] = FloodComponent(grid, x, y, origin, size,
                    minX, minY, maxX, maxY, component);
            }
            return component;
        }

        private bool FloodComponent(GridManager grid, int startX, int startY,
            Vector2Int origin, Vector2Int size, int minX, int minY, int maxX,
            int maxY, int component)
        {
            int head = 0;
            int tail = 0;
            int start = ToIndex(startX, startY, grid.Width);
            queue[tail++] = start;
            componentByCell[start] = component;
            bool escapes = false;
            while (head < tail)
            {
                int currentIndex = queue[head++];
                componentCellCount[component]++;
                int x = currentIndex % grid.Width;
                int y = currentIndex / grid.Width;
                GridCell current = grid.GetCell(x, y);
                for (int i = 0; i < Neighbours.Length; i++)
                {
                    int nx = x + Neighbours[i].x;
                    int ny = y + Neighbours[i].y;
                    // The map edge is a wall, not an exit from a spawn pocket.
                    if (!grid.IsInsideGrid(nx, ny)) continue;
                    if (!CanTraverse(grid, current, nx, ny, origin, size, true)) continue;
                    if (nx < minX || nx > maxX || ny < minY || ny > maxY)
                    {
                        escapes = true;
                        continue;
                    }
                    int nextIndex = ToIndex(nx, ny, grid.Width);
                    if (componentByCell[nextIndex] != 0) continue;
                    componentByCell[nextIndex] = component;
                    queue[tail++] = nextIndex;
                }
            }
            return escapes;
        }

        private bool CanReachSurroundingSpace(GridManager grid, Vector2Int start,
            Vector2Int origin, Vector2Int size, int minX, int minY, int maxX,
            int maxY, bool blockCandidate)
        {
            if (!IsOpen(grid, start.x, start.y, origin, size, blockCandidate)) return false;
            AdvanceVisitStamp();
            int head = 0;
            int tail = 0;
            int startIndex = ToIndex(start.x, start.y, grid.Width);
            visitStampByCell[startIndex] = visitStamp;
            queue[tail++] = startIndex;
            while (head < tail)
            {
                int currentIndex = queue[head++];
                int x = currentIndex % grid.Width;
                int y = currentIndex / grid.Width;
                GridCell current = grid.GetCell(x, y);
                for (int i = 0; i < Neighbours.Length; i++)
                {
                    int nx = x + Neighbours[i].x;
                    int ny = y + Neighbours[i].y;
                    if (!grid.IsInsideGrid(nx, ny)) continue;
                    if (!CanTraverse(grid, current, nx, ny, origin, size,
                            blockCandidate)) continue;
                    if (nx < minX || nx > maxX || ny < minY || ny > maxY) return true;
                    int nextIndex = ToIndex(nx, ny, grid.Width);
                    if (visitStampByCell[nextIndex] == visitStamp) continue;
                    visitStampByCell[nextIndex] = visitStamp;
                    queue[tail++] = nextIndex;
                }
            }
            return false;
        }

        private static bool CanTraverse(GridManager grid, GridCell current,
            int nextX, int nextY, Vector2Int origin, Vector2Int size,
            bool blockCandidate)
        {
            if (!IsOpen(grid, nextX, nextY, origin, size, blockCandidate)) return false;
            GridCell next = grid.GetCell(nextX, nextY);
            if (current == null || !current.CanTraverseTo(next)) return false;
            int dx = nextX - current.GridX;
            int dy = nextY - current.GridY;
            if (dx == 0 || dy == 0) return true;

            // Match AStarPathfinder's no-corner-cutting rule. A reciprocal authored
            // height connector is the one legal diagonal exception.
            if (current.HeightLevel != next.HeightLevel
                && current.HeightConnection == new Vector2Int(nextX, nextY)
                && next.HeightConnection == new Vector2Int(current.GridX, current.GridY))
                return true;
            int sideX1 = current.GridX + dx;
            int sideY1 = current.GridY;
            int sideX2 = current.GridX;
            int sideY2 = current.GridY + dy;
            if (!IsOpen(grid, sideX1, sideY1, origin, size, blockCandidate)
                || !IsOpen(grid, sideX2, sideY2, origin, size, blockCandidate)) return false;
            GridCell sideX = grid.GetCell(sideX1, sideY1);
            GridCell sideY = grid.GetCell(sideX2, sideY2);
            return current.CanTraverseTo(sideX) && sideX.CanTraverseTo(next)
                && current.CanTraverseTo(sideY) && sideY.CanTraverseTo(next);
        }

        private static bool IsOpen(GridManager grid, int x, int y,
            Vector2Int origin, Vector2Int size, bool blockCandidate)
        {
            if (blockCandidate && Contains(origin, size, new Vector2Int(x, y))) return false;
            GridCell cell = grid.GetCell(x, y);
            return cell != null && cell.Walkable && !cell.Occupied;
        }

        private void EnsureBuffers(int cellCount)
        {
            if (componentByCell.Length == cellCount) return;
            componentByCell = new int[cellCount];
            componentEscapes = new bool[cellCount + 1];
            componentSealed = new bool[cellCount + 1];
            componentSeed = new int[cellCount + 1];
            componentCellCount = new int[cellCount + 1];
            visitStampByCell = new int[cellCount];
            queue = new int[cellCount];
            visitStamp = 0;
        }

        private void AdvanceVisitStamp()
        {
            if (visitStamp == int.MaxValue)
            {
                Array.Clear(visitStampByCell, 0, visitStampByCell.Length);
                visitStamp = 1;
            }
            else visitStamp++;
        }

        private static int ToIndex(int x, int y, int width) => y * width + x;

        private static bool Contains(Vector2Int origin, Vector2Int size,
            Vector2Int cell)
            => cell.x >= origin.x && cell.x < origin.x + size.x
                && cell.y >= origin.y && cell.y < origin.y + size.y;
    }
}
