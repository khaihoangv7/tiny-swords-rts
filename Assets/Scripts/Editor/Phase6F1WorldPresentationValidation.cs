#if UNITY_EDITOR
using System;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Terrain;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace TinyKingdom.EditorTools
{
    public static class Phase6F1WorldPresentationValidation
    {
        private static readonly string[] BushPaths =
        {
            "Assets/Terrain/Decorations/Bushes/Bushe1.png",
            "Assets/Terrain/Decorations/Bushes/Bushe2.png",
            "Assets/Terrain/Decorations/Bushes/Bushe3.png",
            "Assets/Terrain/Decorations/Bushes/Bushe4.png"
        };

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6F.1 World Presentation")]
        public static void Validate()
        {
            ValidateBushAssets();
            ValidateOrderingMath();
            ValidateConstants();
            if (EditorApplication.isPlaying) ValidatePlayMode();
            Debug.Log(EditorApplication.isPlaying
                ? "Phase 6F.1 automated validation passed. Complete the visual Play Mode checklist before marking PASS."
                : "Phase 6F.1 asset and architecture validation passed. Enter Play Mode and run this validator again.");
        }

        private static void ValidateBushAssets()
        {
            PresentationAssetCatalog catalog = PresentationAssetCatalog.Load();
            Require(catalog != null, "Presentation asset catalog is missing.");
            for (int i = 0; i < BushPaths.Length; i++)
            {
                TextureImporter importer = AssetImporter.GetAtPath(BushPaths[i]) as TextureImporter;
                Require(importer != null, "Missing bush asset: " + BushPaths[i]);
                Require(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
                    && importer.textureCompression == TextureImporterCompression.Uncompressed
                    && importer.wrapMode == TextureWrapMode.Clamp
                    && Mathf.Approximately(importer.spritePixelsPerUnit, 100f),
                    "Incorrect pixel-art import settings: " + BushPaths[i]);
                Sprite[] imported = AssetDatabase.LoadAllAssetsAtPath(BushPaths[i])
                    .OfType<Sprite>().OrderBy(sprite => NumericSuffix(sprite.name)).ToArray();
                Require(imported.Length == 8 && imported.All(sprite => sprite != null),
                    $"Bushe{i + 1} must contain its verified 8-frame tag.");
                Require(catalog.BushFrames(i).Length == 8,
                    $"Catalog is missing the Bushe{i + 1} sequence.");
            }
        }

        private static void ValidateOrderingMath()
        {
            GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            if (grid == null) return;
            grid.EnsureInitialized();

            // Projected depth, rather than absolute elevation, decides foreground.
            int lowerForeground = GroundSorting.CalculateOrder(grid, 0f, 10f);
            int higherBackground = GroundSorting.CalculateOrder(grid, 2f, 15f);
            Require(lowerForeground > higherBackground,
                "A lower foreground object must be able to render over higher background terrain.");
            int lowerBackground = GroundSorting.CalculateOrder(grid, 0f, 10f);
            int higherForeground = GroundSorting.CalculateOrder(grid, 2f, 11f);
            Require(higherForeground > lowerBackground,
                "A higher object projected into the foreground must remain in front.");

            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                GridCell cell = grid.GetCell(x, y);
                if (cell == null || !cell.HeightConnection.HasValue) continue;
                Vector2Int partnerCoordinate = cell.HeightConnection.Value;
                GridCell partner = grid.GetCell(partnerCoordinate.x, partnerCoordinate.y);
                if (partner == null || cell.HeightLevel >= partner.HeightLevel) continue;
                float lowerProjectedY = GroundSorting.ProjectedGroundY(grid,
                    cell.HeightLevel, cell.WorldPosition.y);
                float upperProjectedY = GroundSorting.ProjectedGroundY(grid,
                    partner.HeightLevel, partner.WorldPosition.y);
                Require(Mathf.Approximately(lowerProjectedY, upperProjectedY),
                    $"Connector ({x},{y}) endpoints do not share projected ground depth.");
                Require(GroundSorting.CalculateOrder(grid, cell.HeightLevel, cell.WorldPosition.y)
                        == GroundSorting.CalculateOrder(grid, partner.HeightLevel,
                            partner.WorldPosition.y),
                    $"Connector ({x},{y}) endpoints do not share a sorting order.");

                Vector3 midpoint = Vector3.Lerp(cell.WorldPosition, partner.WorldPosition, 0.5f);
                float midpointElevation = GroundSorting.ProjectedElevationAt(grid, midpoint, true);
                Require(Mathf.Approximately(midpointElevation,
                        (cell.HeightLevel + partner.HeightLevel) * 0.5f),
                    $"Connector ({x},{y}) does not interpolate elevation continuously.");
            }
        }

        private static void ValidateConstants()
        {
            Require(GroundSorting.ProjectedDepthStepsPerCell == 32
                && GroundSorting.MaxAdditionalOrder < GroundSorting.ProjectedDepthStepsPerCell,
                "Projected-depth precision or fixed tie range is invalid.");
            Require(GroundSorting.TerrainBackgroundOrder(31)
                    < GroundSorting.MinimumExpectedOrder
                && GroundSorting.MaximumExpectedOrder < 0,
                "Terrain/world/Fog sorting domains overlap.");
            Require(Mathf.Approximately(BushPresentation.LargeScale, 1.50f)
                && Mathf.Approximately(BushPresentation.SmallScale, 1.25f),
                "Bush size roles changed: Bushe1/3 must be large and Bushe2/4 small.");
            Require(Mathf.Approximately(BushPresentation.FramesPerSecond, 10f),
                "Bush source timing is 100 ms per frame (10 FPS).");
            Require(Mathf.Approximately(TreePresentation.HarvestWobbleDegrees, 3.5f)
                && Mathf.Approximately(TreePresentation.HarvestWobbleDuration, 0.22f),
                "Tree harvest feedback constants changed.");
        }

        private static void ValidatePlayMode()
        {
            GroundSorting.ResetSceneCacheForTests();
            GroundSorting.EnsureSceneConfigured();
            GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            Require(grid != null && grid.NavigationData != null, "Navigation grid is unavailable.");
            FogRenderer fog = UnityEngine.Object.FindAnyObjectByType<FogRenderer>();
            SpriteRenderer fogVisual = fog != null ? fog.GetComponentInChildren<SpriteRenderer>() : null;
            Require(fogVisual != null
                && fogVisual.sortingOrder > GroundSorting.MaximumExpectedOrder,
                "Fog must render above every elevation-aware world band.");

            ProjectedTerrainForeground[] foreground = UnityEngine.Object
                .FindObjectsByType<ProjectedTerrainForeground>(FindObjectsInactive.Exclude);
            int expectedCliffs = BattlefieldTerraces.Platforms.Sum(platform => platform.Surface.width);
            Require(foreground.Count(item => item.Role == ProjectedTerrainRole.CliffFace)
                    == expectedCliffs,
                "Projected terrain is missing a southern cliff-face cell.");
            Require(foreground.Count(item => item.Role == ProjectedTerrainRole.Stair)
                    == BattlefieldTerraces.ConnectorCount * 2,
                "Projected terrain is missing an official stair half.");
            Require(foreground.All(item => item.GetComponent<SpriteRenderer>() != null
                    && item.GetComponent<SpriteRenderer>().sortingOrder == item.CalculatedOrder),
                "A projected terrain foreground sprite has stale sorting data.");

            foreach (UnitController unit in UnitController.AllUnits.Where(unit => unit != null))
            {
                ElevationSortingGroup sorter = unit.GetComponent<ElevationSortingGroup>();
                Require(sorter != null && sorter.TrackMovement && sorter.Group != null,
                    unit.name + " is missing dynamic elevation sorting.");
                Require(sorter.Group.sortingOrder == sorter.CalculatedOrder,
                    unit.name + " sorting order is stale.");
            }
            foreach (ResourceNode node in ResourceNode.ActiveNodes.Where(node => node != null))
            {
                ElevationSortingGroup sorter = node.GetComponent<ElevationSortingGroup>();
                Require(sorter != null && sorter.Group != null,
                    node.name + " is missing elevation sorting.");
                Require(sorter.TrackMovement == (node.ResourceType == ResourceType.Food),
                    node.name + " dynamic sorting policy is incorrect.");
                Require(!float.IsNaN(sorter.GroundAnchorYOffset)
                    && !float.IsInfinity(sorter.GroundAnchorYOffset),
                    node.name + " has an invalid ground anchor.");
                if (node.ResourceType == ResourceType.Wood)
                {
                    Require(node.GetComponent<TreePresentation>() != null,
                        node.name + " is missing Tree chop feedback.");
                    Require(node.PresentationRenderer != null
                        && node.PresentationRenderer.transform != node.transform,
                        node.name + " wobble must target a visual child, not the gameplay root.");
                }
            }
            foreach (BuildingSite building in UnityEngine.Object.FindObjectsByType<BuildingSite>(
                FindObjectsInactive.Exclude))
            {
                ElevationSortingGroup sorter = building.GetComponent<ElevationSortingGroup>();
                Require(sorter != null && sorter.Group.sortingOrder == sorter.CalculatedOrder,
                    building.name + " is missing or has stale elevation sorting.");
            }
            foreach (CastleDropOff castle in CastleDropOff.Active.Where(castle => castle != null))
            {
                ElevationSortingGroup sorter = castle.GetComponent<ElevationSortingGroup>();
                Require(sorter != null && sorter.Group.sortingOrder == sorter.CalculatedOrder,
                    castle.name + " is missing or has stale elevation sorting.");
            }
            foreach (GoldMineObjective mine in GoldMineObjective.Active.Where(mine => mine != null))
            {
                ElevationSortingGroup sorter = mine.GetComponent<ElevationSortingGroup>();
                Require(sorter != null && sorter.Group.sortingOrder == sorter.CalculatedOrder,
                    mine.name + " is missing or has stale elevation sorting.");
            }

            Transform visualGrid = GameObject.Find("Map")?.transform.Find("Grid");
            for (int level = 1; level <= BattlefieldTerraces.MaxHeight; level++)
            {
                Require(Order(visualGrid, "Shadow_L" + level)
                        == GroundSorting.ElevatedTerrainBackgroundOrder(level, false)
                    && Order(visualGrid, "ElevatedGround_L" + level)
                        == GroundSorting.ElevatedTerrainBackgroundOrder(level, true),
                    "Runtime tilemap elevation order is incorrect for level " + level + ".");
                Require(visualGrid.Find("Shadow_L" + level).GetComponent<TilemapRenderer>().mode
                            == TilemapRenderer.Mode.Chunk
                        && visualGrid.Find("ElevatedGround_L" + level)
                            .GetComponent<TilemapRenderer>().mode == TilemapRenderer.Mode.Chunk,
                    "Terrain Tilemaps must remain in Chunk mode.");
            }

            BushPresentation[] bushes = UnityEngine.Object.FindObjectsByType<BushPresentation>(
                FindObjectsInactive.Exclude);
            Require(bushes.Length > 0, "No animated bushes were configured in the active map.");
            Require(bushes.All(bush => bush.FrameCount == 8 && bush.Visual != null),
                "A bush is missing its full verified animation.");
            Require(bushes.All(bush => bush.Visual.sortingOrder == GroundSorting.CalculateOrder(
                    grid, GroundSorting.ElevationAt(grid, bush.transform.position),
                    bush.transform.position.y)),
                "A bush is not sorted from its stable ground root.");
            Require(bushes.Where(bush => bush.IsLarge).All(bush =>
                    Mathf.Approximately(bush.AppliedScale, BushPresentation.LargeScale))
                && bushes.Where(bush => !bush.IsLarge).All(bush =>
                    Mathf.Approximately(bush.AppliedScale, BushPresentation.SmallScale)),
                "Bush size role mismatch.");
            if (bushes.Length > 1)
                Require(bushes.Select(bush => bush.PhaseSeconds).Distinct().Count() > 1,
                    "Bushes must not all use the same animation phase.");
        }

        private static int Order(Transform root, string name)
            => root?.Find(name)?.GetComponent<TilemapRenderer>()?.sortingOrder ?? int.MinValue;

        private static int NumericSuffix(string name)
        {
            int split = name.LastIndexOf('_');
            return split >= 0 && int.TryParse(name[(split + 1)..], out int value)
                ? value : int.MaxValue;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
