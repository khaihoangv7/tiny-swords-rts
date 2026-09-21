using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController), typeof(SpriteRenderer))]
    public sealed class UnitAnimator : MonoBehaviour
    {
        [SerializeField] private UnitAnimationSet animationSet;

        private UnitController controller;
        private CombatController combat;
        private SpriteRenderer original;
        private SpriteRenderer visual;
        private Sprite[] frames;
        private float elapsed;
        private int frame;
        private bool attackPlaying;
        private bool flipAttack;

        private void Awake()
        {
            controller = GetComponent<UnitController>();
            combat = GetComponent<CombatController>();
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
            if (animationSet == null || frames == null || frames.Length == 0 || !visual.enabled)
                return;
            elapsed += Time.deltaTime;
            float duration = 1f / animationSet.FramesPerSecond;
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
            Begin(chosen);
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
            flipAttack = false;
            Begin(chosen);
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
            visual.flipX = flipAttack;
            // Existing sheets use tight automatic rectangles. Align each sprite to
            // the same point within its source cell without moving the gameplay root.
            float x = sprite.rect.x % animationSet.FrameCellSize + sprite.pivot.x;
            float y = sprite.rect.y + sprite.pivot.y;
            Vector2 offset = (new Vector2(x, y) - animationSet.VisualAnchorPixels)
                / sprite.pixelsPerUnit;
            if (flipAttack) offset.x = -offset.x;
            visual.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
        }
    }
}
