using System;
using System.Collections.Generic;
using System.Reflection;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Terrain;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase6H2NavigationSheepValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6H.2 Navigation & Sheep")]
        public static void Run()
        {
            ValidateAuthoredConnectors();
            ValidateTraversalArchitecture();
            ValidateSoftSheepAndPlacement();
            Debug.Log("Phase 6H.2 navigation and Sheep validation passed: reciprocal "
                + "stairs, endpoint traversal/watchdog, soft Sheep occupancy, trigger "
                + "selection, Sheep-only placement exception, bounded escape search and "
                + "authoritative building blocking verified. Complete the Play Mode matrix "
                + "before marking PASS.");
        }

        private static void ValidateAuthoredConnectors()
        {
            NavigationGridData data = AssetDatabase.LoadAssetAtPath<NavigationGridData>(
                Phase2NavigationBuilder.DataPath);
            Require(data != null, "Map navigation data is missing.");
            var root = new GameObject("Phase6H2ConnectorValidation");
            try
            {
                GridManager grid = root.AddComponent<GridManager>();
                grid.SetNavigationData(data);
                for (int i = 0; i < BattlefieldTerraces.ConnectorCount; i++)
                {
                    TerrainHeightConnectorDefinition definition =
                        BattlefieldTerraces.Connector(i);
                    GridCell lower = grid.GetCell(definition.LowerX, definition.LowerY);
                    GridCell upper = grid.GetCell(definition.UpperX, definition.UpperY);
                    Require(lower != null && upper != null
                        && lower.HeightConnection == definition.UpperCell
                        && upper.HeightConnection == definition.LowerCell,
                        $"Connector {i} endpoints are not reciprocal.");
                    Require(AStarPathfinder.CanTraverseStep(grid, lower, upper)
                        && AStarPathfinder.CanTraverseStep(grid, upper, lower),
                        $"Connector {i} is not bidirectional.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateTraversalArchitecture()
        {
            Require(UnitMovement.ConnectorStuckSeconds >= 0.5f
                && UnitMovement.ConnectorStuckSeconds <= 1.5f,
                "Connector stuck watchdog is outside its bounded recovery range.");
            Require(UnitMovement.BlockedPathRepathInterval >= 0.2f,
                "Blocked-route repath throttle is too aggressive.");
            Require(typeof(WaypointPath).GetMethod("AdvanceCurrentWaypoint",
                    BindingFlags.Instance | BindingFlags.Public) != null,
                "Connector traversal cannot consume one endpoint at a time.");
            Require(typeof(UnitMovement).GetMethod("IsSafeConnectorStep",
                    BindingFlags.Instance | BindingFlags.NonPublic) != null
                && typeof(UnitMovement).GetMethod("UpdateConnectorProgress",
                    BindingFlags.Instance | BindingFlags.NonPublic) != null,
                "Connector-specific validation or watchdog state is missing.");
        }

        private static void ValidateSoftSheepAndPlacement()
        {
            NavigationGridData data = ScriptableObject.CreateInstance<NavigationGridData>();
            Sprite sprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            var root = new GameObject("Phase6H2SheepValidation");
            ResourceNode sheep = null;
            BuildingData house = FindBuildingData(BuildingType.House);
            Require(house != null, "House BuildingData is missing.");
            try
            {
                GridManager grid = root.AddComponent<GridManager>();
                ConfigureOpenGrid(data, new Vector2(1000f, 1000f));
                grid.SetNavigationData(data);
                root.AddComponent<PathfindingSystem>().SetGridManager(grid);

                var sheepObject = new GameObject("ValidationSheep");
                sheepObject.transform.SetParent(root.transform, false);
                sheepObject.transform.position = grid.GridToWorld(2, 2);
                SpriteRenderer renderer = sheepObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                BoxCollider2D collider = sheepObject.AddComponent<BoxCollider2D>();
                sheepObject.AddComponent<ResourceInteractionSlots>().Configure(1);
                sheep = sheepObject.AddComponent<ResourceNode>();
                sheep.Configure(ResourceType.Food, 200, renderer);
                sheepObject.AddComponent<SheepPresentation>().Configure(sheep, renderer,
                    new[] { sprite }, new[] { sprite }, new[] { sprite });
                Require(sheep.TryRegisterOccupancy(grid),
                    "Validation Sheep could not register its soft logical cell.");
                Require(!grid.GetCell(2, 2).Occupied,
                    "Sheep still marks a hard navigation cell occupied.");
                Require(collider.isTrigger,
                    "Sheep gameplay collider is not a nonblocking trigger.");
                WorldSelectionHitArea hitArea = sheepObject
                    .GetComponentInChildren<WorldSelectionHitArea>(true);
                Require(hitArea != null && hitArea.HitCollider != null
                    && hitArea.HitCollider.isTrigger,
                    "Sheep trigger selection coverage was not preserved.");

                ResourceStockpile stockpile = root.AddComponent<ResourceStockpile>();
                stockpile.Configure(Faction.Blue, 999, 999, 999);
                FactionPopulation population = root.AddComponent<FactionPopulation>();
                population.Configure(Faction.Blue, 50);
                BuildingSystem buildings = root.AddComponent<BuildingSystem>();
                buildings.Configure(grid, stockpile, population,
                    new[] { house });
                Physics2D.SyncTransforms();
                Vector2Int origin = new(2, 2);
                Require(buildings.CanPlace(BuildingType.House, origin),
                    "Sheep alone still invalidates building placement.");

                grid.SetOccupied(origin.x, origin.y, true);
                Require(!buildings.CanPlace(BuildingType.House, origin),
                    "A true static blocker was ignored by placement.");
                grid.SetOccupied(origin.x, origin.y, false);

                var siteObject = new GameObject("ValidationBuilding");
                siteObject.transform.SetParent(root.transform, false);
                BuildingSite site = siteObject.AddComponent<BuildingSite>();
                site.Configure(house, Faction.Blue, origin, grid, population);
                Require(grid.GetCell(origin.x, origin.y).Occupied,
                    "Placed building footprint does not block navigation.");
                UnityEngine.Object.DestroyImmediate(siteObject);
                Require(!grid.GetCell(origin.x, origin.y).Occupied,
                    "Destroyed validation building did not release occupancy.");

                Require(BuildingSystem.SheepEscapeMaximumRing > 0
                    && BuildingSystem.SheepEscapeMaximumRing <= 8
                    && typeof(BuildingSystem).GetMethod("TryFindSheepEscape",
                        BindingFlags.Instance | BindingFlags.NonPublic) != null,
                    "Sheep escape search is missing or unbounded.");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static BuildingData FindBuildingData(BuildingType type)
        {
            string[] guids = AssetDatabase.FindAssets("t:BuildingData");
            for (int i = 0; i < guids.Length; i++)
            {
                BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (data != null && data.Type == type) return data;
            }
            return null;
        }

        private static void ConfigureOpenGrid(NavigationGridData data, Vector2 origin)
        {
            var cells = new List<NavigationCellData>();
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                cells.Add(new NavigationCellData(x, y, TerrainType.FlatGround, 0));
            data.Configure(8, 8, origin, 1f, cells);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
