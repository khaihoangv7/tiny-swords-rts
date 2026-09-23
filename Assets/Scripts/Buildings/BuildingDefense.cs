using System.Collections.Generic;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using TinyKingdom.Match;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public sealed class BuildingDefense : MonoBehaviour
    {
        private readonly List<HealthComponent> targets = new();
        private IDefensiveBuildingHost host;
        private BuildingGarrison garrison;
        private Sprite projectileSprite;
        private float projectileSpeed;
        private float projectileArcHeight;
        private float remaining;

        public int LastVolleyArrowCount { get; private set; }

        public void Configure(IDefensiveBuildingHost owner, BuildingGarrison station,
            Sprite arrowSprite, float arrowSpeed, float arcHeight)
        {
            host = owner;
            garrison = station;
            projectileSprite = arrowSprite;
            projectileSpeed = arrowSpeed;
            projectileArcHeight = arcHeight;
            remaining = owner.DefenseData.DefensiveVolleyInterval;
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay || host == null || !host.IsDefenseReady
                || garrison == null
                || garrison.InternalCount == 0) return;
            remaining -= Time.deltaTime;
            if (remaining > 0f) return;
            remaining += host.DefenseData.DefensiveVolleyInterval;
            FireVolley();
        }

        public int FireVolley()
        {
            LastVolleyArrowCount = 0;
            if (!MatchManager.AllowsGameplay || host == null || !host.IsDefenseReady
                || garrison == null
                || garrison.InternalCount <= 0 || projectileSprite == null) return 0;
            FindTargets();
            if (targets.Count == 0) return 0;
            int arrows = garrison.InternalCount;
            for (int i = 0; i < arrows; i++)
                Launch(targets[i % targets.Count]);
            LastVolleyArrowCount = arrows;
            return arrows;
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

        private void Launch(HealthComponent target)
        {
            var arrow = new GameObject("BuildingDefensiveArrow");
            arrow.transform.position = host.HostTransform.position
                + (Vector3)host.DefenseData.DefensiveProjectileOrigin;
            SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
            renderer.sprite = projectileSprite;
            renderer.sortingOrder = 15;
            CombatProjectile projectile = arrow.AddComponent<CombatProjectile>();
            projectile.Initialize(host.Faction, target,
                host.DefenseData.DefensiveArrowDamage, UnitType.Archer,
                projectileSpeed, projectileArcHeight, 1f, false);
        }
    }
}
