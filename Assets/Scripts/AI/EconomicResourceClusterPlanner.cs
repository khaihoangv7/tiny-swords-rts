using System;
using System.Collections.Generic;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    public readonly struct EconomicClusterSnapshot
    {
        public EconomicClusterSnapshot(Vector2Int cell, Vector3 position, int nodeCount,
            int remaining, int expectedWorkers, float nearestDropOffDistance,
            StrategicRisk risk, float score)
        {
            CenterCell = cell;
            CenterPosition = position;
            NodeCount = nodeCount;
            RemainingAmount = remaining;
            ExpectedWorkers = expectedWorkers;
            NearestDropOffDistance = nearestDropOffDistance;
            Risk = risk;
            Score = score;
        }

        public Vector2Int CenterCell { get; }
        public Vector3 CenterPosition { get; }
        public int NodeCount { get; }
        public int RemainingAmount { get; }
        public int ExpectedWorkers { get; }
        public float NearestDropOffDistance { get; }
        public StrategicRisk Risk { get; }
        public float Score { get; }
    }

    // Bounded cache built only from value snapshots exposed by FactionKnowledge.
    // It never retains a ResourceNode or hidden Transform reference.
    [Serializable]
    public sealed class EconomicResourceClusterPlanner
    {
        private static readonly ProfilerMarker RefreshMarker =
            new("TinyKingdom.AI.EconomicClusterRefresh");
        [SerializeField, Min(2)] private int clusterRadiusCells = 5;
        [SerializeField, Min(8)] private int maximumRememberedNodes = 96;
        [SerializeField, Min(1)] private int maximumClusters = 16;
        [SerializeField, Min(1f)] private float refreshSeconds = 8f;
        [SerializeField, Min(1)] private int minimumNodes = 3;
        [SerializeField, Min(1)] private int minimumExpectedWorkers = 2;
        [SerializeField, Min(1f)] private float minimumDropOffDistance = 8f;

        private readonly List<EconomicClusterSnapshot> clusters = new();
        private readonly List<int> memberIndices = new();
        private bool[] consumed = Array.Empty<bool>();
        private float nextRefreshTime;
        private int lastKnownCount = -1;

        public IReadOnlyList<EconomicClusterSnapshot> Clusters => clusters;
        public EconomicClusterSnapshot Best { get; private set; }
        public bool HasBest { get; private set; }
        public string LastRejectionReason { get; private set; } = "Not evaluated";
        public static long RefreshCount { get; private set; }
        public static void ResetPerformanceCounters() => RefreshCount = 0;

        public void Reset()
        {
            clusters.Clear();
            memberIndices.Clear();
            HasBest = false;
            nextRefreshTime = 0f;
            lastKnownCount = -1;
            LastRejectionReason = "Reset";
        }

        public void Refresh(CommanderDecisionContext context, StrategicSafetyMap safety)
        {
            IReadOnlyList<ResourceKnowledgeSnapshot> known = context.Knowledge.KnownResources;
            if (context.Time < nextRefreshTime && known.Count == lastKnownCount) return;
            using var marker = RefreshMarker.Auto();
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            RefreshCount++;
#endif
            nextRefreshTime = context.Time + refreshSeconds;
            lastKnownCount = known.Count;
            clusters.Clear();
            HasBest = false;
            LastRejectionReason = "No viable known resource cluster";
            int count = Mathf.Min(known.Count, maximumRememberedNodes);
            if (consumed.Length < count) consumed = new bool[count];
            Array.Clear(consumed, 0, count);
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return;

            for (int seed = 0; seed < count && clusters.Count < maximumClusters; seed++)
            {
                if (consumed[seed] || known[seed].LastSeenRemainingAmount <= 0) continue;
                memberIndices.Clear();
                Vector2Int seedCell = known[seed].LogicalCell;
                int remaining = 0;
                Vector3 weighted = Vector3.zero;
                for (int i = seed; i < count; i++)
                {
                    ResourceKnowledgeSnapshot node = known[i];
                    if (consumed[i] || node.LastSeenRemainingAmount <= 0
                        || Vector2Int.Distance(seedCell, node.LogicalCell) > clusterRadiusCells)
                        continue;
                    consumed[i] = true;
                    memberIndices.Add(i);
                    int amount = Mathf.Max(1, node.LastSeenRemainingAmount);
                    remaining += amount;
                    weighted += node.LastSeenPosition * amount;
                }
                if (memberIndices.Count == 0) continue;
                Vector3 center = weighted / Mathf.Max(1, remaining);
                Vector2Int centerCell = grid.WorldToGrid(center);
                StrategicRisk risk = safety != null
                    ? safety.RiskAt(centerCell, StrategicPathKind.Civilian)
                    : StrategicRisk.Safe;
                float dropDistance = NearestDropOffDistance(context.Faction, center);
                int workers = Mathf.Clamp(remaining / 120, 1, memberIndices.Count * 2);
                float score = memberIndices.Count * 20f + Mathf.Min(remaining, 1200) * 0.05f
                    + dropDistance * workers - (int)risk * 120f;
                var cluster = new EconomicClusterSnapshot(centerCell, center,
                    memberIndices.Count, remaining, workers, dropDistance, risk, score);
                clusters.Add(cluster);
                if (memberIndices.Count < minimumNodes)
                    LastRejectionReason = "Cluster has too few nodes";
                else if (workers < minimumExpectedWorkers)
                    LastRejectionReason = "Cluster cannot support enough workers";
                else if (dropDistance < minimumDropOffDistance)
                    LastRejectionReason = "Existing drop-off is already close";
                else if (risk >= StrategicRisk.Dangerous)
                    LastRejectionReason = "Cluster is unsafe in legal knowledge";
                else if (!HasBest || score > Best.Score)
                {
                    Best = cluster;
                    HasBest = true;
                    LastRejectionReason = string.Empty;
                }
            }
        }

        private static float NearestDropOffDistance(Faction faction, Vector3 position)
        {
            float best = float.PositiveInfinity;
            IReadOnlyList<IResourceDropOff> dropOffs = ResourceDropOffRegistry.Active;
            for (int i = 0; i < dropOffs.Count; i++)
            {
                IResourceDropOff item = dropOffs[i];
                if (!ResourceDropOffRegistry.IsUsable(item, faction)) continue;
                best = Mathf.Min(best, Vector2.Distance(position, item.WorldPosition));
            }
            return float.IsPositiveInfinity(best) ? 999f : best;
        }
    }
}
