using System;
using System.Collections.Generic;
using TinyKingdom.Audio;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Match;
using TinyKingdom.Vision;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.Economy
{
    public enum WorkerState
    {
        Idle,
        MovingToResource,
        MovingToRememberedResource,
        Gathering,
        ReturningToDropOff,
        WaitingForDropOff
    }

    [RequireComponent(typeof(UnitController), typeof(UnitMovement))]
    public sealed class PawnGatherer : MonoBehaviour
    {
        public const float CargoRecoveryTimeoutSeconds = 3f;
        public const float DropOffRetryBaseSeconds = 1f;
        public const float DropOffRetryMaximumSeconds = 8f;
        public const float FailedDropOffCooldownSeconds = 5f;

        private static readonly ProfilerMarker EconomyTickMarker =
            new("TinyKingdom.Economy.PawnTick");
        private static readonly ProfilerMarker CargoRecoveryMarker =
            new("TinyKingdom.Economy.CargoRecovery");
        private static readonly Vector2Int[] RememberedApproachOffsets =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up,
            new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)
        };

        [SerializeField, Min(1)] private int carryCapacity = 20;
        [SerializeField, Min(0.01f)] private float gatherIntervalSeconds = 1.5f;
        [SerializeField, Min(0f)] private float gatherImpactDelay = 0.45f;
        [SerializeField, Min(1)] private int amountPerGather = 2;
        [SerializeField, Range(0.05f, 0.3f)] private float resourceArrivalTolerance = 0.10f;
        [SerializeField, Min(0f), Tooltip("0 searches all legally explored resource memory.")]
        private float autoResourceSearchRadius = 0f;
        [SerializeField] private bool logRouteFailures = false;

        private UnitController controller;
        private UnitMovement movement;
        private GridManager grid;
        private PathfindingSystem paths;
        private ResourceNode target;
        private IResourceDropOff dropOff;
        private Vector2Int interactionCell;
        private float gatherTimer;
        private float lastGatherImpactTime = float.NegativeInfinity;
        private float dropOffRetry;
        private int consecutiveDropOffFailures;
        private int dropOffPathFailureCount;
        private int cargoRecoveryCount;
        private bool hasRejectedDropOff;
        private EntityId rejectedDropOffId;
        private float rejectedDropOffUntil;
        private float timeSinceLastEconomicProgress;
        private Vector3 lastEconomicPosition;
        private int lastEconomicCarriedAmount;
        private WorkerState lastEconomicState;
        private bool economicProgressInitialized;
        private bool impactApplied;
        private Vector3 interactionPosition;
        private ResourceType carriedType;
        private int carriedAmount;
        private bool resourceJobActive;
        private ResourceType resourceJobType;
        private Predicate<ResourceNode> autoRetargetEligibility;
        private Predicate<Vector3> autoRetargetPositionEligibility;
        private IPathTraversalPolicy strategicTraversal;
        private readonly List<IResourceDropOff> dropOffCandidates = new();
        private readonly List<ResourceNode> visibleResourceCandidates = new();
        private readonly List<ResourceMemorySnapshot> rememberedResourceCandidates = new();
        private Comparison<IResourceDropOff> dropOffComparison;
        private Comparison<ResourceNode> visibleResourceComparison;
        private Comparison<ResourceMemorySnapshot> rememberedResourceComparison;
        private bool hasRememberedResource;
        private EntityId rememberedResourceId;
        private Vector3 rememberedResourcePosition;
        private float rememberedRevealWait;
        private readonly HashSet<EntityId> rejectedRememberedResources = new();

        public WorkerState State { get; private set; }
        public int CarryCapacity => carryCapacity;
        public float GatherIntervalSeconds => gatherIntervalSeconds;
        public int AmountPerGather => amountPerGather;
        public int CarriedAmount => carriedAmount;
        public ResourceType CarriedResourceType => carriedType;
        public ResourceType CurrentJobResourceType => resourceJobType;
        public ResourceNode Target => target;
        public bool HasDropOffTarget => ResourceDropOffRegistry.IsUsable(dropOff,
            controller != null ? controller.Faction : default);
        public EntityId DropOffTargetId => HasDropOffTarget ? dropOff.RuntimeId : default;
        public bool HasResourceJob => resourceJobActive;
        public Vector2Int ReservedGatherCell => interactionCell;
        public int DropOffPathFailureCount => dropOffPathFailureCount;
        public int CargoRecoveryCount => cargoRecoveryCount;
        public float TimeSinceLastEconomicProgress => timeSinceLastEconomicProgress;
        public float AutoResourceSearchRadius => autoResourceSearchRadius;
        public bool IsSeekingRememberedResource => hasRememberedResource;
        public EntityId RememberedResourceId => rememberedResourceId;
        public Vector3 RememberedResourcePosition => rememberedResourcePosition;
        public event Action<ResourceType> WorkCycleStarted;
        public event Action<ResourceType, int> ResourceGathered;

        public static bool IsAutomaticResourcePositionKnown(Faction faction,
            Vector3 position)
        {
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            return vision != null && vision.IsReady && vision.IsExplored(faction, position);
        }

        public void ConfigureGathering(float secondsPerUnit, float impactDelay, int unitsPerStep)
            => ConfigureGathering(secondsPerUnit, impactDelay, unitsPerStep, carryCapacity);

        public void ConfigureGathering(float secondsPerUnit, float impactDelay, int unitsPerStep,
            int capacity)
        {
            gatherIntervalSeconds = Mathf.Max(0.01f, secondsPerUnit);
            gatherImpactDelay = Mathf.Clamp(impactDelay, 0f, gatherIntervalSeconds);
            amountPerGather = Mathf.Max(1, unitsPerStep);
            carryCapacity = Mathf.Max(1, capacity);
        }

        private void Awake()
        {
            controller = GetComponent<UnitController>();
            movement = GetComponent<UnitMovement>();
            ResolveNavigation();
            controller.StateChanged += OnUnitStateChanged;
            dropOffComparison = CompareDropOffCandidates;
            visibleResourceComparison = CompareVisibleResources;
            rememberedResourceComparison = CompareRememberedResources;
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
            => Gather(node, null, null);

        public bool Gather(ResourceNode node, Predicate<ResourceNode> retargetEligibility,
            IPathTraversalPolicy traversalPolicy)
            => Gather(node, retargetEligibility, null, traversalPolicy);

        public bool Gather(ResourceNode node, Predicate<ResourceNode> retargetEligibility,
            Predicate<Vector3> retargetPositionEligibility,
            IPathTraversalPolicy traversalPolicy)
            => Gather(node, retargetEligibility, retargetPositionEligibility,
                traversalPolicy, true);

        public bool Gather(ResourceNode node, Predicate<ResourceNode> retargetEligibility,
            Predicate<Vector3> retargetPositionEligibility,
            IPathTraversalPolicy traversalPolicy, bool tryAlternateOnCommand)
        {
            if (!MatchManager.AllowsGameplay || node == null || !node.IsAvailable
                || controller == null || !controller.IsAlive)
                return false;
            // Repeating an order to the current resource must not restart its
            // work animation, impact timer, reserved slot, or return trip.
            // A stopped route that has not arrived can still be retried.
            if (resourceJobActive && target == node
                && (State == WorkerState.Gathering
                    || State == WorkerState.ReturningToDropOff
                    || State == WorkerState.WaitingForDropOff
                    || (State == WorkerState.MovingToResource
                        && (movement.IsMoving || Arrived()))))
            {
                autoRetargetEligibility = retargetEligibility;
                autoRetargetPositionEligibility = retargetPositionEligibility;
                strategicTraversal = traversalPolicy;
                return true;
            }
            ResolveNavigation();
            if (grid == null || paths == null) return false;

            movement.Stop();
            CancelJob();
            autoRetargetEligibility = retargetEligibility;
            autoRetargetPositionEligibility = retargetPositionEligibility;
            strategicTraversal = traversalPolicy;
            rejectedRememberedResources.Clear();
            resourceJobActive = true;
            resourceJobType = node.ResourceType;
            target = node;
            if (carriedAmount >= carryCapacity
                || (carriedAmount > 0 && carriedType != node.ResourceType))
                return BeginReturn(); // Existing cargo is deposited before changing type.
            // An unavailable interaction slot is a normal command rejection when
            // a group is ordered to one crowded node. Overflow workers retain the
            // same resource intent and reserve another legal same-type node.
            bool accepted = BeginGatherTrip(false);
            if (!accepted && tryAlternateOnCommand) accepted = TryAutoRetarget();
            if (!accepted)
            {
                // Never discard the resource intent while cargo still needs to be
                // delivered. The commander may have selected a depleted/crowded node.
                if (carriedAmount > 0) accepted = BeginReturn();
                else CancelJob();
            }
            return accepted;
        }

        // Used by the authoritative AI command service for economy-role workers.
        // It is deliberately not called globally, so manual player movement and the
        // promoted Scout role retain ownership of their Pawn.
        public bool TryRecoverCargoReturn(Predicate<ResourceNode> retargetEligibility,
            Predicate<Vector3> retargetPositionEligibility,
            IPathTraversalPolicy traversalPolicy)
        {
            if (!MatchManager.AllowsGameplay || controller == null || !controller.IsAlive
                || carriedAmount <= 0) return false;
            if (State == WorkerState.ReturningToDropOff
                && ResourceDropOffRegistry.IsUsable(dropOff, controller.Faction)
                && movement.IsMoving) return true;
            if (State == WorkerState.WaitingForDropOff) return true;

            if (!resourceJobActive)
            {
                resourceJobActive = true;
                resourceJobType = carriedType;
                rejectedRememberedResources.Clear();
            }
            if (retargetEligibility != null)
                autoRetargetEligibility = retargetEligibility;
            if (retargetPositionEligibility != null)
                autoRetargetPositionEligibility = retargetPositionEligibility;
            if (traversalPolicy != null) strategicTraversal = traversalPolicy;
            cargoRecoveryCount++;
            return BeginReturn();
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
            consecutiveDropOffFailures = 0;
            hasRejectedDropOff = false;
            ResetEconomicProgress();
            resourceJobActive = false;
            autoRetargetEligibility = null;
            autoRetargetPositionEligibility = null;
            strategicTraversal = null;
            ClearRememberedResource();
            rejectedRememberedResources.Clear();
        }

        internal bool DepositCargoOnGarrisonEntry(CastleDropOff castle)
        {
            if (castle == null || !castle.IsAvailable || controller == null
                || castle.Faction != controller.Faction || carriedAmount <= 0)
                return false;
            if (!castle.Deposit(carriedType, carriedAmount)) return false;
            carriedAmount = 0;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!MatchManager.AllowsGameplay || controller == null
                || !controller.IsAlive) return;
            using var marker = EconomyTickMarker.Auto();
            UpdateEconomicProgress(deltaTime);
            if (State == WorkerState.Idle)
            {
                if (resourceJobActive && carriedAmount > 0
                    && timeSinceLastEconomicProgress >= CargoRecoveryTimeoutSeconds)
                {
                    cargoRecoveryCount++;
                    BeginReturn();
                }
                return;
            }
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
                case WorkerState.MovingToRememberedResource:
                    TickRememberedResource(deltaTime);
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
                        if (!impactApplied && gatherTimer >= gatherImpactDelay
                            && Time.time - lastGatherImpactTime >= gatherIntervalSeconds)
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
                    if (!ResourceDropOffRegistry.IsUsable(dropOff, controller.Faction))
                    {
                        ReleaseDropOffReservation();
                        dropOff = null;
                        BeginReturn();
                    }
                    else if (ArrivedAt(interactionPosition, dropOff.ArrivalTolerance))
                    {
                        movement.Stop();
                        bool deposited = dropOff.Deposit(carriedType, carriedAmount);
                        if (deposited) carriedAmount = 0;
                        IResourceDropOff completedDropOff = dropOff;
                        completedDropOff.Release(controller);
                        dropOff = null;
                        if (!deposited) BeginReturn();
                        else if (GetComponent<PawnScoutPromotion>()?.IsPromoted == true)
                            CancelJob();
                        else if (resourceJobActive && target != null && target.IsAvailable
                            && BeginGatherTrip(false)) { }
                        else if (resourceJobActive && TryAutoRetarget()) { }
                        else CancelJob();
                    }
                    else if (!movement.IsMoving)
                    {
                        RejectCurrentDropOff();
                        dropOff.Release(controller);
                        dropOff = null;
                        State = WorkerState.WaitingForDropOff;
                        ScheduleDropOffRetry();
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
            if (target == null || !target.IsAvailable) return false;
            ResourceInteractionSlots slots = target.GetComponent<ResourceInteractionSlots>();
            bool reserved = slots != null
                && slots.TryReserve(controller, grid, paths, strategicTraversal,
                    out interactionCell);
            if (reserved)
                interactionPosition = target.InteractionPosition(interactionCell, grid);
            bool moved = reserved && (strategicTraversal == null
                ? movement.TryIssueMove(interactionPosition)
                : movement.TryIssueStrategicMove(interactionPosition, strategicTraversal));
            if (!moved)
            {
                if (logFailure && logRouteFailures)
                    Debug.LogWarning($"Gather route rejected: {name} -> {target.name}. "
                        + "Check the resource collider, interaction slots, and navigation.", this);
                slots?.Release(controller);
                State = WorkerState.Idle;
                return false;
            }
            State = WorkerState.MovingToResource;
            return true;
        }

        private bool BeginReturn()
        {
            using var marker = CargoRecoveryMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.EconomyReturn);
            if (target != null)
                target.GetComponent<ResourceInteractionSlots>()?.Release(controller);
            if (carriedAmount <= 0) { CancelJob(); return false; }
            ResolveNavigation();
            if (dropOff != null)
            {
                ReleaseDropOffReservation();
                dropOff = null;
            }
            dropOffCandidates.Clear();
            IReadOnlyList<IResourceDropOff> registered = ResourceDropOffRegistry.Active;
            for (int i = 0; i < registered.Count; i++)
                if (ResourceDropOffRegistry.IsUsable(registered[i], controller.Faction))
                    dropOffCandidates.Add(registered[i]);
            dropOffCandidates.Sort(dropOffComparison);
            for (int i = 0; i < dropOffCandidates.Count; i++)
            {
                IResourceDropOff candidate = dropOffCandidates[i];
                if (hasRejectedDropOff && Time.time < rejectedDropOffUntil
                    && candidate.RuntimeId.CompareTo(rejectedDropOffId) == 0) continue;
                if (!candidate.TryReserve(controller, grid, paths, strategicTraversal,
                    out Vector3 position)) continue;
                bool moved = strategicTraversal == null
                    ? movement.TryIssueChase(position)
                    : movement.TryIssueStrategicChase(position, strategicTraversal);
                if (!moved)
                {
                    candidate.Release(controller);
                    continue;
                }
                dropOff = candidate;
                interactionPosition = position;
                State = WorkerState.ReturningToDropOff;
                consecutiveDropOffFailures = 0;
                ResetEconomicProgress();
                return true;
            }
            if (logRouteFailures && State != WorkerState.WaitingForDropOff)
                Debug.LogWarning($"No reachable Castle/Tower drop-off slot for {name}; waiting with cargo.", this);
            State = WorkerState.WaitingForDropOff;
            dropOffPathFailureCount++;
            ScheduleDropOffRetry();
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
                // The gameplay cooldown belongs to the Pawn, not the currently
                // targeted node or animation cycle. Switching nodes cannot
                // shorten the 1.5-second interval between successful impacts.
                lastGatherImpactTime = Time.time;
                carriedType = target.ResourceType;
                carriedAmount += taken;
                ResourceGathered?.Invoke(carriedType, taken);
                GameplayAudioEvent? eventId = carriedType switch
                {
                    ResourceType.Gold => GameplayAudioEvent.PawnMining,
                    ResourceType.Wood => GameplayAudioEvent.PawnChopping,
                    _ => null
                };
                if (eventId.HasValue)
                {
                    Vector3 soundPosition = Vector3.Lerp(transform.position,
                        target.transform.position, 0.5f);
                    GameplayAudioService.Request(eventId.Value, soundPosition,
                        controller.Faction);
                }
            }
            if (!target.IsAvailable || carriedAmount >= carryCapacity || taken == 0)
            {
                if (carriedAmount >= carryCapacity) BeginReturn();
                else HandleUnavailableResource();
            }
        }

        private void HandleUnavailableResource()
        {
            if (carriedAmount > 0)
            {
                FinishResourceSearch();
                return;
            }
            if (resourceJobActive && TryAutoRetarget()) return;
            FinishResourceSearch();
        }

        private void FinishResourceSearch()
        {
            if (carriedAmount > 0)
            {
                // Deposit partial cargo first. Retain the resource intent so a newly
                // available equivalent node can be selected after the deposit.
                BeginReturn();
            }
            else CancelJob();
        }

        private bool TryAutoRetarget()
        {
            if (!resourceJobActive || controller == null) return false;
            if (target != null)
                target.GetComponent<ResourceInteractionSlots>()?.Release(controller);
            target = null;
            ClearRememberedResource();
            ResolveNavigation();
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            if (grid == null || paths == null || vision == null || !vision.IsReady) return false;

            visibleResourceCandidates.Clear();
            IReadOnlyList<ResourceNode> active = ResourceNode.ActiveNodes;
            for (int i = 0; i < active.Count; i++)
            {
                ResourceNode node = active[i];
                if (node == null
                    || !vision.IsVisible(controller.Faction, node.transform.position)
                    || !IsWithinAutoSearchRange(node.transform.position)) continue;
                if (!node.IsAvailable || node.ResourceType != resourceJobType) continue;
                if (autoRetargetEligibility != null && !autoRetargetEligibility(node))
                    continue;
                if (autoRetargetPositionEligibility != null
                    && !autoRetargetPositionEligibility(node.transform.position)) continue;
                ResourceInteractionSlots slots = node.GetComponent<ResourceInteractionSlots>();
                if (slots == null || !slots.HasAvailableCapacity(grid)) continue;
                visibleResourceCandidates.Add(node);
            }
            visibleResourceCandidates.Sort(visibleResourceComparison);
            for (int i = 0; i < visibleResourceCandidates.Count; i++)
            {
                ResourceNode candidate = visibleResourceCandidates[i];
                target = candidate;
                if (BeginGatherTrip(false)) return true;
                candidate.GetComponent<ResourceInteractionSlots>()?.Release(controller);
                target = null;
            }

            FogMemorySystem memory = FogMemorySystem.Instance;
            if (memory == null || !memory.IsInitialized) return false;
            rememberedResourceCandidates.Clear();
            foreach (ResourceMemorySnapshot snapshot in
                memory.GetLastSeenResources(controller.Faction))
            {
                if (snapshot == null || snapshot.ResourceType != resourceJobType
                    || snapshot.RemainingAmount <= 0
                    || vision.IsVisible(controller.Faction, snapshot.LogicalPosition)
                    || !IsAutomaticResourcePositionKnown(controller.Faction,
                        snapshot.LogicalPosition)
                    || !IsWithinAutoSearchRange(snapshot.LogicalPosition)) continue;
                if (rejectedRememberedResources.Contains(snapshot.RuntimeId))
                    continue;
                if (autoRetargetPositionEligibility != null
                    && !autoRetargetPositionEligibility(snapshot.LogicalPosition)) continue;
                rememberedResourceCandidates.Add(snapshot);
            }
            rememberedResourceCandidates.Sort(rememberedResourceComparison);
            for (int i = 0; i < rememberedResourceCandidates.Count; i++)
                if (BeginRememberedTrip(rememberedResourceCandidates[i], vision)) return true;
            return false;
        }

        private bool BeginRememberedTrip(ResourceMemorySnapshot snapshot,
            TeamVisionSystem vision)
        {
            int triedMask = 0;
            for (int attempt = 0; attempt < RememberedApproachOffsets.Length; attempt++)
            {
                int bestIndex = -1;
                float bestDistance = float.PositiveInfinity;
                for (int i = 0; i < RememberedApproachOffsets.Length; i++)
                {
                    if ((triedMask & (1 << i)) != 0) continue;
                    Vector2Int cell = snapshot.LogicalCell + RememberedApproachOffsets[i];
                    GridCell candidate = grid.GetCell(cell.x, cell.y);
                    if (candidate == null || !candidate.Walkable || candidate.Occupied
                        || vision.GetVisionState(controller.Faction, cell)
                            == VisionState.Unexplored) continue;
                    if (autoRetargetPositionEligibility != null
                        && !autoRetargetPositionEligibility(candidate.WorldPosition)) continue;
                    float distance = ((Vector2)(candidate.WorldPosition
                        - transform.position)).sqrMagnitude;
                    if (bestIndex >= 0 && distance >= bestDistance) continue;
                    bestIndex = i;
                    bestDistance = distance;
                }
                if (bestIndex < 0) return false;
                triedMask |= 1 << bestIndex;
                Vector2Int destination = snapshot.LogicalCell
                    + RememberedApproachOffsets[bestIndex];
                Vector3 position = grid.GridToWorld(destination.x, destination.y);
                bool moved = strategicTraversal == null
                    ? movement.TryIssueMove(position)
                    : movement.TryIssueStrategicMove(position, strategicTraversal);
                if (!moved) continue;
                rememberedResourceId = snapshot.RuntimeId;
                rememberedResourcePosition = snapshot.LogicalPosition;
                interactionPosition = position;
                rememberedRevealWait = Mathf.Max(0.2f, vision.UpdateInterval * 2f);
                hasRememberedResource = true;
                State = WorkerState.MovingToRememberedResource;
                return true;
            }
            return false;
        }

        private void TickRememberedResource(float deltaTime)
        {
            if (!hasRememberedResource)
            {
                HandleUnavailableResource();
                return;
            }
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            bool rememberedCellVisible = vision != null && vision.IsReady
                && vision.IsVisible(controller.Faction, rememberedResourcePosition);
            if (rememberedCellVisible)
            {
                ResourceNode resolved = ResolveRememberedResource(vision);
                if (resolved != null && resolved.IsAvailable
                    && resolved.ResourceType == resourceJobType)
                {
                    target = resolved;
                    ClearRememberedResource();
                    if (BeginGatherTrip(false)) return;
                    resolved.GetComponent<ResourceInteractionSlots>()?.Release(controller);
                    target = null;
                }
                RejectRememberedResource();
                if (TryAutoRetarget()) return;
                FinishResourceSearch();
                return;
            }
            if (movement.IsMoving) return;
            rememberedRevealWait -= Mathf.Max(0f, deltaTime);
            if (rememberedRevealWait > 0f) return;
            RejectRememberedResource();
            if (TryAutoRetarget()) return;
            FinishResourceSearch();
        }

        private ResourceNode ResolveRememberedResource(TeamVisionSystem vision)
        {
            IReadOnlyList<ResourceNode> nodes = ResourceNode.ActiveNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNode node = nodes[i];
                if (node == null || node.GetEntityId().CompareTo(rememberedResourceId) != 0)
                    continue;
                return vision.IsVisible(controller.Faction, node.transform.position)
                    ? node : null;
            }
            return null;
        }

        private void RejectRememberedResource()
        {
            if (hasRememberedResource)
                rejectedRememberedResources.Add(rememberedResourceId);
            ClearRememberedResource();
        }

        private void ClearRememberedResource()
        {
            hasRememberedResource = false;
            rememberedResourceId = default;
            rememberedResourcePosition = default;
            rememberedRevealWait = 0f;
        }

        private bool IsWithinAutoSearchRange(Vector3 position)
            => autoResourceSearchRadius <= 0f
                || ((Vector2)(position - transform.position)).sqrMagnitude
                    <= autoResourceSearchRadius * autoResourceSearchRadius;

        private int CompareVisibleResources(ResourceNode first, ResourceNode second)
        {
            float firstDistance = ((Vector2)(first.transform.position
                - transform.position)).sqrMagnitude;
            float secondDistance = ((Vector2)(second.transform.position
                - transform.position)).sqrMagnitude;
            int distance = firstDistance.CompareTo(secondDistance);
            return distance != 0 ? distance
                : first.GetEntityId().CompareTo(second.GetEntityId());
        }

        private int CompareRememberedResources(ResourceMemorySnapshot first,
            ResourceMemorySnapshot second)
        {
            float firstDistance = ((Vector2)(first.LogicalPosition
                - transform.position)).sqrMagnitude;
            float secondDistance = ((Vector2)(second.LogicalPosition
                - transform.position)).sqrMagnitude;
            int distance = firstDistance.CompareTo(secondDistance);
            return distance != 0 ? distance : first.RuntimeId.CompareTo(second.RuntimeId);
        }

        private int CompareDropOffCandidates(IResourceDropOff first,
            IResourceDropOff second)
        {
            float firstDistance = ((Vector2)(first.WorldPosition
                - transform.position)).sqrMagnitude;
            float secondDistance = ((Vector2)(second.WorldPosition
                - transform.position)).sqrMagnitude;
            int distance = firstDistance.CompareTo(secondDistance);
            return distance != 0 ? distance : first.RuntimeId.CompareTo(second.RuntimeId);
        }

        private void ReleaseSlots()
        {
            if (target != null)
                target.GetComponent<ResourceInteractionSlots>()?.Release(controller);
            ReleaseDropOffReservation();
        }

        private void ReleaseDropOffReservation()
        {
            if (dropOff == null) return;
            if (dropOff is UnityEngine.Object unityObject && unityObject == null) return;
            dropOff.Release(controller);
        }

        private void UpdateEconomicProgress(float deltaTime)
        {
            if (!economicProgressInitialized)
            {
                ResetEconomicProgress();
                return;
            }
            bool progressed = State != lastEconomicState
                || carriedAmount != lastEconomicCarriedAmount
                || ((Vector2)(transform.position - lastEconomicPosition)).sqrMagnitude
                    >= 0.0025f;
            if (progressed)
            {
                ResetEconomicProgress();
                return;
            }
            timeSinceLastEconomicProgress += Mathf.Max(0f, deltaTime);
            if (State != WorkerState.ReturningToDropOff || !movement.IsMoving
                || timeSinceLastEconomicProgress < CargoRecoveryTimeoutSeconds) return;

            // A path can be topologically valid yet make no physical progress because
            // its reserved edge is congested. Release it once and let the bounded
            // retry choose the next reachable Castle/Tower.
            movement.Stop();
            RejectCurrentDropOff();
            ReleaseDropOffReservation();
            dropOff = null;
            State = WorkerState.WaitingForDropOff;
            dropOffPathFailureCount++;
            cargoRecoveryCount++;
            ScheduleDropOffRetry();
            ResetEconomicProgress();
        }

        private void ResetEconomicProgress()
        {
            economicProgressInitialized = true;
            timeSinceLastEconomicProgress = 0f;
            lastEconomicPosition = transform.position;
            lastEconomicCarriedAmount = carriedAmount;
            lastEconomicState = State;
        }

        private void RejectCurrentDropOff()
        {
            if (!ResourceDropOffRegistry.IsUsable(dropOff,
                    controller != null ? controller.Faction : default)) return;
            hasRejectedDropOff = true;
            rejectedDropOffId = dropOff.RuntimeId;
            rejectedDropOffUntil = Time.time + FailedDropOffCooldownSeconds;
        }

        private void ScheduleDropOffRetry()
        {
            consecutiveDropOffFailures = Mathf.Min(consecutiveDropOffFailures + 1, 4);
            dropOffRetry = Mathf.Min(DropOffRetryMaximumSeconds,
                DropOffRetryBaseSeconds * (1 << (consecutiveDropOffFailures - 1)));
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
