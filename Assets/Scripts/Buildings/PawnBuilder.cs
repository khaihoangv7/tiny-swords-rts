using System;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using TinyKingdom.Match;
using TinyKingdom.Audio;
using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    [RequireComponent(typeof(UnitController), typeof(UnitMovement))]
    public sealed class PawnBuilder : MonoBehaviour
    {
        public const float WorkCycleSeconds = 0.8f;
        public const float RepairImpactDelay = 0.35f;
        public const float InteractionArrivalTolerance = 0.2f;
        public const float ExitWorkDistance = 0.48f;
        public const float ApproachStallSeconds = 1.25f;
        public const float RecoveryRetrySeconds = 1.5f;
        public const int SameSlotRecoveryAttempts = 2;

        private UnitController controller;
        private UnitMovement movement;
        private BuildingSite target;
        private CastleDropOff repairCastle;
        private Vector3 interaction;
        private bool repairing;
        private bool working;
        private float visualCycle;
        private bool workImpactApplied;
        private bool constructionProgressedThisCycle;
        private float lastApproachDistance = float.PositiveInfinity;
        private float lastApproachProgressTime;
        private float nextRecoveryTime;
        private int pathFailureCount;

        public bool HasJob => target != null || repairCastle != null;
        public bool IsWorking => working;
        public BuildingSite Target => target;
        public CastleDropOff RepairCastle => repairCastle;
        public Vector3 InteractionPosition => interaction;
        public float DistanceToInteraction => HasJob
            ? Vector2.Distance(transform.position, interaction) : float.PositiveInfinity;
        public bool IsAtInteraction => HasJob
            && DistanceToInteraction <= InteractionArrivalTolerance;
        public bool IsRepairing => repairing;
        public bool ConstrainSeparationNearSlot => HasJob
            && DistanceToInteraction <= ExitWorkDistance;
        public int ReservedSlotId => target != null
            && target.TryGetBuilderReservation(this, out int slotId, out _, out _)
                ? slotId : -1;
        public int PathFailureCount => pathFailureCount;
        public float LastBuildCommandTime { get; private set; } = -1f;
        public string LastRecoveryReason { get; private set; } = "None";
        public event Action WorkCycleStarted;
        public event Action<BuildingSite> RepairImpact;

        private void Awake()
        {
            controller = GetComponent<UnitController>();
            movement = GetComponent<UnitMovement>();
            controller.StateChanged += OnStateChanged;
        }

        private void OnDestroy()
        {
            if (controller != null) controller.StateChanged -= OnStateChanged;
        }

        public bool Assign(BuildingSite building, bool repair)
        {
            if (!MatchManager.AllowsGameplay || building == null || !controller.IsAlive)
                return false;
            if (target == building && repairing == repair
                && building.IsAssigned(this) && building.HasBuilderReservation(this))
            {
                if (building.TryGetBuilderReservation(this, out _, out _,
                    out Vector3 existing)) interaction = existing;
                LastBuildCommandTime = Time.time;
                return true;
            }
            if (!building.CanAssign(this)
                || !building.TryReserveInteraction(this, out Vector3 position)) return false;
            building.RecordBuilderPathRequest();
            if (!movement.TryIssueMove(position))
            {
                building.Release(this);
                return false;
            }
            CancelJob();
            building.Claim(this);
            GetComponent<PawnGatherer>()?.CancelJob();
            target = building;
            interaction = position;
            repairing = repair;
            working = false;
            visualCycle = 0f;
            workImpactApplied = false;
            constructionProgressedThisCycle = false;
            lastApproachDistance = DistanceToInteraction;
            lastApproachProgressTime = Time.time;
            nextRecoveryTime = Time.time + RecoveryRetrySeconds;
            pathFailureCount = 0;
            LastBuildCommandTime = Time.time;
            LastRecoveryReason = "Initial slot approach";
            return true;
        }

        public bool AssignCastleRepair(CastleDropOff castle)
        {
            if (!MatchManager.AllowsGameplay || castle == null || !castle.NeedsRepair
                || !controller.IsAlive || castle.Faction != controller.Faction)
                return false;
            if (repairCastle == castle)
            {
                LastBuildCommandTime = Time.time;
                return true;
            }
            GridManager grid = movement.GridManager;
            PathfindingSystem paths = grid != null
                ? grid.GetComponent<PathfindingSystem>() : null;
            if (grid == null || paths == null
                || !castle.TryReserve(controller, grid, paths, out Vector3 position))
                return false;
            if (!movement.TryIssueMove(position))
            {
                castle.Release(controller);
                return false;
            }
            CancelJob();
            GetComponent<PawnGatherer>()?.CancelJob();
            GetComponent<UnitStationing>()?.CancelPending();
            // A returning gatherer may have held this same Castle slot. Its
            // cancellation releases that reservation, so claim it again for
            // repair before other workers can use it.
            castle.Release(controller);
            if (!castle.TryReserve(controller, grid, paths, out position)
                || !movement.TryIssueMove(position))
            {
                castle.Release(controller);
                movement.Stop();
                return false;
            }
            repairCastle = castle;
            interaction = position;
            repairing = true;
            working = false;
            visualCycle = 0f;
            workImpactApplied = false;
            lastApproachDistance = DistanceToInteraction;
            lastApproachProgressTime = Time.time;
            nextRecoveryTime = Time.time + RecoveryRetrySeconds;
            pathFailureCount = 0;
            LastBuildCommandTime = Time.time;
            LastRecoveryReason = "Castle repair slot approach";
            return true;
        }

        public void CancelJob()
        {
            if (target != null) target.Release(this);
            if (repairCastle != null) repairCastle.Release(controller);
            target = null;
            repairCastle = null;
            working = false;
            visualCycle = 0f;
            workImpactApplied = false;
            constructionProgressedThisCycle = false;
            lastApproachDistance = float.PositiveInfinity;
            lastApproachProgressTime = 0f;
            nextRecoveryTime = 0f;
            pathFailureCount = 0;
            LastRecoveryReason = "None";
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay)
            {
                if (HasJob) CancelJob();
                return;
            }
            if (!HasJob || !controller.IsAlive) { CancelJob(); return; }
            if (target != null
                    ? !target.IsAlive || (repairing ? !target.NeedsRepair
                        : target.State != BuildingState.UnderConstruction)
                    : repairCastle == null || !repairCastle.NeedsRepair)
            {
                CancelJob();
                return;
            }
            float distance = DistanceToInteraction;
            if (working && distance <= ExitWorkDistance)
            {
                if (movement.IsMoving) movement.Stop();
            }
            else if (distance > InteractionArrivalTolerance)
            {
                working = false;
                if (distance + 0.03f < lastApproachDistance)
                {
                    lastApproachDistance = distance;
                    lastApproachProgressTime = Time.time;
                    pathFailureCount = 0;
                }
                else if (Time.time - lastApproachProgressTime >= ApproachStallSeconds)
                    RequestSlotRecovery("Builder approach made no progress");
                return;
            }
            if (!working)
            {
                movement.Stop();
                working = true;
                visualCycle = 0f;
                workImpactApplied = false;
                constructionProgressedThisCycle = false;
                WorkCycleStarted?.Invoke();
                lastApproachDistance = distance;
                lastApproachProgressTime = Time.time;
                pathFailureCount = 0;
            }
            visualCycle += Time.deltaTime;
            if (repairing)
            {
                if (!workImpactApplied && visualCycle >= RepairImpactDelay)
                {
                    workImpactApplied = true;
                    float restored = target != null
                        ? target.Repair(WorkCycleSeconds)
                        : repairCastle.Repair(WorkCycleSeconds);
                    if (restored > 0f)
                    {
                        if (target != null) RepairImpact?.Invoke(target);
                        else repairCastle.GetComponent<BuildingSelectionPulse>()?.Trigger();
                        PlayHammerImpact();
                    }
                }
            }
            else
            {
                float progressBefore = target.ConstructionProgress;
                target.AdvanceConstruction(Time.deltaTime);
                constructionProgressedThisCycle |= target.ConstructionProgress > progressBefore;
                if (!workImpactApplied && constructionProgressedThisCycle
                    && (visualCycle >= RepairImpactDelay
                        || target.State != BuildingState.UnderConstruction))
                {
                    workImpactApplied = true;
                    PlayHammerImpact();
                }
            }
            if (repairing ? (target != null ? !target.NeedsRepair
                    : repairCastle == null || !repairCastle.NeedsRepair)
                : target.State != BuildingState.UnderConstruction)
            {
                CancelJob();
                return;
            }
            if (visualCycle >= WorkCycleSeconds)
            {
                visualCycle -= WorkCycleSeconds;
                workImpactApplied = false;
                constructionProgressedThisCycle = false;
                WorkCycleStarted?.Invoke();
            }
        }

        private void PlayHammerImpact()
            => GameplayAudioService.Request(GameplayAudioEvent.PawnHammer,
                transform.position, controller.Faction);

        public bool RequestSlotRecovery(string reason)
        {
            if (!HasJob || !controller.IsAlive || Time.time < nextRecoveryTime)
                return false;
            nextRecoveryTime = Time.time + RecoveryRetrySeconds;
            lastApproachProgressTime = Time.time;
            if (repairCastle != null)
            {
                if (pathFailureCount++ < SameSlotRecoveryAttempts)
                {
                    movement.Stop();
                    if (movement.TryIssueMove(interaction))
                    {
                        LastRecoveryReason = reason + ": returning to Castle repair slot";
                        return true;
                    }
                }
                CancelJob();
                movement.Stop();
                LastRecoveryReason = reason + ": Castle repair slot unreachable";
                return false;
            }
            if (pathFailureCount < SameSlotRecoveryAttempts)
            {
                target.RecordBuilderPathRequest();
                movement.Stop();
                pathFailureCount++;
                if (movement.TryIssueMove(interaction))
                {
                    LastRecoveryReason = reason + ": returning to reserved slot";
                    return true;
                }
                LastRecoveryReason = reason + ": reserved slot repath failed";
                if (pathFailureCount < SameSlotRecoveryAttempts) return false;
            }

            if (!target.TryReassignInteraction(this, out Vector3 replacement))
            {
                LastRecoveryReason = reason + ": reserved slot retry failed";
                BuildingSite failedSite = target;
                bool siteHasOtherBuilders = failedSite.ActiveBuilderCount > 1;
                CancelJob();
                movement.Stop();
                if (!siteHasOtherBuilders)
                    failedSite.ReportConstructionAccessFailure(
                        "Bounded builder recovery found no reachable slot");
                return false;
            }
            interaction = replacement;
            pathFailureCount = 0;
            target.RecordBuilderPathRequest();
            movement.Stop();
            if (!movement.TryIssueMove(interaction))
            {
                LastRecoveryReason = reason + ": alternate slot path failed";
                return false;
            }
            lastApproachDistance = DistanceToInteraction;
            LastRecoveryReason = reason + ": reassigned to free reachable slot";
            return true;
        }

        private void OnStateChanged(UnitState state)
        {
            if (state == UnitState.Dead) CancelJob();
        }
    }
}
