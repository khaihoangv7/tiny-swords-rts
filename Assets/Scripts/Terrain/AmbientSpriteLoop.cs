using UnityEngine;

namespace TinyKingdom.Terrain
{
    // Cosmetic only: ducks and water ripples never change gameplay state.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class AmbientSpriteLoop : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 10f;
        [SerializeField] private float phaseSeconds;
        private SpriteRenderer visual;
        public int FrameCount => frames?.Length ?? 0;

        public void Configure(Sprite[] sprites, float phase)
        {
            frames = sprites;
            phaseSeconds = phase;
            visual = GetComponent<SpriteRenderer>();
            if (FrameCount > 0) visual.sprite = frames[0];
        }

        private void Awake() => visual = GetComponent<SpriteRenderer>();

        private void Update()
        {
            if (FrameCount < 2) return;
            int index = Mathf.FloorToInt((Time.time + phaseSeconds) * framesPerSecond) % FrameCount;
            visual.sprite = frames[index];
        }
    }
}
