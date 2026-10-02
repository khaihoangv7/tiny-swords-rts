using UnityEngine;

namespace TinyKingdom.Terrain
{
    // Cosmetic world prop. The root remains its ground contact; only the visual child animates.
    [DisallowMultipleComponent]
    public sealed class BushPresentation : MonoBehaviour
    {
        public const float LargeScale = 1.50f;
        public const float SmallScale = 1.25f;
        public const float FramesPerSecond = 10f;

        [SerializeField, Range(0, 3)] private int variant;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private AmbientSpriteLoop loop;

        public int Variant => variant;
        public bool IsLarge => variant == 0 || variant == 2;
        public float AppliedScale => IsLarge ? LargeScale : SmallScale;
        public SpriteRenderer Visual => visual;
        public int FrameCount => loop != null ? loop.FrameCount : 0;
        public float PhaseSeconds => loop != null ? loop.PhaseSeconds : 0f;

        public static BushPresentation Ensure(GameObject root, SpriteRenderer source,
            Sprite[] frames, int bushVariant, float phaseSeconds)
        {
            if (root == null || source == null || frames == null || frames.Length == 0) return null;
            BushPresentation presentation = root.GetComponent<BushPresentation>();
            if (presentation == null) presentation = root.AddComponent<BushPresentation>();
            presentation.Configure(source, frames, bushVariant, phaseSeconds);
            return presentation;
        }

        public void Configure(SpriteRenderer source, Sprite[] frames, int bushVariant,
            float phaseSeconds)
        {
            variant = Mathf.Clamp(bushVariant, 0, 3);
            if (visual == null)
            {
                if (source.transform == transform)
                {
                    GameObject child = new("Visual");
                    child.transform.SetParent(transform, false);
                    visual = child.AddComponent<SpriteRenderer>();
                    CopyRenderer(source, visual);
                    source.enabled = false;
                }
                else visual = source;
            }

            visual.sprite = frames[0];
            float scale = AppliedScale;
            visual.transform.localScale = Vector3.one * scale;
            loop = visual.GetComponent<AmbientSpriteLoop>();
            if (loop == null) loop = visual.gameObject.AddComponent<AmbientSpriteLoop>();
            loop.Configure(frames, phaseSeconds, FramesPerSecond, true, scale, 128);
        }

        private static void CopyRenderer(SpriteRenderer from, SpriteRenderer to)
        {
            to.color = from.color;
            to.flipX = from.flipX;
            to.flipY = from.flipY;
            to.sharedMaterial = from.sharedMaterial;
            to.maskInteraction = from.maskInteraction;
            to.sortingLayerID = from.sortingLayerID;
            to.sortingOrder = from.sortingOrder;
        }
    }
}
