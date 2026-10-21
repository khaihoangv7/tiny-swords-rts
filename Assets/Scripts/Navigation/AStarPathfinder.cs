using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Navigation
{
    public interface IPathTraversalPolicy
    {
        bool CanEnter(GridCell cell);
        double AdditionalCost(GridCell from, GridCell to);
    }

    public static class AStarPathfinder
    {
        private readonly struct OpenEntry
        {
            public OpenEntry(GridCell cell, double cost, double heuristic, long order)
            {
                Cell = cell;
                Cost = cost;
                Heuristic = heuristic;
                Score = cost + heuristic;
                Order = order;
            }

            public GridCell Cell { get; }
            public double Cost { get; }
            public double Heuristic { get; }
            public double Score { get; }
            public long Order { get; }
        }

        public static readonly double DiagonalCost = Math.Sqrt(2d);
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left,
            new Vector2Int(1, 1),
            new Vector2Int(-1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, -1)
        };

        public static List<GridCell> FindPath(IGridCellProvider grid, GridCell start, GridCell destination)
            => FindPath(grid, start, destination, null);

        public static List<GridCell> FindPath(IGridCellProvider grid, GridCell start,
            GridCell destination, IPathTraversalPolicy policy)
            => FindPath(grid, start, destination, policy, out _);

        // A failed full search has already visited every reachable cell. Callers
        // probing alternate destinations can reuse that result without repeating A*.
        public static List<GridCell> FindPath(IGridCellProvider grid, GridCell start,
            GridCell destination, IPathTraversalPolicy policy,
            out HashSet<GridCell> exhaustedReachable)
        {
            exhaustedReachable = null;
            var emptyPath = new List<GridCell>();
            if (grid == null || start == null || destination == null)
                return emptyPath;
            if (!start.Walkable || start.Occupied || !destination.Walkable || destination.Occupied)
                return emptyPath;
            // The actor may already be standing in newly discovered danger. Allow
            // the start cell so a strategic retreat can leave it, while still
            // requiring every entered cell and the destination to be legal.
            if (policy != null && !policy.CanEnter(destination))
                return emptyPath;

            var open = new List<OpenEntry>();
            var closed = new HashSet<GridCell>();
            var cameFrom = new Dictionary<GridCell, GridCell>();
            var gScore = new Dictionary<GridCell, double> { [start] = 0d };
            var insertionOrder = new Dictionary<GridCell, long> { [start] = 0L };
            long nextOrder = 0L;
            Push(open, new OpenEntry(start, 0d, Heuristic(start, destination), 0L));

            while (open.Count > 0)
            {
                OpenEntry entry = Pop(open);
                GridCell current = entry.Cell;
                // A shorter route may have queued a replacement for this cell.
                // Keep the same closed-cell rule as the original search.
                if (closed.Contains(current)
                    || !gScore.TryGetValue(current, out double currentCost)
                    || entry.Cost != currentCost)
                    continue;
                if (current == destination)
                    return ReconstructPath(cameFrom, current);

                closed.Add(current);

                foreach (Vector2Int direction in Directions)
                {
                    int nextX = current.GridX + direction.x;
                    int nextY = current.GridY + direction.y;
                    if (!grid.IsInsideGrid(nextX, nextY))
                        continue;

                    GridCell neighbor = grid.GetCell(nextX, nextY);
                    if (closed.Contains(neighbor) || !CanTraverseStep(grid, current, neighbor)
                        || policy != null && !policy.CanEnter(neighbor))
                        continue;
                    if (policy != null && IsDiagonal(current, neighbor)
                        && !IsAuthoredHeightConnection(current, neighbor)
                        && (!policy.CanEnter(grid.GetCell(neighbor.GridX, current.GridY))
                            || !policy.CanEnter(grid.GetCell(current.GridX, neighbor.GridY))))
                        continue;

                    double tentativeScore = currentCost + StepCost(current, neighbor)
                        + (policy?.AdditionalCost(current, neighbor) ?? 0d);
                    if (gScore.TryGetValue(neighbor, out double existingScore) && tentativeScore >= existingScore)
                        continue;

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeScore;
                    if (!insertionOrder.TryGetValue(neighbor, out long order))
                        insertionOrder[neighbor] = order = ++nextOrder;
                    Push(open, new OpenEntry(neighbor, tentativeScore,
                        Heuristic(neighbor, destination), order));
                }
            }

            exhaustedReachable = closed;
            return emptyPath;
        }

        public static bool CanTraverseStep(IGridCellProvider grid, GridCell from, GridCell to)
        {
            if (grid == null || from == null || to == null || !from.CanTraverseTo(to))
                return false;
            int dx = to.GridX - from.GridX;
            int dy = to.GridY - from.GridY;
            if (Mathf.Abs(dx) > 1 || Mathf.Abs(dy) > 1 || (dx == 0 && dy == 0))
                return false;
            if (dx == 0 || dy == 0)
                return true;

            // A side-facing stair is one explicitly authored diagonal edge. Its
            // orthogonal side cells deliberately belong to different heights, so
            // the normal same-height corner test cannot represent this crossing.
            if (IsAuthoredHeightConnection(from, to))
                return true;

            GridCell sideX = grid.GetCell(from.GridX + dx, from.GridY);
            GridCell sideY = grid.GetCell(from.GridX, from.GridY + dy);
            return from.CanTraverseTo(sideX) && sideX.CanTraverseTo(to)
                && from.CanTraverseTo(sideY) && sideY.CanTraverseTo(to);
        }

        public static double StepCost(GridCell from, GridCell to)
        {
            return from.GridX != to.GridX && from.GridY != to.GridY ? DiagonalCost : 1d;
        }

        private static bool IsDiagonal(GridCell from, GridCell to)
            => from.GridX != to.GridX && from.GridY != to.GridY;

        private static bool IsAuthoredHeightConnection(GridCell from, GridCell to)
            => from.HeightLevel != to.HeightLevel
                && from.HeightConnection == new Vector2Int(to.GridX, to.GridY)
                && to.HeightConnection == new Vector2Int(from.GridX, from.GridY);

        private static bool IsHigherPriority(OpenEntry first, OpenEntry second)
        {
            if (first.Score != second.Score) return first.Score < second.Score;
            if (first.Heuristic != second.Heuristic)
                return first.Heuristic < second.Heuristic;
            return first.Order < second.Order;
        }

        private static void Push(List<OpenEntry> heap, OpenEntry entry)
        {
            int index = heap.Count;
            heap.Add(entry);
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!IsHigherPriority(entry, heap[parent])) break;
                heap[index] = heap[parent];
                index = parent;
            }
            heap[index] = entry;
        }

        private static OpenEntry Pop(List<OpenEntry> heap)
        {
            OpenEntry best = heap[0];
            int lastIndex = heap.Count - 1;
            OpenEntry last = heap[lastIndex];
            heap.RemoveAt(lastIndex);
            if (lastIndex == 0) return best;

            int index = 0;
            while (index * 2 + 1 < lastIndex)
            {
                int child = index * 2 + 1;
                if (child + 1 < lastIndex
                    && IsHigherPriority(heap[child + 1], heap[child])) child++;
                if (!IsHigherPriority(heap[child], last)) break;
                heap[index] = heap[child];
                index = child;
            }
            heap[index] = last;
            return best;
        }

        private static double Heuristic(GridCell from, GridCell to)
        {
            int dx = Mathf.Abs(from.GridX - to.GridX);
            int dy = Mathf.Abs(from.GridY - to.GridY);
            int diagonal = Mathf.Min(dx, dy);
            return Mathf.Max(dx, dy) + (DiagonalCost - 1d) * diagonal;
        }

        private static List<GridCell> ReconstructPath(Dictionary<GridCell, GridCell> cameFrom, GridCell current)
        {
            var path = new List<GridCell> { current };
            while (cameFrom.TryGetValue(current, out GridCell previous))
            {
                current = previous;
                path.Add(current);
            }

            path.Reverse();
            return path;
        }
    }
}
