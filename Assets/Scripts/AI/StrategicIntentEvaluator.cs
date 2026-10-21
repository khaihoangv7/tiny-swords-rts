using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    public enum StrategicIntent
    {
        DevelopEconomy,
        Scout,
        Fortify,
        PrepareAttack,
        Attack,
        Recover,
        SecureGoldMine
    }

    public readonly struct StrategicIntentScores
    {
        public StrategicIntentScores(float economy, float scout, float fortify,
            float prepare, float attack, float recover, float secureGoldMine = 0f)
        {
            DevelopEconomy = Mathf.Clamp01(economy);
            Scout = Mathf.Clamp01(scout);
            Fortify = Mathf.Clamp01(fortify);
            PrepareAttack = Mathf.Clamp01(prepare);
            Attack = Mathf.Clamp01(attack);
            Recover = Mathf.Clamp01(recover);
            SecureGoldMine = Mathf.Clamp01(secureGoldMine);
        }

        public float DevelopEconomy { get; }
        public float Scout { get; }
        public float Fortify { get; }
        public float PrepareAttack { get; }
        public float Attack { get; }
        public float Recover { get; }
        public float SecureGoldMine { get; }

        public float Get(StrategicIntent intent) => intent switch
        {
            StrategicIntent.DevelopEconomy => DevelopEconomy,
            StrategicIntent.Scout => Scout,
            StrategicIntent.Fortify => Fortify,
            StrategicIntent.PrepareAttack => PrepareAttack,
            StrategicIntent.Attack => Attack,
            StrategicIntent.Recover => Recover,
            StrategicIntent.SecureGoldMine => SecureGoldMine,
            _ => 0f
        };
    }

    // A copied target record. It intentionally contains no GameObject, component or
    // Transform, so losing vision cannot turn strategic memory into a live tracker.
    public readonly struct StrategicTargetSnapshot
    {
        public StrategicTargetSnapshot(EntityId id, BuildingType type, Faction faction,
            Vector3 position, float score, float lastSeenTime, bool visible)
        {
            valid = true;
            RuntimeId = id;
            BuildingType = type;
            Faction = faction;
            LastKnownPosition = position;
            Score = score;
            LastSeenTime = lastSeenTime;
            WasVisibleWhenEvaluated = visible;
        }

        public EntityId RuntimeId { get; }
        public BuildingType BuildingType { get; }
        public Faction Faction { get; }
        public Vector3 LastKnownPosition { get; }
        public float Score { get; }
        public float LastSeenTime { get; }
        public bool WasVisibleWhenEvaluated { get; }
        public bool IsValid => valid;
        private readonly bool valid;
    }

    // One immutable, value-only view of the facts used by the evaluator. Enemy data
    // comes only from FactionKnowledge snapshots and EnemyCompositionMemory.
    public readonly struct StrategicSituationSnapshot
    {
        public StrategicSituationSnapshot(int food, int wood, int gold, int workers,
            int idleWorkers, int population, int populationCap, int infrastructure,
            int castles, int houses, int barracks, int archeries, int monasteries,
            int towers, int warriors, int archers, int lancers, int monks,
            int militaryPopulation, int queuedMilitaryPopulation, float ownStrength,
            float projectedStrength, float supportStrength, float enemyStrength,
            float intelConfidence, float intelAge, bool hasKnownTarget,
            int knownEnemyCastles, int knownEnemyProduction, int knownDangerCount,
            int missingProducerCount,
            bool baseThreatened, bool scoutActive, float exploredFraction,
            float recentLossRatio, Vector2Int stagingCell, StrategicIntent currentIntent,
            float currentIntentAge, float sampledAt)
        {
            Food = food; Wood = wood; Gold = gold;
            WorkerCount = workers; IdleWorkerCount = idleWorkers;
            Population = population; PopulationCap = populationCap;
            InfrastructureCount = infrastructure;
            CastleCount = castles; HouseCount = houses; BarracksCount = barracks;
            ArcheryCount = archeries; MonasteryCount = monasteries;
            TowerCount = towers;
            WarriorCount = warriors; ArcherCount = archers;
            LancerCount = lancers; MonkCount = monks;
            MilitaryPopulation = militaryPopulation;
            QueuedMilitaryPopulation = queuedMilitaryPopulation;
            OwnStrength = ownStrength; ProjectedStrength = projectedStrength;
            SupportStrength = supportStrength; EnemyStrength = enemyStrength;
            IntelConfidence = intelConfidence; IntelAge = intelAge;
            HasKnownTarget = hasKnownTarget;
            KnownEnemyCastleCount = knownEnemyCastles;
            KnownEnemyProductionCount = knownEnemyProduction;
            KnownDangerCount = knownDangerCount;
            MissingProducerCount = missingProducerCount;
            BaseThreatened = baseThreatened;
            ScoutActive = scoutActive; ExploredFraction = exploredFraction;
            RecentLossRatio = recentLossRatio; StagingCell = stagingCell;
            CurrentIntent = currentIntent; CurrentIntentAge = currentIntentAge;
            SampledAt = sampledAt;
        }

        public int Food { get; }
        public int Wood { get; }
        public int Gold { get; }
        public int WorkerCount { get; }
        public int IdleWorkerCount { get; }
        public int Population { get; }
        public int PopulationCap { get; }
        public int InfrastructureCount { get; }
        public int CastleCount { get; }
        public int HouseCount { get; }
        public int BarracksCount { get; }
        public int ArcheryCount { get; }
        public int MonasteryCount { get; }
        public int TowerCount { get; }
        public int WarriorCount { get; }
        public int ArcherCount { get; }
        public int LancerCount { get; }
        public int MonkCount { get; }
        public int MilitaryPopulation { get; }
        public int QueuedMilitaryPopulation { get; }
        public float OwnStrength { get; }
        public float ProjectedStrength { get; }
        public float SupportStrength { get; }
        public float EnemyStrength { get; }
        public float IntelConfidence { get; }
        public float IntelAge { get; }
        public bool HasKnownTarget { get; }
        public int KnownEnemyCastleCount { get; }
        public int KnownEnemyProductionCount { get; }
        public int KnownDangerCount { get; }
        public int MissingProducerCount { get; }
        public bool ProductionReady => MissingProducerCount == 0;
        public bool BaseThreatened { get; }
        public bool ScoutActive { get; }
        public float ExploredFraction { get; }
        public float RecentLossRatio { get; }
        public Vector2Int StagingCell { get; }
        public StrategicIntent CurrentIntent { get; }
        public float CurrentIntentAge { get; }
        public float SampledAt { get; }
    }

    [Serializable]
    public sealed class StrategicIntentEvaluator
    {
        private static readonly ProfilerMarker EvaluationMarker =
            new("TinyKingdom.AI.StrategicIntent.Evaluate");
        private static readonly ProfilerMarker SnapshotMarker =
            new("TinyKingdom.AI.StrategicSnapshot.Build");
        private static readonly ProfilerMarker TargetScoreMarker =
            new("TinyKingdom.AI.StrategicTarget.Score");
        private static readonly StrategicIntent[] Intents =
        {
            StrategicIntent.DevelopEconomy, StrategicIntent.Scout,
            StrategicIntent.Fortify, StrategicIntent.PrepareAttack,
            StrategicIntent.Attack, StrategicIntent.Recover,
            StrategicIntent.SecureGoldMine
        };

        [SerializeField, Min(1f)] private float evaluationInterval = 2f;
        [SerializeField, Min(1f)] private float minimumIntentHold = 8f;
        [SerializeField, Range(0.01f, 0.5f)] private float switchMargin = 0.12f;
        [SerializeField, Range(0.05f, 1f)] private float scoreSmoothing = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float attackReadinessThreshold = 0.65f;
        [SerializeField, Min(1f)] private float attackCommitmentSeconds = 12f;
        [SerializeField, Min(1f)] private float failedAttackRecoverySeconds = 16f;
        [SerializeField, Range(4, 48)] private int maximumTargetCandidates = 24;
        [SerializeField] private GoldMineStrategyModel goldMine = new();

        private float nextEvaluationTime;
        private float transitionTime;
        private float failedAttackTime = -999f;
        private uint observedLaunchSerial;
        private uint observedCompletionSerial;
        private uint failedLaunchSerial;
        private bool hasScores;
        private StrategicTargetSnapshot target;

        public StrategicIntent CurrentIntent { get; private set; }
            = StrategicIntent.DevelopEconomy;
        public StrategicIntent PreviousIntent { get; private set; }
            = StrategicIntent.DevelopEconomy;
        public StrategicIntentScores Scores { get; private set; }
        public StrategicSituationSnapshot Situation { get; private set; }
        public float AttackReadiness { get; private set; }
        public string DecisionReason { get; private set; } = "Initial economy";
        public string TransitionReason => DecisionReason;
        public float TransitionTime => transitionTime;
        public float TransitionGameTime => transitionTime;
        public float LastEvaluationTime { get; private set; } = -1f;
        public float IntentAge(float now) => Mathf.Max(0f, now - transitionTime);
        public float EvaluationInterval => Mathf.Max(1f, evaluationInterval);
        public float MinimumIntentHold => Mathf.Max(1f, minimumIntentHold);
        public float SwitchMargin => Mathf.Clamp(switchMargin, 0.01f, 0.5f);
        public float AttackReadinessThreshold
            => Mathf.Clamp(attackReadinessThreshold, 0.1f, 1f);
        public float AttackCommitmentSeconds => Mathf.Max(1f, attackCommitmentSeconds);
        public float FailedAttackRecoverySeconds
            => Mathf.Max(1f, failedAttackRecoverySeconds);
        public int MaximumTargetCandidates => Mathf.Clamp(maximumTargetCandidates, 4, 48);
        public bool HasStrategicTarget => target.IsValid;
        public StrategicTargetSnapshot StrategicTarget => target;
        public GoldMineStrategyModel GoldMine => goldMine;

        public void Reset()
        {
            CurrentIntent = PreviousIntent = StrategicIntent.DevelopEconomy;
            Scores = default;
            Situation = default;
            target = default;
            AttackReadiness = 0f;
            DecisionReason = "Reset";
            nextEvaluationTime = transitionTime = 0f;
            failedAttackTime = -999f;
            observedLaunchSerial = observedCompletionSerial = 0;
            failedLaunchSerial = 0;
            hasScores = false;
            LastEvaluationTime = -1f;
            goldMine?.Reset();
        }

        public void RequestImmediateReevaluation()
        {
            nextEvaluationTime = 0f;
        }

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles,
            EconomicDemandModel demand, EconomyCommanderPolicy economy,
            BuildingCommanderPolicy buildings, ProductionCommanderPolicy production,
            ScoutingCommanderPolicy scouting, MilitaryCommanderPolicy military,
            StrategicSafetyMap safety)
        {
            if (context.Knowledge == null || production == null || military == null) return;
            ObserveOperationOutcome(context.Time, military);

            bool emergency = HasBaseThreat(context.Knowledge, military);
            bool withdrawal = military.Army.State is ArmyGroupState.Retreating
                or ArmyGroupState.Recovering;
            if (context.Time < nextEvaluationTime && !emergency && !withdrawal) return;
            nextEvaluationTime = context.Time + EvaluationInterval;

            using var evaluation = EvaluationMarker.Auto();
            LastEvaluationTime = context.Time;

            using (TargetScoreMarker.Auto())
                target = SelectTarget(context, military, safety);
            goldMine ??= new GoldMineStrategyModel();
            goldMine.Evaluate(context, military, production, safety);
            using (SnapshotMarker.Auto())
                Situation = BuildSnapshot(context, roles, demand, economy, buildings,
                    production, scouting, military, safety, emergency);
            AttackReadiness = CalculateAttackReadiness(Situation, production, military,
                safety, target);
            StrategicIntentScores raw = CalculateScores(Situation, demand, production,
                scouting, military, context.Time);
            Scores = hasScores ? Smooth(Scores, raw, scoreSmoothing) : raw;
            hasScores = true;

            StrategicIntent desired;
            string reason;
            bool majorOverride = false;
            if (emergency)
            {
                desired = StrategicIntent.Fortify;
                reason = "BaseThreat";
                majorOverride = true;
            }
            else if (withdrawal || context.Time - failedAttackTime
                < FailedAttackRecoverySeconds)
            {
                desired = StrategicIntent.Recover;
                reason = withdrawal ? "StrategicRetreat" : "AttackFailed";
                majorOverride = true;
            }
            else if (CurrentIntent == StrategicIntent.SecureGoldMine
                && military.HasOffensiveTarget
                && military.OffensiveTarget.BuildingType == BuildingType.GoldMine
                && (military.Army.State is ArmyGroupState.PreparingAttack
                    or ArmyGroupState.Advancing or ArmyGroupState.Assaulting))
            {
                desired = StrategicIntent.SecureGoldMine;
                reason = "MineOperationCommitted";
            }
            else if (CurrentIntent == StrategicIntent.Attack
                && (military.Army.State is ArmyGroupState.PreparingAttack
                    or ArmyGroupState.Advancing or ArmyGroupState.Assaulting)
                && context.Time - military.LastOffensiveLaunchTime
                    < AttackCommitmentSeconds)
            {
                desired = StrategicIntent.Attack;
                reason = "AttackCommitted";
            }
            else
            {
                desired = BestIntent(Scores);
                reason = ReasonFor(desired, Situation);
            }

            if (desired == StrategicIntent.Attack
                && (!target.IsValid || AttackReadiness < AttackReadinessThreshold))
            {
                desired = target.IsValid ? StrategicIntent.PrepareAttack
                    : StrategicIntent.Scout;
                reason = target.IsValid ? "ArmyNearlyReady" : "TargetInvalid";
                if (!target.IsValid) majorOverride = true;
            }
            else if (desired == StrategicIntent.Attack
                && CurrentIntent != StrategicIntent.PrepareAttack
                && CurrentIntent != StrategicIntent.Attack)
            {
                desired = StrategicIntent.PrepareAttack;
                reason = "ArmyNearlyReady";
            }

            ApplyTransition(desired, reason, context.Time, majorOverride);
        }

        public bool TryGetTarget(out StrategicTargetSnapshot result)
        {
            result = target;
            return target.IsValid;
        }

        public static StrategicIntent ChooseForValidation(StrategicIntentScores scores)
            => BestIntent(scores);

        private void ObserveOperationOutcome(float now, MilitaryCommanderPolicy military)
        {
            if (military.OffensiveLaunchSerial != observedLaunchSerial)
                observedLaunchSerial = military.OffensiveLaunchSerial;
            if (military.TargetCompletionSerial != observedCompletionSerial)
            {
                observedCompletionSerial = military.TargetCompletionSerial;
                failedAttackTime = -999f;
            }
            if (observedLaunchSerial > observedCompletionSerial
                && failedLaunchSerial != observedLaunchSerial
                && military.CurrentRetreatReason != RetreatReason.None)
            {
                failedAttackTime = now;
                failedLaunchSerial = observedLaunchSerial;
            }
        }

        private StrategicSituationSnapshot BuildSnapshot(CommanderDecisionContext context,
            CommanderPawnRoles roles, EconomicDemandModel demand,
            EconomyCommanderPolicy economy, BuildingCommanderPolicy buildings,
            ProductionCommanderPolicy production, ScoutingCommanderPolicy scouting,
            MilitaryCommanderPolicy military, StrategicSafetyMap safety,
            bool baseThreatened)
        {
            FactionKnowledge knowledge = context.Knowledge;
            FactionPopulation population = knowledge.Population;
            int infrastructure = 0;
            int castles = 0, houses = 0, barracks = 0, archeries = 0;
            int monasteries = 0, towers = 0;
            IReadOnlyList<IDefensiveBuildingHost> ownBuildings = knowledge.OwnBuildings;
            for (int i = 0; i < ownBuildings.Count; i++)
            {
                IDefensiveBuildingHost building = ownBuildings[i];
                if (building == null || !building.IsAlive) continue;
                infrastructure++;
                switch (building.Type)
                {
                    case BuildingType.Castle: castles++; break;
                    case BuildingType.House: houses++; break;
                    case BuildingType.Barracks: barracks++; break;
                    case BuildingType.Archery: archeries++; break;
                    case BuildingType.Monastery: monasteries++; break;
                    case BuildingType.Tower: towers++; break;
                }
            }

            float ownStrength = CombatStrengthEvaluator.OwnArmy(
                military.Army.Members, military.CombatStrength,
                out _, out float support);
            float queued = QueuedStrength(production, military.CombatStrength);
            float enemy = RememberedEnemyStrength(production.EnemyComposition,
                military.CombatStrength, context.Time);
            float intelAge = production.EnemyComposition?.LastSeenTime >= 0f
                ? Mathf.Max(0f, context.Time
                    - production.EnemyComposition.LastSeenTime) : -1f;
            int workers = production.LivingPawnCount;
            int assigned = economy?.AssignmentCount ?? 0;
            float explored = knowledge.TotalCellCount > 0
                ? Mathf.Clamp01((float)knowledge.ExploredCellCount
                    / knowledge.TotalCellCount) : 0f;
            float loss = military.CurrentRetreatReason != RetreatReason.None
                ? 1f - Mathf.Clamp01(military.RemainingForceRatio) : 0f;
            Vector2Int staging = production.HasRallyStagingCell
                ? production.RallyStagingCell : new Vector2Int(-1, -1);
            int knownCastles = 0, knownProduction = 0;
            IReadOnlyList<BuildingKnowledgeSnapshot> known = knowledge.KnownEnemyBuildings;
            for (int i = 0; i < known.Count; i++)
            {
                if (known[i].BuildingType == BuildingType.Castle) knownCastles++;
                else if (known[i].BuildingType is BuildingType.Barracks
                    or BuildingType.Archery or BuildingType.Monastery)
                    knownProduction++;
            }
            int missingProducers = 0;
            if (production.DesiredCount(UnitType.Warrior) > 0.25f && barracks == 0)
                missingProducers++;
            if (production.DesiredCount(UnitType.Archer) > 0.25f && archeries == 0)
                missingProducers++;
            if ((production.DesiredCount(UnitType.Lancer) > 0.25f
                    || production.DesiredCount(UnitType.Monk) > 0.25f)
                && monasteries == 0) missingProducers++;
            int dangerCount = safety == null ? 0
                : safety.KnownStaticDangerCount + safety.VisibleDynamicThreatCount;
            return new StrategicSituationSnapshot(
                demand?.Food ?? 0, demand?.Wood ?? 0, demand?.Gold ?? 0,
                workers, Mathf.Max(0, workers - assigned),
                population?.Used ?? 0, population?.Cap ?? 0, infrastructure,
                castles, houses, barracks, archeries, monasteries, towers,
                production.LivingCount(UnitType.Warrior),
                production.LivingCount(UnitType.Archer),
                production.LivingCount(UnitType.Lancer),
                production.LivingCount(UnitType.Monk),
                production.MilitaryPopulation, production.QueuedMilitaryPopulation,
                ownStrength, ownStrength + queued, support, enemy,
                production.IntelConfidence, intelAge, target.IsValid,
                knownCastles, knownProduction, dangerCount, missingProducers,
                baseThreatened,
                scouting?.Scout != null, explored, loss, staging, CurrentIntent,
                IntentAge(context.Time), context.Time);
        }

        private StrategicIntentScores CalculateScores(StrategicSituationSnapshot s,
            EconomicDemandModel demand, ProductionCommanderPolicy production,
            ScoutingCommanderPolicy scouting, MilitaryCommanderPolicy military,
            float now)
        {
            float resourceDeficit = demand == null ? 0f : Mathf.Clamp01(
                (demand.FoodDeficit + demand.WoodDeficit + demand.GoldDeficit) / 300f);
            float workerGap = Mathf.Clamp01((production.TargetPawnCount - s.WorkerCount)
                / (float)Mathf.Max(1, production.TargetPawnCount));
            float populationPressure = s.PopulationCap > 0
                ? Mathf.Clamp01((s.Population + 2f) / s.PopulationCap) : 1f;
            float infrastructureGap = Mathf.Clamp01(s.MissingProducerCount / 3f);
            float militaryGap = Mathf.Clamp01((military.MinOffensiveMilitaryPopulation
                - s.MilitaryPopulation) / (float)military.MinOffensiveMilitaryPopulation);
            float economy = 0.16f + resourceDeficit * 0.30f + workerGap * 0.22f
                + populationPressure * 0.10f + infrastructureGap * 0.12f
                + militaryGap * 0.10f;

            float staleIntel = s.IntelAge < 0f ? 1f : Mathf.Clamp01(s.IntelAge / 45f);
            float scoutNeed = (1f - s.ExploredFraction) * 0.48f
                + staleIntel * 0.32f + (s.HasKnownTarget ? 0f : 0.20f);
            if (scouting != null && scouting.SafetyState == ScoutSafetyState.Evading)
                scoutNeed *= 0.65f;

            float threatConfidence = s.IntelAge < 0f ? 0f
                : Mathf.Clamp01(s.IntelConfidence);
            float fortify = s.BaseThreatened ? 1f
                : Mathf.Clamp01(s.EnemyStrength / Mathf.Max(1f, s.OwnStrength))
                    * 0.30f * threatConfidence
                    + Mathf.Clamp01(s.KnownDangerCount / 4f) * 0.12f;
            float prepare = s.HasKnownTarget
                ? Mathf.Clamp01(0.25f + AttackReadiness * 0.65f) : 0.10f;
            if (s.MilitaryPopulation < military.MinOffensiveMilitaryPopulation)
                prepare *= 0.75f;
            float attack = s.HasKnownTarget && AttackReadiness >= AttackReadinessThreshold
                ? Mathf.Clamp01(0.20f + AttackReadiness * 0.80f) : 0f;
            float recovery = Mathf.Clamp01(s.RecentLossRatio * 0.90f
                + (now - failedAttackTime < FailedAttackRecoverySeconds ? 0.75f : 0f));
            float secureMine = goldMine?.Current.CanCommit == true
                ? goldMine.Current.Score : 0f;
            return new StrategicIntentScores(economy, scoutNeed, fortify, prepare,
                attack, recovery, secureMine);
        }

        private float CalculateAttackReadiness(StrategicSituationSnapshot s,
            ProductionCommanderPolicy production, MilitaryCommanderPolicy military,
            StrategicSafetyMap safety, StrategicTargetSnapshot candidate)
        {
            float required = Mathf.Max(1f, military.MinOffensiveMilitaryPopulation);
            float force = Mathf.Clamp01((s.MilitaryPopulation
                + s.QueuedMilitaryPopulation * 0.45f) / required);
            float knownRatio = Mathf.Clamp01(s.ProjectedStrength
                / Mathf.Max(1f, s.EnemyStrength * 1.20f));
            float ratio = Mathf.Lerp(0.55f, knownRatio,
                Mathf.Clamp01(s.IntelConfidence));
            float composition = CompositionReadiness(production);
            float support = production.LivingCount(UnitType.Monk)
                + production.QueuedCount(UnitType.Monk) > 0 ? 1f : 0.55f;
            float cohesion = military.Army.State is ArmyGroupState.Ready
                or ArmyGroupState.PreparingAttack ? 1f
                : military.Army.State == ArmyGroupState.Forming ? 0.45f : 0.70f;
            float intel = s.HasKnownTarget
                ? Mathf.Max(0.35f, s.IntelConfidence) : 0f;
            float targetQuality = candidate.IsValid ? candidate.Score : 0f;
            float economy = Mathf.Clamp01((s.Food + s.Wood + s.Gold) / 450f);
            float risk = candidate.IsValid && safety != null
                ? (float)safety.RiskAt(candidate.LastKnownPosition,
                    StrategicPathKind.Army) / (float)StrategicRisk.Dangerous : 0f;
            float readiness = force * 0.23f + ratio * 0.20f
                + composition * 0.12f + support * 0.07f + cohesion * 0.11f
                + intel * 0.10f + targetQuality * 0.08f + economy * 0.09f;
            readiness -= Mathf.Clamp01(risk) * 0.12f;
            readiness -= s.RecentLossRatio * 0.35f;
            if (s.BaseThreatened) readiness -= 0.60f;
            return Mathf.Clamp01(readiness);
        }

        private StrategicTargetSnapshot SelectTarget(CommanderDecisionContext context,
            MilitaryCommanderPolicy military, StrategicSafetyMap safety)
        {
            StrategicTargetSnapshot best = default;
            IReadOnlyList<BuildingKnowledgeSnapshot> known =
                context.Knowledge.KnownEnemyBuildings;
            int count = Mathf.Min(known.Count, MaximumTargetCandidates);
            for (int i = 0; i < count; i++)
            {
                BuildingKnowledgeSnapshot item = known[i];
                if (!FactionRelations.AreHostile(context.Faction, item.Faction)
                    || !item.WasCompleted || item.LastSeenHealthRatio <= 0f
                    || item.BuildingType == BuildingType.GoldMine) continue;
                bool visible = context.Knowledge.TryGetVisibleEnemyBuilding(
                    item.RuntimeId, out _);
                float age = Mathf.Max(0f, context.Time - item.LastSeenTime);
                float confidence = visible ? 1f : Mathf.Clamp01(1f - age / 120f);
                float value = item.BuildingType switch
                {
                    BuildingType.Castle => 1f,
                    BuildingType.Barracks or BuildingType.Archery
                        or BuildingType.Monastery => 0.82f,
                    BuildingType.Tower => 0.62f,
                    _ => 0.42f
                };
                float distance = military.HasStagingPosition
                    ? Vector2.Distance(military.StagingPosition,
                        item.LastSeenPosition) : 0f;
                float proximity = 1f / (1f + distance * 0.04f);
                float risk = safety != null ? (float)safety.RiskAt(
                    item.LastSeenPosition, StrategicPathKind.Army)
                    / (float)StrategicRisk.Dangerous : 0f;
                float continuity = target.IsValid
                    && target.RuntimeId.CompareTo(item.RuntimeId) == 0 ? 0.10f : 0f;
                float score = Mathf.Clamp01(value * 0.50f + confidence * 0.24f
                    + proximity * 0.18f + continuity - Mathf.Clamp01(risk) * 0.14f);
                if (!best.IsValid || score > best.Score + 0.0001f
                    || Mathf.Abs(score - best.Score) <= 0.0001f
                    && item.RuntimeId.CompareTo(best.RuntimeId) < 0)
                    best = new StrategicTargetSnapshot(item.RuntimeId,
                        item.BuildingType, item.Faction, item.LastSeenPosition,
                        score, item.LastSeenTime, visible);
            }
            return best;
        }

        private static bool HasBaseThreat(FactionKnowledge knowledge,
            MilitaryCommanderPolicy military)
        {
            if (military.HasCurrentThreat) return true;
            if (!military.HasBaseAnchor) return false;
            float radius = military.BaseDefenseRadius;
            float radiusSquared = radius * radius;
            IReadOnlyList<VisibleUnitObservation> visible = knowledge.VisibleEnemyUnits;
            for (int i = 0; i < visible.Count; i++)
                if (FactionRelations.IsPlayable(visible[i].Faction)
                    && ((Vector2)(visible[i].Position - military.BaseAnchor)).sqrMagnitude
                        <= radiusSquared)
                    return true;
            return false;
        }

        private void ApplyTransition(StrategicIntent desired, string reason, float now,
            bool majorOverride)
        {
            if (desired == CurrentIntent)
            {
                DecisionReason = reason;
                return;
            }
            bool heldLongEnough = now - transitionTime >= MinimumIntentHold;
            bool clearsOverride = CurrentIntent is StrategicIntent.Fortify
                or StrategicIntent.Recover;
            bool advancesPreparedAttack = CurrentIntent == StrategicIntent.PrepareAttack
                && desired == StrategicIntent.Attack
                && AttackReadiness >= AttackReadinessThreshold;
            if (!majorOverride && !clearsOverride && !heldLongEnough) return;
            if (!majorOverride && !advancesPreparedAttack && Scores.Get(desired)
                < Scores.Get(CurrentIntent) + SwitchMargin) return;
            PreviousIntent = CurrentIntent;
            CurrentIntent = desired;
            transitionTime = now;
            DecisionReason = reason;
        }

        private static StrategicIntent BestIntent(StrategicIntentScores scores)
        {
            StrategicIntent best = StrategicIntent.DevelopEconomy;
            float value = scores.DevelopEconomy;
            for (int i = 0; i < Intents.Length; i++)
            {
                StrategicIntent intent = Intents[i];
                float candidate = scores.Get(intent);
                if (candidate > value + 0.0001f)
                {
                    best = intent;
                    value = candidate;
                }
            }
            return best;
        }

        private static StrategicIntentScores Smooth(StrategicIntentScores oldScores,
            StrategicIntentScores raw, float factor)
        {
            float t = Mathf.Clamp01(factor);
            return new StrategicIntentScores(
                Mathf.Lerp(oldScores.DevelopEconomy, raw.DevelopEconomy, t),
                Mathf.Lerp(oldScores.Scout, raw.Scout, t),
                Mathf.Lerp(oldScores.Fortify, raw.Fortify, t),
                Mathf.Lerp(oldScores.PrepareAttack, raw.PrepareAttack, t),
                Mathf.Lerp(oldScores.Attack, raw.Attack, t),
                Mathf.Lerp(oldScores.Recover, raw.Recover, t),
                Mathf.Lerp(oldScores.SecureGoldMine,
                    raw.SecureGoldMine, t));
        }

        private static float QueuedStrength(ProductionCommanderPolicy production,
            CombatStrengthTuning tuning)
            => production.QueuedCount(UnitType.Warrior) * tuning.WarriorValue
                + production.QueuedCount(UnitType.Archer) * tuning.ArcherValue
                + production.QueuedCount(UnitType.Lancer) * tuning.LancerValue
                + production.QueuedCount(UnitType.Monk) * tuning.MonkSupportValue;

        private static float RememberedEnemyStrength(EnemyCompositionMemory memory,
            CombatStrengthTuning tuning, float now)
        {
            if (memory == null) return 0f;
            return memory.EvidenceCount(UnitType.Warrior, now) * tuning.WarriorValue
                + memory.EvidenceCount(UnitType.Archer, now) * tuning.ArcherValue
                + memory.EvidenceCount(UnitType.Lancer, now) * tuning.LancerValue
                + memory.EvidenceCount(UnitType.Monk, now) * tuning.MonkSupportValue;
        }

        private static float CompositionReadiness(ProductionCommanderPolicy production)
        {
            int types = 0;
            if (production.LivingCount(UnitType.Warrior)
                + production.QueuedCount(UnitType.Warrior) > 0) types++;
            if (production.LivingCount(UnitType.Archer)
                + production.QueuedCount(UnitType.Archer) > 0) types++;
            if (production.LivingCount(UnitType.Lancer)
                + production.QueuedCount(UnitType.Lancer) > 0) types++;
            return Mathf.Clamp01(types / 3f);
        }

        private static string ReasonFor(StrategicIntent intent,
            StrategicSituationSnapshot situation) => intent switch
        {
            StrategicIntent.DevelopEconomy => situation.WorkerCount <= 3
                ? "LowWorkerCount" : "EarlyEconomy",
            StrategicIntent.Scout => situation.IntelAge < 0f
                ? "EnemyUnknown" : "IntelStale",
            StrategicIntent.Fortify => situation.BaseThreatened
                ? "KnownThreat" : situation.IntelConfidence >= 0.5f
                    ? "ArmyDisadvantage" : "BaseRisk",
            StrategicIntent.PrepareAttack => "ArmyNearlyReady",
            StrategicIntent.Attack => "AttackReady",
            StrategicIntent.Recover => "RecoverFromLoss",
            StrategicIntent.SecureGoldMine => "ContestedGoldMine",
            _ => "HighestScore"
        };
    }
}
