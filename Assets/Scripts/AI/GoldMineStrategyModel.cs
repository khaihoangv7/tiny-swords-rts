using System;
using System.Collections.Generic;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    public readonly struct GoldMineOpportunity
    {
        public GoldMineOpportunity(GoldMineKnowledgeSnapshot mine, float score,
            float knownGuardStrength, float ownStrength, bool guarded,
            bool canCommit, bool safeForMiners, string reason)
        {
            Mine = mine;
            Score = score;
            KnownGuardStrength = knownGuardStrength;
            OwnStrength = ownStrength;
            Guarded = guarded;
            CanCommit = canCommit;
            SafeForMiners = safeForMiners;
            Reason = reason;
            IsValid = true;
        }

        public bool IsValid { get; }
        public GoldMineKnowledgeSnapshot Mine { get; }
        public float Score { get; }
        public float KnownGuardStrength { get; }
        public float OwnStrength { get; }
        public bool Guarded { get; }
        public bool CanCommit { get; }
        public bool SafeForMiners { get; }
        public string Reason { get; }
    }

    // Pure, bounded decision model. The only mine/Goblin inputs are copied
    // FactionKnowledge observations; the command policy resolves a live mine
    // only after authoritative visibility has been checked.
    [Serializable]
    public sealed class GoldMineStrategyModel
    {
        private static readonly ProfilerMarker ScoreMarker =
            new("TinyKingdom.AI.GoldMine.Score");
        private static readonly ProfilerMarker ThreatMarker =
            new("TinyKingdom.AI.GoldMine.Threat");
        [SerializeField, Range(1, 8)] private int maximumCandidates = 4;
        [SerializeField, Min(2f)] private float guardRegionRadius = 9f;
        [SerializeField, Min(2f)] private float workerSafetyRadius = 7f;
        [SerializeField, Min(1f)] private float guardMemorySeconds = 180f;
        [SerializeField, Min(1f)] private float workerMemorySeconds = 25f;
        [SerializeField, Min(1f)] private float mineMemorySeconds = 300f;
        [SerializeField, Min(1f)] private float minimumGuardStrengthRatio = 1.35f;
        [SerializeField, Range(0f, 1f)] private float minimumObservedArea = 0.65f;
        [SerializeField, Min(1)] private int minimumEconomyPawns = 3;
        [SerializeField, Min(1f)] private float failedAttemptCooldown = 45f;
        private EntityId blockedMineId;
        private float blockedUntil;

        public int MaximumCandidates => Mathf.Clamp(maximumCandidates, 1, 8);
        public float GuardRegionRadius => Mathf.Max(2f, guardRegionRadius);
        public float WorkerSafetyRadius => Mathf.Max(2f, workerSafetyRadius);
        public float GuardMemorySeconds => Mathf.Max(1f, guardMemorySeconds);
        public GoldMineOpportunity Current { get; private set; }
        public int CandidatesEvaluated { get; private set; }
        public float LastEvaluationTime { get; private set; } = -1f;
        public uint FailedAttemptCount { get; private set; }

        public void Reset()
        {
            Current = default;
            CandidatesEvaluated = 0;
            LastEvaluationTime = -1f;
            blockedMineId = default;
            blockedUntil = 0f;
            FailedAttemptCount = 0;
        }

        public void RecordFailure(EntityId id, float now)
        {
            blockedMineId = id;
            blockedUntil = now + Mathf.Max(1f, failedAttemptCooldown);
            FailedAttemptCount++;
        }

        public GoldMineOpportunity Evaluate(CommanderDecisionContext context,
            MilitaryCommanderPolicy military, ProductionCommanderPolicy production,
            StrategicSafetyMap safety)
        {
            using var marker = ScoreMarker.Auto();
            Current = default;
            CandidatesEvaluated = 0;
            LastEvaluationTime = context.Time;
            FactionKnowledge knowledge = context.Knowledge;
            if (knowledge == null || military == null || production == null)
                return Current;
            IReadOnlyList<GoldMineKnowledgeSnapshot> mines = knowledge.KnownGoldMines;
            float ownStrength = CombatStrengthEvaluator.OwnArmy(
                military.Army.Members, military.CombatStrength, out _, out _);
            int desiredMilitaryPopulation = 0;
            for (int i = 0; i < mines.Count && i < MaximumCandidates; i++)
            {
                GoldMineKnowledgeSnapshot mine = mines[i];
                CandidatesEvaluated++;
                if (mine.State is GoldMineState.Depleted or GoldMineState.Destroyed
                    || mine.LastSeenRemainingGold <= 0
                    || context.Time < blockedUntil
                        && mine.RuntimeId.CompareTo(blockedMineId) == 0)
                    continue;
                bool hostileOwner = mine.HasOwner
                    && mine.Owner != context.Faction;
                bool visible = IsMineVisible(knowledge, mine.RuntimeId);
                float age = Mathf.Max(0f, context.Time - mine.LastSeenTime);
                if (!visible && age > mineMemorySeconds) continue;
                float guardStrength = 0f;
                bool knownGuard = false;
                bool recentWorkerThreat = false;
                using (ThreatMarker.Auto())
                    AssessThreat(knowledge, military.CombatStrength, mine,
                        context.Time, ref guardStrength, ref knownGuard,
                        ref recentWorkerThreat);

                float observedArea = ObservedArea(knowledge, mine.LastSeenPosition);
                bool mineSafe = IsSafeForMiners(visible, observedArea,
                    minimumObservedArea, knownGuard, recentWorkerThreat,
                    safety == null || safety.RiskAt(mine.LastSeenPosition,
                        StrategicPathKind.Civilian) < StrategicRisk.Dangerous)
                    && (!hostileOwner || HasLocalCaptureForce(
                        military.Army.Members, mine.LastSeenPosition,
                        knowledge.TeamVision?.GridManager));
                bool guarded = knownGuard || !mineSafe;
                float requiredStrength = Mathf.Max(1f,
                    guardStrength * minimumGuardStrengthRatio + 1f);
                float readiness = ownStrength / requiredStrength;
                if ((!mine.HasOwner || hostileOwner) && guardStrength > 0f
                    && ownStrength < requiredStrength
                    && production.LivingPawnCount >= minimumEconomyPawns)
                {
                    float strengthPerPopulation = ownStrength
                        / Mathf.Max(1, production.MilitaryPopulation);
                    int desiredPopulation = Mathf.CeilToInt(requiredStrength
                        / Mathf.Max(0.5f, strengthPerPopulation));
                    desiredMilitaryPopulation = Mathf.Max(desiredMilitaryPopulation,
                        desiredPopulation);
                }
                bool contestedOwnMine = mine.HasOwner
                    && mine.Owner == context.Faction && recentWorkerThreat;
                bool canCommit = (guarded && !mine.HasOwner
                    || hostileOwner || contestedOwnMine)
                    && HasSufficientForce(ownStrength, guardStrength,
                        minimumGuardStrengthRatio, production.MilitaryPopulation,
                        military.MinOffensiveMilitaryPopulation)
                    && production.LivingPawnCount >= minimumEconomyPawns
                    && observedArea >= minimumObservedArea
                    && !military.HasCurrentThreat;
                float goldValue = Mathf.Clamp01(mine.LastSeenRemainingGold
                    / (float)GoldMineObjective.MaximumGold);
                float goldNeed = Mathf.Clamp01((200f - knowledge.Stockpile
                    .GetAmount(ResourceType.Gold)) / 200f);
                float distance = military.HasBaseAnchor
                    ? Vector2.Distance(military.BaseAnchor,
                        mine.LastSeenPosition) : 0f;
                float travelPenalty = Mathf.Clamp01(distance / 80f) * 0.16f;
                float uncertainty = visible ? 0f : Mathf.Clamp01(age
                    / mineMemorySeconds) * 0.18f;
                float danger = guardStrength > 0f
                    ? Mathf.Clamp01(guardStrength / Mathf.Max(1f, ownStrength))
                        * 0.12f : 0f;
                float score = Mathf.Clamp01(0.37f + goldValue * 0.18f
                    + goldNeed * 0.12f + Mathf.Clamp01(readiness) * 0.18f
                    + (mineSafe ? 0.10f : 0f)
                    - travelPenalty - uncertainty - danger);
                // A ready army can investigate a recently observed mine even
                // when the Scout has lost sight of it. Workers still require
                // fresh visibility and a cleared approach.
                if (canCommit)
                    score = Mathf.Clamp01(score + 0.33f * goldValue);
                if (hostileOwner && visible && mine.LastSeenOccupants > 0)
                    score = Mathf.Clamp01(score + 0.08f * goldValue);
                if (contestedOwnMine) score = Mathf.Max(score, 0.90f);
                else if (mine.HasOwner && mine.Owner == context.Faction)
                    score = Mathf.Min(score, 0.55f);
                string reason = contestedOwnMine ? "Owned mine contested"
                    : hostileOwner && canCommit ? "Opponent-held mine contestable"
                    : hostileOwner ? "Opponent-held mine recapture postponed"
                    : mineSafe ? "Mine safe for Pawns"
                    : canCommit ? "Guarded mine is contestable"
                    : observedArea < minimumObservedArea ? "Mine area needs scouting"
                    : readiness < 1f ? "Army too weak for known guards"
                    : "Mine pursuit postponed";
                var candidate = new GoldMineOpportunity(mine, score,
                    guardStrength, ownStrength, guarded, canCommit,
                    mineSafe, reason);
                if (!Current.IsValid || candidate.Score > Current.Score + 0.0001f
                    || Mathf.Abs(candidate.Score - Current.Score) <= 0.0001f
                    && mine.RuntimeId.CompareTo(Current.Mine.RuntimeId) < 0)
                    Current = candidate;
            }
            if (desiredMilitaryPopulation > 0)
                production.RequestStrategicMilitaryPopulation(
                    desiredMilitaryPopulation, context.Time);
            return Current;
        }

        private void AssessThreat(FactionKnowledge knowledge,
            CombatStrengthTuning tuning, GoldMineKnowledgeSnapshot mine,
            float now, ref float strength, ref bool knownGuard,
            ref bool workerThreat)
        {
            float guardRadiusSquared = GuardRegionRadius * GuardRegionRadius;
            float workerRadiusSquared = WorkerSafetyRadius * WorkerSafetyRadius;
            IReadOnlyList<GoblinKnowledgeSnapshot> goblins = knowledge.KnownGoblins;
            for (int i = 0; i < goblins.Count; i++)
            {
                GoblinKnowledgeSnapshot goblin = goblins[i];
                float age = Mathf.Max(0f, now - goblin.LastSeenTime);
                float squared = ((Vector2)(goblin.LastSeenPosition
                    - mine.LastSeenPosition)).sqrMagnitude;
                if (squared > guardRadiusSquared || age > guardMemorySeconds)
                    continue;
                knownGuard = true;
                strength += tuning.UnitBaseValue(goblin.UnitType)
                    * goblin.LastSeenHealthRatio
                    * Mathf.Lerp(1f, 1.25f, age / guardMemorySeconds);
                if (age <= workerMemorySeconds && squared <= workerRadiusSquared)
                    workerThreat = true;
            }
            IReadOnlyList<VisibleUnitObservation> visible = knowledge.VisibleEnemyUnits;
            for (int i = 0; i < visible.Count; i++)
            {
                VisibleUnitObservation enemy = visible[i];
                if (enemy.Faction == Faction.Goblin) continue;
                float squared = ((Vector2)(enemy.Position
                    - mine.LastSeenPosition)).sqrMagnitude;
                if (squared > guardRadiusSquared) continue;
                knownGuard = true;
                strength += CombatStrengthEvaluator.VisibleEnemyUnit(enemy, tuning);
                if (squared <= workerRadiusSquared) workerThreat = true;
            }
            MilitaryObservationSnapshot remembered = knowledge.LastMilitaryObservation;
            if (remembered.HasObservation
                && now - remembered.LastSeenTime > 0.75f
                && now - remembered.LastSeenTime <= workerMemorySeconds
                && ((Vector2)(remembered.ObservationRegion
                    - mine.LastSeenPosition)).sqrMagnitude <= guardRadiusSquared)
            {
                knownGuard = true;
                workerThreat = true;
                strength += remembered.WarriorCount * tuning.WarriorValue
                    + remembered.ArcherCount * tuning.ArcherValue
                    + remembered.LancerCount * tuning.LancerValue;
            }
        }

        public static bool HasSufficientForce(float ownStrength,
            float knownThreatStrength, float requiredRatio,
            int militaryPopulation, int minimumPopulation)
            => militaryPopulation >= minimumPopulation
                && ownStrength >= Mathf.Max(1f,
                    knownThreatStrength * Mathf.Max(1f, requiredRatio) + 1f);

        public static bool IsSafeForMiners(bool mineVisible,
            float observedArea, float minimumArea, bool knownGuard,
            bool recentWorkerThreat, bool safeCivilianCell)
            => mineVisible && observedArea >= minimumArea
                && !knownGuard && !recentWorkerThreat && safeCivilianCell;

        private static bool HasLocalCaptureForce(
            IReadOnlyList<UnitController> members, Vector3 minePosition,
            GridManager grid)
        {
            float radiusSquared = GoldMineObjective.ContestRadius
                * GoldMineObjective.ContestRadius;
            int mineHeight = 0;
            if (grid != null)
            {
                Vector2Int mineCell = grid.WorldToGrid(minePosition);
                mineHeight = grid.GetCell(mineCell.x, mineCell.y)?.HeightLevel ?? 0;
            }
            for (int i = 0; i < members.Count; i++)
            {
                UnitController unit = members[i];
                if (unit == null || !unit.IsTargetable
                    || ((Vector2)(unit.transform.position - minePosition))
                        .sqrMagnitude > radiusSquared) continue;
                if (grid != null)
                {
                    Vector2Int unitCell = grid.WorldToGrid(unit.transform.position);
                    if (grid.GetCell(unitCell.x, unitCell.y)?.HeightLevel
                        != mineHeight) continue;
                }
                UnitType? type = unit.GetComponent<HealthComponent>()?.CombatData?.UnitType;
                if (type is UnitType.Warrior or UnitType.Lancer or UnitType.Archer)
                    return true;
            }
            return false;
        }

        private static bool IsMineVisible(FactionKnowledge knowledge, EntityId id)
        {
            IReadOnlyList<GoldMineKnowledgeSnapshot> visible = knowledge.VisibleGoldMines;
            for (int i = 0; i < visible.Count; i++)
                if (visible[i].RuntimeId.CompareTo(id) == 0) return true;
            return false;
        }

        private float ObservedArea(FactionKnowledge knowledge, Vector3 world)
        {
            GridManager grid = knowledge.TeamVision?.GridManager;
            if (grid == null) return 0f;
            Vector2Int center = grid.WorldToGrid(world);
            int observed = 0, total = 0;
            for (int y = -3; y <= 3; y++)
            for (int x = -3; x <= 3; x++)
            {
                Vector2Int cell = center + new Vector2Int(x, y);
                if (!grid.IsInsideGrid(cell.x, cell.y)) continue;
                total++;
                if (knowledge.IsCellExplored(cell)) observed++;
            }
            return total > 0 ? (float)observed / total : 0f;
        }
    }
}
