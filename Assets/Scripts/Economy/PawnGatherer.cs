using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Match;
using UnityEngine;

namespace TinyKingdom.Economy
{
    public enum WorkerState { Idle, MovingToResource, Gathering, ReturningToDropOff, WaitingForDropOff }

    [RequireComponent(typeof(UnitController), typeof(UnitMovement))]
    public sealed class PawnGatherer : MonoBehaviour
    {
        [SerializeField, Min(1)] private int carryCapacity = 10;
        [SerializeField, Min(0.01f)] private float gatherIntervalSeconds = 1.5f;
        [SerializeField, Min(0f)] private float gatherImpactDelay = 0.45f;
        [SerializeField, Min(1)] private int amountPerGather = 1;
        [SerializeField, Range(0.05f, 0.3f)] private float resourceArrivalTolerance = 0.10f;
        [SerializeField, Min(0f)] private float autoResourceSearchRadius = 6f;
        [SerializeField] private bool logRouteFailures = false;

        private UnitController controller;
        private UnitMovement movement;
        private GridManager grid;
        private PathfindingSystem paths;
        private ResourceNode target;
        private CastleDropOff dropOff;
        private Vector2Int interactionCell;
        private float gatherTimer;
        private float dropOffRetry;
        private bool impactApplied;
        private Vector3 interactionPosition;
        private ResourceType carriedType;
        private int carriedAmount;
        private bool resourceJobActive;
        private ResourceType resourceJobType;

        public WorkerState State { get; private set; }
        public int CarryCapacity => carryCapacity;
        public float GatherIntervalSeconds => gatherIntervalSeconds;
        public int CarriedAmount => carriedAmount;
        public ResourceType CarriedResourceType => carriedType;
        public ResourceNode Target => target;
        public float AutoResourceSearchRadius => autoResourceSearchRadius;
        public event Action<ResourceType> WorkCycleStarted;
        public event Action<ResourceType, int> ResourceGathered;

        public void ConfigureGathering(float secondsPerUnit, float impactDelay, int unitsPerStep)
        {
            gatherIntervalSeconds = Mathf.Max(0.01f, secondsPerUnit);
            gatherImpactDelay = Mathf.Clamp(impactDelay, 0f, gatherIntervalSeconds);
            amountPerGather = Mathf.Max(1, unitsPerStep);
        }

        private void Awake()
        {
            controller = GetComponent<UnitController>();
            movement = GetComponent<UnitMovement>();
            ResolveNavigation();
            controller.StateChanged += OnUnitStateChanged;
        }

        private void OnDestroy()
        {
            if (controller != null) controller.StateChanged -= OnUnitStateChanged;
            ReleaseSlots();
        }

        private void OnDisable() => CancelJob();

        private void Update()
        {
            if (!MatchManager.AllowsGameplay)
            {
                if (State != WorkerState.Idle) CancelJob();
                return;
            }
            Tick(Time.deltaTime);
        }

        public bool Gather(ResourceNode node)
        {
            if (!MatchManager.AllowsGameplay || node == null || !node.IsAvailable
                || controller == null || !controller.IsAlive)
                return false;
            ResolveNavigation();
            if (grid == null || paths == null) return false;

            movement.Stop();
            CancelJob();
            resourceJobActive = true;
            resourceJobType = node.ResourceType;
            target = node;
            if (carriedAmount >= carryCapacity
                || (carriedAmount > 0 && carriedType != node.ResourceType))
                return BeginReturn(); // Existing cargo is deposited before changing type.
            // An unavailable interaction slot is a normal command rejection when
            // a group is ordered to one crowded node; keep the Console clean.
            bool accepted = BeginGatherTrip(false);
            if (!accepted) CancelJob();
            return accepted;
        }

        // Called only for a successful explicit Move command. Cargo is retained.
        public void CancelJob()
        {
            ReleaseSlots();
            target = null;
            dropOff = null;
            State = WorkerState.Idle;
            gatherTimer = 0f;
            impactApplied = false;
            dropOffRetry = 0f;
            resourceJobActive = false;
        }

