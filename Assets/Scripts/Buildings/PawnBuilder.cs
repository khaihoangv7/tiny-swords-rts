using System;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using TinyKingdom.Match;
using TinyKingdom.Audio;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    [RequireComponent(typeof(UnitController), typeof(UnitMovement))]
    public sealed class PawnBuilder : MonoBehaviour
    {
        public const float WorkCycleSeconds = 0.8f;
        public const float RepairImpactDelay = 0.35f;
        public const float InteractionArrivalTolerance = 0.2f;

        private UnitController controller;
        private UnitMovement movement;
        private BuildingSite target;
        private Vector3 interaction;
        private bool repairing;
        private bool working;
        private float visualCycle;
        private bool workImpactApplied;
        private bool constructionProgressedThisCycle;

        public bool HasJob => target != null;
        public bool IsWorking => working;
        public BuildingSite Target => target;
        public Vector3 InteractionPosition => interaction;
        public float DistanceToInteraction => target != null
            ? Vector2.Distance(transform.position, interaction) : float.PositiveInfinity;
        public bool IsAtInteraction => HasJob && !movement.IsMoving
            && DistanceToInteraction <= InteractionArrivalTolerance;
        public bool IsRepairing => repairing;
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
            if (!MatchManager.AllowsGameplay || building == null || !controller.IsAlive
                || !building.CanAssign(this)
                || !building.TryReserveInteraction(this, out Vector3 position)
                || !movement.TryIssueMove(position))
            {
                building?.Release(this);
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
            return true;
        }

        public void CancelJob()
        {
            if (target != null) target.Release(this);
            target = null;
            working = false;
            visualCycle = 0f;
            workImpactApplied = false;
            constructionProgressedThisCycle = false;
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay)
            {
                if (target != null) CancelJob();
                return;
            }
            if (target == null || !controller.IsAlive) { CancelJob(); return; }
            if (!target.IsAlive || (repairing ? !target.NeedsRepair
                : target.State != BuildingState.UnderConstruction))
            {
                CancelJob();
                return;
            }
            if (!IsAtInteraction)
            {
                working = false;
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
            }
            visualCycle += Time.deltaTime;
            if (repairing)
            {
                if (!workImpactApplied && visualCycle >= RepairImpactDelay)
                {
                    workImpactApplied = true;
                    if (target.Repair(WorkCycleSeconds) > 0f)
                    {
                        RepairImpact?.Invoke(target);
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
            if (repairing ? !target.NeedsRepair
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

        private void OnStateChanged(UnitState state)
        {
            if (state == UnitState.Dead) CancelJob();
        }
    }
}
