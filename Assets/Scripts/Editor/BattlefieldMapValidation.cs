using System;
using System.Collections.Generic;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Terrain;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class BattlefieldMapValidation
    {
        private const string Pending = "TwoKingdoms.BatchValidation";
        private static double playStarted;
        private static double readySince;
        static BattlefieldMapValidation() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            BattlefieldMapBuilder.Build();
            Phase1MapBuilder.RepairTerrainVisuals();
            SessionState.SetBool(Pending, true);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            // Let editor asset preparers finish before Play Mode; a delayed assembly
            // reload would discard runtime-only state on freshly spawned entities.
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                { readySince=0; return; }
                if (readySince==0) readySince=EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup-readySince>8
                    && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.EnterPlaymode();
                return;
            }
            if (playStarted == 0) playStarted = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - playStarted < 5) return;
            SessionState.SetBool(Pending, false);
            try
            {
                ValidatePlayMode();
                Debug.Log("TWO_KINGDOMS_VALIDATION_PASSED");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateEditMode()
        {
            BattlefieldLayout layout = AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(BattlefieldMapBuilder.LayoutPath);
            GridManager grid = Object.FindAnyObjectByType<GridManager>();
            grid.SetNavigationData(AssetDatabase.LoadAssetAtPath<NavigationGridData>(Phase2NavigationBuilder.DataPath));
            Tilemap flat = GameObject.Find("Map").transform.Find("Grid/FlatGround").GetComponent<Tilemap>();
            Tilemap foam = GameObject.Find("Map").transform.Find("Grid/WaterFoam").GetComponent<Tilemap>();
            Require(grid.Width==BattlefieldLayout.Width && grid.Height==BattlefieldLayout.Height,
                "Navigation must cover the expanded map.");
            var camera = new SerializedObject(Camera.main.GetComponent<MapCameraController>());
            Require(camera.FindProperty("playableBounds").rectValue == new Rect(0,0,grid.Width,grid.Height)
                && camera.FindProperty("maximumZoom").floatValue >= grid.Height*.5f,
                "Camera bounds or zoom still use the old map size.");
            for (int y = 0; y < BattlefieldLayout.Height; y++)
            for (int x = 0; x < BattlefieldLayout.Width; x++)
            {
                Require(flat.HasTile(new(x,y,0)) == BattlefieldLayout.IsLand(x,y), "Visual shoreline mismatch.");
                Require((grid.GetCell(x,y).TerrainType != TerrainType.Water) == BattlefieldLayout.IsLand(x,y), "Navigation shoreline mismatch.");
                Require(foam.HasTile(new(x,y,0)) == BattlefieldLayout.IsShoreline(x,y), "Foam is missing from the shoreline.");
            }
            var used = new HashSet<Vector2Int>();
            foreach (BattlefieldResource r in layout.resources)
            {
                Vector2Int p = grid.WorldToGrid(r.position);
                Require(grid.GetCell(p.x,p.y)?.Walkable == true && used.Add(p), "Resource overlaps or sits off land: " + r.name);
                grid.SetOccupied(p.x,p.y,true);
            }
            foreach (Vector2 castle in new[] {layout.blueCastle,layout.redCastle})
            {
                Vector2Int p = grid.WorldToGrid(castle);
                for(int y=0;y<2;y++) for(int x=-1;x<=1;x++)
                {
                    Require(grid.GetCell(p.x+x,p.y+y).Walkable && used.Add(p+new Vector2Int(x,y)),"Castle footprint overlaps.");
                    grid.SetOccupied(p.x+x,p.y+y,true);
                }
            }
            int mineX=StrategicObjectiveMapLayout.MineOriginX, mineY=StrategicObjectiveMapLayout.MineOriginY;
            for(int y=mineY;y<mineY+2;y++) for(int x=mineX;x<mineX+2;x++) grid.SetOccupied(x,y,true);
            ValidateTerraces();
            ValidatePastures(layout,grid);
            ValidateScenery(layout);
            foreach(Vector2 p in layout.bluePawns.Concat(layout.redPawns))
            {
                Vector2Int cell=grid.WorldToGrid(p);
                Require(grid.GetCell(cell.x,cell.y).Walkable && !grid.GetCell(cell.x,cell.y).Occupied,"Pawn spawn blocked.");
            }
            Vector2Int blueStart=grid.WorldToGrid(layout.blueCastle)+Vector2Int.down;
            double blue=PathCost(grid,blueStart,new(mineX-1,mineY));
            double red=PathCost(grid,grid.WorldToGrid(layout.redCastle)+Vector2Int.down,new(mineX+2,mineY));
            Require(Math.Abs(blue-red)/Math.Max(blue,red)<.10,"Base-to-mine routes differ by over 10%.");
            foreach(BattlefieldResource r in layout.resources)
            {
                Vector2Int cell=grid.WorldToGrid(r.position);
                bool reachable=false;
                foreach(Vector2Int d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
                {
                    Vector2Int target=cell+d;
                    if(AStarPathfinder.FindPath(grid,grid.GetCell(blueStart.x,blueStart.y),grid.GetCell(target.x,target.y)).Count>0)
                    { reachable=true;break; }
                }
                Require(reachable,"Unreachable resource: "+r.name);
            }
            Debug.Log($"Battlefield layout verified: {layout.resources.Length} accessible resources; base-to-mine path cost Blue {blue:F2}, Red {red:F2}; coastline, occupancy and six Pawn spawns valid.");
            grid.SetNavigationData(grid.NavigationData);
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Two Kingdoms Play Mode")]
        public static void ValidatePlayMode()
        {
            Require(EditorApplication.isPlaying,"Enter Play Mode first.");
            CastleDropOff[] castles=Object.FindObjectsByType<CastleDropOff>();
            Require(castles.Length==2 && castles.Select(c=>c.Faction).Distinct().Count()==2,"Exactly two opposing main castles required.");
            MatchManager match=Object.FindAnyObjectByType<MatchManager>();
            Require(match!=null && match.HasBothMainCastles && match.State==MatchState.Playing,"Match did not start.");
            Require(Object.FindObjectsByType<GoldMineObjective>().Length==1,"Central Gold Mine missing or duplicated.");
            Require(Object.FindObjectsByType<BattlefieldScenePreview>().Length==0,"Editor preview is visible during gameplay.");
            UnitController[] units=Object.FindObjectsByType<UnitController>();
            Require(units.Length==6 && units.Count(u=>u.Faction==Faction.Blue)==3
                && units.Count(u=>u.Faction==Faction.Red)==3,"Starting units must be three Pawns per side.");
            Require(units.All(u=>u.GetComponent<PawnGatherer>()!=null),"Prototype combat units are still active.");
            BattlefieldLayout layout=AssetDatabase.LoadAssetAtPath<BattlefieldLayout>(BattlefieldMapBuilder.LayoutPath);
            Require(ResourceNode.ActiveNodes.Count==layout.resources.Length,"Runtime resource layout differs from authored layout.");
            GridManager grid=Object.FindAnyObjectByType<GridManager>();
            foreach(CastleDropOff castle in castles)
                Require(castle.IsProductionReady && castle.IsAlive,
                    $"Castle {castle.Faction} not ready: alive={castle.IsAlive}, available={castle.IsAvailable}, frame={Time.frameCount}.");
            foreach(Vector2 p in layout.bluePawns.Concat(layout.redPawns))
            {
                Vector2Int cell=grid.WorldToGrid(p);
                Require(!grid.GetCell(cell.x,cell.y).Occupied && grid.GetCell(cell.x,cell.y).HeightLevel==2,"Runtime Pawn must start on the upper Castle court.");
            }
            int mineX=StrategicObjectiveMapLayout.MineOriginX, mineY=StrategicObjectiveMapLayout.MineOriginY;
            PathCost(grid,grid.WorldToGrid(layout.blueCastle)+Vector2Int.down,new(mineX-1,mineY));
            PathCost(grid,grid.WorldToGrid(layout.redCastle)+Vector2Int.down,new(mineX+2,mineY));
            ValidateTerraces();
            ValidatePastures(layout,grid);
            ValidateScenery(layout);
            Phase6AVisionValidation.Run();
            Phase6CMatchFlowValidation.Run();
            Debug.Log("Runtime battlefield validated: two main castles, six faction-correct Pawns, central mine, active match and reachable objective.");
        }

        // Validate geometry, sprite-to-cell correspondence and every legal height edge.
        public static void ValidateTerraces()
        {
            GridManager grid=Object.FindAnyObjectByType<GridManager>();
            Transform root=GameObject.Find("Map").transform.Find("Grid");
            for(int i=0;i<BattlefieldTerraces.ConnectorCount;i++)
            {
                var stair=BattlefieldTerraces.Connector(i);
                GridCell lower=grid.GetCell(stair.GridX,stair.LowerY);
                GridCell upper=grid.GetCell(stair.GridX,stair.UpperY);
                Require(lower.TerrainType==TerrainType.Stairs && lower.HeightLevel==stair.LowerHeight
                    && upper.TerrainType==TerrainType.Stairs && upper.HeightLevel==stair.LowerHeight+1,
                    "Invalid side ramp heights: "+i);
                Require(AStarPathfinder.CanTraverseStep(grid,lower,upper)
                    && AStarPathfinder.CanTraverseStep(grid,upper,lower),"Side ramp cannot be used both ways: "+i);
                Tilemap map=root.Find("ElevatedGround_L"+(stair.LowerHeight+1)).GetComponent<Tilemap>();
                Require(map.GetTile<Tile>(new(stair.GridX,stair.LowerY,0))?.sprite?.name==stair.LowerSpriteName
                    && map.GetTile<Tile>(new(stair.GridX,stair.UpperY,0))?.sprite?.name==stair.UpperSpriteName,
                    "Visible ramp differs from navigation: "+i);
            }
            foreach(TerraceDefinition terrace in BattlefieldTerraces.Platforms)
            {
                Tilemap map=root.Find("ElevatedGround_L"+terrace.Height).GetComponent<Tilemap>();
                Tilemap shadow=root.Find("Shadow_L"+terrace.Height).GetComponent<Tilemap>();
                Require(map.GetComponent<TilemapRenderer>().sortingOrder==3+2*terrace.Height
                    && shadow.GetComponent<TilemapRenderer>().sortingOrder==2+2*terrace.Height,
                    "Terrace layer order invalid.");
                for(int x=terrace.Surface.xMin;x<terrace.Surface.xMax;x++)
                for(int y=terrace.Surface.yMin-1;y<terrace.Surface.yMin;y++)
                    Require(grid.GetCell(x,y).TerrainType==TerrainType.Cliff && map.HasTile(new(x,y,0)),
                        "South cliff is missing or has an unintended entrance.");
            }
            for(int y=0;y<grid.Height;y++) for(int x=0;x<grid.Width;x++)
            {
                GridCell from=grid.GetCell(x,y);
                if(!from.Walkable) continue;
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                {
                    GridCell to=grid.GetCell(x+dx,y+dy);
                    if(to==null || to.HeightLevel==from.HeightLevel || !AStarPathfinder.CanTraverseStep(grid,from,to)) continue;
                    Require(from.HeightConnection==new Vector2Int(to.GridX,to.GridY)
                        && to.HeightConnection==new Vector2Int(x,y),"Hidden climb or diagonal height shortcut.");
                }
            }
            foreach(Vector2 castle in new[]{BattlefieldLayout.BlueCastleStart,BattlefieldLayout.RedCastleStart})
            {
                Vector2Int p=grid.WorldToGrid(castle);
                Require(grid.GetCell(p.x,p.y).HeightLevel==2,"Castles must stand on the second tier.");
            }
            ValidateIndependentBaseRoutes(grid);
            Require(grid.GetCell(StrategicObjectiveMapLayout.MineOriginX,StrategicObjectiveMapLayout.MineOriginY).HeightLevel==0,
                "Mine must stand on flat ground.");
            for(int y=18;y<=28;y++) for(int x=24;x<=35;x++)
                Require(grid.GetCell(x,y).HeightLevel==0 && grid.GetCell(x,y).TerrainType==TerrainType.FlatGround,
                    "An old mine terrace or offset ramp remains in the central clearing.");
        }

        private static void ValidateIndependentBaseRoutes(GridManager grid)
        {
            // Close one complete route and prove the other still connects ground
            // to the Castle through both of its visible flights, in both directions.
            for(int side=0;side<2;side++) for(int route=0;route<2;route++)
            {
                int entrance=side*4+route*2;
                int other=side*4+(1-route)*2;
                var blocked=new List<(GridCell cell,bool occupied)>();
                try
                {
                    for(int flight=other;flight<other+2;flight++)
                    {
                        var stair=BattlefieldTerraces.Connector(flight);
                        foreach(int y in new[]{stair.LowerY,stair.UpperY})
                        {
                            GridCell cell=grid.GetCell(stair.GridX,y);
                            blocked.Add((cell,cell.Occupied));
                            grid.SetOccupied(cell.GridX,cell.GridY,true);
                        }
                    }
                    Vector2 castle=side==0?BattlefieldLayout.BlueCastleStart:BattlefieldLayout.RedCastleStart;
                    Vector2Int start=grid.WorldToGrid(castle)+Vector2Int.down;
                    var gate=BattlefieldTerraces.Connector(entrance);
                    GridCell from=grid.GetCell(start.x,start.y), to=grid.GetCell(gate.GridX,gate.LowerY);
                    foreach(var path in new[]{AStarPathfinder.FindPath(grid,from,to),AStarPathfinder.FindPath(grid,to,from)})
                    {
                        Require(path.Count>0,"Castle route blocked when its alternate is closed.");
                        for(int flight=entrance;flight<entrance+2;flight++)
                        {
                            var stair=BattlefieldTerraces.Connector(flight);
                            Require(path.Any(c=>c.GridX==stair.GridX && c.GridY==stair.LowerY)
                                && path.Any(c=>c.GridX==stair.GridX && c.GridY==stair.UpperY),
                                "Castle route skipped one of its two stair flights.");
                        }
                    }
                }
                finally
                {
                    foreach(var item in blocked) grid.SetOccupied(item.cell.GridX,item.cell.GridY,item.occupied);
                }
            }
        }

        private static void ValidatePastures(BattlefieldLayout layout,GridManager grid)
        {
            for(int side=0;side<2;side++)
            {
                RectInt plateau=BattlefieldTerraces.Platforms[side].Surface;
                string prefix=side==0?"Blue_Sheep_":"Red_Sheep_";
                BattlefieldResource[] sheep=layout.resources.Where(r=>r.name.StartsWith(prefix)).ToArray();
                Require(sheep.Length==24 && sheep.Count(r=>r.position.y>plateau.yMax)==12
                    && sheep.Count(r=>r.position.y<plateau.yMin-1)==12,
                    "Each base needs twelve scattered Sheep above and twelve below its plateau.");
                Require(sheep.All(r=>r.position.x>plateau.xMin && r.position.x<plateau.xMax
                    && grid.GetCell((int)r.position.x,(int)r.position.y).HeightLevel==0),
                    "Sheep must graze on the low meadows around their base.");
                int free=0;
                foreach(Vector2Int p in plateau.allPositionsWithin)
                    if(grid.GetCell(p.x,p.y).Walkable && !grid.GetCell(p.x,p.y).Occupied) free++;
                Require(free>=(side==0?171:180),"Castle plateau needs ample unobstructed construction space.");
            }
        }

        private static void ValidateScenery(BattlefieldLayout layout)
        {
            Transform root=GameObject.Find("Map").transform.Find("BattlefieldScenery");
            var visuals=root.GetComponentsInChildren<SpriteRenderer>();
            Require(layout.resources.Count(r=>r.type==ResourceType.Wood)>=300,"Dense woodland is missing.");
            Require(layout.resources.Count(r=>r.type==ResourceType.Food)==73,"Expected 60 meadow Sheep, 8 Castle-rim Sheep and 5 Blue upper-court Sheep.");
            Require(visuals.Count(r=>r.name.StartsWith("Cloud_"))==16,"Expected 16 full-cloud illustrations.");
            AmbientCloudDrift[] clouds=root.GetComponentsInChildren<AmbientCloudDrift>();
            Require(clouds.Length==16 && clouds.Count(c=>BattlefieldLayout.IsPond((int)c.Anchor.x,(int)c.Anchor.y))==2,
                "Clouds must drift inside the map, including one over each pond.");
            foreach(AmbientCloudDrift cloud in clouds)
            {
                SpriteRenderer visual=cloud.GetComponent<SpriteRenderer>();
                Vector2 extent=visual.bounds.extents;
                float minX=float.MaxValue,maxX=float.MinValue;
                for(int sample=0;sample<=80;sample++)
                {
                    Vector2 p=cloud.PositionAt(sample*cloud.Period/40f);
                    Require(p.x-extent.x>=.99f && p.x+extent.x<=BattlefieldLayout.Width-.99f
                        && p.y-extent.y>=.99f && p.y+extent.y<=BattlefieldLayout.Height-.99f,
                        "Drifting cloud leaves the map: "+cloud.name);
                    minX=Mathf.Min(minX,p.x); maxX=Mathf.Max(maxX,p.x);
                }
                Require(maxX-minX>2f && Vector2.Distance(cloud.PositionAt(0),cloud.PositionAt(cloud.Period))<.001f,
                    "Cloud must travel back and forth smoothly.");
                Require(visual.transform.localScale.x>=1.1f && visual.sortingOrder==19
                    && cloud.GetComponent<Collider2D>()==null && cloud.GetComponent<ResourceNode>()==null,
                    "Cloud must be enlarged ambient art without gameplay collision.");
            }
            if(EditorApplication.isPlaying)
                Require(clouds.Any(c=>Vector2.Distance(c.transform.position,c.PositionAt(0))>.03f),
                    "Cloud positions did not advance in Play Mode.");
            Require(visuals.Count(r=>r.name.StartsWith("ForestStone_"))>=40,"Forest stones are missing.");
            Require(visuals.Count(r=>r.name.StartsWith("Duck_"))==16,"Expected 16 ambient Ducks.");
            Require(visuals.Count(r=>r.name.StartsWith("WaterRock_"))==48,"Expected 48 water rocks.");
            foreach(SpriteRenderer visual in visuals)
            {
                bool duck=visual.name.StartsWith("Duck_");
                if(!duck && !visual.name.StartsWith("WaterRock_")) continue;
                Vector3 p=visual.transform.position;
                Require(!BattlefieldLayout.IsLand(Mathf.FloorToInt(p.x),Mathf.FloorToInt(p.y)),
                    "Water decoration has been placed on land: "+visual.name);
                AmbientSpriteLoop loop=visual.GetComponent<AmbientSpriteLoop>();
                Require(loop!=null && loop.FrameCount==(duck?3:16),"Incorrect ambient animation slicing.");
                Require(visual.GetComponent<Collider2D>()==null && visual.GetComponent<ResourceNode>()==null
                    && visual.GetComponent<UnitController>()==null,"Ambient art must not become a gameplay entity.");
            }
            Debug.Log($"Lush scenery verified: {layout.resources.Count(r=>r.type==ResourceType.Wood)} Trees, 73 Sheep, 16 Ducks, 48 animated water rocks and 16 Clouds.");
        }

        private static double PathCost(GridManager grid,Vector2Int from,Vector2Int to)
        {
            List<GridCell> path=AStarPathfinder.FindPath(grid,grid.GetCell(from.x,from.y),grid.GetCell(to.x,to.y));
            Require(path.Count>0,"No route to the contested gold clearing.");
            double cost=0;
            for(int i=1;i<path.Count;i++) cost+=Vector3.Distance(path[i-1].WorldPosition,path[i].WorldPosition);
            Require(path.Any(c=>c.HeightLevel==2) && path.Any(c=>c.HeightLevel==1) && path.Any(c=>c.HeightLevel==0)
                && path.All(c=>c.HeightLevel<=2),"Route must descend both Castle tiers to the flat central mine.");
            return cost;
        }

        private static void Require(bool condition,string message)
        { if(!condition) throw new InvalidOperationException(message); }
    }
}
