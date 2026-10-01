using System;
using TinyKingdom.Terrain;
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
            Require(EditorSceneManager.GetActiveScene().path == "Assets/Scenes/Map.unity", "Open the Map scene first.");
            Transform root = GameObject.Find("Map")?.transform.Find("Grid");
            Require(root != null, "Visual Grid is missing.");
            GetLayer(root, "BG_Color", 0);
            Tilemap foam = GetLayer(root, "WaterFoam", 1);
            Tilemap flat = GetLayer(root, "FlatGround", 2);
            int foamCount = 0;
            for (int y = 0; y < BattlefieldLayout.Height; y++)
            for (int x = 0; x < BattlefieldLayout.Width; x++)
            {
                var p = new Vector3Int(x,y,0);
                Require(flat.HasTile(p) == BattlefieldLayout.IsLand(x,y), "Incorrect island or pond ground.");
                Require(foam.HasTile(p) == BattlefieldLayout.IsShoreline(x,y), "Incorrect shoreline foam.");
                if (foam.HasTile(p))
                {
                    Require(foam.GetTile(p) is AnimatedFoamTile, "Foam must use the verified animation.");
                    foamCount++;
                }
            }
            for (int level = 1; level <= BattlefieldTerraces.MaxHeight; level++)
            {
                Tilemap shadow = GetLayer(root, "Shadow_L"+level, 2+2*level);
                Tilemap surface = GetLayer(root, "ElevatedGround_L"+level, 3+2*level);
                Require(shadow.GetUsedTilesCount() > 0 && surface.GetUsedTilesCount() > 0, "Empty terrace layer.");
                Require(shadow.GetComponent<TilemapCollider2D>() == null
                    && surface.GetComponent<TilemapCollider2D>() == null, "Visual layers must not control navigation.");
            }
            Require(foam.GetComponent<TilemapCollider2D>() == null, "Foam must not create collision.");
            BattlefieldMapValidation.ValidateTerraces();
            Require(root.Find("ElevatedGround_L3")==null,"Unused third elevation layer must be removed.");
            Debug.Log($"Terrain visuals passed: {foamCount} shoreline cells, flat central mine, two-tier Castle bases and two ascent routes per faction.");
        }

        private static Tilemap GetLayer(Transform root, string name, int order)
        {
            Transform child = root.Find(name);
            Require(child != null, "Missing layer: "+name);
            var tilemap = child.GetComponent<Tilemap>();
            var renderer = child.GetComponent<TilemapRenderer>();
            Require(tilemap != null && renderer != null && renderer.sortingOrder == order, "Incorrect layer: "+name);
            return tilemap;
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
