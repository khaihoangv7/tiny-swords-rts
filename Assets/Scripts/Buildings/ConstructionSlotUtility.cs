using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    // Shared deterministic perimeter geometry for placement validation and the
    // runtime site reservation owner. Slots are edge-adjacent, never diagonal.
    public static class ConstructionSlotUtility
    {
        public const int MaximumSlots = 64;

        public static int SlotCount(Vector2Int footprint)
            => Mathf.Min(MaximumSlots, Mathf.Max(0, footprint.x * 2 + footprint.y * 2));

        public static Vector2Int CellAt(Vector2Int origin, Vector2Int footprint,
            int slotIndex)
        {
            int width = Mathf.Max(0, footprint.x);
            int height = Mathf.Max(0, footprint.y);
            if (slotIndex < width)
                return origin + new Vector2Int(slotIndex, -1);
            slotIndex -= width;
            if (slotIndex < width)
                return origin + new Vector2Int(slotIndex, height);
            slotIndex -= width;
            if (slotIndex < height)
                return origin + new Vector2Int(-1, slotIndex);
            slotIndex -= height;
            return origin + new Vector2Int(width, slotIndex);
        }

        public static bool IsValid(GridManager grid, Vector2Int origin,
            Vector2Int footprint, int slotIndex)
        {
            if (grid == null || slotIndex < 0 || slotIndex >= SlotCount(footprint))
                return false;
            GridCell footprintCell = grid.GetCell(origin.x, origin.y);
            Vector2Int cellPosition = CellAt(origin, footprint, slotIndex);
            GridCell cell = grid.GetCell(cellPosition.x, cellPosition.y);
            return footprintCell != null && TerrainRules.IsStableStandingCell(cell)
                && cell.HeightLevel == footprintCell.HeightLevel
                && HasExternalIngress(grid, origin, footprint, cellPosition, cell);
        }

        private static bool HasExternalIngress(GridManager grid, Vector2Int origin,
            Vector2Int footprint, Vector2Int slotPosition, GridCell slot)
        {
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                Vector2Int neighborPosition = slotPosition + new Vector2Int(x, y);
                if (Contains(origin, footprint, neighborPosition)) continue;
                GridCell neighbor = grid.GetCell(neighborPosition.x, neighborPosition.y);
                if (neighbor != null && neighbor.Walkable && !neighbor.Occupied
                    && AStarPathfinder.CanTraverseStep(grid, slot, neighbor)) return true;
            }
            return false;
        }

        private static bool Contains(Vector2Int origin, Vector2Int size,
            Vector2Int cell)
            => cell.x >= origin.x && cell.x < origin.x + size.x
                && cell.y >= origin.y && cell.y < origin.y + size.y;
    }
}
