using System;
using System.Collections.Generic;
using System.Linq;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class MovementPolishValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Movement Polish")]
        public static void Run()
        {
            NavigationGridData data = ScriptableObject.CreateInstance<NavigationGridData>();
            var objectWithGrid = new GameObject("MovementPolishValidationGrid");
            try
            {
                GridManager grid = objectWithGrid.AddComponent<GridManager>();
                Configure(data, 8, 8, (x, y) => TerrainType.FlatGround);
                grid.SetNavigationData(data);

                List<GridCell> diagonal = Path(grid, 0, 0, 3, 3);
                Require(diagonal.Count == 4 && diagonal.Skip(1).Select((cell, i) =>
                    Mathf.Abs(cell.GridX - diagonal[i].GridX) == 1
                    && Mathf.Abs(cell.GridY - diagonal[i].GridY) == 1).All(value => value),
                    "Open ground did not use a shortest diagonal route.");
                double cost = diagonal.Skip(1).Select((cell, i) =>
                    AStarPathfinder.StepCost(diagonal[i], cell)).Sum();
                Require(Math.Abs(cost - 3d * Math.Sqrt(2d)) < 1e-9,
                    "Diagonal cost is not sqrt(2).");

                Configure(data, 2, 2, (x, y) =>
                    (x == 1 && y == 0) ? TerrainType.Water : TerrainType.FlatGround);
                grid.SetNavigationData(data);
                Require(!AStarPathfinder.CanTraverseStep(grid, grid.GetCell(0, 0), grid.GetCell(1, 1))
                    && Path(grid, 0, 0, 1, 1).Count == 3,
                    "Water corner was cut diagonally.");

                Configure(data, 2, 2, (x, y) =>
                    (x == 1 && y == 0) ? TerrainType.Cliff : TerrainType.FlatGround);
                grid.SetNavigationData(data);
                Require(!AStarPathfinder.CanTraverseStep(grid, grid.GetCell(0, 0), grid.GetCell(1, 1)),
                    "Cliff corner was cut diagonally.");

                Configure(data, 2, 2, (x, y) => TerrainType.FlatGround);
                grid.SetNavigationData(data);
                grid.SetOccupied(1, 0, true);
                Require(!AStarPathfinder.CanTraverseStep(grid, grid.GetCell(0, 0), grid.GetCell(1, 1)),
                    "Occupied building corner was cut diagonally.");
                Require(UnitMovement.BlockedPathRepathInterval >= 0.2f
                    && UnitMovement.BlockedPathRepathInterval <= 0.5f,
                    "Blocked-route repath interval is outside the safe responsive range.");
                Require(UnitSeparation.IsSafeSegment(grid,
                        grid.GetCell(1, 0).WorldPosition,
                        grid.GetCell(0, 0).WorldPosition),
                    "A unit already overlapped by a moving blocker cannot escape its cell.");
                Require(!UnitSeparation.IsSafeSegment(grid,
                        grid.GetCell(0, 0).WorldPosition,
                        grid.GetCell(1, 0).WorldPosition),
                    "A unit can still enter a newly occupied resource cell.");

                var heightCells = new List<NavigationCellData>();
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    heightCells.Add(new NavigationCellData(x, y,
                        x == 1 && y == 1 ? TerrainType.ElevatedGround : TerrainType.FlatGround,
                        x == 1 && y == 1 ? 1 : 0));
                data.Configure(2, 2, Vector2.zero, 1f, heightCells);
                grid.SetNavigationData(data);
                Require(!AStarPathfinder.CanTraverseStep(grid, grid.GetCell(0, 0), grid.GetCell(1, 1))
                    && Path(grid, 0, 0, 1, 1).Count == 0,
                    "Diagonal bypassed a stairless height transition.");

                var diagonalStairCells = new List<NavigationCellData>();
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                {
                    var cell = new NavigationCellData(x, y,
                        x == y ? TerrainType.Stairs : TerrainType.Cliff,
                        x == 1 && y == 1 ? 1 : 0);
                    if (x == 0 && y == 0)
                    {
                        cell.hasHeightConnection = true;
                        cell.heightConnection = new Vector2Int(1, 1);
                    }
                    else if (x == 1 && y == 1)
                    {
                        cell.hasHeightConnection = true;
                        cell.heightConnection = new Vector2Int(0, 0);
                    }
                    diagonalStairCells.Add(cell);
                }
                data.Configure(2, 2, Vector2.zero, 1f, diagonalStairCells);
                grid.SetNavigationData(data);
                Require(AStarPathfinder.CanTraverseStep(grid,
                        grid.GetCell(0, 0), grid.GetCell(1, 1))
                    && Path(grid, 0, 0, 1, 1).Count == 2,
                    "Authored diagonal stair connector was not traversable.");

                NavigationGridData map = AssetDatabase.LoadAssetAtPath<NavigationGridData>(
                    Phase2NavigationBuilder.DataPath);
                Require(map != null, "Map navigation data missing.");
                Phase2NavigationValidation.Run(map, false);

                Configure(data, 8, 8, (x, y) => TerrainType.FlatGround);
                grid.SetNavigationData(data);
                List<GridCell> destinations = DestinationDistributor.FindNearbyDestinations(
                    grid, new Vector2Int(4, 4), 3);
                Require(destinations.Count == 3 && destinations.Distinct().Count() == 3,
                    "Group destinations are not distinct.");

                var path = new WaypointPath();
                path.Set(new[] { new Vector3(1.5f, 1.5f) }, new Vector3(0.5f, 0.5f), 0.01f);
                Vector3 intermediate = path.Advance(new Vector3(0.5f, 0.5f), 1f, 0.1f,
                    0.01f, out bool completed);
                Require(!completed && intermediate.x > 0.5f && intermediate.x < 1.5f
                    && Mathf.Abs(intermediate.x - intermediate.y) < 0.0001f,
                    "Diagonal waypoint motion snapped to a tile.");

                ValidateLocalSeparation(grid, data);
                Debug.Log("Movement polish validation passed: octile paths, sqrt(2) cost, Water/Cliff/occupied corners, height, three destinations, continuous diagonal waypoints and local separation. Play Mode interaction remains manual.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(objectWithGrid);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static void ValidateLocalSeparation(GridManager grid, NavigationGridData data)
        {
            List<GridCell> goals = DestinationDistributor.FindNearbyDestinations(
                grid, new Vector2Int(4, 4), 3);
            var paths = new WaypointPath[3];
            Vector3[] moving = { new(3.5f, 3.5f), new(3.5f, 3.5f), new(3.5f, 3.5f) };
            for (int i = 0; i < moving.Length; i++)
            {
                paths[i] = new WaypointPath();
                paths[i].Set(new[] { goals[i].WorldPosition }, moving[i], 0.02f);
            }
            for (int tick = 0; tick < 100; tick++)
            for (int i = 0; i < moving.Length; i++)
            {
                Vector3 desired = paths[i].Advance(moving[i], 3f, 0.05f, 0.02f, out _);
                var neighbors = new List<UnitSeparation.Neighbor>();
                for (int j = 0; j < moving.Length; j++)
                    if (i != j) neighbors.Add(new UnitSeparation.Neighbor(moving[j], 0.35f, j + 1));
                moving[i] = UnitSeparation.Resolve(desired, 0.35f, 1.2f,
                    0.05f, i + 1, neighbors, grid);
            }
            Require(PairsSeparated(moving, 0.25f)
                && moving.Select((position, index) =>
                    Vector3.Distance(position, goals[index].WorldPosition) < 0.75f).All(value => value),
                "Units with distinct nearby destinations ended stacked or missed their goals.");

            Vector3[] positions = { new(3.5f, 3.5f), new(3.5f, 3.5f), new(3.5f, 3.5f) };
            for (int tick = 0; tick < 30; tick++)
            for (int i = 0; i < positions.Length; i++)
            {
                var neighbors = new List<UnitSeparation.Neighbor>();
                for (int j = 0; j < positions.Length; j++)
                    if (i != j) neighbors.Add(new UnitSeparation.Neighbor(positions[j], 0.35f, j + 1));
                positions[i] = UnitSeparation.Resolve(positions[i], 0.35f, 1.2f,
                    0.05f, i + 1, neighbors, grid);
            }
            Require(PairsSeparated(positions, 0.25f),
                "Three living units remained stacked after local separation.");

            Vector3 target = new(4.5f, 4.5f);
            Vector3[] attackers = { target, target, target };
            for (int tick = 0; tick < 30; tick++)
            for (int i = 0; i < attackers.Length; i++)
            {
                var neighbors = new List<UnitSeparation.Neighbor>
                {
                    new(target, 0.35f, 10)
                };
                for (int j = 0; j < attackers.Length; j++)
                    if (i != j) neighbors.Add(new UnitSeparation.Neighbor(attackers[j], 0.35f, j + 1));
                attackers[i] = UnitSeparation.Resolve(attackers[i], 0.35f, 1.2f,
                    0.05f, i + 1, neighbors, grid);
            }
            Require(PairsSeparated(attackers, 0.1f)
                && attackers.All(attacker => Vector3.Distance(attacker, target) <= 1f),
                "Melee attackers stacked or left Warrior attack range.");

            Configure(data, 8, 8, (x, y) => x == 5 && y == 4
                ? TerrainType.Water : TerrainType.FlatGround);
            grid.SetNavigationData(data);
            Vector3 nearEdge = new(4.95f, 4.5f);
            Vector3 corrected = UnitSeparation.Resolve(nearEdge, 0.35f, 1.2f, 0.05f, 1,
                new[] { new UnitSeparation.Neighbor(new Vector2(4.7f, 4.5f), 0.35f, 2) }, grid);
            Require(grid.WorldToGrid(corrected) == new Vector2Int(4, 4),
                "Separation pushed a unit into Water.");

            Configure(data, 8, 8, (x, y) => x == 5 && y == 4
                ? TerrainType.Cliff : TerrainType.FlatGround);
            grid.SetNavigationData(data);
            corrected = UnitSeparation.Resolve(nearEdge, 0.35f, 1.2f, 0.05f, 1,
                new[] { new UnitSeparation.Neighbor(new Vector2(4.7f, 4.5f), 0.35f, 2) }, grid);
            Require(grid.WorldToGrid(corrected) == new Vector2Int(4, 4),
                "Separation pushed a unit into Cliff.");

            var elevated = new List<NavigationCellData>();
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                elevated.Add(new NavigationCellData(x, y, TerrainType.FlatGround,
                    x == 5 && y == 4 ? 1 : 0));
            data.Configure(8, 8, Vector2.zero, 1f, elevated);
            grid.SetNavigationData(data);
            corrected = UnitSeparation.Resolve(nearEdge, 0.35f, 1.2f, 0.05f, 1,
                new[] { new UnitSeparation.Neighbor(new Vector2(4.7f, 4.5f), 0.35f, 2) }, grid);
            Require(grid.WorldToGrid(corrected) == new Vector2Int(4, 4),
                "Separation bypassed a stairless height transition.");
        }

        private static bool PairsSeparated(Vector3[] points, float minimum)
        {
            for (int i = 0; i < points.Length; i++)
            for (int j = i + 1; j < points.Length; j++)
                if (Vector3.Distance(points[i], points[j]) <= minimum) return false;
            return true;
        }

        private static void Configure(NavigationGridData data, int width, int height,
            Func<int, int, TerrainType> terrain)
        {
            var cells = new List<NavigationCellData>();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                cells.Add(new NavigationCellData(x, y, terrain(x, y), 0));
            data.Configure(width, height, Vector2.zero, 1f, cells);
        }

        private static List<GridCell> Path(IGridCellProvider grid, int x0, int y0, int x1, int y1)
            => AStarPathfinder.FindPath(grid, grid.GetCell(x0, y0), grid.GetCell(x1, y1));

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
