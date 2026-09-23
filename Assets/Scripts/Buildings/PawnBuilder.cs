using System;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using TinyKingdom.Match;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    [RequireComponent(typeof(UnitController), typeof(UnitMovement))]
    public sealed class PawnBuilder : MonoBehaviour
    {
        private UnitController controller;
        private UnitMovement movement;
        private BuildingSite target;
        private Vector3 interaction;
        private bool repairing;
        private bool working;
        private float visualCycle;

        public bool HasJob => target != null;
        public bool IsWorking => working;
        public BuildingSite Target => target;
        public event Action WorkCycleStarted;

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
            return true;
        }

        public void CancelJob()
        {
            if (target != null) target.Release(this);
            target = null;
            working = false;
            visualCycle = 0f;
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
            if (movement.IsMoving || Vector2.Distance(transform.position, interaction) > 0.1f)
            {
                working = false;
                return;
            }
            if (!working)
            {
                movement.Stop();
                working = true;
                visualCycle = 0f;
                WorkCycleStarted?.Invoke();
            }
            if (repairing) target.Repair(Time.deltaTime);
            else target.AdvanceConstruction(Time.deltaTime);
            visualCycle += Time.deltaTime;
            if (visualCycle >= 0.8f)
            {
                visualCycle -= 0.8f;
                WorkCycleStarted?.Invoke();
            }
        }

        private void OnStateChanged(UnitState state)
        {
            if (state == UnitState.Dead) CancelJob();
        }
    }
}
