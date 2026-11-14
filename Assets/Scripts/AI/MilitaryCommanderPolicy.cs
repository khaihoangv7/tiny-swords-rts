using System;
using System.Collections.Generic;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    public enum StrategicTargetCategory
    {
        DefensiveBuilding,
        MilitaryBuilding,
        EconomicBuilding,
        MainCastle
    }

    public readonly struct OffensiveTargetSnapshot
    {
        public OffensiveTargetSnapshot(EntityId id, BuildingType type, Faction faction,
            Vector3 position, StrategicTargetCategory category, bool visible)
        {
            RuntimeId = id;
            BuildingType = type;
            Faction = faction;
            LastKnownPosition = position;
            Category = category;
            WasVisibleWhenSelected = visible;
        }

        public EntityId RuntimeId { get; }
        public BuildingType BuildingType { get; }
        public Faction Faction { get; }
        public Vector3 LastKnownPosition { get; }
        public StrategicTargetCategory Category { get; }
        public bool WasVisibleWhenSelected { get; }
    }

    [Serializable]
    public sealed class MilitaryCommanderPolicy
    {
        private static readonly ProfilerMarker ThreatEvaluationMarker =
            new("TinyKingdom.AI.ThreatEvaluation");
        private static readonly ProfilerMarker ArmyRegroupMarker =
            new("TinyKingdom.AI.ArmyRegroup");
        private static readonly ProfilerMarker OffensiveEvaluationMarker =
            new("TinyKingdom.AI.OffensiveEvaluation");
        private static readonly ProfilerMarker TargetSelectionMarker =
            new("TinyKingdom.AI.StrategicTargetSelection");
        private static readonly ProfilerMarker OffensiveAdvanceMarker =
            new("TinyKingdom.AI.OffensiveAdvance");
        private static readonly ProfilerMarker OffensiveRegroupMarker =
            new("TinyKingdom.AI.OffensiveRegroup");
        private static readonly ProfilerMarker CombatStrengthAssessmentMarker =
            new("TinyKingdom.AI.CombatStrengthAssessment");
        private static readonly ProfilerMarker RetreatEvaluationMarker =
            new("TinyKingdom.AI.RetreatEvaluation");
        private static readonly ProfilerMarker RetreatExecutionMarker =
            new("TinyKingdom.AI.RetreatExecution");
        private static readonly ProfilerMarker RecoveryEvaluationMarker =
            new("TinyKingdom.AI.RecoveryEvaluation");
        private static readonly ProfilerMarker LivenessEvaluationMarker =
            new("TinyKingdom.AI.MilitaryLiveness");

        [SerializeField, Min(1f)] private float baseDefenseRadius = 9f;
        [SerializeField, Min(1f)] private float defenseLeashRadius = 12f;
        [SerializeField, Min(0.5f)] private float stagingRegionRadius = 3f;
        [SerializeField, Min(0.1f)] private float destinationArrivalRadius = 0.45f;
        [SerializeField, Min(0.5f)] private float commandRetrySeconds = 1.5f;
        [Header("Phase 7C.2 offense (MVP / provisional)")]
        [SerializeField, Min(1)] private int minOffensiveMilitaryPopulation = 6;
        [SerializeField, Range(0.5f, 1f)] private float assembledFraction = 0.75f;
        [SerializeField, Min(1f)] private float preparationTimeoutSeconds = 10f;
        [SerializeField, Range(0.5f, 1f)] private float timeoutAssemblyFraction = 0.60f;
        [SerializeField, Min(1f)] private float offensiveCohesionRadius = 6f;
        [SerializeField, Min(0.5f)] private float offensiveReevaluationSeconds = 3f;
        [SerializeField, Range(1, 6)] private int maximumObjectiveFailures = 3;
        [Header("Phase 7C.3 retreat/recovery (MVP / provisional)")]
        [SerializeField] private CombatStrengthTuning combatStrength = new();
        [SerializeField, Min(1f)] private float combatAssessmentRadius = 9f;
        [SerializeField, Range(0.1f, 0.9f)] private float retreatRemainingForceRatio = 0.45f;
        [SerializeField, Range(0.1f, 0.9f)] private float retreatAverageHealthRatio = 0.35f;
        [SerializeField, Min(1.01f)] private float retreatEnemyStrengthRatio = 1.40f;
        [SerializeField, Range(1, 5)] private int retreatStableEvaluations = 2;
        [SerializeField, Range(0.5f, 1f)] private float retreatCompletionFraction = 0.75f;
        [SerializeField, Min(1f)] private float retreatCompletionTimeout = 12f;
        [SerializeField, Min(0f)] private float minimumRecoveryDuration = 3f;
        [SerializeField, Min(1f)] private float maximumRecoveryDuration = 10f;
        [SerializeField, Range(0.1f, 1f)] private float recoveryAverageHealthRatio = 0.65f;
        [SerializeField, Range(0.5f, 1f)] private float recoveryAssemblyFraction = 0.75f;
        [SerializeField, Min(0.1f)] private float recoveryMinimumEffectiveStrength = 4f;
        [SerializeField, Min(1f)] private float failedObjectiveCooldown = 12f;
        [Header("Phase 7E.5 liveness")]
        [SerializeField, Min(2f)] private float militaryNoProgressSeconds = 12f;
        [SerializeField, Min(1f)] private float stagingRecoveryCooldown = 4f;
        [SerializeField, Min(2f)] private float attackIdleTimeout = 10f;
        [SerializeField, Min(1)] private int offensiveStagingCommandsPerTick = 1;
        [SerializeField, Min(1)] private int offensiveAdvanceCommandsPerTick = 1;
        [SerializeField, Min(1)] private int offensiveAssaultCommandsPerTick = 1;
        [SerializeField] private TacticalArmyController tactical = new();

        private readonly ArmyGroup army = new();
        [NonSerialized] private GoldMineStrategyModel activeGoldMineModel;
        private float mineOccupationWaitStart = -1f;
        private readonly Dictionary<UnitController, EntityId> attackIntents = new();
        private readonly Dictionary<UnitController, Vector3> moveIntents = new();
        private readonly Dictionary<UnitController, Vector3> attackMoveIntents = new();
        private readonly Dictionary<UnitController, Vector3> formationIntents = new();
        private readonly Dictionary<UnitController, float> retryAfter = new();
        private readonly Dictionary<UnitController, CommanderCommandResult>
            lastCommandResults = new();
        private readonly HashSet<CombatController> combatProgressSources = new();
        private readonly HashSet<CombatController> currentCombatProgressSources = new();
        private readonly List<CombatController> staleCombatProgressSources = new();
        private readonly List<UnitController> operationMembers = new();
        private readonly List<UnitController> staleActors = new();
        private readonly Dictionary<UnitController, Vector3> retreatDestinations = new();
        private bool hasCurrentThreat;
        private EntityId currentThreatId;
        private UnitType currentThreatType;
        private Vector3 currentThreatPosition;
        private bool hasBaseAnchor;
        private Vector3 baseAnchor;
        private bool hasStagingPosition;
        private Vector3 stagingPosition;
        private int assignmentMembershipVersion = -1;
        private bool hasOffensiveTarget;
        private OffensiveTargetSnapshot offensiveTarget;
        private Vector3 currentObjectivePosition;
        private float operationStartTime;
        private float nextOffensiveEvaluationTime;
        private int objectiveFailureCount;
        private bool demoRouteDispatched;
        private bool demoBuildingOrderDispatched;
        private OffensiveTargetSnapshot? demoCastleAfterBarracks;
        private bool coordinatedIslandAssault;
        private readonly List<UnitController> islandDefenseGroup = new();
        private const float IslandDefenseGroupRadius = 5f;
        private bool islandTrainingOpponent;
        private const int IslandCommandsPerTick = 6;
        private EntityId blockedTargetId;
        private float blockedTargetUntil;
        private string objectiveReason = "None";
        private bool cohesionRegroup;
        private Vector3 cohesionRegroupPosition;
        private bool campaignBaselineCaptured;
        private int campaignStartPopulation;
        private int campaignStartMemberCount;
        private float campaignStartEffectiveStrength;
        private float campaignStartAverageHealth;
        private int campaignSurvivorCount;
        private float remainingForceRatio = 1f;
        private float ownEffectiveStrength;
        private float directCombatStrength;
        private float supportStrength;
        private float visibleEnemyStrength;
        private float enemyStrengthRatio;
        private float averageArmyHealth = 1f;
        private int visibleRelevantEnemyCount;
        private Vector3 armyCentroid;
        private RetreatReason currentRetreatReason;
        private RetreatReason pendingRetreatReason;
        private int retreatDangerEvaluations;
        private float retreatStartTime;
        private float retreatArrivedFraction;
        private float recoveryStartTime;
        private bool recoveryReady;
        private bool resumeRecoveryAfterDefense;
        private float preparationStartTime;
        private float lastMilitaryProgressTime = -1f;
        private float lastAcceptedMilitaryCommandTime = -1f;
        private float lastRejectedMilitaryCommandTime = -1f;
        private float lastMilitaryPathRequestTime = -1f;
        private float lastCombatActionTime = -1f;
        private CommanderCommandResult lastMilitaryCommandResult;
        private float lastObservedAssemblyFraction;
        private int lastObservedMembershipVersion = -1;
        private float nextStagingRecoveryTime;
        private int stagingRecoveryCount;
        private int offensiveStagingCommandCursor;
        private int offensiveAdvanceCommandCursor;
        private int offensiveAssaultCommandCursor;
        private int advanceSweepMemberCount;
        private int advanceSweepVisited;
        private int advanceSweepAttempted;
        private int advanceSweepAccepted;
        private int advanceSweepFailed;

        public ArmyGroup Army => army;
        public TacticalArmyController Tactical => tactical ??= new TacticalArmyController();
        public float BaseDefenseRadius => Mathf.Max(1f, baseDefenseRadius);
        public float DefenseLeashRadius => Mathf.Max(BaseDefenseRadius, defenseLeashRadius);
        internal const int DemoCastleAssaultMinimum = 4;
        internal const int IslandAssaultMinimum = 6;
        public int MinOffensiveMilitaryPopulation
            => Mathf.Max(1, minOffensiveMilitaryPopulation);
        public float AssembledFraction => Mathf.Clamp(assembledFraction, 0.5f, 1f);
        public float PreparationTimeoutSeconds => preparationTimeoutSeconds > 0f
            ? preparationTimeoutSeconds : 10f;
        public float TimeoutAssemblyFraction => Mathf.Clamp(timeoutAssemblyFraction,
            0.5f, AssembledFraction);
        public bool HasBaseAnchor => hasBaseAnchor;
        public Vector3 BaseAnchor => baseAnchor;
        public bool HasStagingPosition => hasStagingPosition;
        public Vector3 StagingPosition => stagingPosition;
        public bool HasCurrentThreat => hasCurrentThreat;
        public EntityId CurrentThreatId => currentThreatId;
        public UnitType CurrentThreatType => currentThreatType;
        public Vector3 CurrentThreatPosition => currentThreatPosition;
        public int VisibleThreatCount { get; private set; }
        public int AvailableMilitaryPopulation => PopulationOf(army.Members);
        public bool HasOffensiveTarget => hasOffensiveTarget;
        public OffensiveTargetSnapshot OffensiveTarget => offensiveTarget;
        public Vector3 CurrentObjectivePosition => currentObjectivePosition;
        public bool IsOffensiveTargetVisible { get; private set; }
        public string ObjectiveReason => objectiveReason;
        public float OperationStartTime => operationStartTime;
        public int ObjectiveFailureCount => objectiveFailureCount;
        internal bool DemoRouteDispatched => demoRouteDispatched;
        internal bool DemoBuildingOrderDispatched => demoBuildingOrderDispatched;
        public int OffensiveParticipantCount => operationMembers.Count;
        public CombatStrengthTuning CombatStrength
            => combatStrength ??= new CombatStrengthTuning();
        public float CombatAssessmentRadius
            => combatAssessmentRadius > 0f ? combatAssessmentRadius : 9f;
        public float RetreatRemainingForceRatio => retreatRemainingForceRatio > 0f
            ? Mathf.Clamp01(retreatRemainingForceRatio) : 0.45f;
        public float RetreatAverageHealthRatio => retreatAverageHealthRatio > 0f
            ? Mathf.Clamp01(retreatAverageHealthRatio) : 0.35f;
        public float RetreatEnemyStrengthRatio => retreatEnemyStrengthRatio > 1f
            ? retreatEnemyStrengthRatio : 1.40f;
        public int RetreatStableEvaluations
            => retreatStableEvaluations > 0 ? retreatStableEvaluations : 2;
        public float RetreatCompletionFraction => retreatCompletionFraction > 0f
            ? Mathf.Clamp(retreatCompletionFraction, 0.5f, 1f) : 0.75f;
        public float RetreatCompletionTimeout
            => retreatCompletionTimeout > 0f ? retreatCompletionTimeout : 12f;
        public float MinimumRecoveryDuration
            => minimumRecoveryDuration > 0f ? minimumRecoveryDuration : 3f;
        public float MaximumRecoveryDuration
            => maximumRecoveryDuration > MinimumRecoveryDuration
                ? maximumRecoveryDuration : Mathf.Max(10f, MinimumRecoveryDuration + 1f);
        public float RecoveryAverageHealthRatio => recoveryAverageHealthRatio > 0f
            ? Mathf.Clamp01(recoveryAverageHealthRatio) : 0.65f;
        public float RecoveryAssemblyFraction => recoveryAssemblyFraction > 0f
            ? Mathf.Clamp(recoveryAssemblyFraction, 0.5f, 1f) : 0.75f;
        public float RecoveryMinimumEffectiveStrength
            => recoveryMinimumEffectiveStrength > 0f ? recoveryMinimumEffectiveStrength : 4f;
        public float FailedObjectiveCooldown
            => failedObjectiveCooldown > 0f ? failedObjectiveCooldown : 12f;
        public float MilitaryNoProgressSeconds => Mathf.Max(2f, militaryNoProgressSeconds);
        public float StagingRecoveryCooldown => Mathf.Max(1f, stagingRecoveryCooldown);
        public float AttackIdleTimeout => Mathf.Max(2f, attackIdleTimeout);
        public int OffensiveStagingCommandsPerTick
            => coordinatedIslandAssault ? IslandCommandsPerTick
                : Mathf.Max(1, offensiveStagingCommandsPerTick);
        public float LastMilitaryProgressTime => lastMilitaryProgressTime;
        public float LastAcceptedMilitaryCommandTime => lastAcceptedMilitaryCommandTime;
        public float LastRejectedMilitaryCommandTime => lastRejectedMilitaryCommandTime;
        public float LastMilitaryPathRequestTime => lastMilitaryPathRequestTime;
        public CommanderCommandResult LastMilitaryCommandResult => lastMilitaryCommandResult;
        public int StagingRecoveryCount => stagingRecoveryCount;
        public bool TryGetLastCommandResult(UnitController actor,
            out CommanderCommandResult result)
            => lastCommandResults.TryGetValue(actor, out result);
        public int CampaignStartPopulation => campaignStartPopulation;
        public int CampaignStartMemberCount => campaignStartMemberCount;
        public float CampaignStartEffectiveStrength => campaignStartEffectiveStrength;
        public float CampaignStartAverageHealth => campaignStartAverageHealth;
        public int CampaignSurvivorCount => campaignSurvivorCount;
        public float RemainingForceRatio => remainingForceRatio;
        public float OwnEffectiveStrength => ownEffectiveStrength;
        public float DirectCombatStrength => directCombatStrength;
        public float SupportStrength => supportStrength;
        public float VisibleRelevantEnemyStrength => visibleEnemyStrength;
        public float EnemyStrengthRatio => enemyStrengthRatio;
        public float AverageArmyHealth => averageArmyHealth;
        public int VisibleRelevantEnemyCount => visibleRelevantEnemyCount;
        public Vector3 ArmyCentroid => armyCentroid;
        public RetreatReason CurrentRetreatReason => currentRetreatReason;
        public float RetreatStartTime => retreatStartTime;
        public float RetreatArrivedFraction => retreatArrivedFraction;
        public float RecoveryStartTime => recoveryStartTime;
        public float RecoveryDuration(float now) => army.State == ArmyGroupState.Recovering
            ? Mathf.Max(0f, now - recoveryStartTime) : 0f;
        public bool RecoveryReady => recoveryReady;
        public uint OffensiveLaunchSerial { get; private set; }
        public float LastOffensiveLaunchTime { get; private set; } = -1f;
        public uint TargetCompletionSerial { get; private set; }
        public float LastTargetCompletionTime { get; private set; } = -1f;

        private bool IsOffensiveState => army.State is ArmyGroupState.PreparingAttack
            or ArmyGroupState.Advancing or ArmyGroupState.Assaulting;
        private bool IsWithdrawalState => army.State is ArmyGroupState.Retreating
            or ArmyGroupState.Recovering;

        public void Tick(CommanderDecisionContext context,
            ProductionCommanderPolicy production, UnitController activeScout,
            StrategicSafetyMap safety)
            => Tick(context, production, activeScout, safety, null);

        public void Tick(CommanderDecisionContext context,
            ProductionCommanderPolicy production, UnitController activeScout,
            StrategicSafetyMap safety, StrategicIntentEvaluator strategy,
            HashSet<UnitController> detachedUnits = null, bool demoAssaultApproved = false,
            bool qlIsland = false, bool qlOpponent = false)
        {
            coordinatedIslandAssault = qlIsland;
            islandTrainingOpponent = qlOpponent;
            activeGoldMineModel = strategy?.GoldMine;
            army.Synchronize(context.Knowledge, activeScout, detachedUnits);
            Tactical.Refresh(army.Members, context.Knowledge, army.State, context.Time);
            CombatStrengthEvaluator.OwnArmy(army.Members, CombatStrength,
                out directCombatStrength, out supportStrength);
            RemoveStaleIntents();
            RemoveStaleOperationMembers();
            RefreshAnchorAndStaging(context.Knowledge, production);
            ObserveMovementProgress(context.Time);
            VisibleThreatCount = 0;
            bool demoAttack = demoAssaultApproved && strategy?.CurrentIntent == StrategicIntent.Attack;
            if (!hasBaseAnchor || (!hasStagingPosition && !demoAttack))
            {
                ClearThreat();
                CancelOffense("Missing base or staging", context.Time, false);
                army.SetState(ArmyGroupState.Forming);
                return;
            }

            bool requireEmergency = IsOffensiveState || IsWithdrawalState
                || (coordinatedIslandAssault && demoAttack);
            bool retained = TryRetainCurrentThreat(context.Knowledge,
                requireEmergency, out VisibleUnitObservation threat);
            if (retained)
                VisibleThreatCount = CountVisibleDefenseThreats(context.Knowledge,
                    requireEmergency);
            if (!retained)
                retained = TrySelectThreat(context.Knowledge, requireEmergency, out threat);
            if (retained)
            {
                if (army.State == ArmyGroupState.Retreating)
                    retreatArrivedFraction = FractionInside(operationMembers,
                        stagingPosition, stagingRegionRadius);
                if (army.State == ArmyGroupState.Retreating
                    && retreatArrivedFraction < 0.5f)
                {
                    TickRetreat(context, safety);
                    return;
                }
                bool shouldResumeRecovery = IsWithdrawalState
                    || resumeRecoveryAfterDefense;
                if (IsOffensiveState)
                    CancelOffense("Base defense interrupted offense", context.Time, false);
                else if (army.State == ArmyGroupState.Retreating)
                    ClearCampaignForRecovery();
                EndRetreatDirectives(army.Members);
                resumeRecoveryAfterDefense = shouldResumeRecovery;
                bool changed = !hasCurrentThreat
                    || currentThreatId.CompareTo(threat.RuntimeId) != 0;
                hasCurrentThreat = true;
                currentThreatId = threat.RuntimeId;
                currentThreatType = threat.UnitType;
                currentThreatPosition = threat.Position;
                if (changed) ClearCommandIntents();
                army.SetState(ArmyGroupState.Defending);
                Defend(context, threat);
                return;
            }

            bool wasDefending = hasCurrentThreat || army.State == ArmyGroupState.Defending;
            ClearThreat();
            if (army.Members.Count == 0)
            {
                if (IsOffensiveState || IsWithdrawalState || resumeRecoveryAfterDefense)
                    EnterRecovering(context, "Army unavailable");
                else
                {
                    CancelOffense("Army unavailable", context.Time, true);
                    army.SetState(ArmyGroupState.Forming);
                }
                return;
            }
            if (wasDefending)
            {
                if (resumeRecoveryAfterDefense)
                {
                    resumeRecoveryAfterDefense = false;
                    recoveryStartTime = context.Time;
                    recoveryReady = false;
                    army.SetState(ArmyGroupState.Recovering);
                    InvalidateMoveAssignments();
                }
                else
                {
                    CancelOffense("Defense resolved", context.Time, false);
                    army.SetState(ArmyGroupState.Regrouping);
                    InvalidateMoveAssignments();
                }
            }

            if (army.State == ArmyGroupState.Retreating)
            {
                TickRetreat(context, safety);
                return;
            }

            if (army.State == ArmyGroupState.Recovering)
            {
                TickRecovery(context);
                return;
            }

            if (army.State == ArmyGroupState.PreparingAttack && !cohesionRegroup
                && HandleStagingNoProgress(context, production, strategy,
                    "PrepareAttack made no progress")) return;

            if (IsOffensiveState)
            {
                TickOffense(context, safety, strategy, demoAssaultApproved);
                return;
            }

            float requiredAssembly = recoveryReady
                ? RecoveryAssemblyFraction : AssembledFraction;
            bool sufficientlyStaged = IsAssemblyReady(army.Members, stagingPosition,
                requiredAssembly);
            if (demoAttack && context.Time >= nextOffensiveEvaluationTime
                && TryCollectDemoAssaultGroup(strategy.StrategicTarget.BuildingType
                    is BuildingType.Castle or BuildingType.Barracks))
            {
                if (TryBeginOffense(context, safety, strategy, useAssembledGroup: true)) return;
                operationMembers.Clear();
            }
            if (coordinatedIslandAssault && demoAttack && hasStagingPosition
                && context.Time >= nextOffensiveEvaluationTime
                && strategy.StrategicTarget.BuildingType == BuildingType.Castle
                && army.Members.Count(HarassmentRuleBasedCommander.IsReadyCastleAttacker)
                    >= IslandAssaultMinimum
                && TryBeginOffense(context, safety, strategy))
                return;
            if (!hasStagingPosition) return;
            if (!sufficientlyStaged)
            {
                if (HandleStagingNoProgress(context, production, strategy,
                        "Routine staging stalled")) return;
                if (army.State != ArmyGroupState.Regrouping)
                    army.SetState(ArmyGroupState.Forming);
                Regroup(context, army.State == ArmyGroupState.Regrouping);
                return;
            }

            army.SetState(ArmyGroupState.Ready);
            moveIntents.Clear();
            retryAfter.Clear();
            if (context.Time >= nextOffensiveEvaluationTime
                && AvailableMilitaryPopulation >= MinOffensiveMilitaryPopulation)
            {
                if (strategy == null)
                    TryBeginOffense(context, safety);
                else if (strategy.CurrentIntent is StrategicIntent.Attack
                    or StrategicIntent.SecureGoldMine)
                    TryBeginOffense(context, safety, strategy);
                else if (strategy.CurrentIntent == StrategicIntent.PrepareAttack)
                    objectiveReason = "Strategic preparation at staging";
            }
        }

        public void Reset()
        {
            demoCastleAfterBarracks = null;
            mineOccupationWaitStart = -1f;
            foreach (CombatController combat in combatProgressSources)
                if (combat != null) combat.AttackPerformed -= OnCombatAction;
            combatProgressSources.Clear();
            currentCombatProgressSources.Clear();
            staleCombatProgressSources.Clear();
            EndRetreatDirectives(army.Members);
            army.Reset();
            attackIntents.Clear();
            moveIntents.Clear();
            attackMoveIntents.Clear();
            formationIntents.Clear();
            retryAfter.Clear();
            lastCommandResults.Clear();
            operationMembers.Clear();
            staleActors.Clear();
            retreatDestinations.Clear();
            hasCurrentThreat = false;
            hasBaseAnchor = false;
            hasStagingPosition = false;
            hasOffensiveTarget = false;
            IsOffensiveTargetVisible = false;
            VisibleThreatCount = 0;
            assignmentMembershipVersion = -1;
            operationStartTime = nextOffensiveEvaluationTime = 0f;
            objectiveFailureCount = 0;
            blockedTargetId = default;
            blockedTargetUntil = 0f;
            objectiveReason = "None";
            cohesionRegroup = false;
            campaignBaselineCaptured = false;
            campaignStartPopulation = 0;
            campaignStartMemberCount = 0;
            campaignStartEffectiveStrength = 0f;
            campaignStartAverageHealth = 0f;
            campaignSurvivorCount = 0;
            remainingForceRatio = 1f;
            ownEffectiveStrength = 0f;
            directCombatStrength = 0f;
            supportStrength = 0f;
            visibleEnemyStrength = 0f;
            enemyStrengthRatio = 0f;
            averageArmyHealth = 1f;
            visibleRelevantEnemyCount = 0;
            armyCentroid = default;
            currentRetreatReason = RetreatReason.None;
            pendingRetreatReason = RetreatReason.None;
            retreatDangerEvaluations = 0;
            retreatStartTime = 0f;
            retreatArrivedFraction = 0f;
            recoveryStartTime = 0f;
            recoveryReady = false;
            resumeRecoveryAfterDefense = false;
            preparationStartTime = 0f;
            lastMilitaryProgressTime = -1f;
            lastAcceptedMilitaryCommandTime = -1f;
            lastRejectedMilitaryCommandTime = -1f;
            lastMilitaryPathRequestTime = -1f;
            lastCombatActionTime = -1f;
            lastMilitaryCommandResult = default;
            lastObservedAssemblyFraction = 0f;
            lastObservedMembershipVersion = -1;
            nextStagingRecoveryTime = 0f;
            stagingRecoveryCount = 0;
            offensiveStagingCommandCursor = 0;
            offensiveAdvanceCommandCursor = 0;
            offensiveAssaultCommandCursor = 0;
            advanceSweepMemberCount = advanceSweepVisited = 0;
            advanceSweepAttempted = advanceSweepAccepted = advanceSweepFailed = 0;
            OffensiveLaunchSerial = 0;
            LastOffensiveLaunchTime = -1f;
            TargetCompletionSerial = 0;
            LastTargetCompletionTime = -1f;
            tactical?.Reset();
        }

        private void TryBeginOffense(CommanderDecisionContext context,
            StrategicSafetyMap safety)
            => TryBeginOffense(context, safety, null);

        private bool TryBeginOffense(CommanderDecisionContext context,
            StrategicSafetyMap safety, StrategicIntentEvaluator strategy)
            => TryBeginOffense(context, safety, strategy, useAssembledGroup: false);

        private bool TryBeginOffense(CommanderDecisionContext context,
            StrategicSafetyMap safety, StrategicIntentEvaluator strategy, bool useAssembledGroup)
        {
            using (OffensiveEvaluationMarker.Auto())
            {
                bool selected = strategy != null
                    ? TryResolveStrategicTarget(context, strategy, out OffensiveTargetSnapshot target)
                    : TrySelectOffensiveTarget(context, safety, out target);
                if (!selected && useAssembledGroup && !coordinatedIslandAssault)
                    selected = TrySelectOffensiveTarget(context, safety, out target);
                if (!selected)
                {
                    objectiveReason = "No legally known strategic target";
                    nextOffensiveEvaluationTime = context.Time + 1f;
                    return false;
                }
                if (coordinatedIslandAssault && !useAssembledGroup
                    && army.Members.Count(HarassmentRuleBasedCommander.IsReadyCastleAttacker)
                        < IslandAssaultMinimum)
                    return false;
                hasOffensiveTarget = true;
                offensiveTarget = target;
                mineOccupationWaitStart = -1f;
                currentObjectivePosition = target.LastKnownPosition;
                IsOffensiveTargetVisible = context.Knowledge
                    .TryGetVisibleEnemyBuilding(target.RuntimeId, out _);
                if (!useAssembledGroup)
                {
                    operationMembers.Clear();
                    for (int i = 0; i < army.Members.Count; i++)
                        operationMembers.Add(army.Members[i]);
                }
                operationStartTime = context.Time;
                preparationStartTime = context.Time;
                lastMilitaryProgressTime = context.Time;
                stagingRecoveryCount = 0;
                offensiveStagingCommandCursor = 0;
                offensiveAdvanceCommandCursor = 0;
                objectiveFailureCount = 0;
                demoRouteDispatched = false;
                demoBuildingOrderDispatched = false;
                cohesionRegroup = false;
                recoveryReady = false;
                ClearCampaignBaseline();
                currentRetreatReason = RetreatReason.None;
                objectiveReason = "Selected " + target.Category;
                ClearCommandIntents();
                if (useAssembledGroup)
                {
                    CaptureCampaignBaseline();
                    army.SetState(ArmyGroupState.Advancing);
                    OffensiveLaunchSerial++;
                    LastOffensiveLaunchTime = context.Time;
                    objectiveReason = "Demo assault launched from current formation";
                    IssueAdvance(context, safety, true, demoAssault: true);
                }
                else
                {
                    army.SetState(ArmyGroupState.PreparingAttack);
                    if (coordinatedIslandAssault && target.BuildingType == BuildingType.Castle)
                        Debug.Log($"QL_CASTLE_ASSEMBLY faction={context.Faction} time={context.Time:F2}"
                            + $" troops={operationMembers.Count}");
                    TickPreparingAttack(context, safety);
                }
                return true;
            }
        }

        private bool TryCollectDemoAssaultGroup(bool earlyAssault, bool preserveOperationOnFailure = false)
        {
            Vector3 center = Tactical.ArmyAnchor;
            float radiusSquared = offensiveCohesionRadius * offensiveCohesionRadius;
            if (preserveOperationOnFailure && army.Members.Count(member =>
                    HarassmentRuleBasedCommander.IsReadyCastleAttacker(member)
                    && DistanceSquared(member.transform.position, center) <= radiusSquared)
                    < IslandAssaultMinimum)
                return false;
            operationMembers.Clear();
            int readyAttackers = 0;
            foreach (UnitController member in army.Members)
            {
                if (DistanceSquared(member.transform.position, center) <= radiusSquared)
                {
                    operationMembers.Add(member);
                    if (earlyAssault && HarassmentRuleBasedCommander.IsReadyCastleAttacker(member))
                        readyAttackers++;
                }
            }
            if (earlyAssault && readyAttackers < DemoCastleAssaultMinimum
                && !coordinatedIslandAssault)
            {
                // Units at different rally points can converge while advancing.
                operationMembers.Clear();
                readyAttackers = 0;
                foreach (UnitController member in army.Members)
                {
                    if (!HarassmentRuleBasedCommander.IsReadyCastleAttacker(member)) continue;
                    operationMembers.Add(member);
                    readyAttackers++;
                }
            }
            if (earlyAssault
                ? readyAttackers >= (coordinatedIslandAssault ? IslandAssaultMinimum : DemoCastleAssaultMinimum)
                : operationMembers.Count >= Mathf.CeilToInt(army.Members.Count * 0.75f)
                && PopulationOf(operationMembers) >= MinOffensiveMilitaryPopulation
                && CountAttackers(operationMembers) >= 2)
                return true;
            operationMembers.Clear();
            return false;
        }

        private bool TryResolveStrategicTarget(CommanderDecisionContext context,
            StrategicIntentEvaluator strategy, out OffensiveTargetSnapshot result)
        {
            result = default;
            if (strategy.CurrentIntent == StrategicIntent.SecureGoldMine)
            {
                GoldMineOpportunity opportunity = strategy.GoldMine.Current;
                GoldMineKnowledgeSnapshot mine = opportunity.Mine;
                if (!opportunity.IsValid || !opportunity.CanCommit
                    || context.Time < blockedTargetUntil
                        && mine.RuntimeId.CompareTo(blockedTargetId) == 0)
                    return false;
                result = new OffensiveTargetSnapshot(mine.RuntimeId,
                    BuildingType.GoldMine, mine.HasOwner ? mine.Owner : Faction.Goblin,
                    mine.LastSeenPosition, StrategicTargetCategory.EconomicBuilding,
                    context.Knowledge.IsCellVisible(context.Knowledge.TeamVision
                        .GridManager.WorldToGrid(mine.LastSeenPosition)));
                return true;
            }
            if (strategy == null || !strategy.TryGetTarget(
                    out StrategicTargetSnapshot selected)) return false;
            IReadOnlyList<BuildingKnowledgeSnapshot> known =
                context.Knowledge.KnownEnemyBuildings;
            for (int i = 0; i < known.Count; i++)
            {
                BuildingKnowledgeSnapshot item = known[i];
                if (item.RuntimeId.CompareTo(selected.RuntimeId) != 0
                    || !FactionRelations.AreHostile(context.Faction, item.Faction)
                    || !item.WasCompleted || item.LastSeenHealthRatio <= 0f
                    || item.BuildingType == BuildingType.GoldMine
                    || context.Time < blockedTargetUntil
                        && item.RuntimeId.CompareTo(blockedTargetId) == 0) continue;
                bool visible = context.Knowledge.TryGetVisibleEnemyBuilding(
                    item.RuntimeId, out _);
                result = new OffensiveTargetSnapshot(item.RuntimeId,
                    item.BuildingType, item.Faction, item.LastSeenPosition,
                    CategoryOf(item.BuildingType), visible);
                return true;
            }
            return false;
        }

        private bool TrySelectOffensiveTarget(CommanderDecisionContext context,
            StrategicSafetyMap safety, out OffensiveTargetSnapshot result)
        {
            using (TargetSelectionMarker.Auto())
            {
                result = default;
                bool found = false;
                int bestPriority = int.MaxValue;
                int bestVisibility = int.MaxValue;
                int bestRisk = int.MaxValue;
                float bestDistance = float.PositiveInfinity;
                IReadOnlyList<BuildingKnowledgeSnapshot> known =
                    context.Knowledge.KnownEnemyBuildings;
                for (int i = 0; i < known.Count; i++)
                {
                    BuildingKnowledgeSnapshot item = known[i];
                    if (!FactionRelations.AreHostile(context.Faction, item.Faction)
                        || !item.WasCompleted || item.LastSeenHealthRatio <= 0f
                        || item.BuildingType == BuildingType.GoldMine
                        || context.Time < blockedTargetUntil
                            && item.RuntimeId.CompareTo(blockedTargetId) == 0)
                        continue;
                    StrategicTargetCategory category = CategoryOf(item.BuildingType);
                    int priority = PriorityOf(category);
                    bool visible = context.Knowledge.TryGetVisibleEnemyBuilding(
                        item.RuntimeId, out _);
                    int visibility = visible ? 0 : 1;
                    int risk = safety != null
                        ? (int)safety.RiskAt(item.LastSeenPosition,
                            StrategicPathKind.Army) : 0;
                    float distance = DistanceSquared(item.LastSeenPosition,
                        stagingPosition);
                    if (!found || priority < bestPriority
                        || priority == bestPriority && visibility < bestVisibility
                        || priority == bestPriority && visibility == bestVisibility
                            && risk < bestRisk
                        || priority == bestPriority && visibility == bestVisibility
                            && risk == bestRisk
                            && distance < bestDistance - 0.0001f
                        || priority == bestPriority && visibility == bestVisibility
                            && risk == bestRisk
                            && Mathf.Abs(distance - bestDistance) <= 0.0001f
                            && item.RuntimeId.CompareTo(result.RuntimeId) < 0)
                    {
                        found = true;
                        bestPriority = priority;
                        bestVisibility = visibility;
                        bestRisk = risk;
                        bestDistance = distance;
                        result = new OffensiveTargetSnapshot(item.RuntimeId,
                            item.BuildingType, item.Faction, item.LastSeenPosition,
                            category, visible);
                    }
                }
                return found;
            }
        }

        private void TickOffense(CommanderDecisionContext context,
            StrategicSafetyMap safety, StrategicIntentEvaluator strategy, bool demoAssaultApproved)
        {
            if (hasOffensiveTarget
                && offensiveTarget.BuildingType == BuildingType.GoldMine)
            {
                if (coordinatedIslandAssault && demoAssaultApproved
                    && strategy?.CurrentIntent == StrategicIntent.Attack
                    && strategy.StrategicTarget.BuildingType == BuildingType.Castle
                    && TryResolveStrategicTarget(context, strategy, out _)
                    && TryCollectDemoAssaultGroup(earlyAssault: true, preserveOperationOnFailure: true)
                    && TryBeginOffense(context, safety, strategy, useAssembledGroup: true))
                    return;
                if (coordinatedIslandAssault && demoAssaultApproved && hasStagingPosition
                    && strategy?.CurrentIntent == StrategicIntent.Attack
                    && strategy.StrategicTarget.BuildingType == BuildingType.Castle
                    && context.Time >= nextOffensiveEvaluationTime
                    && army.Members.Count(HarassmentRuleBasedCommander.IsReadyCastleAttacker)
                        >= IslandAssaultMinimum
                    && TryBeginOffense(context, safety, strategy))
                    return;
                TickGoldMineOffense(context, safety, strategy?.GoldMine);
                return;
            }
            StrategicTargetSnapshot preferred = strategy != null ? strategy.StrategicTarget : default;
            if (demoCastleAfterBarracks.HasValue
                && !context.Knowledge.TryGetVisibleEnemyBuilding(offensiveTarget.RuntimeId, out _)
                && (IsObjectiveAreaVisible(context.Knowledge)
                    || !TryRefreshObjective(context.Knowledge, out _)))
            {
                ResumeCastleAssault(context);
            }
            if ((demoAssaultApproved || islandTrainingOpponent) && !demoCastleAfterBarracks.HasValue
                && army.State is (ArmyGroupState.Advancing or ArmyGroupState.Assaulting)
                && offensiveTarget.BuildingType != BuildingType.Castle
                && preferred.IsValid && preferred.BuildingType == BuildingType.Castle
                && TryResolveStrategicTarget(context, strategy, out OffensiveTargetSnapshot castle))
            {
                if (!coordinatedIslandAssault && !islandTrainingOpponent
                    && offensiveTarget.BuildingType == BuildingType.Barracks
                    && context.Knowledge.TryGetVisibleEnemyBuilding(offensiveTarget.RuntimeId, out BuildingKnowledgeSnapshot barracks)
                    && IsBarracksAtAssaultFront(barracks, castle.LastKnownPosition))
                    demoCastleAfterBarracks = castle;
                else
                {
                    SwitchDemoObjective(context, castle);
                    if (islandTrainingOpponent)
                        Debug.Log($"QL_CASTLE_RETARGET faction={context.Faction} time={context.Time:F2}");
                }
            }
            if (islandTrainingOpponent && hasOffensiveTarget
                && (offensiveTarget.BuildingType == BuildingType.Castle || demoCastleAfterBarracks.HasValue))
                demoAssaultApproved = true;
            if (!hasOffensiveTarget || !TryRefreshObjective(context.Knowledge,
                    out BuildingKnowledgeSnapshot known))
            {
                CompleteOffense(context, "Objective resolved or stale");
                return;
            }
            currentObjectivePosition = known.LastSeenPosition;
            IsOffensiveTargetVisible = context.Knowledge.TryGetVisibleEnemyBuilding(
                offensiveTarget.RuntimeId, out BuildingKnowledgeSnapshot visible);
            if (IsOffensiveTargetVisible)
                currentObjectivePosition = visible.LastSeenPosition;
            else if (IsObjectiveAreaVisible(context.Knowledge))
            {
                CompleteOffense(context, "Remembered objective area cleared");
                return;
            }

            if (operationMembers.Count == 0 || CountAttackers(operationMembers) == 0)
            {
                EnterRecovering(context, "Campaign force unavailable");
                return;
            }

            if (!coordinatedIslandAssault && !islandTrainingOpponent && demoAssaultApproved
                && army.State is (ArmyGroupState.Advancing or ArmyGroupState.Assaulting)
                && TryClearBarracksApproach(context, onlyAtFront: true))
            {
                army.SetState(ArmyGroupState.Assaulting);
                TickAssault(context, safety, demoAssaultApproved);
                return;
            }

            // PreparingAttack has its own staging watchdog. Running the shorter
            // attack timeout here would reset its clock before it can fire.
            if (army.State != ArmyGroupState.PreparingAttack
                && HandleAttackNoProgress(context, safety, demoAssaultApproved)) return;

            if (army.State is ArmyGroupState.Advancing or ArmyGroupState.Assaulting
                && EvaluateRetreat(context))
            {
                BeginRetreat(context, safety, pendingRetreatReason);
                return;
            }

            switch (army.State)
            {
                case ArmyGroupState.PreparingAttack:
                    TickPreparingAttack(context, safety);
                    break;
                case ArmyGroupState.Advancing:
                    TickAdvance(context, safety, demoAssaultApproved);
                    break;
                case ArmyGroupState.Assaulting:
                    TickAssault(context, safety, demoAssaultApproved);
                    break;
            }
        }

        private void TickGoldMineOffense(CommanderDecisionContext context,
            StrategicSafetyMap safety, GoldMineStrategyModel mineModel)
        {
            GoldMineKnowledgeSnapshot mine = default;
            bool found = false;
            IReadOnlyList<GoldMineKnowledgeSnapshot> known =
                context.Knowledge.KnownGoldMines;
            for (int i = 0; i < known.Count; i++)
                if (known[i].RuntimeId.CompareTo(offensiveTarget.RuntimeId) == 0)
                { mine = known[i]; found = true; break; }
            if (!found || mine.State is GoldMineState.Depleted
                or GoldMineState.Destroyed)
            {
                CompleteOffense(context, "Gold Mine resolved or unavailable");
                return;
            }
            currentObjectivePosition = mine.LastSeenPosition;
            IsOffensiveTargetVisible = context.Knowledge.IsCellVisible(
                context.Knowledge.TeamVision.GridManager.WorldToGrid(
                    currentObjectivePosition));
            if (operationMembers.Count == 0 || CountAttackers(operationMembers) == 0)
            {
                EnterRecovering(context, "Mine force unavailable");
                return;
            }
            if (mineOccupationWaitStart < 0f
                && army.State != ArmyGroupState.PreparingAttack
                && HandleAttackNoProgress(context, safety)) return;
            if (army.State is ArmyGroupState.Advancing
                or ArmyGroupState.Assaulting && EvaluateRetreat(context))
            {
                BeginRetreat(context, safety, pendingRetreatReason);
                return;
            }
            if (army.State == ArmyGroupState.PreparingAttack)
            {
                TickPreparingAttack(context, safety);
                return;
            }
            Tactical.Refresh(operationMembers, context.Knowledge, army.State,
                context.Time);
            if (Tactical.TryEngageVisibleEnemies(context, operationMembers,
                    Tactical.ArmyAnchor, Tactical.LocalEngagementRadius, default,
                    safety?.ArmyTraversal, true))
            {
                mineOccupationWaitStart = -1f;
                army.SetState(ArmyGroupState.Assaulting);
                objectiveReason = "Clear visible mine guards";
                return;
            }
            if (IsOffensiveTargetVisible && context.Knowledge.IsCellExplored(
                    context.Knowledge.TeamVision.GridManager.WorldToGrid(
                        currentObjectivePosition))
                && !HasKnownMineThreat(context.Knowledge,
                    currentObjectivePosition, context.Time, mineModel)
                && (safety == null || safety.RiskAt(currentObjectivePosition,
                    StrategicPathKind.Civilian) < StrategicRisk.Dangerous)
                && Vector2.Distance(Tactical.ArmyAnchor, currentObjectivePosition)
                    <= Tactical.LocalEngagementRadius * 0.6f)
            {
                if (mine.HasOwner && mine.Owner == context.Faction
                    && mine.LastSeenOccupants >= 2)
                {
                    CompleteOffense(context, "Mine occupied by Red Pawns");
                    return;
                }
                if (mineOccupationWaitStart < 0f)
                    mineOccupationWaitStart = context.Time;
                if (context.Time - mineOccupationWaitStart > 90f)
                {
                    if (mine.HasOwner && mine.Owner == context.Faction
                        && mine.LastSeenOccupants > 0)
                        CompleteOffense(context, "Mine claimed; one Pawn mining");
                    else FailOffense(context, "Cleared mine was not occupied in time");
                    return;
                }
                army.SetState(ArmyGroupState.Assaulting);
                objectiveReason = "Cover cleared mine while Pawns enter";
                return;
            }
            mineOccupationWaitStart = -1f;
            army.SetState(ArmyGroupState.Advancing);
            objectiveReason = "Advancing on known mine guards";
            IssueAdvance(context, safety, false);
        }

        private static bool HasKnownMineThreat(FactionKnowledge knowledge,
            Vector3 mine, float now, GoldMineStrategyModel model)
        {
            if (model == null) return true;
            IReadOnlyList<GoblinKnowledgeSnapshot> goblins = knowledge.KnownGoblins;
            for (int i = 0; i < goblins.Count; i++)
                if (now - goblins[i].LastSeenTime <= model.GuardMemorySeconds
                    && Vector2.Distance(goblins[i].LastSeenPosition, mine)
                        <= model.GuardRegionRadius)
                    return true;
            IReadOnlyList<VisibleUnitObservation> visible = knowledge.VisibleEnemyUnits;
            for (int i = 0; i < visible.Count; i++)
                if (Vector2.Distance(visible[i].Position, mine)
                    <= model.WorkerSafetyRadius)
                    return true;
            return false;
        }

        private void TickPreparingAttack(CommanderDecisionContext context,
            StrategicSafetyMap safety)
        {
            using (OffensiveRegroupMarker.Auto())
            {
                Vector3 assembly = cohesionRegroup ? cohesionRegroupPosition
                    : stagingPosition;
                float assembled = FractionInside(operationMembers, assembly,
                    stagingRegionRadius);
                bool timedOut = context.Time - preparationStartTime
                    >= PreparationTimeoutSeconds;
                bool robustArrival = IsAssemblyReady(operationMembers, assembly,
                    timedOut ? TimeoutAssemblyFraction : AssembledFraction);
                bool islandAssembly = coordinatedIslandAssault && !cohesionRegroup;
                int readyAtAssembly = islandAssembly
                    ? operationMembers.Count(member =>
                        HarassmentRuleBasedCommander.IsReadyCastleAttacker(member)
                        && DistanceSquared(member.transform.position, assembly)
                            <= stagingRegionRadius * stagingRegionRadius)
                    : 0;
                if (islandAssembly ? readyAtAssembly >= IslandAssaultMinimum
                    : robustArrival && (cohesionRegroup || PopulationOf(operationMembers)
                        >= MinOffensiveMilitaryPopulation))
                {
                    if (islandAssembly)
                        operationMembers.RemoveAll(member => DistanceSquared(member.transform.position, assembly)
                            > stagingRegionRadius * stagingRegionRadius);
                    cohesionRegroup = false;
                    bool firstLaunch = !campaignBaselineCaptured;
                    if (firstLaunch)
                        CaptureCampaignBaseline();
                    moveIntents.Clear();
                    retryAfter.Clear();
                    army.SetState(ArmyGroupState.Advancing);
                    if (firstLaunch)
                    {
                        OffensiveLaunchSerial++;
                        LastOffensiveLaunchTime = context.Time;
                    }
                    IssueAdvance(context, safety, true,
                        coordinatedIslandAssault && offensiveTarget.BuildingType != BuildingType.GoldMine);
                    return;
                }
                // Staging is recovered by the no-progress watchdog in Tick:
                // stale assignments, then an alternate cell, then failure.
                // The shorter preparation timeout must not preempt that path.
                if (timedOut && cohesionRegroup
                    && assembled < TimeoutAssemblyFraction)
                {
                    FailOffense(context, "Attack assembly timed out");
                    return;
                }
                if (timedOut && !cohesionRegroup
                    && (islandAssembly
                        ? operationMembers.Count(HarassmentRuleBasedCommander.IsReadyCastleAttacker)
                            < IslandAssaultMinimum
                        : PopulationOf(operationMembers) < MinOffensiveMilitaryPopulation))
                {
                    FailOffense(context, "Attack force fell below minimum while staging");
                    return;
                }
                RegroupMembers(context, operationMembers, assembly, true,
                    safety?.ArmyTraversal, OffensiveStagingCommandsPerTick);
            }
        }

        private void TickAdvance(CommanderDecisionContext context,
            StrategicSafetyMap safety, bool demoAssaultApproved)
        {
            using (OffensiveAdvanceMarker.Auto())
            {
                if (IsOffensiveTargetVisible)
                {
                    attackMoveIntents.Clear();
                    army.SetState(ArmyGroupState.Assaulting);
                    objectiveReason = "Visible objective assault";
                    TickAssault(context, safety, demoAssaultApproved);
                    return;
                }
                Tactical.Refresh(operationMembers, context.Knowledge, army.State,
                    context.Time);
                if (!(demoAssaultApproved && offensiveTarget.BuildingType is (BuildingType.Castle or BuildingType.Barracks))
                    && Tactical.TryEngageVisibleEnemies(context, operationMembers,
                        Tactical.ArmyAnchor, Tactical.LocalEngagementRadius, default,
                        safety?.ArmyTraversal))
                {
                    objectiveReason = "Local tactical engagement";
                    return;
                }
                if ((!demoAssaultApproved || coordinatedIslandAssault)
                    && IsSeverelyFragmented(out Vector3 centroid))
                {
                    cohesionRegroup = true;
                    cohesionRegroupPosition = centroid;
                    ClearCommandIntents();
                    army.SetState(ArmyGroupState.PreparingAttack);
                    preparationStartTime = context.Time;
                    objectiveReason = "Cohesion regroup";
                    TickPreparingAttack(context, safety);
                    return;
                }
                IssueAdvance(context, safety, false, demoAssaultApproved);
            }
        }

        private void IssueAdvance(CommanderDecisionContext context,
            StrategicSafetyMap safety, bool force, bool demoAssault = false)
        {
            if (!demoAssault) EnsureOffensiveDestinations(context.Knowledge);
            int count = operationMembers.Count;
            if (count == 0) return;
            if (advanceSweepMemberCount != count)
            {
                advanceSweepMemberCount = count;
                advanceSweepVisited = 0;
                advanceSweepAttempted = advanceSweepAccepted = advanceSweepFailed = 0;
            }
            int start = offensiveAdvanceCommandCursor % count;
            int issued = 0;
            for (int offset = 0; offset < count; offset++)
            {
                int i = (start + offset) % count;
                UnitController member = operationMembers[i];
                if (IsMonk(member))
                {
                    if (SupportOffense(context, member, safety)) issued++;
                }
                else if (context.Time >= RetryTime(member)
                    && (force || (demoAssault
                        ? !attackMoveIntents.ContainsKey(member) || !HasMovingAttackOrder(member)
                        : !HasActiveAttackOrder(member)))
                    && (demoAssault || attackMoveIntents.ContainsKey(member)))
                {
                    advanceSweepAttempted++;
                    issued++;
                    Vector3 destination = default;
                    CommanderCommandResult result;
                    if (demoAssault)
                        result = TryIssueDemoApproach(context, member, safety, out destination);
                    else
                    {
                        destination = attackMoveIntents[member];
                        result = context.Commands.TryAttackMove(member, destination, safety?.ArmyTraversal);
                    }
                    RecordCommandResult(result, context.Time,
                        member.GetComponent<UnitMovement>());
                    if (result == CommanderCommandResult.Accepted)
                    {
                        attackMoveIntents[member] = destination;
                        moveIntents.Remove(member);
                        advanceSweepAccepted++;
                    }
                    else
                    {
                        retryAfter[member] = context.Time + commandRetrySeconds;
                        if (result == CommanderCommandResult.RejectedUnreachable
                            || result == CommanderCommandResult.RejectedInvalidTarget)
                            advanceSweepFailed++;
                    }
                }
                // Evaluate route failure only after every member had a chance.
                // A one-command budget must not fail an objective after trying
                // just the first few members of a larger army.
                if (++advanceSweepVisited == count)
                {
                    if (advanceSweepAttempted > 0 && advanceSweepAccepted == 0
                        && advanceSweepFailed > 0)
                    {
                        objectiveFailureCount++;
                        if (objectiveFailureCount >= maximumObjectiveFailures)
                        {
                            if (TryResumeReachableCastle(context)) return;
                            if (demoAssault && TryClearBarracksApproach(context)) return;
                            FailOffense(context, "Objective route repeatedly failed");
                            return;
                        }
                    }
                    advanceSweepVisited = 0;
                    advanceSweepAttempted = advanceSweepAccepted = advanceSweepFailed = 0;
                }
                offensiveAdvanceCommandCursor = (i + 1) % count;
                if (issued >= (coordinatedIslandAssault
                    ? IslandCommandsPerTick : Mathf.Max(1, offensiveAdvanceCommandsPerTick))) return;
            }
            // An accepted order is not movement or combat progress. The
            // no-progress watchdog retains failures until actual progress.
        }

        private static bool HasActiveAttackOrder(UnitController member)
        {
            CombatController combat = member.GetComponent<CombatController>();
            return combat != null && (combat.HasAttackMoveOrder || combat.Target != null);
        }

        private static bool HasMovingAttackOrder(UnitController member)
        {
            CombatController combat = member.GetComponent<CombatController>();
            UnitMovement movement = member.GetComponent<UnitMovement>();
            return combat != null && (combat.Target != null
                || combat.HasAttackMoveOrder && movement != null && movement.IsMoving
                    && Time.time - movement.LastMovementProgressTime < UnitMovement.StaleMoveSeconds);
        }

        private CommanderCommandResult TryIssueDemoApproach(CommanderDecisionContext context,
            UnitController member, StrategicSafetyMap safety, out Vector3 destination)
        {
            destination = default;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            PathfindingSystem paths = grid != null ? grid.GetComponent<PathfindingSystem>() : null;
            if (paths == null) return CommanderCommandResult.RejectedUnreachable;
            Vector2Int start = grid.WorldToGrid(member.transform.position);
            Vector2Int center = grid.WorldToGrid(currentObjectivePosition);
            float currentDistance = DistanceSquared(grid.GridToWorld(start.x, start.y), currentObjectivePosition);
            var candidates = new List<GridCell>();
            for (int y = -7; y <= 7; y++)
            for (int x = -7; x <= 7; x++)
            {
                var point = center + new Vector2Int(x, y);
                GridCell cell = grid.GetCell(point.x, point.y);
                if (!TerrainRules.IsStableStandingCell(cell)
                    || DistanceSquared(cell.WorldPosition, currentObjectivePosition) >= currentDistance)
                    continue;
                candidates.Add(cell);
            }
            candidates.Sort((a, b) =>
            {
                float Score(GridCell cell) => DistanceSquared(cell.WorldPosition, currentObjectivePosition)
                    + 0.15f * DistanceSquared(cell.WorldPosition, member.transform.position);
                return Score(a).CompareTo(Score(b));
            });

            // A failed search can return all reachable cells. Reuse that set
            // instead of running A* on every tile behind the same obstruction.
            HashSet<GridCell> reachable = null;
            IPathTraversalPolicy traversal = safety?.ArmyTraversal;
            using var source = PathfindingSystem.TrackCommanderRequest(PathfindingRequestSource.MilitaryCombat);
            foreach (GridCell cell in candidates)
            {
                if (reachable != null && !reachable.Contains(cell)) continue;
                var point = new Vector2Int(cell.GridX, cell.GridY);
                if (paths.FindPath(start, point, traversal, out HashSet<GridCell> exhausted).Count <= 1)
                {
                    reachable ??= exhausted;
                    continue;
                }
                CommanderCommandResult result = context.Commands.TryAttackMove(member, cell.WorldPosition, traversal);
                if (result != CommanderCommandResult.Accepted) continue;
                destination = cell.WorldPosition;
                if (!demoRouteDispatched)
                {
                    demoRouteDispatched = true;
                    Debug.Log($"DEMO_RB01 {context.Faction} advancing toward {offensiveTarget.BuildingType} via {destination}");
                }
                return result;
            }
            return CommanderCommandResult.RejectedUnreachable;
        }

        private void TickAssault(CommanderDecisionContext context,
            StrategicSafetyMap safety, bool demoAssaultApproved)
        {
            if (!IsOffensiveTargetVisible)
            {
                attackIntents.Clear();
                army.SetState(ArmyGroupState.Advancing);
                objectiveReason = "Advance to remembered objective";
                IssueAdvance(context, safety, true, demoAssaultApproved);
                return;
            }
            Tactical.Refresh(operationMembers, context.Knowledge, army.State,
                context.Time);
            if (!(demoAssaultApproved && offensiveTarget.BuildingType is (BuildingType.Castle or BuildingType.Barracks))
                && Tactical.TryEngageVisibleEnemies(context, operationMembers,
                    Tactical.ArmyAnchor, Tactical.LocalEngagementRadius, default,
                    safety?.ArmyTraversal))
            {
                objectiveReason = "Clear visible defenders";
                return;
            }
            int count = operationMembers.Count;
            if (count == 0) return;
            int start = offensiveAssaultCommandCursor % count;
            int issued = 0;
            for (int offset = 0; offset < count; offset++)
            {
                int i = (start + offset) % count;
                UnitController member = operationMembers[i];
                if (IsMonk(member))
                {
                    if (SupportOffense(context, member, safety)) issued++;
                }
                else
                {
                    CombatController combat = member.GetComponent<CombatController>();
                    if (combat?.BuildingTarget != null
                        && combat.BuildingTarget.GetEntityId().CompareTo(
                            offensiveTarget.RuntimeId) == 0)
                        attackIntents[member] = offensiveTarget.RuntimeId;
                    else if (context.Time >= RetryTime(member)
                        && (!demoAssaultApproved || !attackMoveIntents.ContainsKey(member)
                            || !HasMovingAttackOrder(member)))
                    {
                        issued++;
                        CommanderCommandResult result = context.Commands.TryAttackBuilding(
                            member, offensiveTarget.RuntimeId, searchFullPerimeter: demoAssaultApproved);
                        RecordCommandResult(result, context.Time,
                            member.GetComponent<UnitMovement>());
                        if (result == CommanderCommandResult.Accepted)
                        {
                            attackIntents[member] = offensiveTarget.RuntimeId;
                            attackMoveIntents.Remove(member);
                            if (demoAssaultApproved && !demoBuildingOrderDispatched)
                            {
                                demoBuildingOrderDispatched = true;
                                Debug.Log($"DEMO_RB01 {context.Faction} building attack accepted: {offensiveTarget.BuildingType}");
                            }
                        }
                        else
                        {
                            if (demoAssaultApproved && !HasMovingAttackOrder(member)
                                && TryIssueDemoApproach(context, member, safety, out Vector3 approach)
                                    == CommanderCommandResult.Accepted)
                                attackMoveIntents[member] = approach;
                            retryAfter[member] = context.Time + commandRetrySeconds;
                        }
                    }
                }
                offensiveAssaultCommandCursor = (i + 1) % count;
                if (issued >= (coordinatedIslandAssault
                    ? IslandCommandsPerTick : Mathf.Max(1, offensiveAssaultCommandsPerTick))) return;
            }
        }

        private bool SupportOffense(CommanderDecisionContext context,
            UnitController monk, StrategicSafetyMap safety)
        {
            Vector3 desired;
            if (!moveIntents.TryGetValue(monk, out desired))
            {
                Vector3 centroid = OperationCentroid();
                Vector2 away = (Vector2)(centroid - currentObjectivePosition);
                if (away.sqrMagnitude < 0.001f) away = Vector2.left;
                desired = centroid + (Vector3)(away.normalized * 1.5f);
            }
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return false;
            Vector2Int cell = grid.WorldToGrid(desired);
            List<GridCell> choices = DestinationDistributor.FindNearbyDestinations(
                grid, cell, 1, 3, true);
            if (choices.Count == 0) return false;
            desired = choices[0].WorldPosition;
            UnitMovement movement = monk.GetComponent<UnitMovement>();
            if (Vector2.Distance(monk.transform.position, desired)
                    <= destinationArrivalRadius
                || movement != null && movement.IsMoving
                    && moveIntents.TryGetValue(monk, out Vector3 existing)
                    && Vector2.Distance(existing, desired) <= 0.5f
                || context.Time < RetryTime(monk)) return false;
            CommanderCommandResult result = context.Commands.TryMilitaryMove(monk, desired,
                safety?.ArmyTraversal);
            RecordCommandResult(result, context.Time, movement);
            if (result == CommanderCommandResult.Accepted) moveIntents[monk] = desired;
            else retryAfter[monk] = context.Time + commandRetrySeconds;
            return true;
        }

        private void CaptureCampaignBaseline()
        {
            CalculateOwnStrength(operationMembers, out float strength,
                out float healthRatio, out int population, out Vector3 centroid);
            campaignBaselineCaptured = population > 0 && operationMembers.Count > 0;
            campaignStartPopulation = population;
            campaignStartMemberCount = operationMembers.Count;
            campaignStartEffectiveStrength = strength;
            campaignStartAverageHealth = healthRatio;
            campaignSurvivorCount = operationMembers.Count;
            remainingForceRatio = campaignBaselineCaptured ? 1f : 0f;
            ownEffectiveStrength = strength;
            averageArmyHealth = healthRatio;
            armyCentroid = centroid;
            pendingRetreatReason = RetreatReason.None;
            retreatDangerEvaluations = 0;
        }

        private bool EvaluateRetreat(CommanderDecisionContext context)
        {
            using var marker = RetreatEvaluationMarker.Auto();
            AssessCampaignCombat(context.Knowledge);
            if (campaignBaselineCaptured
                && remainingForceRatio < RetreatRemainingForceRatio)
            {
                pendingRetreatReason = RetreatReason.HeavyCasualties;
                retreatDangerEvaluations = RetreatStableEvaluations;
                return true;
            }

            RetreatReason candidate = RetreatReason.None;
            if (averageArmyHealth < RetreatAverageHealthRatio)
                candidate = RetreatReason.LowAverageHealth;
            else if (visibleEnemyStrength > 0f
                && visibleEnemyStrength > ownEffectiveStrength * RetreatEnemyStrengthRatio)
                candidate = RetreatReason.EnemyStrengthOverwhelming;

            if (candidate == RetreatReason.None)
            {
                pendingRetreatReason = RetreatReason.None;
                retreatDangerEvaluations = 0;
                return false;
            }
            if (pendingRetreatReason == candidate) retreatDangerEvaluations++;
            else
            {
                pendingRetreatReason = candidate;
                retreatDangerEvaluations = 1;
            }
            return retreatDangerEvaluations >= RetreatStableEvaluations;
        }

        private void AssessCampaignCombat(FactionKnowledge knowledge)
        {
            using var marker = CombatStrengthAssessmentMarker.Auto();
            CalculateOwnStrength(operationMembers, out ownEffectiveStrength,
                out averageArmyHealth, out int population, out armyCentroid);
            campaignSurvivorCount = operationMembers.Count;
            remainingForceRatio = campaignBaselineCaptured && campaignStartPopulation > 0
                ? Mathf.Clamp01((float)population / campaignStartPopulation) : 1f;
            visibleEnemyStrength = 0f;
            visibleRelevantEnemyCount = 0;
            float radiusSquared = CombatAssessmentRadius * CombatAssessmentRadius;
            IReadOnlyList<VisibleUnitObservation> units = knowledge.VisibleEnemyUnits;
            for (int i = 0; i < units.Count; i++)
            {
                VisibleUnitObservation item = units[i];
                if (DistanceSquared(item.Position, armyCentroid) > radiusSquared) continue;
                float value = CombatStrengthEvaluator.VisibleEnemyUnit(item, CombatStrength);
                if (value <= 0f) continue;
                visibleEnemyStrength += value;
                visibleRelevantEnemyCount++;
            }
            IReadOnlyList<BuildingKnowledgeSnapshot> buildings =
                knowledge.VisibleEnemyBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingKnowledgeSnapshot item = buildings[i];
                if (DistanceSquared(item.LastSeenPosition, armyCentroid) > radiusSquared)
                    continue;
                float value = CombatStrengthEvaluator.VisibleDefensiveBuilding(
                    item, CombatStrength);
                if (value <= 0f) continue;
                visibleEnemyStrength += value;
                visibleRelevantEnemyCount++;
            }
            enemyStrengthRatio = ownEffectiveStrength > 0.001f
                ? visibleEnemyStrength / ownEffectiveStrength
                : visibleEnemyStrength > 0f ? float.PositiveInfinity : 0f;
        }

        private void CalculateOwnStrength(IReadOnlyList<UnitController> members,
            out float strength, out float healthRatio, out int population,
            out Vector3 centroid)
        {
            strength = CombatStrengthEvaluator.OwnArmy(members, CombatStrength,
                out directCombatStrength, out supportStrength);
            healthRatio = 0f;
            population = 0;
            centroid = Vector3.zero;
            int alive = 0;
            for (int i = 0; i < members.Count; i++)
            {
                UnitController member = members[i];
                HealthComponent health = member != null
                    ? member.GetComponent<HealthComponent>() : null;
                if (health?.CombatData == null || !health.IsAlive) continue;
                healthRatio += CombatStrengthEvaluator.HealthRatio(
                    health.CurrentHP, health.MaxHP);
                population += health.CombatData.PopulationCost;
                centroid += member.transform.position;
                alive++;
            }
            healthRatio = alive > 0 ? healthRatio / alive : 0f;
            centroid = alive > 0 ? centroid / alive : stagingPosition;
        }

        private void BeginRetreat(CommanderDecisionContext context,
            StrategicSafetyMap safety, RetreatReason reason)
        {
            if (operationMembers.Count == 0)
            {
                EnterRecovering(context, "Campaign force unavailable");
                return;
            }
            if (hasOffensiveTarget)
            {
                if (offensiveTarget.BuildingType == BuildingType.GoldMine)
                    activeGoldMineModel?.RecordFailure(offensiveTarget.RuntimeId,
                        context.Time);
                blockedTargetId = offensiveTarget.RuntimeId;
                blockedTargetUntil = context.Time + FailedObjectiveCooldown;
            }
            mineOccupationWaitStart = -1f;
            hasOffensiveTarget = false;
            offensiveTarget = default;
            currentObjectivePosition = default;
            IsOffensiveTargetVisible = false;
            cohesionRegroup = false;
            objectiveFailureCount = 0;
            objectiveReason = "Retreat: " + reason;
            currentRetreatReason = reason;
            pendingRetreatReason = RetreatReason.None;
            retreatDangerEvaluations = 0;
            retreatStartTime = context.Time;
            retreatArrivedFraction = 0f;
            nextOffensiveEvaluationTime = context.Time + FailedObjectiveCooldown;
            ClearCommandIntents();
            retreatDestinations.Clear();
            for (int i = 0; i < operationMembers.Count; i++)
                operationMembers[i]?.GetComponent<CombatController>()?.BeginRetreat();
            army.SetState(ArmyGroupState.Retreating);
            BuildRetreatDestinations(context.Knowledge);
            IssueRetreat(context, safety, true);
        }

        private void TickRetreat(CommanderDecisionContext context,
            StrategicSafetyMap safety)
        {
            using var marker = RetreatExecutionMarker.Auto();
            RemoveStaleRetreatDestinations();
            if (operationMembers.Count == 0)
            {
                EnterRecovering(context, "No campaign survivors");
                return;
            }
            retreatArrivedFraction = FractionInside(operationMembers, stagingPosition,
                stagingRegionRadius);
            if (retreatArrivedFraction >= RetreatCompletionFraction
                || context.Time - retreatStartTime >= RetreatCompletionTimeout)
            {
                EnterRecovering(context, "Retreat completed");
                return;
            }
            BuildRetreatDestinations(context.Knowledge);
            IssueRetreat(context, safety, false);
        }

        private void BuildRetreatDestinations(FactionKnowledge knowledge)
        {
            if (retreatDestinations.Count == operationMembers.Count) return;
            retreatDestinations.Clear();
            GridManager grid = knowledge.TeamVision?.GridManager;
            if (grid == null) return;
            List<GridCell> destinations = DestinationDistributor.FindNearbyDestinations(
                grid, grid.WorldToGrid(stagingPosition), operationMembers.Count, 3, true);
            for (int i = 0; i < operationMembers.Count; i++)
                retreatDestinations[operationMembers[i]] = i < destinations.Count
                    ? destinations[i].WorldPosition : stagingPosition;
        }

        private void IssueRetreat(CommanderDecisionContext context,
            StrategicSafetyMap safety, bool force)
        {
            for (int i = 0; i < operationMembers.Count; i++)
            {
                UnitController member = operationMembers[i];
                if (!retreatDestinations.TryGetValue(member, out Vector3 destination))
                    continue;
                if (Vector2.Distance(member.transform.position, destination)
                    <= destinationArrivalRadius) continue;
                UnitMovement movement = member.GetComponent<UnitMovement>();
                CombatController combat = member.GetComponent<CombatController>();
                if (!force && movement != null && movement.IsMoving
                    && combat?.Directive == CombatDirective.Retreat) continue;
                if (context.Time < RetryTime(member)) continue;
                CommanderCommandResult result = context.Commands.TryRetreatMove(member,
                    destination, safety?.ArmyTraversal);
                RecordCommandResult(result, context.Time, movement);
                retryAfter[member] = context.Time + commandRetrySeconds;
            }
        }

        private void EnterRecovering(CommanderDecisionContext context, string reason)
        {
            EndRetreatDirectives(army.Members);
            ClearCampaignForRecovery();
            recoveryStartTime = context.Time;
            recoveryReady = false;
            resumeRecoveryAfterDefense = false;
            objectiveReason = reason;
            nextOffensiveEvaluationTime = Mathf.Max(nextOffensiveEvaluationTime,
                context.Time + MinimumRecoveryDuration);
            army.SetState(ArmyGroupState.Recovering);
            InvalidateMoveAssignments();
        }

        private void TickRecovery(CommanderDecisionContext context)
        {
            using var marker = RecoveryEvaluationMarker.Auto();
            CalculateOwnStrength(army.Members, out ownEffectiveStrength,
                out averageArmyHealth, out int population, out armyCentroid);
            visibleEnemyStrength = 0f;
            enemyStrengthRatio = 0f;
            visibleRelevantEnemyCount = 0;
            float assembled = FractionInside(army.Members, stagingPosition,
                stagingRegionRadius);
            bool durationReady = context.Time - recoveryStartTime
                >= MinimumRecoveryDuration;
            bool durationFallback = context.Time - recoveryStartTime
                >= MaximumRecoveryDuration;
            bool forceReady = averageArmyHealth >= RecoveryAverageHealthRatio
                || ownEffectiveStrength >= RecoveryMinimumEffectiveStrength
                || durationFallback;
            recoveryReady = durationReady
                && population >= MinOffensiveMilitaryPopulation
                && assembled >= RecoveryAssemblyFraction
                && forceReady;
            if (recoveryReady)
            {
                army.SetState(ArmyGroupState.Ready);
                ClearCommandIntents();
                nextOffensiveEvaluationTime = Mathf.Max(nextOffensiveEvaluationTime,
                    context.Time + 0.5f);
                return;
            }
            if (durationFallback)
            {
                recoveryReady = population >= MinOffensiveMilitaryPopulation
                    && assembled >= TimeoutAssemblyFraction;
                army.SetState(recoveryReady
                    ? ArmyGroupState.Ready : ArmyGroupState.Forming);
                objectiveReason = "Recovery timeout; rebuild at staging";
                ClearCommandIntents();
                nextOffensiveEvaluationTime = Mathf.Max(nextOffensiveEvaluationTime,
                    context.Time + 0.5f);
                lastMilitaryProgressTime = context.Time;
                return;
            }
            if (assembled < RecoveryAssemblyFraction)
                Regroup(context, true);
        }

        private void ClearCampaignForRecovery()
        {
            demoCastleAfterBarracks = null;
            mineOccupationWaitStart = -1f;
            hasOffensiveTarget = false;
            offensiveTarget = default;
            currentObjectivePosition = default;
            IsOffensiveTargetVisible = false;
            operationMembers.Clear();
            cohesionRegroup = false;
            objectiveFailureCount = 0;
            retreatDestinations.Clear();
            ClearCampaignBaseline();
            ClearCommandIntents();
        }

        private void ClearCampaignBaseline()
        {
            campaignBaselineCaptured = false;
            campaignStartPopulation = 0;
            campaignStartMemberCount = 0;
            campaignStartEffectiveStrength = 0f;
            campaignStartAverageHealth = 0f;
            campaignSurvivorCount = 0;
            remainingForceRatio = 1f;
            pendingRetreatReason = RetreatReason.None;
            retreatDangerEvaluations = 0;
        }

        private static void EndRetreatDirectives(IReadOnlyList<UnitController> members)
        {
            for (int i = 0; i < members.Count; i++)
                if (members[i] != null)
                    members[i].GetComponent<CombatController>()?.EndRetreat();
        }

        private void RemoveStaleRetreatDestinations()
        {
            staleActors.Clear();
            foreach (UnitController actor in retreatDestinations.Keys)
                if (!operationMembers.Contains(actor)) staleActors.Add(actor);
            for (int i = 0; i < staleActors.Count; i++)
                retreatDestinations.Remove(staleActors[i]);
        }

        private bool TryRefreshObjective(FactionKnowledge knowledge,
            out BuildingKnowledgeSnapshot known)
            => knowledge.TryGetKnownEnemyBuilding(offensiveTarget.RuntimeId, out known)
                && known.Faction == offensiveTarget.Faction
                && known.BuildingType == offensiveTarget.BuildingType
                && known.WasCompleted && known.LastSeenHealthRatio > 0f;

        private bool IsObjectiveAreaVisible(FactionKnowledge knowledge)
        {
            GridManager grid = knowledge.TeamVision?.GridManager;
            return grid != null && knowledge.IsCellVisible(
                grid.WorldToGrid(currentObjectivePosition));
        }

        private void CompleteOffense(CommanderDecisionContext context, string reason)
        {
            TargetCompletionSerial++;
            LastTargetCompletionTime = context.Time;
            CancelOffense(reason, context.Time, true);
            army.SetState(ArmyGroupState.Regrouping);
            InvalidateMoveAssignments();
            Regroup(context, true);
        }

        private void FailOffense(CommanderDecisionContext context, string reason)
        {
            if (hasOffensiveTarget)
            {
                if (offensiveTarget.BuildingType == BuildingType.GoldMine)
                    activeGoldMineModel?.RecordFailure(offensiveTarget.RuntimeId,
                        context.Time);
                blockedTargetId = offensiveTarget.RuntimeId;
                blockedTargetUntil = context.Time + FailedObjectiveCooldown;
            }
            CancelOffense(reason, context.Time, true);
            army.SetState(ArmyGroupState.Regrouping);
            InvalidateMoveAssignments();
            Regroup(context, true);
        }

        private void CancelOffense(string reason, float time, bool cooldown)
        {
            demoCastleAfterBarracks = null;
            mineOccupationWaitStart = -1f;
            hasOffensiveTarget = false;
            offensiveTarget = default;
            currentObjectivePosition = default;
            IsOffensiveTargetVisible = false;
            operationMembers.Clear();
            retreatDestinations.Clear();
            cohesionRegroup = false;
            objectiveFailureCount = 0;
            objectiveReason = reason;
            if (cooldown)
                nextOffensiveEvaluationTime = time + offensiveReevaluationSeconds;
            ClearCampaignBaseline();
            EndRetreatDirectives(army.Members);
            ClearCommandIntents();
        }

        private bool TryRetainCurrentThreat(FactionKnowledge knowledge,
            bool requireEmergency, out VisibleUnitObservation threat)
        {
            threat = default;
            return hasCurrentThreat
                && knowledge.TryGetVisibleEnemy(currentThreatId, out threat)
                && IsSupportedThreat(threat)
                && (!requireEmergency || IsDefenseEmergency(threat))
                && DistanceSquared(threat.Position, baseAnchor)
                    <= DefenseLeashRadius * DefenseLeashRadius;
        }

        private bool TrySelectThreat(FactionKnowledge knowledge, bool requireEmergency,
            out VisibleUnitObservation best)
        {
            using (ThreatEvaluationMarker.Auto())
            {
                best = default;
                bool found = false;
                float radiusSquared = BaseDefenseRadius * BaseDefenseRadius;
                float bestDistance = float.PositiveInfinity;
                IReadOnlyList<VisibleUnitObservation> visible = knowledge.VisibleEnemyUnits;
                for (int i = 0; i < visible.Count; i++)
                {
                    VisibleUnitObservation candidate = visible[i];
                    if (!IsSupportedThreat(candidate)
                        || requireEmergency && !IsDefenseEmergency(candidate)) continue;
                    float distance = DistanceSquared(candidate.Position, baseAnchor);
                    if (distance > radiusSquared) continue;
                    VisibleThreatCount++;
                    if (!found || IsHigherPriority(candidate, distance, best, bestDistance))
                    {
                        found = true;
                        best = candidate;
                        bestDistance = distance;
                    }
                }
                return found;
            }
        }

        private void Defend(CommanderDecisionContext context,
            VisibleUnitObservation threat)
        {
            IReadOnlyList<UnitController> members = army.Members;
            if (coordinatedIslandAssault)
            {
                Vector3 rally = hasStagingPosition ? stagingPosition : baseAnchor;
                islandDefenseGroup.Clear();
                // Use the largest local group so a lone soldier or a unit
                // still walking from production does not receive a chase order.
                foreach (UnitController anchor in members)
                {
                    if (IsMonk(anchor)
                        || DistanceSquared(anchor.transform.position, baseAnchor)
                            > DefenseLeashRadius * DefenseLeashRadius) continue;
                    Vector3 center = anchor.transform.position;
                    float radiusSquared = IslandDefenseGroupRadius * IslandDefenseGroupRadius;
                    int fighters = 0;
                    foreach (UnitController member in members)
                        if (!IsMonk(member) && DistanceSquared(member.transform.position, center)
                                <= radiusSquared) fighters++;
                    if (fighters <= CountAttackers(islandDefenseGroup)) continue;
                    islandDefenseGroup.Clear();
                    foreach (UnitController member in members)
                        if (DistanceSquared(member.transform.position, center) <= radiusSquared)
                            islandDefenseGroup.Add(member);
                }
                int minimum = threat.ThreatActivity == ObservedThreatActivity.AttackingCastle ? 2 : 3;
                if (CountAttackers(islandDefenseGroup) < minimum)
                {
                    objectiveReason = "Hold formation while defenders gather";
                    RegroupMembers(context, members, rally, true, null, IslandCommandsPerTick);
                    return;
                }
                members = islandDefenseGroup;
                objectiveReason = "Defend with a gathered group";
            }
            Tactical.Refresh(members, context.Knowledge, army.State, context.Time);
            if (Tactical.TryEngageVisibleEnemies(context, members, baseAnchor,
                    DefenseLeashRadius, threat.RuntimeId, null)) return;
            for (int i = 0; i < members.Count; i++)
            {
                UnitController member = members[i];
                if (IsMonk(member))
                {
                    SupportDefense(context, member, threat.Position);
                    continue;
                }
                CombatController combat = member.GetComponent<CombatController>();
                if (combat?.Target != null
                    && combat.Target.GetEntityId().CompareTo(threat.RuntimeId) == 0)
                {
                    attackIntents[member] = threat.RuntimeId;
                    continue;
                }
                if (context.Time < RetryTime(member)) continue;
                CommanderCommandResult result = context.Commands.TryAttack(member,
                    threat.RuntimeId);
                RecordCommandResult(result, context.Time,
                    member.GetComponent<UnitMovement>());
                if (result == CommanderCommandResult.Accepted)
                {
                    attackIntents[member] = threat.RuntimeId;
                    moveIntents.Remove(member);
                }
                else retryAfter[member] = context.Time + commandRetrySeconds;
            }
        }

        private int CountVisibleDefenseThreats(FactionKnowledge knowledge,
            bool requireEmergency)
        {
            int count = 0;
            float radiusSquared = BaseDefenseRadius * BaseDefenseRadius;
            IReadOnlyList<VisibleUnitObservation> visible = knowledge.VisibleEnemyUnits;
            for (int i = 0; i < visible.Count; i++)
                if (IsSupportedThreat(visible[i])
                    && (!requireEmergency || IsDefenseEmergency(visible[i]))
                    && DistanceSquared(visible[i].Position, baseAnchor) <= radiusSquared) count++;
            return count;
        }

        private void SupportDefense(CommanderDecisionContext context, UnitController monk,
            Vector3 threatPosition)
        {
            UnitMovement movement = monk.GetComponent<UnitMovement>();
            if (movement == null || movement.IsMoving || context.Time < RetryTime(monk)) return;
            Vector2 retreatDirection = (Vector2)(baseAnchor - threatPosition);
            if (retreatDirection.sqrMagnitude < 0.001f) retreatDirection = Vector2.left;
            Vector3 desired = threatPosition + (Vector3)(retreatDirection.normalized * 2f);
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return;
            Vector2Int cell = grid.WorldToGrid(desired);
            GridCell destination = grid.GetCell(cell.x, cell.y);
            if (destination == null || !destination.Walkable || destination.Occupied) return;
            desired = destination.WorldPosition;
            if (Vector2.Distance(monk.transform.position, desired) <= destinationArrivalRadius)
                return;
            if (moveIntents.TryGetValue(monk, out Vector3 existing)
                && Vector2.Distance(existing, desired) <= 0.5f) return;
            CommanderCommandResult result = context.Commands.TryMilitaryDirectMove(monk,
                desired);
            RecordCommandResult(result, context.Time, movement);
            if (result == CommanderCommandResult.Accepted) moveIntents[monk] = desired;
            else retryAfter[monk] = context.Time + commandRetrySeconds;
        }

        private void Regroup(CommanderDecisionContext context, bool interruptCombat)
        {
            using (ArmyRegroupMarker.Auto())
            {
                EnsureMoveAssignments(context.Knowledge);
                IssueMoveAssignments(context, army.Members, interruptCombat, null);
            }
        }

        private void RegroupMembers(CommanderDecisionContext context,
            IReadOnlyList<UnitController> members, Vector3 center, bool interruptCombat,
            IPathTraversalPolicy traversalPolicy, int commandBudget = int.MaxValue)
        {
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return;
            if (moveIntents.Count != members.Count)
            {
                if (!Tactical.BuildFormationAssignments(context.Knowledge, members,
                        center, moveIntents, true))
                {
                    moveIntents.Clear();
                    List<GridCell> destinations = DestinationDistributor
                        .FindNearbyDestinations(grid, grid.WorldToGrid(center),
                            members.Count, 4, true);
                    int count = Mathf.Min(destinations.Count, members.Count);
                    for (int i = 0; i < count; i++)
                        moveIntents[members[i]] = destinations[i].WorldPosition;
                }
            }
            IssueMoveAssignments(context, members, interruptCombat, traversalPolicy,
                commandBudget);
        }

        private void IssueMoveAssignments(CommanderDecisionContext context,
            IReadOnlyList<UnitController> members, bool interruptCombat,
            IPathTraversalPolicy traversalPolicy, int commandBudget = int.MaxValue)
        {
            int count = members.Count;
            if (count == 0) return;
            bool bounded = commandBudget < count;
            int start = bounded ? offensiveStagingCommandCursor % count : 0;
            int issued = 0;
            for (int offset = 0; offset < count; offset++)
            {
                int i = (start + offset) % count;
                UnitController member = members[i];
                if (!moveIntents.TryGetValue(member, out Vector3 destination)) continue;
                if (Vector2.Distance(member.transform.position, destination)
                    <= destinationArrivalRadius) continue;
                UnitMovement movement = member.GetComponent<UnitMovement>();
                bool activeRoute = movement != null && movement.IsMoving;
                bool staleRoute = activeRoute
                    && context.Time - movement.LastMovementProgressTime
                        >= MilitaryNoProgressSeconds;
                if (!staleRoute && !interruptCombat && activeRoute) continue;
                if (!staleRoute && interruptCombat && activeRoute
                    && member.CurrentState == UnitState.Moving) continue;
                if (context.Time < RetryTime(member)) continue;
                CommanderCommandResult result = context.Commands.TryMilitaryMove(member,
                    destination, traversalPolicy, true);
                RecordCommandResult(result, context.Time, movement);
                if (result != CommanderCommandResult.Accepted)
                    retryAfter[member] = context.Time + commandRetrySeconds;
                if (bounded && ++issued >= commandBudget)
                {
                    offensiveStagingCommandCursor = (i + 1) % count;
                    break;
                }
            }
            if (bounded && issued < commandBudget)
                offensiveStagingCommandCursor = (start + 1) % count;
        }

        private void EnsureMoveAssignments(FactionKnowledge knowledge)
        {
            if (assignmentMembershipVersion == army.MembershipVersion
                && moveIntents.Count == army.Members.Count) return;
            moveIntents.Clear();
            Tactical.BuildFormationAssignments(knowledge, army.Members,
                stagingPosition, moveIntents, true);
            assignmentMembershipVersion = army.MembershipVersion;
        }

        private void EnsureOffensiveDestinations(FactionKnowledge knowledge)
        {
            attackMoveIntents.Clear();
            if (!Tactical.BuildFormationAssignments(knowledge, operationMembers,
                    currentObjectivePosition, formationIntents)) return;
            for (int i = 0; i < operationMembers.Count; i++)
            {
                UnitController member = operationMembers[i];
                if (!formationIntents.TryGetValue(member, out Vector3 destination)) continue;
                if (IsMonk(member)) moveIntents[member] = destination;
                else attackMoveIntents[member] = destination;
            }
        }

        private void RefreshAnchorAndStaging(FactionKnowledge knowledge,
            ProductionCommanderPolicy production)
        {
            hasBaseAnchor = TryGetCastleCenter(knowledge, out baseAnchor);
            GridManager grid = knowledge.TeamVision?.GridManager;
            hasStagingPosition = grid != null && production != null
                && production.HasRallyStagingCell;
            if (hasStagingPosition)
                stagingPosition = grid.GridToWorld(production.RallyStagingCell.x,
                    production.RallyStagingCell.y);
        }

        private static bool TryGetCastleCenter(FactionKnowledge knowledge, out Vector3 center)
        {
            GridManager grid = knowledge.TeamVision?.GridManager;
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost host = buildings[i];
                if (host == null || !host.IsAlive || host.Type != BuildingType.Castle) continue;
                if (grid == null)
                {
                    center = host.HostTransform.position;
                    return true;
                }
                Vector2Int first = host.FootprintOrigin;
                Vector2Int last = first + host.Footprint - Vector2Int.one;
                center = (grid.GridToWorld(first.x, first.y)
                    + grid.GridToWorld(last.x, last.y)) * 0.5f;
                return true;
            }
            center = default;
            return false;
        }

        private bool IsSeverelyFragmented(out Vector3 centroid)
        {
            centroid = Tactical.ArmyAnchor;
            if (operationMembers.Count < 3) return false;
            return FractionInside(operationMembers, centroid, offensiveCohesionRadius) < 0.5f;
        }

        private Vector3 OperationCentroid()
        {
            return operationMembers.Count > 0 ? Tactical.ArmyAnchor : stagingPosition;
        }

        private static float FractionInside(IReadOnlyList<UnitController> members,
            Vector3 center, float radius)
        {
            if (members.Count == 0) return 0f;
            int inside = 0;
            float radiusSquared = radius * radius;
            for (int i = 0; i < members.Count; i++)
                if (DistanceSquared(members[i].transform.position, center)
                    <= radiusSquared) inside++;
            return (float)inside / members.Count;
        }

        private bool IsAssemblyReady(IReadOnlyList<UnitController> members,
            Vector3 center, float requiredFraction)
        {
            if (members.Count == 0) return false;
            float fraction = FractionInside(members, center, stagingRegionRadius);
            if (fraction >= requiredFraction) return true;
            float majority = Mathf.Min(requiredFraction,
                Mathf.Max(0.5f, TimeoutAssemblyFraction));
            bool anchorClose = Vector2.Distance(Tactical.ArmyAnchor, center)
                <= stagingRegionRadius * 1.35f;
            int allowedStragglers = Mathf.Max(1, members.Count / 3);
            return fraction >= majority && anchorClose
                && Tactical.StragglerCount <= allowedStragglers;
        }

        private void ObserveMovementProgress(float now)
        {
            using var marker = LivenessEvaluationMarker.Auto();
            IReadOnlyList<UnitController> members = IsOffensiveState
                && operationMembers.Count > 0 ? operationMembers : army.Members;
            SyncCombatProgressSources(members);
            bool staging = army.State is ArmyGroupState.Forming
                or ArmyGroupState.Regrouping or ArmyGroupState.PreparingAttack
                or ArmyGroupState.Recovering;
            Vector3 assembly = army.State == ArmyGroupState.PreparingAttack
                && cohesionRegroup ? cohesionRegroupPosition : stagingPosition;
            float assembled = staging && hasStagingPosition
                ? FractionInside(members, assembly, stagingRegionRadius) : 0f;
            if (lastMilitaryProgressTime < 0f)
            {
                lastMilitaryProgressTime = now;
                lastObservedAssemblyFraction = assembled;
            }
            if (!IsOffensiveState
                && lastObservedMembershipVersion != army.MembershipVersion)
            {
                lastObservedMembershipVersion = army.MembershipVersion;
                // Newly trained units change the assembly fraction but do not
                // prove that already-stalled members can reach staging.
                lastObservedAssemblyFraction = assembled;
            }
            bool progressed = staging
                ? assembled > lastObservedAssemblyFraction + 0.04f
                : IsOffensiveState
                    && lastCombatActionTime > lastMilitaryProgressTime;
            int membersOutsideAssembly = 0;
            int membersMovingToAssembly = 0;
            float assemblyRadiusSquared = stagingRegionRadius * stagingRegionRadius;
            float assignmentRadiusSquared = assemblyRadiusSquared * 4f;
            for (int i = 0; i < members.Count; i++)
            {
                UnitController member = members[i];
                if (member == null) continue;
                UnitMovement movement = member.GetComponent<UnitMovement>();
                bool movedAfterCommand = movement != null
                    && movement.LastMovementProgressTime > lastMilitaryProgressTime
                    && movement.LastMovementProgressTime
                        > movement.LastAcceptedCommandTime;
                if (staging && hasStagingPosition)
                {
                    if (DistanceSquared(member.transform.position, assembly)
                        > assemblyRadiusSquared)
                    {
                        membersOutsideAssembly++;
                        if (movedAfterCommand
                            && moveIntents.TryGetValue(member, out Vector3 destination)
                            && DistanceSquared(destination, assembly)
                                <= assignmentRadiusSquared)
                            membersMovingToAssembly++;
                    }
                }
                else if (movedAfterCommand && IsOffensiveState
                    && !IsMonk(member)
                    && (attackMoveIntents.ContainsKey(member)
                        || army.State == ArmyGroupState.Assaulting
                            && IsOffensiveTargetVisible
                            && attackIntents.TryGetValue(member,
                                out EntityId assaultTarget)
                            && assaultTarget.CompareTo(offensiveTarget.RuntimeId) == 0))
                    // Chasing a visible building is progress too, even after
                    // Advance replaces the attack-move intent with an attack.
                    progressed = true;
                if (movement != null)
                    lastMilitaryPathRequestTime = Mathf.Max(lastMilitaryPathRequestTime,
                        movement.LastPathRequestTime);
            }
            if (staging && membersOutsideAssembly > 0
                && membersMovingToAssembly >= Mathf.CeilToInt(
                    membersOutsideAssembly * 0.6f))
                progressed = true;
            lastAcceptedMilitaryCommandTime = Mathf.Max(lastAcceptedMilitaryCommandTime,
                Tactical.LastAcceptedCommandTime);
            lastRejectedMilitaryCommandTime = Mathf.Max(lastRejectedMilitaryCommandTime,
                Tactical.LastRejectedCommandTime);
            if (progressed)
            {
                lastMilitaryProgressTime = now;
                stagingRecoveryCount = 0;
                if (IsOffensiveState && !staging) objectiveFailureCount = 0;
            }
            lastObservedAssemblyFraction = assembled;
        }

        private void SyncCombatProgressSources(IReadOnlyList<UnitController> members)
        {
            currentCombatProgressSources.Clear();
            for (int i = 0; i < members.Count; i++)
            {
                UnitController member = members[i];
                if (member == null || !member.IsAlive) continue;
                CombatController combat = member.GetComponent<CombatController>();
                if (combat == null) continue;
                currentCombatProgressSources.Add(combat);
                if (combatProgressSources.Add(combat))
                    combat.AttackPerformed += OnCombatAction;
            }
            staleCombatProgressSources.Clear();
            foreach (CombatController combat in combatProgressSources)
                if (!currentCombatProgressSources.Contains(combat))
                    staleCombatProgressSources.Add(combat);
            for (int i = 0; i < staleCombatProgressSources.Count; i++)
            {
                CombatController combat = staleCombatProgressSources[i];
                if (combat != null) combat.AttackPerformed -= OnCombatAction;
                combatProgressSources.Remove(combat);
            }
        }

        private void OnCombatAction(Vector2 direction)
            => lastCombatActionTime = Time.time;

        private bool HandleStagingNoProgress(CommanderDecisionContext context,
            ProductionCommanderPolicy production, StrategicIntentEvaluator strategy,
            string reason)
        {
            if (context.Time < nextStagingRecoveryTime
                || context.Time - lastMilitaryProgressTime < MilitaryNoProgressSeconds)
                return false;
            nextStagingRecoveryTime = context.Time + StagingRecoveryCooldown;
            stagingRecoveryCount++;
            if (army.State == ArmyGroupState.PreparingAttack
                && stagingRecoveryCount > 2)
            {
                FailOffense(context, "Attack assembly stalled after alternate staging");
                return true;
            }
            bool valid = production != null && production.HasRallyStagingCell
                && ProductionCommanderPolicy.IsValidStagingCell(context.Knowledge,
                    production.RallyStagingCell)
                && CanReachStaging(context.Knowledge,
                    production.RallyStagingCell);
            // A legal grid cell can still leave every route stuck. One retry of
            // stale assignments is enough before choosing a different cell.
            bool recompute = !valid || stagingRecoveryCount >= 2;
            InvalidateMoveAssignments();
            if (recompute)
            {
                production?.InvalidateRallyStagingCell(true, context.Time);
                hasStagingPosition = false;
                objectiveReason = reason + "; alternate staging requested";
            }
            else objectiveReason = reason + "; stale commands invalidated";
            strategy?.RequestImmediateReevaluation();
            lastMilitaryProgressTime = context.Time;
            return recompute;
        }

        private bool CanReachStaging(FactionKnowledge knowledge,
            Vector2Int destination)
        {
            GridManager grid = knowledge?.TeamVision?.GridManager;
            PathfindingSystem paths = grid != null
                ? grid.GetComponent<PathfindingSystem>() : null;
            if (paths == null) return false;
            IReadOnlyList<UnitController> members = IsOffensiveState
                && operationMembers.Count > 0 ? operationMembers : army.Members;
            UnitController representative = null;
            for (int i = 0; i < members.Count; i++)
            {
                UnitController member = members[i];
                if (member != null && member.IsAlive
                    && Vector2.Distance(member.transform.position, stagingPosition)
                        > stagingRegionRadius)
                {
                    representative = member;
                    break;
                }
            }
            if (representative == null) return true;
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.MilitaryRegroup);
            return paths.FindPath(grid.WorldToGrid(representative.transform.position),
                destination).Count > 0;
        }

        private bool HandleAttackNoProgress(CommanderDecisionContext context,
            StrategicSafetyMap safety, bool demoAssault = false)
        {
            if (context.Time - lastMilitaryProgressTime < AttackIdleTimeout)
                return false;
            objectiveFailureCount++;
            ClearCommandIntents();
            lastMilitaryProgressTime = context.Time;
            objectiveReason = "Attack idle watchdog recovery";
            if (objectiveFailureCount >= maximumObjectiveFailures)
            {
                if (TryResumeReachableCastle(context)) return true;
                if (demoAssault && TryClearBarracksApproach(context)) return true;
                FailOffense(context, "Attack made no movement or combat progress");
                return true;
            }
            if (demoAssault && army.State == ArmyGroupState.Assaulting)
                TickAssault(context, safety, demoAssault);
            else if (army.State == ArmyGroupState.PreparingAttack)
            {
                preparationStartTime = context.Time;
                RegroupMembers(context, operationMembers,
                    cohesionRegroup ? cohesionRegroupPosition : stagingPosition,
                    true, safety?.ArmyTraversal, OffensiveStagingCommandsPerTick);
            }
            else IssueAdvance(context, safety, true, demoAssault);
            return true;
        }

        private bool TryClearBarracksApproach(CommanderDecisionContext context, bool onlyAtFront = false)
        {
            if (!hasOffensiveTarget || offensiveTarget.BuildingType != BuildingType.Castle)
                return false;
            Vector2 start = OperationCentroid();
            Vector2 end = currentObjectivePosition;
            Vector2 approach = end - start;
            if (approach.sqrMagnitude < 1f) return false;

            BuildingKnowledgeSnapshot blocker = default;
            float nearest = float.PositiveInfinity;
            foreach (BuildingKnowledgeSnapshot building in context.Knowledge.VisibleEnemyBuildings)
            {
                if (building.BuildingType != BuildingType.Barracks
                    || building.Faction != offensiveTarget.Faction
                    || !building.WasCompleted || building.LastSeenHealthRatio <= 0f
                    || context.Time < blockedTargetUntil && building.RuntimeId.CompareTo(blockedTargetId) == 0)
                    continue;
                Vector2 offset = (Vector2)building.LastSeenPosition - start;
                float along = Vector2.Dot(offset, approach) / approach.sqrMagnitude;
                float corridorRadius = Mathf.Max(building.Footprint.x, building.Footprint.y) * 0.5f + 1f;
                float priority = onlyAtFront ? offset.sqrMagnitude : along;
                if (priority >= nearest || (onlyAtFront
                    ? !IsBarracksAtAssaultFront(building, end)
                    : along <= 0f || along >= 1f
                        || (offset - approach * along).sqrMagnitude > corridorRadius * corridorRadius))
                    continue;
                nearest = priority;
                blocker = building;
            }
            if (float.IsPositiveInfinity(nearest)) return false;
            if (coordinatedIslandAssault || islandTrainingOpponent)
            {
                // Nearness alone does not make a Barracks a route blocker.
                if (!IsOffensiveTargetVisible || CanReachCastle(context, offensiveTarget.RuntimeId))
                    return false;
            }

            demoCastleAfterBarracks = offensiveTarget;
            SwitchDemoObjective(context, new OffensiveTargetSnapshot(blocker.RuntimeId,
                blocker.BuildingType, blocker.Faction, blocker.LastSeenPosition,
                StrategicTargetCategory.MilitaryBuilding, true));
            Debug.Log(coordinatedIslandAssault || islandTrainingOpponent
                ? $"QL_CLEARING_BARRACKS faction={context.Faction} time={context.Time:F2}"
                : $"DEMO_RB01 {context.Faction} clearing Barracks on the Castle approach");
            return true;
        }

        private bool CanReachCastle(CommanderDecisionContext context, EntityId castleId)
        {
            if (!context.Knowledge.TryGetVisibleEnemyBuilding(castleId, out BuildingKnowledgeSnapshot castle))
                return false;
            foreach (UnitController member in operationMembers)
                if (!IsMonk(member) && context.Commands.CanReachVisibleBuildingToAttack(member, castle))
                    return true;
            return false;
        }

        private void ResumeCastleAssault(CommanderDecisionContext context)
        {
            SwitchDemoObjective(context, demoCastleAfterBarracks.Value);
            demoCastleAfterBarracks = null;
            Debug.Log(coordinatedIslandAssault || islandTrainingOpponent
                ? $"QL_CASTLE_RESUMED faction={context.Faction} time={context.Time:F2}"
                : $"DEMO_RB01 {context.Faction} barracks cleared, resuming Castle assault");
        }

        private bool TryResumeReachableCastle(CommanderDecisionContext context)
        {
            if ((!coordinatedIslandAssault && !islandTrainingOpponent)
                || !demoCastleAfterBarracks.HasValue
                || !CanReachCastle(context, demoCastleAfterBarracks.Value.RuntimeId))
                return false;
            ResumeCastleAssault(context);
            return true;
        }

        private bool IsBarracksAtAssaultFront(BuildingKnowledgeSnapshot building, Vector2 castle)
        {
            Vector2 barracks = building.LastSeenPosition;
            float reach = Mathf.Max(building.Footprint.x, building.Footprint.y) * 0.5f + 2f;
            foreach (UnitController member in operationMembers)
            {
                if (IsMonk(member)) continue;
                Vector2 position = member.transform.position;
                if ((position - barracks).sqrMagnitude <= reach * reach
                    && Vector2.Dot(barracks - position, castle - barracks) >= 0f)
                    return true;
            }
            return false;
        }

        private void SwitchDemoObjective(CommanderDecisionContext context, OffensiveTargetSnapshot target)
        {
            offensiveTarget = target;
            currentObjectivePosition = target.LastKnownPosition;
            IsOffensiveTargetVisible = target.WasVisibleWhenSelected;
            ClearCommandIntents();
            army.SetState(ArmyGroupState.Advancing);
            demoRouteDispatched = false;
            demoBuildingOrderDispatched = false;
            objectiveFailureCount = 0;
            advanceSweepVisited = advanceSweepAttempted = advanceSweepAccepted = advanceSweepFailed = 0;
            lastMilitaryProgressTime = context.Time;
        }

        private void RecordCommandResult(CommanderCommandResult result, float time,
            UnitMovement movement)
        {
            lastMilitaryCommandResult = result;
            if (movement != null)
            {
                UnitController actor = movement.GetComponent<UnitController>();
                if (actor != null) lastCommandResults[actor] = result;
            }
            if (movement != null)
                lastMilitaryPathRequestTime = Mathf.Max(lastMilitaryPathRequestTime,
                    movement.LastPathRequestTime);
            if (result == CommanderCommandResult.Accepted)
            {
                lastAcceptedMilitaryCommandTime = time;
            }
            else lastRejectedMilitaryCommandTime = time;
        }

        private void RemoveStaleIntents()
        {
            RemoveStaleKeys(attackIntents);
            RemoveStaleKeys(moveIntents);
            RemoveStaleKeys(attackMoveIntents);
            RemoveStaleKeys(retryAfter);
            RemoveStaleKeys(lastCommandResults);
        }

        private void RemoveStaleKeys<T>(Dictionary<UnitController, T> dictionary)
        {
            staleActors.Clear();
            foreach (UnitController actor in dictionary.Keys)
                if (!ContainsMember(actor)) staleActors.Add(actor);
            for (int i = 0; i < staleActors.Count; i++)
                dictionary.Remove(staleActors[i]);
        }

        private void RemoveStaleOperationMembers()
        {
            for (int i = operationMembers.Count - 1; i >= 0; i--)
                if (!ContainsMember(operationMembers[i])) operationMembers.RemoveAt(i);
        }

        private bool ContainsMember(UnitController actor)
        {
            IReadOnlyList<UnitController> members = army.Members;
            for (int i = 0; i < members.Count; i++)
                if (members[i] == actor) return true;
            return false;
        }

        private void ClearThreat()
        {
            hasCurrentThreat = false;
            currentThreatId = default;
            currentThreatType = default;
            currentThreatPosition = default;
            if (!IsOffensiveState) attackIntents.Clear();
        }

        private void ClearCommandIntents()
        {
            attackIntents.Clear();
            moveIntents.Clear();
            attackMoveIntents.Clear();
            formationIntents.Clear();
            retryAfter.Clear();
            assignmentMembershipVersion = -1;
            offensiveAssaultCommandCursor = 0;
            advanceSweepMemberCount = advanceSweepVisited = 0;
            advanceSweepAttempted = advanceSweepAccepted = advanceSweepFailed = 0;
        }

        private void InvalidateMoveAssignments()
        {
            moveIntents.Clear();
            formationIntents.Clear();
            retryAfter.Clear();
            assignmentMembershipVersion = -1;
        }

        private float RetryTime(UnitController actor)
            => retryAfter.TryGetValue(actor, out float time) ? time : 0f;

        private static bool IsSupportedThreat(VisibleUnitObservation threat)
            => FactionRelations.IsPlayable(threat.Faction)
                && threat.UnitType is UnitType.Pawn or UnitType.Warrior or UnitType.Archer
                    or UnitType.Lancer or UnitType.Monk;

        internal static bool IsDefenseEmergency(VisibleUnitObservation threat)
            => threat.ThreatActivity != ObservedThreatActivity.None
                || threat.UnitType is UnitType.Warrior or UnitType.Archer or UnitType.Lancer;

        private static bool IsHigherPriority(VisibleUnitObservation candidate,
            float candidateDistance, VisibleUnitObservation current, float currentDistance)
        {
            int priority = ThreatPriority(candidate);
            int currentPriority = ThreatPriority(current);
            if (priority != currentPriority) return priority < currentPriority;
            if (Mathf.Abs(candidateDistance - currentDistance) > 0.0001f)
                return candidateDistance < currentDistance;
            return candidate.RuntimeId.CompareTo(current.RuntimeId) < 0;
        }

        private static int ThreatPriority(VisibleUnitObservation threat)
            => threat.ThreatActivity switch
            {
                ObservedThreatActivity.AttackingCastle => 0,
                ObservedThreatActivity.AttackingBuilding => 1,
                ObservedThreatActivity.AttackingUnit => 2,
                _ => threat.UnitType == UnitType.Pawn ? 4 : 3
            };

        private static StrategicTargetCategory CategoryOf(BuildingType type)
            => type switch
            {
                BuildingType.Tower => StrategicTargetCategory.DefensiveBuilding,
                BuildingType.Barracks or BuildingType.Archery or BuildingType.Monastery
                    => StrategicTargetCategory.MilitaryBuilding,
                BuildingType.Castle => StrategicTargetCategory.MainCastle,
                _ => StrategicTargetCategory.EconomicBuilding
            };

        private static int PriorityOf(StrategicTargetCategory category)
            => category switch
            {
                StrategicTargetCategory.DefensiveBuilding => 0,
                StrategicTargetCategory.MilitaryBuilding => 1,
                StrategicTargetCategory.EconomicBuilding => 2,
                _ => 3
            };

        private static int PopulationOf(IReadOnlyList<UnitController> members)
        {
            int total = 0;
            for (int i = 0; i < members.Count; i++)
            {
                UnitCombatData data = members[i]
                    ?.GetComponent<HealthComponent>()?.CombatData;
                if (data != null) total += data.PopulationCost;
            }
            return total;
        }

        private static int CountAttackers(IReadOnlyList<UnitController> members)
        {
            int count = 0;
            for (int i = 0; i < members.Count; i++)
                if (!IsMonk(members[i])) count++;
            return count;
        }

        private static bool IsMonk(UnitController unit)
            => unit?.GetComponent<HealthComponent>()?.CombatData?.UnitType == UnitType.Monk;

        private static float DistanceSquared(Vector3 first, Vector3 second)
            => ((Vector2)(first - second)).sqrMagnitude;
    }
}
