using TinyKingdom.Economy;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class UnitHitFlash : MonoBehaviour
    {
        private const float RedSeconds = 0.09f;
        private const float WhiteSeconds = 0.08f;
        private const float TotalSeconds = RedSeconds + WhiteSeconds;
        private HealthComponent health;
        private SpriteRenderer source;
        private SpriteRenderer overlay;
        private float remaining;

        private void OnEnable()
        {
            health = GetComponent<HealthComponent>();
            health.DamageReceived += OnDamage;
            health.Died += OnDeath;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.DamageReceived -= OnDamage;
                health.Died -= OnDeath;
            }
            if (overlay != null) overlay.enabled = false;
        }

        public void Configure(Material flashMaterial)
        {
            source = GetComponent<UnitAnimator>()?.VisualRenderer
                ?? GetComponent<PawnVisual>()?.VisualRenderer;
            if (source == null || flashMaterial == null)
            {
                Debug.LogError("UnitHitFlash requires an animated SpriteRenderer and flash material.", this);
                return;
            }
            var child = new GameObject("HitFlashOverlay");
            child.transform.SetParent(source.transform, false);
            overlay = child.AddComponent<SpriteRenderer>();
            overlay.sharedMaterial = flashMaterial;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 1;
            overlay.enabled = false;
        }

        private void Update()
        {
            if (remaining <= 0f || overlay == null) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f || !health.IsAlive)
            {
                overlay.enabled = false;
                return;
            }
            overlay.color = remaining > WhiteSeconds
                ? new Color(1f, 0.12f, 0.12f, 0.82f)
                : new Color(1f, 1f, 1f, 0.92f);
        }

        private void LateUpdate()
        {
            if (overlay == null || !overlay.enabled || source == null) return;
            overlay.sprite = source.sprite;
            overlay.flipX = source.flipX;
            overlay.flipY = source.flipY;
        }

        private void OnDamage(HealthComponent _, int loss, bool guarded)
        {
            if (loss <= 0 || overlay == null) return;
            remaining = TotalSeconds;
            overlay.color = new Color(1f, 0.12f, 0.12f, 0.82f);
            overlay.enabled = true;
        }

        private void OnDeath(HealthComponent _) { if (overlay != null) overlay.enabled = false; }
    }
}
