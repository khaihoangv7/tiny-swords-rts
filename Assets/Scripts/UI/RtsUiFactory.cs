using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    internal static class RtsUiFactory
    {
        internal static readonly Color TextColor = new(0.96f, 0.91f, 0.76f, 1f);
        internal static readonly Color MutedTextColor = new(0.72f, 0.75f, 0.78f, 1f);
        internal static readonly Color EnabledColor = Color.white;
        internal static readonly Color DisabledColor = new(0.42f, 0.42f, 0.42f, 0.78f);

        internal static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        internal static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        internal static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f,
            float right = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static Text Label(string name, Transform parent, string value, int size,
            TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = Rect(name, parent);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = style;
            label.color = TextColor;
            label.alignment = alignment;
            label.text = value;
            label.raycastTarget = false;
            Outline outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.07f, 0.08f, 0.9f);
            outline.effectDistance = Vector2.one;
            return label;
        }

        internal static Image Image(string name, Transform parent, Sprite sprite,
            bool preserveAspect = true)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        internal static void BuildPanelFrame(string prefix, RectTransform parent,
            Sprite[] sprites, float borderScale)
        {
            if (sprites == null || sprites.Length != 9) return;
            float scale = Mathf.Max(0.1f, borderScale);
            float left = Mathf.Round(Mathf.Max(sprites[0].rect.width,
                sprites[3].rect.width, sprites[6].rect.width) * scale);
            float right = Mathf.Round(Mathf.Max(sprites[2].rect.width,
                sprites[5].rect.width, sprites[8].rect.width) * scale);
            float top = Mathf.Round(Mathf.Max(sprites[0].rect.height,
                sprites[1].rect.height, sprites[2].rect.height) * scale);
            float bottom = Mathf.Round(Mathf.Max(sprites[6].rect.height,
                sprites[7].rect.height, sprites[8].rect.height) * scale);

            Image surface = Image(prefix + "Surface", parent, sprites[4], false);
            Stretch(surface.rectTransform);
            surface.type = UnityEngine.UI.Image.Type.Simple;
            surface.raycastTarget = true;

            AddFramePiece(prefix + "Top", parent, sprites[1],
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(left - 1f, -top), new Vector2(-right + 1f, 0f));
            AddFramePiece(prefix + "Bottom", parent, sprites[7],
                Vector2.zero, new Vector2(1f, 0f),
                new Vector2(left - 1f, 0f), new Vector2(-right + 1f, bottom));
            AddFramePiece(prefix + "Left", parent, sprites[3],
                Vector2.zero, new Vector2(0f, 1f),
                new Vector2(0f, bottom - 1f), new Vector2(left, -top + 1f));
            AddFramePiece(prefix + "Right", parent, sprites[5],
                new Vector2(1f, 0f), Vector2.one,
                new Vector2(-right, bottom - 1f), new Vector2(0f, -top + 1f));

            AddCorner(prefix + "TopLeft", parent, sprites[0],
                new Vector2(0f, 1f), new Vector2(0f, 1f), left, top);
            AddCorner(prefix + "TopRight", parent, sprites[2],
                Vector2.one, Vector2.one, right, top);
            AddCorner(prefix + "BottomLeft", parent, sprites[6],
                Vector2.zero, Vector2.zero, left, bottom);
            AddCorner(prefix + "BottomRight", parent, sprites[8],
                new Vector2(1f, 0f), new Vector2(1f, 0f), right, bottom);
        }

        private static void AddFramePiece(string name, Transform parent, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            Image image = Image(name, parent, sprite, false);
            image.rectTransform.anchorMin = anchorMin;
            image.rectTransform.anchorMax = anchorMax;
            image.rectTransform.offsetMin = offsetMin;
            image.rectTransform.offsetMax = offsetMax;
            image.type = UnityEngine.UI.Image.Type.Simple;
        }

        private static void AddCorner(string name, Transform parent, Sprite sprite,
            Vector2 anchor, Vector2 pivot, float width, float height)
        {
            Image image = Image(name, parent, sprite, false);
            Anchor(image.rectTransform, anchor, pivot, Vector2.zero,
                new Vector2(width, height));
            image.type = UnityEngine.UI.Image.Type.Simple;
        }
    }
}
