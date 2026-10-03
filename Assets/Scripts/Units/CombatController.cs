using System;
using TinyKingdom.Audio;
using TinyKingdom.Navigation;
using TinyKingdom.Buildings;
using TinyKingdom.Vision;
using TinyKingdom.Match;
using UnityEngine;

namespace TinyKingdom.Units
{
    public enum CombatDirective
    {
        Normal,
        Retreat
    }

    [RequireComponent(typeof(UnitController), typeof(UnitMovement), typeof(HealthComponent))]
    public sealed class CombatController : MonoBehaviour
    {
        private enum CombatOrder { Idle, Move, ExplicitAttack, AttackMove, Returning }

        [SerializeField] private UnitCombatData combatData;
        [SerializeField] private Sprite projectileSprite = null;
        [SerializeField] private Vector2 projectileLaunchOffset = new(0.4f, 0.1f);
        [SerializeField, Min(0f)] private float projectileArcHeight = 1.2f;
        [SerializeField, Min(0.1f)] private float repathInterval = 0.5f;

        private UnitController controller;
        private UnitMovement movement;
        private HealthComponent health;
        private UnitController target;
        private HealthComponent targetHealth;
        private BuildingCombatTarget buildingTarget;
        private bool hasTargetAssignment;
        private GridManager grid;
        private Vector2Int lastTargetCell;
        private Vector3 lastChasePosition;
        private bool hasChasePosition;
        private float repathRemaining;
        private float slotRetryRemaining;
        private float attackCooldown;
        private float rangedWindupRemaining;
        private bool rangedShotPending;
        private bool committedMeleeAttackPending;
        private bool committedMeleeHitApplied;
        private float committedMeleeHitRemaining;
        private float committedMeleeRecoveryRemaining;
        private CombatOrder order;
        private Vector2Int attackMoveDestination;
        private Vector3 attackMoveCommandPosition;
        private IPathTraversalPolicy attackMoveTraversalPolicy;
        private Vector3 autoEngagementOrigin;
        private Vector3 idleDefenseOrigin;
        private bool hasIdleDefenseOrigin;
        private UnitController suppressedAutoTarget;
        private bool autoTarget;
        private bool retaliationTarget;
        private Vector3 retaliationOrigin;
        private float acquisitionRemaining;
        private bool stationedInternal;
        private bool mounted;
        private bool stoppedForMatchEnd;
        private float mountedDamageMultiplier = 1f;
        private float mountedRangeMultiplier = 1f;
        private CombatDirective directive;

        public UnitController Target => target;
        public BuildingCombatTarget BuildingTarget => buildingTarget;
        public bool HasAttackMoveOrder => order == CombatOrder.AttackMove;
        public UnitCombatData CombatData => combatData;
        public CombatProjectile LastProjectile { get; private set; }
        public event Action<Vector2> AttackPerformed;
        public Sprite ProjectileSprite => projectileSprite;
        public float ProjectileArcHeight => projectileArcHeight;
        public bool IsMounted => mounted;
        public float EffectiveAttackRange => combatData == null ? 0f
            : combatData.AttackRange * (mounted ? mountedRangeMultiplier : 1f);
        public float EffectiveDamageMultiplier => mounted ? mountedDamageMultiplier : 1f;
        public bool IsRetaliating => retaliationTarget;
        public CombatDirective Directive => directive;
        public bool HasAttackPresentationListener => AttackPerformed != null;
        public bool IsMeleeAttackCommitted => committedMeleeAttackPending;
        public bool HasAppliedCommittedMeleeHit => committedMeleeAttackPending
            && committedMeleeHitApplied;

        private void Awake()
        {
            ResolveReferences();
            controller.StateChanged += HandleOwnState;
        }

        private void OnDestroy()
        {
            if (controller != null)
                controller.StateChanged -= HandleOwnState;
            ClearTarget();
            ClearBuildingTarget();
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay)
            {
                if (!stoppedForMatchEnd)
                {
                    stoppedForMatchEnd = true;
                    StopCombat();
                }
                return;
            }
            stoppedForMatchEnd = false;
            TickCombat(Time.deltaTime);
        }

        public void Configure(UnitCombatData data)
        {
            combatData = data;
        }

        public bool Attack(UnitController enemy)
        {
            if (!MatchManager.AllowsGameplay) return false;
            ResolveReferences();
            directive = CombatDirective.Normal;
            if (stationedInternal || mounted) return false;
            GetComponent<UnitStationing>()?.CancelPending();
            if (!CanAcquireThroughTeamVision(enemy))
                return false;

            if (target == enemy)
            {
                order = CombatOrder.ExplicitAttack;
                autoTarget = false;
                retaliationTarget = false;
                suppressedAutoTarget = null;
                return true;
            }

            GetComponent<UnitGuard>()?.EndGuard();
            StopCombat();
            order = CombatOrder.ExplicitAttack;
            StartTarget(enemy, false);
            // Cooldown deliberately survives a new order: repeated clicks cannot accelerate attacks.
            TickCombat(0f);
            return target != null || !enemy.IsAlive;
        }

