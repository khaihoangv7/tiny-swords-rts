using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    [Serializable]
    public sealed class ProductionCommanderPolicy
    {
        private static readonly ProfilerMarker StagingSelectionMarker =
            new("TinyKingdom.AI.Staging.Select");
        private static readonly ProfilerMarker EnemyCompositionMarker =
            new("TinyKingdom.AI.EnemyCompositionEvaluation");
        private static readonly ProfilerMarker DesiredCompositionMarker =
            new("TinyKingdom.AI.DesiredComposition");
        private static readonly ProfilerMarker CandidateScoreMarker =
            new("TinyKingdom.AI.ProductionCandidateScore");
        private static readonly UnitType[] MilitaryTypes =
        {
            UnitType.Warrior, UnitType.Archer, UnitType.Lancer, UnitType.Monk
        };

        [SerializeField, Min(1)] private int targetPawnCount = 6;
        [SerializeField, Min(1)] private int targetMilitaryPopulation = 8;
        [SerializeField, Range(1, 2)] private int desiredQueueDepth = 1;
        [SerializeField, Range(0, 4)] private int monkLimit = 2;
        [SerializeField, Min(0.5f)] private float rejectedCommandRetrySeconds = 2f;

        [Header("Reactive composition")]
        [SerializeField] private EnemyCompositionMemory enemyComposition = new();
        [SerializeField, Min(1f)] private float strategyReevaluationSeconds = 4f;
        [SerializeField, Min(1f)] private float desiredCompositionHoldSeconds = 12f;
        [SerializeField, Range(0.01f, 1f)] private float meaningfulDesiredChange = 0.35f;
        [SerializeField, Range(0.05f, 1f)] private float desiredResponseFactor = 0.45f;
        [SerializeField, Range(0f, 0.75f)] private float maximumReactiveInfluence = 0.45f;
        [SerializeField, Range(0f, 0.30f)] private float combatDiversityFloor = 0.12f;
        [SerializeField, Min(1)] private int meaningfulEnemySampleSize = 6;
        [SerializeField, Range(0f, 0.5f)] private float enemyBuildingEvidence = 0.12f;
        [SerializeField, Range(0.05f, 0.5f)] private float monkSupportRatio = 0.14f;
        [SerializeField, Range(0f, 0.5f)] private float enemyMonkSupportThreat = 0.08f;
        // Preserves the old sequence's 3:2:2 combat opening as the no-intel baseline.
        [SerializeField, Range(0.05f, 1f)] private float baselineWarriorWeight = 0.43f;
        [SerializeField, Range(0.05f, 1f)] private float baselineArcherWeight = 0.285f;
        [SerializeField, Range(0.05f, 1f)] private float baselineLancerWeight = 0.285f;

        private readonly List<UnitProductionQueue> producers = new();
        private readonly List<UnitType> queueBuffer = new();
        private readonly int[] livingCounts = new int[7];
        private readonly int[] queuedCounts = new int[7];
        private readonly int[] lastEvaluatedEnemyCounts = new int[5];
        private readonly float[] baselineCounts = new float[5];
        private readonly float[] counterContribution = new float[5];
        private readonly float[] desiredCounts = new float[5];
        private readonly float[] candidateCounts = new float[5];
        private readonly float[] deficits = new float[5];
        private readonly float[] candidateScores = new float[5];
        private readonly float[] threatVector = new float[5];
        private float nextProductionAttemptTime;
        private float nextRallyAttemptTime;
        private float nextStrategyEvaluationTime;
        private float desiredHoldUntil;
        private bool hasDesiredComposition;
        private bool hasStagingCell;
        private Vector2Int stagingCell = new(-1, -1);
        private Vector2Int excludedStagingCell = new(-1, -1);
        private float stagingExclusionUntil;
        private int strategicMilitaryPopulation;
        private float strategicMilitaryTargetUntil;
        private UnitType? lastPreferredMilitary;

        public int TargetPawnCount => targetPawnCount;
        public void ConfigureDifficultyTargets(int pawns, int military)
        {
            targetPawnCount = Mathf.Max(3, pawns);
            targetMilitaryPopulation = Mathf.Max(6, military);
        }
        public int TargetMilitaryPopulation => Mathf.Max(targetMilitaryPopulation,
            strategicMilitaryPopulation);
        public int DesiredQueueDepth => desiredQueueDepth;
        public int MonkLimit => monkLimit;
        public float StrategyReevaluationSeconds => strategyReevaluationSeconds;
        public float DesiredCompositionHoldSeconds => desiredCompositionHoldSeconds;
        public float MeaningfulDesiredChange => meaningfulDesiredChange;
        public float MaximumReactiveInfluence => maximumReactiveInfluence;
        public float CombatDiversityFloor => combatDiversityFloor;
        public float MonkSupportRatio => monkSupportRatio;
        public float BaselineWarriorWeight => baselineWarriorWeight;
        public float BaselineArcherWeight => baselineArcherWeight;
        public float BaselineLancerWeight => baselineLancerWeight;
        public EnemyCompositionMemory EnemyComposition => enemyComposition;
        public int LivingPawnCount { get; private set; }
        public int QueuedPawnCount => queuedCounts[(int)UnitType.Pawn];
        public int MilitaryPopulation { get; private set; }
        public int QueuedMilitaryPopulation { get; private set; }
        public int EffectiveFreePopulation { get; private set; }
        public bool PopulationBlocked { get; private set; }
        public UnitType? ActiveProductionDecision { get; private set; }
        public UnitType? PendingEconomicDemand { get; private set; }
        public Vector2Int RallyStagingCell => stagingCell;
        public bool HasRallyStagingCell => hasStagingCell;
        public uint SuccessfulTrainSerial { get; private set; }
        public float LastSuccessfulTrainTime { get; private set; } = -1f;
        public string DecisionReason { get; private set; } = "NotEvaluated";
        public EnemyIntelSource IntelSource { get; private set; }
        public float IntelConfidence { get; private set; }
        public float IntelAge { get; private set; } = -1f;
        public UnitType? PreferredMilitaryUnit { get; private set; }
        public static long EnqueueAttemptCount { get; private set; }
        public static long SuccessfulEnqueueCount { get; private set; }
        public static long RejectedOrDedupedCount { get; private set; }
        public static long InfrastructureRequestCount { get; private set; }

        public int LivingCount(UnitType type) => livingCounts[(int)type];
        public int QueuedCount(UnitType type) => queuedCounts[(int)type];
        public float DesiredCount(UnitType type) => IsMilitary(type)
            ? desiredCounts[(int)type] : 0f;
        public float CompositionDeficit(UnitType type) => IsMilitary(type)
            ? deficits[(int)type] : 0f;
        public float CandidateScore(UnitType type) => IsMilitary(type)
            ? candidateScores[(int)type] : float.NegativeInfinity;
        public float Threat(UnitType type) => IsMilitary(type)
            ? threatVector[(int)type] : 0f;
        public float CounterContribution(UnitType type) => IsMilitary(type)
            ? counterContribution[(int)type] : 0f;

        public static void ResetPerformanceCounters()
        {
            EnqueueAttemptCount = 0;
            SuccessfulEnqueueCount = 0;
            RejectedOrDedupedCount = 0;
            InfrastructureRequestCount = 0;
        }

        // A known guarded objective can temporarily raise the ordinary army
        // target. Expiry returns production to its configured baseline if the
        // mine becomes stale, lost, or strategically irrelevant.
        public void RequestStrategicMilitaryPopulation(int population, float now)
        {
            int requested = Mathf.Clamp(population, targetMilitaryPopulation, 16);
            if (requested != strategicMilitaryPopulation)
                nextStrategyEvaluationTime = 0f;
            strategicMilitaryPopulation = requested;
            strategicMilitaryTargetUntil = now + 12f;
        }

        public void InvalidateRallyStagingCell(bool avoidCurrent = false, float now = 0f)
        {
            if (avoidCurrent && hasStagingCell)
            {
                excludedStagingCell = stagingCell;
                stagingExclusionUntil = now + 20f;
            }
            hasStagingCell = false;
            stagingCell = new Vector2Int(-1, -1);
            nextRallyAttemptTime = 0f;
        }

        public void Reset()
        {
            producers.Clear();
            queueBuffer.Clear();
            Array.Clear(livingCounts, 0, livingCounts.Length);
            Array.Clear(queuedCounts, 0, queuedCounts.Length);
            Array.Clear(lastEvaluatedEnemyCounts, 0, lastEvaluatedEnemyCounts.Length);
            Array.Clear(baselineCounts, 0, baselineCounts.Length);
            Array.Clear(counterContribution, 0, counterContribution.Length);
            Array.Clear(desiredCounts, 0, desiredCounts.Length);
            Array.Clear(candidateCounts, 0, candidateCounts.Length);
            Array.Clear(deficits, 0, deficits.Length);
            Array.Clear(candidateScores, 0, candidateScores.Length);
            Array.Clear(threatVector, 0, threatVector.Length);
            enemyComposition?.Reset();
            nextProductionAttemptTime = nextRallyAttemptTime = 0f;
            nextStrategyEvaluationTime = desiredHoldUntil = 0f;
            hasDesiredComposition = false;
            hasStagingCell = false;
            stagingCell = new Vector2Int(-1, -1);
            excludedStagingCell = new Vector2Int(-1, -1);
            stagingExclusionUntil = 0f;
            strategicMilitaryPopulation = 0;
            strategicMilitaryTargetUntil = 0f;
            LivingPawnCount = MilitaryPopulation = QueuedMilitaryPopulation = 0;
            EffectiveFreePopulation = 0;
            PopulationBlocked = false;
            ActiveProductionDecision = PendingEconomicDemand = PreferredMilitaryUnit = null;
            lastPreferredMilitary = null;
            SuccessfulTrainSerial = 0;
            LastSuccessfulTrainTime = -1f;
            DecisionReason = "Reset";
            IntelSource = EnemyIntelSource.Unknown;
            IntelConfidence = 0f;
            IntelAge = -1f;
        }

        public void Tick(CommanderDecisionContext context) => Tick(context, null);

        public void Tick(CommanderDecisionContext context,
            BuildingCommanderPolicy buildingPolicy)
        {
            if (strategicMilitaryPopulation > 0
                && context.Time >= strategicMilitaryTargetUntil)
            {
                strategicMilitaryPopulation = 0;
                nextStrategyEvaluationTime = 0f;
            }
            RefreshState(context.Knowledge);
            MaintainMilitaryRallies(context);
            enemyComposition ??= new EnemyCompositionMemory();
            bool newIntel;
            using (EnemyCompositionMarker.Auto())
                newIntel = enemyComposition.Refresh(context.Knowledge, context.Time);
            RefreshIntelDiagnostics(context.Time);
            bool majorIntel = newIntel && IsMajorIntelligenceChange();
            if (!hasDesiredComposition || context.Time >= nextStrategyEvaluationTime
                || majorIntel)
                using (DesiredCompositionMarker.Auto())
                    EvaluateDesiredComposition(context, majorIntel);
            using (CandidateScoreMarker.Auto())
                RefreshDeficitsAndScores(context);

            ActiveProductionDecision = null;
            PopulationBlocked = false;
            if (context.Time < nextProductionAttemptTime) return;
            if (LivingPawnCount + QueuedPawnCount < targetPawnCount)
            {
                DecisionReason = "BaselineNeed:Pawn";
                TryRequest(context, UnitType.Pawn);
                return;
            }
            if (MilitaryPopulation + QueuedMilitaryPopulation >= TargetMilitaryPopulation)
            {
                PendingEconomicDemand = null;
                DecisionReason = "MilitaryTargetReached";
                return;
            }

            UnitType? ideal = SelectPreferredMilitary(false);
            PreferredMilitaryUnit = ideal;
            if (!ideal.HasValue)
            {
                PendingEconomicDemand = null;
                DecisionReason = "NoCompositionDeficit";
                return;
            }

            UnitType selected = ideal.Value;
            if (FindProducer(selected) == null)
            {
                BuildingType required = RequiredProducer(selected);
                if (buildingPolicy != null && buildingPolicy.RequestProductionInfrastructure(
                    required, context.Time)) InfrastructureRequestCount++;
                UnitType? fallback = SelectPreferredMilitary(true);
                DecisionReason = fallback.HasValue
                    ? $"InfrastructureRequested:{required};Fallback:{fallback.Value}"
                    : $"InfrastructureRequested:{required};ProducerUnavailable";
                if (!fallback.HasValue)
                {
                    // There is no legal producer yet, so do not reserve an
                    // impossible unit cost indefinitely. Building demand owns
                    // the near-term reservation until a producer is ready.
                    PendingEconomicDemand = null;
                    nextProductionAttemptTime = context.Time + rejectedCommandRetrySeconds;
                    RejectedOrDedupedCount++;
                    return;
                }
                selected = fallback.Value;
            }
            else DecisionReason = counterContribution[(int)selected] > 0.05f
                ? "CompositionDeficit+CounterPressure" : "BaselineNeed+CompositionDeficit";
            lastPreferredMilitary = selected;
            TryRequest(context, selected);
        }

        private void RefreshState(FactionKnowledge knowledge)
        {
            Array.Clear(livingCounts, 0, livingCounts.Length);
            Array.Clear(queuedCounts, 0, queuedCounts.Length);
            producers.Clear();
            MilitaryPopulation = QueuedMilitaryPopulation = 0;
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                HealthComponent health = units[i] != null
                    ? units[i].GetComponent<HealthComponent>() : null;
                UnitCombatData data = health?.CombatData;
                if (data == null || (int)data.UnitType >= livingCounts.Length) continue;
                livingCounts[(int)data.UnitType]++;
                if (IsMilitary(data.UnitType)) MilitaryPopulation += data.PopulationCost;
            }
            LivingPawnCount = livingCounts[(int)UnitType.Pawn];
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost host = buildings[i];
                if (host == null || !host.IsAlive) continue;
                UnitProductionQueue queue = host.HostTransform.GetComponent<UnitProductionQueue>();
                if (queue == null) continue;
                if (host.IsProductionReady) producers.Add(queue);
                queue.CopyQueue(queueBuffer);
                for (int item = 0; item < queueBuffer.Count; item++)
                {
                    UnitType type = queueBuffer[item];
                    if ((int)type >= queuedCounts.Length) continue;
                    queuedCounts[(int)type]++;
                    UnitCombatData data = queue.GetData(type);
                    if (data != null && IsMilitary(type))
                        QueuedMilitaryPopulation += data.PopulationCost;
                }
            }
            producers.Sort(CompareProducers);
            FactionPopulation population = knowledge.Population;
            EffectiveFreePopulation = population != null
                ? population.Cap - population.Used - population.Reserved : 0;
        }

        private void EvaluateDesiredComposition(CommanderDecisionContext context,
            bool majorIntel)
        {
            nextStrategyEvaluationTime = context.Time + strategyReevaluationSeconds;
            int monkTarget = CalculateMonkSupportTarget();
            BuildBaseline(monkTarget);
            BuildThreatVector(context.Knowledge, context.Time);
            float directThreat = threatVector[(int)UnitType.Warrior]
                + threatVector[(int)UnitType.Archer]
                + threatVector[(int)UnitType.Lancer];
            float significance = Mathf.Clamp01(directThreat
                / Mathf.Max(1, meaningfulEnemySampleSize));
            float reactiveMix = maximumReactiveInfluence * significance;
            float[] raw = candidateCounts;
            Array.Clear(raw, 0, raw.Length);
            float counterTotal = 0f;
            for (int c = 0; c < 3; c++)
            {
                UnitType candidate = MilitaryTypes[c];
                float utility = 0f;
                for (int e = 0; e < 3; e++)
                {
                    UnitType enemy = MilitaryTypes[e];
                    utility += threatVector[(int)enemy]
                        * CombatMatchupEvaluator.Effectiveness(candidate, enemy);
                }
                raw[(int)candidate] = directThreat > 0f ? utility / directThreat : 1f;
                counterTotal += raw[(int)candidate];
            }
            float nonMonkSlots = Mathf.Max(1f, TargetMilitaryPopulation - monkTarget);
            float combatTotal = 0f;
            for (int i = 0; i < 3; i++)
            {
                UnitType type = MilitaryTypes[i];
                float counterWeight = counterTotal > 0f
                    ? raw[(int)type] / counterTotal : 1f / 3f;
                counterWeight = Mathf.Max(combatDiversityFloor, counterWeight);
                raw[(int)type] = Mathf.Lerp(
                    baselineCounts[(int)type] / nonMonkSlots, counterWeight,
                    reactiveMix);
                combatTotal += raw[(int)type];
            }
            for (int i = 0; i < 3; i++)
            {
                UnitType type = MilitaryTypes[i];
                raw[(int)type] = raw[(int)type] / combatTotal * nonMonkSlots;
                counterContribution[(int)type] = raw[(int)type]
                    - baselineCounts[(int)type];
            }
            raw[(int)UnitType.Monk] = monkTarget;
            counterContribution[(int)UnitType.Monk] = 0f;

            float maximumDelta = 0f;
            for (int i = 0; i < MilitaryTypes.Length; i++)
            {
                int index = (int)MilitaryTypes[i];
                maximumDelta = Mathf.Max(maximumDelta,
                    Mathf.Abs(raw[index] - desiredCounts[index]));
            }
            if (!hasDesiredComposition)
            {
                Array.Copy(raw, desiredCounts, raw.Length);
                hasDesiredComposition = true;
                desiredHoldUntil = context.Time + desiredCompositionHoldSeconds;
            }
            else if (maximumDelta >= meaningfulDesiredChange
                && (context.Time >= desiredHoldUntil || majorIntel))
            {
                for (int i = 0; i < MilitaryTypes.Length; i++)
                {
                    int index = (int)MilitaryTypes[i];
                    desiredCounts[index] = Mathf.Lerp(desiredCounts[index], raw[index],
                        desiredResponseFactor);
                }
                desiredHoldUntil = context.Time + desiredCompositionHoldSeconds;
            }
            for (int i = 0; i < MilitaryTypes.Length; i++)
            {
                int index = (int)MilitaryTypes[i];
                lastEvaluatedEnemyCounts[index] = enemyComposition.ObservedCount(
                    MilitaryTypes[i]);
            }
        }

        private int CalculateMonkSupportTarget()
        {
            int projectedNonMonks = livingCounts[(int)UnitType.Warrior]
                + livingCounts[(int)UnitType.Archer]
                + livingCounts[(int)UnitType.Lancer]
                + queuedCounts[(int)UnitType.Warrior]
                + queuedCounts[(int)UnitType.Archer]
                + queuedCounts[(int)UnitType.Lancer];
            if (projectedNonMonks < 3 || monkLimit <= 0) return 0;
            return Mathf.Min(monkLimit,
                Mathf.CeilToInt(projectedNonMonks * monkSupportRatio));
        }

        private void BuildBaseline(int monkTarget)
        {
            Array.Clear(baselineCounts, 0, baselineCounts.Length);
            float combat = Mathf.Max(1f, TargetMilitaryPopulation - monkTarget);
            float total = Mathf.Max(0.01f, baselineWarriorWeight
                + baselineArcherWeight + baselineLancerWeight);
            baselineCounts[(int)UnitType.Warrior] = combat
                * baselineWarriorWeight / total;
            baselineCounts[(int)UnitType.Archer] = combat
                * baselineArcherWeight / total;
            baselineCounts[(int)UnitType.Lancer] = combat
                * baselineLancerWeight / total;
            baselineCounts[(int)UnitType.Monk] = monkTarget;
        }

        private void BuildThreatVector(FactionKnowledge knowledge, float now)
        {
            Array.Clear(threatVector, 0, threatVector.Length);
            threatVector[(int)UnitType.Warrior] = enemyComposition.EvidenceCount(
                UnitType.Warrior, now);
            threatVector[(int)UnitType.Archer] = enemyComposition.EvidenceCount(
                UnitType.Archer, now);
            threatVector[(int)UnitType.Lancer] = enemyComposition.EvidenceCount(
                UnitType.Lancer, now);
            threatVector[(int)UnitType.Monk] = enemyComposition.EvidenceCount(
                UnitType.Monk, now);
            float support = 1f + Mathf.Min(0.35f,
                threatVector[(int)UnitType.Monk] * enemyMonkSupportThreat);
            for (int i = 0; i < 3; i++) threatVector[i] *= support;

            IReadOnlyList<BuildingKnowledgeSnapshot> buildings = knowledge.KnownEnemyBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingKnowledgeSnapshot item = buildings[i];
                float age = Mathf.Max(0f, now - item.LastSeenTime);
                float stale = Mathf.Clamp01(1f - age
                    / Mathf.Max(1f, enemyComposition.DecaySeconds));
                float evidence = enemyBuildingEvidence * stale;
                switch (item.BuildingType)
                {
                    case BuildingType.Barracks:
                        threatVector[(int)UnitType.Warrior] += evidence * 0.5f;
                        threatVector[(int)UnitType.Lancer] += evidence * 0.5f;
                        break;
                    case BuildingType.Archery:
                        threatVector[(int)UnitType.Archer] += evidence;
                        break;
                    case BuildingType.Monastery:
                        threatVector[(int)UnitType.Monk] += evidence;
                        break;
                }
            }
        }

        private void RefreshDeficitsAndScores(CommanderDecisionContext context)
        {
            for (int i = 0; i < MilitaryTypes.Length; i++)
            {
                UnitType type = MilitaryTypes[i];
                int index = (int)type;
                int projected = livingCounts[index] + queuedCounts[index];
                deficits[index] = Mathf.Max(0f, desiredCounts[index] - projected);
                UnitProductionQueue producer = FindProducer(type);
                UnitCombatData data = producer != null ? producer.GetData(type) : FindData(type);
                bool affordable = data != null && context.Knowledge.Stockpile != null
                    && context.Knowledge.Stockpile.CanAfford(data.FoodCost,
                        data.WoodCost, data.GoldCost);
                candidateScores[index] = deficits[index] * 4f
                    + Mathf.Max(0f, counterContribution[index]) * 1.5f
                    + baselineCounts[index] * 0.15f
                    + (type == UnitType.Monk ? deficits[index] * 0.75f : 0f)
                    + (producer != null ? 0.35f : -0.8f)
                    + (affordable ? 0.25f : -0.35f)
                    + (EffectiveFreePopulation > 0 ? 0f : -100f)
                    - queuedCounts[index] * 0.5f
                    + (lastPreferredMilitary == type ? 0.20f : 0f);
                if (type == UnitType.Monk && projected >= monkLimit)
                    candidateScores[index] = float.NegativeInfinity;
            }
        }

        private UnitType? SelectPreferredMilitary(bool requireProducer)
        {
            UnitType? best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < MilitaryTypes.Length; i++)
            {
                UnitType type = MilitaryTypes[i];
                if (deficits[(int)type] <= 0.01f
                    || (requireProducer && FindProducer(type) == null)) continue;
                float score = candidateScores[(int)type];
                if (!best.HasValue || score > bestScore + 0.0001f
                    || (Mathf.Abs(score - bestScore) <= 0.0001f && type < best.Value))
                {
                    best = type;
                    bestScore = score;
                }
            }
            return best;
        }

        private bool IsMajorIntelligenceChange()
        {
            int delta = 0;
            for (int i = 0; i < MilitaryTypes.Length; i++)
            {
                UnitType type = MilitaryTypes[i];
                delta += Mathf.Abs(enemyComposition.ObservedCount(type)
                    - lastEvaluatedEnemyCounts[(int)type]);
            }
            return delta >= 3;
        }

        private void RefreshIntelDiagnostics(float now)
        {
            IntelConfidence = enemyComposition.ConfidenceAt(now);
            IntelSource = enemyComposition.SourceAt(now);
            IntelAge = enemyComposition.HasObservation
                ? Mathf.Max(0f, now - enemyComposition.LastSeenTime) : -1f;
        }

        private void TryRequest(CommanderDecisionContext context, UnitType type)
        {
            UnitProductionQueue producer = FindProducer(type);
            UnitCombatData data = producer != null ? producer.GetData(type) : FindData(type);
            PendingEconomicDemand = type;
            if (producer == null || data == null)
            {
                DecisionReason += ";ProducerUnavailable";
                RejectedOrDedupedCount++;
                nextProductionAttemptTime = context.Time + rejectedCommandRetrySeconds;
                return;
            }
            if (producer.Count >= desiredQueueDepth)
            {
                DecisionReason += ";QueueFullOrDeduped";
                RejectedOrDedupedCount++;
                nextProductionAttemptTime = context.Time + rejectedCommandRetrySeconds;
                return;
            }
            if (EffectiveFreePopulation < data.PopulationCost)
            {
                PopulationBlocked = true;
                DecisionReason += ";PopulationBlocked";
                RejectedOrDedupedCount++;
                nextProductionAttemptTime = context.Time + rejectedCommandRetrySeconds;
                return;
            }
            ActiveProductionDecision = type;
            EnqueueAttemptCount++;
            CommanderCommandResult result = context.Commands.TryTrain(producer, type);
            if (result == CommanderCommandResult.Accepted)
            {
                PendingEconomicDemand = null;
                SuccessfulTrainSerial++;
                SuccessfulEnqueueCount++;
                LastSuccessfulTrainTime = context.Time;
                return;
            }
            RejectedOrDedupedCount++;
            if (result == CommanderCommandResult.RejectedPopulationBlocked)
                PopulationBlocked = true;
            DecisionReason += result == CommanderCommandResult.RejectedInsufficientResources
                ? ";ResourceUnavailable" : ";" + result;
            nextProductionAttemptTime = context.Time + rejectedCommandRetrySeconds;
        }

        public bool TryGetPendingEconomicCost(out int food, out int wood,
            out int gold, out int population)
        {
            food = wood = gold = population = 0;
            if (!PendingEconomicDemand.HasValue) return false;
            UnitCombatData data = FindData(PendingEconomicDemand.Value);
            if (data == null) return false;
            food = data.FoodCost;
            wood = data.WoodCost;
            gold = data.GoldCost;
            population = data.PopulationCost;
            return true;
        }

        private UnitProductionQueue FindProducer(UnitType type)
        {
            for (int i = 0; i < producers.Count; i++)
            {
                IProductionHost host = producers[i].ProductionHost;
                if (host != null && UnitProductionQueue.CanProduce(host.Type, type))
                    return producers[i];
            }
            return null;
        }

        private UnitCombatData FindData(UnitType type)
        {
            for (int i = 0; i < producers.Count; i++)
            {
                UnitCombatData data = producers[i].GetData(type);
                if (data != null) return data;
            }
            return null;
        }

        private static BuildingType RequiredProducer(UnitType type) => type switch
        {
            UnitType.Warrior or UnitType.Lancer => BuildingType.Barracks,
            UnitType.Archer => BuildingType.Archery,
            UnitType.Monk => BuildingType.Monastery,
            _ => BuildingType.Castle
        };

        private static bool IsMilitary(UnitType type)
            => type is UnitType.Warrior or UnitType.Archer or UnitType.Lancer or UnitType.Monk;

        private void MaintainMilitaryRallies(CommanderDecisionContext context)
        {
            if (context.Time < nextRallyAttemptTime) return;
            if (hasStagingCell && !IsValidStagingCell(context.Knowledge, stagingCell))
                InvalidateRallyStagingCell();
            if (!hasStagingCell && !TryFindStagingCell(context.Knowledge,
                    context.Time, out stagingCell))
            {
                nextRallyAttemptTime = context.Time + rejectedCommandRetrySeconds;
                return;
            }
            hasStagingCell = true;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return;
            Vector3 destination = grid.GridToWorld(stagingCell.x, stagingCell.y);
            for (int i = 0; i < producers.Count; i++)
            {
                IProductionHost host = producers[i].ProductionHost;
                if (host == null || host.Type == BuildingType.Castle) continue;
                BuildingRallyPoint rally = host is Component component
                    ? component.GetComponent<BuildingRallyPoint>() : null;
                if (rally == null || (rally.HasRallyPoint && rally.RallyCell == stagingCell))
                    continue;
                CommanderCommandResult result = context.Commands.TrySetRally(rally, destination);
                if (result == CommanderCommandResult.Accepted) return;
                hasStagingCell = false;
                nextRallyAttemptTime = context.Time + rejectedCommandRetrySeconds;
                return;
            }
        }

        private bool TryFindStagingCell(FactionKnowledge knowledge, float now,
            out Vector2Int result)
        {
            using var marker = StagingSelectionMarker.Auto();
            result = default;
            GridManager grid = knowledge.TeamVision?.GridManager;
            if (grid == null || !TryGetCastle(knowledge, out IDefensiveBuildingHost castle))
                return false;
            Vector2Int center = castle.FootprintOrigin
                + new Vector2Int(castle.Footprint.x / 2, castle.Footprint.y / 2);
            int direction = center.x >= grid.Width / 2 ? -1 : 1;
            for (int distance = 5; distance <= 8; distance++)
            for (int side = 0; side <= distance; side++)
            for (int sign = side == 0 ? 0 : -1; sign <= 1; sign += 2)
            {
                Vector2Int cellPosition = center + new Vector2Int(direction * distance,
                    side * sign);
                if (now < stagingExclusionUntil
                    && cellPosition == excludedStagingCell) continue;
                GridCell cell = grid.GetCell(cellPosition.x, cellPosition.y);
                if (cell == null || !TerrainRules.IsStableStandingCell(cell)
                    || !knowledge.IsCellExplored(cellPosition)
                    || IsNearKnownResource(knowledge, cellPosition, 2)
                    || IsInsideOwnBuilding(knowledge, cellPosition)) continue;
                result = cellPosition;
                return true;
            }
            // Some authored Castle courts have no suitable tile in the narrow
            // horizontal band above. Search bounded rings toward map center and
            // check one army member's actual route before accepting a fallback.
            Vector2 towardCenter = new(grid.Width * 0.5f - center.x,
                grid.Height * 0.5f - center.y);
            towardCenter = towardCenter.sqrMagnitude > 0.001f
                ? towardCenter.normalized : Vector2.left;
            UnitController representative = null;
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitType? type = units[i]?.GetComponent<HealthComponent>()
                    ?.CombatData?.UnitType;
                if (type is not (UnitType.Warrior or UnitType.Archer
                    or UnitType.Lancer or UnitType.Monk)) continue;
                representative = units[i];
                break;
            }
            PathfindingSystem paths = grid.GetComponent<PathfindingSystem>();
            HashSet<GridCell> reachable = null;
            for (int radius = 4; radius <= 12; radius++)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    Vector2Int best = default;
                    float bestScore = float.NegativeInfinity;
                    bool found = false;
                    for (int y = -radius; y <= radius; y++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius) continue;
                        Vector2Int candidate = center + new Vector2Int(x, y);
                        GridCell cell = grid.GetCell(candidate.x, candidate.y);
                        if (cell == null || !TerrainRules.IsStableStandingCell(cell)
                            || !knowledge.IsCellExplored(candidate)
                            || now < stagingExclusionUntil
                                && candidate == excludedStagingCell
                            || IsNearKnownResource(knowledge, candidate, 2)
                            || IsInsideOwnBuilding(knowledge, candidate)
                            || reachable != null && !reachable.Contains(cell)) continue;
                        float score = Vector2.Dot(new Vector2(x, y).normalized,
                            towardCenter);
                        if (!found || score > bestScore)
                        { best = candidate; bestScore = score; found = true; }
                    }
                    if (!found) break;
                    if (representative != null && paths != null
                        && reachable == null
                        && paths.FindPath(grid.WorldToGrid(
                                representative.transform.position), best, null,
                            out HashSet<GridCell> exhausted).Count == 0)
                    {
                        reachable = exhausted;
                        if (reachable != null) continue;
                        break;
                    }
                    result = best;
                    return true;
                }
            }
            return false;
        }

        public static bool IsValidStagingCell(FactionKnowledge knowledge,
            Vector2Int position)
        {
            GridCell cell = knowledge?.TeamVision?.GridManager?.GetCell(position.x,
                position.y);
            return cell != null && TerrainRules.IsStableStandingCell(cell)
                && knowledge.IsCellExplored(position)
                && !IsNearKnownResource(knowledge, position, 2)
                && !IsInsideOwnBuilding(knowledge, position);
        }

        private static bool IsNearKnownResource(FactionKnowledge knowledge,
            Vector2Int cell, int radius)
        {
            IReadOnlyList<ResourceKnowledgeSnapshot> resources = knowledge.KnownResources;
            for (int i = 0; i < resources.Count; i++)
                if (Mathf.Abs(resources[i].LogicalCell.x - cell.x) <= radius
                    && Mathf.Abs(resources[i].LogicalCell.y - cell.y) <= radius) return true;
            return false;
        }

        private static bool IsInsideOwnBuilding(FactionKnowledge knowledge, Vector2Int cell)
        {
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost host = buildings[i];
                if (host == null || !host.IsAlive) continue;
                Vector2Int origin = host.FootprintOrigin;
                Vector2Int size = host.Footprint;
                if (cell.x >= origin.x && cell.x < origin.x + size.x
                    && cell.y >= origin.y && cell.y < origin.y + size.y) return true;
            }
            return false;
        }

        private static bool TryGetCastle(FactionKnowledge knowledge,
            out IDefensiveBuildingHost castle)
        {
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i] != null && buildings[i].IsAlive
                    && buildings[i].Type == BuildingType.Castle)
                {
                    castle = buildings[i];
                    return true;
                }
            castle = null;
            return false;
        }

        private static int CompareProducers(UnitProductionQueue first,
            UnitProductionQueue second)
        {
            IProductionHost a = first.ProductionHost;
            IProductionHost b = second.ProductionHost;
            int type = a.Type.CompareTo(b.Type);
            if (type != 0) return type;
            int y = a.FootprintOrigin.y.CompareTo(b.FootprintOrigin.y);
            return y != 0 ? y : a.FootprintOrigin.x.CompareTo(b.FootprintOrigin.x);
        }
    }
}
