using System;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.AI
{
    public enum EnemyIntelSource
    {
        Unknown = 0,
        CurrentVisible = 1,
        RecentMemory = 2,
        StaleMemory = 3
    }

    // Bounded, value-only military intelligence. Hidden enemies never remain as
    // live objects here; the last legal visible snapshot is frozen and only its
    // confidence changes with age.
    [Serializable]
    public sealed class EnemyCompositionMemory
    {
        [SerializeField, Min(0f)] private float fullConfidenceSeconds = 12f;
        [SerializeField, Min(1f)] private float decaySeconds = 75f;

        private readonly int[] observedCounts = new int[5];
        private readonly float[] lastSeenTimes = { -1f, -1f, -1f, -1f, -1f };
        private readonly int[] recentVisibleFloors = new int[5];
        private readonly float[] recentVisibleTimes = { -1f, -1f, -1f, -1f, -1f };
        private float latestObservationTime = -1f;
        private Vector3 observationRegion;
        private uint observationSerial;

        public float FullConfidenceSeconds => fullConfidenceSeconds;
        public float DecaySeconds => decaySeconds;
        public float LastSeenTime => latestObservationTime;
        public Vector3 ObservationRegion => observationRegion;
        public uint ObservationSerial => observationSerial;
        public bool HasObservation => LastSeenTime >= 0f && TotalObserved > 0;
        public int TotalObserved => observedCounts[(int)UnitType.Warrior]
            + observedCounts[(int)UnitType.Archer]
            + observedCounts[(int)UnitType.Lancer]
            + observedCounts[(int)UnitType.Monk];

        public int ObservedCount(UnitType type)
            => IsTracked(type) ? observedCounts[(int)type] : 0;
        public float LastSeenTimeFor(UnitType type)
            => IsTracked(type) ? lastSeenTimes[(int)type] : -1f;

        public void Reset()
        {
            Array.Clear(observedCounts, 0, observedCounts.Length);
            Array.Clear(recentVisibleFloors, 0, recentVisibleFloors.Length);
            for (int i = 0; i < lastSeenTimes.Length; i++) lastSeenTimes[i] = -1f;
            for (int i = 0; i < recentVisibleTimes.Length; i++) recentVisibleTimes[i] = -1f;
            latestObservationTime = -1f;
            observationRegion = Vector3.zero;
            observationSerial = 0;
        }

        public bool Refresh(FactionKnowledge knowledge, float now)
        {
            if (knowledge == null) return false;
            MilitaryObservationSnapshot legal = knowledge.LastMilitaryObservation;
            if (!legal.HasObservation || legal.LastSeenTime <= LastSeenTime + 0.0001f)
                return false;
            Observe(legal, now);
            return true;
        }

        public void Observe(MilitaryObservationSnapshot legalObservation, float now)
        {
            if (!legalObservation.HasObservation) return;
            float observedAt = Mathf.Min(now, legalObservation.LastSeenTime);
            ObserveType(UnitType.Warrior, legalObservation.WarriorCount, observedAt);
            ObserveType(UnitType.Archer, legalObservation.ArcherCount, observedAt);
            ObserveType(UnitType.Lancer, legalObservation.LancerCount, observedAt);
            ObserveType(UnitType.Monk, legalObservation.MonkCount, observedAt);
            latestObservationTime = observedAt;
            observationRegion = legalObservation.ObservationRegion;
            observationSerial++;
        }

        public float ConfidenceAt(float now)
        {
            if (!HasObservation) return 0f;
            float weighted = 0f;
            int total = 0;
            for (int i = 0; i < observedCounts.Length; i++)
            {
                if (observedCounts[i] <= 0) continue;
                weighted += EvidenceCount((UnitType)i, now);
                total += observedCounts[i];
            }
            return total > 0 ? weighted / total : 0f;
        }

        public float ConfidenceAt(UnitType type, float now)
            => IsTracked(type) && observedCounts[(int)type] > 0
                ? Mathf.Clamp01(EvidenceCount(type, now) / observedCounts[(int)type]) : 0f;

        public float EvidenceCount(UnitType type, float now)
        {
            if (!IsTracked(type)) return 0f;
            int index = (int)type;
            float remembered = observedCounts[index]
                * ConfidenceForTime(lastSeenTimes[index], now);
            float visibleFloor = recentVisibleFloors[index]
                * ConfidenceForTime(recentVisibleTimes[index], now);
            return Mathf.Max(remembered, visibleFloor);
        }

        private float ConfidenceForTime(float seenTime, float now)
        {
            if (seenTime < 0f) return 0f;
            float age = Mathf.Max(0f, now - seenTime);
            if (age <= fullConfidenceSeconds) return 1f;
            return Mathf.Clamp01(1f - (age - fullConfidenceSeconds)
                / Mathf.Max(1f, decaySeconds));
        }

        public EnemyIntelSource SourceAt(float now)
        {
            if (!HasObservation || ConfidenceAt(now) <= 0f) return EnemyIntelSource.Unknown;
            float age = Mathf.Max(0f, now - LastSeenTime);
            if (age <= 0.75f) return EnemyIntelSource.CurrentVisible;
            return age <= fullConfidenceSeconds
                ? EnemyIntelSource.RecentMemory : EnemyIntelSource.StaleMemory;
        }

        private void ObserveType(UnitType type, int visibleCount, float observedAt)
        {
            if (visibleCount <= 0) return;
            int index = (int)type;
            // A smaller partial sighting is not proof that remembered hidden units
            // died. Keep the older larger evidence decaying; only equal/greater
            // evidence refreshes this type.
            if (visibleCount < observedCounts[index])
            {
                recentVisibleFloors[index] = visibleCount;
                recentVisibleTimes[index] = observedAt;
                return;
            }
            observedCounts[index] = visibleCount;
            lastSeenTimes[index] = observedAt;
            recentVisibleFloors[index] = 0;
            recentVisibleTimes[index] = -1f;
        }

        private static bool IsTracked(UnitType type)
            => type is UnitType.Warrior or UnitType.Archer or UnitType.Lancer
                or UnitType.Monk;
    }

    // Read-only adapter over the authoritative gameplay counter table.
    public static class CombatMatchupEvaluator
    {
        public static float Effectiveness(UnitType candidate, UnitType enemy)
            => CombatDamage.Multiplier(candidate, enemy);
    }
}
