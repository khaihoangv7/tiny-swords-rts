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
        public int GridX { get; }
        public int LowerY { get; }
        public int UpperY { get; }
        public TerrainStairOrientation Orientation { get; }
        public string UpperSpriteName { get; }
        public string LowerSpriteName { get; }
        public int LowerHeight { get; }

        public TerrainHeightConnectorDefinition(int gridX, int lowerY, int upperY,
            TerrainStairOrientation orientation, string upperSpriteName,
            string lowerSpriteName, int lowerHeight = 0)
        {
            GridX = gridX;
            LowerY = lowerY;
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
