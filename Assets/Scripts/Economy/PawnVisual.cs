using TinyKingdom.Units;
using TinyKingdom.Buildings;
using UnityEngine;

namespace TinyKingdom.Economy
{
    [RequireComponent(typeof(UnitController), typeof(SpriteRenderer))]
    public sealed class PawnVisual : MonoBehaviour
    {
        [SerializeField] private PawnAnimationSet animationSet;
        private SpriteRenderer visual;
        private UnitController controller;
        private UnitMovement movement;
        private PawnGatherer gatherer;
        private CombatController combat;
        private PawnBuilder builder;
        private Sprite[] frames;
        private int frame;
        private float elapsed;
        private float attackRemaining;
        private float workVisualRemaining;
        private float buildVisualRemaining;
        private bool loop;
        private bool facingLeft;

        private const float WorkAnimationDuration = 0.5f;
        private const float FacingEpsilon = 0.01f;

        public SpriteRenderer VisualRenderer => visual;

        public void Configure(PawnAnimationSet set)
        {
            animationSet = set;
            ChooseStateVisual(true);
        }

        private void Awake()
        {
            controller = GetComponent<UnitController>();
            movement = GetComponent<UnitMovement>();
            gatherer = GetComponent<PawnGatherer>();
            combat = GetComponent<CombatController>();
            builder = GetComponent<PawnBuilder>();
            SpriteRenderer source = GetComponent<SpriteRenderer>();
            var child = new GameObject("AnimatedVisual");
            child.transform.SetParent(transform, false);
            visual = child.AddComponent<SpriteRenderer>();
            visual.sharedMaterial = source.sharedMaterial;
            visual.sortingLayerID = source.sortingLayerID;
            visual.sortingOrder = source.sortingOrder;
            visual.color = source.color;
            source.enabled = false;
            if (gatherer != null) gatherer.WorkCycleStarted += OnWorkCycleStarted;
            if (combat != null) combat.AttackPerformed += OnAttackPerformed;
            if (builder != null) builder.WorkCycleStarted += OnBuildCycleStarted;
        }

        private void OnDestroy()
        {
            if (gatherer != null) gatherer.WorkCycleStarted -= OnWorkCycleStarted;
            if (combat != null) combat.AttackPerformed -= OnAttackPerformed;
            if (builder != null) builder.WorkCycleStarted -= OnBuildCycleStarted;
        }

        private void Update()
        {
            if (visual == null || animationSet == null || controller == null) return;
            if (!controller.IsAlive) { visual.enabled = false; return; }
            UpdateFacing();
            if (gatherer == null || gatherer.State != WorkerState.Gathering)
                workVisualRemaining = 0f;
            else if (workVisualRemaining > 0f)
                workVisualRemaining = Mathf.Max(0f, workVisualRemaining - Time.deltaTime);
            if (builder == null || !builder.IsWorking) buildVisualRemaining = 0f;
            else if (buildVisualRemaining > 0f)
                buildVisualRemaining = Mathf.Max(0f, buildVisualRemaining - Time.deltaTime);
            if (attackRemaining > 0f)
            {
                attackRemaining -= Time.deltaTime;
                if (attackRemaining <= 0f) ChooseStateVisual(true);
            }
            if (attackRemaining <= 0f) ChooseStateVisual(false);
            if (frames == null || frames.Length <= 1) return;
            elapsed += Time.deltaTime;
            float frameDuration = workVisualRemaining > 0f
                || buildVisualRemaining > 0f ? WorkAnimationDuration / frames.Length : 0.1f;
            while (elapsed >= frameDuration)
            {
                elapsed -= frameDuration;
                if (frame + 1 < frames.Length) frame++;
                else if (loop) frame = 0;
                ShowFrame();
            }
        }

        private void OnWorkCycleStarted(ResourceType type)
        {
            if (animationSet == null || attackRemaining > 0f) return;
            Sprite[] work = type switch
            {
                ResourceType.Wood => animationSet.Chop,
                ResourceType.Gold => animationSet.Mine,
                _ => animationSet.Knife
            };
            workVisualRemaining = WorkAnimationDuration;
            FaceTowards(gatherer?.Target != null
                ? gatherer.Target.transform.position - transform.position : Vector2.zero);
            Begin(work, false);
        }

