using System.Collections.Generic;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using TinyKingdom.Match;
using TinyKingdom.Presentation;
using TinyKingdom.Audio;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public sealed class BuildingDefense : MonoBehaviour
    {
        public const int CastleAutomaticArrowDamage = 12;
        public const float VolleyShotInterval = 0.14f;
        public const float TowerArrowSpeed = 16f;
        public const float TowerHitTolerance = 0.30f;
        public const float CannonInterval = 5f;
        private readonly List<HealthComponent> targets = new();
        private IDefensiveBuildingHost host;
        private BuildingGarrison garrison;
        private BuildingDefenseUpgrade upgrade;
        private HealthComponent volleyTarget;
        private int shotsRemaining;
        private int nextShotIndex;
        private float shotRemaining;
        private float cannonRemaining;
        private Sprite projectileSprite;
        private Phase6C2AssetCatalog cannonAssets;
        private float projectileSpeed;
        private float projectileArcHeight;
        private float remaining;

        public int LastVolleyArrowCount { get; private set; }
        public int ShotsRemaining => shotsRemaining;
        public Sprite ArrowSprite => projectileSprite;

        public static int ArrowCount(BuildingType type, int garrisonCount,
            bool upgradePurchased)
            => Mathf.Max(0, garrisonCount)
                + (type == BuildingType.Castle || type == BuildingType.Tower
                    && upgradePurchased ? 1 : 0);

        public static int ArrowDamage(BuildingType type, int shotIndex,
            int garrisonDamage)
            => type == BuildingType.Castle && shotIndex == 0
                ? CastleAutomaticArrowDamage : garrisonDamage;

        public void Configure(IDefensiveBuildingHost owner, BuildingGarrison station,
            Sprite arrowSprite, float arrowSpeed, float arcHeight)
        {
            host = owner;
            garrison = station;
            upgrade = GetComponent<BuildingDefenseUpgrade>();
            projectileSprite = arrowSprite;
            cannonAssets = owner.Type == BuildingType.Castle
                ? Phase6C2AssetCatalog.Load() : null;
            projectileSpeed = arrowSpeed;
            projectileArcHeight = arcHeight;
            remaining = owner.DefenseData.DefensiveVolleyInterval;
            cannonRemaining = CannonInterval;
            shotsRemaining = 0;
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay || host == null || !host.IsDefenseReady
                || garrison == null)
            {
                shotsRemaining = 0;
                volleyTarget = null;
                return;
            }
            if (shotsRemaining > 0)
            {
                shotRemaining -= Time.deltaTime;
                if (shotRemaining <= 0f) FireNextShot();
            }
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                remaining = host.DefenseData.DefensiveVolleyInterval;
                FireVolley();
            }
            if (host.Type != BuildingType.Castle || upgrade?.Purchased != true) return;
            cannonRemaining -= Time.deltaTime;
            if (cannonRemaining > 0f) return;
            cannonRemaining = CannonInterval;
            FindTargets();
            if (targets.Count > 0)
                GoblinDynamiteProjectile.Spawn(
                    host.HostTransform.position
                        + (Vector3)host.DefenseData.DefensiveProjectileOrigin,
                    targets[0].transform.position, host.Faction,
                    cannonAssets);
        }

        public int FireVolley()
        {
            if (!MatchManager.AllowsGameplay || host == null || !host.IsDefenseReady
                || garrison == null
                || projectileSprite == null || shotsRemaining > 0) return 0;
            int arrows = ArrowCount(host.Type, garrison.InternalCount,
                upgrade?.Purchased == true);
            if (arrows == 0) return 0;
            FindTargets();
            if (targets.Count == 0) return 0;
            volleyTarget = targets[0];
            shotsRemaining = arrows;
            nextShotIndex = 0;
            shotRemaining = 0f;
            LastVolleyArrowCount = arrows;
            FireNextShot();
            return arrows;
        }

        private void FireNextShot()
        {
            if (shotsRemaining == 0) return;
            // An occupant who leaves during the volley cannot keep contributing
            // arrows after departure. The automatic shot remains available.
            if (nextShotIndex >= ArrowCount(host.Type, garrison.InternalCount,
                    upgrade?.Purchased == true)
                || volleyTarget == null || !volleyTarget.IsTargetable
                || !CombatVisibility.CanFactionCurrentlyObserveTarget(
                    host.Faction, volleyTarget))
            {
                shotsRemaining = 0;
                volleyTarget = null;
                return;
            }
            Launch(volleyTarget, nextShotIndex++);
            shotsRemaining--;
            shotRemaining = VolleyShotInterval;
            if (shotsRemaining == 0) volleyTarget = null;
        }

        private void FindTargets()
        {
            targets.Clear();
            float rangeSquared = host.DefenseData.DefensiveAttackRange
                * host.DefenseData.DefensiveAttackRange;
            foreach (UnitController unit in UnitController.ActiveUnits)
            {
                if (unit == null || !unit.IsTargetable || unit.Faction == host.Faction) continue;
                HealthComponent health = unit.GetComponent<HealthComponent>();
                if (health == null || !health.IsTargetable
                    || !CombatVisibility.CanFactionCurrentlyObserveTarget(
                        host.Faction, health)
                    || ((Vector2)(unit.transform.position - host.HostTransform.position)).sqrMagnitude
                        > rangeSquared) continue;
                targets.Add(health);
            }
            targets.Sort((a, b) =>
            {
                float da = ((Vector2)(a.transform.position - host.HostTransform.position)).sqrMagnitude;
                float db = ((Vector2)(b.transform.position - host.HostTransform.position)).sqrMagnitude;
                int distance = da.CompareTo(db);
                if (distance != 0) return distance;
                int x = a.transform.position.x.CompareTo(b.transform.position.x);
                if (x != 0) return x;
                int y = a.transform.position.y.CompareTo(b.transform.position.y);
                return y != 0 ? y : a.GetEntityId().CompareTo(b.GetEntityId());
            });
        }

        private void Launch(HealthComponent target, int shotIndex)
        {
            var arrow = new GameObject("BuildingDefensiveArrow");
            arrow.transform.position = host.HostTransform.position
                + (Vector3)host.DefenseData.DefensiveProjectileOrigin;
            SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
            renderer.sprite = projectileSprite;
            renderer.sortingOrder = 15;
            CombatProjectile projectile = arrow.AddComponent<CombatProjectile>();
            int damage = ArrowDamage(host.Type, shotIndex,
                host.DefenseData.DefensiveArrowDamage);
            if (host.Type == BuildingType.Tower)
                projectile.InitializeStraight(host.Faction, target, damage,
                    Mathf.Max(TowerArrowSpeed, projectileSpeed), TowerHitTolerance);
            else
                projectile.Initialize(host.Faction, target, damage, UnitType.Archer,
                    projectileSpeed, projectileArcHeight, 1f, false);
            if (host.Type == BuildingType.Tower && shotIndex == 0
                && upgrade?.Purchased == true)
                GameplayAudioService.Request(GameplayAudioEvent.ArcherAttack,
                    arrow.transform.position, host.Faction);
        }
    }
}
