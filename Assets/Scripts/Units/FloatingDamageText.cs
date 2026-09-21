using UnityEngine;

namespace TinyKingdom.Units
{
    public sealed class FloatingDamageText : MonoBehaviour
    {
        private TextMesh label;
        private Color startColor;
        private float elapsed;
        private const float Lifetime = 0.85f;

        public static void Show(Vector3 position, int actualLoss, bool guarded)
        {
            if (actualLoss <= 0) return;
            var obj = new GameObject("FloatingDamage");
            obj.transform.position = position + new Vector3(0f, 0.8f, -0.2f);
            var text = obj.AddComponent<TextMesh>();
            text.text = guarded ? $"{actualLoss} Guard" : actualLoss.ToString();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.11f;
            text.fontSize = 32;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = guarded ? new Color(0.45f, 0.8f, 1f) : new Color(1f, 0.35f, 0.25f);
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = text.font.material;
            renderer.sortingOrder = 30;
            var effect = obj.AddComponent<FloatingDamageText>();
            effect.label = text;
            effect.startColor = text.color;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            transform.position += Vector3.up * (0.7f * Time.deltaTime);
            Color color = startColor;
            color.a = 1f - Mathf.Clamp01(elapsed / Lifetime);
            label.color = color;
            if (elapsed >= Lifetime) Destroy(gameObject);
        }
    }
}
