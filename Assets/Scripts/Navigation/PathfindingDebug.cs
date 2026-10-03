using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Navigation
{
    [ExecuteAlways]
    public sealed class PathfindingDebug : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        [SerializeField] private PathfindingSystem pathfindingSystem;
        [SerializeField] private bool drawGrid = true;
        [SerializeField] private bool drawPath = true;
        [SerializeField] private bool drawConnectorDetails = false;
        [SerializeField] private bool drawOnlyWhenSelected = true;
        [SerializeField] private Vector2Int startCell = new(3, 3);
        [SerializeField] private Vector2Int destinationCell = new(36, 24);
        [SerializeField, Range(0.05f, 1f)] private float cellFill = 0.82f;

        public void Configure(GridManager manager, PathfindingSystem system)
        {
            gridManager = manager;
            pathfindingSystem = system;
        }

        private void OnDrawGizmos()
        {
            if (!drawOnlyWhenSelected)
                DrawDebugView();
        }

        private void OnDrawGizmosSelected()
        {
            if (drawOnlyWhenSelected)
                DrawDebugView();
        }

        private void DrawDebugView()
        {
            if (!drawGrid && !drawPath && !drawConnectorDetails)
                return;
            if (gridManager == null || gridManager.NavigationData == null)
                return;

            gridManager.EnsureInitialized();
            if (drawGrid)
            {
                float size = gridManager.NavigationData.CellSize * cellFill;
                Vector3 cubeSize = new(size, size, 0.01f);
                for (int y = 0; y < gridManager.Height; y++)
                for (int x = 0; x < gridManager.Width; x++)
                {
                    GridCell cell = gridManager.GetCell(x, y);
                    Gizmos.color = GetCellColor(cell);
                    Gizmos.DrawCube(cell.WorldPosition, cubeSize);
                }
            }

            if (drawConnectorDetails)
                DrawConnectorDetails();

            if (!drawPath || pathfindingSystem == null)
                return;

            List<GridCell> path = pathfindingSystem.FindPath(startCell, destinationCell);
            Gizmos.color = Color.magenta;
            for (int i = 0; i < path.Count; i++)
            {
                Gizmos.DrawSphere(path[i].WorldPosition + Vector3.back * 0.1f, 0.14f);
                if (i > 0)
                    Gizmos.DrawLine(path[i - 1].WorldPosition, path[i].WorldPosition);
            }
        }

        private void DrawConnectorDetails()
        {
            float radius = gridManager.NavigationData.CellSize * 0.18f;
            float cellSize = gridManager.NavigationData.CellSize;
            for (int y = 0; y < gridManager.Height; y++)
            for (int x = 0; x < gridManager.Width; x++)
            {
                GridCell lower = gridManager.GetCell(x, y);
                if (lower == null || !lower.HeightConnection.HasValue) continue;
                Vector2Int partnerPosition = lower.HeightConnection.Value;
                GridCell upper = gridManager.GetCell(partnerPosition.x,
                    partnerPosition.y);
                if (upper == null || lower.HeightLevel >= upper.HeightLevel
                    || upper.HeightConnection != new Vector2Int(x, y)) continue;
                // Blue points: authored endpoints and connector centreline.
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(lower.WorldPosition + Vector3.back * 0.12f, radius);
                Gizmos.DrawSphere(upper.WorldPosition + Vector3.back * 0.12f, radius);
                Gizmos.DrawLine(lower.WorldPosition + Vector3.back * 0.12f,
                    upper.WorldPosition + Vector3.back * 0.12f);

                // Yellow cells: exact no-build/no-gather transit corridor.
                Gizmos.color = Color.yellow;
                Vector3 cellSize3 = new(cellSize * 0.9f, cellSize * 0.9f, 0f);
                Gizmos.DrawWireCube(lower.WorldPosition + Vector3.back * 0.1f, cellSize3);
                Gizmos.DrawWireCube(upper.WorldPosition + Vector3.back * 0.1f, cellSize3);

                // Green confirms both logical stair cells remain pathable.
                if (lower.Walkable && upper.Walkable)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(lower.WorldPosition + Vector3.back * 0.13f,
                        radius * 0.55f);
                    Gizmos.DrawWireSphere(upper.WorldPosition + Vector3.back * 0.13f,
                        radius * 0.55f);
                }

                // Red marks only immediate blocked neighbours, making a cliff or
                // building intruding into the painted opening easy to spot.
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    GridCell neighbour = gridManager.GetCell(x + ox, y + oy);
                    if (neighbour == null || neighbour.Walkable && !neighbour.Occupied) continue;
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(neighbour.WorldPosition + Vector3.back * 0.08f,
                        cellSize3 * 0.82f);
                }
            }
        }

        private static Color GetCellColor(GridCell cell)
        {
            if (cell.Occupied)
                return new Color(0.55f, 0f, 0.55f, 0.45f);

            return cell.TerrainType switch
            {
                TerrainType.Water => new Color(0f, 0.25f, 0.8f, 0.22f),
                TerrainType.Cliff => new Color(0.9f, 0.1f, 0.05f, 0.38f),
                TerrainType.ElevatedGround => new Color(1f, 0.75f, 0f, 0.30f),
                TerrainType.Stairs => new Color(0.1f, 1f, 1f, 0.38f),
                _ => new Color(0f, 0.8f, 0.2f, 0.16f)
            };
        }
    }
}
