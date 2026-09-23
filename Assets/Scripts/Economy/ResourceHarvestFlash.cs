using UnityEngine;

namespace TinyKingdom.Economy
{
    // Presentation only. ResourceNode.RemainingAmount remains authoritative.
    public sealed class ResourceHarvestFlash : MonoBehaviour
    {
        private SpriteRenderer visual;
        private Color baseColor;
        private float remaining;
        private bool hideWhenFinished;

        public void Configure(SpriteRenderer renderer)
        {
            visual = renderer;
            baseColor = renderer != null ? renderer.color : Color.white;
        }

        public void Play(bool hideAfter)
        {
            if (visual == null) return;
            remaining = 0.14f;
            hideWhenFinished |= hideAfter;
            visual.enabled = true;
            visual.color = new Color(1f, 0.45f, 0.45f, baseColor.a);
        }

        private void Update()
        {
            if (remaining <= 0f || visual == null) return;
            remaining -= Time.deltaTime;
            if (remaining > 0f) return;
            visual.color = baseColor;
            if (hideWhenFinished) visual.enabled = false;
            hideWhenFinished = false;
        }
    }
}