        public bool Attack(BuildingCombatTarget enemy)
        {
            if (!MatchManager.AllowsGameplay) return false;
            ResolveReferences();
            directive = CombatDirective.Normal;
            if (stationedInternal || mounted || enemy == null || !enemy.IsTargetable
                || enemy.Faction == controller.Faction
                || !CombatVisibility.CanFactionCurrentlyObserveTarget(
                    controller.Faction, enemy)) return false;
            GetComponent<UnitStationing>()?.CancelPending();
            GetComponent<UnitGuard>()?.EndGuard();
            StopCombat();
            order = CombatOrder.ExplicitAttack;
            buildingTarget = enemy;
            buildingTarget.CombatDied += HandleBuildingTargetDeath;
            repathRemaining = 0f;
            TickCombat(0f);
            return buildingTarget != null || !enemy.IsAlive;
        }

        public bool AttackMove(Vector2Int destination)
            => AttackMove(destination, null, null);

        public bool AttackMove(Vector2Int destination, Vector3 commandPosition)
            => AttackMove(destination, commandPosition, null);

        public bool AttackMove(Vector2Int destination, Vector3 commandPosition,
            IPathTraversalPolicy traversalPolicy)
            => AttackMove(destination, (Vector3?)commandPosition, traversalPolicy);

        private bool AttackMove(Vector2Int destination, Vector3? commandPosition,
            IPathTraversalPolicy traversalPolicy)
        {
            if (!MatchManager.AllowsGameplay) return false;
            ResolveReferences();
            directive = CombatDirective.Normal;
            if (stationedInternal || mounted || !controller.IsAlive) return false;
            GetComponent<UnitStationing>()?.CancelPending();
            bool issued = commandPosition.HasValue && traversalPolicy != null
                ? movement.TryIssueAttackMove(destination, commandPosition.Value,
                    traversalPolicy)
                : commandPosition.HasValue
                    ? movement.TryIssueAttackMove(destination, commandPosition.Value)
                : movement.TryIssueAttackMove(destination);
            if (!issued) return false;
            ClearTarget();
            ClearBuildingTarget();
            order = movement.IsMoving ? CombatOrder.AttackMove : CombatOrder.Idle;
            hasIdleDefenseOrigin = false;
            attackMoveDestination = destination;
            attackMoveCommandPosition = commandPosition
                ?? (movement.GridManager != null
                    ? movement.GridManager.GridToWorld(destination.x, destination.y)
                    : transform.position);
            attackMoveTraversalPolicy = traversalPolicy;
            suppressedAutoTarget = null;
            acquisitionRemaining = 0f;
            return true;
        }

        // Called only after a normal Move path has been validated.
        public void OnMoveIssued()
        {
            directive = CombatDirective.Normal;
            ClearTarget();
            ClearBuildingTarget();
            order = CombatOrder.Move;
            hasIdleDefenseOrigin = false;
            suppressedAutoTarget = null;
            attackMoveTraversalPolicy = null;
            // UnitMovement replaces or coalesces the path immediately after this
            // callback. Clearing it here made equivalent rapid commands tear down
            // an otherwise valid route.
        }

        public void StopCombat()
        {
            directive = CombatDirective.Normal;
            ClearTarget();
            ClearBuildingTarget();
            order = CombatOrder.Idle;
            hasIdleDefenseOrigin = false;
            suppressedAutoTarget = null;
            attackMoveTraversalPolicy = null;
            movement ??= GetComponent<UnitMovement>();
            movement.Stop();
        }

        public bool TryRetaliate(UnitController attacker)
        {
            if (!MatchManager.AllowsGameplay) return false;
            ResolveReferences();
            if (directive == CombatDirective.Retreat
                || combatData == null || !combatData.CanRetaliateWhenDamaged
                || !controller.IsAlive || stationedInternal || mounted
                || controller.CurrentState == UnitState.Guarding
                || order is CombatOrder.ExplicitAttack or CombatOrder.Move
                || buildingTarget != null || !IsValidTarget(attacker))
                return false;

            // Keep any current valid combat target to prevent target thrashing.
            if (target != null && IsValidTarget(target)
                && CanContinueCurrentTarget(target)) return false;
            ClearTarget();
            if (order == CombatOrder.Returning) order = CombatOrder.Idle;
            retaliationOrigin = transform.position;
            StartTarget(attacker, false);
            retaliationTarget = target != null;
            TickCombat(0f);
            return retaliationTarget;
        }

