using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    public enum ScoutSafetyState { Exploring, Evading, Repositioning }
    public enum ScoutIntent
    {
        SearchingForEnemy,
        Exploring,
        ReconEnemyBase,
        ObserveMilitary,
        ReScout,
        Evading,
        Repositioning
    }

    public enum ScoutTargetKind
    {
        None,
        Frontier,
        EnemyBaseRecon,
        ProductionRecon,
        MilitaryRecon,
        ReScout,
        RememberedResource
    }

    [Serializable]
    public sealed class ScoutingCommanderPolicy
    {
        public bool HasPendingPromotionDemand => Scout == null
            && LastPromotionResult != null
            && LastPromotionResult.StartsWith("Deferred: insufficient", StringComparison.Ordinal);
        private static readonly ProfilerMarker CandidateGenerationMarker =
            new("TinyKingdom.AI.ScoutCandidateGeneration");
        private static readonly ProfilerMarker FrontierScoringMarker =
            new("TinyKingdom.AI.ScoutFrontierScoring");
        private static readonly ProfilerMarker RescoutScoringMarker =
            new("TinyKingdom.AI.ScoutRescoutScoring");
        private static readonly ProfilerMarker ReconScoringMarker =
            new("TinyKingdom.AI.ReconScoring");
        private static readonly ProfilerMarker ObservationPointGenerationMarker =
            new("TinyKingdom.AI.ObservationPointGeneration");
        private static readonly ProfilerMarker ScoutRetreatMarker =
            new("TinyKingdom.AI.ScoutEvasion");
        private const float PlannedBuildingWoodFallbackSeconds = 60f;

        [Header("Promotion")]
        [SerializeField, Min(1)] private int minimumPawnsForScout = 4;
        [SerializeField, Min(1f)] private float promotionRetrySeconds = 5f;
        [SerializeField, Min(10f)] private float busyPromotionFallbackSeconds = 60f;

        [Header("Target stability")]
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.35f;
        [SerializeField, Min(0.5f)] private float targetReevaluationSeconds = 3f;
        [SerializeField, Min(0.5f)] private float exhaustedRecheckSeconds = 8f;
        [SerializeField, Min(0.5f)] private float unreachableRetrySeconds = 5f;
        [SerializeField, Min(2f)] private float noProgressRecoverySeconds = 8f;
        [SerializeField, Min(5f)] private float failedTargetRecoverySeconds = 18f;

        [Header("Bounded exploration")]
        [SerializeField, Range(8, 96)] private int maxFrontierCandidates = 48;
        [SerializeField, Range(1, 3)] private int informationSampleStep = 2;
        [SerializeField, Range(4, 16)] private int maximumInformationRadiusCells = 10;

        [Header("Frontier score")]
        [SerializeField, Min(0f)] private float informationGainWeight = 4f;
        [SerializeField, Min(0f)] private float noveltyWeight = 1.5f;
        [SerializeField, Min(0f)] private float unknownRegionWeight = 2f;
        [SerializeField, Min(0f)] private float explorationDepthWeight = 1.25f;
        [SerializeField, Min(0f)] private float enemyInterestWeight = 2.5f;
        [SerializeField, Min(0f)] private float strategicRevisitWeight = 3f;
        [SerializeField, Min(0f)] private float travelWeight = 0.12f;
        [SerializeField, Min(0f)] private float riskWeight = 14f;

        [Header("Reconnaissance score")]
        [SerializeField] private StrategicRisk explorationRiskBudget = StrategicRisk.Safe;
        [SerializeField] private StrategicRisk reconRiskBudget = StrategicRisk.Risky;
        [SerializeField, Range(8, 64)] private int maxReconCandidates = 40;
        [SerializeField, Range(8, 64)] private int maxRescoutResourceCandidates = 16;
        [SerializeField, Min(0f)] private float reconInformationWeight = 2.5f;
        [SerializeField, Min(0f)] private float reconStrategicWeight = 4f;
        [SerializeField, Min(0f)] private float reconStalenessWeight = 0.35f;
        [SerializeField, Min(0f)] private float reconMilitaryWeight = 10f;
        [SerializeField, Min(0f)] private float reconInfrastructureWeight = 6f;
        [SerializeField, Min(0f)] private float reconRiskWeight = 8f;
        [SerializeField, Min(0.5f)] private float reconDwellSeconds = 2.5f;
        [SerializeField, Min(1f)] private float reconMissionCooldownSeconds = 18f;
        [SerializeField, Min(1f)] private float evasionReentryCooldownSeconds = 12f;

        [Header("Recent target suppression")]
        [SerializeField, Range(4, 16)] private int recentTargetCapacity = 8;
        [SerializeField, Min(1f)] private float recentTargetCooldownSeconds = 30f;
        [SerializeField, Range(1, 6)] private int recentTargetRadiusCells = 3;
        [SerializeField, Min(0f)] private float recentTargetPenalty = 48f;

        [Header("Re-scout and evasion")]
        [SerializeField, Min(1f)] private float staleMemorySeconds = 20f;
        [SerializeField, Range(2, 12)] private int rememberedApproachRadius = 3;
        [SerializeField, Range(3, 16)] private int evasionSearchRadiusCells = 9;
        [SerializeField, Min(1f)] private float visibleArmyEvasionRadius = 7f;
        [SerializeField, Min(0.5f)] private float evasionRetrySeconds = 1.5f;

        private readonly Dictionary<int, float> blockedUntil = new();
        private readonly Vector2Int[] rejectedDestinations = new Vector2Int[3];
        private readonly List<Vector2Int> frontierScratch = new(128);
        private readonly List<FrontierSeed> frontierSelection = new(48);
        private readonly List<ReconCandidate> reconScratch = new(48);
        private readonly Vector2Int[] recentTargetCells = new Vector2Int[16];
        private readonly ScoutTargetKind[] recentTargetKinds = new ScoutTargetKind[16];
        private readonly float[] recentTargetTimes = new float[16];
        private UnitController scout;
        private Vector2Int destinationCell;
        private Vector2Int observationCell;
        private Vector2Int lastTargetCell;
        private bool hasDestination;
        private bool hasLastTarget;
        private ScoutTargetKind targetKind;
        private ScoutIntent intent = ScoutIntent.SearchingForEnemy;
        private EntityId targetMemoryId;
        private ScoutSafetyState safetyState;
        private string dangerReason = "None";
        private Vector2Int retreatCell;
        private bool hasRetreatDestination;
        private bool explorationExhausted;
        private float nextTargetEvaluationTime;
        private System.Predicate<Vector3> foodExplorationArea;

        internal void SetFoodExplorationArea(System.Predicate<Vector3> area)
            => foodExplorationArea = area;
        private float nextPromotionAttemptTime;
        private float noIdlePromotionSince = -1f;
        private float reservedWoodShortageSince = -1f;
        private bool awaitingPromotedCargoReturn;
        private float promotedCargoReturnDeadline;
        private float nextPromotedCargoReturnAttempt;
        private float targetAssignedTime;
        private float nextEvasionAttemptTime;
        private float repositioningStartTime = -1f;
        private int rejectedTargetBatches;
        private float reconArrivedTime = -1f;
        private float nextReconMissionTime;
        private int recentTargetCount;
        private int recentTargetWriteIndex;
        private int nextRescoutMemoryIndex;
        private bool strategicInformationPriority;
        private bool searchAcrossIsland;

        public UnitController Scout => scout;
        public bool HasDestination => hasDestination;
        public Vector2Int DestinationCell => destinationCell;
        public ScoutTargetKind TargetKind => targetKind;
        public ScoutIntent Intent => intent;
        public EntityId TargetMemoryId => targetMemoryId;
        public ScoutSafetyState SafetyState => safetyState;
        public string DangerReason => dangerReason;
        public Vector2Int RetreatCell => retreatCell;
        public bool HasRetreatDestination => hasRetreatDestination;
        public int MinimumPawnsForScout => minimumPawnsForScout;
        public int MinimumPawnsForWorkerScout => minimumPawnsForScout;
        public float BusyPromotionFallbackSeconds
            => Mathf.Max(10f, busyPromotionFallbackSeconds);
        public int MaxFrontierCandidates => maxFrontierCandidates;
        public float InformationGainWeight => informationGainWeight;
        public float NoveltyWeight => noveltyWeight;
        public float StrategicRevisitWeight => strategicRevisitWeight;
        public float TravelWeight => travelWeight;
        public float RiskWeight => riskWeight;
        public float UnknownRegionWeight => unknownRegionWeight;
        public float ExplorationDepthWeight => explorationDepthWeight;
        public float ReconStrategicWeight => reconStrategicWeight;
        public int MaxReconCandidates => maxReconCandidates;
        public int MaxRescoutResourceCandidates
            => Mathf.Clamp(maxRescoutResourceCandidates, 8, 64);
        public StrategicRisk ExplorationRiskBudget => explorationRiskBudget;
        public StrategicRisk ReconRiskBudget => reconRiskBudget;
        public int RecentTargetCapacity => Mathf.Clamp(recentTargetCapacity, 4,
            recentTargetCells.Length);
        public int RecentTargetCount => recentTargetCount;
        public bool ExplorationExhausted => explorationExhausted;
        public int FrontierCandidateCount { get; private set; }
        public int ScoredFrontierCount { get; private set; }
        public int StaleRevisitCandidateCount { get; private set; }
        public int ReconCandidateCount { get; private set; }
        public float LastInformationGain { get; private set; }
        public StrategicRisk LastKnownRisk { get; private set; }
        public float LastTravelCost { get; private set; }
        public float LastTargetScore { get; private set; }
        public float LastStrategicValue { get; private set; }
        public float LastStaleness { get; private set; }
        public float CurrentTargetAge(float time) => hasDestination
            ? Mathf.Max(0f, time - targetAssignedTime) : 0f;
        public string LastPromotionResult { get; private set; } = "Not attempted";
        public float LastScoutDeathTime { get; private set; } = -1f;
        public bool StrategicInformationPriority => strategicInformationPriority;
        public uint SuccessfulDestinationSerial { get; private set; }
        public float LastSuccessfulDestinationTime { get; private set; } = -1f;
        public uint StallRecoveryCount { get; private set; }
        public uint RejectedDestinationCount { get; private set; }
        public uint FailedTargetRecoveryCount { get; private set; }
        public float LastFailedTargetRecoveryTime { get; private set; } = -1f;
        public CommanderCommandResult LastRejectedMoveResult { get; private set; }
            = CommanderCommandResult.Accepted;
        public float RepositioningDuration(float time) =>
            safetyState == ScoutSafetyState.Repositioning
                && repositioningStartTime >= 0f
                ? Mathf.Max(0f, time - repositioningStartTime) : 0f;

        public void Reset()
        {
            scout = null;
            ClearTarget();
            safetyState = ScoutSafetyState.Exploring;
            dangerReason = "None";
            hasRetreatDestination = false;
            explorationExhausted = false;
            nextTargetEvaluationTime = 0f;
            nextPromotionAttemptTime = 0f;
            noIdlePromotionSince = -1f;
            reservedWoodShortageSince = -1f;
            awaitingPromotedCargoReturn = false;
            promotedCargoReturnDeadline = 0f;
            nextPromotedCargoReturnAttempt = 0f;
            nextEvasionAttemptTime = 0f;
            LastPromotionResult = "Not attempted";
            LastScoutDeathTime = -1f;
            FrontierCandidateCount = 0;
            ScoredFrontierCount = 0;
            StaleRevisitCandidateCount = 0;
            ReconCandidateCount = 0;
            SuccessfulDestinationSerial = 0;
            LastSuccessfulDestinationTime = -1f;
            StallRecoveryCount = RejectedDestinationCount = 0;
            FailedTargetRecoveryCount = 0;
            LastFailedTargetRecoveryTime = -1f;
            LastRejectedMoveResult = CommanderCommandResult.Accepted;
            repositioningStartTime = -1f;
            rejectedTargetBatches = 0;
            blockedUntil.Clear();
            frontierScratch.Clear();
            frontierSelection.Clear();
            reconScratch.Clear();
            recentTargetCount = 0;
            recentTargetWriteIndex = 0;
            nextRescoutMemoryIndex = 0;
            reconArrivedTime = -1f;
            nextReconMissionTime = 0f;
            intent = ScoutIntent.SearchingForEnemy;
            strategicInformationPriority = false;
            searchAcrossIsland = false;
        }

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles)
            => Tick(context, roles, null, null);

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles,
            StrategicSafetyMap safety)
            => Tick(context, roles, safety, null);

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles,
            StrategicSafetyMap safety, BuildingCommanderPolicy building)
            => Tick(context, roles, safety, building, null);

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles,
            StrategicSafetyMap safety, BuildingCommanderPolicy building,
            StrategicIntentEvaluator strategy, bool prioritizeEnemyCastle = false)
        {
            searchAcrossIsland = prioritizeEnemyCastle && !HasKnownEnemyCastle(context.Knowledge);
            strategicInformationPriority = strategy?.CurrentIntent
                == StrategicIntent.Scout || searchAcrossIsland;
            SynchronizeOfficialScout(context, roles);
            if (scout == null)
            {
                TryPromoteScout(context, roles, safety, building);
                if (scout == null) return;
            }

            if (FinishPromotedCargoReturn(context, safety)) return;

            UnitMovement movement = scout.GetComponent<UnitMovement>();
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (movement == null || grid == null) return;

            if (safety != null && ShouldEvade(context.Knowledge, movement, safety,
                    out bool hasThreatCenter, out Vector3 threatCenter))
            {
                BeginOrContinueEvasion(context, movement, safety,
                    hasThreatCenter, threatCenter);
                return;
            }

            if (safetyState == ScoutSafetyState.Evading)
            {
                if (movement.IsMoving && HasRecentMovementProgress(movement)) return;
                if (movement.IsMoving)
                {
                    movement.Stop();
                    StallRecoveryCount++;
                }
                safetyState = ScoutSafetyState.Repositioning;
                repositioningStartTime = context.Time;
                intent = ScoutIntent.Repositioning;
                hasRetreatDestination = false;
                dangerReason = "None";
                nextTargetEvaluationTime = context.Time;
            }

            if (hasDestination)
            {
                if (movement.IsMoving && !HasRecentMovementProgress(movement))
                {
                    movement.Stop();
                    StallRecoveryCount++;
                }
                bool reached = Vector2.Distance(scout.transform.position,
                    grid.GridToWorld(destinationCell.x, destinationCell.y)) <= arrivalDistance;
                if (!reached && movement.IsMoving && IsTargetStillUseful(context, safety)
                    && (safety == null || safety.IsPathSafe(movement.CurrentPath,
                        StrategicPathKind.Scout, movement.CurrentPathWaypointIndex))) return;

                if (reached && IsReconTarget(targetKind))
                {
                    if (reconArrivedTime < 0f) reconArrivedTime = context.Time;
                    if (context.Time - reconArrivedTime < reconDwellSeconds) return;
                    nextReconMissionTime = context.Time + reconMissionCooldownSeconds;
                }

                if (!reached && !movement.IsMoving)
                    Block(destinationCell, grid.Width, context.Time);
                RememberLastTarget(context.Time);
                ClearTarget();
                intent = HasEnemyInterest(context.Knowledge)
                    ? ScoutIntent.Exploring : ScoutIntent.SearchingForEnemy;
                nextTargetEvaluationTime = context.Time;
            }

            if (context.Time < nextTargetEvaluationTime) return;

            const int maxMoveAttempts = 3;
            int rescoutBudget = Mathf.Min(MaxRescoutResourceCandidates,
                context.Knowledge.KnownResources.Count);
            for (int attempt = 0; attempt < maxMoveAttempts; attempt++)
            {
                if (!TrySelectTarget(context, safety, ref rescoutBudget,
                    out ScoutTarget selected))
                {
                    explorationExhausted = true;
                    rejectedTargetBatches = 0;
                    // No viable destination is an exhausted search, not an
                    // indefinitely active repositioning operation.
                    safetyState = ScoutSafetyState.Exploring;
                    repositioningStartTime = -1f;
                    intent = HasEnemyInterest(context.Knowledge)
                        ? ScoutIntent.Exploring : ScoutIntent.SearchingForEnemy;
                    nextTargetEvaluationTime = context.Time + (strategicInformationPriority
                        ? exhaustedRecheckSeconds * 0.5f : exhaustedRecheckSeconds);
                    return;
                }

                CommanderCommandResult result = context.Commands.TryScoutMove(scout,
                    grid.GridToWorld(selected.Destination.x, selected.Destination.y),
                    safety?.ScoutTraversal);
                if (result == CommanderCommandResult.Accepted)
                {
                    AssignTarget(selected, context.Time,
                        HasEnemyInterest(context.Knowledge));
                    safetyState = ScoutSafetyState.Exploring;
                    repositioningStartTime = -1f;
                    rejectedTargetBatches = 0;
                    explorationExhausted = false;
                    SuccessfulDestinationSerial++;
                    LastSuccessfulDestinationTime = context.Time;
                    return;
                }
                Block(selected.Destination, grid.Width, context.Time);
                rejectedDestinations[attempt] = selected.Destination;
                RejectedDestinationCount++;
                LastRejectedMoveResult = result;
            }

            if (++rejectedTargetBatches >= 3)
            {
                float cooldown = Mathf.Max(5f, failedTargetRecoverySeconds);
                for (int i = 0; i < maxMoveAttempts; i++)
                    blockedUntil[CellKey(rejectedDestinations[i], grid.Width)]
                        = context.Time + cooldown;
                rejectedTargetBatches = 0;
                FailedTargetRecoveryCount++;
                LastFailedTargetRecoveryTime = context.Time;
                explorationExhausted = true;
                safetyState = ScoutSafetyState.Exploring;
                repositioningStartTime = -1f;
                intent = HasEnemyInterest(context.Knowledge)
                    ? ScoutIntent.Exploring : ScoutIntent.SearchingForEnemy;
                nextTargetEvaluationTime = context.Time + cooldown;
                return;
            }
            nextTargetEvaluationTime = context.Time + (strategicInformationPriority
                ? targetReevaluationSeconds * 0.5f : targetReevaluationSeconds);
        }

        private bool HasRecentMovementProgress(UnitMovement movement)
            => Time.time - movement.LastMovementProgressTime
                < Mathf.Max(2f, noProgressRecoverySeconds);

        private void SynchronizeOfficialScout(CommanderDecisionContext context,
            CommanderPawnRoles roles)
        {
            PawnScoutPromotion active = PawnScoutPromotion.ActiveFor(context.Faction);
            UnitController authoritative = active != null ? active.Controller : null;
            if (authoritative == scout && IsUsableOfficialScout(context, scout))
            {
                reservedWoodShortageSince = -1f;
                roles?.TryAssign(scout, CommanderPawnRole.Scout);
                return;
            }

            if (!ReferenceEquals(scout, null) && authoritative == null)
                LastScoutDeathTime = context.Time;
            scout = IsUsableOfficialScout(context, authoritative) ? authoritative : null;
            awaitingPromotedCargoReturn = false;
            ClearTarget();
            hasRetreatDestination = false;
            safetyState = ScoutSafetyState.Exploring;
            repositioningStartTime = -1f;
            rejectedTargetBatches = 0;
            intent = ScoutIntent.SearchingForEnemy;
            if (scout != null)
            {
                reservedWoodShortageSince = -1f;
                roles?.TryAssign(scout, CommanderPawnRole.Scout);
            }
        }

        private void TryPromoteScout(CommanderDecisionContext context,
            CommanderPawnRoles roles, StrategicSafetyMap safety,
            BuildingCommanderPolicy building)
        {
            if (context.Time < nextPromotionAttemptTime) return;
            nextPromotionAttemptTime = context.Time + promotionRetrySeconds;

            if (!HasMeaningfulInformationNeed(context))
            {
                noIdlePromotionSince = -1f;
                reservedWoodShortageSince = -1f;
                LastPromotionResult = "Deferred: no frontier or stale strategic memory";
                return;
            }

            int reservedBuildingWood = ReservedBuildingWood(context, building);
            ResourceStockpile stockpile = context.Knowledge.Stockpile;
            int availableWood = stockpile != null
                ? stockpile.GetAmount(ResourceType.Wood) : 0;
            if (availableWood < PawnScoutPromotion.WoodCost)
            {
                noIdlePromotionSince = -1f;
                reservedWoodShortageSince = -1f;
                LastPromotionResult = "Deferred: insufficient uncommitted Wood";
                return;
            }
            if (reservedBuildingWood > 0
                && availableWood < PawnScoutPromotion.WoodCost + reservedBuildingWood)
            {
                if (reservedWoodShortageSince < 0f)
                    reservedWoodShortageSince = context.Time;
                // A planned building has not spent its Wood yet. Give that plan
                // time to proceed, then allow the Scout to discover more
                // resource options if the reservation keeps it blocked.
                if (context.Time - reservedWoodShortageSince
                    < PlannedBuildingWoodFallbackSeconds)
                {
                    noIdlePromotionSince = -1f;
                    LastPromotionResult = "Deferred: insufficient uncommitted Wood";
                    return;
                }
            }
            else reservedWoodShortageSince = -1f;

            UnitController candidate = ChoosePromotionCandidate(context.Knowledge, roles);
            if (candidate == null)
            {
                if (!strategicInformationPriority)
                    noIdlePromotionSince = -1f;
                else if (noIdlePromotionSince < 0f)
                    noIdlePromotionSince = context.Time;
                if (strategicInformationPriority && noIdlePromotionSince >= 0f
                    && context.Time - noIdlePromotionSince
                        >= BusyPromotionFallbackSeconds)
                    candidate = ChooseBusyPromotionCandidate(context.Knowledge, roles);
                if (candidate == null)
                {
                    LastPromotionResult = "Deferred: no available Economy Pawn";
                    return;
                }
            }

            CommanderCommandResult result = context.Commands.TryPromoteScout(candidate);
            LastPromotionResult = result.ToString();
            if (result != CommanderCommandResult.Accepted) return;

            noIdlePromotionSince = -1f;
            reservedWoodShortageSince = -1f;
            scout = candidate;
            roles?.TryAssign(scout, CommanderPawnRole.Scout);
            PawnGatherer cargo = scout.GetComponent<PawnGatherer>();
            awaitingPromotedCargoReturn = cargo != null && cargo.CarriedAmount > 0;
            promotedCargoReturnDeadline = context.Time + 30f;
            nextPromotedCargoReturnAttempt = context.Time;
            ClearTarget();
            intent = ScoutIntent.SearchingForEnemy;
            nextTargetEvaluationTime = context.Time;
        }

        private bool FinishPromotedCargoReturn(CommanderDecisionContext context,
            StrategicSafetyMap safety)
        {
            if (!awaitingPromotedCargoReturn || scout == null) return false;
            PawnGatherer gatherer = scout.GetComponent<PawnGatherer>();
            if (gatherer == null || gatherer.CarriedAmount <= 0
                || context.Time >= promotedCargoReturnDeadline)
            {
                gatherer?.CancelJob();
                scout.GetComponent<UnitMovement>()?.Stop();
                awaitingPromotedCargoReturn = false;
                return false;
            }
            if (context.Time >= nextPromotedCargoReturnAttempt)
            {
                context.Commands.TryReturnResources(scout, safety);
                nextPromotedCargoReturnAttempt = context.Time + promotionRetrySeconds;
            }
            return true;
        }

        private static int ReservedBuildingWood(CommanderDecisionContext context,
            BuildingCommanderPolicy building)
        {
            if (building == null || building.ActiveSite != null
                || !building.PlannedBuilding.HasValue) return 0;
            BuildingData data = context.Commands.BuildingSystem?.GetData(
                building.PlannedBuilding.Value);
            return data != null ? data.WoodCost : 0;
        }

        private UnitController ChoosePromotionCandidate(FactionKnowledge knowledge,
            CommanderPawnRoles roles)
        {
            if (CountLivingPawns(knowledge) < minimumPawnsForScout) return null;
            UnitController best = null;
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (!IsPromotionCandidate(unit, roles)) continue;
                if (best == null
                    || unit.GetEntityId().CompareTo(best.GetEntityId()) < 0) best = unit;
            }
            return best;
        }

        private UnitController ChooseBusyPromotionCandidate(FactionKnowledge knowledge,
            CommanderPawnRoles roles)
        {
            if (CountLivingPawns(knowledge) < minimumPawnsForScout) return null;
            UnitController best = null;
            int leastCargo = int.MaxValue;
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit == null || !unit.IsAlive || !unit.IsWorldParticipant
                    || roles == null
                    || roles.GetRole(unit) != CommanderPawnRole.Economy
                    || unit.GetComponent<PawnScoutPromotion>()?.IsPromoted == true
                    || unit.CurrentState is UnitState.Attacking or UnitState.Chasing)
                    continue;
                UnitCombatData data = unit.GetComponent<HealthComponent>()?.CombatData;
                PawnGatherer gatherer = unit.GetComponent<PawnGatherer>();
                PawnBuilder builder = unit.GetComponent<PawnBuilder>();
                if (data == null || data.UnitType != UnitType.Pawn
                    || gatherer == null || builder != null && builder.HasJob) continue;
                int cargo = gatherer.CarriedAmount;
                if (best == null || cargo < leastCargo
                    || cargo == leastCargo
                        && unit.GetEntityId().CompareTo(best.GetEntityId()) < 0)
                {
                    best = unit;
                    leastCargo = cargo;
                }
            }
            return best;
        }

        public static bool IsPromotionCandidate(UnitController unit,
            CommanderPawnRoles roles)
        {
            if (unit == null || !unit.IsAlive || !unit.IsWorldParticipant
                || roles == null || roles.GetRole(unit) != CommanderPawnRole.Economy
                || unit.GetComponent<PawnScoutPromotion>()?.IsPromoted == true)
                return false;
            UnitCombatData data = unit.GetComponent<HealthComponent>()?.CombatData;
            PawnGatherer gatherer = unit.GetComponent<PawnGatherer>();
            PawnBuilder builder = unit.GetComponent<PawnBuilder>();
            UnitMovement movement = unit.GetComponent<UnitMovement>();
            return data != null && data.UnitType == UnitType.Pawn && gatherer != null
                && gatherer.State == WorkerState.Idle
                && (builder == null || !builder.HasJob)
                && (movement == null || !movement.IsMoving)
                && unit.CurrentState == UnitState.Idle;
        }

        private bool HasMeaningfulInformationNeed(CommanderDecisionContext context)
        {
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return false;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (IsFrontier(context, cell)) return true;
            }
            return CountStaleMemories(context) > 0;
        }

        private bool TrySelectTarget(CommanderDecisionContext context,
            StrategicSafetyMap safety, ref int rescoutBudget,
            out ScoutTarget result)
        {
            StaleRevisitCandidateCount = 0;
            if (foodExplorationArea != null
                && TrySelectFrontier(context, safety, out result, foodExplorationArea))
                return true;
            bool hasEnemyInterest = HasEnemyInterest(context.Knowledge);
            if (searchAcrossIsland && TrySelectFrontier(context, safety, out result))
                return true;
            if (hasEnemyInterest && context.Time >= nextReconMissionTime
                && TrySelectReconTarget(context, safety, out result)) return true;
            // When the enemy Castle is still unknown, a safe frontier provides
            // new strategic information. Do not let plentiful stale resource
            // memories consume every Scout mission after recon has no target.
            bool preferFrontier = strategicInformationPriority
                && !HasKnownEnemyCastle(context.Knowledge);
            if (preferFrontier && TrySelectFrontier(context, safety, out result))
                return true;
            if (strategicInformationPriority
                && TrySelectRescoutTarget(context, safety, ref rescoutBudget,
                    out result)) return true;
            if (!preferFrontier && TrySelectFrontier(context, safety, out result))
                return true;
            if (!strategicInformationPriority
                && TrySelectRescoutTarget(context, safety, ref rescoutBudget,
                    out result)) return true;
            result = default;
            return false;
        }

        private bool TrySelectFrontier(CommanderDecisionContext context,
            StrategicSafetyMap safety, out ScoutTarget result,
            System.Predicate<Vector3> area = null)
        {
            result = default;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null || scout == null) return false;
            GenerateFrontierCandidates(context, grid, area);
            if (frontierScratch.Count == 0) return false;
            SelectFrontierSeeds(context, grid, safety);

            using var marker = FrontierScoringMarker.Auto();
            bool found = false;
            ScoredFrontierCount = 0;
            for (int i = 0; i < frontierSelection.Count; i++)
            {
                Vector2Int cell = frontierSelection[i].Cell;
                if (IsBlocked(cell, grid.Width, context.Time)) continue;
                StrategicRisk risk = safety != null
                    ? safety.RiskAt(cell, StrategicPathKind.Scout) : StrategicRisk.Safe;
                if (risk > (searchAcrossIsland ? reconRiskBudget : explorationRiskBudget)) continue;

                float informationGain = EstimateInformationGain(context, grid, cell);
                float regionNovelty = EstimateUnknownRegionValue(context, grid, cell);
                float depth = EstimateExplorationDepth(grid, safety, cell);
                float enemyInterest = EstimateEnemyInterest(context, grid, cell);
                float novelty = hasLastTarget
                    ? Mathf.Min(10f, Vector2Int.Distance(cell, lastTargetCell)) : 5f;
                float travel = Vector2.Distance(scout.transform.position,
                    grid.GridToWorld(cell.x, cell.y));
                float suppression = RecentTargetSuppression(cell, context.Time);
                float score = informationGainWeight * informationGain
                    + noveltyWeight * novelty
                    + unknownRegionWeight * regionNovelty
                    + explorationDepthWeight * depth
                    + enemyInterestWeight * enemyInterest
                    + IslandReconProgress(context.Faction, grid, cell)
                    - travelWeight * travel
                    - riskWeight * (int)risk
                    - suppression;
                ScoredFrontierCount++;
                if (!found || IsBetter(score, cell, result.Score, result.Destination))
                {
                    found = true;
                    result = new ScoutTarget(ScoutTargetKind.Frontier, cell, cell,
                        default, informationGain, enemyInterest + depth,
                        0f, risk, travel, score);
                }
            }
            return found;
        }

        private void GenerateFrontierCandidates(CommanderDecisionContext context,
            GridManager grid, System.Predicate<Vector3> area)
        {
            using var marker = CandidateGenerationMarker.Auto();
            frontierScratch.Clear();
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (IsFrontier(context, cell)
                    && (area == null || area(grid.GridToWorld(x, y))))
                    frontierScratch.Add(cell);
            }
            FrontierCandidateCount = frontierScratch.Count;
        }

        private void SelectFrontierSeeds(CommanderDecisionContext context,
            GridManager grid, StrategicSafetyMap safety)
        {
            frontierSelection.Clear();
            int limit = Mathf.Min(maxFrontierCandidates, frontierScratch.Count);
            for (int i = 0; i < frontierScratch.Count; i++)
            {
                Vector2Int cell = frontierScratch[i];
                if (IsBlocked(cell, grid.Width, context.Time)) continue;
                StrategicRisk risk = safety != null
                    ? safety.RiskAt(cell, StrategicPathKind.Scout) : StrategicRisk.Safe;
                if (risk > (searchAcrossIsland ? reconRiskBudget : explorationRiskBudget)) continue;
                float cheapValue = EstimateUnknownRegionValue(context, grid, cell)
                    + EstimateExplorationDepth(grid, safety, cell)
                    + EstimateEnemyInterest(context, grid, cell)
                    + IslandReconProgress(context.Faction, grid, cell)
                    - RecentTargetSuppression(cell, context.Time) * 0.25f;
                var seed = new FrontierSeed(cell, cheapValue);
                if (frontierSelection.Count < limit)
                {
                    frontierSelection.Add(seed);
                    continue;
                }
                int weakest = 0;
                for (int j = 1; j < frontierSelection.Count; j++)
                    if (frontierSelection[j].CheapValue
                        < frontierSelection[weakest].CheapValue) weakest = j;
                if (cheapValue > frontierSelection[weakest].CheapValue)
                    frontierSelection[weakest] = seed;
            }
        }

        private float EstimateUnknownRegionValue(CommanderDecisionContext context,
            GridManager grid, Vector2Int center)
        {
            const int radius = 8;
            const int step = 2;
            int unknown = 0;
            for (int y = -radius; y <= radius; y += step)
            for (int x = -radius; x <= radius; x += step)
            {
                if (x * x + y * y > radius * radius) continue;
                var sample = center + new Vector2Int(x, y);
                if (grid.IsInsideGrid(sample.x, sample.y)
                    && !context.Knowledge.IsCellExplored(sample)) unknown++;
            }
            return unknown;
        }

        private float IslandReconProgress(Faction faction, GridManager grid, Vector2Int cell)
        {
            if (!searchAcrossIsland) return 0f;
            // Public map orientation: north and south shores, explored via normal fog.
            int forward = faction == Faction.Blue ? cell.y : grid.Height - 1 - cell.y;
            return 60f * forward / Mathf.Max(1, grid.Height - 1);
        }

        private static float EstimateExplorationDepth(GridManager grid,
            StrategicSafetyMap safety, Vector2Int cell)
        {
            if (safety == null || !safety.HasHomeAnchor) return 0f;
            float diagonal = Mathf.Max(1f, Mathf.Sqrt(grid.Width * grid.Width
                + grid.Height * grid.Height) * grid.NavigationData.CellSize);
            return 20f * Vector2.Distance(grid.GridToWorld(cell.x, cell.y),
                safety.HomeAnchor) / diagonal;
        }

        private float EstimateEnemyInterest(CommanderDecisionContext context,
            GridManager grid, Vector2Int cell)
        {
            Vector3 position = grid.GridToWorld(cell.x, cell.y);
            float best = 0f;
            IReadOnlyList<BuildingKnowledgeSnapshot> buildings =
                context.Knowledge.KnownEnemyBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                float distance = Vector2.Distance(position, buildings[i].LastSeenPosition);
                best = Mathf.Max(best, BuildingImportance(buildings[i].BuildingType)
                    * Mathf.Clamp01(1f - distance / 14f));
            }
            MilitaryObservationSnapshot military =
                context.Knowledge.LastMilitaryObservation;
            if (military.HasObservation)
            {
                float distance = Vector2.Distance(position, military.ObservationRegion);
                best = Mathf.Max(best, Mathf.Min(6f, military.TotalCount)
                    * Mathf.Clamp01(1f - distance / 12f));
            }
            return best;
        }

        private float EstimateInformationGain(CommanderDecisionContext context,
            GridManager grid, Vector2Int center)
        {
            float cellSize = Mathf.Max(0.1f, grid.NavigationData.CellSize);
            int radius = Mathf.Clamp(Mathf.CeilToInt(scout.VisionRadius / cellSize),
                2, maximumInformationRadiusCells);
            int step = Mathf.Max(1, informationSampleStep);
            int unknown = 0;
            int radiusSquared = radius * radius;
            for (int y = -radius; y <= radius; y += step)
            for (int x = -radius; x <= radius; x += step)
            {
                if (x * x + y * y > radiusSquared) continue;
                int sampleX = center.x + x;
                int sampleY = center.y + y;
                if (!grid.IsInsideGrid(sampleX, sampleY)) continue;
                if (!context.Knowledge.IsCellExplored(
                    new Vector2Int(sampleX, sampleY))) unknown++;
            }
            return unknown;
        }

        private bool TrySelectReconTarget(CommanderDecisionContext context,
            StrategicSafetyMap safety, out ScoutTarget result)
        {
            using var marker = ReconScoringMarker.Auto();
            result = default;
            reconScratch.Clear();
            ReconCandidateCount = 0;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null || scout == null) return false;

            using (ObservationPointGenerationMarker.Auto())
            {
                IReadOnlyList<BuildingKnowledgeSnapshot> buildings =
                    context.Knowledge.KnownEnemyBuildings;
                AddBuildingReconCandidates(context, grid, safety, buildings,
                    BuildingType.Castle);
                AddBuildingReconCandidates(context, grid, safety, buildings,
                    BuildingType.Barracks);
                AddBuildingReconCandidates(context, grid, safety, buildings,
                    BuildingType.Archery);
                AddBuildingReconCandidates(context, grid, safety, buildings,
                    BuildingType.Monastery);
                AddBuildingReconCandidates(context, grid, safety, buildings,
                    BuildingType.Tower);
                AddBuildingReconCandidates(context, grid, safety, buildings,
                    BuildingType.House);

                MilitaryObservationSnapshot military =
                    context.Knowledge.LastMilitaryObservation;
                if (military.HasObservation && reconScratch.Count < maxReconCandidates)
                {
                    if (context.Time - military.LastSeenTime >= staleMemorySeconds)
                        StaleRevisitCandidateCount++;
                    Vector2Int anchor = grid.WorldToGrid(military.ObservationRegion);
                    AddReconRing(context, grid, safety, anchor, default,
                        ScoutTargetKind.MilitaryRecon, Mathf.Min(8f, military.TotalCount),
                        context.Time - military.LastSeenTime, true, 0f);
                }
            }

            ReconCandidateCount = reconScratch.Count;

            bool found = false;
            for (int i = 0; i < reconScratch.Count; i++)
            {
                ReconCandidate candidate = reconScratch[i];
                float information = EstimateInformationGain(context, grid,
                    candidate.Destination);
                float travel = Vector2.Distance(scout.transform.position,
                    grid.GridToWorld(candidate.Destination.x, candidate.Destination.y));
                float suppression = RecentTargetSuppression(candidate.Destination,
                    context.Time);
                if (suppression >= recentTargetPenalty * 0.75f
                    && candidate.Staleness < staleMemorySeconds) continue;
                float score = reconStrategicWeight * candidate.StrategicValue
                    + reconInformationWeight * information
                    + reconStalenessWeight * Mathf.Min(90f, candidate.Staleness)
                    + (candidate.IsMilitary ? reconMilitaryWeight : 0f)
                    + (candidate.IsInfrastructure ? reconInfrastructureWeight : 0f)
                    - travelWeight * travel
                    - reconRiskWeight * (int)candidate.Risk
                    - suppression;
                ScoutTargetKind kind = candidate.Staleness >= staleMemorySeconds
                    ? ScoutTargetKind.ReScout : candidate.Kind;
                if (!found || IsBetter(score, candidate.Destination,
                    result.Score, result.Destination))
                {
                    found = true;
                    result = new ScoutTarget(kind, candidate.Destination,
                        candidate.Observation, candidate.MemoryId, information,
                        candidate.StrategicValue, candidate.Staleness,
                        candidate.Risk, travel, score);
                }
            }
            return found;
        }

        private void AddBuildingReconCandidates(CommanderDecisionContext context,
            GridManager grid, StrategicSafetyMap safety,
            IReadOnlyList<BuildingKnowledgeSnapshot> buildings, BuildingType type)
        {
            for (int i = 0; i < buildings.Count && reconScratch.Count < maxReconCandidates;
                i++)
            {
                BuildingKnowledgeSnapshot memory = buildings[i];
                if (memory.BuildingType != type || !memory.WasCompleted
                    || memory.LastSeenHealthRatio <= 0f) continue;
                if (context.Time - memory.LastSeenTime >= staleMemorySeconds)
                    StaleRevisitCandidateCount++;
                ScoutTargetKind kind = type == BuildingType.Castle
                    ? ScoutTargetKind.EnemyBaseRecon
                    : type is BuildingType.Barracks or BuildingType.Archery
                        or BuildingType.Monastery
                        ? ScoutTargetKind.ProductionRecon
                        : ScoutTargetKind.EnemyBaseRecon;
                AddReconRing(context, grid, safety,
                    grid.WorldToGrid(memory.LastSeenPosition), memory.RuntimeId, kind,
                    BuildingImportance(type), context.Time - memory.LastSeenTime,
                    false, memory.DefensiveAttackRange, true);
            }
        }

        private void AddReconRing(CommanderDecisionContext context, GridManager grid,
            StrategicSafetyMap safety, Vector2Int anchor, EntityId memoryId,
            ScoutTargetKind kind, float strategicValue, float staleness,
            bool military, float defensiveRange, bool infrastructure = false)
        {
            float cellSize = Mathf.Max(0.1f, grid.NavigationData.CellSize);
            int visionCells = Mathf.Max(3, Mathf.FloorToInt(scout.VisionRadius / cellSize));
            int defenseCells = Mathf.CeilToInt((defensiveRange + 1.5f) / cellSize);
            // Non-defensive anchors use the outer part of the Scout's sight radius.
            // Castle/Tower anchors move the ring beyond known attack range instead
            // of clamping it back into their forbidden core.
            int ringRadius = Mathf.Max(Mathf.Max(2, visionCells - 1), defenseCells);
            const int angleCount = 8;
            for (int angle = 0; angle < angleCount
                && reconScratch.Count < maxReconCandidates; angle++)
            {
                float radians = angle * Mathf.PI * 2f / angleCount;
                var cell = anchor + new Vector2Int(
                    Mathf.RoundToInt(Mathf.Cos(radians) * ringRadius),
                    Mathf.RoundToInt(Mathf.Sin(radians) * ringRadius));
                GridCell navigation = grid.GetCell(cell.x, cell.y);
                if (navigation == null || !navigation.Walkable
                    || IsBlocked(cell, grid.Width, context.Time)
                    || context.Knowledge.FogMemory != null
                        && context.Knowledge.FogMemory.IsKnownOccupied(
                            context.Faction, cell)) continue;
                StrategicRisk risk = safety != null
                    ? safety.RiskAt(cell, StrategicPathKind.Scout) : StrategicRisk.Safe;
                if (risk > reconRiskBudget) continue;
                reconScratch.Add(new ReconCandidate(cell, anchor, memoryId, kind,
                    strategicValue, Mathf.Max(0f, staleness), risk, military,
                    infrastructure));
            }
        }

        private static bool HasEnemyInterest(FactionKnowledge knowledge)
            => knowledge != null && (knowledge.KnownEnemyBuildings.Count > 0
                || knowledge.LastMilitaryObservation.HasObservation);

        private static bool HasKnownEnemyCastle(FactionKnowledge knowledge)
        {
            if (knowledge == null) return false;
            IReadOnlyList<BuildingKnowledgeSnapshot> buildings =
                knowledge.KnownEnemyBuildings;
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i].BuildingType == BuildingType.Castle) return true;
            return false;
        }

        private bool TrySelectRescoutTarget(CommanderDecisionContext context,
            StrategicSafetyMap safety, ref int rescoutBudget,
            out ScoutTarget result)
        {
            using var marker = RescoutScoringMarker.Auto();
            result = default;
            bool found = false;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return false;
            IReadOnlyList<ResourceKnowledgeSnapshot> resources = context.Knowledge.KnownResources;
            int resourceCount = resources.Count;
            if (resourceCount == 0)
            {
                nextRescoutMemoryIndex = 0;
                return false;
            }
            // Finding an approach checks a ring of cells for each memory. A
            // rotating fixed window covers all memories across evaluations
            // without scoring hundreds of rings in one Commander frame.
            // Three rejected move attempts can reach this method in one Tick.
            // Share one window across them so total scoring stays bounded.
            int limit = Mathf.Min(rescoutBudget, resourceCount);
            if (limit <= 0) return false;
            int start = nextRescoutMemoryIndex % resourceCount;
            nextRescoutMemoryIndex = (start + limit) % resourceCount;
            rescoutBudget -= limit;
            for (int offset = 0; offset < limit; offset++)
            {
                ResourceKnowledgeSnapshot memory =
                    resources[(start + offset) % resourceCount];
                float age = context.Time - memory.LastSeenTime;
                if (age < staleMemorySeconds
                    || context.Knowledge.IsCellVisible(memory.LogicalCell)
                    || !TryFindRememberedApproach(context, memory.LogicalCell, safety,
                        out Vector2Int destination, out StrategicRisk risk,
                        out float travel)) continue;
                StaleRevisitCandidateCount++;
                float strategicValue = ResourceImportance(memory.ResourceType)
                    + age / staleMemorySeconds;
                float score = strategicRevisitWeight * strategicValue
                    - travelWeight * travel - riskWeight * (int)risk;
                if (!found || IsBetter(score, destination, result.Score, result.Destination))
                {
                    found = true;
                    result = new ScoutTarget(ScoutTargetKind.RememberedResource,
                        destination, memory.LogicalCell, memory.RuntimeId,
                        0f, strategicValue, age, risk, travel, score);
                }
            }
            return found;
        }

        private bool TryFindRememberedApproach(CommanderDecisionContext context,
            Vector2Int observedCell, StrategicSafetyMap safety, out Vector2Int result,
            out StrategicRisk resultRisk, out float resultTravel)
        {
            result = default;
            resultRisk = StrategicRisk.ForbiddenForCivilian;
            resultTravel = float.PositiveInfinity;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null || scout == null) return false;
            bool found = false;
            for (int radius = 1; radius <= rememberedApproachRadius; radius++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius) continue;
                var cell = observedCell + new Vector2Int(x, y);
                GridCell navigation = grid.GetCell(cell.x, cell.y);
                if (navigation == null || !navigation.Walkable
                    || !context.Knowledge.IsCellExplored(cell)
                    || context.Knowledge.FogMemory != null
                        && context.Knowledge.FogMemory.IsKnownOccupied(
                            context.Faction, cell)) continue;
                StrategicRisk risk = safety != null
                    ? safety.RiskAt(cell, StrategicPathKind.Scout) : StrategicRisk.Safe;
                if (risk > explorationRiskBudget) continue;
                float travel = Vector2.Distance(scout.transform.position,
                    navigation.WorldPosition);
                if (!found || risk < resultRisk
                    || risk == resultRisk && travel < resultTravel - 0.0001f
                    || risk == resultRisk && Mathf.Abs(travel - resultTravel) <= 0.0001f
                        && (cell.y < result.y || cell.y == result.y && cell.x < result.x))
                {
                    found = true;
                    result = cell;
                    resultRisk = risk;
                    resultTravel = travel;
                }
            }
            return found;
        }

        private int CountStaleMemories(CommanderDecisionContext context)
        {
            int count = 0;
            IReadOnlyList<BuildingKnowledgeSnapshot> buildings =
                context.Knowledge.KnownEnemyBuildings;
            for (int i = 0; i < buildings.Count; i++)
                if (context.Time - buildings[i].LastSeenTime >= staleMemorySeconds) count++;
            IReadOnlyList<ResourceKnowledgeSnapshot> resources = context.Knowledge.KnownResources;
            for (int i = 0; i < resources.Count; i++)
                if (context.Time - resources[i].LastSeenTime >= staleMemorySeconds) count++;
            return count;
        }

        private bool IsTargetStillUseful(CommanderDecisionContext context,
            StrategicSafetyMap safety)
        {
            if (!hasDestination) return false;
            StrategicRisk budget = IsReconTarget(targetKind)
                ? reconRiskBudget : explorationRiskBudget;
            if (safety != null && safety.RiskAt(destinationCell,
                StrategicPathKind.Scout) > budget) return false;
            return targetKind == ScoutTargetKind.Frontier
                ? IsFrontier(context, destinationCell)
                : IsReconTarget(targetKind)
                    || !context.Knowledge.IsCellVisible(observationCell);
        }

        private bool ShouldEvade(FactionKnowledge knowledge, UnitMovement movement,
            StrategicSafetyMap safety, out bool hasThreatCenter, out Vector3 threatCenter)
        {
            hasThreatCenter = TryVisibleThreatCenter(knowledge, out threatCenter);
            if (hasThreatCenter)
            {
                dangerReason = "visible enemy military nearby";
                return true;
            }
            if (safety.RiskAt(scout.transform.position, StrategicPathKind.Scout)
                >= StrategicRisk.Dangerous)
            {
                dangerReason = "known strategic danger";
                return true;
            }
            if (hasDestination && safety.RiskAt(destinationCell, StrategicPathKind.Scout)
                >= StrategicRisk.Dangerous)
            {
                dangerReason = "destination became forbidden";
                return true;
            }
            if (movement.IsMoving && !safety.IsPathSafe(movement.CurrentPath,
                StrategicPathKind.Scout, movement.CurrentPathWaypointIndex))
            {
                dangerReason = "route became unsafe";
                return true;
            }
            return false;
        }

        private bool TryVisibleThreatCenter(FactionKnowledge knowledge, out Vector3 center)
        {
            center = Vector3.zero;
            int count = 0;
            float radiusSquared = visibleArmyEvasionRadius * visibleArmyEvasionRadius;
            IReadOnlyList<VisibleUnitObservation> units = knowledge.VisibleEnemyUnits;
            for (int i = 0; i < units.Count; i++)
            {
                VisibleUnitObservation unit = units[i];
                if (unit.UnitType is not (UnitType.Warrior or UnitType.Archer
                    or UnitType.Lancer or UnitType.Monk)
                    || ((Vector2)(unit.Position - scout.transform.position)).sqrMagnitude
                        > radiusSquared) continue;
                center += unit.Position;
                count++;
            }
            if (count == 0) return false;
            center /= count;
            return true;
        }

        private void BeginOrContinueEvasion(CommanderDecisionContext context,
            UnitMovement movement, StrategicSafetyMap safety, bool hasThreatCenter,
            Vector3 threatCenter)
        {
            using var marker = ScoutRetreatMarker.Auto();
            if (safetyState == ScoutSafetyState.Evading && movement.IsMoving
                && HasRecentMovementProgress(movement)
                && safety.IsPathSafe(movement.CurrentPath, StrategicPathKind.Scout,
                    movement.CurrentPathWaypointIndex)) return;
            if (safetyState == ScoutSafetyState.Evading
                && context.Time < nextEvasionAttemptTime) return;
            nextEvasionAttemptTime = context.Time + evasionRetrySeconds;
            if (movement.IsMoving && !HasRecentMovementProgress(movement))
                StallRecoveryCount++;
            movement.Stop();
            RememberLastTarget(context.Time);
            ClearTarget();
            safetyState = ScoutSafetyState.Evading;
            intent = ScoutIntent.Evading;
            nextReconMissionTime = Mathf.Max(nextReconMissionTime,
                context.Time + evasionReentryCooldownSeconds);
            hasRetreatDestination = false;
            if (!safety.TryFindLocalRetreatCell(context.Knowledge,
                scout.transform.position, StrategicPathKind.Scout,
                evasionSearchRadiusCells, hasThreatCenter, threatCenter,
                out Vector2Int destination)) return;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return;
            if (context.Commands.TryScoutMove(scout,
                grid.GridToWorld(destination.x, destination.y),
                safety.ScoutRetreatTraversal) != CommanderCommandResult.Accepted) return;
            retreatCell = destination;
            hasRetreatDestination = true;
        }

        private static bool IsFrontier(CommanderDecisionContext context, Vector2Int cell)
        {
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            GridCell navigation = grid?.GetCell(cell.x, cell.y);
            if (grid == null || navigation == null || !navigation.Walkable
                || !context.Knowledge.IsCellExplored(cell)) return false;
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                if ((x == 0 && y == 0) || !grid.IsInsideGrid(cell.x + x, cell.y + y))
                    continue;
                if (!context.Knowledge.IsCellExplored(
                    new Vector2Int(cell.x + x, cell.y + y))) return true;
            }
            return false;
        }

        private void AssignTarget(ScoutTarget target, float time, bool enemyKnown)
        {
            destinationCell = target.Destination;
            observationCell = target.Observation;
            targetMemoryId = target.MemoryId;
            targetKind = target.Kind;
            intent = target.Kind switch
            {
                ScoutTargetKind.EnemyBaseRecon or ScoutTargetKind.ProductionRecon
                    => ScoutIntent.ReconEnemyBase,
                ScoutTargetKind.MilitaryRecon => ScoutIntent.ObserveMilitary,
                ScoutTargetKind.ReScout or ScoutTargetKind.RememberedResource
                    => ScoutIntent.ReScout,
                _ => enemyKnown ? ScoutIntent.Exploring
                    : ScoutIntent.SearchingForEnemy
            };
            hasDestination = true;
            targetAssignedTime = time;
            reconArrivedTime = -1f;
            nextTargetEvaluationTime = time + targetReevaluationSeconds;
            LastInformationGain = target.InformationGain;
            LastKnownRisk = target.Risk;
            LastTravelCost = target.Travel;
            LastTargetScore = target.Score;
            LastStrategicValue = target.StrategicValue;
            LastStaleness = target.Staleness;
        }

        private void RememberLastTarget(float time)
        {
            if (!hasDestination) return;
            lastTargetCell = destinationCell;
            hasLastTarget = true;
            int capacity = RecentTargetCapacity;
            recentTargetCells[recentTargetWriteIndex] = destinationCell;
            recentTargetKinds[recentTargetWriteIndex] = targetKind;
            recentTargetTimes[recentTargetWriteIndex] = time;
            recentTargetWriteIndex = (recentTargetWriteIndex + 1) % capacity;
            recentTargetCount = Mathf.Min(capacity, recentTargetCount + 1);
        }

        private void ClearTarget()
        {
            hasDestination = false;
            targetKind = ScoutTargetKind.None;
            destinationCell = default;
            observationCell = default;
            targetMemoryId = default;
            reconArrivedTime = -1f;
        }

        private float RecentTargetSuppression(Vector2Int cell, float time)
        {
            float strongest = 0f;
            int capacity = RecentTargetCapacity;
            for (int i = 0; i < recentTargetCount; i++)
            {
                int index = (recentTargetWriteIndex - 1 - i + capacity) % capacity;
                float age = time - recentTargetTimes[index];
                if (age < 0f || age >= recentTargetCooldownSeconds) continue;
                float distance = Vector2Int.Distance(cell, recentTargetCells[index]);
                if (distance > recentTargetRadiusCells) continue;
                float distanceFactor = 1f - distance / (recentTargetRadiusCells + 1f);
                strongest = Mathf.Max(strongest, recentTargetPenalty
                    * (1f - age / recentTargetCooldownSeconds) * distanceFactor);
            }
            return strongest;
        }

        public string RecentTargetHistory(float time)
        {
            if (recentTargetCount == 0) return "None";
            var summary = new System.Text.StringBuilder(128);
            int capacity = RecentTargetCapacity;
            for (int i = 0; i < recentTargetCount; i++)
            {
                int index = (recentTargetWriteIndex - 1 - i + capacity) % capacity;
                if (i > 0) summary.Append(" | ");
                summary.Append(recentTargetKinds[index]).Append('@')
                    .Append(recentTargetCells[index]).Append(" age=")
                    .Append(Mathf.Max(0f, time - recentTargetTimes[index]).ToString("0.0"));
            }
            return summary.ToString();
        }

        private static bool IsReconTarget(ScoutTargetKind kind)
            => kind is ScoutTargetKind.EnemyBaseRecon
                or ScoutTargetKind.ProductionRecon
                or ScoutTargetKind.MilitaryRecon
                or ScoutTargetKind.ReScout;

        private static bool IsUsableOfficialScout(CommanderDecisionContext context,
            UnitController unit)
            => unit != null && unit.IsAlive && unit.IsWorldParticipant
                && unit.Faction == context.Faction
                && unit.GetComponent<PawnScoutPromotion>()?.IsPromoted == true;

        private static int CountLivingPawns(FactionKnowledge knowledge)
        {
            int count = 0;
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitCombatData data = units[i]?.GetComponent<HealthComponent>()?.CombatData;
                if (data != null && data.UnitType == UnitType.Pawn) count++;
            }
            return count;
        }

        private static float BuildingImportance(BuildingType type)
            => type switch
            {
                BuildingType.Castle => 6f,
                BuildingType.Tower => 5f,
                BuildingType.Barracks or BuildingType.Archery or BuildingType.Monastery => 4f,
                BuildingType.GoldMine => 3f,
                _ => 2f
            };

        private static float ResourceImportance(ResourceType type)
            => type switch
            {
                ResourceType.Gold => 3f,
                ResourceType.Wood => 2f,
                _ => 1f
            };

        private static bool IsBetter(float score, Vector2Int cell,
            float bestScore, Vector2Int bestCell)
            => score > bestScore + 0.0001f
                || Mathf.Abs(score - bestScore) <= 0.0001f
                    && (cell.y < bestCell.y || cell.y == bestCell.y && cell.x < bestCell.x);

        private void Block(Vector2Int cell, int width, float time)
            => blockedUntil[CellKey(cell, width)] = time + unreachableRetrySeconds;

        private bool IsBlocked(Vector2Int cell, int width, float time)
            => blockedUntil.TryGetValue(CellKey(cell, width), out float until) && time < until;

        private static int CellKey(Vector2Int cell, int width) => cell.y * width + cell.x;

        private readonly struct ScoutTarget
        {
            public ScoutTarget(ScoutTargetKind kind, Vector2Int destination,
                Vector2Int observation, EntityId memoryId, float informationGain,
                float strategicValue, float staleness, StrategicRisk risk,
                float travel, float score)
            {
                Kind = kind;
                Destination = destination;
                Observation = observation;
                MemoryId = memoryId;
                InformationGain = informationGain;
                StrategicValue = strategicValue;
                Staleness = staleness;
                Risk = risk;
                Travel = travel;
                Score = score;
            }

            public ScoutTargetKind Kind { get; }
            public Vector2Int Destination { get; }
            public Vector2Int Observation { get; }
            public EntityId MemoryId { get; }
            public float InformationGain { get; }
            public float StrategicValue { get; }
            public float Staleness { get; }
            public StrategicRisk Risk { get; }
            public float Travel { get; }
            public float Score { get; }
        }

        private readonly struct FrontierSeed
        {
            public FrontierSeed(Vector2Int cell, float cheapValue)
            {
                Cell = cell;
                CheapValue = cheapValue;
            }

            public Vector2Int Cell { get; }
            public float CheapValue { get; }
        }

        private readonly struct ReconCandidate
        {
            public ReconCandidate(Vector2Int destination, Vector2Int observation,
                EntityId memoryId, ScoutTargetKind kind, float strategicValue,
                float staleness, StrategicRisk risk, bool isMilitary,
                bool isInfrastructure)
            {
                Destination = destination;
                Observation = observation;
                MemoryId = memoryId;
                Kind = kind;
                StrategicValue = strategicValue;
                Staleness = staleness;
                Risk = risk;
                IsMilitary = isMilitary;
                IsInfrastructure = isInfrastructure;
            }

            public Vector2Int Destination { get; }
            public Vector2Int Observation { get; }
            public EntityId MemoryId { get; }
            public ScoutTargetKind Kind { get; }
            public float StrategicValue { get; }
            public float Staleness { get; }
            public StrategicRisk Risk { get; }
            public bool IsMilitary { get; }
            public bool IsInfrastructure { get; }
        }
    }
}
