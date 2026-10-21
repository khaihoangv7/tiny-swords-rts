using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.Navigation
{
    public enum PathfindingRequestSource
    {
        Gameplay,
        CommanderMove,
        CommanderGather,
        CommanderBuild,
        CommanderRally,
        CommanderScout,
        EconomyReturn,
        MilitaryMove,
        MilitaryFormation,
        MilitaryCombat,
        MilitaryRegroup,
        ResourceFallback
    }

    public sealed class PathfindingSystem : MonoBehaviour
    {
        private static readonly ProfilerMarker FindPathMarker =
            new("TinyKingdom.Pathfinding.FindPath");
        [System.ThreadStatic] private static PathfindingRequestSource currentSource;
        private static readonly long[] RequestsBySource = new long[
            System.Enum.GetValues(typeof(PathfindingRequestSource)).Length];

        public readonly struct RequestSourceScope : System.IDisposable
        {
            private readonly PathfindingRequestSource previous;

            internal RequestSourceScope(PathfindingRequestSource source)
            {
                previous = currentSource;
                currentSource = source;
            }

            public void Dispose() => currentSource = previous;
        }

        [SerializeField] private GridManager gridManager;

        public GridManager GridManager => gridManager;
        public static long TotalRequests { get; private set; }

        public static RequestSourceScope TrackCommanderRequest(
            PathfindingRequestSource source) => new(source);

        public static long GetRequestCount(PathfindingRequestSource source)
            => RequestsBySource[(int)source];

        public static void ResetPerformanceCounters()
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            TotalRequests = 0;
            System.Array.Clear(RequestsBySource, 0, RequestsBySource.Length);
#endif
        }

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
            => FindPath(start, destination, null);

        public List<GridCell> FindPath(Vector2Int start, Vector2Int destination,
            IPathTraversalPolicy policy)
            => FindPath(start, destination, policy, out _);

        public List<GridCell> FindPath(Vector2Int start, Vector2Int destination,
            IPathTraversalPolicy policy, out HashSet<GridCell> exhaustedReachable)
        {
            exhaustedReachable = null;
            using (FindPathMarker.Auto())
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                TotalRequests++;
                RequestsBySource[(int)currentSource]++;
#endif
                if (gridManager == null)
                    return new List<GridCell>();

                return AStarPathfinder.FindPath(
                    gridManager,
                    gridManager.GetCell(start.x, start.y),
                    gridManager.GetCell(destination.x, destination.y), policy,
                    out exhaustedReachable);
            }
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