        public void TickCombat(float deltaTime)
        {
            if (!MatchManager.AllowsGameplay) return;
            ResolveReferences();
            float elapsedTime = Mathf.Max(0f, deltaTime);
            attackCooldown = Mathf.Max(0f, attackCooldown - elapsedTime);
            acquisitionRemaining -= elapsedTime;
            if (!controller.IsAlive || stationedInternal
                || controller.CurrentState == UnitState.Guarding)
                return;
            if (committedMeleeAttackPending)
            {
                TickCommittedMeleeAttack(elapsedTime);
                if (committedMeleeAttackPending || target == null) return;
            }
            if (mounted)
            {
                TickMountedCombat(deltaTime);
                return;
            }
            if (buildingTarget != null)
            {
                TickBuildingCombat(deltaTime);
                return;
            }
            if (hasTargetAssignment && target == null)
            {
                FinishTarget(false);
                return;
            }
            bool autoLeashBroken = autoTarget && IsOutsideAutoChaseLimit();
            bool retaliationLeashBroken = retaliationTarget
                && IsOutsideRetaliationLeash();
            if (target != null && (!IsValidTarget(target)
                || autoLeashBroken || retaliationLeashBroken))
            {
                FinishTarget(autoLeashBroken || retaliationLeashBroken);
                return;
            }
            if (target != null && !CanContinueCurrentTarget(target))
            {
                LoseTargetToFog();
                return;
            }

            if (target == null)
            {
                if (order == CombatOrder.Returning && !movement.IsMoving)
                    hasIdleDefenseOrigin = false;
                if ((order == CombatOrder.Move || order == CombatOrder.Returning
                    || order == CombatOrder.AttackMove) && !movement.IsMoving)
                    order = CombatOrder.Idle;
                if (directive != CombatDirective.Retreat
                    && combatData != null && combatData.AutoAcquireTargets
                    && (order == CombatOrder.Idle || order == CombatOrder.AttackMove)
                    && acquisitionRemaining <= 0f)
                {
                    acquisitionRemaining = combatData != null
                        ? Mathf.Max(0.05f, combatData.AcquisitionInterval) : 0.25f;
                    UnitController nearest = FindNearestAutoTarget();
                    if (nearest != null) StartTarget(nearest, true);
                }
                if (target == null) return;
            }

            grid ??= movement.GridManager != null ? movement.GridManager
                : FindAnyObjectByType<GridManager>();
            if (grid == null)
            {
                FinishTarget(false);
                return;
            }

            Vector2Int ownCell = grid.WorldToGrid(transform.position);
            Vector2Int targetCell = grid.WorldToGrid(target.transform.position);
            GridCell own = grid.GetCell(ownCell.x, ownCell.y);
            GridCell enemy = grid.GetCell(targetCell.x, targetCell.y);
            if (own == null || enemy == null || !own.Walkable || !enemy.Walkable)
            {
                FinishTarget(false);
                return;
            }

            if (autoTarget && acquisitionRemaining <= 0f)
            {
                acquisitionRemaining = Mathf.Max(0.05f, combatData.AcquisitionInterval);
                bool canAttackHere = own.HeightLevel == enemy.HeightLevel
                    && Vector2.Distance(transform.position, target.transform.position)
                        <= combatData.AttackRange;
                PathfindingSystem route = grid.GetComponent<PathfindingSystem>();
                if (!canAttackHere && (route == null
                    || route.FindPath(ownCell, targetCell).Count == 0))
                {
                    FinishTarget(false);
                    return;
                }
            }

            if (combatData.AttackDelivery == AttackDelivery.Projectile)
            {
                TickRangedCombat(deltaTime, own, enemy, targetCell);
                return;
            }

            float range = combatData.AttackRange;
            repathRemaining -= Mathf.Max(0f, deltaTime);
            slotRetryRemaining -= Mathf.Max(0f, deltaTime);
            bool hasSlot = false;
            Vector3 slotPosition = default;
            PathfindingSystem pathfinding = grid.GetComponent<PathfindingSystem>();
            hasSlot = MeleeEngagementSlots.TryGet(controller, target, range,
                grid, pathfinding, out slotPosition);
            if (!hasSlot)
            {
                MeleeEngagementSlots.Release(controller);
                if (slotRetryRemaining <= 0f)
                {
                    hasSlot = MeleeEngagementSlots.TryReserve(controller, target, range,
                        grid, pathfinding, out slotPosition);
                    if (!hasSlot) slotRetryRemaining = repathInterval;
                }
            }
            Vector3 chasePosition = hasSlot ? slotPosition : target.transform.position;
            bool slotShifted = hasSlot && hasChasePosition
                && Vector2.Distance(lastChasePosition, chasePosition)
                    >= grid.NavigationData.CellSize * 0.25f;
            bool needsChase = repathRemaining <= 0f && (!movement.IsMoving
                || targetCell != lastTargetCell || slotShifted || !hasChasePosition);
            bool inRange = own.HeightLevel == enemy.HeightLevel
                && Vector2.Distance(transform.position, target.transform.position) <= range;
            bool approachingSlot = hasSlot && MeleeEngagementSlots.ReservationCount(target) > 1
                && Vector2.Distance(transform.position, slotPosition)
                    > movement.SeparationRadius * 0.5f;
            if (needsChase && (!inRange || approachingSlot))
            {
                bool issued = hasSlot ? movement.TryIssueChase(slotPosition)
                    : movement.TryIssueChase(targetCell);
                if (!issued)
                {
                    if (hasSlot)
                    {
                        MeleeEngagementSlots.Release(controller);
                        hasSlot = false;
                        approachingSlot = false;
                        issued = movement.TryIssueChase(targetCell);
                    }
                    if (!issued && !inRange)
                    {
                        FinishTarget(false);
                        return;
                    }
                }
                lastTargetCell = targetCell;
                lastChasePosition = chasePosition;
                hasChasePosition = true;
                repathRemaining = repathInterval;
            }
            if (inRange)
            {
                if (!approachingSlot && movement.IsMoving)
                    movement.Stop();
                controller.SetState(approachingSlot && movement.IsMoving
                    ? UnitState.Chasing : UnitState.Attacking);
                if (attackCooldown <= 0f)
                {
                    attackCooldown = combatData.AttackInterval;
                    Vector2 direction = target.transform.position - transform.position;
                    if (combatData.CommitsMeleeAttack)
                        BeginCommittedMeleeAttack(direction);
                    else
                    {
                        AttackPerformed?.Invoke(direction);
                        bool damaged = targetHealth.ReceiveAttack(combatData.BaseDamage,
                            combatData.UnitType, combatData.AttackDelivery, 1f, true,
                            controller);
                        if (damaged) PlayMeleeAttackSound();
                    }
                }
                return;
            }
            controller.SetState(UnitState.Chasing);
        }

