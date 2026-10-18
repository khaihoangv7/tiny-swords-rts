#pragma warning disable 0618
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyKingdom.Buildings;
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
    public static class GoblinIslandMapBuilder
    {
        public const string ScenePath="Assets/Scenes/GoblinIsland.unity";
        public const string LayoutPath="Assets/Data/GoblinIslandLayout.asset";
        public const string NavigationPath="Assets/Data/Navigation/GoblinIslandNavigation.asset";
        private const string Art="Assets/Terrain/GoblinIsland";
        private const string Torch="Assets/Units/Goblins Units/Goblins_Torch/Torch_Yellow.png";
        private const string Tnt="Assets/Units/Goblins Units/Goblins_TNT/TNT_Red.png";
        private const string Barrel="Assets/Units/Goblins Units/Goblins_Barrel/Barrel_Red.png";

        [MenuItem("Tools/Tiny Kingdom/Build Map 3 - Goblin Island")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            GameObject[] guards=PrepareGuards();
            PrepareBridge();
            GoblinHutData hutData=PrepareHuts();
            BattlefieldLayout layout=BuildLayout();
            NavigationGridData nav=GetOrCreate<NavigationGridData>(NavigationPath);
            var cells=new List<NavigationCellData>();
            for(int y=0;y<GoblinIslandLayout.Height;y++)
            for(int x=0;x<GoblinIslandLayout.Width;x++) cells.Add(GoblinIslandLayout.Cell(x,y));
            nav.Configure(GoblinIslandLayout.Width,GoblinIslandLayout.Height,Vector2.zero,1f,cells);
            EditorUtility.SetDirty(nav); AssetDatabase.SaveAssets();
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null
                && !AssetDatabase.CopyAsset("Assets/Scenes/Map.unity",ScenePath))
                throw new InvalidOperationException("The original gameplay scene is missing.");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            layout=AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(LayoutPath);
            nav=AssetDatabase.LoadAssetAtPath<NavigationGridData>(NavigationPath);
            hutData=AssetDatabase.LoadAssetAtPath<GoblinHutData>("Assets/Data/GoblinHutData.asset");
            if(hutData==null || hutData.intact==null || hutData.wreck==null)
                throw new InvalidOperationException("Goblin hut presentation data is incomplete.");
            Transform map=GameObject.Find("Map").transform;
            Transform grid=map.Find("Grid");
            foreach(string old in new[]{"ProjectedTerrainForeground","Battlefield_EditorPreview","BattlefieldScenery","IslandGuards"})
                if(map.Find(old)!=null) Object.DestroyImmediate(map.Find(old).gameObject);
            GridManager navigation=map.GetComponentInChildren<GridManager>(true);
            navigation.SetNavigationData(nav); EditorUtility.SetDirty(navigation);
            PaintTerrain(grid);
            Decorate(map);
            Preview(map,layout,guards);
            var guardRoot=new GameObject("IslandGuards"); guardRoot.transform.SetParent(map,false);
            var spawner=new SerializedObject(guardRoot.AddComponent<IslandGuardSpawner>());
            Put(spawner,"prefabs",GoblinIslandLayout.Guards.Select((p,i)=>guards[i%3]).ToArray());
            var positions=spawner.FindProperty("positions"); positions.arraySize=GoblinIslandLayout.Guards.Length;
            for(int i=0;i<positions.arraySize;i++) positions.GetArrayElementAtIndex(i).vector2Value=GoblinIslandLayout.Guards[i];
            spawner.FindProperty("hutData").objectReferenceValue=hutData;
            var hutCells=spawner.FindProperty("huts"); hutCells.arraySize=GoblinIslandLayout.Huts.Length;
            for(int i=0;i<hutCells.arraySize;i++) hutCells.GetArrayElementAtIndex(i).vector2IntValue=GoblinIslandLayout.Huts[i];
            spawner.ApplyModifiedPropertiesWithoutUndo();
            var economy=new SerializedObject(Object.FindAnyObjectByType<EconomyTestSetup>());
            economy.FindProperty("battlefieldLayout").objectReferenceValue=layout;
            economy.ApplyModifiedPropertiesWithoutUndo();
            Camera camera=Camera.main;
            camera.transform.position=new Vector3(GoblinIslandLayout.BlueCastle.x-1f,8f,-10f); camera.orthographicSize=8f;
            var settings=new SerializedObject(camera.GetComponent<MapCameraController>());
            settings.FindProperty("playableBounds").rectValue=new Rect(0,0,GoblinIslandLayout.Width,GoblinIslandLayout.Height);
            settings.FindProperty("maximumZoom").floatValue=22f; settings.ApplyModifiedPropertiesWithoutUndo();
            Validate(navigation,layout,grid);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath);
            var paths=new[]{"Assets/Scenes/MapSelection.unity","Assets/Scenes/Map.unity","Assets/Scenes/LinearLane.unity",ScenePath};
            EditorBuildSettings.scenes=paths.Select(p=>new EditorBuildSettingsScene(p,true))
                .Concat(EditorBuildSettings.scenes.Where(s=>File.Exists(s.path)&&!paths.Contains(s.path))).ToArray();
            AssetDatabase.SaveAssets();
            ExportOverview();
            Debug.Log("GOBLIN_ISLAND_BUILD_PASSED: three middle-hill guards, nine island guards and three huts; northern hill has no guards.");
        }

        private static T GetOrCreate<T>(string path) where T:ScriptableObject
        {
            T asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset!=null) return asset;
            asset=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,path); return asset;
        }

        // Editor-only placement uses the real traversal rules before accepting denser scenery.
        private sealed class ResourcePlacementGrid : IGridCellProvider
        {
            private readonly GridCell[,] cells=new GridCell[GoblinIslandLayout.Width,GoblinIslandLayout.Height];
            private readonly GridCell start;
            private static readonly Vector2Int[] Directions={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left,
                new(1,1),new(-1,1),new(1,-1),new(-1,-1)};
            public int Width=>GoblinIslandLayout.Width;
            public int Height=>GoblinIslandLayout.Height;
            public bool IsInsideGrid(int x,int y)=>x>=0&&x<Width&&y>=0&&y<Height;
            public GridCell GetCell(int x,int y)=>IsInsideGrid(x,y)?cells[x,y]:null;
            private static Vector2Int Coordinate(Vector2 p)=>new(Mathf.FloorToInt(p.x),Mathf.FloorToInt(p.y));

            public ResourcePlacementGrid(BattlefieldLayout layout,List<BattlefieldResource> resources)
            {
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                {
                    var data=GoblinIslandLayout.Cell(x,y);
                    cells[x,y]=new GridCell(x,y,new Vector3(x+0.5f,y+0.5f),data.terrainType,data.heightLevel,
                        data.hasHeightConnection?data.heightConnection:null);
                }
                foreach(var p in GoblinIslandLayout.Huts.Append(layout.mineOrigin))
                    for(int y=p.y;y<p.y+2;y++) for(int x=p.x;x<p.x+2;x++) cells[x,y].SetOccupied(true);
                foreach(var castle in new[]{layout.blueCastle,layout.redCastle})
                {
                    var p=Coordinate(castle);
                    for(int y=p.y;y<p.y+2;y++) for(int x=p.x-2;x<=p.x+2;x++) cells[x,y].SetOccupied(true);
                }
                foreach(var node in resources.Where(r=>r.type!=ResourceType.Food))
                { var p=Coordinate(node.position); cells[p.x,p.y].SetOccupied(true); }
                var from=Coordinate(layout.bluePawns[0]); start=cells[from.x,from.y];
            }

            public bool TryReserve(Vector2Int p,ResourceType type,List<BattlefieldResource> resources)
            {
                GridCell candidate=GetCell(p.x,p.y);
                if(candidate==null || candidate.Occupied) return false;
                bool blocking=type!=ResourceType.Food;
                if(blocking) candidate.SetOccupied(true);
                var reached=new HashSet<GridCell>{start}; var queue=new Queue<GridCell>(); queue.Enqueue(start);
                while(queue.Count>0)
                {
                    var current=queue.Dequeue();
                    foreach(var d in Directions)
                    {
                        GridCell next=GetCell(current.GridX+d.x,current.GridY+d.y);
                        if(next!=null && !reached.Contains(next) && AStarPathfinder.CanTraverseStep(this,current,next))
                        { reached.Add(next); queue.Enqueue(next); }
                    }
                }
                bool CanHarvest(Vector2Int node)
                {
                    int height=GetCell(node.x,node.y).HeightLevel;
                    return Directions.Take(4).Any(d=>
                    {
                        var work=GetCell(node.x+d.x,node.y+d.y);
                        return work!=null && work.HeightLevel==height && TerrainRules.IsStableStandingCell(work) && reached.Contains(work);
                    });
                }
                bool valid=CanHarvest(p) && resources.All(r=>CanHarvest(Coordinate(r.position)))
                    && GoblinIslandLayout.HillClearings.All(c=>reached.Contains(GetCell(c.x,c.y)))
                    && Enumerable.Range(0,GoblinIslandLayout.ConnectorCount).All(i=>
                    {
                        var stair=GoblinIslandLayout.Connector(i);
                        return reached.Contains(GetCell(stair.LowerX,stair.LowerY))
                            && reached.Contains(GetCell(stair.UpperX,stair.UpperY));
                    });
                if(!valid && blocking) candidate.SetOccupied(false);
                return valid;
            }
        }

        private static BattlefieldLayout BuildLayout()
        {
            var layout=GetOrCreate<BattlefieldLayout>(LayoutPath);
            var original=AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(BattlefieldMapBuilder.LayoutPath);
            layout.redPawnAnimations=original.redPawnAnimations;
            layout.blueCastle=GoblinIslandLayout.BlueCastle; layout.redCastle=GoblinIslandLayout.RedCastle;
            layout.mineOrigin=GoblinIslandLayout.MineOrigin;
            layout.bluePawns=new[]{new Vector2(45.5f,7.5f),new Vector2(47.5f,7.5f),new Vector2(49.5f,7.5f)}
                .Select(GoblinIslandLayout.Shift).ToArray();
            layout.redPawns=layout.bluePawns.Select(p=>p+Vector2.up*31f).ToArray();
            var list=new List<BattlefieldResource>(); var used=new HashSet<Vector2Int>();
            var blocked=new HashSet<Vector2Int>();
            ResourcePlacementGrid placementGrid=null;
            bool Reserved(Vector2Int p)
            {
                if(GoblinIslandLayout.IsApproach(p.x,p.y)) return true;
                foreach(var clearing in GoblinIslandLayout.HillClearings)
                    if(Vector2Int.Distance(p,clearing)<1.1f) return true;
                foreach(Vector2 c in new[]{layout.blueCastle,layout.redCastle})
                    if(Mathf.Abs(p.x+0.5f-c.x)<3.5f && p.y>=c.y-3 && p.y<=c.y+2) return true;
                foreach(Vector2 g in GoblinIslandLayout.Guards)
                    if(Vector2.Distance(g,new Vector2(p.x+0.5f,p.y+0.5f))<1.4f) return true;
                foreach(Vector2Int h in GoblinIslandLayout.Huts)
                    if(new RectInt(h.x-1,h.y-1,4,4).Contains(p)) return true;
                for(int i=0;i<GoblinIslandLayout.ConnectorCount;i++)
                {
                    var stair=GoblinIslandLayout.Connector(i);
                    if(Vector2Int.Distance(p,stair.LowerCell)<1.5f || Vector2Int.Distance(p,stair.UpperCell)<1.5f) return true;
                }
                return false;
            }
            bool Add(string name,ResourceType type,Vector2 pos,int variant)
            {
                if(!name.StartsWith("West_")) pos=GoblinIslandLayout.Shift(pos);
                Vector2Int p=new(Mathf.FloorToInt(pos.x),Mathf.FloorToInt(pos.y));
                var cell=GoblinIslandLayout.Cell(p.x,p.y);
                if(cell.terrainType is not (TerrainType.FlatGround or TerrainType.ElevatedGround)
                    || Reserved(p) || used.Contains(p)) return false;
                if(type==ResourceType.Gold && name.StartsWith("West_"))
                {
                    for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                    {
                        var nearby=GoblinIslandLayout.Cell(p.x+dx,p.y+dy);
                        if(nearby.heightLevel!=cell.heightLevel
                            || nearby.terrainType is not (TerrainType.FlatGround or TerrainType.ElevatedGround)) return false;
                    }
                }
                float spacing=name.StartsWith("West_")?1.5f:1.7f;
                if(type==ResourceType.Wood && (GoblinIslandLayout.GrassColor(p.x,p.y)==4
                    || list.Any(n=>Vector2.Distance(n.position,pos)<spacing))) return false;
                if(type!=ResourceType.Food)
                {
                    int OpenWorkCells(Vector2Int node)
                    {
                        int height=GoblinIslandLayout.Cell(node.x,node.y).heightLevel;
                        return new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right}.Count(d=>
                        {
                            Vector2Int next=node+d; var terrain=GoblinIslandLayout.Cell(next.x,next.y);
                            return next!=p && !blocked.Contains(next) && terrain.heightLevel==height
                                && terrain.terrainType is TerrainType.FlatGround or TerrainType.ElevatedGround;
                        });
                    }
                    if(OpenWorkCells(p)<2) return false;
                    foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                        if(blocked.Contains(p+d) && OpenWorkCells(p+d)==0) return false;
                }
                if(placementGrid!=null && !placementGrid.TryReserve(p,type,list)) return false;
                if(type!=ResourceType.Food) blocked.Add(p);
                used.Add(p);
                list.Add(new BattlefieldResource{name=name,type=type,position=pos,variant=variant});
                return true;
            }
            int TreeVariant(Vector2 p,int index,bool west=false)
            {
                if(!west) p=GoblinIslandLayout.Shift(p);
                int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y);
                if(GoblinIslandLayout.IsIsland(x,y)) return index%3==0?1:0;
                return GoblinIslandLayout.GrassColor(x,y)==3?1:2+index%2;
            }
            // Fixed seeds scatter the authored groves in the editor; there is no runtime map generation.
            for(int side=0;side<2;side++)
            {
                int dy=side*31; string prefix=side==0?"Blue":"Red";
                Vector2[] trees={new(39.3f,9.7f),new(41.2f,10.6f),new(53.7f,10.2f),new(55.2f,8.3f),
                    new(40.3f,2.8f),new(43.4f,2.4f),new(50.2f,2.6f),new(54.1f,3.2f)};
                for(int i=0;i<trees.Length;i++)
                { Vector2 pos=trees[i]+Vector2.up*dy; Add(prefix+"_Tree_"+i,ResourceType.Wood,pos,TreeVariant(pos,i)); }
                Vector2[] gold={new(40.5f,7.5f),new(40.5f,8.5f),new(53.5f,6.5f),new(53.5f,7.5f),new(43.5f,4.5f),new(44.5f,4.5f)};
                for(int i=0;i<gold.Length;i++) Add(prefix+"_Gold_"+i,ResourceType.Gold,gold[i]+Vector2.up*dy,i);
                Vector2[] flock={new(41.3f,3.6f),new(42.5f,4.7f),new(45.4f,3.6f),new(46.5f,4.6f),new(48.4f,3.5f),
                    new(49.3f,4.6f),new(51.6f,3.7f),new(52.4f,4.6f),new(54.4f,3.5f),new(40.4f,4.4f)};
                for(int i=0;i<flock.Length;i++) Add(prefix+"_Sheep_"+i,ResourceType.Food,flock[i]+Vector2.up*dy,0);
                for(int i=0;i<6;i++)
                    Add(prefix+"_MeadowSheep_"+i,ResourceType.Food,new(18.3f+i*1.4f,6.4f+(i%2)*1.3f+dy),0);
                for(int i=0;i<4;i++)
                    Add(prefix+"_OuterGold_"+i,ResourceType.Gold,new(34.5f+i%2*1.3f,4.5f+i/2*1.3f+dy),i+2);
                var random=new System.Random(7301+side); int count=0;
                for(int attempt=0;attempt<8000 && count<24;attempt++)
                {
                    Vector2 pos=new(13.2f+(float)random.NextDouble()*24.5f,1.3f+(float)random.NextDouble()*10.3f+dy);
                    if(Add(prefix+"_Grove_"+count,ResourceType.Wood,pos,TreeVariant(pos,count))) count++;
                }
                if(count!=24) throw new InvalidOperationException("The outer grove is too crowded.");
            }
            Vector2[] goldIsland={new(34.4f,15.4f),new(38.3f,15.7f),new(41.5f,17.2f),new(44.2f,15.5f),
                new(47.3f,16.2f),new(53.4f,16.7f),new(51.2f,27.3f),new(53.3f,28.4f),new(55.1f,26.4f),
                new(37.4f,27.6f),new(35.7f,28.5f),new(22.5f,26.4f)};
            for(int i=0;i<goldIsland.Length;i++) Add("Island_Gold_"+i,ResourceType.Gold,goldIsland[i],i%6);
            var islandRandom=new System.Random(1947);
            for(int i=0;i<16;i++)
            {
                Vector2 pos=new(19.2f+(float)islandRandom.NextDouble()*6f,15.3f+(float)islandRandom.NextDouble()*12.8f);
                Add("Island_Sheep_"+i,ResourceType.Food,pos,0);
            }
            int islandTrees=0;
            for(int attempt=0;attempt<10000 && islandTrees<58;attempt++)
            {
                Vector2 pos=new(15.4f+(float)islandRandom.NextDouble()*40.5f,15.3f+(float)islandRandom.NextDouble()*13.8f);
                if(Add("Island_Pine_"+islandTrees,ResourceType.Wood,pos,TreeVariant(pos,islandTrees))) islandTrees++;
            }
            placementGrid=new ResourcePlacementGrid(layout,list);
            var westRandom=new System.Random(9127);
            Vector2[] goldPockets={new(11,12),new(5,26),new(12,36),new(14.5f,5),new(3,18),new(8,32),new(14,29)};
            int westGold=0;
            for(int attempt=0;attempt<6000 && westGold<32;attempt++)
            {
                Vector2 pos=goldPockets[attempt%goldPockets.Length]+new Vector2(
                    (float)westRandom.NextDouble()*4f-2f,(float)westRandom.NextDouble()*3f-1.5f);
                if(pos.x<1.3f || pos.x>16.7f) continue;
                if(Add("West_Gold_"+westGold,ResourceType.Gold,pos,westGold%6)) westGold++;
            }
            int westSheep=0;
            for(int attempt=0;attempt<4000 && westSheep<50;attempt++)
            {
                Vector2 pos=new(1.4f+(float)westRandom.NextDouble()*15f,1.4f+(float)westRandom.NextDouble()*40f);
                if(Add("West_Sheep_"+westSheep,ResourceType.Food,pos,0)) westSheep++;
            }
            int westTrees=0;
            for(int attempt=0;attempt<12000 && westTrees<85;attempt++)
            {
                Vector2 pos=new(1.4f+(float)westRandom.NextDouble()*15f,1.4f+(float)westRandom.NextDouble()*40f);
                if(Add("West_Grove_"+westTrees,ResourceType.Wood,pos,TreeVariant(pos,westTrees,true))) westTrees++;
            }
            layout.resources=list.ToArray(); EditorUtility.SetDirty(layout); return layout;
        }

        private static Tile Tile(int color,int row,int col) => AssetDatabase.LoadAssetAtPath<Tile>(
            $"Assets/Tiles/Terrain/Color{color}/Tilemap_color{color}_r{row}c{col}.asset")
            ?? throw new InvalidOperationException("Missing native terrain tile.");
        private static Tilemap Layer(Transform grid,string name,int order)
        {
            Transform child=grid.Find(name);
            var obj=child!=null?child.gameObject:new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer));
            obj.transform.SetParent(grid,false); obj.GetComponent<TilemapRenderer>().sortingOrder=order;
            obj.GetComponent<TilemapRenderer>().mode=TilemapRenderer.Mode.Chunk;
            return obj.GetComponent<Tilemap>();
        }

        private static void PaintTerrain(Transform grid)
        {
            foreach(var t in grid.GetComponentsInChildren<Tilemap>()) if(t.name!="BG_Color") t.ClearAllTiles();
            foreach(string obsolete in new[]{"ElevatedGround_L3","Shadow_L3","StairFront_L1","StairFront_L2"})
                if(grid.Find(obsolete)!=null) Object.DestroyImmediate(grid.Find(obsolete).gameObject);
            Tilemap background=Layer(grid,"BG_Color",GroundSorting.TerrainBackgroundOrder(0));
            TileBase water=background.GetTilesBlock(background.cellBounds).FirstOrDefault(t=>t!=null);
            if(water!=null)
                for(int y=-2;y<GoblinIslandLayout.Height+2;y++) for(int x=-2;x<GoblinIslandLayout.Width+2;x++)
                    background.SetTile(new(x,y,0),water);
            Tilemap flat=Layer(grid,"FlatGround",GroundSorting.TerrainBackgroundOrder(2));
            Tilemap foam=Layer(grid,"WaterFoam",GroundSorting.TerrainBackgroundOrder(1));
            Tilemap meadows=Layer(grid,"MeadowsAndPaths",GroundSorting.TerrainBackgroundOrder(3));
            var foamTile=AssetDatabase.LoadAssetAtPath<AnimatedFoamTile>("Assets/Tiles/Water/AnimatedFoam.asset");
            for(int y=0;y<GoblinIslandLayout.Height;y++) for(int x=0;x<GoblinIslandLayout.Width;x++)
            {
                if(!GoblinIslandLayout.IsGround(x,y)) continue;
                int row=!GoblinIslandLayout.IsGround(x,y+1)?0:!GoblinIslandLayout.IsGround(x,y-1)?2:1;
                int col=!GoblinIslandLayout.IsGround(x-1,y)?0:!GoblinIslandLayout.IsGround(x+1,y)?2:1;
                flat.SetTile(new(x,y,0),Tile(GoblinIslandLayout.IsIsland(x,y)?5:1,row,col));
                if(GoblinIslandLayout.IsShore(x,y)) foam.SetTile(new(x,y,0),foamTile);
                int palette=GoblinIslandLayout.GrassColor(x,y);
                if(palette!=1 && palette!=5 && !GoblinIslandLayout.HasRaisedSurface(x,y))
                {
                    bool Same(int px,int py)=>GoblinIslandLayout.IsGround(px,py) && GoblinIslandLayout.GrassColor(px,py)==palette;
                    int pr=!Same(x,y+1)?0:!Same(x,y-1)?2:1;
                    int pc=!Same(x-1,y)?0:!Same(x+1,y)?2:1;
                    meadows.SetTile(new(x,y,0),Tile(palette,pr,pc));
                }
            }
            var surfaces=new Tilemap[GoblinIslandLayout.MaxHeight+1];
            var shadows=new Tilemap[surfaces.Length];
            var shadow=AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/Shadows/Shadow.asset");
            for(int level=1;level<=GoblinIslandLayout.MaxHeight;level++)
            {
                surfaces[level]=Layer(grid,"ElevatedGround_L"+level,GroundSorting.ElevatedTerrainBackgroundOrder(level,true));
                shadows[level]=Layer(grid,"Shadow_L"+level,GroundSorting.ElevatedTerrainBackgroundOrder(level,false));
                for(int y=0;y<GoblinIslandLayout.Height;y++) for(int x=0;x<GoblinIslandLayout.Width;x++)
                {
                    if(GoblinIslandLayout.HasSurface(x,y,level))
                    {
                        int row=!GoblinIslandLayout.HasSurface(x,y+1,level)?0:!GoblinIslandLayout.HasSurface(x,y-1,level)?2:1;
                        int col=!GoblinIslandLayout.HasSurface(x-1,y,level)?5:!GoblinIslandLayout.HasSurface(x+1,y,level)?7:6;
                        surfaces[level].SetTile(new(x,y,0),Tile(GoblinIslandLayout.GrassColor(x,y),row,col));
                        shadows[level].SetTile(new(x,y-1,0),shadow);
                    }
                    else if(GoblinIslandLayout.IsCliff(x,y,level))
                    {
                        int col=!GoblinIslandLayout.HasSurface(x-1,y+1,level)?5:!GoblinIslandLayout.HasSurface(x+1,y+1,level)?7:6;
                        surfaces[level].SetTile(new(x,y,0),Tile(GoblinIslandLayout.GrassColor(x,y+1),5,col));
                        shadows[level].SetTile(new(x,y,0),shadow);
                    }
                }
            }
            for(int i=0;i<GoblinIslandLayout.ConnectorCount;i++)
            {
                var stair=GoblinIslandLayout.Connector(i); int level=stair.LowerHeight+1;
                int color=GoblinIslandLayout.GrassColor(stair.UpperX,stair.UpperY);
                int col=stair.Orientation==TerrainStairOrientation.DescendsLeft?0:3;
                surfaces[level].SetTile(new(stair.VisualX,stair.UpperY,0),Tile(color,4,col));
                surfaces[level].SetTile(new(stair.VisualX,stair.LowerY,0),Tile(color,5,col));
                int row=!GoblinIslandLayout.HasSurface(stair.UpperX,stair.UpperY+1,level)?0:
                    !GoblinIslandLayout.HasSurface(stair.UpperX,stair.UpperY-1,level)?2:1;
                surfaces[level].SetTile(new(stair.UpperX,stair.UpperY,0),Tile(color,row,6));
                if(GoblinIslandLayout.IsCliff(stair.UpperX,stair.LowerY,level))
                    surfaces[level].SetTile(new(stair.UpperX,stair.LowerY,0),Tile(color,5,6));
                shadows[level].SetTile(new(stair.VisualX,stair.LowerY,0),null);
            }
        }

        private static GameObject Place(Transform parent,string name,Sprite sprite,Vector2 p,float scale,int order)
        {
            var obj=new GameObject(name); obj.transform.SetParent(parent,false);
            obj.transform.localPosition=new Vector3(p.x,p.y,0);
            obj.transform.localScale=new Vector3(scale,scale,1);
            var sr=obj.AddComponent<SpriteRenderer>(); sr.sprite=sprite; sr.sortingOrder=order; return obj;
        }
        private static Sprite[] Frames(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s=>{int i=s.name.LastIndexOf('_'); return int.TryParse(s.name.Substring(i+1),out int n)?n:0;}).ToArray();

        private static void PrepareBridge()
        {
            if(!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder("Assets/Terrain","GoblinIsland");
            string path=Art+"/Bridge.png";
            if(!File.Exists(path)) File.Copy("Assets/Terrain/TwoKingdoms/Bridge.png",path);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            Pixel(importer,64); importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.spritesheet=new[]{new SpriteMetaData{name="Bridge_Horizontal",rect=new Rect(16,192,160,64),alignment=0,pivot=new(0.5f,0.5f)},
                new SpriteMetaData{name="Bridge_Vertical",rect=new Rect(0,16,64,160),alignment=0,pivot=new(0.5f,0.5f)}};
            importer.SaveAndReimport();
        }
        private static GoblinHutData PrepareHuts()
        {
            Sprite Import(string path,float contact)
            {
                AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Pixel(importer,100); importer.spriteImportMode=SpriteImportMode.Single;
                var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment=(int)SpriteAlignment.Custom;
                settings.spritePivot=new Vector2(0.5f,contact/192f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport(); return Frames(path)[0];
            }
            var data=GetOrCreate<GoblinHutData>("Assets/Data/GoblinHutData.asset");
            data.intact=Import(Art+"/GoblinHut.png",21);
            data.wreck=Import(Art+"/GoblinHutDestroyed.png",18);
            EditorUtility.SetDirty(data); return data;
        }
        private static void Decorate(Transform map)
        {
            var scenery=new GameObject("BattlefieldScenery"); scenery.transform.SetParent(map,false);
            Sprite[] bridges=Frames(Art+"/Bridge.png");
            for(int i=0;i<3;i++)
            {
                RectInt r=GoblinIslandLayout.Bridges[i];
                Sprite sprite=bridges.Single(s=>s.name==(i==2?"Bridge_Horizontal":"Bridge_Vertical"));
                Place(scenery.transform,"Bridge_"+i,sprite,r.center,GoblinIslandLayout.BridgeVisualScale,GroundSorting.TerrainBackgroundOrder(14));
            }
            DecorateRiverAndStones(scenery.transform,map.GetComponentInChildren<GridManager>());
            int count=0;
            for(int y=3;y<GoblinIslandLayout.Height-2;y+=3) for(int x=3;x<GoblinIslandLayout.Width-2;x+=4)
            {
                if(!GoblinIslandLayout.IsGround(x,y)||GoblinIslandLayout.IsApproach(x,y)
                    || GoblinIslandLayout.Cell(x,y).terrainType!=TerrainType.FlatGround) continue;
                int kind=(x+y)%4+1;
                Place(scenery.transform,"Bush_"+count++,Frames($"Assets/Terrain/Decorations/Bushes/Bushe{kind}.png")[0],
                    new(x+0.35f,y+0.35f),kind%2==0?0.9f:1.15f,GroundSorting.CalculateOrder(
                        map.GetComponentInChildren<GridManager>(),0,y+0.35f));
            }
            Vector2[] cloudPositions={new(8,7),new(18,10),new(8,36),new(19,39),new(22,22),new(48,19),
                new(50,28),new(35,37),new(35,6),new(7,27),new(21,32),new(47,14)};
            for(int i=0;i<cloudPositions.Length;i++)
            {
                int kind=i%8+1; bool small=kind==3||kind==4||kind==8;
                var cloud=Place(scenery.transform,"Cloud_"+i,Frames($"Assets/Terrain/Decorations/Clouds/Clouds_{kind:00}.png")[0],
                    (i==0 || i==2 || i==9)?cloudPositions[i]+Vector2.right*2f:GoblinIslandLayout.Shift(cloudPositions[i]),small?1.8f:1.1f,19);
                cloud.GetComponent<SpriteRenderer>().color=new Color(1,1,1,small?0.64f:0.55f);
                cloud.AddComponent<AmbientCloudDrift>().Configure(new Rect(1,1,GoblinIslandLayout.Width-2,GoblinIslandLayout.Height-2),new(1.3f+i%3*0.3f,0.15f),54+i*3,i*0.61f);
            }
        }

        private static void DecorateRiverAndStones(Transform scenery,GridManager grid)
        {
            Sprite[] ducks=Frames("Assets/Terrain/Decorations/Rubber Duck/Rubber duck.png");
            if(ducks.Length!=3 || ducks.Any(s=>s.rect.width!=32 || s.rect.height!=32))
                throw new InvalidOperationException("Ducks require the three verified complete 32x32 frames.");
            Vector2[] duckPositions={new(17.4f,30.65f),new(22.3f,31.15f),new(36.3f,30.55f),new(47.3f,31.15f),
                new(18.4f,13.15f),new(23.2f,12.65f),new(38.3f,13.25f),new(49.3f,12.75f),
                new(12.65f,16.3f),new(13.25f,26.5f),new(57.55f,18.5f),new(58.25f,26.3f)};
            for(int i=0;i<duckPositions.Length;i++)
            {
                Vector2 p=GoblinIslandLayout.Shift(duckPositions[i]);
                var duck=Place(scenery,"Duck_"+i,ducks[0],p,1.6f,GroundSorting.CalculateOrder(grid,0,p.y));
                duck.GetComponent<SpriteRenderer>().flipX=i%2==1;
                duck.AddComponent<AmbientSpriteLoop>().Configure(ducks,i*0.071f);
            }
            Sprite[][] waterRocks=Enumerable.Range(1,4).Select(i=>Frames(
                $"Assets/Terrain/Decorations/Rocks in the Water/Water Rocks_{i:00}.png")).ToArray();
            if(waterRocks.Any(frames=>frames.Length!=16 || frames.Any(s=>s.rect.width!=64 || s.rect.height!=64)))
                throw new InvalidOperationException("Water rocks require sixteen verified complete 64x64 frames.");
            Vector2[] waterPositions={new(15.4f,31.25f),new(24.5f,30.5f),new(34.2f,31.2f),new(41.5f,30.6f),new(54.5f,31.1f),
                new(15.3f,12.7f),new(25.4f,13.4f),new(33.2f,12.6f),new(43.6f,13.3f),new(54.3f,12.75f),
                new(12.65f,14.8f),new(13.25f,19.6f),new(12.65f,24f),new(13.1f,28.5f),new(58.15f,16.3f),new(57.7f,22.9f)};
            for(int i=0;i<waterPositions.Length;i++)
            {
                Vector2 p=GoblinIslandLayout.Shift(waterPositions[i]); Sprite[] frames=waterRocks[i%4];
                var rock=Place(scenery,"WaterRock_"+i,frames[0],p,0.9f+i%3*0.15f,GroundSorting.CalculateOrder(grid,0,p.y));
                rock.AddComponent<AmbientSpriteLoop>().Configure(frames,i*0.113f);
            }
            Sprite[] stones=Enumerable.Range(1,4).Select(i=>Frames($"Assets/Terrain/Decorations/Rocks/Rock{i}.png")[0]).ToArray();
            Vector2[] stonePositions={new(3.7f,2.8f),new(15.5f,12.7f),new(2.4f,17.2f),new(14.8f,29.8f),new(4.5f,31f),new(14.7f,39.5f),
                new(16.2f,15.6f),new(22.4f,18.4f),new(18.4f,25.6f),new(25.3f,27.4f),new(32.6f,17f),new(34.6f,26.8f),
                new(42.8f,16.5f),new(47.8f,28f),new(54.8f,24.2f),new(54.3f,16.3f),
                new(15.4f,3.6f),new(22.6f,9.6f),new(34.4f,8.8f),new(55.4f,3.9f),
                new(16.3f,34.6f),new(25.3f,40.8f),new(35.4f,36.4f),new(55.5f,35.4f)};
            for(int i=0;i<stonePositions.Length;i++)
            {
                Vector2 p=i<6?stonePositions[i]:GoblinIslandLayout.Shift(stonePositions[i]);
                Place(scenery,"GroundRock_"+i,stones[i%4],p,1.25f+i%4*0.18f,
                    GroundSorting.CalculateOrder(grid,GroundSorting.ElevationAt(grid,p),p.y));
            }
        }

        private static void Preview(Transform map,BattlefieldLayout layout,GameObject[] guards)
        {
            var root=new GameObject("Battlefield_EditorPreview"); root.transform.SetParent(map,false);
            root.tag="EditorOnly"; root.AddComponent<BattlefieldScenePreview>();
            var grid=map.GetComponentInChildren<GridManager>();
            foreach(string side in new[]{"Blue","Red"})
            {
                Vector2 p=side=="Blue"?layout.blueCastle:layout.redCastle;
                var castle=Place(root.transform,side+"_Main_Castle",Frames($"Assets/Buildings/{side} Buildings/Castle.png")[0],p,1,
                    GroundSorting.CalculateOrder(grid,0,p.y));
                BuildingVisualPresentation.Apply(castle.GetComponent<SpriteRenderer>(),BuildingType.Castle,p);
            }
            PresentationAssetCatalog catalog=PresentationAssetCatalog.Load();
            foreach(var node in layout.resources)
            {
                Sprite s=node.type==ResourceType.Wood?catalog.Tree(node.variant):node.type==ResourceType.Gold?catalog.Gold(node.variant):catalog.SheepIdle[0];
                float scale=node.type==ResourceType.Gold?(node.variant==5?1.55f:1.4f):1.6f;
                Vector2 p=node.position;
                if(node.type==ResourceType.Wood) p.y+=s.rect.height*0.5f*scale/s.pixelsPerUnit;
                int order=GroundSorting.CalculateOrder(grid,GroundSorting.ElevationAt(grid,node.position),node.position.y);
                Place(root.transform,node.name,s,p,scale,order);
            }
            var blue=AssetDatabase.LoadAssetAtPath<PawnAnimationSet>("Assets/UnitAnimations/BluePawn.asset");
            foreach(Vector2 p in layout.bluePawns) Place(root.transform,"Blue_Pawn",blue.Idle[0],p,1.6f,GroundSorting.CalculateOrder(grid,0,p.y));
            foreach(Vector2 p in layout.redPawns) Place(root.transform,"Red_Pawn",layout.redPawnAnimations.Idle[0],p,1.6f,GroundSorting.CalculateOrder(grid,0,p.y));
            Vector2 mine=(Vector2)layout.mineOrigin+Vector2.one;
            Place(root.transform,"Island_Gold_Mine",Phase6C2AssetCatalog.Load().GoldMineInactive,
                mine+Vector2.up*GoldMineObjective.VisualGroundOffsetY,GoldMineObjective.VisualScale,
                GroundSorting.CalculateOrder(grid,GroundSorting.ElevationAt(grid,mine),mine.y));
            for(int i=0;i<GoblinIslandLayout.Guards.Length;i++) Place(root.transform,"Guard_"+i,
                guards[i%3].GetComponent<UnitAnimator>().AnimationSet.Idle[0],GoblinIslandLayout.Guards[i],GoblinIslandLayout.GuardVisualScale,
                GroundSorting.CalculateOrder(grid,GroundSorting.ElevationAt(grid,GoblinIslandLayout.Guards[i]),GoblinIslandLayout.Guards[i].y));
            var hutData=AssetDatabase.LoadAssetAtPath<GoblinHutData>("Assets/Data/GoblinHutData.asset");
            for(int i=0;i<GoblinIslandLayout.Huts.Length;i++)
            {
                Vector2 p=(Vector2)GoblinIslandLayout.Huts[i]+Vector2.one;
                Place(root.transform,"Goblin_Hut_"+i,hutData.intact,p,hutData.visualScale,
                    GroundSorting.CalculateOrder(grid,GroundSorting.ElevationAt(grid,p),p.y));
            }
        }

        private static void Pixel(TextureImporter importer,int ppu)
        {
            importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=ppu;
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.wrapMode=TextureWrapMode.Clamp;
            importer.GetSourceTextureWidthAndHeight(out int w,out int h);
            importer.maxTextureSize=Mathf.NextPowerOfTwo(Mathf.Max(w,h));
        }
        private static void Slice(string path,int cell)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); Pixel(importer,100);
            importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.GetSourceTextureWidthAndHeight(out int w,out int h);
            int columns=w/cell; int rows=h/cell; var slices=new SpriteMetaData[columns*rows];
            for(int i=0;i<slices.Length;i++) slices[i]=new SpriteMetaData{
                name=Path.GetFileNameWithoutExtension(path)+"_"+i,rect=new Rect(i%columns*cell,h-(i/columns+1)*cell,cell,cell),
                alignment=0,pivot=new(0.5f,0.5f)};
            importer.spritesheet=slices; importer.SaveAndReimport();
        }
        private static void Put(SerializedObject so,string property,Object[] values)
        {
            var p=so.FindProperty(property); p.arraySize=values.Length;
            for(int i=0;i<values.Length;i++) p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];
        }
        private static UnitAnimationSet Animation(string name,string path,int cell,int idleStart,int idleCount,int runStart,int runCount,int attackStart,int attackCount)
        {
            var set=GetOrCreate<UnitAnimationSet>("Assets/UnitAnimations/"+name+".asset");
            Sprite[] all=Frames(path); var so=new SerializedObject(set);
            so.FindProperty("framesPerSecond").floatValue=10; so.FindProperty("attackFramesPerSecond").floatValue=10;
            so.FindProperty("visualScale").floatValue=GoblinIslandLayout.GuardVisualScale;
            so.FindProperty("frameCellSize").intValue=cell; so.FindProperty("visualAnchorPixels").vector2Value=new(cell/2,cell/2);
            Put(so,"idle",all.Skip(idleStart).Take(idleCount).ToArray()); Put(so,"run",all.Skip(runStart).Take(runCount).ToArray());
            Put(so,"attack",all.Skip(attackStart).Take(attackCount).ToArray());
            foreach(string p in new[]{"guard","attackDown","attackDownRight","attackUp","attackUpRight"}) Put(so,p,Array.Empty<Sprite>());
            if(name=="IslandGoblinTorch")
            { Put(so,"attackDown",all.Skip(21).Take(6).ToArray()); Put(so,"attackDownRight",all.Skip(21).Take(6).ToArray());
                Put(so,"attackUp",all.Skip(28).Take(6).ToArray()); Put(so,"attackUpRight",all.Skip(28).Take(6).ToArray()); }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(set); return set;
        }
        private static GameObject[] PrepareGuards()
        {
            Slice(Torch,192); Slice(Tnt,192); Slice(Barrel,128);
            Slice("Assets/Units/Goblins Units/Goblins_TNT/Dynamite.png",64);
            Slice("Assets/Particle FX/Explosion_03.png",192);
            var torch=Animation("IslandGoblinTorch",Torch,192,0,7,7,6,14,6);
            var tnt=Animation("IslandGoblinTNT",Tnt,192,0,6,7,6,14,7);
            var barrel=Animation("GoblinBarrel",Barrel,128,0,1,24,3,30,3);
            var stats=GetOrCreate<UnitCombatData>("Assets/Data/GoblinBarrelCombatData.asset"); var so=new SerializedObject(stats);
            so.FindProperty("unitType").enumValueIndex=(int)UnitType.GoblinBarrel;
            so.FindProperty("maxHP").intValue=100; so.FindProperty("baseDamage").intValue=60; so.FindProperty("moveSpeed").floatValue=3;
            so.FindProperty("foodCost").intValue=0; so.FindProperty("goldCost").intValue=0; so.FindProperty("populationCost").intValue=0;
            so.FindProperty("autoAcquireTargets").boolValue=false; so.FindProperty("visionRadius").floatValue=0;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(stats);
            var barrelData=GetOrCreate<GoblinBarrelData>("Assets/Data/GoblinBarrelData.asset");
            Sprite[] all=Frames(Barrel); barrelData.emerge=all.Skip(6).Take(6).ToArray(); barrelData.withdraw=all.Skip(18).Take(6).ToArray();
            EditorUtility.SetDirty(barrelData);
            var catalog=Phase6C2AssetCatalog.Load();
            catalog.Set(catalog.Monk(Faction.Blue),catalog.Monk(Faction.Red),torch,tnt,catalog.HealEffect(Faction.Blue),
                catalog.HealEffect(Faction.Red),Frames("Assets/Units/Goblins Units/Goblins_TNT/Dynamite.png"),
                Frames("Assets/Particle FX/Explosion_03.png"),catalog.GoldMineInactive,catalog.GoldMineActive,catalog.GoldMineDestroyed);
            EditorUtility.SetDirty(catalog);
            var result=new GameObject[3];
            var animations=new[]{torch,tnt,barrel};
            var combatData=new[]{AssetDatabase.LoadAssetAtPath<UnitCombatData>("Assets/Data/GoblinTorchCombatData.asset"),
                AssetDatabase.LoadAssetAtPath<UnitCombatData>("Assets/Data/GoblinTntCombatData.asset"),stats};
            for(int i=0;i<3;i++)
            {
                string name=i==0?"IslandGoblinTorch":i==1?"IslandGoblinTNT":"GoblinBarrel";
                string path="Assets/Prefabs/Units/"+name+".prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)
                    AssetDatabase.CopyAsset("Assets/Prefabs/Units/"+(i==1?"GoblinTNT":"GoblinTorch")+".prefab",path);
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.name=name; root.GetComponent<UnitController>().Configure(Faction.Goblin);
                    root.GetComponent<HealthComponent>().Configure(combatData[i]);
                    root.GetComponent<UnitAnimator>().Configure(animations[i]);
                    root.GetComponent<SpriteRenderer>().sprite=animations[i].Idle[0];
                    if(i==2)
                    {
                        foreach(Component c in new Component[]{root.GetComponent<CombatController>(),root.GetComponent<DelayedRegeneration>()})
                            if(c!=null) Object.DestroyImmediate(c,true);
                        (root.GetComponent<GoblinBarrelCombat>()??root.AddComponent<GoblinBarrelCombat>()).Configure(barrelData);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                result[i]=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return result;
        }

        public static void Validate(GridManager grid,BattlefieldLayout layout,Transform visuals)
        {
            var occupied=new HashSet<Vector2Int>();
            var used=new HashSet<Vector2Int>();
            foreach(var r in layout.resources)
            {
                Vector2Int p=grid.WorldToGrid(r.position); GridCell c=grid.GetCell(p.x,p.y);
                if(!TerrainRules.IsStableStandingCell(c)||!used.Add(p)) throw new InvalidOperationException("Invalid resource: "+r.name);
                if(r.type!=ResourceType.Food) occupied.Add(p);
                if(r.type==ResourceType.Wood && (GoblinIslandLayout.IsIsland(p.x,p.y)?r.variant>1:r.variant==0))
                    throw new InvalidOperationException("Tree variant disagrees with the requested biome.");
            }
            foreach(Vector2 castle in new[]{layout.blueCastle,layout.redCastle})
            {
                Vector2Int p=grid.WorldToGrid(castle);
                if(grid.GetCell(p.x,p.y).HeightLevel!=0) throw new InvalidOperationException("Castles must stay on flat ground.");
                for(int y=p.y;y<=p.y+1;y++) for(int x=p.x-2;x<=p.x+2;x++) occupied.Add(new(x,y));
            }
            foreach(Vector2Int h in GoblinIslandLayout.Huts)
                for(int y=h.y;y<h.y+2;y++) for(int x=h.x;x<h.x+2;x++)
                {
                    if(used.Contains(new(x,y)) || grid.GetCell(x,y).HeightLevel!=0)
                        throw new InvalidOperationException("Goblin hut footprint conflicts with resources or terrain.");
                    occupied.Add(new(x,y));
                }
            for(int y=layout.mineOrigin.y;y<layout.mineOrigin.y+2;y++) for(int x=layout.mineOrigin.x;x<layout.mineOrigin.x+2;x++) occupied.Add(new(x,y));
            GridCell At(Vector2 p) { var c=grid.WorldToGrid(p); return grid.GetCell(c.x,c.y); }
            GridCell blue=At(layout.bluePawns[0]),red=At(layout.redPawns[0]);
            try
            {
                foreach(var p in occupied) grid.SetOccupied(p.x,p.y,true);
                if(AStarPathfinder.FindPath(grid,blue,red).Count==0) throw new InvalidOperationException("Bases are disconnected.");
                foreach(var r in layout.resources)
                {
                    var p=grid.WorldToGrid(r.position); int h=grid.GetCell(p.x,p.y).HeightLevel;
                    bool accessible=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right}
                        .Select(d=>grid.GetCell(p.x+d.x,p.y+d.y)).Any(c=>TerrainRules.IsStableStandingCell(c)
                            && c.HeightLevel==h && AStarPathfinder.FindPath(grid,blue,c).Count>0);
                    if(!accessible) throw new InvalidOperationException("Unreachable resource: "+r.name);
                }
                for(int i=0;i<GoblinIslandLayout.ConnectorCount;i++)
                { var s=GoblinIslandLayout.Connector(i); var a=grid.GetCell(s.LowerX,s.LowerY); var b=grid.GetCell(s.UpperX,s.UpperY);
                    if(!a.CanTraverseTo(b)||!b.CanTraverseTo(a)||AStarPathfinder.FindPath(grid,blue,a).Count==0)
                        throw new InvalidOperationException("Disconnected plateau ramp: "+i); }
                foreach(var r in GoblinIslandLayout.Bridges)
                    if(AStarPathfinder.FindPath(grid,blue,grid.GetCell(r.xMin+1,r.yMin+1)).Count==0)
                        throw new InvalidOperationException("A bridge is unreachable.");
                // Closing all bridge cells isolates the island: painted ground cannot create a fourth crossing.
                foreach(var r in GoblinIslandLayout.Bridges) for(int y=r.yMin;y<r.yMax;y++) for(int x=r.xMin;x<r.xMax;x++) grid.SetOccupied(x,y,true);
                if(AStarPathfinder.FindPath(grid,blue,grid.GetCell(33+GoblinIslandLayout.WestExpansion,23)).Count>0)
                    throw new InvalidOperationException("An unintended river crossing bypasses the three bridges.");
            }
            finally
            { foreach(var p in occupied) grid.SetOccupied(p.x,p.y,false);
                foreach(var r in GoblinIslandLayout.Bridges) for(int y=r.yMin;y<r.yMax;y++) for(int x=r.xMin;x<r.xMax;x++) grid.SetOccupied(x,y,false); }
            for(int y=0;y<GoblinIslandLayout.Height;y++) for(int x=0;x<GoblinIslandLayout.Width;x++)
            {
                if(grid.GetCell(x,y).TerrainType!=GoblinIslandLayout.Cell(x,y).terrainType
                    || visuals.Find("FlatGround").GetComponent<Tilemap>().HasTile(new(x,y,0))!=GoblinIslandLayout.IsGround(x,y))
                    throw new InvalidOperationException("Baked terrain disagrees at "+new Vector2Int(x,y));
                if(GoblinIslandLayout.IsIsland(x,y) && (grid.GetCell(x,y).HeightLevel!=0
                    || grid.GetCell(x,y).TerrainType!=TerrainType.FlatGround
                    || visuals.Find("ElevatedGround_L1").GetComponent<Tilemap>().HasTile(new(x,y,0))
                    || visuals.Find("ElevatedGround_L2").GetComponent<Tilemap>().HasTile(new(x,y,0))))
                    throw new InvalidOperationException("The Goblin island must have no elevation or cliff artwork.");
            }
            foreach(ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int b=layout.resources.Count(r=>r.type==type&&r.name.StartsWith("Blue"));
                int r=layout.resources.Count(r=>r.type==type&&r.name.StartsWith("Red"));
                if(b!=r) throw new InvalidOperationException("Unequal starting resources: "+type);
            }
            Debug.Log("GOBLIN_ISLAND_NAVIGATION_PASSED: all resources reachable, reciprocal island/west-strip ramps, exactly three crossings. Resources="+layout.resources.Length);
        }

        [MenuItem("Tools/Tiny Kingdom/Export Goblin Island Overview")]
        public static void ExportOverview()
        {
            Camera camera=Camera.main; Vector3 old=camera.transform.position; float zoom=camera.orthographicSize;
            RenderTexture oldTarget=camera.targetTexture, oldActive=RenderTexture.active;
            int width=GoblinIslandLayout.Width*32,height=GoblinIslandLayout.Height*32;
            var target=new RenderTexture(width,height,24); var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.transform.position=new(GoblinIslandLayout.Width*0.5f,GoblinIslandLayout.Height*0.5f,-10); camera.orthographicSize=GoblinIslandLayout.Height*0.5f;
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
                Directory.CreateDirectory("Documentation"); File.WriteAllBytes("Documentation/GoblinIsland-overview.png",texture.EncodeToPNG());
            }
            finally { camera.targetTexture=oldTarget; RenderTexture.active=oldActive; camera.transform.position=old; camera.orthographicSize=zoom;
                Object.DestroyImmediate(texture); target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
