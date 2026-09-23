using UnityEngine;

namespace TinyKingdom.Economy
{
    // Plays the verified six-frame highlight sheet after a successful Gold impact.
    public sealed class GoldHarvestHighlight : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float ambientHighlightDelay = 10f;
        private ResourceNode node;
        private Sprite normal;
        private Sprite[] frames;
        private int frame;
        private float remaining;
        private float ambientRemaining;
        private bool playing;

        public float AmbientHighlightDelay => ambientHighlightDelay;
        public bool IsPlaying => playing;

        public void Configure(ResourceNode source, Sprite normalSprite, Sprite[] highlightFrames)
        {
            node = source;
            normal = normalSprite;
            frames = highlightFrames;
            ambientRemaining = ambientHighlightDelay;
        }

        public void Play()
        {
            if (node == null || !node.IsAvailable || frames == null || frames.Length == 0
                || playing) return;
            playing = true;
            frame = 0;
            remaining = FrameDuration(0);
            node.SetPresentationSprite(frames[0]);
        }

        private void Update()
        {
            if (node == null || !node.IsAvailable)
            {
                playing = false;
                return;
            }
            if (!playing)
            {
                ambientRemaining -= Time.deltaTime;
                if (ambientRemaining <= 0f) Play();
                return;
            }
            remaining -= Time.deltaTime;
            while (remaining <= 0f)
            {
                frame++;
                if (frame >= frames.Length)
                {
                    playing = false;
                    node.SetPresentationSprite(normal);
                    ambientRemaining = ambientHighlightDelay;
                    return;
                }
                node.SetPresentationSprite(frames[frame]);
                remaining += FrameDuration(frame);
            }
        }

        private float FrameDuration(int index) => index == frames.Length - 1 ? 0.5f : 0.1f;
    }
}
