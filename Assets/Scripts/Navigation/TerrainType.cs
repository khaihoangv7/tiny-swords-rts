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

        // Height-connector endpoints are transit cells. Units may path through
        // them, but gameplay systems must not reserve them as persistent stand,
        // work, gather, spawn or formation destinations.
        public static bool IsConnectorCorridor(GridCell cell)
            => cell != null && (cell.TerrainType == TerrainType.Stairs
                || cell.HeightConnection.HasValue);

        public static bool IsStableStandingCell(GridCell cell)
            => cell != null && cell.Walkable && !cell.Occupied
                && !IsConnectorCorridor(cell)
                && (cell.TerrainType == TerrainType.FlatGround
                    || cell.TerrainType == TerrainType.ElevatedGround);
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
        public Vector2Int VisualLowerCell => new(VisualX, LowerY);
        public Vector2Int VisualUpperCell => new(VisualX, UpperY);
        public Vector2Int SurfaceConnectionCell => UpperCell;
        public Vector2Int CliffConnectionCell => new(UpperX, UpperY - 1);

        public bool ContainsCorridorCell(Vector2Int cell)
            => cell == LowerCell || cell == UpperCell;

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
