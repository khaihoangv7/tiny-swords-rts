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
}
