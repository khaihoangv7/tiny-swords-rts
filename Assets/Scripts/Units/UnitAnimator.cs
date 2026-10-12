using UnityEngine;

namespace TinyKingdom.Units
{
    public static class UnitVisualPresentation
    {
        public const float Scale = 1.6f;

        public static void ApplyAlignedScale(SpriteRenderer renderer, Vector2 alignedOffset)
            => ApplyAlignedScale(renderer, alignedOffset, Scale);

        public static void ApplyAlignedScale(SpriteRenderer renderer, Vector2 alignedOffset,
            float presentationScale)
        {
            if (renderer == null) return;
            float scale = Mathf.Max(0.01f, presentationScale);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.transform.localPosition = new Vector3(
                alignedOffset.x * scale, alignedOffset.y * scale, 0f);
        }
    }

    [RequireComponent(typeof(UnitController), typeof(SpriteRenderer))]
    public sealed class UnitAnimator : MonoBehaviour
    {
        [SerializeField] private UnitAnimationSet animationSet;

        private UnitController controller;
        private CombatController combat;
        private UnitMovement movement;
        private SpriteRenderer original;
        private SpriteRenderer visual;
        private Sprite[] frames;
        private float elapsed;
        private int frame;
        private bool attackPlaying;
        private bool flipAttack;
        private bool facingLeft;
        private const float FacingEpsilon = 0.01f;
        public SpriteRenderer VisualRenderer => visual;
        public UnitAnimationSet AnimationSet => animationSet;
        public bool IsAttackPresentationConnected => combat == null
            || combat.HasAttackPresentationListener;

        private void Awake()
        {
            controller = GetComponent<UnitController>();
            combat = GetComponent<CombatController>();
            movement = GetComponent<UnitMovement>();
            original = GetComponent<SpriteRenderer>();
            var child = new GameObject("AnimatedVisual");
            child.transform.SetParent(transform, false);
            visual = child.AddComponent<SpriteRenderer>();
            visual.sharedMaterial = original.sharedMaterial;
            visual.sortingLayerID = original.sortingLayerID;
            visual.sortingOrder = original.sortingOrder;
            visual.color = original.color;
            original.enabled = false;
            controller.StateChanged += HandleState;
            if (combat != null) combat.AttackPerformed += PlayAttack;
            HandleState(controller.CurrentState);
        }

        private void OnDestroy()
        {
            if (controller != null) controller.StateChanged -= HandleState;
            if (combat != null) combat.AttackPerformed -= PlayAttack;
        }

        private void Update()
        {
            UpdateFacing();
            if (animationSet == null || frames == null || frames.Length == 0 || !visual.enabled)
                return;
            elapsed += Time.deltaTime;
            float duration = 1f / (attackPlaying
                ? animationSet.AttackFramesPerSecond : animationSet.FramesPerSecond);
            while (elapsed >= duration)
            {
                elapsed -= duration;
                if (frame + 1 < frames.Length)
                    frame++;
                else if (attackPlaying)
                {
                    attackPlaying = false;
                    ShowStateVisual();
                    return;
                }
                else frame = 0;
                ShowFrame();
            }
        }

        public void Configure(UnitAnimationSet set)
        {
            animationSet = set;
            if (controller != null) HandleState(controller.CurrentState);
        }

        private void PlayAttack(Vector2 direction)
        {
            if (animationSet == null || controller.CurrentState == UnitState.Dead) return;
            Sprite[] chosen = animationSet.AttackFor(direction);
            if (chosen == null || chosen.Length == 0) return;
            attackPlaying = true;
            flipAttack = direction.x < 0f;
            if (Mathf.Abs(direction.x) > FacingEpsilon)
                facingLeft = direction.x < 0f;
            Begin(chosen);
        }

        public void PlayAction(Vector2 direction) => PlayAttack(direction);

        public void PlaySequence(Sprite[] sequence)
        {
            if (sequence == null || sequence.Length == 0 || !controller.IsAlive) return;
            attackPlaying = true;
            flipAttack = facingLeft;
            Begin(sequence);
        }

        private void HandleState(UnitState state)
        {
            if (visual == null) return;
            if (state == UnitState.Dead)
            {
                attackPlaying = false;
                visual.enabled = false;
                return;
            }
            if (state != UnitState.Attacking) attackPlaying = false;
            if (!attackPlaying) ShowStateVisual();
        }

        private void ShowStateVisual()
        {
            if (animationSet == null || visual == null) return;
            Sprite[] chosen = controller.CurrentState switch
            {
                UnitState.Moving or UnitState.Chasing => animationSet.Run,
                UnitState.Guarding => animationSet.Guard,
                _ => animationSet.Idle
            };
            Begin(chosen);
        }

        private void UpdateFacing()
        {
            if (visual == null || controller == null || !controller.IsAlive) return;
            Vector2 direction = movement != null ? movement.LastMovementDirection : Vector2.zero;
            if (controller.CurrentState == UnitState.Attacking && combat != null)
            {
                if (combat.Target != null)
                    direction = combat.Target.transform.position - transform.position;
                else if (combat.BuildingTarget != null)
                    direction = combat.BuildingTarget.AttackPresentationPoint(
                        transform.position) - transform.position;
            }
            if (Mathf.Abs(direction.x) <= FacingEpsilon) return;
            bool next = direction.x < 0f;
            if (next == facingLeft) return;
            facingLeft = next;
            if (!attackPlaying) ShowFrame();
        }

        private void Begin(Sprite[] chosen)
        {
            frames = chosen;
            frame = 0;
            elapsed = 0f;
            ShowFrame();
        }

        private void ShowFrame()
        {
            if (frames == null || frame >= frames.Length || frames[frame] == null) return;
            Sprite sprite = frames[frame];
            visual.sprite = sprite;
            bool flip = attackPlaying ? flipAttack : facingLeft;
            visual.flipX = flip;
            // Existing sheets use tight automatic rectangles. Align each sprite to
            // the same point within its source cell without moving the gameplay root.
            float x = sprite.rect.x % animationSet.FrameCellSize + sprite.pivot.x;
            float y = sprite.rect.y % animationSet.FrameCellSize + sprite.pivot.y;
            Vector2 offset = (new Vector2(x, y) - animationSet.VisualAnchorPixels)
                / sprite.pixelsPerUnit;
            if (flip) offset.x = -offset.x;
            UnitVisualPresentation.ApplyAlignedScale(visual, offset, animationSet.VisualScale);
        }
    }
}
