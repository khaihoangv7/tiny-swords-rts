using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    internal static class RtsUiFactory
    {
        private static readonly Dictionary<Sprite, Sprite> SlicedCenters = new();
        private static readonly Dictionary<Sprite, Sprite> VerticalSlicedCenters = new();
        internal static readonly Color TextColor = new(0.96f, 0.91f, 0.76f, 1f);
        internal static readonly Color MutedTextColor = new(0.72f, 0.75f, 0.78f, 1f);
        internal static readonly Color EnabledColor = Color.white;
        internal static readonly Color DisabledColor = new(0.42f, 0.42f, 0.42f, 0.78f);

        internal static Vector2 SafeCorner(bool right, bool top)
        {
            Rect area = Screen.safeArea;
            return new Vector2((right ? area.xMax : area.xMin)
                    / Mathf.Max(1f, Screen.width),
                (top ? area.yMax : area.yMin)
                    / Mathf.Max(1f, Screen.height));
        }

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
            UiLanguage.Bind(label);
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
            Sprite[] sprites, float borderScale, bool tilePaper = false)
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
            // Banner corner scrolls extend below its straight lower edge.
            // Stop the parchment at that edge so it does not leave a pale
            // strip underneath the finished frame.
            Stretch(surface.rectTransform, 0f,
                tilePaper ? bottom - sprites[7].rect.height * scale : 0f);
            // Banner_4 includes dark edge pixels; tiling it makes a visible
            // graph-paper grid. Keep one continuous parchment field instead.
            surface.type = UnityEngine.UI.Image.Type.Simple;
            surface.raycastTarget = true;

            AddFramePiece(prefix + "Top", parent, sprites[1],
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(left - 1f, -top), new Vector2(-right + 1f, 0f), tilePaper);
            AddFramePiece(prefix + "Bottom", parent, sprites[7],
                Vector2.zero, new Vector2(1f, 0f),
                new Vector2(left - 1f,
                    tilePaper ? bottom - sprites[7].rect.height * scale : 0f),
                new Vector2(-right + 1f, bottom), tilePaper);
            AddFramePiece(prefix + "Left", parent, sprites[3],
                Vector2.zero, new Vector2(0f, 1f),
                new Vector2(0f, bottom - 1f), new Vector2(left, -top + 1f),
                tilePaper, false);
            AddFramePiece(prefix + "Right", parent, sprites[5],
                new Vector2(1f, 0f), Vector2.one,
                new Vector2(-right, bottom - 1f), new Vector2(0f, -top + 1f),
                tilePaper, false);

            AddCorner(prefix + "TopLeft", parent, sprites[0],
                new Vector2(0f, 1f), new Vector2(0f, 1f), left, top);
            AddCorner(prefix + "TopRight", parent, sprites[2],
                Vector2.one, Vector2.one, right, top);
            AddCorner(prefix + "BottomLeft", parent, sprites[6],
                Vector2.zero, Vector2.zero, left, bottom);
            AddCorner(prefix + "BottomRight", parent, sprites[8],
                new Vector2(1f, 0f), new Vector2(1f, 0f), right, bottom);
        }

        // Tiny Swords ribbons are three-piece art. Keep the end caps at their
        // native proportions and slice the center horizontally for a continuous face.
        internal static void BuildRibbon(string prefix, RectTransform parent,
            Sprite[] sheet, int first, float scale)
        {
            if (sheet == null || sheet.Length <= first + 2) return;
            Sprite leftSprite = sheet[first];
            Sprite centerSprite = sheet[first + 1];
            Sprite rightSprite = sheet[first + 2];
            float leftWidth = leftSprite.rect.width * scale;
            float rightWidth = rightSprite.rect.width * scale;
            Image left = Image(prefix + "Left", parent, leftSprite);
            Anchor(left.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(leftWidth * 0.5f, 0f),
                leftSprite.rect.size * scale);
            Image middle = Image(prefix + "Middle", parent,
                CreateSlicedCenter(centerSprite, 8f), false);
            middle.type = UnityEngine.UI.Image.Type.Sliced;
            // Draw the stretched center beneath the caps. Their cropped
            // sprite edges contain transparent pixels, so a wider overlap
            // prevents a visible slit at either join.
            middle.transform.SetAsFirstSibling();
            middle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            middle.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            float centerY = (centerSprite.rect.center.y - leftSprite.rect.center.y)
                * scale;
            middle.rectTransform.offsetMin = new Vector2(leftWidth - 4f,
                centerY - centerSprite.rect.height * scale * 0.5f);
            middle.rectTransform.offsetMax = new Vector2(-rightWidth + 4f,
                centerY + centerSprite.rect.height * scale * 0.5f);
            Image right = Image(prefix + "Right", parent, rightSprite);
            Anchor(right.rectTransform, new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-rightWidth * 0.5f,
                    (rightSprite.rect.center.y - leftSprite.rect.center.y) * scale),
                rightSprite.rect.size * scale);
        }

        internal static Sprite CreateSlicedCenter(Sprite source, float horizontalBorder)
        {
            if (source == null) return null;
            if (SlicedCenters.TryGetValue(source, out Sprite cached) && cached != null)
                return cached;
            float border = Mathf.Min(horizontalBorder, source.rect.width * 0.25f);
            Sprite sliced = Sprite.Create(source.texture, source.rect,
                new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(border, 0f, border, 0f));
            sliced.name = source.name + "_SlicedCenter";
            SlicedCenters[source] = sliced;
            return sliced;
        }

        private static Sprite CreateVerticalSlicedCenter(Sprite source,
            float verticalBorder)
        {
            if (source == null) return null;
            if (VerticalSlicedCenters.TryGetValue(source, out Sprite cached)
                && cached != null) return cached;
            float border = Mathf.Min(verticalBorder, source.rect.height * 0.25f);
            Sprite sliced = Sprite.Create(source.texture, source.rect,
                new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0,
                SpriteMeshType.FullRect, new Vector4(0f, border, 0f, border));
            sliced.name = source.name + "_VerticalSlicedCenter";
            VerticalSlicedCenters[source] = sliced;
            return sliced;
        }

        private static void AddFramePiece(string name, Transform parent, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            bool sliced = false, bool horizontal = true)
        {
            Image image = Image(name, parent, sliced
                ? horizontal ? CreateSlicedCenter(sprite, 8f)
                    : CreateVerticalSlicedCenter(sprite, 8f)
                : sprite, false);
            image.rectTransform.anchorMin = anchorMin;
            image.rectTransform.anchorMax = anchorMax;
            image.rectTransform.offsetMin = offsetMin;
            image.rectTransform.offsetMax = offsetMax;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced
                : UnityEngine.UI.Image.Type.Simple;
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
