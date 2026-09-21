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

        public GridCell(int gridX, int gridY, Vector3 worldPosition, TerrainType terrainType, int heightLevel)
        {
            GridX = gridX;
            GridY = gridY;
            WorldPosition = worldPosition;
            TerrainType = terrainType;
            Walkable = TerrainRules.IsWalkable(terrainType);
            HeightLevel = heightLevel;
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

            return heightDifference == 1
                && (TerrainType == TerrainType.Stairs || other.TerrainType == TerrainType.Stairs);
        }

        public override string ToString()
        {
            return $"({GridX}, {GridY}) {TerrainType} H{HeightLevel}";
        }
    }
}
