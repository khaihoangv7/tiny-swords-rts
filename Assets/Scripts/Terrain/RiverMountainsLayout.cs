using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Terrain
{
    // Map two retains its legacy class name. Visuals and navigation bake these
    // authored shapes separately; resource placement never determines terrain.
    public static class RiverMountainsLayout
    {
        public const int Width = BattlefieldLayout.OriginalWidth;
        public const int Height = BattlefieldLayout.Height;
        public const int LaneBottom = 17;
        public const int LaneTop = 27;
        public static readonly Vector2 BlueCastle = new(6.5f, 23.5f);
        public static readonly Vector2 RedCastle = new(53.5f, 23.5f);
        public static readonly TerraceDefinition[] Platforms =
        {
            new(3,15,15,16,1,3), new(42,15,15,16,1,3),
            new(3,21,7,5,2,3), new(50,21,7,5,2,3)
        };
        public const int ConnectorCount = 4;
        public const int RampCopyOffset = 1;

        internal static bool OverlapsStairAccess(Vector2Int origin, Vector2Int size)
        {
            RectInt footprint = new(origin, size);
            for (int i = 0; i < ConnectorCount; i++)
            {
                TerrainHeightConnectorDefinition stair = Connector(i);
                // Keep the ramp and the side exits clear at both elevations.
                RectInt landing = new(stair.VisualX - 1, stair.LowerY - 1, 3,
                    stair.UpperY - stair.LowerY + RampCopyOffset + 2);
                if (footprint.Overlaps(landing))
                    return true;
            }

            TerrainHeightConnectorDefinition blueInner = Connector(1);
            TerrainHeightConnectorDefinition redInner = Connector(3);
            // The landings supply the short turns between tiers. Keep the
            // connecting row below the central mine clear all the way across.
            RectInt lane = new(blueInner.LowerX, blueInner.LowerY,
                redInner.LowerX - blueInner.LowerX + 1, 1);
            // The opening Pawns stand at x=5..7 and x=52..54 on the Castle courts.
            RectInt blueCourt = new(5, blueInner.UpperY, blueInner.UpperX - 5, 1);
            RectInt redCourt = new(redInner.UpperX + 1, redInner.UpperY, blueCourt.width, 1);
            return footprint.Overlaps(lane) || footprint.Overlaps(blueCourt)
                || footprint.Overlaps(redCourt);
        }

        // Two complete native ramp visuals overlap by one cell front/back.
        // Their shared gate has ONE authoritative height transition: overlapping
        // artwork must not give the same cell two different navigation heights.
        public static TerrainHeightConnectorDefinition Connector(int i)
        {
            if (i < 0 || i >= ConnectorCount)
                throw new System.ArgumentOutOfRangeException(nameof(i));
            bool red = i >= 2;
            int local = i % 2;
            int x = local == 0 ? Platforms[0].Surface.xMax : Platforms[2].Surface.xMax;
            if (red) x = Width - 1 - x;
            int lowerY=20;
            int column = red ? 0 : 3;
            return new TerrainHeightConnectorDefinition(x,lowerY,x,lowerY+1,
                red ? TerrainStairOrientation.DescendsLeft : TerrainStairOrientation.DescendsRight,
                $"Tilemap_color3_r4c{column}", $"Tilemap_color3_r5c{column}", local == 0 ? 0 : 1);
        }

        public static bool TryConnectorCell(int x,int y,out TerrainHeightConnectorDefinition stair)
        {
            for (int i=0;i<ConnectorCount;i++)
            {
                stair=Connector(i);
                if (x==stair.VisualX && (y==stair.LowerY || y==stair.UpperY)) return true;
            }
            stair=default;
            return false;
        }

        public static bool TryRampCell(int x,int y,out TerrainHeightConnectorDefinition stair)
        {
            for (int i=0;i<ConnectorCount;i++)
            {
                stair=Connector(i);
                if (x==stair.VisualX && y>=stair.LowerY && y<=stair.UpperY+RampCopyOffset) return true;
            }
            stair=default;
            return false;
        }

        public static bool IsLand(int x, int y)
        {
            if (x < 2 || x > 57 || y < 11 || y > 33) return false;
            if (y >= LaneBottom && y <= LaneTop) return true;
            if (IsCentralIsland(x,y)) return true;
            int inset = y == 11 || y == 33 ? 2 : y == 12 || y == 32 ? 1 : 0;
            return x >= 2 + inset && x <= 18 - inset
                || x >= 41 + inset && x <= 57 - inset;
        }

        public static bool IsShoreline(int x, int y)
            => IsLand(x, y) && (!IsLand(x - 1, y) || !IsLand(x + 1, y)
                || !IsLand(x, y - 1) || !IsLand(x, y + 1));

        public static bool IsRoad(int x, int y)
            => (x >= 14 && x <= 45 && y >= 20 && y <= 24)
                || IsMineClearing(x,y);

        public static bool IsCentralIsland(int x,int y)
            => Mathf.Pow((x+0.5f-30f)/8f,2)+Mathf.Pow((y+0.5f-22f)/8f,2)<=1f;

        public static bool IsMineClearing(int x,int y)
            => Mathf.Pow((x+0.5f-30f)/4.8f,2)+Mathf.Pow((y+0.5f-22f)/4.8f,2)<=1f;

        public static NavigationCellData Cell(int x, int y)
        {
            var cell=new NavigationCellData(x,y,IsLand(x,y) ? TerrainType.FlatGround : TerrainType.Water,0);
            foreach (TerraceDefinition platform in Platforms)
            {
                if (platform.Surface.Contains(new Vector2Int(x,y)))
                { cell.terrainType=TerrainType.ElevatedGround; cell.heightLevel=platform.Height; }
                else if (platform.IsCliff(x,y))
                { cell.terrainType=TerrainType.Cliff; cell.heightLevel=platform.Height-1; }
            }
            if (TryConnectorCell(x,y,out TerrainHeightConnectorDefinition stair))
            {
                cell.terrainType=TerrainType.Stairs;
                cell.heightLevel=y==stair.LowerY ? stair.LowerHeight : stair.LowerHeight+1;
                cell.hasHeightConnection=true;
                cell.heightConnection=new Vector2Int(x,y==stair.LowerY ? stair.UpperY : stair.LowerY);
            }
            else if (TryRampCell(x,y,out stair))
            {
                // The rear copy's upper cell is an approach on the same tier,
                // not another height transition or a second gameplay stair.
                cell.terrainType=TerrainType.ElevatedGround;
                cell.heightLevel=stair.LowerHeight+1;
            }
            return cell;
        }
    }
}
