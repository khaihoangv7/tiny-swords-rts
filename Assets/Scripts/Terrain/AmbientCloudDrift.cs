using UnityEngine;

namespace TinyKingdom.Terrain
{
    // Cosmetic motion of a whole native cloud illustration, independent of gameplay.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class AmbientCloudDrift : MonoBehaviour
    {
        [SerializeField] private Vector2 anchor;
        [SerializeField] private Vector2 amplitude;
        [SerializeField, Min(1f)] private float period = 60f;
        [SerializeField] private float phase;

        public Vector2 Anchor => anchor;
        public float Period => period;

        public void Configure(Rect bounds, Vector2 travel, float seconds, float startPhase)
        {
            Vector2 extent = GetComponent<SpriteRenderer>().bounds.extents;
            amplitude = new Vector2(
                Mathf.Min(Mathf.Abs(travel.x), Mathf.Max(0, bounds.width*.5f-extent.x)),
                Mathf.Min(Mathf.Abs(travel.y), Mathf.Max(0, bounds.height*.5f-extent.y)));
            Vector2 margin = extent + amplitude;
            anchor = new Vector2(
                Mathf.Clamp(transform.position.x, bounds.xMin+margin.x, bounds.xMax-margin.x),
                Mathf.Clamp(transform.position.y, bounds.yMin+margin.y, bounds.yMax-margin.y));
            period = Mathf.Max(1f, seconds);
            phase = startPhase;
            ApplyPosition(0);
        }

        public Vector2 PositionAt(float seconds)
        {
            float angle = seconds * (Mathf.PI*2f/period) + phase;
            return anchor + new Vector2(Mathf.Sin(angle)*amplitude.x, Mathf.Sin(angle)*amplitude.y);
        }

        private void Update() => ApplyPosition(Time.time);

        private void ApplyPosition(float seconds)
        {
            Vector2 p = PositionAt(seconds);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
    }
}
