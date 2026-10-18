using TinyKingdom.Vision;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class UnitHealthBar : MonoBehaviour
    {
        public static readonly Color32 DefaultFriendlyFillColor = new(74, 201, 86, 255);
        public static readonly Color32 DefaultEnemyFillColor = new(255, 62, 62, 255);

        // The source sheet has three separated frame pieces with transparent
        // padding. These are the opaque 16 + 64 + 16 pixel regions.
        private const int FrameWidth = 96;
        private const int FrameHeight = 21;
        private const int InnerX = 11;
        private const int InnerY = 6;
        private const int InnerWidth = 74;
        private const int InnerHeight = 6;

        private HealthComponent health;
        private Transform bar;
        private SpriteRenderer fill;
        private Texture2D frameTexture;
        private Texture2D fillTexture;
        private Sprite frameSprite;
        private Sprite croppedFill;
        private int visibleFillPixels = -1;
        private bool suppressed;
        [SerializeField] private Color32 friendlyFillColor = new(74, 201, 86, 255);
        [SerializeField] private Color32 enemyFillColor = new(255, 62, 62, 255);

        public Color32 AppliedFillColor => fill != null ? (Color32)fill.color : default;

        private void OnEnable()
        {
            health = GetComponent<HealthComponent>();
            health.HealthChanged += Refresh;
            health.Died += OnDeath;
            PlayerViewRelation.Changed += OnPlayerViewFactionChanged;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.HealthChanged -= Refresh;
                health.Died -= OnDeath;
            }
            PlayerViewRelation.Changed -= OnPlayerViewFactionChanged;
        }

        private void OnDestroy()
        {
            if (croppedFill != null) Destroy(croppedFill);
            if (frameSprite != null) Destroy(frameSprite);
            if (fillTexture != null) Destroy(fillTexture);
            if (frameTexture != null) Destroy(frameTexture);
        }

        public void Configure(Sprite left, Sprite middle, Sprite right, Sprite fillSprite)
        {
            if (left == null || middle == null || right == null || fillSprite == null
                || left.texture != middle.texture || left.texture != right.texture
                || !left.texture.isReadable || !fillSprite.texture.isReadable
                || left.rect.width != 17 || middle.rect.width != 66
                || right.rect.width != 17 || left.rect.height != FrameHeight
                || middle.rect.height != FrameHeight || right.rect.height != FrameHeight)
            {
                Debug.LogError("Tiny Swords health bar sprites or readable imports are invalid.", this);
                return;
            }

            frameSprite = CreateFrame(left, middle, right);
            fillTexture = CreateFill(fillSprite);
            if (fillTexture == null)
            {
                Debug.LogError("Tiny Swords health bar fill contains no opaque color.", this);
                return;
            }

            var root = new GameObject("TinySwordsHealthBar");
            bar = root.transform;
            bar.SetParent(transform, false);
            float scale = GetComponent<UnitAnimator>()?.AnimationSet?.VisualScale
                ?? UnitVisualPresentation.Scale;
            bar.localPosition = new Vector3(0f, 1.01f * scale / UnitVisualPresentation.Scale, -0.1f);
            MakeSprite("Frame", frameSprite, 0f, 0f, 32);
            // Frame pixels [11,84] x [6,11] are the rectangular interior.
            // A left-pivot fill starts at the first inner pixel and stays there.
            fill = MakeSprite("Fill", null,
                (InnerX - FrameWidth * 0.5f) / left.pixelsPerUnit,
                (InnerY + InnerHeight * 0.5f - FrameHeight * 0.5f)
                    / left.pixelsPerUnit, 33);
            ApplyFactionColor();
            Refresh(health);
        }

        private Sprite CreateFrame(Sprite left, Sprite middle, Sprite right)
        {
            Texture2D source = left.texture;
            Color32[] sourcePixels = source.GetPixels32();
            var pixels = new Color32[FrameWidth * FrameHeight];
            // Discard the transparent padding at the seams of the imported slices.
            CopySlice(left, 0, 16, 0, sourcePixels, source.width, pixels);
            CopySlice(middle, 1, 64, 16, sourcePixels, source.width, pixels);
            CopySlice(right, 1, 16, 80, sourcePixels, source.width, pixels);
            frameTexture = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            frameTexture.SetPixels32(pixels);
            frameTexture.Apply(false);
            return Sprite.Create(frameTexture, new Rect(0, 0, FrameWidth, FrameHeight),
                new Vector2(0.5f, 0.5f), left.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static void CopySlice(Sprite sprite, int inset, int width, int destinationX,
            Color32[] source, int sourceWidth, Color32[] destination)
        {
            int sourceX = Mathf.RoundToInt(sprite.rect.x) + inset;
            int sourceY = Mathf.RoundToInt(sprite.rect.y);
            for (int y = 0; y < FrameHeight; y++)
                for (int x = 0; x < width; x++)
                    destination[y * FrameWidth + destinationX + x] =
                        source[(sourceY + y) * sourceWidth + sourceX + x];
        }

        private static Texture2D CreateFill(Sprite source)
        {
            Texture2D image = source.texture;
            Color32[] pixels = image.GetPixels32();
            Color32 color = default;
            Rect rect = source.rect;
            for (int y = (int)rect.y; y < rect.yMax && color.a == 0; y++)
                for (int x = (int)rect.x; x < rect.xMax; x++)
                {
                    Color32 candidate = pixels[y * image.width + x];
                    if (candidate.a == 0) continue;
                    color = candidate;
                    break;
                }
            if (color.a == 0) return null;

            var texture = new Texture2D(InnerWidth, InnerHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var solid = new Color32[InnerWidth * InnerHeight];
            for (int i = 0; i < solid.Length; i++)
                solid[i] = new Color32(255, 255, 255, color.a);
            texture.SetPixels32(solid);
            texture.Apply(false);
            return texture;
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, float x, float y, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(bar, false);
            obj.transform.localPosition = new Vector3(x, y, 0f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void Refresh(HealthComponent target)
        {
            if (bar == null || target == null) return;
            bool visible = !suppressed && target.IsAlive && target.CurrentHP < target.MaxHP;
            bar.gameObject.SetActive(visible);
            if (!visible) return;

            float ratio = Mathf.Clamp01(target.CurrentHP / (float)target.MaxHP);
            int pixels = Mathf.Clamp(Mathf.CeilToInt(InnerWidth * ratio), 0, InnerWidth);
            if (pixels == visibleFillPixels) return;
            if (croppedFill != null) Destroy(croppedFill);
            croppedFill = pixels > 0
                ? Sprite.Create(fillTexture, new Rect(0, 0, pixels, InnerHeight),
                    new Vector2(0f, 0.5f), frameSprite.pixelsPerUnit, 0,
                    SpriteMeshType.FullRect)
                : null;
            fill.sprite = croppedFill;
            visibleFillPixels = pixels;
        }

        private void OnDeath(HealthComponent _) { if (bar != null) bar.gameObject.SetActive(false); }

        private void OnPlayerViewFactionChanged(Faction _) => ApplyFactionColor();

        private void ApplyFactionColor()
        {
            if (fill == null) return;
            UnitController owner = GetComponent<UnitController>();
            fill.color = owner != null && PlayerViewRelation.IsFriendlyToView(owner.Faction)
                ? friendlyFillColor : enemyFillColor;
        }

        public void SetSuppressed(bool value)
        {
            suppressed = value;
            Refresh(health != null ? health : GetComponent<HealthComponent>());
        }
    }
}