        private void OnAttackPerformed(Vector2 direction)
        {
            if (animationSet == null || !controller.IsAlive) return;
            workVisualRemaining = 0f;
            attackRemaining = Mathf.Max(0.1f, animationSet.Knife.Length * 0.1f);
            FaceTowards(direction);
            Begin(animationSet.Knife, false);
        }

        private void OnBuildCycleStarted()
        {
            if (animationSet == null || attackRemaining > 0f
                || animationSet.Hammer.Length == 0) return;
            buildVisualRemaining = WorkAnimationDuration;
            FaceTowards(builder?.Target != null
                ? builder.Target.transform.position - transform.position : Vector2.zero);
            Begin(animationSet.Hammer, false);
        }

        private void ChooseStateVisual(bool force)
        {
            if (animationSet == null || visual == null || attackRemaining > 0f) return;
            Sprite[] chosen;
            bool moving = movement != null && movement.IsMoving;
            if (builder != null && builder.HasJob && animationSet.Hammer.Length > 0)
            {
                chosen = moving && animationSet.RunHammer.Length > 0
                    ? animationSet.RunHammer
                    : builder.IsWorking && buildVisualRemaining > 0f
                        ? animationSet.Hammer
                        : animationSet.IdleHammer.Length > 0
                            ? animationSet.IdleHammer : animationSet.Idle;
            }
            else if (gatherer != null && gatherer.State == WorkerState.Gathering)
            {
                chosen = (gatherer.Target?.ResourceType, workVisualRemaining > 0f) switch
                {
                    (ResourceType.Wood, true) => animationSet.Chop,
                    (ResourceType.Wood, false) => animationSet.IdleAxe,
                    (ResourceType.Gold, true) => animationSet.Mine,
                    (ResourceType.Gold, false) => animationSet.IdlePickaxe,
                    (_, true) => animationSet.Knife,
                    _ => animationSet.IdleKnife
                };
            }
            else if (gatherer != null && gatherer.CarriedAmount > 0)
            {
                chosen = (gatherer.CarriedResourceType, moving) switch
                {
                    (ResourceType.Wood, true) => animationSet.RunWood,
                    (ResourceType.Wood, false) => animationSet.IdleWood,
                    (ResourceType.Gold, true) => animationSet.RunGold,
                    (ResourceType.Gold, false) => animationSet.IdleGold,
                    (ResourceType.Food, true) => animationSet.RunFood,
                    _ => animationSet.IdleFood
                };
            }
            else chosen = moving ? animationSet.Run : animationSet.Idle;
            if (force || !ReferenceEquals(frames, chosen)) Begin(chosen, true);
        }

        private void Begin(Sprite[] chosen, bool repeat)
        {
            frames = chosen;
            frame = 0;
            elapsed = 0f;
            loop = repeat;
            ShowFrame();
        }

        private void UpdateFacing()
        {
            Vector2 direction = movement != null ? movement.LastMovementDirection : Vector2.zero;
            if (builder != null && builder.IsWorking && builder.Target != null)
                direction = builder.Target.transform.position - transform.position;
            else if (gatherer != null && gatherer.State == WorkerState.Gathering
                && gatherer.Target != null)
                direction = gatherer.Target.transform.position - transform.position;
            else if (controller.CurrentState == UnitState.Attacking && combat != null)
            {
                if (combat.Target != null)
                    direction = combat.Target.transform.position - transform.position;
                else if (combat.BuildingTarget != null)
                    direction = combat.BuildingTarget.TargetTransform.position - transform.position;
            }
            FaceTowards(direction);
        }

        private void FaceTowards(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) <= FacingEpsilon) return;
            bool next = direction.x < 0f;
            if (next == facingLeft) return;
            facingLeft = next;
            ShowFrame();
        }

        private void ShowFrame()
        {
            if (visual == null || frames == null || frame >= frames.Length
                || frames[frame] == null) return;
            Sprite sprite = frames[frame];
            visual.sprite = sprite;
            visual.flipX = facingLeft;
            float x = sprite.rect.x % 192f + sprite.pivot.x;
            float y = sprite.rect.y + sprite.pivot.y;
            Vector2 offset = (new Vector2(x, y) - new Vector2(96f, 96f)) / sprite.pixelsPerUnit;
            if (facingLeft) offset.x = -offset.x;
            visual.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
        }
    }
}
