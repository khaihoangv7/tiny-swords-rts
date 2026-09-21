using System;
using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController), typeof(UnitMovement), typeof(HealthComponent))]
    public sealed class CombatController : MonoBehaviour
    {
        [SerializeField] private UnitCombatData combatData;
        [SerializeField] private Sprite projectileSprite = null;
        [SerializeField, Min(0.1f)] private float repathInterval = 0.5f;

        private UnitController controller;
        private UnitMovement movement;
        private HealthComponent health;
        private UnitController target;
        private HealthComponent targetHealth;
        private GridManager grid;
        private Vector2Int lastTargetCell;
        private float repathRemaining;
        private float attackCooldown;

        public UnitController Target => target;
        public UnitCombatData CombatData => combatData;
        public CombatProjectile LastProjectile { get; private set; }
        public event Action<Vector2> AttackPerformed;

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
        }

        private void Update() => TickCombat(Time.deltaTime);

        public void Configure(UnitCombatData data)
        {
            combatData = data;
        }

        public bool Attack(UnitController enemy)
        {
            ResolveReferences();
            if (!IsValidTarget(enemy))
                return false;

            GetComponent<UnitGuard>()?.EndGuard();
            StopCombat();
            target = enemy;
            targetHealth = enemy.GetComponent<HealthComponent>();
            targetHealth.Died += HandleTargetDeath;
            repathRemaining = 0f;
            // Cooldown deliberately survives a new order: repeated clicks cannot accelerate attacks.
            TickCombat(0f);
            return target != null || !enemy.IsAlive;
        }

        public void StopCombat()
        {
            ClearTarget();
            movement ??= GetComponent<UnitMovement>();
            movement.Stop();
        }

        public void TickCombat(float deltaTime)
        {
            ResolveReferences();
            attackCooldown = Mathf.Max(0f, attackCooldown - Mathf.Max(0f, deltaTime));
            if (controller.CurrentState == UnitState.Guarding)
                return;
            if (target == null)
                return;
            if (!IsValidTarget(target))
            {
                StopCombat();
                return;
            }

            grid ??= FindAnyObjectByType<GridManager>();
            if (grid == null)
            {
                StopCombat();
                return;
            }

            Vector2Int ownCell = grid.WorldToGrid(transform.position);
            Vector2Int targetCell = grid.WorldToGrid(target.transform.position);
            GridCell own = grid.GetCell(ownCell.x, ownCell.y);
            GridCell enemy = grid.GetCell(targetCell.x, targetCell.y);
            if (own == null || enemy == null || !own.Walkable || !enemy.Walkable)
            {
                StopCombat();
                return;
            }

            float range = combatData.AttackRange;
            if (own.HeightLevel == enemy.HeightLevel
                && Vector2.Distance(transform.position, target.transform.position) <= range)
            {
                if (movement.IsMoving)
                    movement.Stop();
                controller.SetState(UnitState.Attacking);
                if (attackCooldown <= 0f)
                {
                    attackCooldown = combatData.AttackInterval;
                    Vector2 direction = target.transform.position - transform.position;
                    if (combatData.AttackDelivery == AttackDelivery.Projectile)
                        LaunchProjectile();
                    else
                        targetHealth.ReceiveAttack(combatData.BaseDamage,
                            combatData.UnitType, combatData.AttackDelivery);
                    AttackPerformed?.Invoke(direction);
                }
                return;
            }

            repathRemaining -= Mathf.Max(0f, deltaTime);
            if (!movement.IsMoving || (targetCell != lastTargetCell && repathRemaining <= 0f))
            {
                if (!movement.TryIssueChase(targetCell))
                {
                    StopCombat();
                    return;
                }
                lastTargetCell = targetCell;
                repathRemaining = repathInterval;
            }
            controller.SetState(UnitState.Chasing);
        }

        private bool IsValidTarget(UnitController enemy)
        {
            if (combatData == null || !controller.IsAlive || !health.IsAlive || enemy == null
                || enemy == controller || enemy.Faction == controller.Faction || !enemy.IsAlive
                || !enemy.gameObject.activeInHierarchy)
                return false;
            HealthComponent enemyHealth = enemy.GetComponent<HealthComponent>();
            return enemyHealth != null && enemyHealth.isActiveAndEnabled
                && enemyHealth.CombatData != null && enemyHealth.IsAlive;
        }

        private void LaunchProjectile()
        {
            var arrow = new GameObject("CombatProjectile");
            arrow.transform.position = transform.position + new Vector3(0f, 0f, -0.1f);
            SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
            renderer.sprite = projectileSprite;
            renderer.sortingOrder = 15;
            CombatProjectile projectile = arrow.AddComponent<CombatProjectile>();
            projectile.Initialize(controller.Faction, target, combatData.BaseDamage,
                combatData.UnitType, combatData.ProjectileSpeed);
            LastProjectile = projectile;
        }

        private void ResolveReferences()
        {
            controller ??= GetComponent<UnitController>();
            movement ??= GetComponent<UnitMovement>();
            health ??= GetComponent<HealthComponent>();
        }

        private void HandleTargetDeath(HealthComponent deadTarget) => StopCombat();

        private void HandleOwnState(UnitState state)
        {
            if (state == UnitState.Dead)
                ClearTarget();
        }

        private void ClearTarget()
        {
            if (targetHealth != null)
                targetHealth.Died -= HandleTargetDeath;
            target = null;
            targetHealth = null;
        }
    }
}
