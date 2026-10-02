using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Terrain
{
    // Cosmetic only: ducks and water ripples never change gameplay state.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class AmbientSpriteLoop : MonoBehaviour
    {
        private sealed class Scheduler : MonoBehaviour
        {
            private void Update()
            {
                float time = Time.time;
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    AmbientSpriteLoop item = active[i];
                    if (item == null) active.RemoveAt(i);
                    else item.Tick(time);
                }
            }
        }

        private static readonly List<AmbientSpriteLoop> active = new();
        private static Scheduler scheduler;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 10f;
        [SerializeField] private float phaseSeconds;
        [SerializeField] private bool preserveBottomAnchor;
        [SerializeField] private float anchoredScale = 1f;
        [SerializeField] private int sourceFrameWidthPixels = 128;
        private SpriteRenderer visual;
        private int displayedFrame = -1;
        public int FrameCount => frames?.Length ?? 0;
        public float FramesPerSecond => framesPerSecond;
        public float PhaseSeconds => phaseSeconds;

        public void Configure(Sprite[] sprites, float phase, float fps = 10f,
            bool keepBottomAnchor = false, float visualScale = 1f,
            int sourceFrameWidth = 128)
        {
            frames = sprites;
            phaseSeconds = phase;
            framesPerSecond = Mathf.Max(0.01f, fps);
            preserveBottomAnchor = keepBottomAnchor;
            anchoredScale = Mathf.Max(0.01f, visualScale);
            sourceFrameWidthPixels = Mathf.Max(1, sourceFrameWidth);
            visual = GetComponent<SpriteRenderer>();
            displayedFrame = -1;
            if (FrameCount > 0) ApplyFrame(0);
        }

        private void Awake() => visual = GetComponent<SpriteRenderer>();

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            EnsureScheduler();
        }

        private void OnDisable() => active.Remove(this);

        private void Tick(float time)
        {
            if (FrameCount < 2) return;
            if (visual == null) visual = GetComponent<SpriteRenderer>();
            int index = Mathf.FloorToInt((time + phaseSeconds) * framesPerSecond) % FrameCount;
            if (index != displayedFrame) ApplyFrame(index);
        }

        private void ApplyFrame(int index)
        {
            Sprite sprite = frames[index];
            visual.sprite = sprite;
            displayedFrame = index;
            if (!preserveBottomAnchor || sprite == null) return;
            Rect rect = sprite.rect;
            float frameLeft = Mathf.Floor(rect.x / sourceFrameWidthPixels)
                * sourceFrameWidthPixels;
            float pivotInFrame = rect.x - frameLeft + sprite.pivot.x;
            float x = (pivotInFrame - sourceFrameWidthPixels * 0.5f)
                / sprite.pixelsPerUnit * anchoredScale;
            float y = sprite.pivot.y / sprite.pixelsPerUnit * anchoredScale;
            transform.localPosition = new Vector3(x, y, transform.localPosition.z);
        }

        private static void EnsureScheduler()
        {
            if (scheduler != null) return;
            var root = new GameObject("AmbientSpriteLoopScheduler");
            root.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(root);
            scheduler = root.AddComponent<Scheduler>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active.Clear();
            scheduler = null;
        }
    }
}
