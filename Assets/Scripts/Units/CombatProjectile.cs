using UnityEngine;

namespace TinyKingdom.Units
{
    public sealed class CombatProjectile : MonoBehaviour
    {
        private Faction owner;
        private MonoBehaviour targetObject;
        private ICombatDamageReceiver target;
        private int authorizedBaseDamage;
        private UnitType attackerType;
        private float speed;
        private float arcHeight;
        private float travelDuration;
        private float elapsed;
        private Vector3 launchPosition;
        private bool resolved;
        private float sourceMultiplier = 1f;
        private bool applyCounterMultiplier = true;
        private UnitController damageSource;

        public bool IsResolved => resolved;

        public void Initialize(Faction ownerFaction, UnitController assignedTarget,
            int baseDamage, UnitType sourceType, float travelSpeed, float height = 1.2f,
            UnitController source = null)
        {
            Initialize(ownerFaction, assignedTarget != null
                ? assignedTarget.GetComponent<HealthComponent>() : null,
                baseDamage, sourceType, travelSpeed, height, 1f, true, source);
        }

        public void Initialize(Faction ownerFaction, ICombatDamageReceiver assignedTarget,
            int baseDamage, UnitType sourceType, float travelSpeed, float height,
            float damageMultiplier, bool useCounterMultiplier,
            UnitController source = null)
        {
            owner = ownerFaction;
            target = assignedTarget;
            targetObject = assignedTarget as MonoBehaviour;
            authorizedBaseDamage = baseDamage;
            attackerType = sourceType;
            speed = travelSpeed;
            arcHeight = Mathf.Max(0f, height);
            sourceMultiplier = Mathf.Max(0f, damageMultiplier);
            applyCounterMultiplier = useCounterMultiplier;
            damageSource = source;
            launchPosition = transform.position;
            elapsed = 0f;
            travelDuration = assignedTarget != null && speed > 0f
                ? Mathf.Max(0.1f, Vector2.Distance(launchPosition, assignedTarget.TargetTransform.position) / speed)
                : 0.1f;
            resolved = false;
            if (assignedTarget != null)
                FaceFlightDirection(assignedTarget.TargetTransform.position - launchPosition
                    + Vector3.up * (4f * arcHeight));
        }

        private void Update() => TickProjectile(Time.deltaTime);

        public void TickProjectile(float deltaTime)
        {
            if (resolved) return;
            if (target == null || targetObject == null || !targetObject.gameObject.activeInHierarchy
                || !targetObject.isActiveAndEnabled || !target.IsAlive || !target.IsTargetable
                || target.Faction == owner || authorizedBaseDamage <= 0 || speed <= 0f)
            {
                Resolve();
                return;
            }

            elapsed = Mathf.Min(travelDuration, elapsed + Mathf.Max(0f, deltaTime));
            float progress = elapsed / travelDuration;
            Vector3 destination = target.TargetTransform.position;
            destination.z = launchPosition.z;
            if (progress >= 1f)
            {
                transform.position = destination;
                target.ReceiveAttack(authorizedBaseDamage, attackerType,
                    AttackDelivery.Projectile, sourceMultiplier, applyCounterMultiplier,
                    damageSource);
                Resolve();
                return;
            }

            Vector3 previous = transform.position;
            Vector3 straightPosition = Vector3.Lerp(launchPosition, destination, progress);
            transform.position = straightPosition
                + Vector3.up * (4f * arcHeight * progress * (1f - progress));
            FaceFlightDirection(transform.position - previous);
        }

        private void FaceFlightDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void Resolve()
        {
            resolved = true;
            Destroy(gameObject);
        }
    }
}