        private void BeginCommittedMeleeAttack(Vector2 direction)
        {
            committedMeleeAttackPending = true;
            committedMeleeHitApplied = false;
            committedMeleeHitRemaining = combatData.CommittedMeleeHitDelay;
            committedMeleeRecoveryRemaining = combatData.CommittedMeleeDuration;
            if (movement.IsMoving) movement.Stop();
            controller.SetState(UnitState.Attacking);
            AttackPerformed?.Invoke(direction);
            TickCommittedMeleeAttack(0f);
        }

        private void TickCommittedMeleeAttack(float elapsedTime)
        {
            if (!committedMeleeAttackPending) return;
            if (!controller.IsAlive || !health.IsAlive || target == null
                || targetHealth == null || !targetHealth.IsTargetable
                || !FactionRelations.AreHostile(controller.Faction, target.Faction))
            {
                CancelCommittedMeleeAttack();
                return;
            }

            if (movement.IsMoving) movement.Stop();
            controller.SetState(UnitState.Attacking);
            committedMeleeHitRemaining -= elapsedTime;
            committedMeleeRecoveryRemaining -= elapsedTime;
            if (!committedMeleeHitApplied && committedMeleeHitRemaining <= 0f)
            {
                // Mark first because a lethal hit synchronously clears the target.
                committedMeleeHitApplied = true;
                bool damaged = targetHealth.ReceiveAttack(combatData.BaseDamage,
                    combatData.UnitType, combatData.AttackDelivery, 1f, true,
                    controller);
                if (damaged) PlayMeleeAttackSound();
            }
            if (!committedMeleeAttackPending || committedMeleeRecoveryRemaining > 0f)
                return;

            committedMeleeAttackPending = false;
            committedMeleeHitRemaining = 0f;
            committedMeleeRecoveryRemaining = 0f;
        }

        private void CancelCommittedMeleeAttack()
        {
            committedMeleeAttackPending = false;
            committedMeleeHitApplied = false;
            committedMeleeHitRemaining = 0f;
            committedMeleeRecoveryRemaining = 0f;
        }

        private void TickRangedCombat(float deltaTime, GridCell own, GridCell enemy,
            Vector2Int targetCell)
        {
            if (rangedShotPending)
            {
                rangedWindupRemaining -= Mathf.Max(0f, deltaTime);
                if (rangedWindupRemaining > 0f)
                    return;

                rangedShotPending = false;
                rangedWindupRemaining = 0f;
                // A moving live target stays locked during windup only while the
                // attacker's faction can still observe it (or direct retaliation applies).
                if (!CanContinueCurrentTarget(target))
                {
                    LoseTargetToFog();
                    return;
                }
                LaunchProjectile();
            }

            if (own.HeightLevel == enemy.HeightLevel
                && Vector2.Distance(transform.position, target.transform.position) <= combatData.AttackRange)
            {
                if (movement.IsMoving)
                    movement.Stop();
                controller.SetState(UnitState.Attacking);
                if (attackCooldown <= 0f)
                {
                    attackCooldown = combatData.AttackInterval;
                    rangedShotPending = true;
                    rangedWindupRemaining = combatData.RangedReleaseDelay;
                    Vector2 direction = target.transform.position - transform.position;
                    AttackPerformed?.Invoke(direction);
                    if (rangedWindupRemaining <= 0f)
                    {
                        rangedShotPending = false;
                        LaunchProjectile();
                    }
                }
                return;
            }

            repathRemaining -= Mathf.Max(0f, deltaTime);
            if (!movement.IsMoving || (targetCell != lastTargetCell && repathRemaining <= 0f))
            {
                if (!movement.TryIssueChase(targetCell))
                {
                    FinishTarget(false);
                    return;
                }
                lastTargetCell = targetCell;
                repathRemaining = repathInterval;
            }
            controller.SetState(UnitState.Chasing);
        }

