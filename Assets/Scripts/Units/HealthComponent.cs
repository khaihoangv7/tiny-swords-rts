using System;
using UnityEngine;
using TinyKingdom.Vision;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController))]
    public sealed class HealthComponent : MonoBehaviour, ICombatDamageReceiver
    {
        [SerializeField] private UnitCombatData combatData;
        [SerializeField] private int currentHP;

        private UnitController controller;
        private bool initialized;

        public event Action<HealthComponent> Died;
        public event Action<HealthComponent, int, bool> DamageReceived;
        public event Action<HealthComponent> HealthChanged;
        public event Action<ICombatDamageReceiver> CombatDied;
        public UnitCombatData CombatData => combatData;
        public int CurrentHP => currentHP;
        public int MaxHP => combatData != null ? combatData.MaxHP : 0;
        public bool IsAlive => initialized && currentHP > 0 && Controller.IsAlive;
        public bool IsTargetable => IsAlive && Controller.IsTargetable;
        public Transform TargetTransform => transform;
        Faction ICombatDamageReceiver.Faction => Controller.Faction;
        private UnitController Controller => controller != null ? controller : controller = GetComponent<UnitController>();

        private void Awake() => Initialize();

        public void Configure(UnitCombatData data)
        {
            combatData = data;
            currentHP = data != null ? data.MaxHP : 0;
            initialized = data != null;
        }

        public void Initialize()
        {
            if (initialized || combatData == null)
                return;
            currentHP = combatData.MaxHP;
            initialized = true;
        }

        public bool TakeDamage(int damage)
        {
            if (!IsTargetable) return false;
            bool guarding = GetComponent<UnitGuard>()?.IsGuarding == true;
            int final = guarding ? (int)Math.Floor(damage * 0.75d + 0.5d) : damage;
            return ApplyDamage(final, guarding && final < damage);
        }

        public bool ReceiveAttack(int baseDamage, UnitType attacker, AttackDelivery delivery)
            => ReceiveAttack(baseDamage, attacker, delivery, 1f, true, null);

        public bool ReceiveAttack(int baseDamage, UnitType attacker, AttackDelivery delivery,
            float sourceMultiplier, bool applyCounterMultiplier,
            UnitController damageSource = null)
        {
            if (!IsTargetable) return false;
            bool guarding = GetComponent<UnitGuard>()?.IsGuarding == true;
            int normal = CombatDamage.Calculate(baseDamage, attacker, CombatData.UnitType,
                delivery, false, sourceMultiplier, applyCounterMultiplier);
            int final = CombatDamage.Calculate(baseDamage, attacker, CombatData.UnitType,
                delivery, guarding, sourceMultiplier, applyCounterMultiplier);
            return ApplyDamage(final, guarding && final < normal, damageSource);
        }

        private bool ApplyDamage(int damage, bool reducedByGuard,
            UnitController damageSource = null)
        {
            Initialize();
            if (!IsAlive || damage <= 0)
                return false;

            int before = currentHP;
            currentHP = Mathf.Max(0, currentHP - damage);
            DamageReceived?.Invoke(this, before - currentHP, reducedByGuard);
            HealthChanged?.Invoke(this);
            if (currentHP > 0)
            {
                GetComponent<CombatController>()?.TryRetaliate(damageSource);
            }
            else
            {
                Vector3 deathPosition = transform.position;
                Controller.MarkDead();
                if (FogWorldVisibility.ShouldPresentDeath(Controller.Faction, deathPosition))
                    UnitDeathVfx.Spawn(deathPosition);
                Died?.Invoke(this);
                CombatDied?.Invoke(this);
                SpriteRenderer sprite = GetComponent<SpriteRenderer>();
                if (sprite != null)
                    sprite.enabled = false;
            }
            return true;
        }
    }
}
