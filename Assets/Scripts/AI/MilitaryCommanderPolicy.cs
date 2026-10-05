using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
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
        [SerializeField] private TacticalArmyController tactical = new();

        private readonly ArmyGroup army = new();
        private readonly Dictionary<UnitController, EntityId> attackIntents = new();
        private readonly Dictionary<UnitController, Vector3> moveIntents = new();
        private readonly Dictionary<UnitController, Vector3> attackMoveIntents = new();
        private readonly Dictionary<UnitController, Vector3> formationIntents = new();
        private readonly Dictionary<UnitController, float> retryAfter = new();
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

        public ArmyGroup Army => army;
        public TacticalArmyController Tactical => tactical ??= new TacticalArmyController();
        public float BaseDefenseRadius => Mathf.Max(1f, baseDefenseRadius);
        public float DefenseLeashRadius => Mathf.Max(BaseDefenseRadius, defenseLeashRadius);
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
        {
            army.Synchronize(context.Knowledge, activeScout);
            Tactical.Refresh(army.Members, context.Knowledge, army.State, context.Time);
            CombatStrengthEvaluator.OwnArmy(army.Members, CombatStrength,
                out directCombatStrength, out supportStrength);
            RemoveStaleIntents();
            RemoveStaleOperationMembers();
            RefreshAnchorAndStaging(context.Knowledge, production);
            VisibleThreatCount = 0;
            if (!hasBaseAnchor || !hasStagingPosition)
            {
                ClearThreat();
                CancelOffense("Missing base or staging", context.Time, false);
                army.SetState(ArmyGroupState.Forming);
                return;
            }

            bool requireEmergency = IsOffensiveState || IsWithdrawalState;
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

            if (IsOffensiveState)
            {
                TickOffense(context, safety);
                return;
            }

            bool sufficientlyStaged = recoveryReady
                ? FractionInside(army.Members, stagingPosition, stagingRegionRadius)
                    >= RecoveryAssemblyFraction
                : FractionInside(army.Members, stagingPosition, stagingRegionRadius)
                    >= AssembledFraction;
            if (!sufficientlyStaged)
            {
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
                TryBeginOffense(context, safety);
        }

        public void Reset()
        {
            EndRetreatDirectives(army.Members);
            army.Reset();
            attackIntents.Clear();
            moveIntents.Clear();
            attackMoveIntents.Clear();
            formationIntents.Clear();
            retryAfter.Clear();
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
            OffensiveLaunchSerial = 0;
            LastOffensiveLaunchTime = -1f;
            TargetCompletionSerial = 0;
            LastTargetCompletionTime = -1f;
            tactical?.Reset();
        }

        private void TryBeginOffense(CommanderDecisionContext context,
            StrategicSafetyMap safety)
        {
            using (OffensiveEvaluationMarker.Auto())
            {
                if (!TrySelectOffensiveTarget(context, safety,
                        out OffensiveTargetSnapshot target))
                {
                    objectiveReason = "No legally known strategic target";
                    nextOffensiveEvaluationTime = context.Time + 1f;
                    return;
                }
                hasOffensiveTarget = true;
                offensiveTarget = target;
                currentObjectivePosition = target.LastKnownPosition;
                IsOffensiveTargetVisible = context.Knowledge
                    .TryGetVisibleEnemyBuilding(target.RuntimeId, out _);
                operationMembers.Clear();
                for (int i = 0; i < army.Members.Count; i++)
                    operationMembers.Add(army.Members[i]);
                operationStartTime = context.Time;
                preparationStartTime = context.Time;
                objectiveFailureCount = 0;
                cohesionRegroup = false;
                recoveryReady = false;
                ClearCampaignBaseline();
                currentRetreatReason = RetreatReason.None;
                objectiveReason = "Selected " + target.Category;
                ClearCommandIntents();
                army.SetState(ArmyGroupState.PreparingAttack);
                TickPreparingAttack(context, safety);
            }
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
            StrategicSafetyMap safety)
        {
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
                    TickAdvance(context, safety);
                    break;
                case ArmyGroupState.Assaulting:
                    TickAssault(context, safety);
                    break;
            }
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
                if ((assembled >= AssembledFraction
                        || timedOut && assembled >= TimeoutAssemblyFraction)
                    && (cohesionRegroup || PopulationOf(operationMembers)
                        >= MinOffensiveMilitaryPopulation))
                {
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
                    IssueAdvance(context, safety, true);
                    return;
                }
                if (timedOut && assembled < TimeoutAssemblyFraction)
                {
                    FailOffense(context, "Attack assembly timed out");
                    return;
                }
                RegroupMembers(context, operationMembers, assembly, true,
                    safety?.ArmyTraversal);
            }
        }

        private void TickAdvance(CommanderDecisionContext context,
            StrategicSafetyMap safety)
        {
            using (OffensiveAdvanceMarker.Auto())
            {
                if (IsOffensiveTargetVisible)
                {
                    attackMoveIntents.Clear();
                    army.SetState(ArmyGroupState.Assaulting);
                    objectiveReason = "Visible objective assault";
                    TickAssault(context, safety);
                    return;
                }
                Tactical.Refresh(operationMembers, context.Knowledge, army.State,
                    context.Time);
                if (Tactical.TryEngageVisibleEnemies(context, operationMembers,
                        Tactical.ArmyAnchor, Tactical.LocalEngagementRadius, default,
                        safety?.ArmyTraversal))
                {
                    objectiveReason = "Local tactical engagement";
                    return;
                }
                if (IsSeverelyFragmented(out Vector3 centroid))
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
                IssueAdvance(context, safety, false);
            }
        }

        private void IssueAdvance(CommanderDecisionContext context,
            StrategicSafetyMap safety, bool force)
        {
            EnsureOffensiveDestinations(context.Knowledge);
            int attempted = 0;
            int accepted = 0;
            int failed = 0;
            for (int i = 0; i < operationMembers.Count; i++)
            {
                UnitController member = operationMembers[i];
                if (IsMonk(member))
                {
                    SupportOffense(context, member, safety);
                    continue;
                }
                if (!attackMoveIntents.TryGetValue(member, out Vector3 destination))
                    continue;
                CombatController combat = member.GetComponent<CombatController>();
                if (!force && combat != null
                    && (combat.HasAttackMoveOrder || combat.Target != null)) continue;
                if (context.Time < RetryTime(member)) continue;
                attempted++;
                CommanderCommandResult result = context.Commands.TryAttackMove(member,
                    destination, safety?.ArmyTraversal);
                if (result == CommanderCommandResult.Accepted)
                {
                    attackMoveIntents[member] = destination;
                    moveIntents.Remove(member);
                    accepted++;
                }
                else
                {
                    retryAfter[member] = context.Time + commandRetrySeconds;
                    if (result == CommanderCommandResult.RejectedUnreachable
                        || result == CommanderCommandResult.RejectedInvalidTarget) failed++;
                }
            }
            if (attempted > 0 && accepted == 0 && failed > 0)
            {
                objectiveFailureCount++;
                if (objectiveFailureCount >= maximumObjectiveFailures)
                    FailOffense(context, "Objective route repeatedly failed");
            }
            else if (accepted > 0) objectiveFailureCount = 0;
        }

        private void TickAssault(CommanderDecisionContext context,
            StrategicSafetyMap safety)
        {
            if (!IsOffensiveTargetVisible)
            {
                attackIntents.Clear();
                army.SetState(ArmyGroupState.Advancing);
                objectiveReason = "Advance to remembered objective";
                IssueAdvance(context, safety, true);
                return;
            }
            Tactical.Refresh(operationMembers, context.Knowledge, army.State,
                context.Time);
            if (Tactical.TryEngageVisibleEnemies(context, operationMembers,
                    Tactical.ArmyAnchor, Tactical.LocalEngagementRadius, default,
                    safety?.ArmyTraversal))
            {
                objectiveReason = "Clear visible defenders";
                return;
            }
            for (int i = 0; i < operationMembers.Count; i++)
            {
                UnitController member = operationMembers[i];
                if (IsMonk(member))
                {
                    SupportOffense(context, member, safety);
                    continue;
                }
                CombatController combat = member.GetComponent<CombatController>();
                if (combat?.BuildingTarget != null
                    && combat.BuildingTarget.GetEntityId().CompareTo(
                        offensiveTarget.RuntimeId) == 0)
                {
                    attackIntents[member] = offensiveTarget.RuntimeId;
                    continue;
                }
                if (context.Time < RetryTime(member)) continue;
                CommanderCommandResult result = context.Commands.TryAttackBuilding(member,
                    offensiveTarget.RuntimeId);
                if (result == CommanderCommandResult.Accepted)
                {
                    attackIntents[member] = offensiveTarget.RuntimeId;
                    attackMoveIntents.Remove(member);
                }
                else retryAfter[member] = context.Time + commandRetrySeconds;
            }
        }

        private void SupportOffense(CommanderDecisionContext context,
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
            if (grid == null) return;
            Vector2Int cell = grid.WorldToGrid(desired);
            List<GridCell> choices = DestinationDistributor.FindNearbyDestinations(
                grid, cell, 1, 3, true);
            if (choices.Count == 0) return;
            desired = choices[0].WorldPosition;
            UnitMovement movement = monk.GetComponent<UnitMovement>();
            if (Vector2.Distance(monk.transform.position, desired)
                    <= destinationArrivalRadius
                || movement != null && movement.IsMoving
                    && moveIntents.TryGetValue(monk, out Vector3 existing)
                    && Vector2.Distance(existing, desired) <= 0.5f
                || context.Time < RetryTime(monk)) return;
            CommanderCommandResult result = context.Commands.TryMove(monk, desired,
                safety?.ArmyTraversal);
            if (result == CommanderCommandResult.Accepted) moveIntents[monk] = desired;
            else retryAfter[monk] = context.Time + commandRetrySeconds;
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
                blockedTargetId = offensiveTarget.RuntimeId;
                blockedTargetUntil = context.Time + FailedObjectiveCooldown;
            }
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
                context.Commands.TryRetreatMove(member, destination,
                    safety?.ArmyTraversal);
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
            if (assembled < RecoveryAssemblyFraction)
                Regroup(context, true);
        }

        private void ClearCampaignForRecovery()
        {
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
                members[i]?.GetComponent<CombatController>()?.EndRetreat();
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
            CommanderCommandResult result = context.Commands.TryMove(monk, desired);
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
            IPathTraversalPolicy traversalPolicy)
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
            IssueMoveAssignments(context, members, interruptCombat, traversalPolicy);
        }

        private void IssueMoveAssignments(CommanderDecisionContext context,
            IReadOnlyList<UnitController> members, bool interruptCombat,
            IPathTraversalPolicy traversalPolicy)
        {
            for (int i = 0; i < members.Count; i++)
            {
                UnitController member = members[i];
                if (!moveIntents.TryGetValue(member, out Vector3 destination)) continue;
                if (Vector2.Distance(member.transform.position, destination)
                    <= destinationArrivalRadius) continue;
                UnitMovement movement = member.GetComponent<UnitMovement>();
                if (!interruptCombat && movement != null && movement.IsMoving) continue;
                if (interruptCombat && movement != null && movement.IsMoving
                    && member.CurrentState == UnitState.Moving) continue;
                if (context.Time < RetryTime(member)) continue;
                CommanderCommandResult result = context.Commands.TryMove(member,
                    destination, traversalPolicy);
                if (result != CommanderCommandResult.Accepted)
                    retryAfter[member] = context.Time + commandRetrySeconds;
            }
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

        private void RemoveStaleIntents()
        {
            RemoveStaleKeys(attackIntents);
            RemoveStaleKeys(moveIntents);
            RemoveStaleKeys(attackMoveIntents);
            RemoveStaleKeys(retryAfter);
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

        private static bool IsDefenseEmergency(VisibleUnitObservation threat)
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
