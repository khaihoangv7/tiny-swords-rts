using System;
using System.Text;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    [DefaultExecutionOrder(100)]
    public sealed class RuleBasedCommander : MonoBehaviour, ICommander
    {
        private static readonly ProfilerMarker CommanderTickMarker =
            new("TinyKingdom.AI.CommanderTick");
        private static readonly ProfilerMarker KnowledgeRefreshMarker =
            new("TinyKingdom.AI.KnowledgeRefresh");
        private static readonly ProfilerMarker RoleSyncMarker =
            new("TinyKingdom.AI.RoleSync");
        private static readonly ProfilerMarker BuildingPolicyMarker =
            new("TinyKingdom.AI.BuildingPolicy");
        private static readonly ProfilerMarker ProductionPolicyMarker =
            new("TinyKingdom.AI.ProductionPolicy");
        private static readonly ProfilerMarker ScoutingPolicyMarker =
            new("TinyKingdom.AI.ScoutingPolicy");
        private static readonly ProfilerMarker MilitaryPolicyMarker =
            new("TinyKingdom.AI.MilitaryPolicy");
        private static readonly ProfilerMarker EconomyPolicyMarker =
            new("TinyKingdom.AI.EconomyPolicy");

        [SerializeField] private Faction faction = Faction.Red;
        [SerializeField] private bool commanderEnabled = true;
        [SerializeField, Min(0.1f)] private float thinkInterval = 0.50f;
        [SerializeField] private FactionKnowledge knowledge;
        [SerializeField] private FactionCommandService commandService;
        [SerializeField] private CommanderPawnRoles pawnRoles = new();
        [SerializeField] private BuildingCommanderPolicy buildingPolicy = new();
        [SerializeField] private ProductionCommanderPolicy productionPolicy = new();
        [SerializeField] private EconomyCommanderPolicy economyPolicy = new();
        [SerializeField] private ScoutingCommanderPolicy scoutingPolicy = new();
        [SerializeField] private MilitaryCommanderPolicy militaryPolicy = new();
        [SerializeField] private StrategicSafetyMap strategicSafety = new();
        [SerializeField] private CommanderProgressDiagnostics progressDiagnostics = new();
        [SerializeField] private bool drawScoutGizmo = false;
        [SerializeField] private bool drawMilitaryGizmos = false;
        [SerializeField] private bool drawStrategicSafetyGizmos = false;
        [Header("Runtime debug (disabled by default)")]
        [SerializeField] private bool exposeRuntimeDebug = false;
        [SerializeField] private bool warnOnStrategicStall = false;
        [SerializeField, Min(20f)] private float stallWarningSeconds = 25f;
        [SerializeField] private ulong debugThinkTick;
        [SerializeField] private int debugKnownFood;
        [SerializeField] private int debugKnownWood;
        [SerializeField] private int debugKnownGold;
        [SerializeField] private int debugFoodAssignments;
        [SerializeField] private int debugWoodAssignments;
        [SerializeField] private int debugGoldAssignments;
        [SerializeField] private UnitController debugScout;
        [SerializeField] private Vector2Int debugScoutDestination;
        [SerializeField] private int debugExploredCells;
        [SerializeField] private int debugVisibleEnemyUnits;
        [SerializeField] private int debugKnownEnemyBuildings;
        [SerializeField] private int debugPawnCount;
        [SerializeField] private int debugAvailableEconomyPawns;
        [SerializeField] private int debugPromotedScoutPawns;
        [SerializeField] private int debugPawnTarget;
        [SerializeField] private int debugMilitaryPopulation;
        [SerializeField] private int debugMilitaryTarget;
        [SerializeField] private int debugEffectiveFreePopulation;
        [SerializeField] private string debugPlannedBuilding;
        [SerializeField] private UnitController debugBuilder;
        [SerializeField] private Vector2Int debugBuildCell;
        [SerializeField] private int debugCompletedCoreInfrastructure;
        [SerializeField] private string debugProductionDecision;
        [SerializeField] private Vector2Int debugRallyStagingCell;
        [SerializeField] private ArmyGroupState debugArmyState;
        [SerializeField] private int debugArmyMemberCount;
        [SerializeField] private string debugCurrentThreatId;
        [SerializeField] private UnitType debugCurrentThreatType;
        [SerializeField] private Vector3 debugCurrentThreatPosition;
        [SerializeField] private float debugBaseDefenseRadius;
        [SerializeField] private float debugDefenseLeashRadius;
        [SerializeField] private Vector3 debugMilitaryStagingPosition;
        [SerializeField] private int debugVisibleThreats;
        [SerializeField] private int debugOffensiveReadyPopulation;
        [SerializeField] private int debugMinOffensivePopulation;
        [SerializeField] private string debugOffensiveTargetType;
        [SerializeField] private string debugOffensiveTargetId;
        [SerializeField] private Vector3 debugOffensiveTargetPosition;
        [SerializeField] private bool debugOffensiveTargetVisible;
        [SerializeField] private string debugOffensiveReason;
        [SerializeField] private float debugOffensiveStartTime;
        [SerializeField] private int debugOffensiveFailures;
        [SerializeField] private int debugCampaignStartPopulation;
        [SerializeField] private int debugCampaignStartMemberCount;
        [SerializeField] private float debugCampaignStartStrength;
        [SerializeField] private int debugCampaignSurvivors;
        [SerializeField] private float debugRemainingForceRatio;
        [SerializeField] private float debugOwnEffectiveStrength;
        [SerializeField] private float debugVisibleEnemyStrength;
        [SerializeField] private float debugEnemyOwnStrengthRatio;
        [SerializeField] private float debugAverageArmyHealth;
        [SerializeField] private int debugRelevantVisibleEnemies;
        [SerializeField] private Vector3 debugArmyCentroid;
        [SerializeField] private string debugRetreatReason;
        [SerializeField] private float debugRetreatStartTime;
        [SerializeField] private float debugRetreatArrivedFraction;
        [SerializeField] private float debugRecoveryDuration;
        [SerializeField] private bool debugRecoveryReady;
        [SerializeField] private int debugKnownStaticDangers;
        [SerializeField] private int debugVisibleDynamicDangers;
        [SerializeField] private string debugScoutSafetyState;
        [SerializeField] private string debugScoutDangerReason;
        [SerializeField] private Vector2Int debugScoutRetreatCell;
        [SerializeField] private string debugScoutTargetKind;
        [SerializeField] private string debugScoutIntent;
        [SerializeField] private int debugScoutFrontierCount;
        [SerializeField] private int debugScoutStaleCandidateCount;
        [SerializeField] private float debugScoutInformationGain;
        [SerializeField] private string debugScoutKnownRisk;
        [SerializeField] private float debugScoutTravelCost;
        [SerializeField] private float debugScoutTargetScore;
        [SerializeField] private float debugScoutStrategicValue;
        [SerializeField] private float debugScoutStaleness;
        [SerializeField] private float debugLastEnemyCastleObservation = -1f;
        [SerializeField] private string debugLastMilitaryObservation;
        [SerializeField] private string debugRecentScoutTargets;
        [SerializeField] private float debugScoutTargetAge;
        [SerializeField] private string debugScoutPromotionResult;
        [SerializeField] private float debugLastScoutDeathTime;
        [SerializeField] private float debugLastGatherAssignmentTime;
        [SerializeField] private float debugLastBuildCommandTime;
        [SerializeField] private float debugLastTrainCommandTime;
        [SerializeField] private float debugLastScoutDestinationTime;
        [SerializeField] private float debugLastArmyStateChangeTime;
        [SerializeField] private float debugLastOffensiveLaunchTime;
        [SerializeField] private float debugLastTargetCompletionTime;
        [SerializeField] private float debugLastResourceIncomeTime;
        [SerializeField] private float debugLastMeaningfulActionTime;
        [SerializeField, TextArea(4, 16)] private string debugEconomyWorkers;

        private float nextThinkTime;

        public Faction Faction => faction;
        public bool CommanderEnabled => commanderEnabled;
        public float ThinkInterval => thinkInterval;
        public ulong ThinkTick { get; private set; }
        public FactionKnowledge Knowledge => knowledge;
        public FactionCommandService CommandService => commandService;
        public EconomyCommanderPolicy EconomyPolicy => economyPolicy;
        public ScoutingCommanderPolicy ScoutingPolicy => scoutingPolicy;
        public CommanderPawnRoles PawnRoles => pawnRoles;
        public BuildingCommanderPolicy BuildingPolicy => buildingPolicy;
        public ProductionCommanderPolicy ProductionPolicy => productionPolicy;
        public MilitaryCommanderPolicy MilitaryPolicy => militaryPolicy;
        public StrategicSafetyMap StrategicSafety => strategicSafety;
        public CommanderProgressDiagnostics ProgressDiagnostics => progressDiagnostics;
        public float StallWarningSeconds => Mathf.Max(20f, stallWarningSeconds);
        public UnitController CurrentScout => scoutingPolicy?.Scout;
        public bool IsConfigured => FactionRelations.IsPlayable(faction) && knowledge != null
            && commandService != null && knowledge.Faction == faction
            && commandService.Faction == faction;

        public void Configure(Faction owner, FactionKnowledge factionKnowledge,
            FactionCommandService commands, float interval = 0.50f)
        {
            faction = owner;
            knowledge = factionKnowledge;
            commandService = commands;
            thinkInterval = Mathf.Max(0.1f, interval);
            nextThinkTime = Time.time;
        }

        private void OnEnable()
        {
            militaryPolicy ??= new MilitaryCommanderPolicy();
            strategicSafety ??= new StrategicSafetyMap();
            progressDiagnostics ??= new CommanderProgressDiagnostics();
            nextThinkTime = Time.time;
        }

        private void OnDisable()
        {
            economyPolicy?.Reset();
            scoutingPolicy?.Reset();
            buildingPolicy?.Reset(pawnRoles);
            productionPolicy?.Reset();
            militaryPolicy?.Reset();
            strategicSafety?.Reset();
            progressDiagnostics?.Reset();
            pawnRoles?.Reset();
            ThinkTick = 0;
        }

        private void Update()
        {
            if (!commanderEnabled || !IsConfigured || Time.time < nextThinkTime) return;
            MatchManager match = MatchManager.Instance;
            if (match == null || match.State != MatchState.Playing) return;
            nextThinkTime = Time.time + thinkInterval;
            Think(new CommanderDecisionContext(faction, Time.time, knowledge, commandService));
        }

        public void Think(CommanderDecisionContext context)
        {
            if (!commanderEnabled || context.Faction != faction
                || MatchManager.Instance == null
                || MatchManager.Instance.State != MatchState.Playing) return;
            using (CommanderTickMarker.Auto())
            {
                using (KnowledgeRefreshMarker.Auto())
                    context.Knowledge.Refresh();
                strategicSafety.Refresh(context.Knowledge);
                using (RoleSyncMarker.Auto())
                    pawnRoles.Synchronize(context.Knowledge);
                using (BuildingPolicyMarker.Auto())
                    buildingPolicy.Tick(context, pawnRoles, strategicSafety);
                using (ProductionPolicyMarker.Auto())
                    productionPolicy.Tick(context);
                using (ScoutingPolicyMarker.Auto())
                    scoutingPolicy.Tick(context, pawnRoles, strategicSafety, buildingPolicy);
                using (MilitaryPolicyMarker.Auto())
                    militaryPolicy.Tick(context, productionPolicy, scoutingPolicy.Scout,
                        strategicSafety);
                using (EconomyPolicyMarker.Auto())
                    economyPolicy.Tick(context, pawnRoles, strategicSafety);
                progressDiagnostics.Observe(context.Time, context.Knowledge,
                    economyPolicy, buildingPolicy, productionPolicy, scoutingPolicy,
                    militaryPolicy);
                if (warnOnStrategicStall && progressDiagnostics.TryConsumeStallWarning(
                        context.Time, StallWarningSeconds, out float stalledSeconds))
                    Debug.LogWarning("AI STALL: State=" + militaryPolicy.Army.State
                        + ", Seconds=" + stalledSeconds.ToString("0.0")
                        + ", Pawns=" + productionPolicy.LivingPawnCount
                        + ", GatherAssignments=" + economyPolicy.AssignmentCount
                        + ", ArmyMembers=" + militaryPolicy.Army.Members.Count, this);
                ThinkTick++;
                if (exposeRuntimeDebug) RefreshDebug();
            }
        }

        private void RefreshDebug()
        {
            debugThinkTick = ThinkTick;
            var economyDetails = new StringBuilder(256);
            for (int i = 0; i < knowledge.OwnUnits.Count; i++)
            {
                UnitController unit = knowledge.OwnUnits[i];
                PawnGatherer gatherer = unit != null
                    ? unit.GetComponent<PawnGatherer>() : null;
                if (unit == null || gatherer == null) continue;
                UnitMovement movement = unit.GetComponent<UnitMovement>();
                economyDetails.Append(unit.GetEntityId().ToString()).Append(" | ")
                    .Append(pawnRoles.GetRole(unit)).Append(" | ")
                    .Append(gatherer.CarriedResourceType).Append(' ')
                    .Append(gatherer.CarriedAmount).Append('/').Append(gatherer.CarryCapacity)
                    .Append(" | resource=")
                    .Append(gatherer.Target != null
                        ? gatherer.Target.GetEntityId().ToString() : "None")
                    .Append(" | dropoff=")
                    .Append(gatherer.HasDropOffTarget
                        ? gatherer.DropOffTargetId.ToString() : "None")
                    .Append(" | state=").Append(gatherer.State)
                    .Append(" moving=").Append(movement != null && movement.IsMoving)
                    .Append(" failures=").Append(gatherer.DropOffPathFailureCount)
                    .Append(" slot=").Append(gatherer.ReservedGatherCell)
                    .Append(" noProgress=")
                    .Append(gatherer.TimeSinceLastEconomicProgress.ToString("0.0"))
                    .AppendLine("s");
            }
            debugEconomyWorkers = economyDetails.ToString();
            debugKnownFood = debugKnownWood = debugKnownGold = 0;
            for (int i = 0; i < knowledge.KnownResources.Count; i++)
            {
                switch (knowledge.KnownResources[i].ResourceType)
                {
                    case ResourceType.Food: debugKnownFood++; break;
                    case ResourceType.Wood: debugKnownWood++; break;
                    case ResourceType.Gold: debugKnownGold++; break;
                }
            }
            debugFoodAssignments = economyPolicy.FoodAssignments;
            debugWoodAssignments = economyPolicy.WoodAssignments;
            debugGoldAssignments = economyPolicy.GoldAssignments;
            debugScout = scoutingPolicy.Scout;
            debugScoutDestination = scoutingPolicy.HasDestination
                ? scoutingPolicy.DestinationCell : new Vector2Int(-1, -1);
            debugExploredCells = knowledge.ExploredCellCount;
            debugVisibleEnemyUnits = knowledge.VisibleEnemyUnits.Count;
            debugKnownEnemyBuildings = knowledge.KnownEnemyBuildings.Count;
            debugPawnCount = productionPolicy.LivingPawnCount;
            debugAvailableEconomyPawns = pawnRoles.EconomyCount;
            debugPromotedScoutPawns = pawnRoles.ScoutCount;
            debugPawnTarget = productionPolicy.TargetPawnCount;
            debugMilitaryPopulation = productionPolicy.MilitaryPopulation
                + productionPolicy.QueuedMilitaryPopulation;
            debugMilitaryTarget = productionPolicy.TargetMilitaryPopulation;
            debugEffectiveFreePopulation = productionPolicy.EffectiveFreePopulation;
            debugPlannedBuilding = buildingPolicy.PlannedBuilding?.ToString() ?? "None";
            debugBuilder = buildingPolicy.CurrentBuilder;
            debugBuildCell = buildingPolicy.CurrentBuildCell;
            debugCompletedCoreInfrastructure = buildingPolicy.CompletedCoreInfrastructure;
            debugProductionDecision = productionPolicy.ActiveProductionDecision?.ToString()
                ?? "None";
            debugRallyStagingCell = productionPolicy.HasRallyStagingCell
                ? productionPolicy.RallyStagingCell : new Vector2Int(-1, -1);
            debugArmyState = militaryPolicy.Army.State;
            debugArmyMemberCount = militaryPolicy.Army.Members.Count;
            debugCurrentThreatId = militaryPolicy.HasCurrentThreat
                ? militaryPolicy.CurrentThreatId.ToString() : "None";
            debugCurrentThreatType = militaryPolicy.CurrentThreatType;
            debugCurrentThreatPosition = militaryPolicy.HasCurrentThreat
                ? militaryPolicy.CurrentThreatPosition : Vector3.zero;
            debugBaseDefenseRadius = militaryPolicy.BaseDefenseRadius;
            debugDefenseLeashRadius = militaryPolicy.DefenseLeashRadius;
            debugMilitaryStagingPosition = militaryPolicy.HasStagingPosition
                ? militaryPolicy.StagingPosition : Vector3.zero;
            debugVisibleThreats = militaryPolicy.VisibleThreatCount;
            debugOffensiveReadyPopulation = militaryPolicy.AvailableMilitaryPopulation;
            debugMinOffensivePopulation = militaryPolicy.MinOffensiveMilitaryPopulation;
            debugOffensiveTargetType = militaryPolicy.HasOffensiveTarget
                ? militaryPolicy.OffensiveTarget.BuildingType.ToString() : "None";
            debugOffensiveTargetId = militaryPolicy.HasOffensiveTarget
                ? militaryPolicy.OffensiveTarget.RuntimeId.ToString() : "None";
            debugOffensiveTargetPosition = militaryPolicy.HasOffensiveTarget
                ? militaryPolicy.CurrentObjectivePosition : Vector3.zero;
            debugOffensiveTargetVisible = militaryPolicy.IsOffensiveTargetVisible;
            debugOffensiveReason = militaryPolicy.ObjectiveReason;
            debugOffensiveStartTime = militaryPolicy.OperationStartTime;
            debugOffensiveFailures = militaryPolicy.ObjectiveFailureCount;
            debugCampaignStartPopulation = militaryPolicy.CampaignStartPopulation;
            debugCampaignStartMemberCount = militaryPolicy.CampaignStartMemberCount;
            debugCampaignStartStrength = militaryPolicy.CampaignStartEffectiveStrength;
            debugCampaignSurvivors = militaryPolicy.CampaignSurvivorCount;
            debugRemainingForceRatio = militaryPolicy.RemainingForceRatio;
            debugOwnEffectiveStrength = militaryPolicy.OwnEffectiveStrength;
            debugVisibleEnemyStrength = militaryPolicy.VisibleRelevantEnemyStrength;
            debugEnemyOwnStrengthRatio = militaryPolicy.EnemyStrengthRatio;
            debugAverageArmyHealth = militaryPolicy.AverageArmyHealth;
            debugRelevantVisibleEnemies = militaryPolicy.VisibleRelevantEnemyCount;
            debugArmyCentroid = militaryPolicy.ArmyCentroid;
            debugRetreatReason = militaryPolicy.CurrentRetreatReason.ToString();
            debugRetreatStartTime = militaryPolicy.RetreatStartTime;
            debugRetreatArrivedFraction = militaryPolicy.RetreatArrivedFraction;
            debugRecoveryDuration = militaryPolicy.RecoveryDuration(Time.time);
            debugRecoveryReady = militaryPolicy.RecoveryReady;
            debugKnownStaticDangers = strategicSafety.KnownStaticDangerCount;
            debugVisibleDynamicDangers = strategicSafety.VisibleDynamicThreatCount;
            debugScoutSafetyState = scoutingPolicy.SafetyState.ToString();
            debugScoutDangerReason = scoutingPolicy.DangerReason;
            debugScoutRetreatCell = scoutingPolicy.HasRetreatDestination
                ? scoutingPolicy.RetreatCell : new Vector2Int(-1, -1);
            debugScoutTargetKind = scoutingPolicy.TargetKind.ToString();
            debugScoutIntent = scoutingPolicy.Intent.ToString();
            debugScoutFrontierCount = scoutingPolicy.FrontierCandidateCount;
            debugScoutStaleCandidateCount = scoutingPolicy.StaleRevisitCandidateCount;
            debugScoutInformationGain = scoutingPolicy.LastInformationGain;
            debugScoutKnownRisk = scoutingPolicy.LastKnownRisk.ToString();
            debugScoutTravelCost = scoutingPolicy.LastTravelCost;
            debugScoutTargetScore = scoutingPolicy.LastTargetScore;
            debugScoutStrategicValue = scoutingPolicy.LastStrategicValue;
            debugScoutStaleness = scoutingPolicy.LastStaleness;
            debugLastEnemyCastleObservation = -1f;
            for (int i = 0; i < knowledge.KnownEnemyBuildings.Count; i++)
                if (knowledge.KnownEnemyBuildings[i].BuildingType == BuildingType.Castle)
                    debugLastEnemyCastleObservation = Mathf.Max(
                        debugLastEnemyCastleObservation,
                        knowledge.KnownEnemyBuildings[i].LastSeenTime);
            MilitaryObservationSnapshot military = knowledge.LastMilitaryObservation;
            debugLastMilitaryObservation = military.HasObservation
                ? $"W{military.WarriorCount} A{military.ArcherCount} "
                    + $"L{military.LancerCount} M{military.MonkCount} "
                    + $"at {military.ObservationRegion} t={military.LastSeenTime:0.0}"
                : "None";
            debugRecentScoutTargets = scoutingPolicy.RecentTargetHistory(Time.time);
            debugScoutTargetAge = scoutingPolicy.CurrentTargetAge(Time.time);
            debugScoutPromotionResult = scoutingPolicy.LastPromotionResult;
            debugLastScoutDeathTime = scoutingPolicy.LastScoutDeathTime;
            debugLastGatherAssignmentTime = progressDiagnostics.LastGatherAssignmentTime;
            debugLastBuildCommandTime = progressDiagnostics.LastBuildCommandTime;
            debugLastTrainCommandTime = progressDiagnostics.LastTrainCommandTime;
            debugLastScoutDestinationTime = progressDiagnostics.LastScoutDestinationTime;
            debugLastArmyStateChangeTime = progressDiagnostics.LastArmyStateChangeTime;
            debugLastOffensiveLaunchTime = progressDiagnostics.LastOffensiveLaunchTime;
            debugLastTargetCompletionTime = progressDiagnostics.LastTargetCompletionTime;
            debugLastResourceIncomeTime = progressDiagnostics.LastResourceIncomeTime;
            debugLastMeaningfulActionTime = progressDiagnostics.LastMeaningfulActionTime;
        }

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            if (knowledge == null || knowledge.TeamVision?.GridManager == null) return;
            if (drawScoutGizmo && scoutingPolicy != null && scoutingPolicy.HasDestination)
            {
                Vector2Int cell = scoutingPolicy.DestinationCell;
                Vector3 position = knowledge.TeamVision.GridManager.GridToWorld(cell.x, cell.y);
                Gizmos.color = scoutingPolicy.TargetKind switch
                {
                    ScoutTargetKind.Frontier => Color.green,
                    ScoutTargetKind.EnemyBaseRecon or ScoutTargetKind.ProductionRecon
                        or ScoutTargetKind.MilitaryRecon => Color.cyan,
                    ScoutTargetKind.ReScout => Color.yellow,
                    _ => Color.white
                };
                Gizmos.DrawWireSphere(position, 0.35f);
                if (scoutingPolicy.Scout != null)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(scoutingPolicy.Scout.transform.position, position);
                }
            }
            if (drawMilitaryGizmos && militaryPolicy != null)
            {
                if (militaryPolicy.HasBaseAnchor)
                {
                    Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.8f);
                    Gizmos.DrawWireSphere(militaryPolicy.BaseAnchor,
                        militaryPolicy.BaseDefenseRadius);
                    Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.6f);
                    Gizmos.DrawWireSphere(militaryPolicy.BaseAnchor,
                        militaryPolicy.DefenseLeashRadius);
                }
                if (militaryPolicy.HasStagingPosition)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireSphere(militaryPolicy.StagingPosition, 0.4f);
                }
                if (militaryPolicy.HasCurrentThreat)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(militaryPolicy.CurrentThreatPosition, 0.5f);
                }
                if (militaryPolicy.HasOffensiveTarget)
                {
                    Gizmos.color = militaryPolicy.IsOffensiveTargetVisible
                        ? Color.red : new Color(1f, 0.65f, 0f, 0.8f);
                    Gizmos.DrawWireSphere(militaryPolicy.CurrentObjectivePosition, 0.65f);
                    if (militaryPolicy.HasStagingPosition)
                        Gizmos.DrawLine(militaryPolicy.StagingPosition,
                            militaryPolicy.CurrentObjectivePosition);
                }
                if (militaryPolicy.Army.State is ArmyGroupState.Advancing
                    or ArmyGroupState.Assaulting or ArmyGroupState.Retreating)
                {
                    Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.45f);
                    Gizmos.DrawWireSphere(militaryPolicy.ArmyCentroid,
                        militaryPolicy.CombatAssessmentRadius);
                }
            }
            if (drawStrategicSafetyGizmos && strategicSafety != null
                && strategicSafety.HasHomeAnchor)
            {
                Gizmos.color = new Color(0.1f, 0.9f, 0.3f, 0.75f);
                Gizmos.DrawWireSphere(strategicSafety.HomeAnchor,
                    strategicSafety.HomeEconomicRadius);
                if (scoutingPolicy != null && scoutingPolicy.HasRetreatDestination)
                {
                    Vector2Int cell = scoutingPolicy.RetreatCell;
                    Vector3 position = knowledge.TeamVision.GridManager.GridToWorld(
                        cell.x, cell.y);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(position, 0.4f);
                }
            }
#endif
        }
    }
}
