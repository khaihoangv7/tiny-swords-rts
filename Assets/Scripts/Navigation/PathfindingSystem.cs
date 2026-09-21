using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Navigation
{
    public sealed class PathfindingSystem : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;

        public GridManager GridManager => gridManager;

        private void Awake()
        {
            if (gridManager == null)
                gridManager = GetComponent<GridManager>();
        }

        public void SetGridManager(GridManager manager)
        {
            gridManager = manager;
        }

        public List<GridCell> FindPath(Vector2Int start, Vector2Int destination)
        {
            if (gridManager == null)
                return new List<GridCell>();

            return AStarPathfinder.FindPath(
                gridManager,
                gridManager.GetCell(start.x, start.y),
                gridManager.GetCell(destination.x, destination.y));
        }

        public List<Vector3> FindWorldPath(Vector3 startWorldPosition, Vector3 destinationWorldPosition)
        {
            if (gridManager == null)
                return new List<Vector3>();

            Vector2Int start = gridManager.WorldToGrid(startWorldPosition);
            Vector2Int destination = gridManager.WorldToGrid(destinationWorldPosition);
            List<GridCell> cells = FindPath(start, destination);
            var worldPath = new List<Vector3>(cells.Count);
            foreach (GridCell cell in cells)
                worldPath.Add(cell.WorldPosition);
            return worldPath;
        }
    }
}
