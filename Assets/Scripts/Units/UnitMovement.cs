using System.Collections.Generic;
using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController))]
    public sealed class UnitMovement : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float movementSpeed = 3f;
        [SerializeField, Min(0.001f)] private float arrivalTolerance = 0.02f;
        [SerializeField, Min(0.01f)] private float separationRadius = 0.35f;
        [SerializeField, Min(0.01f)] private float separationSpeed = 1.2f;
        [SerializeField] private bool drawPathWhenSelected = true;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private PathfindingSystem pathfindingSystem;

        private readonly WaypointPath activePath = new();
        private UnitController unitController;

        public float MovementSpeed => movementSpeed;
        public float SeparationRadius => Mathf.Max(0.01f, separationRadius);
        public float SeparationSpeed => Mathf.Max(0.01f, separationSpeed);
        public IReadOnlyList<Vector3> CurrentPath => activePath.Waypoints;
        public bool IsMoving => activePath.HasRemainingWaypoints;

        private void Awake()
        {
            unitController = GetComponent<UnitController>();
            unitController.StateChanged += HandleStateChanged;
            UnitCombatData combatData = GetComponent<HealthComponent>()?.CombatData;
            if (combatData != null)
                movementSpeed = combatData.MoveSpeed;
            ResolveNavigationReferences();
        }

        private void OnDestroy()
        {
            if (unitController != null)
                unitController.StateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            TickMovement(Time.deltaTime);
        }

        public void Configure(GridManager grid, PathfindingSystem pathfinding)
        {
            gridManager = grid;
            pathfindingSystem = pathfinding;
        }

        public bool TryIssueMove(Vector2Int destination)
        {
            return TryIssuePath(destination, false);
        }

        public bool TryIssueChase(Vector2Int destination)
        {
            return TryIssuePath(destination, true);
        }

        private bool TryIssuePath(Vector2Int destination, bool chasing)
        {
            unitController ??= GetComponent<UnitController>();
            if (!unitController.IsAlive)
                return false;
            if (chasing && unitController.CurrentState == UnitState.Guarding)
                return false;
            if (!chasing)
                GetComponent<UnitGuard>()?.EndGuard();
            ResolveNavigationReferences();
            if (gridManager == null || pathfindingSystem == null || !gridManager.IsInsideGrid(destination.x, destination.y))
                return false;

            GridCell destinationCell = gridManager.GetCell(destination.x, destination.y);
            if (destinationCell == null || !destinationCell.Walkable || destinationCell.Occupied)
                return false;

            Vector2Int start = gridManager.WorldToGrid(transform.position);
            List<GridCell> path = pathfindingSystem.FindPath(start, destination);
            if (path.Count == 0)
                return false;

            var worldPath = new List<Vector3>(path.Count);
            foreach (GridCell cell in path)
                worldPath.Add(new Vector3(cell.WorldPosition.x, cell.WorldPosition.y, transform.position.z));

            if (!chasing)
                GetComponent<CombatController>()?.StopCombat();
            activePath.Set(worldPath, transform.position, arrivalTolerance);
            unitController.SetState(activePath.HasRemainingWaypoints
                ? (chasing ? UnitState.Chasing : UnitState.Moving) : UnitState.Idle);
            return true;
        }

        public void Stop()
        {
            activePath.Clear();
            unitController ??= GetComponent<UnitController>();
            if (unitController.IsAlive)
                unitController.SetState(UnitState.Idle);
        }

        public void TickMovement(float deltaTime)
        {
            unitController ??= GetComponent<UnitController>();
            if (unitController.CurrentState == UnitState.Guarding)
            {
                activePath.Clear();
                return;
            }
            if (!unitController.IsAlive)
            {
                activePath.Clear();
                return;
            }
            ResolveNavigationReferences();
            Vector3 current = transform.position;
            Vector3 desired = current;
            bool hadPath = activePath.HasRemainingWaypoints;
            bool completed = false;
            int previousWaypoint = activePath.NextWaypointIndex;
            if (hadPath)
            {
                desired = activePath.Advance(current, movementSpeed, deltaTime,
                    arrivalTolerance, out completed);
                if (!UnitSeparation.IsSafeSegment(gridManager, current, desired))
                {
                    activePath.RestoreNextWaypoint(previousWaypoint);
                    desired = current;
                    completed = false;
                }
            }

            transform.position = UnitSeparation.Apply(this, desired, deltaTime, gridManager);
            if (hadPath && completed)
                Stop();
        }

        private void HandleStateChanged(UnitState state)
        {
            if (state == UnitState.Dead)
                activePath.Clear();
        }

        private void ResolveNavigationReferences()
        {
            if (gridManager == null)
                gridManager = FindAnyObjectByType<GridManager>();
            if (pathfindingSystem == null)
                pathfindingSystem = FindAnyObjectByType<PathfindingSystem>();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawPathWhenSelected || activePath.Waypoints.Count == 0)
                return;

            Gizmos.color = Color.magenta;
            for (int i = 0; i < activePath.Waypoints.Count; i++)
            {
                Gizmos.DrawSphere(activePath.Waypoints[i], 0.08f);
                if (i > 0)
                    Gizmos.DrawLine(activePath.Waypoints[i - 1], activePath.Waypoints[i]);
            }
        }
    }
}
