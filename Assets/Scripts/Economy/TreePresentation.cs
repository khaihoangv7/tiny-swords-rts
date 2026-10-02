using UnityEngine;

namespace TinyKingdom.Economy
{
    // Loops the verified eight-frame Tree tag; depletion swaps to the matching stump.
    public sealed class TreePresentation : MonoBehaviour
    {
        public const float HarvestWobbleDegrees = 3.5f;
        public const float HarvestWobbleDuration = 0.22f;
        private ResourceNode node;
        private Sprite[] frames;
        private int frame;
        private float elapsed;
        private Transform visualTransform;
        private Quaternion restRotation;
        private float wobbleRemaining;

        public void Configure(ResourceNode source, Sprite[] animationFrames)
        {
            if (node != null) node.Harvested -= OnHarvested;
            node = source;
            frames = animationFrames;
            visualTransform = source != null && source.PresentationRenderer != null
                ? source.PresentationRenderer.transform : null;
            restRotation = visualTransform != null ? visualTransform.localRotation : Quaternion.identity;
            if (node != null) node.Harvested += OnHarvested;
        }

        private void Update()
        {
            if (node != null && node.IsAvailable && frames != null && frames.Length >= 2)
            {
                elapsed += Time.deltaTime;
                while (elapsed >= 0.1f)
                {
                    elapsed -= 0.1f;
                    frame = (frame + 1) % frames.Length;
                    node.SetPresentationSprite(frames[frame]);
                }
            }

            if (visualTransform == null || wobbleRemaining <= 0f) return;
            wobbleRemaining = Mathf.Max(0f, wobbleRemaining - Time.deltaTime);
            float progress = 1f - wobbleRemaining / HarvestWobbleDuration;
            float angle = Mathf.Sin(progress * Mathf.PI * 3f)
                * (1f - progress) * HarvestWobbleDegrees;
            visualTransform.localRotation = restRotation * Quaternion.Euler(0f, 0f, angle);
            if (wobbleRemaining <= 0f) visualTransform.localRotation = restRotation;
        }

        private void OnHarvested(ResourceNode source, int amount)
        {
            if (source == node && source.ResourceType == ResourceType.Wood && amount > 0)
                wobbleRemaining = HarvestWobbleDuration;
        }

        private void OnEnable()
        {
            if (node == null) return;
            node.Harvested -= OnHarvested;
            node.Harvested += OnHarvested;
        }

        private void OnDisable()
        {
            if (node != null) node.Harvested -= OnHarvested;
            if (visualTransform != null) visualTransform.localRotation = restRotation;
            wobbleRemaining = 0f;
        }

        private void OnDestroy()
        {
            if (node != null) node.Harvested -= OnHarvested;
        }
    }
}