        private void TickMountedCombat(float deltaTime)
        {
            if (combatData == null || combatData.AttackDelivery != AttackDelivery.Projectile)
                return;
            float range = combatData.AttackRange * mountedRangeMultiplier;
            if (target != null && (!CanAcquireThroughTeamVision(target)
                || Vector2.Distance(transform.position, target.transform.position) > range))
            {
                ClearTarget();
                controller.SetState(UnitState.Idle);
            }
            if (buildingTarget != null)
                ClearBuildingTarget();
            if (target == null)
            {
                if (acquisitionRemaining > 0f) return;
                acquisitionRemaining = Mathf.Max(0.05f, combatData.AcquisitionInterval);
                UnitController nearest = null;
                float best = float.PositiveInfinity;
                foreach (UnitController candidate in UnitController.ActiveUnits)
                {
                    if (!CanAcquireThroughTeamVision(candidate)) continue;
                    float distance = ((Vector2)(candidate.transform.position - transform.position)).sqrMagnitude;
                    if (distance > range * range) continue;
                    if (distance < best - 0.0001f
                        || (Mathf.Abs(distance - best) <= 0.0001f
                            && IsEarlierPosition(candidate, nearest)))
                    {
                        nearest = candidate;
                        best = distance;
                    }
                }
                if (nearest == null) { controller.SetState(UnitState.Idle); return; }
                StartTarget(nearest, true);
            }
            if (rangedShotPending)
            {
                rangedWindupRemaining -= Mathf.Max(0f, deltaTime);
                if (rangedWindupRemaining > 0f) return;
                rangedShotPending = false;
                rangedWindupRemaining = 0f;
                if (!CanAcquireThroughTeamVision(target)
                    || Vector2.Distance(transform.position, target.transform.position) > range)
                {
                    ClearTarget();
                    return;
                }
                LaunchProjectile(mountedDamageMultiplier);
            }
            controller.SetState(UnitState.Attacking);
            if (attackCooldown > 0f) return;
            attackCooldown = combatData.AttackInterval;
            rangedShotPending = true;
            rangedWindupRemaining = combatData.RangedReleaseDelay;
            AttackPerformed?.Invoke(target.transform.position - transform.position);
            if (rangedWindupRemaining <= 0f)
            {
                rangedShotPending = false;
                LaunchProjectile(mountedDamageMultiplier);
            }
        }

        private void TickBuildingCombat(float deltaTime)
        {
            if (buildingTarget == null || !buildingTarget.IsTargetable
                || !CombatVisibility.CanFactionCurrentlyObserveTarget(
                    controller.Faction, buildingTarget))
            {
                ClearBuildingTarget();
                movement.Stop();
                order = CombatOrder.Idle;
                return;
            }
            float range = combatData.AttackRange;
            bool inRange = buildingTarget.DistanceFrom(transform.position) <= range;
            if (combatData.AttackDelivery == AttackDelivery.Projectile)
            {
                if (rangedShotPending)
                {
                    rangedWindupRemaining -= Mathf.Max(0f, deltaTime);
                    if (rangedWindupRemaining > 0f) return;
                    rangedShotPending = false;
                    if (buildingTarget == null || !buildingTarget.IsTargetable
                        || !CombatVisibility.CanFactionCurrentlyObserveTarget(
                            controller.Faction, buildingTarget))
                    {
                        ClearBuildingTarget();
                        return;
                    }
                    LaunchBuildingProjectile();
                }
                if (inRange)
                {
                    // Repeated Stop calls force Attacking -> Idle -> Attacking every
                    // frame and cancel the presentation clip that just started.
                    if (movement.IsMoving) movement.Stop();
                    controller.SetState(UnitState.Attacking);
                    if (attackCooldown <= 0f)
                    {
                        attackCooldown = combatData.AttackInterval;
                        rangedShotPending = true;
                        rangedWindupRemaining = combatData.RangedReleaseDelay;
                        AttackPerformed?.Invoke(buildingTarget.AttackPresentationPoint(
                            transform.position) - transform.position);
                    }
                    return;
                }
            }
            else if (inRange)
            {
                if (movement.IsMoving) movement.Stop();
                controller.SetState(UnitState.Attacking);
                if (attackCooldown <= 0f)
                {
                    attackCooldown = combatData.AttackInterval;
                    Vector3 attackDirection = buildingTarget.AttackPresentationPoint(
                        transform.position) - transform.position;
                    AttackPerformed?.Invoke(attackDirection);
                    bool damaged = buildingTarget.ReceiveAttack(combatData.BaseDamage,
                        combatData.UnitType, combatData.AttackDelivery);
                    if (damaged) PlayMeleeAttackSound();
                }
                return;
            }
            repathRemaining -= Mathf.Max(0f, deltaTime);
            if (repathRemaining <= 0f && (!movement.IsMoving
                || buildingTarget.DistanceFrom(transform.position) > range))
            {
                grid ??= movement.GridManager != null ? movement.GridManager
                    : FindAnyObjectByType<GridManager>();
                if (grid == null || !buildingTarget.TryFindAttackPosition(controller,
                    range, grid, out Vector3 position) || !movement.TryIssueChase(position))
                {
                    ClearBuildingTarget();
                    order = CombatOrder.Idle;
                    return;
                }
                repathRemaining = repathInterval;
            }
            controller.SetState(UnitState.Chasing);
        }

