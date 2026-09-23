using UnityEngine;

namespace TinyKingdom.Input
{
    // Fixed ground marker for a player-issued AttackMove destination. Explicit
    // Attack keeps using AttackCommandFeedback, which follows its target.
    public sealed class AttackMoveCommandFeedback : MonoBehaviour
    {
        private const int SegmentCount = 40;
        private const float Lifetime = 0.45f;
        private static readonly Color[] Colors =
        {
            new(1f, 0.1f, 0.08f, 0.95f),
            new(1f, 0.42f, 0.30f, 0.85f)
        };

        private readonly LineRenderer[] rings = new LineRenderer[2];
        private Material material;
        private float elapsed;

        public static void Spawn(Vector3 destination)
        {
            var marker = new GameObject("AttackMoveCommandFeedback");
            marker.transform.position = new Vector3(destination.x, destination.y, 0f);
            marker.AddComponent<AttackMoveCommandFeedback>();
        }

        private void Awake()
        {
            material = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < rings.Length; i++)
            {
                var ringObject = new GameObject(i == 0 ? "OuterRing" : "InnerRing");
                ringObject.transform.SetParent(transform, false);
                LineRenderer ring = ringObject.AddComponent<LineRenderer>();
                ring.sharedMaterial = material;
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = SegmentCount;
                ring.startWidth = ring.endWidth = 0.022f;
                ring.numCornerVertices = 2;
                ring.sortingOrder = 8;
                rings[i] = ring;
            }
            Draw(0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }
            Draw(elapsed / Lifetime);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        private void Draw(float progress)
        {
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, progress);
            for (int ringIndex = 0; ringIndex < rings.Length; ringIndex++)
            {
                Color color = Colors[ringIndex];
                color.a *= opacity;
                rings[ringIndex].startColor = rings[ringIndex].endColor = color;
                Vector2 start = ringIndex == 0 ? new Vector2(0.82f, 0.28f)
                    : new Vector2(0.62f, 0.21f);
                Vector2 end = ringIndex == 0 ? new Vector2(0.48f, 0.16f)
                    : new Vector2(0.28f, 0.09f);
                Vector2 size = Vector2.Lerp(start, end, progress);
                for (int point = 0; point < SegmentCount; point++)
                {
                    float angle = point * 2f * Mathf.PI / SegmentCount;
                    rings[ringIndex].SetPosition(point,
                        new Vector3(Mathf.Cos(angle) * size.x * 0.5f,
                            Mathf.Sin(angle) * size.y * 0.5f, 0f));
                }
            }
        }
    }
}
