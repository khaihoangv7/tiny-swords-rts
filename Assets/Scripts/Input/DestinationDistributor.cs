using System.Collections.Generic;
using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Input
{
    public static class DestinationDistributor
    {
        public static List<GridCell> FindNearbyDestinations(IGridCellProvider grid, Vector2Int center,
            int requestedCount, int maximumRadius = 8)
        {
            var result = new List<GridCell>();
            if (grid == null || requestedCount <= 0 || !grid.IsInsideGrid(center.x, center.y))
                return result;

            GridCell centerCell = grid.GetCell(center.x, center.y);
            if (centerCell == null || !centerCell.Walkable || centerCell.Occupied)
                return result;

            AddIfValid(grid, center.x, center.y, centerCell.HeightLevel, result, requestedCount);
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
}
