using TinyKingdom.Presentation;
using UnityEngine;

namespace TinyKingdom.Units
{
    public sealed class DelayedRegeneration : MonoBehaviour
    {
        public const float DelaySeconds = 5f;
        public const float TickSeconds = 1f;

        [SerializeField] private float delay = DelaySeconds;
        [SerializeField] private float interval = TickSeconds;

        private HealthComponent health;
        private float remaining;
        private float fractional;
        private Sprite[] effect;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            remaining = delay;
            effect = Phase6C2AssetCatalog.Load()?.HealEffect(
                GetComponent<UnitController>()?.Faction ?? Faction.Blue);
            if (health != null)
                health.DamageReceived += OnDamage;
        }

        private void OnDestroy()
        {
            if (health != null)
                health.DamageReceived -= OnDamage;
        }

        private void OnDamage(HealthComponent _, int actual, bool __)
        {
            if (actual > 0)
                remaining = delay;
        }

        private void Update()
        {
            if (health == null || !health.IsAlive || health.CurrentHP >= health.MaxHP)
                return;

            remaining -= Time.deltaTime;
            if (remaining > 0f)
                return;

            remaining += interval;
            fractional += health.MaxHP * 0.01f;
            int amount = Mathf.FloorToInt(fractional);
            fractional -= amount;
            int restored = health.Heal(amount);
            if (restored > 0)
            {
                OneShotSpriteVfx.Spawn("Heal_Effect", transform.position, effect,
                    10f, 1f, 18, GetComponent<UnitController>().Faction);
            }
        }
    }
}
