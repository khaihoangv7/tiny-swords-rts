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
        {
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

            var open = new List<GridCell> { start };
            var closed = new HashSet<GridCell>();
            var cameFrom = new Dictionary<GridCell, GridCell>();
            var gScore = new Dictionary<GridCell, double> { [start] = 0d };
            var fScore = new Dictionary<GridCell, double> { [start] = Heuristic(start, destination) };

            while (open.Count > 0)
            {
                GridCell current = GetLowestScore(open, fScore, destination);
                if (current == destination)
                    return ReconstructPath(cameFrom, current);

                open.Remove(current);
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

                    double tentativeScore = gScore[current] + StepCost(current, neighbor)
                        + (policy?.AdditionalCost(current, neighbor) ?? 0d);
                    if (gScore.TryGetValue(neighbor, out double existingScore) && tentativeScore >= existingScore)
                        continue;

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeScore;
                    fScore[neighbor] = tentativeScore + Heuristic(neighbor, destination);
                    if (!open.Contains(neighbor))
                        open.Add(neighbor);
                }
            }

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

        private static GridCell GetLowestScore(List<GridCell> open, Dictionary<GridCell, double> fScore,
            GridCell destination)
        {
            GridCell best = open[0];
            double bestScore = fScore[best];
            double bestHeuristic = Heuristic(best, destination);

            for (int i = 1; i < open.Count; i++)
            {
                GridCell candidate = open[i];
                double candidateScore = fScore[candidate];
                double candidateHeuristic = Heuristic(candidate, destination);
                if (candidateScore < bestScore
                    || (candidateScore == bestScore && candidateHeuristic < bestHeuristic))
                {
                    best = candidate;
                    bestScore = candidateScore;
                    bestHeuristic = candidateHeuristic;
                }
            }

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
