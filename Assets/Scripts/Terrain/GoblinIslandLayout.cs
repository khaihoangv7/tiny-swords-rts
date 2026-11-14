using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Terrain
{
    // The sketch is authored once, then baked independently into visuals and navigation.
    public static class GoblinIslandLayout
    {
        public const int WestExpansion = 6;
        public const int Width = BattlefieldLayout.OriginalWidth + WestExpansion;
        public const int Height = BattlefieldLayout.Height;
        public const int MaxHeight = 2;
        public static Vector2 Shift(Vector2 p) => p + Vector2.right * WestExpansion;
        public static readonly Vector2 BlueCastle = Shift(new(47f, 9.5f));
        public static readonly Vector2 RedCastle = Shift(new(47f, 40.5f));
        public static readonly Vector2Int MineOrigin = new(38 + WestExpansion, 22);
        public const float GuardVisualScale = 1.7f;
        public const float BridgeVisualScale = 1.6f;
        public const int IslandGuardCount = 9;
        public static readonly Vector2[] Guards =
        {
            Shift(new(36.5f,22.5f)), Shift(new(42.5f,24.5f)), Shift(new(39.5f,19.5f)),
            Shift(new(34.5f,20.5f)), Shift(new(44.5f,25.5f)), Shift(new(36.5f,18.5f)),
            Shift(new(47.5f,24.5f)), Shift(new(49.5f,21.5f)), Shift(new(52.5f,19.5f)),
            new(10.5f,24.5f), new(12.5f,26.5f), new(10.5f,27.5f)
        };
        public static readonly Vector2Int[] Huts = { new(40,17), new(51,26), new(55,17) };
        public static readonly RectInt[] Bridges = { new(34,29,2,4), new(34,11,2,4), new(17,21,4,2) };
        public static readonly TerraceDefinition[] Platforms =
        {
            // Unequal bands form three connected, asymmetric hill outlines.
            new(3,4,10,3,1,3), new(2,7,13,4,1,3), new(4,11,11,3,1,3), new(6,14,6,3,1,3),
            new(8,10,5,3,2,1), new(7,13,5,2,2,1), new(8,15,3,2,2,1),
            new(3,20,12,3,1,3), new(2,23,13,4,1,3), new(3,27,10,2,1,3), new(3,29,6,2,1,3),
            new(4,25,4,4,2,1), new(4,29,3,2,2,1),
            new(7,33,8,3,1,3), new(4,36,12,3,1,3), new(2,39,13,2,1,3), new(4,41,8,2,1,3)
        };
        public const int ConnectorCount = 8;

        public static TerrainHeightConnectorDefinition Connector(int index) => index switch
        {
            0 => Stair(1,7,0,3,true), 1 => Stair(15,10,0,3,false), 2 => Stair(7,10,1,1,true),
            3 => Stair(1,23,0,3,true), 4 => Stair(15,21,0,3,false), 5 => Stair(8,26,1,1,false),
            6 => Stair(16,36,0,3,false), 7 => Stair(1,39,0,3,true),
            _ => throw new System.ArgumentOutOfRangeException(nameof(index))
        };

        private static TerrainHeightConnectorDefinition Stair(int x,int y,int height,int color,bool left)
        {
            int col=left?0:3;
            return new(x,y,x+(left?1:-1),y+1,
                left ? TerrainStairOrientation.DescendsLeft : TerrainStairOrientation.DescendsRight,
                $"Tilemap_color{color}_r4c{col}", $"Tilemap_color{color}_r5c{col}",height);
        }

        public static bool TryConnectorCell(int x,int y,out TerrainHeightConnectorDefinition stair)
        {
            for (int i=0;i<ConnectorCount;i++)
            {
                stair=Connector(i);
                if (stair.ContainsCorridorCell(new(x,y))) return true;
            }
            stair=default; return false;
        }

        public static bool IsIsland(int x,int y) => x>=20 && x<=62 && y>=14 && y<=29;
        public static bool IsRiver(int x,int y) => x>=18 && x<=64 && y>=12 && y<=31
            && (x<=19 || x>=63 || y<=13 || y>=30);
        public static int SurfaceHeight(int x,int y)
        {
            int height=0;
            foreach(var p in Platforms) if(p.Surface.Contains(new(x,y))) height=Mathf.Max(height,p.Height);
            return height;
        }
        public static bool HasRaisedSurface(int x,int y) => SurfaceHeight(x,y)>0;
        public static bool HasSurface(int x,int y,int height) => SurfaceHeight(x,y)>=height;
        public static bool IsCliff(int x,int y,int height) => !HasSurface(x,y,height)&&HasSurface(x,y+1,height);
        public static bool IsCliff(int x,int y) => IsCliff(x,y,1)||IsCliff(x,y,2);
        public static bool IsBridge(int x,int y)
        {
            foreach (RectInt r in Bridges) if (r.Contains(new(x,y))) return true;
            return false;
        }
        public static bool IsGround(int x,int y) => x>=1 && x<=Width-2 && y>=1 && y<=Height-2 && !IsRiver(x,y);
        public static bool IsShore(int x,int y) => IsGround(x,y)
            && (!IsGround(x-1,y) || !IsGround(x+1,y) || !IsGround(x,y-1) || !IsGround(x,y+1));
        public static bool IsApproach(int x,int y) => x>=32 && x<=39 && y>=8 && y<=35
            || x>=14 && x<=24 && y>=20 && y<=24
            || x>=38 && x<=51 && y>=20 && y<=25;

        internal static bool OverlapsCrossingAccess(Vector2Int origin, Vector2Int size)
        {
            RectInt footprint = new(origin, size);
            foreach (RectInt bridge in Bridges)
                if (footprint.Overlaps(bridge)) return true;
            for (int y = footprint.yMin; y < footprint.yMax; y++)
            for (int x = footprint.xMin; x < footprint.xMax; x++)
                if (IsApproach(x, y)) return true;
            for (int i = 0; i < ConnectorCount; i++)
            {
                TerrainHeightConnectorDefinition stair = Connector(i);
                int minX = Mathf.Min(stair.LowerX, stair.UpperX) - 1;
                RectInt landing = new(minX, stair.LowerY - 1, 4, 4);
                if (footprint.Overlaps(landing)) return true;
            }
            return false;
        }

        public static readonly Vector2Int[] HillClearings={new(4,8),new(10,13),new(11,24),new(5,28),new(10,38)};

        // Native palette islands and meandering paths. Placement follows this biome map.
        public static int GrassColor(int x,int y)
        {
            if(IsIsland(x,y)) return 5;
            foreach(var p in Platforms)
                if(p.Height==SurfaceHeight(x,y)&&p.Surface.Contains(new(x,y))) return p.Color;
            if(x<18) return Mathf.Pow((x-8f)/7f,2)+Mathf.Pow((y-21f)/14f,2)<1f?3:1;
            x-=WestExpansion;
            float pathY=y<12?6.5f+Mathf.Sin(x*0.18f)*1.3f:37.5f+Mathf.Sin(x*0.18f+1f)*1.3f;
            if(x>=12 && Mathf.Abs(y+0.5f-pathY)<1.3f) return 4;
            float a=Mathf.Pow((x-19f)/10f,2)+Mathf.Pow((y-8f)/4.8f,2);
            float b=Mathf.Pow((x-23f)/11f,2)+Mathf.Pow((y-36f)/4.8f,2);
            float c=Mathf.Pow((x-5f)/4.3f,2)+Mathf.Pow((y-20f)/9f,2);
            float d=Mathf.Pow((x-47f)/9f,2)+Mathf.Pow((y-8f)/6f,2);
            float e=Mathf.Pow((x-47f)/9f,2)+Mathf.Pow((y-39f)/6f,2);
            if(a<1f || b<1f || c<1f || d<1f || e<1f) return 3;
            return 1;
        }

        public static NavigationCellData Cell(int x,int y)
        {
            var cell=new NavigationCellData(x,y,IsGround(x,y)||IsBridge(x,y)
                ? TerrainType.FlatGround : TerrainType.Water,0);
            int height=SurfaceHeight(x,y);
            if(height>0) { cell.terrainType=TerrainType.ElevatedGround; cell.heightLevel=height; }
            for(int level=1;level<=MaxHeight;level++)
                if(IsCliff(x,y,level)) { cell.terrainType=TerrainType.Cliff; cell.heightLevel=level-1; }
            if(IsBridge(x,y)) { cell.terrainType=TerrainType.FlatGround; cell.heightLevel=0; }
            if (TryConnectorCell(x,y,out var stair))
            {
                bool lower=new Vector2Int(x,y)==stair.LowerCell;
                cell.terrainType=TerrainType.Stairs;
                cell.heightLevel=lower?stair.LowerHeight:stair.LowerHeight+1;
                cell.hasHeightConnection=true;
                cell.heightConnection=lower?stair.UpperCell:stair.LowerCell;
            }
            return cell;
        }
    }
}
