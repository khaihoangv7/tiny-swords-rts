using System;
using UnityEngine;

namespace TinyKingdom.Navigation
{
    public sealed class GridManager : MonoBehaviour, IGridCellProvider
    {
        [SerializeField] private NavigationGridData navigationData;

        private GridCell[,] cells;

        public NavigationGridData NavigationData => navigationData;
        public int Width => navigationData != null ? navigationData.Width : 0;
        public int Height => navigationData != null ? navigationData.Height : 0;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void SetNavigationData(NavigationGridData data)
        {
            navigationData = data;
            cells = null;
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (cells != null)
                return;
            if (navigationData == null)
                throw new InvalidOperationException("GridManager requires NavigationGridData.");
            if (navigationData.CellCount != navigationData.Width * navigationData.Height)
                throw new InvalidOperationException("NavigationGridData does not contain a complete grid.");

            cells = new GridCell[navigationData.Width, navigationData.Height];
            for (int y = 0; y < navigationData.Height; y++)
            for (int x = 0; x < navigationData.Width; x++)
            {
                if (!navigationData.TryGetCellData(x, y, out NavigationCellData data))
                    throw new InvalidOperationException($"Navigation data is missing cell ({x}, {y}).");

                cells[x, y] = new GridCell(x, y, GridToWorld(x, y), data.terrainType, data.heightLevel,
                    data.hasHeightConnection ? data.heightConnection : null);
            }
        }

        public bool IsInsideGrid(int gridX, int gridY)
        {
            return navigationData != null && navigationData.IsInsideGrid(gridX, gridY);
        }

        public GridCell GetCell(int gridX, int gridY)
        {
            if (!IsInsideGrid(gridX, gridY))
                return null;

            EnsureInitialized();
            return cells[gridX, gridY];
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            if (navigationData == null)
                throw new InvalidOperationException("GridManager requires NavigationGridData.");

            Vector2 offset = (Vector2)worldPosition - navigationData.WorldOrigin;
            return new Vector2Int(
                Mathf.FloorToInt(offset.x / navigationData.CellSize),
                Mathf.FloorToInt(offset.y / navigationData.CellSize));
        }

        public Vector3 GridToWorld(int gridX, int gridY)
        {
            if (navigationData == null)
                throw new InvalidOperationException("GridManager requires NavigationGridData.");

            return new Vector3(
                navigationData.WorldOrigin.x + (gridX + 0.5f) * navigationData.CellSize,
                navigationData.WorldOrigin.y + (gridY + 0.5f) * navigationData.CellSize,
                0f);
        }

        public bool SetOccupied(int gridX, int gridY, bool occupied)
        {
            GridCell cell = GetCell(gridX, gridY);
            if (cell == null)
                return false;

            cell.SetOccupied(occupied);
            return true;
        }
    }
}
