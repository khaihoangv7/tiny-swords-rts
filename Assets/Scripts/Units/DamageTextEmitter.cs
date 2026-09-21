using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class DamageTextEmitter : MonoBehaviour
    {
        private HealthComponent health;

        private void OnEnable()
        {
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
