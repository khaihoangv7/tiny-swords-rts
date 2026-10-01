using UnityEngine;

namespace TinyKingdom.Navigation
{
    public sealed class GridCell
    {
        public int GridX { get; }
        public int GridY { get; }
        public Vector3 WorldPosition { get; }
        public TerrainType TerrainType { get; }
        public bool Walkable { get; }
        public int HeightLevel { get; }
        public bool Occupied { get; private set; }
        public Vector2Int? HeightConnection { get; }

        public GridCell(int gridX, int gridY, Vector3 worldPosition, TerrainType terrainType, int heightLevel,
            Vector2Int? heightConnection = null)
        {
            GridX = gridX;
            GridY = gridY;
            WorldPosition = worldPosition;
            TerrainType = terrainType;
            Walkable = TerrainRules.IsWalkable(terrainType);
            HeightLevel = heightLevel;
            HeightConnection = heightConnection;
        }

        public void SetOccupied(bool occupied)
        {
            Occupied = occupied;
        }

        public bool CanTraverseTo(GridCell other)
        {
            if (other == null || !other.Walkable || other.Occupied)
                return false;

            int heightDifference = Mathf.Abs(HeightLevel - other.HeightLevel);
            if (heightDifference == 0)
                return true;

            // Authored stairs permit exactly one height-crossing edge. Their other
            // sides cannot provide shortcuts through the surrounding cliff faces.
            if (HeightConnection.HasValue || other.HeightConnection.HasValue)
                return heightDifference == 1
                    && HeightConnection == new Vector2Int(other.GridX,other.GridY)
                    && other.HeightConnection == new Vector2Int(GridX,GridY);

            return heightDifference == 1
                && (TerrainType == TerrainType.Stairs || other.TerrainType == TerrainType.Stairs);
        }

        public override string ToString()
        {
            return $"({GridX}, {GridY}) {TerrainType} H{HeightLevel}";
        }
    }
}
