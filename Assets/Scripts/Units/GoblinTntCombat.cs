using System.Collections.Generic;
using TinyKingdom.Match;
using TinyKingdom.Presentation;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController), typeof(UnitMovement), typeof(HealthComponent))]
    public sealed class GoblinTntCombat : MonoBehaviour
    {
        public const int Damage = 18;
        public const float AttackRange = 4.5f;
        public const float AttackInterval = 2.5f;
        public const float ExplosionRadius = 1.25f;
        public const float KnockbackDistance = 0.60f;
        public const float TargetScanInterval = 0.25f;
        public const float ChaseRepathInterval = 0.30f;
        public const float ChaseDestinationThreshold = 0.35f;
        public const int ThrowFrameCount = 7;
        public const float ThrowFramesPerSecond = 10f;
        public const int ThrowReleaseFrame = 6;
        public const float ThrowReleaseSeconds = ThrowReleaseFrame / ThrowFramesPerSecond;
        private const float DetectionRange = 6f;
        private const float Leash = 7f;
        private static readonly ProfilerMarker TargetScanMarker =
            new("TinyKingdom.GoblinTNT.TargetScan");
        private static readonly ProfilerMarker ChaseRepathMarker =
            new("TinyKingdom.GoblinTNT.ChaseRepath");
        private UnitController controller;
        private UnitMovement movement;
        private UnitAnimator animator;
        private HealthComponent target;
        private Vector3 guardOrigin;
        private float cooldown;
        private float scanRemaining;
        private float repathRemaining;
        private float releaseRemaining;
        private Vector3 pendingImpact;
        private Vector3 lastChaseDestination;
        private bool hasChaseDestination;
        private Phase6C2AssetCatalog assets;

        private void Awake()
        {
            controller = GetComponent<UnitController>(); movement = GetComponent<UnitMovement>();
            animator = GetComponent<UnitAnimator>(); guardOrigin = transform.position;
            assets = Phase6C2AssetCatalog.Load();
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay || !controller.IsAlive) return;
            float deltaTime = Time.deltaTime;
            cooldown = Mathf.Max(0f, cooldown - deltaTime);
            scanRemaining -= deltaTime;
            repathRemaining -= deltaTime;
            if (releaseRemaining > 0f)
            {
                releaseRemaining -= deltaTime;
                if (releaseRemaining <= 0f)
                    GoblinDynamiteProjectile.Spawn(transform.position + Vector3.up * 0.25f,
                        pendingImpact, controller, assets);
                return;
            }
            if (!Valid(target)
                || ((Vector2)(target.transform.position - guardOrigin)).sqrMagnitude > Leash * Leash)
            {
                target = null;
                hasChaseDestination = false;
                repathRemaining = 0f;
            }
            if (target == null && scanRemaining <= 0f)
            {
                scanRemaining = TargetScanInterval;
                using (TargetScanMarker.Auto())
                    target = FindNearest();
                if (target != null)
                {
                    hasChaseDestination = false;
                    repathRemaining = 0f;
                }
            }
            if (target == null)
            {
                bool awayFromGuard = ((Vector2)(transform.position - guardOrigin)).sqrMagnitude
                    > 0.2f * 0.2f;
                if (awayFromGuard && !movement.IsMoving && repathRemaining <= 0f)
                {
                    using (ChaseRepathMarker.Auto())
                        movement.TryIssueChase(guardOrigin);
                    repathRemaining = ChaseRepathInterval;
                }
                return;
            }
            Vector3 targetPosition = target.transform.position;
            float distanceSquared = ((Vector2)(transform.position - targetPosition)).sqrMagnitude;
            if (distanceSquared > AttackRange * AttackRange)
            {
                bool targetMoved = !hasChaseDestination
                    || ((Vector2)(targetPosition - lastChaseDestination)).sqrMagnitude
                        >= ChaseDestinationThreshold * ChaseDestinationThreshold;
                if (repathRemaining <= 0f && (!movement.IsMoving || targetMoved))
                {
                    bool issued;
                    using (ChaseRepathMarker.Auto())
                        issued = movement.TryIssueChase(targetPosition);
                    repathRemaining = ChaseRepathInterval;
                    if (issued)
                    {
                        lastChaseDestination = targetPosition;
                        hasChaseDestination = true;
                    }
                }
                return;
            }
            if (movement.IsMoving) movement.Stop();
            if (cooldown > 0f) return;
            cooldown = AttackInterval;
            pendingImpact = target.transform.position;
            releaseRemaining = ThrowReleaseSeconds;
            animator?.PlayAction(target.transform.position - transform.position);
        }

        private HealthComponent FindNearest()
        {
            HealthComponent best = null; float bestDistance = DetectionRange * DetectionRange;
            int bestId = int.MaxValue;
            for (int stableIndex = 0; stableIndex < UnitController.ActiveUnits.Count; stableIndex++)
            {
                UnitController unit = UnitController.ActiveUnits[stableIndex];
                HealthComponent health = unit != null ? unit.GetComponent<HealthComponent>() : null;
                if (!Valid(health)) continue;
                float guardDistance = ((Vector2)(unit.transform.position - guardOrigin)).sqrMagnitude;
                if (guardDistance > DetectionRange * DetectionRange) continue;
                float distance = ((Vector2)(unit.transform.position - transform.position)).sqrMagnitude;
                int id = stableIndex;
                if (distance < bestDistance - 0.0001f
                    || (Mathf.Abs(distance - bestDistance) <= 0.0001f && id < bestId))
                { best = health; bestDistance = distance; bestId = id; }
            }
            return best;
        }

        private bool Valid(HealthComponent candidate)
        {
            if (candidate == null || !candidate.IsTargetable) return false;
            Faction faction = candidate.GetComponent<UnitController>().Faction;
            return faction is Faction.Blue or Faction.Red;
        }
    }

    public sealed class GoblinDynamiteProjectile : MonoBehaviour
    {
        private const float Speed = 6f;
        private Vector3 start;
        private Vector3 target;
        private float distance;
        private float elapsed;
        private float duration;
        private SpriteRenderer visual;
        private Sprite[] frames;
        private Sprite[] explosion;
        private UnitController source;

        public static void Spawn(Vector3 origin, Vector3 destination, UnitController owner,
            Phase6C2AssetCatalog assets)
        {
            var obj = new GameObject("GoblinDynamiteProjectile");
            obj.transform.position = origin;
            GoblinDynamiteProjectile projectile = obj.AddComponent<GoblinDynamiteProjectile>();
            projectile.start = origin; projectile.target = destination; projectile.source = owner;
            projectile.frames = assets?.Dynamite; projectile.explosion = assets?.Explosion03;
            projectile.distance = Vector2.Distance(origin, destination);
            projectile.duration = Mathf.Max(0.1f, projectile.distance / Speed);
            projectile.visual = obj.AddComponent<SpriteRenderer>();
            projectile.visual.sortingOrder = 18;
            if (projectile.frames != null && projectile.frames.Length > 0)
                projectile.visual.sprite = projectile.frames[0];
            projectile.visual.enabled = Vision.FogWorldVisibility.Instance == null
                || Vision.FogWorldVisibility.Instance.ShouldPresent(Faction.Goblin, origin);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 point = Vector3.Lerp(start, target, t);
            point.y += 1.1f * 4f * t * (1f - t);
            transform.position = point;
            if (frames != null && frames.Length > 0)
                visual.sprite = frames[Mathf.FloorToInt(elapsed * 12f) % frames.Length];
            visual.enabled = Vision.FogWorldVisibility.Instance == null
                || Vision.FogWorldVisibility.Instance.ShouldPresent(
                    Faction.Goblin, transform.position);
            if (t >= 1f) Impact();
        }

        private void Impact()
        {
            OneShotSpriteVfx.Spawn("GoblinTntExplosion", target, explosion,
                12f, 1f, 19, Faction.Goblin);
            // Damage may kill a unit, which unregisters it from ActiveUnits.
            // Iterate over a snapshot so those removals cannot invalidate this pass.
            var targets = new List<UnitController>(UnitController.ActiveUnits);
            foreach (UnitController unit in targets)
            {
                if (unit == null || unit.Faction is not (Faction.Blue or Faction.Red)) continue;
                if (Vector2.Distance(unit.transform.position, target) > GoblinTntCombat.ExplosionRadius)
                    continue;
                HealthComponent health = unit.GetComponent<HealthComponent>();
                if (health == null || !health.ReceiveAttack(GoblinTntCombat.Damage,
                    UnitType.GoblinTNT, AttackDelivery.Projectile, 1f, false, source)) continue;
                unit.GetComponent<UnitMovement>()?.ApplyDeterministicKnockback(
                    target, GoblinTntCombat.KnockbackDistance);
            }
            Destroy(gameObject);
        }
    }
}