        private void StartTarget(UnitController enemy, bool automatic)
        {
            movement.Stop();
            retaliationTarget = false;
            target = enemy;
            targetHealth = enemy.GetComponent<HealthComponent>();
            hasTargetAssignment = true;
            targetHealth.Died += HandleTargetDeath;
            autoTarget = automatic;
            if (automatic)
            {
                if (order == CombatOrder.Idle)
                {
                    if (!hasIdleDefenseOrigin)
                    {
                        idleDefenseOrigin = transform.position;
                        hasIdleDefenseOrigin = true;
                    }
                    autoEngagementOrigin = idleDefenseOrigin;
                }
                else autoEngagementOrigin = transform.position;
            }
            repathRemaining = 0f;
            slotRetryRemaining = 0f;
            hasChasePosition = false;
            if (combatData.AttackDelivery != AttackDelivery.Melee) return;
            grid ??= movement.GridManager != null ? movement.GridManager
                : FindAnyObjectByType<GridManager>();
            if (!MeleeEngagementSlots.TryReserve(controller, target, combatData.AttackRange,
                grid, grid != null ? grid.GetComponent<PathfindingSystem>() : null, out _))
                slotRetryRemaining = repathInterval;
        }

        private UnitController FindNearestAutoTarget()
        {
            if (combatData == null) return null;
            grid ??= movement.GridManager != null ? movement.GridManager
                : FindAnyObjectByType<GridManager>();
            if (grid == null) return null;
            Vector2Int ownPosition = grid.WorldToGrid(transform.position);
            GridCell ownCell = grid.GetCell(ownPosition.x, ownPosition.y);
            PathfindingSystem pathfinding = grid.GetComponent<PathfindingSystem>();
            if (ownCell == null || !ownCell.Walkable || pathfinding == null) return null;

            float radiusSquared = combatData.AcquisitionRadius * combatData.AcquisitionRadius;
            float rangeSquared = combatData.AttackRange * combatData.AttackRange;
            UnitController nearest = null;
            float bestDistance = float.PositiveInfinity;
            foreach (UnitController candidate in UnitController.ActiveUnits)
            {
                if (!CanAcquireThroughTeamVision(candidate)) continue;
                float distance = ((Vector2)(candidate.transform.position - transform.position)).sqrMagnitude;
                if (candidate == suppressedAutoTarget)
                {
                    if (distance > radiusSquared) suppressedAutoTarget = null;
                    else continue;
                }
                if (distance > radiusSquared) continue;
                if (order == CombatOrder.Idle && hasIdleDefenseOrigin
                    && ((Vector2)(candidate.transform.position - idleDefenseOrigin)).sqrMagnitude
                        > combatData.AutoChaseLimit * combatData.AutoChaseLimit)
                    continue;
                Vector2Int position = grid.WorldToGrid(candidate.transform.position);
                GridCell enemyCell = grid.GetCell(position.x, position.y);
                if (enemyCell == null || !enemyCell.Walkable || enemyCell.Occupied) continue;
                if ((distance > rangeSquared || enemyCell.HeightLevel != ownCell.HeightLevel)
                    && pathfinding.FindPath(ownPosition, position).Count == 0) continue;
                if (distance < bestDistance - 0.0001f
                    || (Mathf.Abs(distance - bestDistance) <= 0.0001f
                        && IsEarlierPosition(candidate, nearest)))
                {
                    nearest = candidate;
                    bestDistance = distance;
                }
            }
            return nearest;
        }

        private static bool IsEarlierPosition(UnitController candidate, UnitController current)
        {
            if (current == null) return true;
            Vector3 a = candidate.transform.position;
            Vector3 b = current.transform.position;
            if (!Mathf.Approximately(a.x, b.x)) return a.x < b.x;
            // Identical positions keep the registration order of UnitController.ActiveUnits.
            return a.y < b.y;
        }

