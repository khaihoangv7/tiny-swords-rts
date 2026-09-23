using UnityEngine;

namespace TinyKingdom.Economy
{
    // World-space feedback only. Stockpile and cargo events are authoritative.
    public sealed class ResourceGainPopup : MonoBehaviour
    {
        private SpriteRenderer icon;
        private TextMesh amountLabel;
        private Vector3 origin;
        private float elapsed;
        private const float Lifetime = 0.85f;

        public static void Show(Vector3 position, Sprite sprite, int amount)
        {
            if (sprite == null || amount <= 0) return;
            var obj = new GameObject("ResourceGainPopup");
            obj.transform.position = position;
            obj.AddComponent<ResourceGainPopup>().Initialize(sprite, amount);
        }

        private void Initialize(Sprite sprite, int amount)
        {
            origin = transform.position;
            var iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localPosition = new Vector3(-0.14f, 0f, 0f);
            float iconSize = Mathf.Max(sprite.rect.width, sprite.rect.height)
                / sprite.pixelsPerUnit;
            iconObject.transform.localScale = Vector3.one * (0.25f / iconSize);
            icon = iconObject.AddComponent<SpriteRenderer>();
            icon.sprite = sprite;
            icon.sortingOrder = 40;

            var labelObject = new GameObject("Amount");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0.02f, -0.1f, 0f);
            amountLabel = labelObject.AddComponent<TextMesh>();
            amountLabel.text = "+" + amount;
            amountLabel.fontSize = 32;
            amountLabel.characterSize = 0.08f;
            amountLabel.anchor = TextAnchor.LowerLeft;
            amountLabel.color = Color.white;
            amountLabel.GetComponent<MeshRenderer>().sortingOrder = 40;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Lifetime) { Destroy(gameObject); return; }
            float progress = elapsed / Lifetime;
            transform.position = origin + Vector3.up * (0.4f * progress);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
            if (icon != null) icon.color = new Color(1f, 1f, 1f, alpha);
            if (amountLabel != null) amountLabel.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
