using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Presentation;
using TinyKingdom.Terrain;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TinyKingdom.UI
{
    // Scenery on the instantiated menu copy only. Never writes to map/navigation assets.
    public sealed class MainMenuVillage
    {
        private readonly Transform world;
        private readonly MainMenuAssetCatalog art;
        private readonly Func<Vector3, int> orderAt;
        private readonly List<Rect> clearings = new();
        private Transform village;

        public MainMenuVillage(Transform world, MainMenuAssetCatalog art, Func<Vector3, int> orderAt)
        { this.world = world; this.art = art; this.orderAt = orderAt; }

        public void Build()
        {
            if (art.villageBuildings == null || art.villageBuildings.Length != 7) return;
            village = new GameObject("Menu Riverside Village").transform;
            village.SetParent(world, false);
            PaintRiver();
            // Door anchors, rather than sprite centres, define placement and depth.
            Building(0, new(6.6f, 10f));
            Building(1, new(19.1f, 10f));
            Building(2, new(6.6f, 15.4f));
            Building(0, new(19.3f, 15.7f));
            Building(3, new(9.9f, 9f));
            Building(4, new(16.5f, 9f));
            Building(5, new(19.4f, 21.2f));
            Building(6, new(6.3f, 18.5f));
            Building(6, new(19.6f, 18.7f));
            Building(1, new(7f, 21.4f));
            Building(2, new(11f, 21.4f));
            ClearOriginalProps();
            PlantGroves();
            AddRiverDetails();
        }

        private static bool IsRiver(int x, int y)
        {
            if (x < 0 || x > 36 || y < 2 || y > 32) return false;
            int centre = x < 7 ? 26 : x < 14 ? 25 : 24;
            bool northBranch = x <= 26 && y >= centre - 1 && y <= centre + 1;
            int bend = y < 10 ? 25 : y < 18 ? 24 : 23;
            return northBranch || (y <= 24 && x >= bend - 1 && x <= bend + 1);
        }

        private static bool IsLand(int x, int y)
            => BattlefieldLayout.IsLand(x, y) && !IsRiver(x, y);

        private void PaintRiver()
        {
            Transform grid = world.Find("Grid");
            Tilemap ground = grid?.Find("FlatGround")?.GetComponent<Tilemap>();
            Tilemap meadows = grid?.Find("MeadowsAndPaths")?.GetComponent<Tilemap>();
            Tilemap foam = grid?.Find("WaterFoam")?.GetComponent<Tilemap>();
            if (ground == null || art.villageShore?.Length != 9) return;
            for (int y = 2; y < 32; y++)
            for (int x = 0; x < 37; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (IsRiver(x, y))
                {
                    ground.SetTile(cell, null);
                    meadows?.SetTile(cell, null);
                    foam?.SetTile(cell, null);
                }
                else if (IsLand(x, y) && (IsRiver(x - 1, y) || IsRiver(x + 1, y)
                    || IsRiver(x, y - 1) || IsRiver(x, y + 1)))
                {
                    int column = !IsLand(x - 1, y) ? 0 : !IsLand(x + 1, y) ? 2 : 1;
                    int row = !IsLand(x, y + 1) ? 0 : !IsLand(x, y - 1) ? 2 : 1;
                    ground.SetTile(cell, art.villageShore[row * 3 + column]);
                    meadows?.SetTile(cell, null);
                    foam?.SetTile(cell, art.villageFoam);
                }
            }
            if (art.villageBridges?.Length == 2)
            {
                SpriteRenderer bridge = Prop("Northern Footbridge", art.villageBridges[1], new(9.5f, 25.5f), 1.65f, false);
                bridge.sortingOrder = GroundSorting.TerrainBackgroundOrder(5);
                bridge = Prop("Eastern Footbridge", art.villageBridges[0], new(24f, 17.5f), 1.65f, false);
                bridge.sortingOrder = GroundSorting.TerrainBackgroundOrder(5);
            }
        }

        private void Building(int index, Vector2 door)
        {
            Sprite sprite = art.villageBuildings[index];
            Prop("Village " + sprite.name, sprite, door, BuildingVisualPresentation.NormalBuildingScale);
            float halfWidth = sprite.bounds.size.x * BuildingVisualPresentation.NormalBuildingScale * .5f;
            clearings.Add(new Rect(door.x - halfWidth - .35f, door.y - .6f,
                halfWidth * 2f + .7f, 2.8f));
        }

        private void ClearOriginalProps()
        {
            foreach (SpriteRenderer prop in world.GetComponentsInChildren<SpriteRenderer>())
            {
                if (prop.transform.IsChildOf(village) || prop.name.EndsWith("_Main_Castle")
                    || prop.GetComponent<AmbientCloudDrift>() != null) continue;
                Vector2 position = prop.transform.position;
                int x = Mathf.FloorToInt(position.x), y = Mathf.FloorToInt(position.y);
                bool remove = IsRiver(x, y) || IsRiver(x - 1, y) || IsRiver(x + 1, y)
                    || IsRiver(x, y - 1) || IsRiver(x, y + 1);
                foreach (Rect clearing in clearings) remove |= clearing.Contains(position);
                if (remove) prop.gameObject.SetActive(false);
            }
        }

        private void PlantGroves()
        {
            // Each grove has its own native grass patch, and irregular tree spacing.
            Grove(new RectInt(3, 17, 4, 5), new[] { new Vector2(3.6f, 18.3f), new(4.2f, 20.5f), new(5.1f, 21.3f) });
            Grove(new RectInt(8, 17, 3, 4), new[] { new Vector2(8.7f, 19.1f), new(9.5f, 20.2f) });
            Grove(new RectInt(15, 17, 3, 4), new[] { new Vector2(16.4f, 19.8f), new(17.1f, 18.8f) });
            Grove(new RectInt(2, 8, 3, 5), new[] { new Vector2(2.8f, 9.3f), new(3.7f, 11.1f), new(3.1f, 12.3f) });
            Grove(new RectInt(8, 27, 7, 4), new[] { new Vector2(8.6f, 28.1f), new(10.1f, 29.5f), new(12.4f, 28.7f), new(13.8f, 29.6f) });
            Grove(new RectInt(20, 8, 3, 6), new[] { new Vector2(21.7f, 9.3f), new(21.5f, 12.5f) });
            Vector2[] shrubs = { new(5.2f, 9f), new(7.6f, 12.8f), new(18.6f, 12.5f), new(20.4f, 14.2f),
                new(9.3f, 14.5f), new(16.8f, 14.6f), new(4.6f, 22.8f), new(13.3f, 22f), new(15.7f, 21.8f),
                new(21.1f, 21.3f), new(7.3f, 27.5f), new(16.2f, 27.4f), new(3.8f, 7.8f), new(20.6f, 7.6f) };
            for (int i = 0; i < shrubs.Length; i++)
                Loop("Village Bush", art.villageBushFrames, i % 4 * 8, 8, shrubs[i], i % 2 == 0 ? 1.5f : 1.25f, 128, i * .113f);
        }

        private void Grove(RectInt patch, Vector2[] trees)
        {
            if (art.villageMeadow?.Length != 9) return;
            Transform grid = world.Find("Grid");
            // One overlay per tier keeps grass above its matching surface, below entities.
            int elevation = patch.yMin >= 14 && patch.xMin >= 8 && patch.xMax <= 18 ? 2
                : patch.xMin >= 5 && patch.xMax <= 21 && patch.yMin >= 8 && patch.yMax <= 21 ? 1 : 0;
            string layerName = "Menu Grove Grass L" + elevation;
            Transform existing = grid.Find(layerName);
            if (existing == null)
            {
                existing = new GameObject(layerName, typeof(Tilemap), typeof(TilemapRenderer)).transform;
                existing.SetParent(grid, false);
                var renderer = existing.GetComponent<TilemapRenderer>();
                renderer.sortingOrder = GroundSorting.TerrainBackgroundOrder(elevation == 0 ? 4 : elevation == 1 ? 12 : 14);
                renderer.sharedMaterial = grid.Find("FlatGround").GetComponent<TilemapRenderer>().sharedMaterial;
            }
            var layer = existing.GetComponent<Tilemap>();
            for (int y = patch.yMin; y < patch.yMax; y++)
            for (int x = patch.xMin; x < patch.xMax; x++)
            {
                if (!IsLand(x, y)) continue;
                int col = x == patch.xMin ? 0 : x == patch.xMax - 1 ? 2 : 1;
                int row = y == patch.yMax - 1 ? 0 : y == patch.yMin ? 2 : 1;
                layer.SetTile(new Vector3Int(x, y, 0), art.villageMeadow[row * 3 + col]);
            }
            for (int i = 0; i < trees.Length; i++)
                Loop("Village Tree", art.villageTreeFrames, i % 4 * 8, 8, trees[i], 1.6f, 192, i * .171f);
        }

        private void AddRiverDetails()
        {
            Vector2[] ducks = { new(4.5f, 26f), new(12.3f, 25.3f), new(16.8f, 24.5f), new(23.7f, 20.2f), new(24.4f, 12.1f) };
            for (int i = 0; i < ducks.Length; i++)
                Loop("River Duck", art.villageDuckFrames, 0, 3, ducks[i], 1.6f, 32, i * .173f, false);
            Vector2[] stones = { new(6.3f, 26.7f), new(15.3f, 23.6f), new(22.7f, 22.3f), new(25.1f, 14.3f) };
            for (int i = 0; i < stones.Length; i++)
                Loop("River Ripples", art.villageWaterFrames, 0, 16, stones[i], 1.1f, 64, i * .291f, false);
        }

        private void Loop(string name, Sprite[] source, int first, int count, Vector2 anchor,
            float scale, int cellWidth, float phase, bool bottom = true)
        {
            if (source == null || source.Length < first + count) return;
            Sprite[] frames = new Sprite[count];
            Array.Copy(source, first, frames, 0, count);
            SpriteRenderer renderer = Prop(name, frames[0], anchor, scale, false);
            // Keep bottom anchoring on a fixed root; cropped frame pivots may vary.
            renderer.gameObject.AddComponent<AmbientSpriteLoop>().Configure(frames, phase, 10f, bottom, scale, cellWidth);
            if (!bottom) renderer.sortingOrder = GroundSorting.TerrainBackgroundOrder(6);
        }

        private SpriteRenderer Prop(string name, Sprite sprite, Vector2 position, float scale, bool bottom = true)
        {
            var root = new GameObject(name);
            root.transform.SetParent(village, false);
            root.transform.position = position;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(scale, scale, 1);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = world.Find("Grid/FlatGround").GetComponent<TilemapRenderer>().sharedMaterial;
            renderer.sortingOrder = orderAt(position);
            if (bottom) visual.transform.localPosition = new Vector3(-sprite.bounds.center.x * scale, -sprite.bounds.min.y * scale);
            else visual.transform.localPosition = -sprite.bounds.center * scale;
            return renderer;
        }
    }
}
