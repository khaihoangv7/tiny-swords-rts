using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class DamageTextEmitter : MonoBehaviour
    {
        // Phase 5A uses hit flash and a health bar. Kept for legacy scene/prefab
        // compatibility, but no normal combat numbers are emitted by default.
        [SerializeField] private bool showDamageNumbers = false;
        private HealthComponent health;

        private void OnEnable()
        {
            if (!showDamageNumbers) return;
            health = GetComponent<HealthComponent>();
            health.DamageReceived += HandleDamage;
        }

        private void OnDisable()
        {
            if (health != null) health.DamageReceived -= HandleDamage;
        }

        private void HandleDamage(HealthComponent target, int actualLoss, bool guarded)
            => FloatingDamageText.Show(target.transform.position, actualLoss, guarded);
    }
}
