using TinyKingdom.Audio;
using TinyKingdom.Match;
using TinyKingdom.Presentation;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController), typeof(UnitMovement), typeof(HealthComponent))]
    public sealed class MonkHealer : MonoBehaviour
    {
        public const float HealAmount = 6.25f;
        public const float HealInterval = 2f;
        public const float HealRange = 2f;
        public const float AutoDetectionRange = 4f;
        private const float ScanInterval = 0.25f;

        private UnitController controller;
        private UnitMovement movement;
        private UnitAnimator animator;
        private HealthComponent target;
        private bool manual;
        private bool explicitMovement;
        private float cooldown;
        private float scanRemaining;
        private float repathRemaining;
        private Sprite[] healEffect;

        public HealthComponent Target => target;
        public bool HasManualOrder => manual && target != null;

        private void Awake()
        {
            controller = GetComponent<UnitController>();
            movement = GetComponent<UnitMovement>();
            animator = GetComponent<UnitAnimator>();
            healEffect = Phase6C2AssetCatalog.Load()?.HealEffect(controller.Faction);
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay || !controller.IsAlive || controller.IsProtected) return;
            cooldown = Mathf.Max(0f, cooldown - Time.deltaTime);
            scanRemaining -= Time.deltaTime;
            repathRemaining -= Time.deltaTime;
            if (explicitMovement)
            {
                if (movement.IsMoving) return;
                explicitMovement = false;
                scanRemaining = ScanInterval;
            }
            if (!IsEligible(target))
            {
                target = null;
                manual = false;
            }
            if (target == null && scanRemaining <= 0f)
            {
                scanRemaining = ScanInterval;
                target = FindAutomaticTarget();
            }
            if (target == null) return;
            float distance = Vector2.Distance(transform.position, target.transform.position);
            if (!manual && distance > AutoDetectionRange + 0.1f)
            {
                target = null;
                return;
            }
            if (distance > HealRange)
            {
                if (repathRemaining <= 0f)
                {
                    repathRemaining = 0.4f;
                    movement.TryIssueChase(target.transform.position);
                }
                return;
            }
            if (movement.IsMoving) movement.Stop();
            if (cooldown > 0f) return;
            int restored = target.Heal(HealAmount);
            if (restored <= 0) return;
            cooldown = HealInterval;
            GameplayAudioService.Request(GameplayAudioEvent.MonkHeal,
                target.transform.position, controller.Faction);
            Vector2 direction = target.transform.position - transform.position;
            animator?.PlayAction(direction);
            OneShotSpriteVfx.Spawn("Heal_Effect", target.transform.position,
                healEffect, 10f, 1f, 18, target.GetComponent<UnitController>().Faction);
            if (target.CurrentHP >= target.MaxHP)
            {
                target = null;
                manual = false;
            }
        }

        public bool AssignManual(HealthComponent ally)
        {
            if (!IsEligible(ally)) return false;
            target = ally;
            manual = true;
            explicitMovement = false;
            repathRemaining = 0f;
            return true;
        }

        public void CancelExplicitOrder()
        {
            target = null;
            manual = false;
            explicitMovement = true;
            scanRemaining = ScanInterval;
        }

        private HealthComponent FindAutomaticTarget()
        {
            HealthComponent best = null;
            float bestRatio = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;
            int bestId = int.MaxValue;
            for (int stableIndex = 0; stableIndex < UnitController.ActiveUnits.Count; stableIndex++)
            {
                UnitController unit = UnitController.ActiveUnits[stableIndex];
                if (unit == null || unit == controller || unit.Faction != controller.Faction) continue;
                HealthComponent health = unit.GetComponent<HealthComponent>();
                if (!IsEligible(health)) continue;
                float distance = Vector2.Distance(transform.position, unit.transform.position);
                if (distance > AutoDetectionRange) continue;
                float ratio = health.MaxHP > 0 ? health.CurrentHP / (float)health.MaxHP : 1f;
                int id = stableIndex;
                if (ratio < bestRatio - 0.0001f
                    || (Mathf.Abs(ratio - bestRatio) <= 0.0001f && distance < bestDistance - 0.0001f)
                    || (Mathf.Abs(ratio - bestRatio) <= 0.0001f
                        && Mathf.Abs(distance - bestDistance) <= 0.0001f && id < bestId))
                {
                    best = health; bestRatio = ratio; bestDistance = distance; bestId = id;
                }
            }
            return best;
        }

        private bool IsEligible(HealthComponent health)
        {
            if (health == null || !health.IsAlive || health.CurrentHP >= health.MaxHP) return false;
            UnitController ally = health.GetComponent<UnitController>();
            if (ally == null || ally == controller || ally.Faction != controller.Faction) return false;
            UnitType type = health.CombatData != null ? health.CombatData.UnitType : UnitType.GoblinTorch;
            return type is UnitType.Pawn or UnitType.Warrior or UnitType.Archer
                or UnitType.Lancer or UnitType.Monk;
        }
    }

}
