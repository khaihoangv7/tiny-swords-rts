using System.Collections.Generic;
using TinyKingdom.Audio;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Presentation;
using TinyKingdom.UI;
using TinyKingdom.Vision;
using UnityEngine;

namespace TinyKingdom.Units
{
    public enum PawnScoutEligibility
    {
        Eligible,
        InvalidPawn,
        WrongFaction,
        MatchNotPlaying,
        AlreadyPromoted,
        ActiveScoutExists,
        InsufficientWood,
        MissingPresentation
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitController), typeof(HealthComponent))]
    public sealed class PawnScoutPromotion : MonoBehaviour, IUnitVisionModifier
    {
        public const int WoodCost = 50;
        public const float ScoutVisionMultiplier = 2.5f;
        public const float FireAudioVolumeMultiplier = 0.35f;
        public const float FireFramesPerSecond = 10f;
        public const float FireVisualScale = 0.7f;
        public static readonly Vector3 FireLocalPosition = new(0f, 1.00f, -0.05f);

        private static readonly List<PawnScoutPromotion> ActivePromotions = new();
        private static RtsHudAssetCatalog sharedPresentation;
        private UnitController controller;
        private HealthComponent health;
        private PawnScoutFirePlayer firePlayer;
        [SerializeField] private bool promoted;
        [SerializeField] private Sprite[] configuredFrames = System.Array.Empty<Sprite>();
        private float nextAudioRefresh;

        public bool IsPromoted => promoted && controller != null && controller.IsAlive;
        public float VisionMultiplier => IsPromoted ? ScoutVisionMultiplier : 1f;
        public UnitController Controller => controller;
        public PawnScoutFirePlayer FirePlayer => firePlayer;

        private void Awake()
        {
            controller = GetComponent<UnitController>();
            health = GetComponent<HealthComponent>();
        }

        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<UnitController>();
            if (health == null) health = GetComponent<HealthComponent>();
            if (health != null) health.Died += HandleDeath;
            if (promoted)
            {
                if (HasValidFrames(configuredFrames)) EnsureVisual(configuredFrames);
                Register();
                nextAudioRefresh = 0f;
            }
            controller?.RefreshVisionModifier();
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= HandleDeath;
            Unregister();
            StopAudio();
        }

        private void OnDestroy()
        {
            Unregister();
            StopAudio();
        }

        private void Update()
        {
            if (!IsPromoted) return;
            bool presented = controller.IsWorldParticipant;
            if (firePlayer != null && firePlayer.gameObject.activeSelf != presented)
                firePlayer.gameObject.SetActive(presented);
            if (!presented)
            {
                if (GameplayAudioService.IsLoopActive(this,
                        GameplayAudioEvent.BuildingFire)) StopAudio();
                return;
            }
            if (Time.unscaledTime < nextAudioRefresh) return;
            nextAudioRefresh = Time.unscaledTime + 0.25f;
            bool audible = MatchManager.AllowsGameplay
                && GameplayAudioService.CanHearWorld(transform.position, controller.Faction);
            if (audible)
                GameplayAudioService.StartLoop(this, GameplayAudioEvent.BuildingFire,
                    transform.position, controller.Faction, FireAudioVolumeMultiplier);
            else StopAudio();
        }

        public static PawnScoutPromotion ActiveFor(Faction faction)
        {
            for (int i = ActivePromotions.Count - 1; i >= 0; i--)
            {
                PawnScoutPromotion item = ActivePromotions[i];
                if (item == null || !item.IsPromoted)
                {
                    ActivePromotions.RemoveAt(i);
                    continue;
                }
                if (item.controller.Faction == faction) return item;
            }
            return null;
        }

        public static PawnScoutEligibility Evaluate(UnitController pawn,
            Faction humanFaction, ResourceStockpile stockpile, Sprite[] fireFrames)
        {
            if (MatchManager.Instance != null
                && MatchManager.Instance.HumanFaction != humanFaction)
                return PawnScoutEligibility.WrongFaction;
            return EvaluateForFaction(pawn, humanFaction, stockpile, fireFrames);
        }

        public static PawnScoutEligibility EvaluateForFaction(UnitController pawn,
            Faction ownerFaction, ResourceStockpile stockpile, Sprite[] fireFrames)
        {
            HealthComponent health = pawn != null ? pawn.GetComponent<HealthComponent>() : null;
            if (pawn == null || !pawn.IsAlive || !pawn.IsWorldParticipant || health == null
                || health.CombatData == null || health.CombatData.UnitType != UnitType.Pawn)
                return PawnScoutEligibility.InvalidPawn;
            if (pawn.Faction != ownerFaction || stockpile == null
                || stockpile.Faction != ownerFaction)
                return PawnScoutEligibility.WrongFaction;
            if (!MatchManager.AllowsGameplay)
                return PawnScoutEligibility.MatchNotPlaying;
            PawnScoutPromotion existing = pawn.GetComponent<PawnScoutPromotion>();
            if (existing != null && existing.IsPromoted)
                return PawnScoutEligibility.AlreadyPromoted;
            if (ActiveFor(ownerFaction) != null)
                return PawnScoutEligibility.ActiveScoutExists;
            if (!stockpile.CanAfford(0, WoodCost, 0))
                return PawnScoutEligibility.InsufficientWood;
            if (!HasValidFrames(fireFrames))
                return PawnScoutEligibility.MissingPresentation;
            return PawnScoutEligibility.Eligible;
        }

        public static PawnScoutEligibility EvaluateForFaction(UnitController pawn,
            Faction ownerFaction, ResourceStockpile stockpile)
        {
            sharedPresentation ??= RtsHudAssetCatalog.Load();
            return EvaluateForFaction(pawn, ownerFaction, stockpile,
                sharedPresentation != null ? sharedPresentation.ScoutFireFrames : null);
        }

        public static bool TryPromote(UnitController pawn, Faction humanFaction,
            ResourceStockpile stockpile, Sprite[] fireFrames)
        {
            if (Evaluate(pawn, humanFaction, stockpile, fireFrames)
                != PawnScoutEligibility.Eligible)
                return false;

            return PromoteValidated(pawn, stockpile, fireFrames);
        }

        public static bool TryPromoteForFaction(UnitController pawn, Faction ownerFaction,
            ResourceStockpile stockpile, Sprite[] fireFrames)
        {
            if (EvaluateForFaction(pawn, ownerFaction, stockpile, fireFrames)
                != PawnScoutEligibility.Eligible)
                return false;

            return PromoteValidated(pawn, stockpile, fireFrames);
        }

        public static bool TryPromoteForFaction(UnitController pawn, Faction ownerFaction,
            ResourceStockpile stockpile)
        {
            sharedPresentation ??= RtsHudAssetCatalog.Load();
            return TryPromoteForFaction(pawn, ownerFaction, stockpile,
                sharedPresentation != null ? sharedPresentation.ScoutFireFrames : null);
        }

        private static bool PromoteValidated(UnitController pawn,
            ResourceStockpile stockpile, Sprite[] fireFrames)
        {
            if (!stockpile.Spend(0, WoodCost, 0)) return false;

            PawnScoutPromotion promotion = pawn.GetComponent<PawnScoutPromotion>();
            if (promotion == null)
                promotion = pawn.gameObject.AddComponent<PawnScoutPromotion>();
            if (promotion.Activate(fireFrames)) return true;

            stockpile.Add(ResourceType.Wood, WoodCost);
            return false;
        }

        public static string DisabledReason(PawnScoutEligibility eligibility)
            => eligibility switch
            {
                PawnScoutEligibility.ActiveScoutExists => "Only one Scout may be active.",
                PawnScoutEligibility.AlreadyPromoted => "This Pawn is already the active Scout.",
                PawnScoutEligibility.InsufficientWood => "Requires 50 Wood.",
                PawnScoutEligibility.MatchNotPlaying => "Available only while the match is playing.",
                PawnScoutEligibility.WrongFaction => "Only a human-controlled friendly Pawn may become a Scout.",
                PawnScoutEligibility.MissingPresentation => "Scout fire presentation is unavailable.",
                _ => "Select a living friendly Pawn."
            };

        public static string TooltipDetails(PawnScoutEligibility eligibility)
            => eligibility switch
            {
                PawnScoutEligibility.Eligible =>
                    "Cost: 50 Wood\nVision: ×2.5\nMaximum active: 1",
                PawnScoutEligibility.ActiveScoutExists =>
                    "Cost: 50 Wood\nVision: ×2.5\nMaximum active: 1\nOnly one Scout may be active.",
                PawnScoutEligibility.AlreadyPromoted =>
                    "Cost: 50 Wood\nVision: ×2.5\nMaximum active: 1\nThis Pawn is already the active Scout.",
                PawnScoutEligibility.InsufficientWood =>
                    "Cost: 50 Wood\nVision: ×2.5\nMaximum active: 1\nRequires 50 Wood.",
                PawnScoutEligibility.MatchNotPlaying =>
                    "Cost: 50 Wood\nVision: ×2.5\nMaximum active: 1\nAvailable only while the match is playing.",
                PawnScoutEligibility.WrongFaction =>
                    "Cost: 50 Wood\nVision: ×2.5\nMaximum active: 1\nOnly a human-controlled friendly Pawn may become a Scout.",
                PawnScoutEligibility.MissingPresentation =>
                    "Cost: 50 Wood\nVision: ×2.5\nMaximum active: 1\nScout fire presentation is unavailable.",
                _ => "Cost: 50 Wood\nVision: ×2.5\nMaximum active: 1\nSelect a living friendly Pawn."
            };

        private bool Activate(Sprite[] fireFrames)
        {
            // Edit Mode validation can add this component before Awake runs.
            controller ??= GetComponent<UnitController>();
            health ??= GetComponent<HealthComponent>();
            if (!HasValidFrames(fireFrames) || controller == null || !controller.IsAlive)
                return false;
            promoted = true;
            configuredFrames = fireFrames;
            EnsureVisual(configuredFrames);
            PlayPromotionFeedback();
            if (controller.Faction == PlayerViewRelation.PlayerViewFaction)
                GameplayAudioService.RequestUi(GameplayAudioEvent.ScoutPromotion);
            Register();
            controller.RefreshVisionModifier();
            TeamVisionSystem.Instance?.RecomputeNow();
            nextAudioRefresh = 0f;
            return true;
        }

        private void PlayPromotionFeedback()
        {
            HitSquashStretchPresentation reaction =
                GetComponent<HitSquashStretchPresentation>();
            if (reaction == null)
            {
                SpriteRenderer body = null;
                UnitAnimator animator = GetComponent<UnitAnimator>();
                if (animator != null) body = animator.VisualRenderer;
                TinyKingdom.Economy.PawnVisual pawnVisual =
                    GetComponent<TinyKingdom.Economy.PawnVisual>();
                if (body == null && pawnVisual != null)
                    body = pawnVisual.VisualRenderer;
                if (body == null) body = GetComponent<SpriteRenderer>();
                if (body == null) return;
                reaction = gameObject.AddComponent<HitSquashStretchPresentation>();
                reaction.Configure(body, health);
            }
            reaction.Trigger();
        }

        private void EnsureVisual(Sprite[] fireFrames)
        {
            Transform existing = transform.Find("PawnScoutFire");
            GameObject root;
            if (existing != null) root = existing.gameObject;
            else
            {
                root = new GameObject("PawnScoutFire");
                root.transform.SetParent(transform, false);
            }
            root.transform.localPosition = FireLocalPosition;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * FireVisualScale;
            firePlayer = root.GetComponent<PawnScoutFirePlayer>();
            if (firePlayer == null)
                firePlayer = root.AddComponent<PawnScoutFirePlayer>();
            SpriteRenderer pawnRenderer = GetComponent<SpriteRenderer>();
            firePlayer.Configure(fireFrames,
                pawnRenderer != null ? pawnRenderer.sortingOrder + 20 : 20);
            root.SetActive(true);
        }

        private void HandleDeath(HealthComponent _)
        {
            promoted = false;
            Unregister();
            StopAudio();
            if (firePlayer != null) firePlayer.gameObject.SetActive(false);
            controller?.RefreshVisionModifier();
        }

        private void Register()
        {
            if (!ActivePromotions.Contains(this)) ActivePromotions.Add(this);
        }

        private void Unregister() => ActivePromotions.Remove(this);

        private void StopAudio()
            => GameplayAudioService.StopLoop(this, GameplayAudioEvent.BuildingFire);

        private static bool HasValidFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length != 7) return false;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] == null) return false;
            return true;
        }
    }

    [DisallowMultipleComponent]
    public sealed class PawnScoutFirePlayer : MonoBehaviour
    {
        private Sprite[] frames;
        private SpriteRenderer visual;
        private float elapsed;

        public int FrameCount => frames?.Length ?? 0;
        public SpriteRenderer Visual => visual;

        public void Configure(Sprite[] source, int sortingOrder)
        {
            frames = source ?? System.Array.Empty<Sprite>();
            Transform frame = transform.Find("Frame");
            if (frame == null)
            {
                var frameObject = new GameObject("Frame");
                frameObject.transform.SetParent(transform, false);
                frame = frameObject.transform;
            }
            visual = frame.GetComponent<SpriteRenderer>();
            if (visual == null)
                visual = frame.gameObject.AddComponent<SpriteRenderer>();
            visual.sortingOrder = sortingOrder;
            elapsed = 0f;
            ApplyFrame(0);
        }

        private void Update()
        {
            if (visual == null || frames == null || frames.Length == 0) return;
            elapsed += Time.deltaTime;
            ApplyFrame(Mathf.FloorToInt(elapsed * PawnScoutPromotion.FireFramesPerSecond)
                % frames.Length);
        }

        private void ApplyFrame(int index)
        {
            if (visual == null || frames == null || frames.Length == 0) return;
            Sprite sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
            visual.sprite = sprite;
            visual.transform.localPosition = sprite != null
                ? -(Vector3)sprite.bounds.center : Vector3.zero;
        }
    }
}
