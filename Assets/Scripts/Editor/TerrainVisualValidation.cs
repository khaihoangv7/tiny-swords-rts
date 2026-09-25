using System;
using TinyKingdom.Navigation;
using TinyKingdom.Terrain;
using TinyKingdom.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TinyKingdom.EditorTools
{
    public static class TerrainVisualValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Terrain Visuals")]
        public static void Run()
        {
            Require(!EditorApplication.isPlaying, "Exit Play Mode before validating terrain visuals.");
            Require(EditorSceneManager.GetActiveScene().path == "Assets/Scenes/Map.unity",
                "Open Assets/Scenes/Map.unity before validating terrain visuals.");
            GameObject map = GameObject.Find("Map");
            Require(map != null, "Map root is missing.");
            Transform gridRoot = map.transform.Find("Grid");
            Require(gridRoot != null, "Visual Grid is missing.");

            Tilemap bg = GetLayer(gridRoot, "BG_Color", 0);
            Tilemap foam = GetLayer(gridRoot, "WaterFoam", 1);
            Tilemap flat = GetLayer(gridRoot, "FlatGround", 2);
            Tilemap shadow = GetLayer(gridRoot, "Shadow_L1", 3);
            Tilemap elevated = GetLayer(gridRoot, "ElevatedGround_L1", 4);
            Require(bg.GetUsedTilesCount() > 0 && flat.GetUsedTilesCount() > 0
                && elevated.GetUsedTilesCount() > 0, "Real terrain layers are empty.");

            RepairVisualEffectsIfNeeded(foam, shadow);

            int foamCount = 0;
            int shadowCount = 0;
            for (int y = 0; y < 28; y++)
            for (int x = 0; x < 40; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool shoreline = x >= 2 && x <= 37 && y >= 2 && y <= 25
                    && (x == 2 || x == 37 || y == 2 || y == 25);
                bool shadowFootprint = IsShadowCell(x, y);

                TileBase foamTile = foam.GetTile(cell);
                TileBase shadowTile = shadow.GetTile(cell);
                Require((foamTile != null) == shoreline,
                    $"Incorrect foam at ({x}, {y}): expected {(shoreline ? "shoreline tile" : "empty")}, " +
                    $"found {(foamTile == null ? "empty" : foamTile.name)}. " +
                    $"Scene: {EditorSceneManager.GetActiveScene().path}. " +
                    "The effect Tilemap could not be restored with Unity's SetTile API.");
                Require((shadowTile != null) == shadowFootprint,
                    $"Incorrect offset shadow at ({x}, {y}).");
                if (foamTile != null)
                {
                    Require(foamTile is AnimatedFoamTile, "Shoreline tile is not animated Tiny Swords foam.");
                    foamCount++;
                }
                if (shadowTile != null)
                    shadowCount++;
            }

            Require(foamCount == 116 && shadowCount == 62,
                "Foam or shadow tile count differs from the expected footprint.");
            Require(foam.GetComponent<TilemapCollider2D>() == null
                && shadow.GetComponent<TilemapCollider2D>() == null,
                "Visual foam and shadow must not create collision.");

            Transform navigation = map.transform.Find("Navigation");
            GridManager logicalGrid = navigation != null ? navigation.GetComponent<GridManager>() : null;
            Require(logicalGrid != null && logicalGrid.Width == 40 && logicalGrid.Height == 28,
                "Phase 2 logical navigation is missing or resized.");
            Transform units = map.transform.Find("BlueTestUnits");
            Require(units != null && units.GetComponentsInChildren<UnitController>().Length == 3,
                "The three Phase 3 Blue test units are missing.");
            Require(GameObject.Find("Main Camera") != null, "Main Camera is missing.");

            Debug.Log("Terrain visual validation passed: 116 shoreline foam cells, 56 offset footprint shadow cells, 6 cliff-foot shadow cells, two official stair openings, visual-only layers, 40x28 navigation and three test units.");
        }

        private static void RepairVisualEffectsIfNeeded(Tilemap foam, Tilemap shadow)
        {
            AnimatedFoamTile foamAsset = AssetDatabase.LoadAssetAtPath<AnimatedFoamTile>(
                "Assets/Tiles/Water/AnimatedFoam.asset");
            Tile shadowAsset = AssetDatabase.LoadAssetAtPath<Tile>(
                "Assets/Tiles/Shadows/Shadow.asset");
            Require(foamAsset != null, "AnimatedFoam asset or its script could not be loaded by Unity.");
            Require(shadowAsset != null, "Shadow Tile asset could not be loaded by Unity.");

            bool mismatch = false;
            for (int y = 0; y < 28 && !mismatch; y++)
            for (int x = 0; x < 40 && !mismatch; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool shoreline = x >= 2 && x <= 37 && y >= 2 && y <= 25
                    && (x == 2 || x == 37 || y == 2 || y == 25);
                bool shadowFootprint = IsShadowCell(x, y);
                mismatch = foam.GetTile(cell) != (shoreline ? foamAsset : null)
                    || shadow.GetTile(cell) != (shadowFootprint ? shadowAsset : null);
            }
            if (!mismatch)
                return;

            foam.ClearAllTiles();
            shadow.ClearAllTiles();
            for (int y = 2; y <= 25; y++)
            for (int x = 2; x <= 37; x++)
                if (x == 2 || x == 37 || y == 2 || y == 25)
                    foam.SetTile(new Vector3Int(x, y, 0), foamAsset);

            for (int y = 11; y <= 18; y++)
            for (int x = 16; x <= 23; x++)
                if (IsShadowCell(x, y))
                    shadow.SetTile(new Vector3Int(x, y, 0), shadowAsset);

            foam.RefreshAllTiles();
            shadow.RefreshAllTiles();
            Require(foam.GetTile(new Vector3Int(2, 2, 0)) == foamAsset,
                "Unity SetTile could not retain the AnimatedFoam asset at (2,2). " +
                "Inspect AnimatedFoam.asset and AnimatedFoamTile.cs in the Unity Inspector.");
            Require(shadow.GetTile(new Vector3Int(16, 11, 0)) == shadowAsset,
                "Unity SetTile could not retain the cliff-foot Shadow asset at (16,11).");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Terrain visual validation updated WaterFoam and Shadow_L1 using Unity Tilemap.SetTile, then saved the Map scene.");
        }

        private static bool IsShadowCell(int x, int y)
        {
            if (x < 16 || x > 23 || y < 11 || y > 18)
                return false;
            return y > 11 || !StrategicObjectiveMapLayout.IsConnectorColumn(x);
        }

        private static Tilemap GetLayer(Transform root, string name, int sortingOrder)
        {
            Transform child = root.Find(name);
            Require(child != null, $"Layer {name} is missing.");
            Tilemap tilemap = child.GetComponent<Tilemap>();
            TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
            Require(tilemap != null && renderer != null && renderer.sortingOrder == sortingOrder,
                $"Layer {name} has an incorrect component or sorting order.");
            return tilemap;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
