using System;
using System.Collections.Generic;
using System.Linq;
using TinyKingdom.Navigation;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase2NavigationValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 2 Navigation")]
        public static void RunFromMenu()
        {
            NavigationGridData data = AssetDatabase.LoadAssetAtPath<NavigationGridData>(
                Phase2NavigationBuilder.DataPath);
            Run(data, true);
        }

        public static void Run(NavigationGridData data, bool logSuccess)
        {
            if (data == null)
                throw new InvalidOperationException("MapNavigation asset was not found.");

            var grid = new ValidationGrid(data);

            List<GridCell> validPath = Find(grid, 12, 15, 47, 31);
            Require(validPath.Count > 0, "A valid flat-ground path was not found.");
            Require(validPath.All(cell => cell.TerrainType != TerrainType.Water),
                "A valid path entered Water.");

            List<GridCell> cliffDetour = Find(grid, 4, 8, 22, 8);
            Require(cliffDetour.Count > 0, "A path around the central cliff was not found.");
            Require(cliffDetour.All(cell => cell.TerrainType != TerrainType.Cliff),
                "A path entered a Cliff cell.");

            GridCell flat = grid.GetCell(22, 17);
            GridCell elevated = grid.GetCell(20, 16);
            Require(!flat.CanTraverseTo(elevated),
                "Direct movement from height 0 to height 1 was incorrectly allowed.");
            List<GridCell> elevatedDetour = Find(grid, 22, 17, 20, 16);
            Require(elevatedDetour.Count > 0
                && elevatedDetour.Any(cell => cell.TerrainType == TerrainType.Stairs),
                "The elevated destination should be reachable only by routing through a stair.");

            Require(Find(grid, 12, 15, 0, 0).Count == 0,
                "A Water destination should fail safely.");

            GridCell occupied = grid.GetCell(10, 3);
            occupied.SetOccupied(true);
            List<GridCell> occupiedDetour = Find(grid, 9, 3, 11, 3);
            Require(occupiedDetour.Count > 0 && !occupiedDetour.Contains(occupied),
                "A path did not avoid an occupied cell.");
            occupied.SetOccupied(false);

            ValidateStairsSupport();

            if (logSuccess)
                Debug.Log("Phase 2 validation passed: flat path, Water, Cliff, explicit stair height transition, occupancy and unreachable cases.");
        }

        private static void ValidateStairsSupport()
        {
            var stairGrid = new ValidationGrid(3, 1, new[]
            {
                new NavigationCellData(0, 0, TerrainType.FlatGround, 0),
                new NavigationCellData(1, 0, TerrainType.Stairs, 0),
                new NavigationCellData(2, 0, TerrainType.ElevatedGround, 1)
            });
            Require(Find(stairGrid, 0, 0, 2, 0).Count == 3,
                "A Stairs cell did not connect height 0 and height 1.");
        }

        private static List<GridCell> Find(IGridCellProvider grid, int startX, int startY, int endX, int endY)
        {
            return AStarPathfinder.FindPath(
                grid,
                grid.GetCell(startX, startY),
                grid.GetCell(endX, endY));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class ValidationGrid : IGridCellProvider
        {
            private readonly GridCell[,] cells;

            public int Width { get; }
            public int Height { get; }

            public ValidationGrid(NavigationGridData data)
            {
                Width = data.Width;
                Height = data.Height;
                cells = new GridCell[Width, Height];
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (!data.TryGetCellData(x, y, out NavigationCellData cellData))
                        throw new InvalidOperationException($"Missing validation cell ({x}, {y}).");
                    cells[x, y] = CreateCell(cellData);
                }
            }

            public ValidationGrid(int width, int height, IReadOnlyList<NavigationCellData> data)
            {
                Width = width;
                Height = height;
                cells = new GridCell[width, height];
                foreach (NavigationCellData cellData in data)
                    cells[cellData.gridX, cellData.gridY] = CreateCell(cellData);
            }

            public bool IsInsideGrid(int gridX, int gridY)
            {
                return gridX >= 0 && gridX < Width && gridY >= 0 && gridY < Height;
            }

            public GridCell GetCell(int gridX, int gridY)
            {
                return IsInsideGrid(gridX, gridY) ? cells[gridX, gridY] : null;
            }

            private static GridCell CreateCell(NavigationCellData data)
            {
                return new GridCell(data.gridX, data.gridY,
                    new Vector3(data.gridX + 0.5f, data.gridY + 0.5f),
                    data.terrainType, data.heightLevel,
                    data.hasHeightConnection ? data.heightConnection : null);
            }
        }
    }
}
