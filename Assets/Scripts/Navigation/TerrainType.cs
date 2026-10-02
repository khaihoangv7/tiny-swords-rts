using UnityEngine;

namespace TinyKingdom.Navigation
{
    public enum TerrainType
    {
        Water = 0,
        FlatGround = 1,
        Cliff = 2,
        ElevatedGround = 3,
        Stairs = 4
    }

    public static class TerrainRules
    {
        public static bool IsWalkable(TerrainType terrainType)
        {
            return terrainType == TerrainType.FlatGround
                || terrainType == TerrainType.ElevatedGround
                || terrainType == TerrainType.Stairs;
        }

        public static int DefaultHeight(TerrainType terrainType)
        {
            return terrainType == TerrainType.ElevatedGround ? 1 : 0;
        }
    }

    public enum TerrainStairOrientation
    {
        DescendsLeft = 0,
        DescendsRight = 1
    }

    public readonly struct TerrainHeightConnectorDefinition
    {
        public int LowerX { get; }
        public int LowerY { get; }
        public int UpperX { get; }
        public int UpperY { get; }
        public TerrainStairOrientation Orientation { get; }
        public string UpperSpriteName { get; }
        public string LowerSpriteName { get; }
        public int LowerHeight { get; }

        public Vector2Int LowerCell => new(LowerX, LowerY);
        public Vector2Int UpperCell => new(UpperX, UpperY);
        // Tiny Swords side-ramp halves are authored as one vertical visual pair.
        // Navigation endpoints remain diagonal and are intentionally independent.
        public int VisualX => LowerX;

        public TerrainHeightConnectorDefinition(int lowerX, int lowerY, int upperX, int upperY,
            TerrainStairOrientation orientation, string upperSpriteName,
            string lowerSpriteName, int lowerHeight = 0)
        {
            LowerX = lowerX;
            LowerY = lowerY;
            UpperX = upperX;
            UpperY = upperY;
            Orientation = orientation;
            UpperSpriteName = upperSpriteName;
            LowerSpriteName = lowerSpriteName;
            LowerHeight = lowerHeight;
        }
    }

    // Shared mine footprint in the flat central clearing.
    public static class StrategicObjectiveMapLayout
    {
        public const int MineOriginX = 29;
        public const int MineOriginY = 21;
        public const int MineWidth = 2;
        public const int MineHeight = 2;
    }
}
