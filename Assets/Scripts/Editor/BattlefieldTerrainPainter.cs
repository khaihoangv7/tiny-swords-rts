using System;
using TinyKingdom.Terrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TinyKingdom.EditorTools
{
    public static class BattlefieldTerrainPainter
    {
        public static void Paint(Transform grid)
        {
            var surfaces = new Tilemap[4];
            var shadows = new Tilemap[4];
            Tile shadowTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tiles/Shadows/Shadow.asset");
            // Keep only the current Castle levels; the contested clearing stays flat.
            for (int level=BattlefieldTerraces.MaxHeight+1;level<=3;level++)
            {
                Transform oldSurface=grid.Find("ElevatedGround_L"+level);
                Transform oldShadow=grid.Find("Shadow_L"+level);
                if(oldSurface!=null) UnityEngine.Object.DestroyImmediate(oldSurface.gameObject);
                if(oldShadow!=null) UnityEngine.Object.DestroyImmediate(oldShadow.gameObject);
            }
            for (int level=1;level<=BattlefieldTerraces.MaxHeight;level++)
            {
                shadows[level]=Layer(grid,"Shadow_L"+level,2+2*level);
                surfaces[level]=Layer(grid,"ElevatedGround_L"+level,3+2*level);
                shadows[level].ClearAllTiles(); surfaces[level].ClearAllTiles();
            }
            foreach(TerraceDefinition terrace in BattlefieldTerraces.Platforms)
            {
                RectInt rect=terrace.Surface;
                Tilemap surface=surfaces[terrace.Height];
                Tilemap shadow=shadows[terrace.Height];
                for(int y=rect.yMin;y<rect.yMax;y++)
                for(int x=rect.xMin;x<rect.xMax;x++)
                {
                    int row=y==rect.yMax-1?0:y==rect.yMin?2:1;
                    int col=x==rect.xMin?5:x==rect.xMax-1?7:6;
                    surface.SetTile(new(x,y,0),Get(terrace.Color,row,col));
                    shadow.SetTile(new(x,y-1,0),shadowTile);
                }
                for(int x=rect.xMin;x<rect.xMax;x++)
                {
                    int col=x==rect.xMin?5:x==rect.xMax-1?7:6;
                    // Row 5 is the finished cliff foot. Row 4 is an optional wall
                    // extension: using both doubled the visible height.
                    surface.SetTile(new(x,rect.yMin-1,0),Get(terrace.Color,5,col));
                    shadow.SetTile(new(x,rect.yMin-1,0),shadowTile);
                }
            }
            for(int i=0;i<BattlefieldTerraces.ConnectorCount;i++)
            {
                var stair=BattlefieldTerraces.Connector(i);
                int level=stair.LowerHeight+1;
                surfaces[level].SetTile(new(stair.GridX,stair.UpperY,0),Get(stair.UpperSpriteName));
                surfaces[level].SetTile(new(stair.GridX,stair.LowerY,0),Get(stair.LowerSpriteName));
                shadows[level].SetTile(new(stair.GridX,stair.LowerY,0),null);
            }
        }

        private static Tilemap Layer(Transform parent,string name,int order)
        {
            Transform child=parent.Find(name);
            GameObject obj=child!=null?child.gameObject:new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer));
            obj.transform.SetParent(parent,false);
            obj.GetComponent<TilemapRenderer>().sortingOrder=order;
            return obj.GetComponent<Tilemap>();
        }

        private static Tile Get(int color,int row,int col)=>Get($"Tilemap_color{color}_r{row}c{col}");
        private static Tile Get(string name)
        {
            char color=name[13];
            Tile tile=AssetDatabase.LoadAssetAtPath<Tile>($"Assets/Tiles/Terrain/Color{color}/{name}.asset");
            if(tile==null) throw new InvalidOperationException("Missing terrain tile: "+name);
            return tile;
        }
    }
}
