using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public sealed class BuildingCombatTarget : MonoBehaviour, ICombatDamageReceiver
    {
        private readonly List<Vector3> candidates = new();
        private IDefensiveBuildingHost host;
        private MonoBehaviour hostBehaviour;
        private SpriteRenderer visual;
        private Color baseColor;
        private float flashRemaining;

        public Faction Faction => host.Faction;
        public BuildingType Type => host != null ? host.Type : default;
        public bool IsAlive => host != null && host.IsAlive;
        public bool IsTargetable => IsAlive && host.IsDefenseReady;
        public Transform TargetTransform => host.HostTransform;
        public Collider2D TargetCollider => host.InteractionCollider;
        public event Action<ICombatDamageReceiver> CombatDied;

        public void Configure(IDefensiveBuildingHost owner)
        {
            host = owner;
            hostBehaviour = owner as MonoBehaviour;
            visual = owner.HostTransform.GetComponentInChildren<SpriteRenderer>();
            if (visual != null) baseColor = visual.color;
            owner.DefenseHostDestroyed += OnDestroyed;
        }

        private void OnDestroy()
        {
            if (host != null) host.DefenseHostDestroyed -= OnDestroyed;
        }

        private void Update()
        {
            if (flashRemaining <= 0f || visual == null) return;
            flashRemaining -= Time.deltaTime;
            if (flashRemaining <= 0f) visual.color = baseColor;
        }

        public bool ReceiveAttack(int baseDamage, UnitType attacker, AttackDelivery delivery,
            float sourceMultiplier = 1f, bool applyCounterMultiplier = true,
            UnitController damageSource = null)
        {
            if (!IsTargetable || baseDamage <= 0) return false;
            int damage = (int)Math.Floor(baseDamage * Math.Max(0d, sourceMultiplier) + 0.5d);
            bool applied = host.ReceiveBuildingDamage(damage);
            if (applied && visual != null && host.IsAlive)
            {
                baseColor = visual.color;
                visual.color = new Color(1f, 0.35f, 0.35f, baseColor.a);
                flashRemaining = 0.12f;
            }
            return applied;
        }

        [ContextMenu("Phase 5C2/Test Damage 25")]
        private void TestDamage() => host?.ReceiveBuildingDamage(25);

        [ContextMenu("Phase 5C2/Test Destroy Building")]
        private void TestDestroy() => host?.ReceiveBuildingDamage(host.MaxHP);

        public float DistanceFrom(Vector3 point)
        {
            Vector2 closest = TargetCollider != null
                ? TargetCollider.ClosestPoint(point) : (Vector2)TargetTransform.position;
            return Vector2.Distance(point, closest);
        }

        public Vector3 AttackPresentationPoint(Vector3 attackerPosition)
        {
            if (TargetCollider == null) return TargetTransform.position;
            Vector2 closest = TargetCollider.ClosestPoint(attackerPosition);
            if ((closest - (Vector2)attackerPosition).sqrMagnitude <= 0.000001f)
                return TargetTransform.position;
            return new Vector3(closest.x, closest.y, TargetTransform.position.z);
        }

        public bool TryFindAttackPosition(UnitController attacker, float attackRange,
            GridManager grid, out Vector3 position)
        {
            position = default;
            if (attacker == null || grid == null || host == null) return false;
            PathfindingSystem paths = grid.GetComponent<PathfindingSystem>();
            if (paths == null) return false;
            BuildingSpawnPoint.TryFindMany(host, grid, 16, candidates, 3);
            Vector2Int start = grid.WorldToGrid(attacker.transform.position);
            float best = float.PositiveInfinity;
            foreach (Vector3 candidate in candidates)
            {
                if (DistanceFrom(candidate) > attackRange + 0.15f) continue;
                Vector2Int destination = grid.WorldToGrid(candidate);
                if (paths.FindPath(start, destination).Count == 0) continue;
                float distance = ((Vector2)(candidate - attacker.transform.position)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                position = candidate;
            }
            return best < float.PositiveInfinity;
        }

        private void OnDestroyed() => CombatDied?.Invoke(this);
    }
}
