using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.UI
{
    // A stationary presentation actor. It never joins unit/combat/economy registries.
    public sealed class MenuBackdropActor : MonoBehaviour
    {
        private UnitAnimationSet data;
        private SpriteRenderer visual;
        private float phase;
        private int displayed = -1;

        public UnitState State => UnitState.Idle;
        public SpriteRenderer Visual => visual;

        public void Configure(UnitAnimationSet animation, int sortingOrder, float offset, bool flip)
        {
            data = animation;
            phase = offset;
            var body = new GameObject("Idle Visual");
            body.transform.SetParent(transform, false);
            visual = body.AddComponent<SpriteRenderer>();
            visual.sortingOrder = sortingOrder;
            visual.flipX = flip;
            ApplyFrame(0);
        }

        private void Update()
        {
            if (data == null || data.Idle.Length == 0) return;
            int frame = Mathf.FloorToInt((Time.unscaledTime + phase) * data.FramesPerSecond)
                % data.Idle.Length;
            if (frame != displayed) ApplyFrame(frame);
        }

        private void ApplyFrame(int frame)
        {
            if (data == null || data.Idle.Length == 0 || visual == null) return;
            Sprite sprite = data.Idle[frame];
            if (sprite == null) return;
            visual.sprite = sprite;
            displayed = frame;
            Vector2 pivot = new(sprite.rect.x % data.FrameCellSize + sprite.pivot.x,
                sprite.rect.y % data.FrameCellSize + sprite.pivot.y);
            Vector2 offset = (pivot - data.VisualAnchorPixels) / sprite.pixelsPerUnit;
            if (visual.flipX) offset.x = -offset.x;
            UnitVisualPresentation.ApplyAlignedScale(visual, offset, data.VisualScale);
        }
    }
}
