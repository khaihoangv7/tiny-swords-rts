using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Navigation
{
    [Serializable]
    public struct NavigationCellData
    {
        public int gridX;
        public int gridY;
        public TerrainType terrainType;
        public int heightLevel;

        public NavigationCellData(int gridX, int gridY, TerrainType terrainType, int heightLevel)
        {
            this.gridX = gridX;
            this.gridY = gridY;
            this.terrainType = terrainType;
            this.heightLevel = heightLevel;
        }
    }

    [CreateAssetMenu(menuName = "Tiny Kingdom/Navigation Grid Data", fileName = "NavigationGridData")]
    public sealed class NavigationGridData : ScriptableObject
    {
        [SerializeField, Min(1)] private int width = 40;
        [SerializeField, Min(1)] private int height = 28;
        [SerializeField] private Vector2 worldOrigin;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        [SerializeField] private List<NavigationCellData> cells = new();

        public int Width => width;
        public int Height => height;
        public Vector2 WorldOrigin => worldOrigin;
        public float CellSize => cellSize;
        public int CellCount => cells.Count;

        public bool TryGetCellData(int gridX, int gridY, out NavigationCellData cellData)
        {
            if (!IsInsideGrid(gridX, gridY))
            {
                cellData = default;
                return false;
            }

            int index = gridY * width + gridX;
            if (index >= cells.Count)
            {
                cellData = default;
                return false;
            }

            cellData = cells[index];
            return cellData.gridX == gridX && cellData.gridY == gridY;
        }

        public bool IsInsideGrid(int gridX, int gridY)
        {
            return gridX >= 0 && gridX < width && gridY >= 0 && gridY < height;
        }

        public void Configure(int newWidth, int newHeight, Vector2 newWorldOrigin, float newCellSize,
            IReadOnlyList<NavigationCellData> newCells)
        {
            if (newWidth <= 0 || newHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(newWidth), "Grid dimensions must be positive.");
            if (newCellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(newCellSize), "Cell size must be positive.");
            if (newCells == null || newCells.Count != newWidth * newHeight)
                throw new ArgumentException("Cell data must contain exactly width * height entries.", nameof(newCells));

            width = newWidth;
            height = newHeight;
            worldOrigin = newWorldOrigin;
            cellSize = newCellSize;
            cells = new List<NavigationCellData>(newCells);
        }
    }
}
