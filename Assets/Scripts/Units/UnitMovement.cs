using System.Collections.Generic;
using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController))]
    public sealed class UnitMovement : MonoBehaviour
    {
        public const float BlockedPathRepathInterval = 0.35f;

        [SerializeField, Min(0.01f)] private float movementSpeed = 3f;
        [SerializeField, Min(0.001f)] private float arrivalTolerance = 0.02f;
        [SerializeField, Min(0.01f)] private float commandDestinationEpsilon = 0.2f;
        [SerializeField, Min(0.01f)] private float separationRadius = 0.35f;
        [SerializeField, Min(0.01f)] private float separationSpeed = 1.2f;
        [SerializeField] private bool drawPathWhenSelected = true;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private PathfindingSystem pathfindingSystem;

        private readonly WaypointPath activePath = new();
        private UnitController unitController;
        private bool suspended;
        private bool hasCommandDestination;
        private Vector2Int commandDestinationCell;
        private Vector3? commandFinalPosition;
        private Vector3? commandPointerPosition;
        private bool hasChaseDestination;
        private Vector2Int chaseDestinationCell;
        private Vector3? chaseFinalPosition;
        private IPathTraversalPolicy activeTraversalPolicy;
        private float blockedRepathCooldown;
        private Vector2 lastMovementDirection = Vector2.right;

        public float MovementSpeed => movementSpeed;
        public GridManager GridManager => gridManager;
        public float SeparationRadius => Mathf.Max(0.01f, separationRadius);
        public float SeparationSpeed => Mathf.Max(0.01f, separationSpeed);
        public IReadOnlyList<Vector3> CurrentPath => activePath.Waypoints;
        public int CurrentPathWaypointIndex => activePath.NextWaypointIndex;
        public bool IsMoving => activePath.HasRemainingWaypoints;
        public bool IsSuspended => suspended;
        public float CommandDestinationEpsilon => commandDestinationEpsilon;
        public Vector2 LastMovementDirection => lastMovementDirection;

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
            => TryIssuePath(destination, false, null, false, null);

        public bool TryIssueMove(Vector2Int destination, Vector3 commandPosition)
            => TryIssuePath(destination, false, null, false, commandPosition);

        public bool TryIssueMove(Vector3 destination)
        {
            ResolveNavigationReferences();
            if (gridManager == null) return false;
            return TryIssuePath(gridManager.WorldToGrid(destination), false, destination, false,
                destination);
        }

        public bool TryIssueStrategicMove(Vector3 destination, IPathTraversalPolicy policy)
        {
            ResolveNavigationReferences();
            if (gridManager == null || policy == null) return false;
            return TryIssuePath(gridManager.WorldToGrid(destination), false, destination,
                false, destination, policy);
        }

        public bool TryIssueStrategicChase(Vector3 destination, IPathTraversalPolicy policy)
        {
            ResolveNavigationReferences();
            if (gridManager == null || policy == null) return false;
            return TryIssuePath(gridManager.WorldToGrid(destination), true, destination,
                false, null, policy);
        }

        public bool CanIssueMove(Vector2Int destination)
        {
            unitController ??= GetComponent<UnitController>();
            return unitController.IsAlive && TryFindPath(destination, out _);
        }

        internal bool TryIssueAttackMove(Vector2Int destination)
            => TryIssuePath(destination, false, null, true, null);

        internal bool TryIssueAttackMove(Vector2Int destination, Vector3 commandPosition)
            => TryIssuePath(destination, false, null, true, commandPosition);

        public bool TryIssueChase(Vector2Int destination)
        {
            return TryIssuePath(destination, true, null, false, null);
        }

        public bool TryIssueChase(Vector3 destination)
        {
            ResolveNavigationReferences();
            if (gridManager == null) return false;
            return TryIssuePath(gridManager.WorldToGrid(destination), true, destination, false,
                null);
        }

        private bool TryIssuePath(Vector2Int destination, bool chasing, Vector3? finalPosition,
            bool attackMove, Vector3? commandPosition,
            IPathTraversalPolicy traversalPolicy = null)
        {
            unitController ??= GetComponent<UnitController>();
            if (!unitController.IsAlive || suspended)
                return false;
            if (!chasing) GetComponent<UnitStationing>()?.CancelPending();
            if (chasing && unitController.CurrentState == UnitState.Guarding)
                return false;
            if (!chasing)
                GetComponent<UnitGuard>()?.EndGuard();

            bool equivalentCommand = !chasing && hasCommandDestination
                && destination == commandDestinationCell
                && EquivalentPoint(finalPosition, commandFinalPosition)
                && EquivalentPoint(commandPosition, commandPointerPosition);
            bool equivalentChase = chasing && hasChaseDestination
                && activePath.HasRemainingWaypoints
                && destination == chaseDestinationCell
                && EquivalentPoint(finalPosition, chaseFinalPosition);
            if (traversalPolicy == null && (equivalentCommand || equivalentChase))
            {
                if (!chasing && !attackMove)
                    GetComponent<CombatController>()?.OnMoveIssued();
                unitController.SetState(activePath.HasRemainingWaypoints
                    ? (chasing ? UnitState.Chasing : UnitState.Moving) : UnitState.Idle);
                return true;
            }
            if (!TryFindPath(destination, traversalPolicy, out List<GridCell> path))
                return false;

            if (!TrySetActivePath(path, finalPosition)) return false;

            if (!chasing && !attackMove)
                GetComponent<CombatController>()?.OnMoveIssued();
            activeTraversalPolicy = traversalPolicy;
            blockedRepathCooldown = 0f;
            if (!chasing)
            {
                hasChaseDestination = false;
                hasCommandDestination = true;
                commandDestinationCell = destination;
                commandFinalPosition = finalPosition;
                commandPointerPosition = commandPosition;
            }
            else
            {
                hasCommandDestination = false;
                hasChaseDestination = true;
                chaseDestinationCell = destination;
                chaseFinalPosition = finalPosition;
            }
            unitController.SetState(activePath.HasRemainingWaypoints
                ? (chasing ? UnitState.Chasing : UnitState.Moving) : UnitState.Idle);
            return true;
        }

        private bool EquivalentPoint(Vector3? candidate, Vector3? current)
        {
            if (!candidate.HasValue && !current.HasValue) return true;
            if (!candidate.HasValue || !current.HasValue) return false;
            return Vector2.Distance(candidate.Value, current.Value)
                <= commandDestinationEpsilon;
        }

        private bool TryFindPath(Vector2Int destination, out List<GridCell> path)
            => TryFindPath(destination, null, out path);

        private bool TryFindPath(Vector2Int destination, IPathTraversalPolicy policy,
            out List<GridCell> path)
        {
            path = null;
            ResolveNavigationReferences();
            if (gridManager == null || pathfindingSystem == null
                || !gridManager.IsInsideGrid(destination.x, destination.y))
                return false;

            GridCell destinationCell = gridManager.GetCell(destination.x, destination.y);
            if (destinationCell == null || !destinationCell.Walkable || destinationCell.Occupied)
                return false;

            Vector2Int start = gridManager.WorldToGrid(transform.position);
            path = pathfindingSystem.FindPath(start, destination, policy);
            return path.Count > 0;
        }

        public void Stop()
        {
            activePath.Clear();
            hasCommandDestination = false;
            commandFinalPosition = null;
            commandPointerPosition = null;
            hasChaseDestination = false;
            chaseFinalPosition = null;
            activeTraversalPolicy = null;
            blockedRepathCooldown = 0f;
            unitController ??= GetComponent<UnitController>();
            if (unitController.IsAlive)
                unitController.SetState(UnitState.Idle);
        }

        public void SetSuspended(bool value)
        {
            suspended = value;
            if (value) activePath.Clear();
        }

        public float ApplyDeterministicKnockback(Vector3 origin, float distance)
        {
            ResolveNavigationReferences();
            if (suspended || gridManager == null || distance <= 0f) return 0f;
            Vector2 direction = (Vector2)(transform.position - origin);
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();
            Vector3 start = transform.position;
            for (int step = 10; step >= 1; step--)
            {
                float amount = distance * step / 10f;
                Vector3 candidate = start + (Vector3)(direction * amount);
                Vector2Int cellPosition = gridManager.WorldToGrid(candidate);
                GridCell cell = gridManager.GetCell(cellPosition.x, cellPosition.y);
                if (cell == null || !cell.Walkable || cell.Occupied) continue;
                if (!UnitSeparation.IsSafeSegment(gridManager, start, candidate)) continue;
                transform.position = candidate;
                return amount;
            }
            return 0f;
        }

        public void TickMovement(float deltaTime)
        {
            unitController ??= GetComponent<UnitController>();
            if (suspended || unitController.CurrentState == UnitState.Guarding)
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
                    TryRepathBlockedRoute(deltaTime);
                }
                else blockedRepathCooldown = 0f;
            }

            Vector3 resolved = UnitSeparation.Apply(this, desired, deltaTime, gridManager);
            Vector2 movement = resolved - current;
            if (hadPath && movement.sqrMagnitude > 0.000001f)
                lastMovementDirection = movement.normalized;
            transform.position = resolved;
            if (hadPath && completed)
                Stop();
        }

        private void HandleStateChanged(UnitState state)
        {
            if (state == UnitState.Dead)
                activePath.Clear();
        }

        private void TryRepathBlockedRoute(float deltaTime)
        {
            if (!hasCommandDestination && !hasChaseDestination) return;
            blockedRepathCooldown -= Mathf.Max(0f, deltaTime);
            if (blockedRepathCooldown > 0f) return;
            blockedRepathCooldown = BlockedPathRepathInterval;

            Vector2Int destination = hasChaseDestination
                ? chaseDestinationCell : commandDestinationCell;
            Vector3? finalPosition = hasChaseDestination
                ? chaseFinalPosition : commandFinalPosition;
            if (!TryFindPath(destination, activeTraversalPolicy,
                    out List<GridCell> replacement))
                return;
            if (TrySetActivePath(replacement, finalPosition))
                blockedRepathCooldown = 0f;
        }

        private bool TrySetActivePath(IReadOnlyList<GridCell> path,
            Vector3? finalPosition)
        {
            if (path == null || path.Count == 0) return false;
            GridCell destinationCell = path[path.Count - 1];
            if (finalPosition.HasValue
                && !UnitSeparation.IsSafeSegment(gridManager,
                    destinationCell.WorldPosition, finalPosition.Value))
                return false;

            // A* includes the start cell. Reissuing or rebuilding a route while
            // between centers must not send the unit back to its previous center.
            var worldPath = new List<Vector3>(Mathf.Max(0, path.Count - 1));
            for (int i = path.Count > 1 ? 1 : 0; i < path.Count; i++)
            {
                GridCell cell = path[i];
                worldPath.Add(new Vector3(cell.WorldPosition.x, cell.WorldPosition.y,
                    transform.position.z));
            }
            if (finalPosition.HasValue)
            {
                if (path.Count == 1 && UnitSeparation.IsSafeSegment(gridManager,
                    transform.position, finalPosition.Value))
                    worldPath.Clear();
                else if (path.Count > 1 && UnitSeparation.IsSafeSegment(gridManager,
                    path[path.Count - 2].WorldPosition, finalPosition.Value))
                    worldPath.RemoveAt(worldPath.Count - 1);
                worldPath.Add(new Vector3(finalPosition.Value.x,
                    finalPosition.Value.y, transform.position.z));
            }
            activePath.Set(worldPath, transform.position, arrivalTolerance);
            return true;
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
