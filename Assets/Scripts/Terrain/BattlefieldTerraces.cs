using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Terrain
{
    // Authored geometry. Rendering and navigation consume these definitions separately.
    public readonly struct TerraceDefinition
    {
        public readonly RectInt Surface;
        public readonly int Height;
        public readonly int Color;
        public TerraceDefinition(int x, int y, int width, int height, int level, int color)
        { Surface = new RectInt(x,y,width,height); Height = level; Color = color; }
        public bool IsCliff(int x,int y) => x>=Surface.xMin && x<Surface.xMax
            && y==Surface.yMin-1;
    }

    public static class BattlefieldTerraces
    {
        public static readonly TerraceDefinition[] Platforms =
        {
            new(5,8,16,13,1,3),   // Original Blue lower landing and construction terrace.
            new(39 + BattlefieldLayout.HorizontalExpansion,24,16,13,1,3), // Red equivalent lower terrace.
            new(8,14,10,7,2,1),   // Brighter upper courts distinguish height two from the lower landing.
            new(42 + BattlefieldLayout.HorizontalExpansion,30,10,7,2,1)
        };

        public const int MaxHeight = 2;
        public const int ConnectorCount = 8;
        public static TerrainHeightConnectorDefinition Connector(int i) => i switch
        {
            0 => Stair(21,15,0,3,false), // Blue east: ground -> landing -> court.
            1 => Stair(18,16,1,1,false),
            2 => Stair(4,12,0,3,true),   // Blue west, independent second route.
            3 => Stair(7,13,1,1,true),
            4 => Stair(38 + BattlefieldLayout.HorizontalExpansion,29,0,3,true), // Red west: inward-facing main route.
            5 => Stair(41 + BattlefieldLayout.HorizontalExpansion,30,1,1,true),
            6 => Stair(55 + BattlefieldLayout.HorizontalExpansion,28,0,3,false),// Red east, independent second route.
            7 => Stair(52 + BattlefieldLayout.HorizontalExpansion,29,1,1,false),
            _ => throw new System.ArgumentOutOfRangeException(nameof(i))
        };

        private static TerrainHeightConnectorDefinition Stair(int x,int lowerY,int height,int color,bool left)
            => new(x,lowerY,x+(left?1:-1),lowerY+1,
                left ? TerrainStairOrientation.DescendsLeft : TerrainStairOrientation.DescendsRight,
                $"Tilemap_color{color}_r4c{(left?0:3)}",$"Tilemap_color{color}_r5c{(left?0:3)}",height);

        public static bool TryGetTargetTerrace(TerrainHeightConnectorDefinition stair,
            out TerraceDefinition terrace)
        {
            int targetHeight=stair.LowerHeight+1;
            foreach(TerraceDefinition candidate in Platforms)
            {
                if(candidate.Height==targetHeight && candidate.Surface.Contains(stair.UpperCell))
                { terrace=candidate; return true; }
            }
            terrace=default;
            return false;
        }

        // Official Tiny Swords stairs require the plateau edge tile at the ramp
        // exit to become a centre connection tile. If that exit is on the south
        // cliff row, the adjacent cliff foot must also become its centre piece.
        public static bool RequiresCliffConnection(TerrainHeightConnectorDefinition stair)
            => TryGetTargetTerrace(stair,out TerraceDefinition terrace)
                && stair.UpperY==terrace.Surface.yMin;

        public static string SurfaceConnectionSpriteName(TerrainHeightConnectorDefinition stair)
        {
            if(!TryGetTargetTerrace(stair,out TerraceDefinition terrace))
                throw new System.InvalidOperationException("Stair upper exit is outside its target terrace.");
            int row=stair.UpperY==terrace.Surface.yMax-1?0
                : stair.UpperY==terrace.Surface.yMin?2:1;
            return $"Tilemap_color{terrace.Color}_r{row}c6";
        }

        public static string CliffConnectionSpriteName(TerrainHeightConnectorDefinition stair)
        {
            if(!TryGetTargetTerrace(stair,out TerraceDefinition terrace))
                throw new System.InvalidOperationException("Stair upper exit is outside its target terrace.");
            return $"Tilemap_color{terrace.Color}_r5c6";
        }

        public static bool TryConnectorCell(int x,int y,out TerrainHeightConnectorDefinition connector)
        {
            for(int i=0;i<ConnectorCount;i++)
            {
                connector=Connector(i);
                Vector2Int cell=new(x,y);
                if(cell==connector.LowerCell || cell==connector.UpperCell) return true;
            }
            connector=default; return false;
        }

        public static NavigationCellData Cell(int x,int y)
        {
            var cell=new NavigationCellData(x,y,BattlefieldLayout.IsLand(x,y) ? TerrainType.FlatGround : TerrainType.Water,0);
            foreach(TerraceDefinition platform in Platforms)
            {
                if(platform.Surface.Contains(new Vector2Int(x,y)))
                { cell.terrainType=TerrainType.ElevatedGround; cell.heightLevel=platform.Height; }
                else if(platform.IsCliff(x,y))
                { cell.terrainType=TerrainType.Cliff; cell.heightLevel=platform.Height-1; }
            }
            if(TryConnectorCell(x,y,out TerrainHeightConnectorDefinition stair))
            {
                cell.terrainType=TerrainType.Stairs;
                bool lower=new Vector2Int(x,y)==stair.LowerCell;
                cell.heightLevel=lower ? stair.LowerHeight : stair.LowerHeight+1;
                cell.hasHeightConnection=true;
                cell.heightConnection=lower ? stair.UpperCell : stair.LowerCell;
            }
            return cell;
        }

        public static bool Covers(int x,int y) => Platforms.AnyContains(x,y)
            || TryConnectorCell(x,y,out _);

        private static bool AnyContains(this TerraceDefinition[] platforms,int x,int y)
        {
            foreach(TerraceDefinition platform in platforms)
                if(platform.Surface.Contains(new Vector2Int(x,y)) || platform.IsCliff(x,y)) return true;
            return false;
        }
    }
}
