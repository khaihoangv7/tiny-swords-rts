using TinyKingdom.Presentation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    // Tiny Swords BigBar: one assembled static frame and one left-anchored,
    // pixel-cropped continuous fill. UnitHealthBar continues using SmallBar.
    public sealed class BuildingHealthBar : MonoBehaviour
    {
        public const int FramePixelWidth = 114;
        public const int FramePixelHeight = 53;
        public const int InnerPixelX = 12;
        public const int InnerPixelY = 13;
        public const int InnerPixelWidth = 90;
        public const int InnerPixelHeight = 26;
        public const int VisibleFillPixelHeight = 24;
        private const int SourceFillPixelWidth = 64;

        private IDefensiveBuildingHost host;
        private Transform bar;
        private SpriteRenderer fill;
        private Texture2D frameTexture;
        private Texture2D fillTexture;
        private Sprite frameSprite;
        private Sprite croppedFill;
        private int visibleFillPixels = -1;
        [SerializeField] private Color32 friendlyFillColor = new(74, 201, 86, 255);
        [SerializeField] private Color32 enemyFillColor = new(255, 62, 62, 255);

        public bool IsConfigured => host != null && bar != null;
        public Color32 AppliedFillColor => fill != null ? (Color32)fill.color : default;

        public static int CalculateFillPixelWidth(float normalizedHP)
            => Mathf.Clamp(Mathf.CeilToInt(InnerPixelWidth
                * Mathf.Clamp01(normalizedHP)), 0, InnerPixelWidth);

        public void Configure(IDefensiveBuildingHost owner, float localY)
        {
            if (host != null)
            {
                host.DefenseHostHealthChanged -= Refresh;
                host.DefenseHostDestroyed -= Hide;
            }
            host = owner;
            BuildingPresentationCatalog catalog = BuildingPresentationCatalog.Load();
            if (host == null || catalog == null
                || !CreateAssets(catalog.BigBarLeft, catalog.BigBarMiddle,
                    catalog.BigBarRight, catalog.BigBarFill))
            {
                Debug.LogError("Phase 6B.2 large Tiny Swords Life Bar assets are unavailable.", this);
                return;
            }

            var root = new GameObject("BuildingLargeLifeBar");
            bar = root.transform;
            bar.SetParent(owner.HostTransform, false);
            bar.localPosition = new Vector3(0f, localY, -0.1f);
            bar.localScale = new Vector3(
                BuildingPresentationProfiles.For(owner.Type).LifeBarWidthScale, 1f, 1f);
            MakeSprite("StaticFrame", frameSprite, Vector2.zero, 32);
            fill = MakeSprite("ContinuousFill", null,
                new Vector2((InnerPixelX - FramePixelWidth * 0.5f) / 100f,
                    (InnerPixelY + InnerPixelHeight * 0.5f
                        - FramePixelHeight * 0.5f) / 100f), 33);
            ApplyFactionColor();
            host.DefenseHostHealthChanged += Refresh;
            host.DefenseHostDestroyed += Hide;
            Refresh();
        }

        private void OnDestroy()
        {
            if (host != null)
            {
                host.DefenseHostHealthChanged -= Refresh;
                host.DefenseHostDestroyed -= Hide;
            }
            if (croppedFill != null) Destroy(croppedFill);
            if (frameSprite != null) Destroy(frameSprite);
            if (fillTexture != null) Destroy(fillTexture);
            if (frameTexture != null) Destroy(frameTexture);
        }

        private void OnEnable() => PlayerViewRelation.Changed += OnPlayerViewFactionChanged;

        private void OnDisable() => PlayerViewRelation.Changed -= OnPlayerViewFactionChanged;

        private bool CreateAssets(Sprite left, Sprite middle, Sprite right, Sprite fillSource)
        {
            if (left == null || middle == null || right == null || fillSource == null
                || left.texture != middle.texture || left.texture != right.texture
                || !left.texture.isReadable || !fillSource.texture.isReadable
                || left.rect.width != 26 || middle.rect.width != 66
                || right.rect.width != 26 || left.rect.height != FramePixelHeight
                || middle.rect.height != FramePixelHeight
                || right.rect.height != FramePixelHeight
                || fillSource.rect.width != SourceFillPixelWidth
                || fillSource.rect.height != InnerPixelHeight)
                return false;

            Color32[] sourcePixels = left.texture.GetPixels32();
            var framePixels = new Color32[FramePixelWidth * FramePixelHeight];
            CopySlice(left, 0, 25, 0, sourcePixels, left.texture.width, framePixels);
            CopySlice(middle, 1, 64, 25, sourcePixels, left.texture.width, framePixels);
            CopySlice(right, 1, 25, 89, sourcePixels, left.texture.width, framePixels);
            frameTexture = NewTexture(FramePixelWidth, FramePixelHeight, framePixels);
            frameSprite = Sprite.Create(frameTexture,
                new Rect(0, 0, FramePixelWidth, FramePixelHeight),
                new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);

            // The source is a 64 px repeatable pixel pattern. The assembled frame's
            // measured inner container is 90 px, so repeat the source horizontally
            // instead of treating its atlas width as the runtime capacity.
            Color32[] fillPixels = new Color32[InnerPixelWidth * InnerPixelHeight];
            Color32[] fillAtlas = fillSource.texture.GetPixels32();
            int sourceX = Mathf.RoundToInt(fillSource.rect.x);
            int sourceY = Mathf.RoundToInt(fillSource.rect.y);
            byte maximumValue = 1;
            for (int y = 0; y < InnerPixelHeight; y++)
            for (int x = 0; x < SourceFillPixelWidth; x++)
            {
                Color32 source = fillAtlas[(sourceY + y) * fillSource.texture.width
                    + sourceX + x];
                maximumValue = System.Math.Max(maximumValue,
                    System.Math.Max(source.r, System.Math.Max(source.g, source.b)));
            }
            for (int y = 0; y < InnerPixelHeight; y++)
            for (int x = 0; x < InnerPixelWidth; x++)
            {
                Color32 source = fillAtlas[
                    (sourceY + y) * fillSource.texture.width
                    + sourceX + x % SourceFillPixelWidth];
                byte value = (byte)Mathf.RoundToInt(255f
                    * System.Math.Max(source.r, System.Math.Max(source.g, source.b))
                    / maximumValue);
                fillPixels[y * InnerPixelWidth + x] =
                    new Color32(value, value, value, source.a);
            }
            fillTexture = NewTexture(InnerPixelWidth, InnerPixelHeight, fillPixels);
            return true;
        }

        private static Texture2D NewTexture(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        private static void CopySlice(Sprite sprite, int inset, int width, int destinationX,
            Color32[] source, int sourceWidth, Color32[] destination)
        {
            int sourceX = Mathf.RoundToInt(sprite.rect.x) + inset;
            int sourceY = Mathf.RoundToInt(sprite.rect.y);
            for (int y = 0; y < FramePixelHeight; y++)
            for (int x = 0; x < width; x++)
                destination[y * FramePixelWidth + destinationX + x] =
                    source[(sourceY + y) * sourceWidth + sourceX + x];
        }

        private SpriteRenderer MakeSprite(string name, Sprite sprite, Vector2 position, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(bar, false);
            obj.transform.localPosition = new Vector3(position.x, position.y, 0f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void Refresh()
        {
            if (bar == null || host == null) return;
            bool visible = host.IsAlive && host.IsDefenseReady && host.CurrentHP < host.MaxHP;
            bar.gameObject.SetActive(visible);
            if (!visible) return;
            float ratio = Mathf.Clamp01(host.CurrentHP / host.MaxHP);
            int pixels = CalculateFillPixelWidth(ratio);
            if (pixels == visibleFillPixels) return;
            if (croppedFill != null) Destroy(croppedFill);
            croppedFill = pixels > 0
                ? Sprite.Create(fillTexture, new Rect(0, 0, pixels, InnerPixelHeight),
                    new Vector2(0f, 0.5f), 100f, 0, SpriteMeshType.FullRect)
                : null;
            fill.sprite = croppedFill;
            visibleFillPixels = pixels;
        }

        private void Hide()
        {
            if (bar != null) bar.gameObject.SetActive(false);
        }

        private void OnPlayerViewFactionChanged(Faction _) => ApplyFactionColor();

        private void ApplyFactionColor()
        {
            if (fill == null || host == null) return;
            fill.color = PlayerViewRelation.IsFriendlyToView(host.Faction)
                ? friendlyFillColor : enemyFillColor;
        }
    }
}
