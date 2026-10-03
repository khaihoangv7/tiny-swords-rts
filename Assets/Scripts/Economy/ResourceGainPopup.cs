using UnityEngine;

namespace TinyKingdom.Economy
{
    // World-space feedback only. Stockpile and cargo events are authoritative.
    public sealed class ResourceGainPopup : MonoBehaviour
    {
        public const float DefaultIconWorldSize = 0.25f;
        public const int DefaultAmountFontSize = 32;
        public const float DepositIconWorldSize = 0.60f;
        public const int DepositAmountFontSize = 72;
        public const float DepositIconOffsetX = -0.34f;
        public const float DepositAmountOffsetX = 0.08f;
        public const float AmountCharacterSize = 0.08f;

        private SpriteRenderer icon;
        private TextMesh amountLabel;
        private Vector3 origin;
        private float elapsed;
        private const float Lifetime = 0.85f;

        public static void Show(Vector3 position, Sprite sprite, int amount)
            => Show(position, sprite, amount, DefaultIconWorldSize,
                DefaultAmountFontSize, false);

        public static void ShowDeposit(Vector3 position, Sprite sprite, int amount)
            => Show(position, sprite, amount, DepositIconWorldSize,
                DepositAmountFontSize, true);

        private static void Show(Vector3 position, Sprite sprite, int amount,
            float iconWorldSize, int amountFontSize, bool depositLayout)
        {
            if (sprite == null || amount <= 0) return;
            var obj = new GameObject("ResourceGainPopup");
            obj.transform.position = position;
            obj.AddComponent<ResourceGainPopup>().Initialize(sprite, amount,
                iconWorldSize, amountFontSize, depositLayout);
        }

        private void Initialize(Sprite sprite, int amount, float iconWorldSize,
            int amountFontSize, bool depositLayout)
        {
            origin = transform.position;
            var iconObject = new GameObject("Icon");
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localPosition = new Vector3(
                depositLayout ? DepositIconOffsetX : -0.14f, 0f, 0f);
            float iconSize = Mathf.Max(sprite.rect.width, sprite.rect.height)
                / sprite.pixelsPerUnit;
            iconObject.transform.localScale = Vector3.one * (iconWorldSize / iconSize);
            icon = iconObject.AddComponent<SpriteRenderer>();
            icon.sprite = sprite;
            icon.sortingOrder = 40;

            var labelObject = new GameObject("Amount");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = depositLayout
                ? new Vector3(DepositAmountOffsetX, 0f, 0f)
                : new Vector3(0.02f, -0.1f, 0f);
            amountLabel = labelObject.AddComponent<TextMesh>();
            amountLabel.text = "+" + amount;
            amountLabel.fontSize = amountFontSize;
            amountLabel.characterSize = AmountCharacterSize;
            amountLabel.anchor = depositLayout
                ? TextAnchor.MiddleLeft
                : TextAnchor.LowerLeft;
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
