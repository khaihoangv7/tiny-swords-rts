using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Terrain;
using TinyKingdom.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TinyKingdom.EditorTools
{
    public static class RiverMountainsMapBuilder
    {
        private const string OriginalScene = "Assets/Scenes/Map.unity";
        private const string ScenePath = "Assets/Scenes/LinearLane.unity";
        private const string MenuPath = "Assets/Scenes/MapSelection.unity";
        private const string LayoutPath = "Assets/Data/LinearLaneLayout.asset";
        private const string NavigationPath = "Assets/Data/Navigation/LinearLaneNavigation.asset";

        [MenuItem("Tools/Tiny Kingdom/Build Map 2 - Straight Lane")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building the second map.");

            MigrateAsset("Assets/Scenes/RiverMountains.unity", ScenePath);
            MigrateAsset("Assets/Data/RiverMountainsLayout.asset", LayoutPath);
            MigrateAsset("Assets/Data/Navigation/RiverMountainsNavigation.asset", NavigationPath);

            BattlefieldLayout source = AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(
                BattlefieldMapBuilder.LayoutPath);
            if (source == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(OriginalScene) == null)
                throw new InvalidOperationException("Build the original Two Kingdoms map first.");

            BattlefieldLayout layout = BuildLayout(source);
            NavigationGridData navigation = BuildNavigation();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null
                && !AssetDatabase.CopyAsset(OriginalScene, ScenePath))
                throw new InvalidOperationException("Could not copy the original map scene.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // CopyAsset/OpenScene can refresh the AssetDatabase and invalidate
            // the temporary managed handles returned while creating assets.
            layout = AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(LayoutPath);
            navigation = AssetDatabase.LoadAssetAtPath<NavigationGridData>(NavigationPath);
            if (layout == null || navigation == null)
                throw new InvalidOperationException("The second map assets did not import.");
            GameObject map = GameObject.Find("Map");
            if (map == null) throw new InvalidOperationException("The map root is missing.");
            Transform grid = map.transform.Find("Grid");
            if (grid == null) throw new InvalidOperationException("The visual grid is missing.");

            PaintTerrain(grid, layout);
            Decorate(map.transform, layout);
            BuildPreview(map.transform, layout);

            GridManager gridManager = map.GetComponentInChildren<GridManager>(true);
            if (gridManager == null) throw new InvalidOperationException("GridManager is missing.");
            gridManager.SetNavigationData(navigation);
            EditorUtility.SetDirty(gridManager);

            EconomyTestSetup economy = Object.FindAnyObjectByType<EconomyTestSetup>();
            if (economy == null) throw new InvalidOperationException("Economy setup is missing.");
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("battlefieldLayout").objectReferenceValue = layout;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(economy);

            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Main Camera is missing.");
            camera.transform.position = new Vector3(12f, 22f, -10f);
            camera.orthographicSize = 11f;
            var cameraSettings = new SerializedObject(camera.GetComponent<MapCameraController>());
            cameraSettings.FindProperty("playableBounds").rectValue =
                new Rect(0f, 0f, RiverMountainsLayout.Width, RiverMountainsLayout.Height);
            cameraSettings.FindProperty("maximumZoom").floatValue = RiverMountainsLayout.Height * 0.5f;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(camera);

            Validate(gridManager, layout, grid);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildMenuScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("Straight Lane map built and validated. Use MapSelection to choose a battlefield.");
        }

        private static void MigrateAsset(string oldPath, string newPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(oldPath) == null
                || AssetDatabase.LoadMainAssetAtPath(newPath) != null) return;
            string error = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }

        [MenuItem("Tools/Tiny Kingdom/Export Straight Lane Overview")]
        public static void ExportOverview()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before exporting the map.");
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Main Camera is missing.");
            Vector3 oldPosition = camera.transform.position;
            float oldZoom = camera.orthographicSize;
            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            var target = new RenderTexture(1920, 1056, 24);
            var output = new Texture2D(1920, 1056, TextureFormat.RGB24, false);
            try
            {
                camera.transform.position = new Vector3(30f, 22f, -10f);
                camera.orthographicSize = 16.5f;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                output.ReadPixels(new Rect(0, 0, 1920, 1056), 0, 0);
                output.Apply();
                Directory.CreateDirectory("Documentation");
                File.WriteAllBytes("Documentation/LinearLane-overview.png", output.EncodeToPNG());
                // A close view makes the tightly overlapping native copies
                // inspectable without zooming the overview.
                camera.transform.position = new Vector3(10f,23f,-10f);
                camera.orthographicSize = 6f;
                camera.Render();
                output.ReadPixels(new Rect(0,0,1920,1056),0,0);
                output.Apply();
                File.WriteAllBytes("Documentation/LinearLane-stairs.png",output.EncodeToPNG());
                camera.transform.position = new Vector3(12.2f,23.8f,-10f);
                camera.orthographicSize = 2.2f;
                camera.Render();
                output.ReadPixels(new Rect(0,0,1920,1056),0,0);
                output.Apply();
                File.WriteAllBytes("Documentation/LinearLane-gold.png",output.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                camera.transform.position = oldPosition;
                camera.orthographicSize = oldZoom;
                Object.DestroyImmediate(output);
                target.Release();
                Object.DestroyImmediate(target);
            }
            Debug.Log("Straight Lane overview exported to Documentation/LinearLane-overview.png");
        }

        private static BattlefieldLayout BuildLayout(BattlefieldLayout source)
        {
            BattlefieldLayout layout = AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<BattlefieldLayout>();
                AssetDatabase.CreateAsset(layout, LayoutPath);
            }
            layout.blueCastle = RiverMountainsLayout.BlueCastle;
            layout.redCastle = RiverMountainsLayout.RedCastle;
            layout.bluePawns = new[] { new Vector2(5.5f, 21.5f), new Vector2(6.5f, 21.5f), new Vector2(7.5f, 21.5f) };
            layout.redPawns = layout.bluePawns.Select(p => new Vector2(60f-p.x,p.y)).ToArray();
            layout.redPawnAnimations = source.redPawnAnimations;
            var resources = new List<BattlefieldResource>();
            var trees = new List<Vector2>
            {
                new(3.5f,21.5f), new(3.5f,26.5f), new(13.5f,26.5f),
                new(16.5f,26.5f), new(14.5f,18.5f)
            };
            // Pines frame the newly enlarged first tier without filling its
            // construction area or occupying the Sheep's rows.
            foreach (int y in new[] {15,30})
                for (int x=3;x<=15;x+=2) trees.Add(new Vector2(x+0.5f,y+0.5f));
            // Hand-scattered groups: irregular cells and small within-cell
            // offsets look natural. The four pink-marked entries are omitted
            // at placement below, retaining the other Sheep names/positions.
            var sheep = new List<Vector2>
            {
                new(5.4f,26.75f), new(6.2f,27.35f), new(6.85f,29.45f), new(8.4f,28.15f),
                new(9.65f,27.2f), new(10.45f,29.3f), new(11.85f,28.55f), new(12.3f,27.25f),
                new(14.25f,28.4f), new(15.6f,29.15f), new(16.25f,27.65f), new(17.15f,28.35f),
                new(5.4f,17.75f), new(6.7f,18.6f), new(7.25f,16.55f), new(8.6f,19.3f),
                new(9.3f,17.35f), new(10.7f,18.25f), new(11.3f,16.8f), new(12.6f,19.15f),
                new(13.45f,17.65f), new(16.2f,18.4f)
            };
            // Red uses its own hand-authored flock (local left-base coordinates
            // mirrored only when placed). Empty the four marked cells, without
            // deleting food or repeating Blue's pattern.
            Vector2[] redSheep =
            {
                new(4.45f,28.1f), new(5.3f,27.4f), new(6.6f,28.15f), new(7.4f,29.55f),
                new(8.2f,26.55f), new(8.85f,28.7f), new(10.3f,27.8f), new(11.65f,29.35f),
                new(12.5f,27.2f), new(14.25f,29.15f), new(15.7f,28.5f), new(16.3f,27.4f),
                new(4.3f,18.2f), new(5.7f,16.7f), new(6.45f,19.3f), new(7.8f,17.55f),
                new(8.55f,18.6f), new(9.3f,16.25f), new(10.65f,19.35f), new(11.4f,17.4f),
                new(12.7f,18.1f), new(16.2f,17.6f)
            };
            // Two large stones behind medium/small fragments, matching the
            // reference. Each remains a separate, reachable native Gold node.
            Vector2[] gold = { new(11.75f,24.25f), new(13.1f,24.5f), new(12.2f,23.65f),
                new(13.1f,23.55f), new(11.25f,23.4f), new(12.1f,22.9f) };
            int[] goldVariants = {4,5,2,3,1,0};
            // Fixed staggered rows frame both clearings and the widened lane.
            // Their gaps are harvest aisles; every tree is a real Wood node.
            for (int y=12;y<=32;y+=2)
            for (int x=3+(y/2%2);x<30;x+=2)
            {
                NavigationCellData cell=RiverMountainsLayout.Cell(x,y);
                bool forest = x<=18 ? y<=15 || y>=29 || x>=16 || x<=3
                    : y<=18 || y>=26;
                Vector2 p=new(x+0.5f,y+0.5f);
                if (!forest || cell.terrainType!=TerrainType.FlatGround
                    || IsCoastalAisle(x,y)
                    || RiverMountainsLayout.IsRoad(x,y)
                    || RiverMountainsLayout.TryRampCell(x,y,out _)
                    || gold.Concat(sheep).Concat(trees).Any(other=>Vector2.Distance(other,p)<1.6f)) continue;
                trees.Add(p);
            }
            // Additional lane-edge pines remain approachable from the clear road.
            // Do not fill the harvest aisles or use any other tree variants.
            foreach (int y in new[] {19,25})
            foreach (int x in new[] {19,21,23})
            {
                Vector2 p=new(x+0.5f,y+0.5f);
                if (RiverMountainsLayout.Cell(x,y).terrainType==TerrainType.FlatGround
                    && !RiverMountainsLayout.IsRoad(x,y)
                    && !gold.Concat(sheep).Concat(trees).Any(other=>Vector2.Distance(other,p)<1.4f))
                    trees.Add(p);
            }
            // Fill previously sparse coastal corners, keeping the interior
            // harvest aisles open and avoiding the Sheep pastures.
            Vector2[] coastalPines =
            {
                new(6.5f,11.5f), new(10.5f,11.5f), new(14.5f,11.5f),
                new(6.5f,33.5f), new(10.5f,33.5f), new(14.5f,33.5f),
                new(2.5f,13.5f), new(18.5f,13.5f),
                new(2.5f,31.5f), new(18.5f,31.5f)
            };
            foreach (Vector2 p in coastalPines)
            {
                Vector2Int cell=ToCell(p);
                if (RiverMountainsLayout.Cell(cell.x,cell.y).terrainType==TerrainType.FlatGround
                    && !IsCoastalAisle(cell.x,cell.y)
                    && !gold.Concat(sheep).Concat(trees).Any(other=>Vector2.Distance(other,p)<1.4f))
                    trees.Add(p);
            }
            // Denser outer woodland only. Do not add or move a single pine on
            // the two Castle tiers, or block the narrow coastal harvest aisles.
            Vector2[] outerPines =
            {
                new(19.5f,27.5f), new(8.5f,11.5f), new(28.5f,29.5f), new(8.5f,33.5f),
                new(16.5f,11.5f), new(19.5f,17.5f), new(16.5f,33.5f), new(26.5f,15.5f),
                new(21.5f,27.5f), new(21.5f,17.5f), new(4.5f,11.5f), new(4.5f,33.5f),
                new(12.5f,11.5f), new(12.5f,33.5f), new(25.5f,19.5f), new(25.5f,25.5f)
            };
            foreach (Vector2 p in outerPines)
            {
                Vector2Int cell=ToCell(p);
                if (RiverMountainsLayout.Cell(cell.x,cell.y).terrainType!=TerrainType.FlatGround
                    || RiverMountainsLayout.IsRoad(cell.x,cell.y) || IsCoastalAisle(cell.x,cell.y)
                    || gold.Concat(sheep).Concat(trees).Any(other=>Vector2.Distance(other,p)<1.25f))
                    throw new InvalidOperationException("An outer pine obstructs the authored layout: "+p);
                trees.Add(p);
            }
            // Densify only the two red-marked northern forest bands. Move the
            // existing H0 row one cell inward and fill its gaps with six pines.
            // Keep y=32 and the inward end x=17 open as a harvest aisle: filling
            // both staggered rows would trap their resource interaction cells.
            for (int i=0;i<trees.Count;i++)
                if (trees[i].y==32.5f && trees[i].x<=18f)
                    trees[i]=new Vector2(trees[i].x==17.5f?16.5f:trees[i].x,31.5f);
            for (int side = 0; side < 2; side++)
            {
                void Add(Vector2 p, ResourceType type, int index)
                {
                    if (side == 1) p.x = RiverMountainsLayout.Width - p.x;
                    resources.Add(new BattlefieldResource
                    {
                        name = (side == 0 ? "Blue" : "Red") + "_" + type + "_" + index,
                        type = type, position = p,
                        variant = type == ResourceType.Wood ? index % 2 : type == ResourceType.Gold ? goldVariants[index] : 0
                    });
                }
                // Index zero is the outer H2 pine marked red in the sketch.
                // Skip it without renumbering/changing the other pine variants.
                for (int i = 1; i < trees.Count; i++) Add(trees[i], ResourceType.Wood, i);
                Vector2[] markedTrees = side == 0
                    ? new Vector2[] {new(4.5f,30.5f), new(8.5f,30.5f), new(12.5f,30.5f), new(16.5f,30.5f),
                        new(3.5f,28.5f), new(4.5f,28.5f), new(3.5f,18.5f), new(4.5f,16.5f),
                        new(3.5f,25.5f), new(9.5f,25.5f)}
                    : new Vector2[] {new(4.5f,30.5f), new(8.5f,30.5f), new(12.5f,30.5f), new(16.5f,30.5f)};
                for (int i=0;i<markedTrees.Length;i++) Add(markedTrees[i],ResourceType.Wood,trees.Count+i);
                Vector2[] northPines={new(4.5f,31.5f),new(6.5f,31.5f),new(8.5f,31.5f),
                    new(10.5f,31.5f),new(12.5f,31.5f),new(14.5f,31.5f)};
                for (int i=0;i<northPines.Length;i++)
                    Add(northPines[i],ResourceType.Wood,trees.Count+markedTrees.Length+i);
                Vector2[] flock=side==0?sheep.ToArray():redSheep;
                for (int i = 0; i < flock.Length; i++)
                {
                    // Delete exactly the two pink-marked Sheep in each base.
                    if (side==0?(i==14 || i==18):(i==13 || i==17)) continue;
                    Add(flock[i],ResourceType.Food,i);
                }
                for (int i = 0; i < gold.Length; i++) Add(gold[i], ResourceType.Gold, i);
            }
            layout.resources = resources.ToArray();
            EditorUtility.SetDirty(layout);
            return layout;
        }

        private static NavigationGridData BuildNavigation()
        {
            NavigationGridData data = AssetDatabase.LoadAssetAtPath<NavigationGridData>(NavigationPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<NavigationGridData>();
                AssetDatabase.CreateAsset(data, NavigationPath);
            }
            var cells = new List<NavigationCellData>(RiverMountainsLayout.Width * RiverMountainsLayout.Height);
            for (int y = 0; y < RiverMountainsLayout.Height; y++)
            for (int x = 0; x < RiverMountainsLayout.Width; x++)
                cells.Add(RiverMountainsLayout.Cell(x, y));
            data.Configure(RiverMountainsLayout.Width, RiverMountainsLayout.Height,
                Vector2.zero, 1f, cells);
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void PaintTerrain(Transform grid, BattlefieldLayout layout)
        {
            foreach (Tilemap old in grid.GetComponentsInChildren<Tilemap>())
                if (old.name != "BG_Color") old.ClearAllTiles();
            // Rebuild only the two authored Castle levels, not map-one terraces.
            for (int height = 3; height <= 3; height++)
            {
                Transform surface = grid.Find("ElevatedGround_L" + height);
                Transform shadow = grid.Find("Shadow_L" + height);
                if (surface != null) Object.DestroyImmediate(surface.gameObject);
                if (shadow != null) Object.DestroyImmediate(shadow.gameObject);
            }
            Tilemap flat = grid.Find("FlatGround").GetComponent<Tilemap>();
            Tilemap foam = grid.Find("WaterFoam").GetComponent<Tilemap>();
            Tilemap road = grid.Find("MeadowsAndPaths").GetComponent<Tilemap>();
            AnimatedFoamTile foamTile = AssetDatabase.LoadAssetAtPath<AnimatedFoamTile>(
                "Assets/Tiles/Water/AnimatedFoam.asset");
            if (foamTile == null) throw new InvalidOperationException("The verified foam tile is missing.");
            for (int y = 0; y < RiverMountainsLayout.Height; y++)
            for (int x = 0; x < RiverMountainsLayout.Width; x++)
            {
                if (!RiverMountainsLayout.IsLand(x, y)) continue;
                var cell = new Vector3Int(x, y, 0);
                int row = !RiverMountainsLayout.IsLand(x, y + 1) ? 0
                    : !RiverMountainsLayout.IsLand(x, y - 1) ? 2 : 1;
                int col = !RiverMountainsLayout.IsLand(x - 1, y) ? 0
                    : !RiverMountainsLayout.IsLand(x + 1, y) ? 2 : 1;
                flat.SetTile(cell, Tile(1, row, col));
                if (RiverMountainsLayout.IsShoreline(x, y)) foam.SetTile(cell, foamTile);
                if (RiverMountainsLayout.IsRoad(x, y)
                    && RiverMountainsLayout.Cell(x,y).terrainType==TerrainType.FlatGround)
                {
                    int roadRow=!RiverMountainsLayout.IsRoad(x,y+1)?0:!RiverMountainsLayout.IsRoad(x,y-1)?2:1;
                    int roadCol=!RiverMountainsLayout.IsRoad(x-1,y)?0:!RiverMountainsLayout.IsRoad(x+1,y)?2:1;
                    road.SetTile(cell,Tile(4,roadRow,roadCol));
                }
            }
            PaintTerraces(grid);
        }

        private static Tilemap Layer(Transform grid,string name,int order)
        {
            Transform existing=grid.Find(name);
            GameObject obj=existing!=null?existing.gameObject:new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer));
            obj.transform.SetParent(grid,false);
            obj.GetComponent<TilemapRenderer>().sortingOrder=order;
            return obj.GetComponent<Tilemap>();
        }

        private static void PaintTerraces(Transform grid)
        {
            var surfaces=new Tilemap[3];
            var shadows=new Tilemap[3];
            var stairFronts=new Tilemap[3];
            Tilemap rampBacking=grid.Find("MeadowsAndPaths").GetComponent<Tilemap>();
            Tile shadowTile=AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/Shadows/Shadow.asset");
            for (int level=1;level<=2;level++)
            {
                shadows[level]=Layer(grid,"Shadow_L"+level,2+level*2);
                surfaces[level]=Layer(grid,"ElevatedGround_L"+level,3+level*2);
                stairFronts[level]=Layer(grid,"StairFront_L"+level,4+level*2);
                shadows[level].ClearAllTiles(); surfaces[level].ClearAllTiles(); stairFronts[level].ClearAllTiles();
            }
            foreach (TerraceDefinition platform in RiverMountainsLayout.Platforms)
            {
                RectInt r=platform.Surface;
                bool HasSurface(int x,int y) => RiverMountainsLayout.Platforms.Any(other=>
                    other.Height==platform.Height && other.Surface.Contains(new Vector2Int(x,y)));
                for (int y=r.yMin;y<r.yMax;y++)
                for (int x=r.xMin;x<r.xMax;x++)
                {
                    // Only the external borders of an authored surface have edges.
                    int row=!HasSurface(x,y+1)?0:!HasSurface(x,y-1)?2:1;
                    int col=!HasSurface(x-1,y)?5:!HasSurface(x+1,y)?7:6;
                    surfaces[platform.Height].SetTile(new(x,y,0),Tile(platform.Color,row,col));
                    shadows[platform.Height].SetTile(new(x,y-1,0),shadowTile);
                }
                for (int x=r.xMin;x<r.xMax;x++)
                {
                    if (HasSurface(x,r.yMin-1)) continue;
                    int col=x==r.xMin?5:x==r.xMax-1?7:6;
                    surfaces[platform.Height].SetTile(new(x,r.yMin-1,0),Tile(platform.Color,5,col));
                    shadows[platform.Height].SetTile(new(x,r.yMin-1,0),shadowTile);
                }
            }
            for (int i=0;i<RiverMountainsLayout.ConnectorCount;i++)
            {
                TerrainHeightConnectorDefinition stair=RiverMountainsLayout.Connector(i);
                int level=stair.LowerHeight+1;
                int col=stair.Orientation==TerrainStairOrientation.DescendsLeft?0:3;
                // Bring the rear copy one cell closer. Separate visual layers
                // keep both complete, identically oriented r4/r5 pairs intact
                // at their overlapping cell; no rotation or scaling is used.
                int offset=RiverMountainsLayout.RampCopyOffset;
                surfaces[level].SetTile(new(stair.GridX,stair.UpperY+offset,0),Tile(3,4,col));
                surfaces[level].SetTile(new(stair.GridX,stair.LowerY+offset,0),Tile(3,5,col));
                stairFronts[level].SetTile(new(stair.GridX,stair.UpperY,0),Tile(3,4,col));
                stairFronts[level].SetTile(new(stair.GridX,stair.LowerY,0),Tile(3,5,col));
                for (int y=stair.LowerY;y<=stair.UpperY+offset;y++)
                {
                    rampBacking.SetTile(new(stair.GridX,y,0),Tile(3,1,6));
                    shadows[level].SetTile(new(stair.GridX,y,0),null);
                }
            }
        }

        private static void Decorate(Transform map, BattlefieldLayout layout)
        {
            Transform scenery = map.Find("BattlefieldScenery");
            if (scenery == null)
            {
                var root = new GameObject("BattlefieldScenery");
                root.transform.SetParent(map, false);
                scenery = root.transform;
            }
            for (int i = scenery.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(scenery.GetChild(i).gameObject);

            Sprite bush = FirstSprite("Assets/Terrain/Decorations/Bushes/Bushe3.png");
            for (int side = 0; side < 2; side++)
            {
                foreach (Vector2 p in new[] { new Vector2(4.5f, 14.5f), new Vector2(16.5f, 14.5f),
                    new Vector2(4.5f, 29.5f), new Vector2(16.5f, 29.5f) })
                {
                    Vector2 position = side == 0 ? p : new Vector2(60f - p.x, p.y);
                    Place(scenery, "Lane_Bush_" + side + "_" + p, bush, position, 6, 0.8f);
                }
                Sprite[] rocks = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/Terrain/Decorations/Rocks in the Water/Water Rocks_01.png")
                    .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
                foreach (Vector2 p in new[] { new Vector2(6f, 10f), new Vector2(13f, 10f),
                    new Vector2(6f, 33f), new Vector2(13f, 33f) })
                {
                    Vector2 position = side == 0 ? p : new Vector2(60f - p.x, p.y);
                    GameObject rock = Place(scenery, "Lane_WaterRock_" + side + "_" + p, rocks[0],
                        position, 3, 1f);
                    rock.AddComponent<AmbientSpriteLoop>().Configure(rocks, side * 0.2f);
                }
            }
            Vector2[] scatteredBushes = {new(12.6f,28.5f), new(25.5f,21.2f),
                new(34.3f,24.3f), new(46.8f,17.8f)};
            for (int i=0;i<scatteredBushes.Length;i++)
                Place(scenery,"Lane_ExtraBush_"+i,FirstSprite(
                    $"Assets/Terrain/Decorations/Bushes/Bushe{i+1}.png"),scatteredBushes[i],8,1f);

            // The three yellow patches: mixed low tufts and round shrubs, with
            // irregular spacing and sizes rather than three uniform rows.
            Vector2[][] bushPatches =
            {
                new Vector2[] {new(18.75f,25.45f),new(19.65f,25.25f),new(20.5f,25.75f),new(21.75f,25.35f),
                    new(22.4f,25.65f),new(23.65f,25.2f),new(24.45f,25.55f),new(25.8f,25.75f)},
                new Vector2[] {new(27.75f,28.4f),new(28.45f,28.15f),new(29.3f,28.65f),new(30.2f,28.4f),
                    new(31.2f,28.65f),new(31.65f,27.75f),new(29.85f,27.65f),new(32f,28.4f)},
                new Vector2[] {new(34.2f,25.45f),new(35.1f,25.7f),new(36.35f,25.25f),new(37.4f,25.6f),
                    new(38.1f,25.3f),new(39.3f,25.65f),new(40.2f,25.25f),new(41.25f,25.65f)}
            };
            int[] bushKinds={1,4,2,3,1,2,4,3};
            float[] bushScales={1.2f,0.85f,1f,1.1f,0.95f,1.25f,0.8f,1f};
            for (int patch=0;patch<bushPatches.Length;patch++)
            for (int i=0;i<bushPatches[patch].Length;i++)
                Place(scenery,$"Lane_MarkedBush_{patch}_{i}",FirstSprite(
                    $"Assets/Terrain/Decorations/Bushes/Bushe{bushKinds[(i+patch)%8]}.png"),
                    bushPatches[patch][i],8,bushScales[(i+patch*3)%8]);

            // Two lilac land patches and two offshore patches. The latter use
            // the verified ripple strips, not land boulders floating on water.
            Vector2[] stonePositions={new(23.2f,18.1f),new(23.8f,17.75f),new(22.6f,18.3f),
                new(36.2f,18.25f),new(37f,17.85f),new(35.55f,18.05f)};
            int[] stoneKinds={2,3,1,4,1,3};
            float[] stoneScales={1.8f,0.85f,1f,1.8f,1.05f,0.8f};
            for (int i=0;i<stonePositions.Length;i++)
                Place(scenery,"Lane_MarkedStone_"+i,FirstSprite(
                    $"Assets/Terrain/Decorations/Rocks/Rock{stoneKinds[i]}.png"),
                    stonePositions[i],8,stoneScales[i]);
            Vector2[] waterStonePositions={new(39.5f,16.2f),new(40.2f,15.85f),
                new(25.45f,14.25f),new(24.7f,13.95f)};
            for (int i=0;i<waterStonePositions.Length;i++)
            {
                Sprite[] frames=AssetDatabase.LoadAllAssetsAtPath(
                    $"Assets/Terrain/Decorations/Rocks in the Water/Water Rocks_{(i%2==0?2:4):00}.png")
                    .OfType<Sprite>().OrderBy(sprite=>sprite.name).ToArray();
                if (frames.Length!=16 || frames.Any(sprite=>sprite.rect.width!=64
                    || sprite.rect.height!=64 || sprite.pixelsPerUnit!=64))
                    throw new InvalidOperationException("Water rocks must use their sixteen native 64x64 frames.");
                GameObject stone=Place(scenery,"Lane_MarkedWaterStone_"+i,frames[0],
                    waterStonePositions[i],3,i%2==0?1.3f:0.8f);
                stone.AddComponent<AmbientSpriteLoop>().Configure(frames,i*0.11f);
            }

            Vector2[] cloudPositions={new(8f,32f),new(18f,29.8f),new(42.5f,32.3f),new(53f,31.6f),
                new(8f,12.3f),new(19f,13.2f),new(42f,12.6f),new(52.5f,13f),new(29.8f,31.8f),new(33f,15.5f)};
            int[] cloudKinds={1,3,5,8,2,8,1,3,4,3};
            for (int i=0;i<cloudPositions.Length;i++)
            {
                Sprite sprite=FirstSprite($"Assets/Terrain/Decorations/Clouds/Clouds_{cloudKinds[i]:00}.png");
                if (sprite.rect.width!=576 || sprite.rect.height!=256 || sprite.pixelsPerUnit!=64)
                    throw new InvalidOperationException("Clouds must remain whole native illustrations, not sliced frames.");
                bool small=cloudKinds[i]==3 || cloudKinds[i]==4 || cloudKinds[i]==8;
                GameObject cloud=Place(scenery,"Lane_Cloud_"+i,sprite,cloudPositions[i],19,small?1.8f:1.1f);
                cloud.GetComponent<SpriteRenderer>().color=new Color(1f,1f,1f,small?0.72f:0.62f);
                cloud.AddComponent<AmbientCloudDrift>().Configure(new Rect(1,1,58,42),
                    new Vector2(1.2f+(i%3)*0.3f,0.15f+(i%2)*0.1f),52f+i*3f,i*0.7f);
            }

            // Reuse the already verified/imported three-frame floating strip.
            // These animals are scenery only, never food or navigation blockers.
            Sprite[] ducks=AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Terrain/Decorations/Rubber Duck/Rubber duck.png")
                .OfType<Sprite>().OrderBy(sprite=>sprite.name).ToArray();
            if (ducks.Length!=3 || ducks.Any(sprite=>sprite.rect.width!=32
                || sprite.rect.height!=32 || sprite.pixelsPerUnit!=64))
                throw new InvalidOperationException("The verified Duck strip must contain three native 32x32 frames at 64 PPU.");
            Vector2[] duckPositions = {new(1.25f,18.2f), new(1.2f,26f), new(58.8f,17.4f),
                new(58.75f,25.7f), new(22f,29.4f), new(37.4f,14.7f)};
            for (int i=0;i<duckPositions.Length;i++)
            {
                GameObject duck=Place(scenery,"Lane_Duck_"+i,ducks[0],duckPositions[i],3,1.6f);
                duck.GetComponent<SpriteRenderer>().flipX=i%2==1;
                duck.AddComponent<AmbientSpriteLoop>().Configure(ducks,i*0.071f);
            }
        }

        private static void BuildPreview(Transform map, BattlefieldLayout layout)
        {
            Transform old = map.Find("Battlefield_EditorPreview");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var preview = new GameObject("Battlefield_EditorPreview");
            preview.transform.SetParent(map, false);
            preview.tag = "EditorOnly";
            preview.AddComponent<BattlefieldScenePreview>();
            GameObject blueCastle=Place(preview.transform, "Blue_Main_Castle", FirstSprite(
                "Assets/Buildings/Blue Buildings/Castle.png"), layout.blueCastle, 10, 1f);
            GameObject redCastle=Place(preview.transform, "Red_Main_Castle", FirstSprite(
                "Assets/Buildings/Red Buildings/Castle.png"), layout.redCastle, 10, 1f);
            // Preview the existing runtime building scale, anchored to the same
            // ground contact. This does not change gameplay roots or footprints.
            foreach (GameObject castle in new[] {blueCastle,redCastle})
            {
                SpriteRenderer renderer=castle.GetComponent<SpriteRenderer>();
                TinyKingdom.Buildings.BuildingVisualPresentation.Apply(renderer,
                    TinyKingdom.Buildings.BuildingType.Castle,castle.transform.localPosition);
            }
            PresentationAssetCatalog catalog = PresentationAssetCatalog.Load();
            foreach (BattlefieldResource resource in layout.resources)
            {
                Sprite sprite = resource.type == ResourceType.Wood ? catalog.Tree(resource.variant)
                    : resource.type == ResourceType.Gold ? catalog.Gold(resource.variant)
                    : catalog.SheepIdle[0];
                Vector2 position = resource.position;
                float scale=resource.type==ResourceType.Gold ? resource.variant==5?1.55f:1.4f : 1.3f;
                if (resource.type == ResourceType.Wood)
                    position.y += sprite.rect.height * 0.5f * scale / sprite.pixelsPerUnit;
                Place(preview.transform, resource.name, sprite, position, 10, scale);
            }
            PawnAnimationSet bluePawns = AssetDatabase.LoadAssetAtPath<PawnAnimationSet>(
                "Assets/UnitAnimations/BluePawn.asset");
            foreach (Vector2 position in layout.bluePawns)
                Place(preview.transform, "Blue_Pawn", bluePawns.Idle[0], position, 10, 1f);
            foreach (Vector2 position in layout.redPawns)
                Place(preview.transform, "Red_Pawn", layout.redPawnAnimations.Idle[0], position, 10, 1f);
            Place(preview.transform, "Central_Gold_Mine",
                Phase6C2AssetCatalog.Load().GoldMineInactive,
                new Vector2(StrategicObjectiveMapLayout.MineOriginX + 1f,
                    StrategicObjectiveMapLayout.MineOriginY + 1f + GoldMineObjective.VisualGroundOffsetY),
                10, GoldMineObjective.VisualScale);
        }

        private static void BuildMenuScene()
        {
            Scene menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath) != null
                ? EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (Object.FindAnyObjectByType<MapSelectionMenu>() == null)
                new GameObject("Map Selection").AddComponent<MapSelectionMenu>();
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if (Camera.main == null)
            {
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            }
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu, MenuPath);
        }

        private static void ConfigureBuildSettings()
        {
            string[] priority = { MenuPath, OriginalScene, ScenePath };
            var scenes = priority.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
            scenes.AddRange(EditorBuildSettings.scenes.Where(scene =>
                File.Exists(scene.path) && !priority.Contains(scene.path, StringComparer.Ordinal)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void Validate(GridManager grid, BattlefieldLayout layout, Transform visuals)
        {
            if (grid.Width != RiverMountainsLayout.Width || grid.Height != RiverMountainsLayout.Height)
                throw new InvalidOperationException("The second navigation grid has the wrong dimensions.");
            Tilemap flat = visuals.Find("FlatGround").GetComponent<Tilemap>();
            Tilemap foam = visuals.Find("WaterFoam").GetComponent<Tilemap>();
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var p = new Vector3Int(x, y, 0);
                GridCell cell = grid.GetCell(x, y);
                NavigationCellData expected=RiverMountainsLayout.Cell(x,y);
                if (flat.HasTile(p) != RiverMountainsLayout.IsLand(x, y)
                    || foam.HasTile(p) != RiverMountainsLayout.IsShoreline(x, y)
                    || cell.TerrainType!=expected.terrainType
                    || cell.HeightLevel!=expected.heightLevel)
                    throw new InvalidOperationException($"Linear terrain and visuals disagree at ({x}, {y}).");
                if (expected.terrainType==TerrainType.ElevatedGround
                    && !visuals.Find("ElevatedGround_L"+expected.heightLevel).GetComponent<Tilemap>().HasTile(p))
                    throw new InvalidOperationException("An elevated navigation cell has no surface: "+p);
                if (x >= 19 && x <= 40 && cell.Walkable
                    && (y < RiverMountainsLayout.LaneBottom || y > RiverMountainsLayout.LaneTop)
                    && !RiverMountainsLayout.IsCentralIsland(x,y))
                    throw new InvalidOperationException("An alternate route bypasses the single lane.");
            }
            for (int i=0;i<RiverMountainsLayout.ConnectorCount;i++)
            {
                TerrainHeightConnectorDefinition stair=RiverMountainsLayout.Connector(i);
                Tilemap surface=visuals.Find("ElevatedGround_L"+(stair.LowerHeight+1)).GetComponent<Tilemap>();
                Tilemap front=visuals.Find("StairFront_L"+(stair.LowerHeight+1)).GetComponent<Tilemap>();
                int offset=RiverMountainsLayout.RampCopyOffset;
                int col=stair.Orientation==TerrainStairOrientation.DescendsLeft?0:3;
                if (offset!=1
                    || surface.GetTile(new(stair.GridX,stair.UpperY+offset,0))!=Tile(3,4,col)
                    || surface.GetTile(new(stair.GridX,stair.LowerY+offset,0))!=Tile(3,5,col)
                    || front.GetTile(new(stair.GridX,stair.UpperY,0))!=Tile(3,4,col)
                    || front.GetTile(new(stair.GridX,stair.LowerY,0))!=Tile(3,5,col))
                    throw new InvalidOperationException("Double stair visuals must overlap exactly one cell and retain their native orientation.");
            }
            for (int side=0;side<2;side++)
            {
                RectInt court=RiverMountainsLayout.Platforms[2+side].Surface;
                Vector2 castle=side==0?layout.blueCastle:layout.redCastle;
                if (court.width!=7 || court.height!=5 || Vector2.Distance(court.center,castle)>0.01f)
                    throw new InvalidOperationException("The seven-cell upper court must be centered on its Castle.");
            }
            foreach (string side in new[] {"Blue","Red"})
            {
                BattlefieldResource[] trees=layout.resources.Where(node=>node.type==ResourceType.Wood
                    && node.name.StartsWith(side,StringComparison.Ordinal)).ToArray();
                if (trees.Length!=(side=="Blue"?93:87) || trees.Count(node=>
                    RiverMountainsLayout.Cell(ToCell(node.position).x,ToCell(node.position).y).heightLevel>0)!=(side=="Blue"?28:22))
                    throw new InvalidOperationException("The marked pine additions/removals differ from the sketch.");
                if (layout.resources.Count(node=>node.type==ResourceType.Food
                    && node.name.StartsWith(side,StringComparison.Ordinal))!=20)
                    throw new InvalidOperationException("Deleting the four pink-marked Sheep must leave 20 per faction.");
                int[] gold=layout.resources.Where(node=>node.type==ResourceType.Gold
                    && node.name.StartsWith(side,StringComparison.Ordinal)).Select(node=>node.variant).OrderBy(i=>i).ToArray();
                if (!gold.SequenceEqual(new[] {0,1,2,3,4,5}))
                    throw new InvalidOperationException("Each reference-style Gold cluster must use the six verified sizes.");
            }
            Transform scenery=visuals.parent.Find("BattlefieldScenery");
            Transform[] decorations=scenery.Cast<Transform>().ToArray();
            if (decorations.Count(item=>item.name.StartsWith("Lane_ExtraBush_"))!=4
                || decorations.Count(item=>item.name.StartsWith("Lane_Duck_"))!=6
                || decorations.Count(item=>item.name.StartsWith("Lane_MarkedBush_"))!=24
                || decorations.Count(item=>item.name.StartsWith("Lane_MarkedStone_"))!=6
                || decorations.Count(item=>item.name.StartsWith("Lane_MarkedWaterStone_"))!=4
                || decorations.Count(item=>item.name.StartsWith("Lane_Cloud_"))!=10
                || scenery.GetComponentsInChildren<Collider2D>(true).Length!=0
                || scenery.GetComponentsInChildren<ResourceNode>(true).Length!=0)
                throw new InvalidOperationException("Clouds, mixed bushes, rocks and Ducks must remain nonblocking scenery.");
            foreach (Transform decoration in decorations.Where(item=>item.name.StartsWith("Lane_Marked")))
            {
                Vector2Int p=ToCell(decoration.position);
                bool water=decoration.name.StartsWith("Lane_MarkedWaterStone_");
                if (RiverMountainsLayout.IsLand(p.x,p.y)==water
                    || !water && RiverMountainsLayout.IsRoad(p.x,p.y))
                    throw new InvalidOperationException("A marked decoration is on the wrong ground or obstructs the clear road.");
            }
            foreach (AmbientCloudDrift cloud in scenery.GetComponentsInChildren<AmbientCloudDrift>())
            {
                Vector2 extent=cloud.GetComponent<SpriteRenderer>().bounds.extents;
                for (int i=0;i<16;i++)
                {
                    Vector2 p=cloud.PositionAt(cloud.Period*i/16f);
                    if (p.x-extent.x<1 || p.x+extent.x>59 || p.y-extent.y<1 || p.y+extent.y>43)
                        throw new InvalidOperationException("A drifting cloud leaves map bounds.");
                }
            }
            foreach (Transform duck in decorations.Where(item=>item.name.StartsWith("Lane_Duck_")))
            {
                Vector2Int p=ToCell(duck.position);
                if (RiverMountainsLayout.IsLand(p.x,p.y) || duck.GetComponent<AmbientSpriteLoop>().FrameCount!=3)
                    throw new InvalidOperationException("A decorative Duck is not using its water-only ambient mapping.");
            }
            var used = new HashSet<Vector2Int>();
            foreach (BattlefieldResource resource in layout.resources)
            {
                Vector2Int p = ToCell(resource.position);
                if (!grid.GetCell(p.x,p.y).Walkable || !used.Add(p)
                    || RiverMountainsLayout.IsRoad(p.x, p.y)
                    || RiverMountainsLayout.TryRampCell(p.x,p.y,out _)
                    || resource.type==ResourceType.Wood && (resource.variant<0 || resource.variant>1))
                    throw new InvalidOperationException("Resource obstructs the lane: " + resource.name);
                if (resource.type==ResourceType.Food && grid.GetCell(p.x,p.y).HeightLevel!=1)
                    throw new InvalidOperationException("Every Sheep must start on the first tier: "+resource.name);
                if (resource.type==ResourceType.Wood && (p==new Vector2Int(3,21) || p==new Vector2Int(56,21))
                    || resource.type==ResourceType.Food && (p==new Vector2Int(6,16) || p==new Vector2Int(10,16)
                        || p==new Vector2Int(53,16) || p==new Vector2Int(49,16)
                        || p==new Vector2Int(7,16) || p==new Vector2Int(11,16)
                        || p==new Vector2Int(54,16) || p==new Vector2Int(50,16)))
                    throw new InvalidOperationException("A red-marked tree/Sheep location was not emptied.");
            }
            foreach (Vector2 spawn in layout.bluePawns.Concat(layout.redPawns))
            {
                Vector2Int p = ToCell(spawn);
                if (!grid.GetCell(p.x, p.y).Walkable || used.Contains(p))
                    throw new InvalidOperationException("A starting Pawn is blocked.");
            }
            if (layout.bluePawns.Length != 3 || layout.redPawns.Length != 3)
                throw new InvalidOperationException("Both factions need exactly three starting Pawns.");
            foreach (ResourceType type in new[] { ResourceType.Food, ResourceType.Wood, ResourceType.Gold })
            {
                int blue = layout.resources.Count(resource => resource.type == type
                    && Vector2.Distance(resource.position, layout.blueCastle) <= 11f);
                int red = layout.resources.Count(resource => resource.type == type
                    && Vector2.Distance(resource.position, layout.redCastle) <= 11f);
                // Food/Gold remain equal. The user's green marks add six more
                // Blue trees than Red; accept exactly that authored difference.
                if (type==ResourceType.Wood ? blue-red!=6 : blue!=red)
                    throw new InvalidOperationException("Starting resources differ from the approved sketch: " + type);
            }
            Vector2Int blueStart = ToCell(layout.bluePawns[0]), redStart = ToCell(layout.redPawns[0]);
            // Test authored routes with the actual opening blockers, then leave
            // edit-mode navigation clean. Runtime repeats this with live entities.
            var blockers=new HashSet<Vector2Int>(used);
            foreach (Vector2 castle in new[] {layout.blueCastle,layout.redCastle})
            {
                Vector2Int center=ToCell(castle);
                for (int y=center.y;y<=center.y+1;y++)
                for (int x=center.x-2;x<=center.x+2;x++)
                {
                    GridCell cell=grid.GetCell(x,y);
                    if (!cell.Walkable || cell.HeightLevel!=2 || used.Contains(new(x,y)))
                        throw new InvalidOperationException("Castle footprint/clearance must be empty height-2 ground.");
                    blockers.Add(new(x,y));
                }
            }
            for (int y=21;y<=22;y++) for (int x=29;x<=30;x++) blockers.Add(new(x,y));
            try
            {
                foreach (Vector2Int p in blockers) grid.SetOccupied(p.x,p.y,true);
                ValidateLinearRoutes(grid, layout, grid.GetCell(blueStart.x, blueStart.y),
                    grid.GetCell(redStart.x, redStart.y));
                // The two overlapping visuals represent one shared gate, not
                // conflicting height endpoints on the same cell. Closing its
                // logical edge must block passage; decorative pixels add no bypass.
                for (int i=0;i<RiverMountainsLayout.ConnectorCount;i++)
                {
                    TerrainHeightConnectorDefinition stair=RiverMountainsLayout.Connector(i);
                    grid.SetOccupied(stair.GridX,stair.LowerY,true);
                    grid.SetOccupied(stair.GridX,stair.UpperY,true);
                    bool open=AStarPathfinder.FindPath(grid,grid.GetCell(blueStart.x,blueStart.y),
                        grid.GetCell(redStart.x,redStart.y)).Count>0;
                    grid.SetOccupied(stair.GridX,stair.LowerY,false);
                    grid.SetOccupied(stair.GridX,stair.UpperY,false);
                    if (open) throw new InvalidOperationException("An overlapping visual ramp permits an unintended height bypass: "+i);
                }
            }
            finally { foreach (Vector2Int p in blockers) grid.SetOccupied(p.x,p.y,false); }
            Debug.Log($"LINEAR_LANE_VALIDATED: two-tier bases, four double stair gates, wide lane and circular mine clearing; {layout.resources.Length} resources.");
        }

        public static void ValidateLinearRoutes(GridManager grid, BattlefieldLayout layout,
            GridCell blue, GridCell red)
        {
            if (AStarPathfinder.FindPath(grid, blue, red).Count == 0
                || AStarPathfinder.FindPath(grid, red, blue).Count == 0)
                throw new InvalidOperationException("The straight lane cannot connect both factions.");
            foreach (BattlefieldResource node in layout.resources)
            {
                GridCell start = node.name.StartsWith("Blue", StringComparison.Ordinal) ? blue : red;
                Vector2Int p = ToCell(node.position);
                bool reachable = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down }
                    .Select(offset => grid.GetCell(p.x + offset.x, p.y + offset.y))
                    .Any(cell => cell != null && cell.HeightLevel==grid.GetCell(p.x,p.y).HeightLevel
                        && AStarPathfinder.FindPath(grid, start, cell).Count > 0);
                if (!reachable) throw new InvalidOperationException("Cannot reach resource: " + node.name);
            }
            for (int i=0;i<RiverMountainsLayout.ConnectorCount;i++)
            {
                TerrainHeightConnectorDefinition stair=RiverMountainsLayout.Connector(i);
                GridCell lower=grid.GetCell(stair.GridX,stair.LowerY);
                GridCell upper=grid.GetCell(stair.GridX,stair.UpperY);
                if (!lower.CanTraverseTo(upper) || !upper.CanTraverseTo(lower)
                    || AStarPathfinder.FindPath(grid,blue,lower).Count==0
                    || AStarPathfinder.FindPath(grid,red,upper).Count==0)
                    throw new InvalidOperationException($"A stair endpoint is disconnected: {i}; lower {lower}, occupied={lower.Occupied}, partner={lower.HeightConnection}; upper {upper}, occupied={upper.Occupied}, partner={upper.HeightConnection}; occupants="
                        +string.Join(",",ResourceNode.ActiveNodes.Where(node=>node.OccupiedCell==new Vector2Int(stair.GridX,stair.LowerY)
                            || node.OccupiedCell==new Vector2Int(stair.GridX,stair.UpperY)).Select(node=>node.name)));
            }
            // The occupied mine footprint must leave a lane above and below it.
            foreach (Vector2Int p in new[] { new Vector2Int(29, 19), new Vector2Int(30, 24) })
            {
                GridCell destination = grid.GetCell(p.x, p.y);
                if (AStarPathfinder.FindPath(grid, blue, destination).Count == 0
                    || AStarPathfinder.FindPath(grid, red, destination).Count == 0)
                    throw new InvalidOperationException("The mine blocks passage through the straight lane.");
            }
        }

        private static Vector2Int ToCell(Vector2 position)
            => new(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));

        // The enlarged H1 platforms leave narrow H0 passages beside their
        // shoreline. Keep them open so the coastal forests remain harvestable.
        private static bool IsCoastalAisle(int x,int y)
            => (x==2 || x==18) && y>=13 && y<=31;

        private static Tile Tile(int color, int row, int column)
        {
            string name = $"Tilemap_color{color}_r{row}c{column}";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(
                $"Assets/Tiles/Terrain/Color{color}/{name}.asset");
            if (tile == null) throw new InvalidOperationException("Missing terrain tile: " + name);
            return tile;
        }

        private static Sprite FirstSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(item => item.name).FirstOrDefault();
            if (sprite == null) throw new InvalidOperationException("Missing verified sprite: " + path);
            return sprite;
        }

        private static GameObject Place(Transform parent, string name, Sprite sprite,
            Vector2 position, int order, float scale)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            obj.transform.localScale = Vector3.one * scale;
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return obj;
        }
    }
}
