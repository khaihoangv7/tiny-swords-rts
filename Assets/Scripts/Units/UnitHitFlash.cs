using System.Collections;
using TinyKingdom.Economy;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(HealthComponent))]
    public sealed class UnitHitFlash : MonoBehaviour
    {
        private const float RedSeconds = 0.09f;
        private const float WhiteSeconds = 0.08f;
        private const float TotalSeconds = RedSeconds + WhiteSeconds;
        private HealthComponent health;
        private SpriteRenderer source;
        private SpriteRenderer overlay;
        private float remaining;

        private void OnEnable()
        {
            health = GetComponent<HealthComponent>();
            health.DamageReceived += OnDamage;
            health.Died += OnDeath;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.DamageReceived -= OnDamage;
                health.Died -= OnDeath;
            }
            if (overlay != null) overlay.enabled = false;
        }

        public void Configure(Material flashMaterial)
        {
            source = GetComponent<UnitAnimator>()?.VisualRenderer
                ?? GetComponent<PawnVisual>()?.VisualRenderer;
            if (source == null || flashMaterial == null)
            {
                Debug.LogError("UnitHitFlash requires an animated SpriteRenderer and flash material.", this);
                return;
            }
            var child = new GameObject("HitFlashOverlay");
            child.transform.SetParent(source.transform, false);
            overlay = child.AddComponent<SpriteRenderer>();
            overlay.sharedMaterial = flashMaterial;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder + 1;
            overlay.enabled = false;
        }

        private void Update()
        {
            if (remaining <= 0f || overlay == null) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f || !health.IsAlive)
            {
                overlay.enabled = false;
                return;
            }
            overlay.color = remaining > WhiteSeconds
                ? new Color(1f, 0.12f, 0.12f, 0.82f)
                : new Color(1f, 1f, 1f, 0.92f);
        }

        private void LateUpdate()
        {
            if (overlay == null || !overlay.enabled || source == null) return;
            overlay.sprite = source.sprite;
            overlay.flipX = source.flipX;
            overlay.flipY = source.flipY;
        }

        private void OnDamage(HealthComponent _, int loss, bool guarded)
        {
            if (loss <= 0 || overlay == null) return;
            remaining = TotalSeconds;
            overlay.color = new Color(1f, 0.12f, 0.12f, 0.82f);
            overlay.enabled = true;
        }

        private void OnDeath(HealthComponent _) { if (overlay != null) overlay.enabled = false; }
    }
}

namespace TinyKingdom.Presentation
{
    // Reusable presentation-only impact deformation. The wrapper is a child of the
    // gameplay root, so colliders, movement, selection, UI and shadows stay unchanged.
    [DisallowMultipleComponent]
    public sealed class HitSquashStretchPresentation : MonoBehaviour
    {
        public const float DefaultSquashScaleY = 0.84f;
        public const float DefaultStretchScaleY = 1.12f;
        public const float DefaultSquashDuration = 0.055f;
        public const float DefaultStretchDuration = 0.065f;
        public const float DefaultRecoverDuration = 0.08f;

        [SerializeField, Range(0.5f, 1f)] private float squashScaleY = DefaultSquashScaleY;
        [SerializeField, Range(1f, 1.5f)] private float stretchScaleY = DefaultStretchScaleY;
        [SerializeField, Min(0.01f)] private float squashDuration = DefaultSquashDuration;
        [SerializeField, Min(0.01f)] private float stretchDuration = DefaultStretchDuration;
        [SerializeField, Min(0.01f)] private float recoverDuration = DefaultRecoverDuration;

        private Transform visual;
        private SpriteRenderer visualRenderer;
        private Transform reactionRoot;
        private HealthComponent health;
        private Coroutine activeReaction;
        private Vector3 baseRootLocalPosition;
        private Vector3 baseRootLocalScale;
        private float currentScaleFactor = 1f;
        private bool subscribed;

