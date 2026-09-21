using UnityEngine;

namespace TinyKingdom.Units
{
    public sealed class CombatProjectile : MonoBehaviour
    {
        private Faction owner;
        private UnitController target;
        private HealthComponent targetHealth;
        private int authorizedBaseDamage;
        private UnitType attackerType;
        private float speed;
        private bool resolved;

        public bool IsResolved => resolved;

        public void Initialize(Faction ownerFaction, UnitController assignedTarget,
            int baseDamage, UnitType sourceType, float travelSpeed)
        {
            owner = ownerFaction;
            target = assignedTarget;
            targetHealth = assignedTarget != null ? assignedTarget.GetComponent<HealthComponent>() : null;
            authorizedBaseDamage = baseDamage;
            attackerType = sourceType;
            speed = travelSpeed;
            resolved = false;
        }

        private void Update() => TickProjectile(Time.deltaTime);

        public void TickProjectile(float deltaTime)
        {
            if (resolved) return;
            if (target == null || targetHealth == null || !target.gameObject.activeInHierarchy
                || !targetHealth.isActiveAndEnabled || !targetHealth.IsAlive
                || target.Faction == owner || authorizedBaseDamage <= 0 || speed <= 0f)
            {
                Resolve();
                return;
            }

            Vector3 destination = target.transform.position;
            destination.z = transform.position.z;
            Vector3 delta = destination - transform.position;
            float step = speed * Mathf.Max(0f, deltaTime);
            if (delta.sqrMagnitude <= step * step)
            {
                transform.position = destination;
                targetHealth.ReceiveAttack(authorizedBaseDamage, attackerType,
                    AttackDelivery.Projectile);
                Resolve();
                return;
            }

            transform.position += delta.normalized * step;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void Resolve()
        {
            resolved = true;
            Destroy(gameObject);
        }
    }
}
