using TinyKingdom.Audio;
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
        private bool straightFlight;
        private Vector3 fixedDestination;
        private float hitTolerance;

        public bool IsResolved => resolved;

        // Defensive Tower arrows snapshot the aim point. Archer projectiles
        // continue to use the existing homing arc below.
        public void InitializeStraight(Faction ownerFaction,
            ICombatDamageReceiver assignedTarget,
            int baseDamage, float travelSpeed, float tolerance)
        {
            Initialize(ownerFaction, assignedTarget, baseDamage, UnitType.Archer,
                travelSpeed, 0f, 1f, false);
            straightFlight = true;
            hitTolerance = Mathf.Max(0f, tolerance);
            fixedDestination = assignedTarget != null
                ? assignedTarget.TargetTransform.position : transform.position;
            fixedDestination.z = launchPosition.z;
            FaceFlightDirection(fixedDestination - launchPosition);
        }

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
            straightFlight = false;
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

            if (straightFlight)
            {
                TickStraight(deltaTime);
                return;
            }

            elapsed = Mathf.Min(travelDuration, elapsed + Mathf.Max(0f, deltaTime));
            float progress = elapsed / travelDuration;
            Vector3 destination = target.TargetTransform.position;
            destination.z = launchPosition.z;
            if (progress >= 1f)
            {
                transform.position = destination;
                bool unitTarget = targetObject is HealthComponent;
                bool damaged = target.ReceiveAttack(authorizedBaseDamage, attackerType,
                    AttackDelivery.Projectile, sourceMultiplier, applyCounterMultiplier,
                    damageSource);
                if (damaged && unitTarget && attackerType == UnitType.Archer)
                    GameplayAudioService.Request(GameplayAudioEvent.ArcherArrowImpact,
                        destination, owner);
                Resolve();
                return;
            }

            Vector3 previous = transform.position;
            Vector3 straightPosition = Vector3.Lerp(launchPosition, destination, progress);
            transform.position = straightPosition
                + Vector3.up * (4f * arcHeight * progress * (1f - progress));
            FaceFlightDirection(transform.position - previous);
        }

        private void TickStraight(float deltaTime)
        {
            elapsed = Mathf.Min(travelDuration, elapsed + Mathf.Max(0f, deltaTime));
            Vector3 previous = transform.position;
            transform.position = Vector3.Lerp(launchPosition, fixedDestination,
                elapsed / travelDuration);
            Vector2 segment = (Vector2)(transform.position - previous);
            Vector2 towardTarget = (Vector2)(target.TargetTransform.position - previous);
            float fraction = segment.sqrMagnitude > 0.000001f
                ? Mathf.Clamp01(Vector2.Dot(towardTarget, segment) / segment.sqrMagnitude)
                : 0f;
            Vector2 nearest = (Vector2)previous + segment * fraction;
            bool willHit = Vector2.Distance(nearest,
                target.TargetTransform.position) <= hitTolerance;
            float maximumBlockDistance = willHit
                ? ((Vector2)previous - nearest).sqrMagnitude : segment.sqrMagnitude;
            RaycastHit2D[] crossings = Physics2D.LinecastAll(previous,
                transform.position);
            foreach (RaycastHit2D crossing in crossings)
            {
                Collider2D collider = crossing.collider;
                bool targetCollider = target is HealthComponent health
                    && collider != null
                    && collider.GetComponentInParent<HealthComponent>() == health;
                if (collider == null || collider.isTrigger
                    || collider.OverlapPoint(launchPosition)
                    || targetCollider
                    || ((Vector2)crossing.point - (Vector2)previous).sqrMagnitude
                        > maximumBlockDistance)
                    continue;
                transform.position = crossing.point;
                ResolveMiss();
                return;
            }
            if (willHit)
            {
                bool damaged = target.ReceiveAttack(authorizedBaseDamage, attackerType,
                    AttackDelivery.Projectile, sourceMultiplier, applyCounterMultiplier,
                    damageSource);
                if (damaged)
                    GameplayAudioService.Request(GameplayAudioEvent.ArcherArrowImpact,
                        transform.position, owner);
                Resolve();
            }
            else if (elapsed >= travelDuration)
            {
                // The projectile sprite is the closest available ground-arrow
                // visual. It has no collider and is removed after a short time.
                ResolveMiss();
            }
        }

        private void ResolveMiss()
        {
            resolved = true;
            Destroy(gameObject, 2f);
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
