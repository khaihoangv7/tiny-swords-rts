using TinyKingdom.Navigation;
using TinyKingdom.Buildings;
using TinyKingdom.Presentation;
using TinyKingdom.Terrain;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace TinyKingdom.UI
{
    // Uses authored visuals and read-only height data, with no running match.
    public sealed class MainMenuBackdrop : MonoBehaviour
    {
        private static readonly Vector3 CameraAnchor = new(19f, 17.4f, -10f);
        private Camera menuCamera;
        private MainMenuAssetCatalog assets;
        private Transform world;
        private float began;

        public void Configure(MainMenuAssetCatalog catalog)
        {
            assets = catalog;
            if (catalog.backdropPrefab == null)
            {
                Debug.LogError("Main menu backdrop missing. Run Prepare Main Menu.");
                return;
            }
            world = Instantiate(catalog.backdropPrefab, transform).transform;
            world.name = "Live Kingdom";
            menuCamera = Camera.main;
            if (menuCamera != null)
            {
                menuCamera.orthographic = true;
                menuCamera.transform.SetPositionAndRotation(CameraAnchor, Quaternion.identity);
                menuCamera.orthographicSize = 11.5f;
                menuCamera.clearFlags = CameraClearFlags.SolidColor;
                menuCamera.backgroundColor = new Color(0.20f, 0.47f, 0.48f);
            }
            // The isolated menu scene needs the same 2D illumination as the map.
            if (FindAnyObjectByType<Light2D>() == null)
            {
                var lightObject = new GameObject("Menu Global Light");
                lightObject.transform.SetParent(transform, false);
                Light2D light = lightObject.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Global;
                light.intensity = 1f;
            }
            ConfigureDepth();
            ConfigureCastles();
            new MainMenuVillage(world, assets, position => OrderAt(position)).Build();
            AddForeground();
            AddGuards();
            AddClouds();
            began = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            if (menuCamera == null) return;
            float time = Time.unscaledTime - began;
            menuCamera.transform.position = CameraAnchor + new Vector3(
                Mathf.Sin(time * 0.075f) * 0.22f, Mathf.Sin(time * 0.055f) * 0.08f, 0f);
        }

        private void ConfigureDepth()
        {
            foreach (TilemapRenderer renderer in world.GetComponentsInChildren<TilemapRenderer>())
            {
                int order = renderer.name switch
                {
                    "BG_Color" => 0,
                    "WaterFoam" => 1,
                    "FlatGround" => 2,
                    "MeadowsAndPaths" => 3,
                    "Decorations" => 4,
                    "Shadow_L1" => 10,
                    "ElevatedGround_L1" => 11,
                    "Shadow_L2" => 12,
                    "ElevatedGround_L2" => 13,
                    _ => 2
                };
                renderer.sortingOrder = GroundSorting.TerrainBackgroundOrder(order);
                renderer.mode = TilemapRenderer.Mode.Chunk;
            }
            foreach (SpriteRenderer renderer in world.GetComponentsInChildren<SpriteRenderer>())
                renderer.sortingOrder = renderer.GetComponent<AmbientCloudDrift>() != null
                    ? 19 : OrderAt(renderer.transform.position);
        }

        private void ConfigureCastles()
        {
            foreach (SpriteRenderer renderer in world.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!renderer.name.EndsWith("_Main_Castle")) continue;
                float ground = renderer.bounds.min.y;
                float scale = BuildingVisualPresentation.NormalBuildingScale;
                renderer.transform.localScale = new Vector3(scale, scale, 1f);
                renderer.transform.position += Vector3.up * (ground - renderer.bounds.min.y);
                // Keep the already-resolved authored ground depth after enlarging the artwork.
            }
        }

        private int OrderAt(Vector3 ground, int extra = 1, int? elevation = null)
        {
            NavigationGridData terrain = assets.backdropTerrain;
            if (terrain == null) return GroundSorting.WorldOrder;
            float size = Mathf.Max(0.01f, terrain.CellSize);
            Vector2 local = (Vector2)ground - terrain.WorldOrigin;
            terrain.TryGetCellData(Mathf.FloorToInt(local.x / size),
                Mathf.FloorToInt(local.y / size), out NavigationCellData cell);
            float projectedY = ground.y - (elevation ?? cell.heightLevel) * size;
            int depth = Mathf.Clamp(Mathf.RoundToInt((terrain.WorldOrigin.y + terrain.Height * size
                - projectedY) / size * GroundSorting.ProjectedDepthStepsPerCell),
                0, GroundSorting.MaxProjectedDepthOffset);
            return GroundSorting.ProjectedWorldBaseOrder + depth + extra;
        }

        private void AddForeground()
        {
            // Reuse the map's verified cliff/stair cells and projected-depth rules.
            Transform grid = world.Find("Grid");
            if (grid == null) return;
            var foreground = new GameObject("Menu Terrain Foreground");
            foreground.transform.SetParent(world, false);
            foreach (TerraceDefinition terrace in BattlefieldTerraces.Platforms)
            {
                Tilemap source = grid.Find("ElevatedGround_L" + terrace.Height)?.GetComponent<Tilemap>();
                for (int x = terrace.Surface.xMin; x < terrace.Surface.xMax; x++)
                    Foreground(source, new Vector3Int(x, terrace.Surface.yMin - 1, 0),
                        terrace.Height - 1, GroundSorting.CliffFaceAdditionalOrder, foreground.transform);
            }
            for (int i = 0; i < BattlefieldTerraces.ConnectorCount; i++)
            {
                TerrainHeightConnectorDefinition stair = BattlefieldTerraces.Connector(i);
                Tilemap source = grid.Find("ElevatedGround_L" + (stair.LowerHeight + 1))?.GetComponent<Tilemap>();
                Foreground(source, new Vector3Int(stair.VisualX, stair.LowerY, 0), stair.LowerHeight, 0, foreground.transform);
                Foreground(source, new Vector3Int(stair.VisualX, stair.UpperY, 0), stair.LowerHeight + 1, 0, foreground.transform);
            }
        }

        private void Foreground(Tilemap source, Vector3Int cell, int height, int extra, Transform parent)
        {
            if (source == null || source.GetSprite(cell) == null) return;
            var part = new GameObject("Terrain " + cell);
            part.transform.SetParent(parent, false);
            part.transform.position = source.GetCellCenterWorld(cell);
            part.transform.localScale = source.transform.lossyScale;
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = source.GetSprite(cell);
            renderer.color = source.GetColor(cell);
            TilemapRenderer original = source.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = original.sharedMaterial;
            renderer.sortingLayerID = original.sortingLayerID;
            renderer.sortingOrder = OrderAt(part.transform.position, extra, height);
        }

        private void AddGuards()
        {
            if (assets.idleGuards == null || assets.idleGuards.Length < 3) return;
            // Stationary guards occupy clear court/landing ground, not stairs.
            Vector2[] positions =
            {
                new(10.5f, 17.3f), new(15.5f, 17.3f),
                new(11.5f, 14.8f), new(14.5f, 14.8f),
                new(11.5f, 12.8f), new(14.5f, 12.8f),
                new(12f, 11.1f), new(14f, 11.1f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                var actor = new GameObject("Menu Guard " + i);
                actor.transform.SetParent(world, false);
                actor.transform.position = positions[i];
                actor.AddComponent<MenuBackdropActor>().Configure(assets.idleGuards[i % 3],
                    OrderAt(actor.transform.position), i * 0.137f, (i & 1) != 0);
            }
        }

        private void AddClouds()
        {
            AmbientCloudDrift template = world.GetComponentInChildren<AmbientCloudDrift>();
            if (template == null) return;
            for (int i = 0; i < 2; i++)
            {
                AmbientCloudDrift cloud = Instantiate(template, world);
                cloud.name = "Menu Drifting Cloud " + i;
                cloud.transform.position = i == 0 ? new Vector3(6f, 24f) : new Vector3(23f, 9f);
                cloud.transform.localScale *= 0.85f;
                SpriteRenderer renderer = cloud.GetComponent<SpriteRenderer>();
                renderer.color = new Color(1f, 1f, 1f, 0.43f);
                renderer.sortingOrder = 19;
                cloud.Configure(new Rect(-15f, -10f, 90f, 70f), new Vector2(5f, 0.45f),
                    52f + i * 17f, i * 2.4f);
            }
            AddCanopyClouds(template);
        }

        private void AddCanopyClouds(AmbientCloudDrift fallback)
        {
            if (menuCamera == null) return;
            AmbientCloudDrift template = fallback;
            foreach (AmbientCloudDrift candidate in world.GetComponentsInChildren<AmbientCloudDrift>())
            {
                Sprite sprite = candidate.GetComponent<SpriteRenderer>().sprite;
                if (sprite != null && sprite.texture.name == "Clouds_02")
                { template = candidate; break; }
            }

            // Stagger large and small whole clouds over the upper-right backdrop.
            // Widths account for the transparent margins of the inspected Clouds_02.
            Vector3[] placements =
            {
                new(.44f, .96f, .26f), new(.58f, .91f, .30f),
                new(.73f, .98f, .28f), new(.88f, .94f, .32f), new(1.02f, .99f, .26f),
                new(.45f, .82f, .18f), new(.60f, .80f, .22f),
                new(.75f, .84f, .17f), new(.89f, .82f, .20f), new(1.02f, .86f, .18f)
            };
            float viewWidth = menuCamera.orthographicSize * 2f * menuCamera.aspect;
            for (int i = 0; i < placements.Length; i++)
            {
                AmbientCloudDrift cloud = Instantiate(template, world);
                cloud.name = "Menu Canopy Cloud " + i;
                SpriteRenderer renderer = cloud.GetComponent<SpriteRenderer>();
                float scale = viewWidth * placements[i].z
                    / Mathf.Max(.01f, renderer.sprite.bounds.size.x * .54f);
                cloud.transform.localScale = new Vector3(scale, scale, 1f);
                Vector3 position = menuCamera.ViewportToWorldPoint(
                    new Vector3(placements[i].x, placements[i].y, -menuCamera.transform.position.z));
                cloud.transform.position = new Vector3(position.x, position.y, 0f);
                renderer.color = new Color(1f, 1f, 1f, i < 5 ? .88f : .78f);
                renderer.flipX = (i & 1) != 0;
                renderer.sortingOrder = 20;
                cloud.Configure(new Rect(-100f, -100f, 240f, 240f),
                    new Vector2(.55f, .12f), 65f + i * 9f, i * .83f);
            }
            AddSideClouds(template);
        }

        private void AddSideClouds(AmbientCloudDrift template)
        {
            // Keep the central Castle and courtyard visible between faint edge clouds.
            Vector3[] placements =
            {
                new(5f, 17.6f, 1.45f),
                new(21.1f, 15.9f, .85f),
                new(6.3f, 10.7f, 1.1f)
            };
            for (int i = 0; i < placements.Length; i++)
            {
                AmbientCloudDrift cloud = Instantiate(template, world);
                cloud.name = "Menu Faint Side Cloud " + i;
                Vector3 placement = placements[i];
                cloud.transform.position = new Vector3(placement.x, placement.y, 0f);
                cloud.transform.localScale = new Vector3(placement.z, placement.z, 1f);
                SpriteRenderer renderer = cloud.GetComponent<SpriteRenderer>();
                renderer.color = new Color(1f, 1f, 1f, i == 1 ? .26f : .32f);
                renderer.flipX = (i & 1) != 0;
                renderer.sortingOrder = 19;
                cloud.Configure(new Rect(-15f, -10f, 90f, 70f),
                    new Vector2(.9f, .18f), 78f + i * 13f, i * 1.6f);
            }
        }
    }
}