        private bool IsOutsideAutoChaseLimit()
        {
            if (!autoTarget || target == null || combatData == null) return false;
            float limitSquared = combatData.AutoChaseLimit * combatData.AutoChaseLimit;
            return ((Vector2)(target.transform.position - autoEngagementOrigin)).sqrMagnitude
                    > limitSquared
                || ((Vector2)(transform.position - autoEngagementOrigin)).sqrMagnitude
                    > limitSquared;
        }

        private bool IsOutsideRetaliationLeash()
        {
            if (!retaliationTarget || target == null || combatData == null) return false;
            float limitSquared = combatData.RetaliationLeash * combatData.RetaliationLeash;
            return ((Vector2)(target.transform.position - retaliationOrigin)).sqrMagnitude
                    > limitSquared
                || ((Vector2)(transform.position - retaliationOrigin)).sqrMagnitude
                    > limitSquared;
        }

        private void FinishTarget(bool leashBroken)
        {
            if (leashBroken && autoTarget) suppressedAutoTarget = target;
            bool wasAutomatic = autoTarget;
            bool wasRetaliation = retaliationTarget;
            ClearTarget();
            movement.Stop();
            if (order == CombatOrder.AttackMove)
            {
                if (TryAcquireImmediately()) return;
                ResumeAttackMove();
                return;
            }
            order = CombatOrder.Idle;
            if (wasRetaliation) return;
            if (!leashBroken && TryAcquireImmediately()) return;
            if (wasAutomatic && hasIdleDefenseOrigin)
            {
                order = CombatOrder.Returning;
                if (!movement.TryIssueChase(autoEngagementOrigin) || !movement.IsMoving)
                {
                    order = CombatOrder.Idle;
                    hasIdleDefenseOrigin = false;
                }
                return;
            }
            if (leashBroken) TryAcquireImmediately();
        }

        private void LoseTargetToFog()
        {
            bool wasAutomatic = autoTarget;
            ClearTarget();
            movement.Stop();
            if (order == CombatOrder.AttackMove)
            {
                ResumeAttackMove();
                return;
            }
            order = CombatOrder.Idle;
            if (!wasAutomatic || !hasIdleDefenseOrigin) return;
            order = CombatOrder.Returning;
            if (!movement.TryIssueChase(autoEngagementOrigin) || !movement.IsMoving)
            {
                order = CombatOrder.Idle;
                hasIdleDefenseOrigin = false;
            }
        }

        private bool TryAcquireImmediately()
        {
            if (directive == CombatDirective.Retreat
                || combatData == null || !combatData.AutoAcquireTargets) return false;
            acquisitionRemaining = combatData != null
                ? Mathf.Max(0.05f, combatData.AcquisitionInterval) : 0.25f;
            UnitController nearest = FindNearestAutoTarget();
            if (nearest == null) return false;
            StartTarget(nearest, true);
            return true;
        }

        private void ResumeAttackMove()
        {
            if (directive == CombatDirective.Retreat)
            {
                order = CombatOrder.Move;
                return;
            }
            bool issued = attackMoveTraversalPolicy != null
                ? movement.TryIssueAttackMove(attackMoveDestination,
                    attackMoveCommandPosition, attackMoveTraversalPolicy)
                : movement.TryIssueAttackMove(attackMoveDestination);
            if (!issued || !movement.IsMoving)
                order = CombatOrder.Idle;
        }

        private bool IsValidTarget(UnitController enemy)
        {
            if (combatData == null || !controller.IsAlive || !health.IsAlive || enemy == null
                || enemy == controller || !FactionRelations.AreHostile(controller.Faction, enemy.Faction)
                || !enemy.IsTargetable
                || !enemy.gameObject.activeInHierarchy)
                return false;
            HealthComponent enemyHealth = enemy.GetComponent<HealthComponent>();
            return enemyHealth != null && enemyHealth.isActiveAndEnabled
                && enemyHealth.CombatData != null && enemyHealth.IsTargetable;
        }

        public bool CanAcquireThroughTeamVision(UnitController enemy)
        {
            ResolveReferences();
            if (!IsValidTarget(enemy)) return false;
            HealthComponent receiver = enemy.GetComponent<HealthComponent>();
            return CombatVisibility.CanFactionCurrentlyObserveTarget(controller.Faction,
                receiver);
        }

        private bool CanContinueCurrentTarget(UnitController enemy)
        {
            if (!IsValidTarget(enemy)) return false;
            HealthComponent receiver = enemy.GetComponent<HealthComponent>();
            return CombatVisibility.CanContinueTarget(controller.Faction, receiver,
                retaliationTarget ? CombatAwarenessReason.Retaliation
                    : CombatAwarenessReason.TeamVision);
        }

