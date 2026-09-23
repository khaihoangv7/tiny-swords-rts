using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;
using System.Collections.Generic;

namespace TinyKingdom.Buildings
{
    public static class BuildingSpawnPoint
    {
        // Try the four edges first, then a compact second ring. Never enter the
        // producer's footprint or jump to arbitrary distant terrain.
        public static bool TryFind(IProductionHost host, GridManager grid, out Vector3 position)
        {
            var positions = new List<Vector3>();
            if (TryFindMany(host, grid, 1, positions))
            {
                position = positions[0];
                return true;
            }
            position = default;
            return false;
        }

        public static bool TryFindMany(IProductionHost host, GridManager grid, int count,
            List<Vector3> positions, int maximumRing = 10)
        {
            positions.Clear();
            if (host == null || grid == null || count <= 0) return false;
            Vector2Int origin = host.FootprintOrigin;
            Vector2Int size = host.Footprint;
            GridCell footprintCell = grid.GetCell(origin.x, origin.y);
            if (footprintCell == null) return false;
            for (int ring = 1; ring <= Mathf.Max(1, maximumRing); ring++)
            {
                for (int x = origin.x; x < origin.x + size.x; x++)
                    Add(x, origin.y - ring, footprintCell, grid, positions, count);
                for (int y = origin.y; y < origin.y + size.y; y++)
                    Add(origin.x + size.x - 1 + ring, y, footprintCell, grid, positions, count);
                for (int x = origin.x; x < origin.x + size.x; x++)
                    Add(x, origin.y + size.y - 1 + ring, footprintCell, grid, positions, count);
                for (int y = origin.y; y < origin.y + size.y; y++)
                    Add(origin.x - ring, y, footprintCell, grid, positions, count);
                if (positions.Count >= count) return true;
            }
            return positions.Count > 0;
        }

        private static void Add(int x, int y, GridCell footprintCell, GridManager grid,
            List<Vector3> positions, int count)
        {
            if (positions.Count >= count) return;
            if (Check(x, y, footprintCell, grid, out Vector3 position)
                && !positions.Exists(item => Vector2.Distance(item, position) < 0.1f))
                positions.Add(position);
        }

        private static bool Check(int x, int y, GridCell footprintCell,
            GridManager grid, out Vector3 position)
        {
            position = default;
            GridCell cell = grid.GetCell(x, y);
            if (cell == null || !cell.Walkable || cell.Occupied
                || cell.HeightLevel != footprintCell.HeightLevel) return false;
            foreach (UnitController unit in UnitController.ActiveUnits)
                if (unit != null && unit.IsAlive
                    && Vector2.Distance(unit.transform.position, cell.WorldPosition) < 0.7f)
                    return false;
            position = cell.WorldPosition;
            return true;
        }
    }
}