        public bool IsConfigured => visualRenderer != null && reactionRoot != null;
        public float SquashScaleY => squashScaleY;
        public float StretchScaleY => stretchScaleY;
        public float SquashDuration => squashDuration;
        public float StretchDuration => stretchDuration;
        public float RecoverDuration => recoverDuration;
        public Transform ReactionRoot => reactionRoot;
        public Transform VisualTransform => visual;

        public void Configure(SpriteRenderer bodyRenderer, HealthComponent damageSource = null)
        {
            if (bodyRenderer == null)
            {
                Debug.LogError("Squash/stretch requires a dedicated visual SpriteRenderer.", this);
                return;
            }

            if (visualRenderer != bodyRenderer || reactionRoot == null)
            {
                StopAndReset();
                visualRenderer = bodyRenderer;
                visual = bodyRenderer.transform;
                Transform originalParent = visual.parent;
                var wrapper = new GameObject("SquashStretchVisualRoot");
                reactionRoot = wrapper.transform;
                reactionRoot.SetParent(originalParent, false);
                reactionRoot.SetSiblingIndex(visual.GetSiblingIndex());
                reactionRoot.localPosition = Vector3.zero;
                reactionRoot.localRotation = Quaternion.identity;
                reactionRoot.localScale = Vector3.one;
                visual.SetParent(reactionRoot, false);
                baseRootLocalPosition = reactionRoot.localPosition;
                baseRootLocalScale = reactionRoot.localScale;
            }

            BindHealth(damageSource);
            ResetPresentation();
        }

        public void Trigger()
        {
            if (!isActiveAndEnabled || !IsConfigured) return;
            if (activeReaction != null) StopCoroutine(activeReaction);
            ResetPresentation();
            activeReaction = StartCoroutine(PlayReaction());
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            StopAndReset();
        }

        private void OnDestroy() => Unsubscribe();

        private void LateUpdate()
        {
            if (IsConfigured) ApplyScale(currentScaleFactor);
        }

        private void BindHealth(HealthComponent source)
        {
            if (health == source)
            {
                Subscribe();
                return;
            }
            Unsubscribe();
            health = source;
            Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed || health == null || !isActiveAndEnabled) return;
            health.DamageReceived += OnDamageReceived;
            health.Died += OnDeath;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || health == null) return;
            health.DamageReceived -= OnDamageReceived;
            health.Died -= OnDeath;
            subscribed = false;
        }

        private void OnDamageReceived(HealthComponent _, int actualLoss, bool __)
        {
            if (actualLoss > 0) Trigger();
        }

        private void OnDeath(HealthComponent _) => StopAndReset();

        private IEnumerator PlayReaction()
        {
            yield return AnimateScale(1f, squashScaleY, squashDuration);
            yield return AnimateScale(squashScaleY, stretchScaleY, stretchDuration);
            yield return AnimateScale(stretchScaleY, 1f, recoverDuration);
            currentScaleFactor = 1f;
            ApplyScale(1f);
            activeReaction = null;
        }

        private IEnumerator AnimateScale(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                currentScaleFactor = Mathf.Lerp(from, to,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            currentScaleFactor = to;
        }

        private void ApplyScale(float factor)
        {
            if (reactionRoot == null || visualRenderer == null) return;
            float footOffset = visual.localPosition.y;
            if (visualRenderer.sprite != null)
                footOffset += visualRenderer.sprite.bounds.min.y * visual.localScale.y;

            reactionRoot.localScale = new Vector3(baseRootLocalScale.x,
                baseRootLocalScale.y * factor, baseRootLocalScale.z);
            reactionRoot.localPosition = new Vector3(baseRootLocalPosition.x,
                baseRootLocalPosition.y + baseRootLocalScale.y * footOffset * (1f - factor),
                baseRootLocalPosition.z);
        }

        private void StopAndReset()
        {
            if (activeReaction != null)
            {
                StopCoroutine(activeReaction);
                activeReaction = null;
            }
            ResetPresentation();
        }

        private void ResetPresentation()
        {
            currentScaleFactor = 1f;
            if (reactionRoot == null) return;
            reactionRoot.localPosition = baseRootLocalPosition;
            reactionRoot.localScale = baseRootLocalScale;
        }
    }
}