        internal bool DepositCargoOnGarrisonEntry(CastleDropOff castle)
        {
            if (castle == null || !castle.IsAvailable || controller == null
                || castle.Faction != controller.Faction || carriedAmount <= 0)
                return false;
            castle.Deposit(carriedType, carriedAmount);
            carriedAmount = 0;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!MatchManager.AllowsGameplay || controller == null
                || !controller.IsAlive || State == WorkerState.Idle) return;
            switch (State)
            {
                case WorkerState.MovingToResource:
                    if (target == null || !target.IsAvailable)
                    {
                        HandleUnavailableResource();
                    }
                    else if (Arrived())
                    {
                        movement.Stop();
                        State = WorkerState.Gathering;
                        BeginWorkCycle();
                    }
                    break;
                case WorkerState.Gathering:
                    if (target == null || !target.IsAvailable || carriedAmount >= carryCapacity)
                    {
                        if (carriedAmount >= carryCapacity) BeginReturn();
                        else HandleUnavailableResource();
                        break;
                    }
                    gatherTimer += Mathf.Max(0f, deltaTime);
                    while (State == WorkerState.Gathering)
                    {
                        if (!impactApplied && gatherTimer >= gatherImpactDelay)
                        {
                            impactApplied = true;
                            ApplyGatherImpact();
                        }
                        if (State != WorkerState.Gathering || gatherTimer < gatherIntervalSeconds)
                            break;
                        gatherTimer -= gatherIntervalSeconds;
                        BeginWorkCycle();
                    }
                    break;
                case WorkerState.ReturningToDropOff:
                    if (dropOff == null || !dropOff.IsAvailable)
                    {
                        if (dropOff != null)
                            dropOff.Release(controller);
                        dropOff = null;
                        State = WorkerState.Idle; // Keep cargo if the Castle vanishes.
                    }
                    else if (ArrivedAt(interactionPosition, dropOff.ArrivalTolerance))
                    {
                        movement.Stop();
                        dropOff.Deposit(carriedType, carriedAmount);
                        carriedAmount = 0;
                        dropOff.Release(controller);
                        dropOff = null;
                        if (resourceJobActive && target != null && target.IsAvailable)
                            BeginGatherTrip();
                        else if (resourceJobActive && TryAutoRetarget()) { }
                        else CancelJob();
                    }
                    break;
                case WorkerState.WaitingForDropOff:
                    dropOffRetry -= Mathf.Max(0f, deltaTime);
                    if (dropOffRetry <= 0f) BeginReturn();
                    break;
            }
        }

        private bool BeginGatherTrip(bool logFailure = true)
        {
            if (target == null || !target.IsAvailable) { CancelJob(); return false; }
            ResourceInteractionSlots slots = target.GetComponent<ResourceInteractionSlots>();
            bool reserved = slots != null
                && slots.TryReserve(controller, grid, paths, out interactionCell);
            if (reserved)
                interactionPosition = target.InteractionPosition(interactionCell, grid);
            if (!reserved || !movement.TryIssueMove(interactionPosition))
            {
                if (logFailure && logRouteFailures)
                    Debug.LogWarning($"Gather route rejected: {name} -> {target.name}. "
                        + "Check the resource collider, interaction slots, and navigation.", this);
                slots?.Release(controller);
                State = WorkerState.Idle;
                if (logFailure)
                {
                    resourceJobActive = false;
                    target = null;
                }
                return false;
            }
            State = WorkerState.MovingToResource;
            return true;
        }

        private bool BeginReturn()
        {
            if (target != null)
                target.GetComponent<ResourceInteractionSlots>()?.Release(controller);
            if (carriedAmount <= 0) { CancelJob(); return false; }
            ResolveNavigation();
            CastleDropOff nearest = null;
            Vector3 bestPosition = default;
            float bestDistance = float.PositiveInfinity;
            IReadOnlyList<CastleDropOff> castles = CastleDropOff.Active;
            for (int i = 0; i < castles.Count; i++)
            {
                CastleDropOff candidate = castles[i];
                if (!candidate.IsAvailable || candidate.Faction != controller.Faction) continue;
                if (!candidate.TryReserve(controller, grid, paths, out Vector3 position)) continue;
                float distance = ((Vector2)position
                    - (Vector2)transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    if (nearest != null) nearest.Release(controller);
                    nearest = candidate;
                    bestPosition = position;
                    bestDistance = distance;
                }
                else candidate.Release(controller);
            }
            if (nearest == null || !movement.TryIssueChase(bestPosition))
            {
                if (logRouteFailures && State != WorkerState.WaitingForDropOff)
                    Debug.LogWarning($"No reachable Castle drop-off slot for {name}; waiting with cargo.", this);
                if (nearest != null) nearest.Release(controller);
                State = WorkerState.WaitingForDropOff;
                dropOffRetry = 0.5f;
                return true;
            }
            dropOff = nearest;
            interactionPosition = bestPosition;
            State = WorkerState.ReturningToDropOff;
            return true;
        }

