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
using UnityEngine.Tilemaps;

namespace TinyKingdom.EditorTools
{
    public static class Phase6H2NavigationSheepValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6H.2 Stairs Navigation")]
        public static void RunStairsNavigation() => Run();

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6H.2 Navigation & Sheep")]
        public static void Run()
        {
            ValidateVerifiedStairAssets();
            ValidateAuthoredConnectors();
            ValidateTraversalArchitecture();
            ValidateSoftSheepAndPlacement();
            Debug.Log("Phase 6H.2 stairs navigation validation passed: verified Color3 "
                + "side-stair assets, reciprocal endpoints, reserved transit corridors, "
                + "bounded traversal recovery, stable gather/build/formation destinations, "
                + "and soft Sheep behavior verified. Complete the mandatory Play Mode "
                + "stairs, resource and placement matrix before marking PASS.");
        }

        private static void ValidateVerifiedStairAssets()
        {
            const string sheetPath = "Assets/Terrain/Tileset/Tilemap_color3.png";
            TextureImporter importer = AssetImporter.GetAtPath(sheetPath) as TextureImporter;
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            Require(importer != null && Mathf.Approximately(importer.spritePixelsPerUnit, 64f)
                && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled,
                "Color3 stair sheet must retain 64 PPU, Point filtering and no mipmaps.");
            Require(sheet != null && sheet.width == 576 && sheet.height == 384,
                "Color3 stair sheet must retain its verified 576x384 dimensions.");
            string[] names =
            {
                "Tilemap_color3_r4c0", "Tilemap_color3_r5c0",
                "Tilemap_color3_r4c3", "Tilemap_color3_r5c3"
            };
            foreach (string name in names)
            {
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(
                    $"Assets/Tiles/Terrain/Color3/{name}.asset");
                Require(tile != null && tile.sprite != null && tile.sprite.name == name,
                    $"Verified stair Tile asset is missing or mismapped: {name}.");
                Require(Mathf.Approximately(tile.sprite.rect.width, 64f)
                    && Mathf.Approximately(tile.sprite.rect.height, 64f)
                    && Mathf.Approximately(tile.sprite.pixelsPerUnit, 64f)
                    && Vector2.Distance(tile.sprite.pivot, new Vector2(32f, 32f)) < 0.01f,
                    $"{name} must remain a centered native 64x64 tile at 64 PPU.");
            }
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
                var corridor = new HashSet<Vector2Int>();
                Transform visualGrid = GameObject.Find("Map")?.transform.Find("Grid");
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
                    Require(corridor.Add(definition.LowerCell)
                        && corridor.Add(definition.UpperCell),
                        $"Connector {i} duplicates another reserved corridor cell.");
                    Require(TerrainRules.IsConnectorCorridor(lower)
                        && TerrainRules.IsConnectorCorridor(upper)
                        && !TerrainRules.IsStableStandingCell(lower)
                        && !TerrainRules.IsStableStandingCell(upper),
                        $"Connector {i} endpoints are not reserved as transit-only cells.");
                    Require(!lower.Occupied && !upper.Occupied,
                        $"Connector {i} corridor is occupied by a static blocker.");
                    Require(AStarPathfinder.CanTraverseStep(grid, lower, upper)
                        && AStarPathfinder.CanTraverseStep(grid, upper, lower),
                        $"Connector {i} is not bidirectional.");
                    Require(AStarPathfinder.FindPath(grid, lower, upper).Count == 2
                        && AStarPathfinder.FindPath(grid, upper, lower).Count == 2,
                        $"Connector {i} does not produce the direct reciprocal edge.");
                    float cellSize = data.CellSize;
                    Require(Mathf.Approximately(Mathf.Abs(upper.WorldPosition.x
                            - lower.WorldPosition.x), cellSize)
                        && Mathf.Approximately(upper.WorldPosition.y
                            - lower.WorldPosition.y, cellSize),
                        $"Connector {i} world endpoints do not match the authored diagonal.");
                    Require(definition.VisualLowerCell == new Vector2Int(
                            definition.LowerX, definition.LowerY)
                        && definition.VisualUpperCell == new Vector2Int(
                            definition.LowerX, definition.UpperY),
                        $"Connector {i} does not retain the native same-column visual pair.");
                    ValidateSceneTerrainPair(visualGrid, definition, i);

                    List<GridCell> destinations = DestinationDistributor
                        .FindNearbyDestinations(grid, definition.LowerCell, 8, 3, true);
                    Require(destinations.Count > 0
                        && destinations.TrueForAll(cell =>
                            !TerrainRules.IsConnectorCorridor(cell)),
                        $"Connector {i} can still become a formation/ground destination.");
                    Require(!ResourceInteractionSlots.IsValidGatherCell(lower, lower)
                        && !ResourceInteractionSlots.IsValidGatherCell(upper, upper),
                        $"Connector {i} can still become a resource gather slot.");
                    Require(!BuildingSystem.IsValidPlacementCell(lower, lower.HeightLevel)
                        && !BuildingSystem.IsValidPlacementCell(upper, upper.HeightLevel),
                        $"Connector {i} can still be covered by a building footprint.");
                    ValidateNearbyResourceSlots(grid, definition, i);
                    Debug.Log($"Connector {i}: lower {definition.LowerCell} H"
                        + $"{lower.HeightLevel}, upper {definition.UpperCell} H"
                        + $"{upper.HeightLevel}, corridor [{definition.LowerCell}, "
                        + $"{definition.UpperCell}], orientation {definition.Orientation}.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateNearbyResourceSlots(GridManager grid,
            TerrainHeightConnectorDefinition definition, int connectorIndex)
        {
            int nearbyResources = 0;
            int rejectedCorridorCandidates = 0;
            ResourceNode[] resources = UnityEngine.Object.FindObjectsByType<ResourceNode>(
                FindObjectsInactive.Include);
            for (int i = 0; i < resources.Length; i++)
            {
                Vector2Int center = grid.WorldToGrid(resources[i].transform.position);
                int lowerDistance = Mathf.Max(Mathf.Abs(center.x - definition.LowerX),
                    Mathf.Abs(center.y - definition.LowerY));
                int upperDistance = Mathf.Max(Mathf.Abs(center.x - definition.UpperX),
                    Mathf.Abs(center.y - definition.UpperY));
                if (Mathf.Min(lowerDistance, upperDistance) > 2) continue;
                nearbyResources++;
                GridCell target = grid.GetCell(center.x, center.y);
                foreach (Vector2Int corridorCell in new[]
                    { definition.LowerCell, definition.UpperCell })
                {
                    if (Mathf.Max(Mathf.Abs(center.x - corridorCell.x),
                            Mathf.Abs(center.y - corridorCell.y)) != 1) continue;
                    GridCell candidate = grid.GetCell(corridorCell.x, corridorCell.y);
                    Require(!ResourceInteractionSlots.IsValidGatherCell(target, candidate),
                        $"Resource near connector {connectorIndex} accepts corridor cell "
                        + $"{corridorCell} as a gather slot.");
                    rejectedCorridorCandidates++;
                }
            }
            Debug.Log($"Connector {connectorIndex}: nearby resources {nearbyResources}, "
                + $"rejected corridor gather candidates {rejectedCorridorCandidates}.");
        }

        private static void ValidateSceneTerrainPair(Transform visualGrid,
            TerrainHeightConnectorDefinition definition, int index)
        {
            if (visualGrid == null) return;
            Tilemap map = visualGrid.Find("ElevatedGround_L"
                + (definition.LowerHeight + 1))?.GetComponent<Tilemap>();
            Require(map != null, $"Connector {index} terrain Tilemap is missing.");
            TileBase lower = map.GetTile(new Vector3Int(definition.VisualLowerCell.x,
                definition.VisualLowerCell.y, 0));
            TileBase upper = map.GetTile(new Vector3Int(definition.VisualUpperCell.x,
                definition.VisualUpperCell.y, 0));
            Require(lower is Tile lowerTile && lowerTile.sprite != null
                    && lowerTile.sprite.name == definition.LowerSpriteName
                && upper is Tile upperTile && upperTile.sprite != null
                    && upperTile.sprite.name == definition.UpperSpriteName,
                $"Connector {index} scene terrain does not use its verified native pair.");
            TileBase surfaceConnection = map.GetTile(new Vector3Int(
                definition.SurfaceConnectionCell.x, definition.SurfaceConnectionCell.y, 0));
            string expectedSurface = BattlefieldTerraces
                .SurfaceConnectionSpriteName(definition);
            Require(surfaceConnection is Tile surfaceTile && surfaceTile.sprite != null
                    && surfaceTile.sprite.name == expectedSurface,
                $"Connector {index} still has a closed plateau edge at its upper exit; "
                + $"expected {expectedSurface}.");
            if (!BattlefieldTerraces.RequiresCliffConnection(definition)) return;
            TileBase cliffConnection = map.GetTile(new Vector3Int(
                definition.CliffConnectionCell.x, definition.CliffConnectionCell.y, 0));
            string expectedCliff = BattlefieldTerraces
                .CliffConnectionSpriteName(definition);
            Require(cliffConnection is Tile cliffTile && cliffTile.sprite != null
                    && cliffTile.sprite.name == expectedCliff,
                $"Connector {index} still has a closed cliff corner below its opening; "
                + $"expected {expectedCliff}.");
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
            Require(typeof(UnitSeparation).GetMethod("ApplyAlongAxis",
                    BindingFlags.Static | BindingFlags.Public) != null,
                "Connector traversal does not constrain separation to its authored axis.");
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
                var visualObject = new GameObject("Visual");
                visualObject.transform.SetParent(sheepObject.transform, false);
                SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                BoxCollider2D collider = sheepObject.AddComponent<BoxCollider2D>();
                sheepObject.AddComponent<ResourceInteractionSlots>().Configure(1);
                sheep = sheepObject.AddComponent<ResourceNode>();
                sheep.Configure(ResourceType.Food, 200, renderer);
                sheepObject.AddComponent<SheepPresentation>().Configure(sheep, renderer,
                    new[] { sprite }, new[] { sprite }, new[] { sprite });
                Require(sheep.TryRegisterOccupancy(grid),
                    "Validation Sheep could not register its soft logical cell.");
                Require(sheep.OccupiedCell == new Vector2Int(2, 2),
                    "Validation Sheep visual alignment moved its logical root cell.");
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
