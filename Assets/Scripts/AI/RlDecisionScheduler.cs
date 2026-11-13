using UnityEngine;

namespace TinyKingdom.AI
{
    // Receives elapsed simulation seconds from MatchManager. One call can
    // consume at most one decision, even when a frame skips multiple deadlines.
    public sealed class RlDecisionScheduler
    {
        public const float Interval = 2f;
        public float NextDecisionTime { get; private set; }
        public float LastDrift { get; private set; }
        public int SkippedDeadlines { get; private set; }

        public void Reset(float now = 0f)
        {
            NextDecisionTime = Mathf.Max(0f, now);
            LastDrift = 0f;
            SkippedDeadlines = 0;
        }

        public bool TryConsume(float simulationTime, bool playing)
        {
            if (!playing || float.IsNaN(simulationTime)
                || float.IsInfinity(simulationTime)
                || simulationTime + 0.0001f < NextDecisionTime) return false;
            LastDrift = Mathf.Max(0f, simulationTime - NextDecisionTime);
            int missed = Mathf.Max(0, Mathf.FloorToInt(LastDrift / Interval));
            SkippedDeadlines += missed;
            NextDecisionTime = simulationTime + Interval;
            return true;
        }
    }
}
