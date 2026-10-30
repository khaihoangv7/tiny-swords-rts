using UnityEngine;

namespace TinyKingdom.Units
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/Unit Animation Set")]
    public sealed class UnitAnimationSet : ScriptableObject
    {
        [SerializeField, Min(1f)] private float framesPerSecond = 10f;
        [SerializeField, Min(1f)] private float attackFramesPerSecond = 10f;
        [SerializeField, Min(1)] private int frameCellSize = 192;
        [SerializeField] private Vector2 visualAnchorPixels = new(96f, 96f);
        [SerializeField, Min(0.1f)] private float visualScale = UnitVisualPresentation.Scale;
        [SerializeField] private Sprite[] idle = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] run = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attack = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attackAlternate = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] guard = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attackDown = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attackDownRight = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attackUp = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attackUpRight = System.Array.Empty<Sprite>();

        public float FramesPerSecond => framesPerSecond;
        public float AttackFramesPerSecond => attackFramesPerSecond;
        public int FrameCellSize => frameCellSize;
        public Vector2 VisualAnchorPixels => visualAnchorPixels;
        public float VisualScale => visualScale;
        public Sprite[] Idle => idle;
        public Sprite[] Run => run;
        public Sprite[] Attack => attack;
        public Sprite[] AttackAlternate => attackAlternate;
        public Sprite[] Guard => guard;
        public Sprite[] AttackDown => attackDown;
        public Sprite[] AttackDownRight => attackDownRight;
        public Sprite[] AttackUp => attackUp;
        public Sprite[] AttackUpRight => attackUpRight;

        public Sprite[] AttackFor(Vector2 direction)
        {
            if (attackDown == null || attackDown.Length == 0)
                return attack;
            float x = Mathf.Abs(direction.x);
            float y = direction.y;
            if (y < -x * 2f) return attackDown;
            if (y > x * 2f) return attackUp;
            if (y < -x * 0.5f) return attackDownRight;
            if (y > x * 0.5f) return attackUpRight;
            return attack;
        }
    }
}