        private void LaunchProjectile(float sourceMultiplier = 1f)
        {
            var arrow = new GameObject("CombatProjectile");
            float facing = target.transform.position.x < transform.position.x ? -1f : 1f;
            arrow.transform.position = transform.position + new Vector3(
                projectileLaunchOffset.x * facing, projectileLaunchOffset.y, -0.1f);
            SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
            renderer.sprite = projectileSprite;
            renderer.sortingOrder = 15;
            CombatProjectile projectile = arrow.AddComponent<CombatProjectile>();
            projectile.Initialize(controller.Faction,
                target.GetComponent<HealthComponent>(), combatData.BaseDamage,
                combatData.UnitType, combatData.ProjectileSpeed, projectileArcHeight,
                sourceMultiplier, true, controller);
            LastProjectile = projectile;
            PlayArcherAttackSound();
        }

        private void LaunchBuildingProjectile()
        {
            var arrow = new GameObject("CombatProjectile");
            float facing = buildingTarget.TargetTransform.position.x < transform.position.x ? -1f : 1f;
            arrow.transform.position = transform.position + new Vector3(
                projectileLaunchOffset.x * facing, projectileLaunchOffset.y, -0.1f);
            SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
            renderer.sprite = projectileSprite;
            renderer.sortingOrder = 15;
            CombatProjectile projectile = arrow.AddComponent<CombatProjectile>();
            projectile.Initialize(controller.Faction, buildingTarget, combatData.BaseDamage,
                combatData.UnitType, combatData.ProjectileSpeed, projectileArcHeight, 1f, true,
                controller);
            LastProjectile = projectile;
            PlayArcherAttackSound();
        }

        private void PlayMeleeAttackSound()
        {
            GameplayAudioEvent? eventId = combatData.UnitType switch
            {
                UnitType.Warrior => GameplayAudioEvent.WarriorAttack,
                UnitType.Lancer => GameplayAudioEvent.LancerAttack,
                _ => null
            };
            if (eventId.HasValue)
                GameplayAudioService.Request(eventId.Value, transform.position,
                    controller.Faction);
        }

        private void PlayArcherAttackSound()
        {
            if (combatData.UnitType == UnitType.Archer)
                GameplayAudioService.Request(GameplayAudioEvent.ArcherAttack,
                    transform.position, controller.Faction);
        }

        public void SetStationedInternal(bool value)
        {
            stationedInternal = value;
            if (value) StopCombat();
        }

        public void SetMountedContext(bool value, float damageMultiplier, float rangeMultiplier)
        {
            mounted = value;
            mountedDamageMultiplier = value ? Mathf.Max(1f, damageMultiplier) : 1f;
            mountedRangeMultiplier = value ? Mathf.Max(1f, rangeMultiplier) : 1f;
            StopCombat();
            acquisitionRemaining = 0f;
        }

        private void ResolveReferences()
        {
            controller ??= GetComponent<UnitController>();
            movement ??= GetComponent<UnitMovement>();
            health ??= GetComponent<HealthComponent>();
        }

        public void BeginRetreat()
        {
            ResolveReferences();
            ClearTarget();
            ClearBuildingTarget();
            directive = CombatDirective.Retreat;
            order = CombatOrder.Move;
            hasIdleDefenseOrigin = false;
            suppressedAutoTarget = null;
            attackMoveTraversalPolicy = null;
            movement.Stop();
        }

        public void ConfirmRetreatMovement()
        {
            directive = CombatDirective.Retreat;
            order = CombatOrder.Move;
            hasIdleDefenseOrigin = false;
            suppressedAutoTarget = null;
            attackMoveTraversalPolicy = null;
        }

        public void EndRetreat()
        {
            if (directive != CombatDirective.Retreat) return;
            directive = CombatDirective.Normal;
            acquisitionRemaining = 0f;
        }

        private void HandleTargetDeath(HealthComponent deadTarget)
        {
            MeleeEngagementSlots.ReleaseTarget(target);
            if (mounted)
            {
                ClearTarget();
                acquisitionRemaining = 0f;
                controller.SetState(UnitState.Idle);
                return;
            }
            FinishTarget(false);
        }

        private void HandleBuildingTargetDeath(ICombatDamageReceiver deadTarget)
        {
            ClearBuildingTarget();
            movement.Stop();
            order = CombatOrder.Idle;
        }

        private void HandleOwnState(UnitState state)
        {
            if (state == UnitState.Dead)
            {
                ClearTarget();
                order = CombatOrder.Idle;
            }
        }

        private void ClearTarget()
        {
            CancelCommittedMeleeAttack();
            rangedShotPending = false;
            rangedWindupRemaining = 0f;
            MeleeEngagementSlots.Release(controller);
            if (targetHealth != null)
                targetHealth.Died -= HandleTargetDeath;
            target = null;
            targetHealth = null;
            hasTargetAssignment = false;
            autoTarget = false;
            retaliationTarget = false;
            hasChasePosition = false;
        }

        private void ClearBuildingTarget()
        {
            rangedShotPending = false;
            rangedWindupRemaining = 0f;
            if (buildingTarget != null)
                buildingTarget.CombatDied -= HandleBuildingTargetDeath;
            buildingTarget = null;
        }
    }
}
