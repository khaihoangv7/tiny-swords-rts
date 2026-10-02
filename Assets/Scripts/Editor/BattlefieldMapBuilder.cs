using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Terrain;
using TinyKingdom.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace TinyKingdom.EditorTools
{
    public static class BattlefieldMapBuilder
    {
        public const string LayoutPath = "Assets/Data/TwoKingdomsLayout.asset";
        private const string ScenePath = "Assets/Scenes/Map.unity";
        private static readonly Dictionary<string, Tile> tiles = new();

        [MenuItem("Tools/Tiny Kingdom/Build Two Kingdoms Battlefield")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory("Logs/BeforeTwoKingdoms");
            if (!File.Exists("Logs/BeforeTwoKingdoms/Map.unity"))
                File.Copy(ScenePath, "Logs/BeforeTwoKingdoms/Map.unity");
            Phase1MapBuilder.Build();
            Phase2NavigationBuilder.Build();
            tiles.Clear();
            BattlefieldLayout layout = MakeLayout();
            GameObject map = GameObject.Find("Map");
            Transform grid = map.transform.Find("Grid");
            grid.Find("BG_Color").GetComponent<TilemapRenderer>().sortingOrder = GroundSorting.TerrainBackgroundOrder(0);
            grid.Find("WaterFoam").GetComponent<TilemapRenderer>().sortingOrder = GroundSorting.TerrainBackgroundOrder(1);
            grid.Find("FlatGround").GetComponent<TilemapRenderer>().sortingOrder = GroundSorting.TerrainBackgroundOrder(2);
            grid.Find("Shadow_L1").GetComponent<TilemapRenderer>().sortingOrder = GroundSorting.ElevatedTerrainBackgroundOrder(1, false);
            grid.Find("ElevatedGround_L1").GetComponent<TilemapRenderer>().sortingOrder = GroundSorting.ElevatedTerrainBackgroundOrder(1, true);
            grid.Find("Decorations").GetComponent<TilemapRenderer>().sortingOrder = GroundSorting.TerrainBackgroundOrder(4);
            Tilemap meadows = Layer(grid, "MeadowsAndPaths", GroundSorting.TerrainBackgroundOrder(3));
            PaintClearings(meadows);
            Transform details = ReplaceRoot(map.transform, "BattlefieldScenery");
            PaintScenery(details, layout);
            MakePreview(map.transform, layout);

            EconomyTestSetup economy = Object.FindAnyObjectByType<EconomyTestSetup>();
            if (economy == null) throw new InvalidOperationException("Map economy setup is missing.");
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("battlefieldLayout").objectReferenceValue = layout;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (string name in new[] { "BlueTestUnits", "RedTestUnits", "CombatTestUnits" })
                map.transform.Find(name)?.gameObject.SetActive(false);
            Camera camera = Camera.main;
            camera.transform.position = new Vector3(17f, 16f, -10f);
            camera.orthographicSize = 12f;
            var cameraSettings = new SerializedObject(camera.GetComponent<MapCameraController>());
            cameraSettings.FindProperty("playableBounds").rectValue = new Rect(0,0,BattlefieldLayout.Width,BattlefieldLayout.Height);
            cameraSettings.FindProperty("maximumZoom").floatValue = BattlefieldLayout.Height * .5f;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(camera);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            BattlefieldMapValidation.ValidateEditMode();
            RenderOverview();
            Debug.Log("Two Kingdoms battlefield saved: two-tier Castle bases, two routes per faction, flat central mine and drifting Tiny Swords clouds.");
        }

        private static BattlefieldLayout MakeLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<BattlefieldLayout>();
                AssetDatabase.CreateAsset(layout, LayoutPath);
            }
            layout.blueCastle = BattlefieldLayout.BlueCastleStart;
            layout.redCastle = BattlefieldLayout.RedCastleStart;
            layout.bluePawns = new[] { new Vector2(11.5f,14.5f), new Vector2(12.5f,14.5f), new Vector2(13.5f,14.5f) };
            layout.redPawns = new[] { new Vector2(46.5f,30.5f), new Vector2(47.5f,30.5f), new Vector2(48.5f,30.5f) };
            layout.redPawnAnimations = MakeRedPawnSet();
            var resources = new List<BattlefieldResource>();
            void Add(string name, ResourceType type, Vector2 p, int variant)
                => resources.Add(new BattlefieldResource { name=name, type=type, position=p, variant=variant });
            Vector2 Mirror(Vector2 p) => new(BattlefieldLayout.Width-p.x,BattlefieldLayout.Height-p.y);
            Vector2[] forest = { new(17.5f,24.5f),new(19.5f,25.5f),new(21.5f,24.5f),
                new(18.5f,27.5f),new(20.5f,28.5f),new(22.5f,27.5f),
                new(17.5f,5.5f),new(19.5f,4.5f),new(21.5f,5.5f),
                new(18.5f,3.5f),new(20.5f,3.5f),new(22.5f,4.5f) };
            // Twelve Sheep above and twelve below each base, all on the low meadows.
            // Stagger their positions; the plateau remains clear for construction.
            Vector2[] sheep = { new(7.5f,23.5f),new(11.5f,22.5f),new(14.5f,24.5f),new(18.5f,22.5f),
                new(7.5f,4.5f),new(10.5f,6.5f),new(13.5f,4.5f),new(16.5f,6.5f),
                new(5.5f,25.5f),new(9.5f,25.5f),new(16.5f,23.5f),new(20.5f,22.5f),
                new(5.5f,5.5f),new(9.5f,3.5f),new(14.5f,6.5f),new(18.5f,6.5f),
                new(6.5f,22.5f),new(12.5f,25.5f),new(15.5f,22.5f),new(19.5f,24.5f),
                new(6.5f,3.5f),new(8.5f,6.5f),new(12.5f,5.5f),new(15.5f,3.5f) };
            Vector2[] gold = { new(22.5f,10.5f),new(23.5f,12.5f),new(22.5f,8.5f) };
            for(int side=0;side<2;side++)
            {
                string name=side==0?"Blue":"Red";
                for(int i=0;i<forest.Length;i++)
                    Add(name+"_Wood_"+(i+1),ResourceType.Wood,
                        side==0?forest[i]:Mirror(forest[i]),i%4);
                for(int i=0;i<sheep.Length;i++)
                    Add(name+"_Sheep_"+(i+1),ResourceType.Food,
                        side==0?sheep[i]:Mirror(sheep[i]),i);
                for(int i=0;i<gold.Length;i++)
                    Add(name+"_Gold_"+(i+1),ResourceType.Gold,
                        side==0?gold[i]:Mirror(gold[i]),i);
            }
            Vector2[] centralGold = { new(24.5f,24.5f),new(25.5f,26.5f),new(34.5f,21.5f),
                new(35.5f,20.5f),new(27.5f,26.5f),new(32.5f,26.5f) };
            for(int i=0;i<centralGold.Length;i++) Add("Central_Gold_"+(i+1),ResourceType.Gold,centralGold[i],3+i%3);
            Vector2[] groves = { new(5.5f,29.5f),new(7.5f,30.5f),new(9.5f,29.5f),new(6.5f,33.5f),new(8.5f,34.5f),
                new(11.5f,32.5f),new(13.5f,34.5f),new(15.5f,32.5f),new(12.5f,37.5f),new(16.5f,37.5f),
                new(19.5f,35.5f),new(21.5f,37.5f),new(23.5f,34.5f),new(25.5f,38.5f),new(27.5f,40.5f),
                new(32.5f,40.5f),new(34.5f,38.5f),new(35.5f,34.5f),new(36.5f,37.5f),new(24.5f,31.5f) };
            for(int i=0;i<groves.Length;i++)
            {
                Add("North_Grove_"+i,ResourceType.Wood,groves[i],i%4);
                Add("South_Grove_"+i,ResourceType.Wood,Mirror(groves[i]),(i+2)%4);
            }
            Vector2[] wildSheep = { new(12.5f,28.5f),new(15.5f,30.5f),new(47.5f,15.5f),new(44.5f,13.5f),
                new(8.5f,27.5f),new(10.5f,28.5f),new(13.5f,30.5f),new(16.5f,29.5f),
                new(51.5f,16.5f),new(49.5f,15.5f),new(46.5f,13.5f),new(43.5f,14.5f) };
            for(int i=0;i<wildSheep.Length;i++) Add("Meadow_Sheep_"+i,ResourceType.Food,wildSheep[i],i);
            // Dense, mirrored woodland uses real gatherable Trees. Leave the
            // Castle skirts, Sheep pastures and the central battle lane open.
            var forestRandom=new System.Random(270926);
            bool CanPlant(Vector2 p)
            {
                if(!BattlefieldLayout.IsLand((int)p.x,(int)p.y)
                    || BattlefieldLayout.IsShoreline((int)p.x,(int)p.y)
                    || IsBattleLane(p)) return false;
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                    if(BattlefieldTerraces.Covers((int)p.x+dx,(int)p.y+dy)) return false;
                return resources.All(r=>Vector2.Distance(r.position,p)>=(r.type==ResourceType.Wood?.88f:1.7f)
                    && ((int)r.position.x!=(int)p.x || (int)r.position.y!=(int)p.y));
            }
            int groveIndex=0;
            for(int y=22;y<BattlefieldLayout.Height-2;y++)
            for(int x=3;x<BattlefieldLayout.Width-3;x+=2)
            {
                // Cross aisles every four rows and a clear column between stands
                // keep dense woodland harvestable without enclosing interior trees.
                if(y%4==0 || forestRandom.NextDouble()>.93) continue;
                var p=new Vector2(x+.5f+(float)forestRandom.NextDouble()*.24f-.12f,
                    y+.5f+(float)forestRandom.NextDouble()*.12f-.06f);
                Vector2 opposite=Mirror(p);
                if(!CanPlant(p) || !CanPlant(opposite)) continue;
                Add("Woodland_North_"+groveIndex,ResourceType.Wood,p,groveIndex%4);
                Add("Woodland_South_"+groveIndex,ResourceType.Wood,opposite,(groveIndex+2)%4);
                groveIndex++;
            }
            // Small, equal resource pockets on the edges of each Castle base.
            // Keep the middle courts and both stair approaches free for building.
            Vector2[] rimTrees = { new(8.5f,20.5f),new(18.5f,20.5f),new(5.5f,9.5f),new(20.5f,9.5f) };
            Vector2[] rimSheep = { new(6.5f,8.5f),new(8.5f,9.5f),new(17.5f,8.5f),new(19.5f,9.5f) };
            Vector2[] rimGold = { new(8.5f,16.5f),new(17.5f,12.5f) };
            for(int side=0;side<2;side++)
            {
                string name=side==0?"Blue_Rim_":"Red_Rim_";
                Vector2 offset=side==0?Vector2.zero:new Vector2(34,16);
                for(int i=0;i<rimTrees.Length;i++)
                    Add(name+"Wood_"+i,ResourceType.Wood,rimTrees[i]+offset,i);
                for(int i=0;i<rimSheep.Length;i++)
                    Add(name+"Sheep_"+i,ResourceType.Food,rimSheep[i]+offset,i);
                for(int i=0;i<rimGold.Length;i++)
                    Add(name+"Gold_"+i,ResourceType.Gold,rimGold[i]+offset,i==0?4:1);
            }
            // Blue Castle edge planting: two dark pines per tier and five upper-court Sheep.
            Vector2[] terracePines = { new(7.5f,9.5f),new(18.5f,9.5f),new(8.5f,18.5f),new(17.5f,18.5f) };
            Vector2[] courtSheep = { new(10.5f,19.5f),new(12.5f,18.5f),new(14.5f,19.5f),
                new(8.5f,12.5f),new(17.5f,14.5f) };
            for(int i=0;i<terracePines.Length;i++)
                Add("Blue_Terrace_Wood_"+i,ResourceType.Wood,terracePines[i],0);
            for(int i=0;i<courtSheep.Length;i++)
                Add("Blue_Terrace_Sheep_"+i,ResourceType.Food,courtSheep[i],i);
            layout.resources = resources.ToArray();
            EditorUtility.SetDirty(layout);
            return layout;
        }

        private static bool IsBattleLane(Vector2 p)
        {
            float Segment(Vector2 a,Vector2 b)
            {
                Vector2 delta=b-a;
                float t=Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude);
                return Vector2.Distance(p,a+t*delta);
            }
            return (p.x>24 && p.x<36 && p.y>18 && p.y<29)
                || Segment(new(21.5f,15.5f),new(28,21))<2.4f
                || Segment(new(38.5f,29.5f),new(32,23))<2.4f;
        }

        private static PawnAnimationSet MakeRedPawnSet()
        {
            const string path = "Assets/UnitAnimations/RedPawn.asset";
            PawnAnimationSet blue = AssetDatabase.LoadAssetAtPath<PawnAnimationSet>("Assets/UnitAnimations/BluePawn.asset");
            PawnAnimationSet red = AssetDatabase.LoadAssetAtPath<PawnAnimationSet>(path);
            if (red == null) { red = Object.Instantiate(blue); AssetDatabase.CreateAsset(red, path); }
            EditorUtility.CopySerialized(blue, red);
            red.name = "RedPawn";
            var serialized = new SerializedObject(red);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference
                    || property.objectReferenceValue is not Sprite source) continue;
                string sourcePath = AssetDatabase.GetAssetPath(source);
                string redPath = sourcePath.Replace("/Blue Units/", "/Red Units/");
                Sprite counterpart = AssetDatabase.LoadAllAssetsAtPath(redPath)
                    .OfType<Sprite>().FirstOrDefault(sprite => sprite.name == source.name);
                if (counterpart == null) throw new InvalidOperationException("Missing Red Pawn frame: " + source.name);
                property.objectReferenceValue = counterpart;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(red);
            return red;
        }

        private static void PaintClearings(Tilemap layer)
        {
            var ochre = new HashSet<Vector2Int>();
            void Ellipse(float cx, float cy, float rx, float ry)
            {
                for (int y = 2; y < BattlefieldLayout.Height-2; y++)
                for (int x = 2; x < BattlefieldLayout.Width-2; x++)
                    if (Mathf.Pow((x + .5f - cx) / rx, 2) + Mathf.Pow((y + .5f - cy) / ry, 2) <= 1)
                        ochre.Add(new Vector2Int(x, y));
            }
            void Road(Vector2 a, Vector2 b)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(a,b) * 3);
                for (int i = 0; i <= steps; i++)
                { Vector2 p = Vector2.Lerp(a,b,(float)i/steps); Ellipse(p.x,p.y,1.65f,1.4f); }
            }
            Ellipse(30,22,5.5f,4f);
            Road(new(21.5f,15.5f),new(28,21));
            Road(new(38.5f,29.5f),new(32,23));
            Road(new(21.5f,15.5f),new(22,9)); Road(new(38.5f,29.5f),new(38,35));
            PaintMask(layer, ochre, 4);
            var meadow = new HashSet<Vector2Int>();
            foreach (Vector2 center in new[] {new Vector2(11,24),new Vector2(13,5),new Vector2(49,20),new Vector2(47,39),
                new Vector2(10,32),new Vector2(50,12),new Vector2(20,30),new Vector2(40,14)})
                for (int y = 3; y < BattlefieldLayout.Height-3; y++)
                for (int x = 3; x < BattlefieldLayout.Width-3; x++)
                    if (Mathf.Pow((x-center.x)/6f,2)+Mathf.Pow((y-center.y)/2.7f,2)<1
                        && !ochre.Contains(new(x,y))) meadow.Add(new(x,y));
            PaintMask(layer, meadow, 3);
            var coastalGrass = new HashSet<Vector2Int>();
            foreach (Vector2 center in new[] {new Vector2(5,32),new Vector2(55,12),new Vector2(29,38),new Vector2(31,6)})
                for (int y = 3; y < BattlefieldLayout.Height-3; y++)
                for (int x = 3; x < BattlefieldLayout.Width-3; x++)
                    if (Mathf.Pow((x-center.x)/4.8f,2)+Mathf.Pow((y-center.y)/2.7f,2)<1
                        && !ochre.Contains(new(x,y)) && !meadow.Contains(new(x,y))) coastalGrass.Add(new(x,y));
            PaintMask(layer, coastalGrass, 5);
        }

        private static void PaintMask(Tilemap layer, HashSet<Vector2Int> mask, int color)
        {
            foreach (Vector2Int p in mask)
            {
                if (!BattlefieldLayout.IsLand(p.x,p.y) || BattlefieldLayout.IsShoreline(p.x,p.y)
                    || BattlefieldTerraces.Covers(p.x,p.y)) continue;
                int row = !mask.Contains(p+Vector2Int.up) ? 0 : !mask.Contains(p+Vector2Int.down) ? 2 : 1;
                int col = !mask.Contains(p+Vector2Int.left) ? 0 : !mask.Contains(p+Vector2Int.right) ? 2 : 1;
                layer.SetTile(new Vector3Int(p.x,p.y,0), Tile(color,row,col));
            }
        }

        private static void PaintScenery(Transform root, BattlefieldLayout layout)
        {
            // Fixed seed is for decorative scatter only; this is one authored map.
            var random = new System.Random(260926);
            for (int i = 0; i < 680; i++)
            {
                float x = 3.2f + (float)random.NextDouble()*(BattlefieldLayout.Width-6.4f);
                float y = 3.3f + (float)random.NextDouble()*(BattlefieldLayout.Height-6.6f);
                if (!BattlefieldLayout.IsLand((int)x,(int)y)
                    || BattlefieldTerraces.Covers((int)x,(int)y)
                    || Vector2.Distance(new(x,y),layout.blueCastle)<4
                    || Vector2.Distance(new(x,y),layout.redCastle)<4
                    || layout.resources.Any(r=>Vector2.Distance(r.position,new(x,y))<.65f)) continue;
                int variant = i % 4;
                Sprite[] frames = LoadFrames($"Assets/Terrain/Decorations/Bushes/Bushe{variant+1}.png");
                PlaceBush(root,"GroundCover_"+i,frames,new(x,y),variant,i*.037f);
            }
            // Native dark grass tufts sit in compact, nonblocking clusters near the Blue court edges.
            Vector2[] grassCenters = { new(7.65f,17.8f),new(18.35f,19.1f),new(10f,8.25f),new(15.5f,8.25f) };
            Vector2[] grassOffsets = { new(-.28f,-.1f),new(.25f,0),new(.03f,.3f) };
            Sprite[] grass=LoadFrames("Assets/Terrain/Decorations/Bushes/Bushe3.png");
            for(int cluster=0;cluster<grassCenters.Length;cluster++)
                for(int tuft=0;tuft<grassOffsets.Length;tuft++)
                    PlaceBush(root,$"Blue_Terrace_Grass_{cluster}_{tuft}",grass,
                        grassCenters[cluster]+grassOffsets[tuft],2,(cluster*3+tuft)*.071f);
            Sprite[][] waterFrames=Enumerable.Range(1,4).Select(i=>BattlefieldAmbientAssets.Frames(
                $"Assets/Terrain/Decorations/Rocks in the Water/Water Rocks_0{i}.png",64,16)).ToArray();
            Sprite[] rockSprites=Enumerable.Range(1,4).Select(i=>First($"Assets/Terrain/Decorations/Rocks/Rock{i}.png")).ToArray();
            var coast=new List<Vector2>();
            for(int x=4;x<57;x+=5) { coast.Add(new(x,1.15f)); coast.Add(new(60-x,42.8f)); }
            for(int y=6;y<40;y+=5) { coast.Add(new(1.05f,y)); coast.Add(new(58.9f,44-y)); }
            coast.AddRange(new Vector2[]{new(3,3),new(5,41),new(57,3),new(55,41),
                new(27.7f,36.6f),new(32.3f,38.3f),new(27.7f,5.6f),new(32.3f,7.3f),
                new(20,.6f),new(43,43.3f),new(.7f,18),new(59.3f,26)});
            for(int i=0;i<coast.Count;i++)
            {
                PlaceAnimated(root,"WaterRock_"+i,waterFrames[i%4],coast[i],2,1.1f+i%3*.15f,i*.13f);
                if(i<36 && i%3==0) Place(root,"CoastalBoulder_"+i,rockSprites[i%4],
                    coast[i]+new Vector2(.28f,.18f),6,1.2f);
            }

            int rockIndex=0;
            foreach(BattlefieldResource tree in layout.resources.Where(r=>r.type==ResourceType.Wood))
            {
                Vector2 p=tree.position+new Vector2(rockIndex%2==0?.7f:-.7f,-.28f);
                if(!BattlefieldLayout.IsLand((int)p.x,(int)p.y) || BattlefieldTerraces.Covers((int)p.x,(int)p.y)
                    || IsBattleLane(p) || layout.resources.Any(r=>Vector2.Distance(r.position,p)<.6f)) continue;
                Place(root,"ForestStone_"+rockIndex,rockSprites[rockIndex%4],p,6,.9f+rockIndex%3*.12f);
                if(++rockIndex==64) break;
            }

            Sprite[] ducks=BattlefieldAmbientAssets.Frames("Assets/Terrain/Decorations/Rubber Duck/Rubber duck.png",32,3);
            Vector2[] duckPositions={new(28.4f,37.8f),new(29.5f,36.8f),new(30.7f,38),new(31.5f,36.4f),
                new(28.4f,6.9f),new(29.5f,5.8f),new(30.7f,7),new(31.5f,5.5f),
                new(4.5f,41),new(5.7f,41.6f),new(54.8f,2.5f),new(56,3.1f),
                new(1.3f,14.5f),new(1.4f,16),new(58.7f,30),new(58.8f,32)};
            for(int i=0;i<duckPositions.Length;i++)
                PlaceAnimated(root,"Duck_"+i,ducks,duckPositions[i],3,1.6f,i*.071f)
                    .GetComponent<SpriteRenderer>().flipX=i%2==1;

            Sprite[] clouds=Enumerable.Range(1,8).Select(i=>First($"Assets/Terrain/Decorations/Clouds/Clouds_0{i}.png")).ToArray();
            Vector2[] cloudPositions={new(8,3),new(20,3.3f),new(40,3),new(52,3.4f),
                new(9,41),new(21,41),new(39,41),new(51,41),
                new(10,30),new(20,34),new(40,10),new(50,14),new(27,30),new(33,14),
                new(30,37.4f),new(30,6.4f)};
            for(int i=0;i<cloudPositions.Length;i++)
            {
                bool pond=i>=14;
                int variant=pond?(i==14?2:7):i%8;
                GameObject cloud=Place(root,"Cloud_"+i,clouds[variant],cloudPositions[i],19,
                    variant is 2 or 7?1.8f:1.1f);
                cloud.GetComponent<SpriteRenderer>().color=new Color(1,1,1,pond?.62f:i>=8?.72f:.9f);
                cloud.AddComponent<AmbientCloudDrift>().Configure(
                    new Rect(1,1,BattlefieldLayout.Width-2,BattlefieldLayout.Height-2),
                    new Vector2(pond?1.2f:2f,pond?.15f:.3f),48+i%6*6,i*.83f);
            }
        }

        private static void MakePreview(Transform parent, BattlefieldLayout layout)
        {
            Transform root = ReplaceRoot(parent,"Battlefield_EditorPreview");
            root.gameObject.tag = "EditorOnly";
            root.gameObject.AddComponent<BattlefieldScenePreview>();
            Place(root,"Blue_Main_Castle",First("Assets/Buildings/Blue Buildings/Castle.png"),layout.blueCastle,10,1);
            Place(root,"Red_Main_Castle",First("Assets/Buildings/Red Buildings/Castle.png"),layout.redCastle,10,1);
            PresentationAssetCatalog catalog = PresentationAssetCatalog.Load();
            foreach(BattlefieldResource item in layout.resources)
            {
                Sprite sprite = item.type == ResourceType.Wood ? catalog.Tree(item.variant)
                    : item.type == ResourceType.Gold ? catalog.Gold(item.variant) : catalog.SheepIdle[0];
                float scale = item.type == ResourceType.Gold
                    ? item.variant == 5 ? 1.55f : 1.4f
                    : UnitVisualPresentation.Scale;
                Vector2 position = item.position;
                if(item.type == ResourceType.Wood)
                    position.y += sprite.rect.height*.5f/sprite.pixelsPerUnit*scale;
                Place(root,item.name,sprite,position,10,scale);
            }
            Place(root,"Central_Gold_Mine",Phase6C2AssetCatalog.Load().GoldMineInactive,
                new(StrategicObjectiveMapLayout.MineOriginX+1,StrategicObjectiveMapLayout.MineOriginY+1+GoldMineObjective.VisualGroundOffsetY),10,GoldMineObjective.VisualScale);
            PawnAnimationSet blue = AssetDatabase.LoadAssetAtPath<PawnAnimationSet>("Assets/UnitAnimations/BluePawn.asset");
            foreach(Vector2 p in layout.bluePawns)
                Place(root,"Blue_Pawn",blue.Idle[0],p,10,UnitVisualPresentation.Scale);
            foreach(Vector2 p in layout.redPawns)
                Place(root,"Red_Pawn",layout.redPawnAnimations.Idle[0],p,10,UnitVisualPresentation.Scale);
        }

        private static Sprite First(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer == null) throw new InvalidOperationException("Missing Tiny Swords art: "+path);
            // Clouds are single illustrations; automatic slicing splits detached wisps.
            if(path.Contains("/Clouds/") && (importer.spriteImportMode != SpriteImportMode.Single
                || importer.spritePixelsPerUnit!=64))
            { importer.spriteImportMode=SpriteImportMode.Single; importer.spritePixelsPerUnit=64; importer.SaveAndReimport(); }
            if(importer.filterMode!=FilterMode.Point || importer.textureCompression!=TextureImporterCompression.Uncompressed
                || importer.mipmapEnabled || importer.wrapMode!=TextureWrapMode.Clamp)
            {
                importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled=false; importer.wrapMode=TextureWrapMode.Clamp; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name).First();
        }

        private static Sprite[] LoadFrames(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite =>
                {
                    int split = sprite.name.LastIndexOf('_');
                    return split >= 0 && int.TryParse(sprite.name[(split + 1)..], out int value)
                        ? value : int.MaxValue;
                }).ToArray();

        private static GameObject Place(Transform parent,string name,Sprite sprite,Vector2 p,int order,float scale)
        {
            var obj=new GameObject(name); obj.transform.SetParent(parent,false);
            obj.transform.position=p; obj.transform.localScale=Vector3.one*scale;
            var renderer=obj.AddComponent<SpriteRenderer>(); renderer.sprite=sprite; renderer.sortingOrder=order;
            if (!name.StartsWith("Cloud_"))
                GroundSorting.ApplyStatic(renderer, obj.transform.position, 0f);
            return obj;
        }

        private static GameObject PlaceBush(Transform parent, string name, Sprite[] frames,
            Vector2 position, int variant, float phase)
        {
            GameObject obj = Place(parent, name, frames[0], position, 0, 1f);
            SpriteRenderer source = obj.GetComponent<SpriteRenderer>();
            BushPresentation bush = BushPresentation.Ensure(obj, source, frames, variant, phase);
            GroundSorting.ApplyStatic(bush.Visual, obj.transform.position, 0f);
            return obj;
        }

        private static GameObject PlaceAnimated(Transform parent,string name,Sprite[] frames,Vector2 p,int order,float scale,float phase)
        {
            GameObject obj=Place(parent,name,frames[0],p,order,scale);
            obj.AddComponent<AmbientSpriteLoop>().Configure(frames,phase);
            return obj;
        }

        private static Transform ReplaceRoot(Transform parent,string name)
        {
            Transform old=parent.Find(name); if(old!=null) Object.DestroyImmediate(old.gameObject);
            var obj=new GameObject(name); obj.transform.SetParent(parent,false); return obj.transform;
        }

        private static Tilemap Layer(Transform parent,string name,int order)
        {
            Transform root=ReplaceRoot(parent,name);
            Tilemap map=root.gameObject.AddComponent<Tilemap>();
            root.gameObject.AddComponent<TilemapRenderer>().sortingOrder=order;
            return map;
        }

        private static Tile Tile(int color,int row,int col)
        {
            string name=$"Tilemap_color{color}_r{row}c{col}";
            if(!tiles.TryGetValue(name,out Tile tile))
                tiles[name]=tile=AssetDatabase.LoadAssetAtPath<Tile>($"Assets/Tiles/Terrain/Color{color}/{name}.asset");
            if(tile==null) throw new InvalidOperationException("Missing tile "+name);
            return tile;
        }

        [MenuItem("Tools/Tiny Kingdom/Export Battlefield Overview")]
        public static void RenderOverview()
        {
            Camera camera=Camera.main;
            Vector3 position=camera.transform.position; float zoom=camera.orthographicSize;
            RenderTexture oldTarget=camera.targetTexture; RenderTexture oldActive=RenderTexture.active;
            int width=BattlefieldLayout.Width*40, height=BattlefieldLayout.Height*40;
            var target=new RenderTexture(width,height,24);
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.transform.position=new Vector3(BattlefieldLayout.Width*.5f,BattlefieldLayout.Height*.5f,-10);
                camera.orthographicSize=BattlefieldLayout.Height*.5f;
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
                Directory.CreateDirectory("Documentation");
                File.WriteAllBytes("Documentation/TwoKingdoms-overview.png",image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=oldTarget; RenderTexture.active=oldActive;
                camera.transform.position=position; camera.orthographicSize=zoom;
                Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
