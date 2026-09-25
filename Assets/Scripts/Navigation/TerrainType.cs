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

        public TerrainHeightConnectorDefinition(int gridX, int lowerY, int upperY,
            TerrainStairOrientation orientation, string upperSpriteName,
            string lowerSpriteName)
        {
            GridX = gridX;
            LowerY = lowerY;
            UpperY = upperY;
            Orientation = orientation;
            UpperSpriteName = upperSpriteName;
            LowerSpriteName = lowerSpriteName;
        }
    }

    // Shared logical coordinates for the small central strategic-objective plateau.
    // The two two-cell connectors replace only their configured cliff columns.
    public static class StrategicObjectiveMapLayout
    {
        public const int MineOriginX = 19;
        public const int MineOriginY = 13;
        public const int MineWidth = 2;
        public const int MineHeight = 2;
        public const int LeftConnectorX = 17;
        public const int RightConnectorX = 22;
        public const int ConnectorLowerY = 11;
        public const int ConnectorUpperY = 12;
        public const int ConnectorCount = 2;

        public static TerrainHeightConnectorDefinition GetConnector(int index)
        {
            return index switch
            {
                0 => new TerrainHeightConnectorDefinition(LeftConnectorX,
                    ConnectorLowerY, ConnectorUpperY,
                    TerrainStairOrientation.DescendsLeft,
                    "Tilemap_color2_r4c0", "Tilemap_color2_r5c0"),
                1 => new TerrainHeightConnectorDefinition(RightConnectorX,
                    ConnectorLowerY, ConnectorUpperY,
                    TerrainStairOrientation.DescendsRight,
                    "Tilemap_color2_r4c3", "Tilemap_color2_r5c3"),
                _ => throw new System.ArgumentOutOfRangeException(nameof(index))
            };
        }

        public static bool IsConnectorColumn(int x)
            => x == LeftConnectorX || x == RightConnectorX;

        public static bool TryGetConnector(int x,
            out TerrainHeightConnectorDefinition connector)
        {
            for (int i = 0; i < ConnectorCount; i++)
            {
                TerrainHeightConnectorDefinition candidate = GetConnector(i);
                if (candidate.GridX != x) continue;
                connector = candidate;
                return true;
            }

            connector = default;
            return false;
        }

        public static bool TryGetConnectorCell(int x, int y, out int heightLevel)
        {
            heightLevel = 0;
            if (!TryGetConnector(x, out TerrainHeightConnectorDefinition connector))
                return false;
            if (y == connector.LowerY)
                return true;
            if (y != connector.UpperY)
                return false;
            heightLevel = 1;
            return true;
        }
    }
}
