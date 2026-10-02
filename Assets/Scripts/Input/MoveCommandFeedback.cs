using UnityEngine;

namespace TinyKingdom.Input
{
    public sealed class MoveCommandFeedback : MonoBehaviour
    {
        public const float PresentationScale = 1.35f;

        private const int SegmentCount = 40;
        private static readonly Color[] RingColors =
        {
            new Color(0.35f, 1f, 0.75f, 0.9f),
            new Color(0.9f, 1f, 0.95f, 0.8f)
        };

        [SerializeField, Min(0.1f)] private float lifetime = 0.45f;
        [SerializeField, Min(0.005f)] private float ringLineWidth = 0.022f;
        [SerializeField] private Vector2 outerStartSize = new Vector2(0.75f, 0.26f);
        [SerializeField] private Vector2 outerEndSize = new Vector2(0.48f, 0.16f);
        [SerializeField] private Vector2 innerStartSize = new Vector2(0.55f, 0.18f);
        [SerializeField] private Vector2 innerEndSize = new Vector2(0.30f, 0.10f);

        private readonly LineRenderer[] rings = new LineRenderer[2];
        private Material ringMaterial;
        private float elapsed;

        public static void Spawn(Vector3 destination)
        {
            var marker = new GameObject("MoveCommandFeedback");
            marker.transform.position = new Vector3(destination.x, destination.y, 0f);
            marker.AddComponent<MoveCommandFeedback>();
        }

        private void Awake()
        {
            ringMaterial = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < rings.Length; i++)
            {
                var ringObject = new GameObject(i == 0 ? "OuterRing" : "InnerRing");
                ringObject.transform.SetParent(transform, false);
                LineRenderer ring = ringObject.AddComponent<LineRenderer>();
                ring.sharedMaterial = ringMaterial;
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = SegmentCount;
                ring.startWidth = ringLineWidth * PresentationScale;
                ring.endWidth = ringLineWidth * PresentationScale;
                ring.numCornerVertices = 2;
                ring.sortingOrder = 8;
                rings[i] = ring;
            }
            Draw(0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
            Draw(elapsed / lifetime);
        }

        private void OnDestroy()
        {
            if (ringMaterial != null)
                Destroy(ringMaterial);
        }

        private void Draw(float progress)
        {
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, progress);
            for (int ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                LineRenderer ring = rings[ringIndex];
                Color color = RingColors[ringIndex];
                color.a *= opacity;
                ring.startColor = color;
                ring.endColor = color;
                Vector2 size = ringIndex == 0
                    ? Vector2.Lerp(outerStartSize, outerEndSize, progress)
                    : Vector2.Lerp(innerStartSize, innerEndSize, progress);
                size *= PresentationScale;
                for (int point = 0; point < SegmentCount; point++)
                {
                    float angle = point * 2f * Mathf.PI / SegmentCount;
                    ring.SetPosition(point, new Vector3(Mathf.Cos(angle) * size.x * 0.5f,
                        Mathf.Sin(angle) * size.y * 0.5f, 0f));
                }
            }
        }
    }
}