        private bool Arrived()
        {
            // Only the completed path to this Pawn's reserved world-space slot
            // can authorize a work cycle; proximity to the node is irrelevant.
            return ArrivedAt(interactionPosition, resourceArrivalTolerance);
        }

        private bool ArrivedAt(Vector3 position, float tolerance)
            => !movement.IsMoving && Vector2.Distance(transform.position, position) <= tolerance;

        private void BeginWorkCycle()
        {
            gatherTimer = 0f;
            impactApplied = false;
            if (target != null) WorkCycleStarted?.Invoke(target.ResourceType);
        }

        private void ApplyGatherImpact()
        {
            int taken = target.Take(Mathf.Min(amountPerGather, carryCapacity - carriedAmount));
            if (taken > 0)
            {
                carriedType = target.ResourceType;
                carriedAmount += taken;
                ResourceGathered?.Invoke(carriedType, taken);
            }
            if (!target.IsAvailable || carriedAmount >= carryCapacity || taken == 0)
            {
                if (carriedAmount >= carryCapacity) BeginReturn();
                else HandleUnavailableResource();
            }
        }

        private void HandleUnavailableResource()
        {
            if (resourceJobActive && TryAutoRetarget()) return;
            if (carriedAmount > 0)
            {
                // No nearby same-type replacement: deposit partial cargo, then stop.
                resourceJobActive = false;
                BeginReturn();
            }
            else CancelJob();
        }

        private bool TryAutoRetarget()
        {
            if (!resourceJobActive || autoResourceSearchRadius <= 0f) return false;
            if (target != null)
                target.GetComponent<ResourceInteractionSlots>()?.Release(controller);
            target = null;
            float maximumSqr = autoResourceSearchRadius * autoResourceSearchRadius;
            var candidates = new List<ResourceNode>();
            foreach (ResourceNode node in ResourceNode.ActiveNodes)
            {
                if (node == null || !node.IsAvailable || node.ResourceType != resourceJobType)
                    continue;
                float distance = ((Vector2)(node.transform.position - transform.position)).sqrMagnitude;
                if (distance <= maximumSqr) candidates.Add(node);
            }
            candidates.Sort((a, b) =>
            {
                float da = ((Vector2)(a.transform.position - transform.position)).sqrMagnitude;
                float db = ((Vector2)(b.transform.position - transform.position)).sqrMagnitude;
                int distance = da.CompareTo(db);
                if (distance != 0) return distance;
                int x = a.transform.position.x.CompareTo(b.transform.position.x);
                if (x != 0) return x;
                int y = a.transform.position.y.CompareTo(b.transform.position.y);
                return y != 0 ? y : string.CompareOrdinal(a.name, b.name);
            });
            foreach (ResourceNode candidate in candidates)
            {
                target = candidate;
                if (BeginGatherTrip(false)) return true;
                candidate.GetComponent<ResourceInteractionSlots>()?.Release(controller);
                target = null;
            }
            return false;
        }

        private void ReleaseSlots()
        {
            if (target != null)
                target.GetComponent<ResourceInteractionSlots>()?.Release(controller);
            if (dropOff != null)
                dropOff.Release(controller);
        }

        private void OnUnitStateChanged(UnitState state)
        {
            if (state == UnitState.Dead) CancelJob();
        }

        private void ResolveNavigation()
        {
            grid ??= movement != null && movement.GridManager != null
                ? movement.GridManager : FindAnyObjectByType<GridManager>();
            paths ??= grid != null ? grid.GetComponent<PathfindingSystem>() : null;
        }
    }
}
