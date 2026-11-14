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
        private static readonly ProfilerMarker StrategicIntentMarker =
            new("TinyKingdom.AI.StrategicIntent");
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
        [SerializeField] private EconomicDemandModel economicDemand = new();
        [SerializeField] private ScoutingCommanderPolicy scoutingPolicy = new();
        [SerializeField] private MilitaryCommanderPolicy militaryPolicy = new();
        [SerializeField] private StrategicSafetyMap strategicSafety = new();
        [SerializeField] private StrategicIntentEvaluator strategicIntent = new();
        [SerializeField] private GoldMineWorkerPolicy goldMineWorkers = new();
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
        [SerializeField] private string debugEnemyComposition;
        [SerializeField] private string debugDesiredComposition;
        [SerializeField] private string debugProjectedComposition;
        [SerializeField] private string debugProductionScores;
        [SerializeField] private string debugProductionInfrastructureNeed;
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
        [SerializeField] private TacticalArmyState debugTacticalState;
        [SerializeField] private Vector3 debugTacticalArmyAnchor;
        [SerializeField] private float debugTacticalCohesionRadius;
        [SerializeField] private int debugTacticalFrontlineCount;
        [SerializeField] private int debugTacticalBacklineCount;
        [SerializeField] private int debugTacticalSupportCount;
        [SerializeField] private int debugTacticalStragglerCount;
        [SerializeField] private int debugTacticalVisibleEnemies;
        [SerializeField] private float debugDirectCombatStrength;
        [SerializeField] private float debugSupportStrength;
        [SerializeField] private int debugTacticalAttackCommands;
        [SerializeField] private int debugTacticalRetargets;
        [SerializeField] private int debugTacticalFormationUpdates;
        [SerializeField] private int debugTacticalRepositions;
        [SerializeField, TextArea(3, 12)] private string debugTacticalUnits;
        [SerializeField] private string debugTacticalTargetSaturation;
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
        [SerializeField] private float debugScoutRepositioningDuration;
        [SerializeField] private uint debugScoutStallRecoveries;
        [SerializeField] private uint debugScoutFailedTargetRecoveries;
        [SerializeField] private uint debugScoutRejectedDestinations;
        [SerializeField] private string debugScoutLastRejectedMove;
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
        [SerializeField, TextArea(2, 8)] private string debugConstructionSite;
        [SerializeField, TextArea(3, 12)] private string debugConstructionBuilders;
        [SerializeField] private string debugEconomicDemand;
        [SerializeField] private string debugEconomicReassignment;
        [SerializeField] private string debugEconomicOutpost;
        [SerializeField] private int debugEconomicClusterCount;
        [SerializeField] private StrategicIntent debugStrategicIntent;
        [SerializeField] private StrategicIntent debugPreviousStrategicIntent;
        [SerializeField] private float debugStrategicIntentAge;
        [SerializeField] private string debugStrategicScores;
        [SerializeField] private float debugAttackReadiness;
        [SerializeField] private string debugStrategicTarget;
        [SerializeField] private float debugStrategicTargetAge;
        [SerializeField] private float debugRecentStrategicLoss;
        [SerializeField] private string debugStrategicReason;
        [SerializeField] private float debugStrategicOwnArmyStrength;
        [SerializeField] private float debugStrategicKnownEnemyStrength;
        [SerializeField] private float debugStrategicEnemyStrengthConfidence;
        [SerializeField] private string debugStrategicTargetType;
        [SerializeField] private float debugStrategicTargetScore;
        [SerializeField] private Vector2Int debugStrategicStagingCell;
        [SerializeField] private float debugStrategicTransitionTime;
        [SerializeField] private string debugGoldMineOpportunity;
        [SerializeField] private int debugKnownGoblins;
        [SerializeField] private uint debugMineAssignments;
        [SerializeField] private uint debugMineFailures;

        private float nextThinkTime;
        private bool externallyDriven;

        public Faction Faction => faction;
        public bool CommanderEnabled => commanderEnabled;
        public float ThinkInterval => thinkInterval;
        public ulong ThinkTick { get; private set; }
        public FactionKnowledge Knowledge => knowledge;
        public FactionCommandService CommandService => commandService;
        public EconomyCommanderPolicy EconomyPolicy => economyPolicy;
        public EconomicDemandModel EconomicDemand => economicDemand;
        public ScoutingCommanderPolicy ScoutingPolicy => scoutingPolicy;
        public CommanderPawnRoles PawnRoles => pawnRoles;
        public BuildingCommanderPolicy BuildingPolicy => buildingPolicy;
        public ProductionCommanderPolicy ProductionPolicy => productionPolicy;
        public MilitaryCommanderPolicy MilitaryPolicy => militaryPolicy;
        public StrategicSafetyMap StrategicSafety => strategicSafety;
        public StrategicIntentEvaluator StrategicIntent => strategicIntent;
        public GoldMineWorkerPolicy GoldMineWorkers => goldMineWorkers;
        public CommanderProgressDiagnostics ProgressDiagnostics => progressDiagnostics;
        public float StallWarningSeconds => Mathf.Max(20f, stallWarningSeconds);
        public UnitController CurrentScout => scoutingPolicy?.Scout;
        public bool IsConfigured => FactionRelations.IsPlayable(faction) && knowledge != null
            && commandService != null && knowledge.Faction == faction
            && commandService.Faction == faction;

        // The RL adapter owns the decision clock while enabled. The ordinary
        // Update path remains unchanged when no adapter is active.
        public void SetExternalDrive(bool value) => externallyDriven = value;

        public bool ThinkExternal(CommanderDecisionContext context,
            StrategicIntent? requestedIntent)
        {
            externalIntent = requestedIntent;
            externalIntentAccepted = !requestedIntent.HasValue;
            try { Think(context); return externalIntentAccepted; }
            finally { externalIntent = null; }
        }

        private StrategicIntent? externalIntent;
        private bool externalIntentAccepted;
        private HarassmentRuleBasedCommander harassment;
        private MatchDifficulty? demoDifficulty;

        internal void ConfigureDemo(MatchDifficulty difficulty, HarassmentRuleBasedCommander strategy = null)
        {
            demoDifficulty = difficulty;
            harassment = strategy;
            if (knowledge != null) knowledge.ObserveBuildingEdges = strategy != null;
        }

        public void Configure(Faction owner, FactionKnowledge factionKnowledge,
            FactionCommandService commands, float interval = 0.50f)
        {
            faction = owner;
            knowledge = factionKnowledge;
            commandService = commands;
            thinkInterval = Mathf.Max(0.1f, interval);
            nextThinkTime = Time.time;
        }

        private void Start()
        {
            if (faction != Faction.Red && !demoDifficulty.HasValue) return;
            MatchDifficultyCatalog catalog = MatchDifficultyCatalog.Load();
            if (catalog == null) return;
            CommanderDifficultyProfile profile = catalog.Profile(demoDifficulty ?? MatchDifficultyCatalog.Selected);
            thinkInterval = Mathf.Max(0.1f, profile.thinkInterval);
            productionPolicy.ConfigureDifficultyTargets(profile.targetPawns, profile.targetMilitary);
        }

        private void OnEnable()
        {
            militaryPolicy ??= new MilitaryCommanderPolicy();
            strategicSafety ??= new StrategicSafetyMap();
            strategicIntent ??= new StrategicIntentEvaluator();
            goldMineWorkers ??= new GoldMineWorkerPolicy();
            progressDiagnostics ??= new CommanderProgressDiagnostics();
            economicDemand ??= new EconomicDemandModel();
            nextThinkTime = Time.time;
        }

        private void OnDisable()
        {
            economyPolicy?.Reset();
            economicDemand?.Reset();
            scoutingPolicy?.Reset();
            buildingPolicy?.Reset(pawnRoles);
            productionPolicy?.Reset();
            militaryPolicy?.Reset();
            strategicSafety?.Reset();
            strategicIntent?.Reset();
            goldMineWorkers?.Reset(pawnRoles);
            progressDiagnostics?.Reset();
            pawnRoles?.Reset();
            ThinkTick = 0;
        }

        private void Update()
        {
            if (externallyDriven || !commanderEnabled || !IsConfigured
                || Time.time < nextThinkTime) return;
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
                economicDemand.Refresh(context, buildingPolicy, productionPolicy,
                    scoutingPolicy);
                using (BuildingPolicyMarker.Auto())
                    buildingPolicy.Tick(context, pawnRoles, strategicSafety,
                        economicDemand, economyPolicy);
                using (ProductionPolicyMarker.Auto())
                    productionPolicy.Tick(context, buildingPolicy);
                bool qlIsland = QlExperiment.UsesGoblinIslandStrategy(this);
                using (ScoutingPolicyMarker.Auto())
                    scoutingPolicy.Tick(context, pawnRoles, strategicSafety,
                        buildingPolicy, strategicIntent, qlIsland);
                economicDemand.Refresh(context, buildingPolicy, productionPolicy,
                    scoutingPolicy);
                StrategicIntent? intent = harassment == null ? externalIntent : harassment.Tick(context);
                bool qlCommander = QlExperiment.Active?.Controls(this) == true;
                bool qlOpponent = QlExperiment.IsGoblinIslandTrainingOpponent(this);
                // Production and economy actions have no intent of their own.
                // Preserve a selected siege while those actions are processed.
                if (qlIsland && !intent.HasValue
                    && strategicIntent.CurrentIntent == AI.StrategicIntent.Attack)
                    intent = AI.StrategicIntent.Attack;
                bool demoAssaultApproved = (harassment != null || qlCommander)
                    && (intent == AI.StrategicIntent.Attack
                        || (qlCommander && militaryPolicy.HasOffensiveTarget
                            && militaryPolicy.OffensiveTarget.BuildingType != BuildingType.GoldMine));
                using (StrategicIntentMarker.Auto())
                    strategicIntent.Tick(context, pawnRoles, economicDemand,
                        economyPolicy, buildingPolicy, productionPolicy,
                        scoutingPolicy, militaryPolicy, strategicSafety,
                        intent, demoAssaultApproved,
                        prioritizeCastle: harassment != null || qlCommander || qlOpponent,
                        commanderHandlesAssembly: qlIsland);
                externalIntentAccepted = strategicIntent.LastExternalIntentAccepted;
                using (MilitaryPolicyMarker.Auto())
                    militaryPolicy.Tick(context, productionPolicy, scoutingPolicy.Scout,
                        strategicSafety, strategicIntent, harassment?.DetachedUnits,
                        demoAssaultApproved, qlIsland, qlOpponent);
                goldMineWorkers.Tick(context, pawnRoles,
                    strategicIntent.GoldMine.Current, strategicSafety,
                    strategicIntent.GoldMine);
                using (EconomyPolicyMarker.Auto())
                    economyPolicy.Tick(context, pawnRoles, strategicSafety, economicDemand);
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
                if (warnOnStrategicStall
                    && progressDiagnostics.TryCaptureMilitaryLivenessStall(
                        context.Time, StallWarningSeconds, context.Knowledge,
                        strategicIntent, militaryPolicy, out string militarySnapshot))
                    Debug.LogWarning(militarySnapshot, this);
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
                    .Append(gatherer.CarriedAmount).Append('/').Append(gatherer.DisplayCarryCapacity)
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
            debugEconomicDemand = $"Stock F{economicDemand.Food}/W{economicDemand.Wood}/G{economicDemand.Gold} "
                + $"Deficit F{economicDemand.FoodDeficit}/W{economicDemand.WoodDeficit}/G{economicDemand.GoldDeficit} "
                + $"Gold cargo {economicDemand.CarriedGold}, en route {economicDemand.GoldInTransit}, "
                + $"expected {economicDemand.ExpectedGold}, planning gap {economicDemand.GoldPlanningDeficit}; "
                + economicDemand.Reason;
            debugEconomicReassignment = economyPolicy.LastReassignmentReason
                + $" at {economyPolicy.LastReassignmentTime:0.0}";
            debugEconomicOutpost = buildingPolicy.OutpostDecision;
            debugEconomicClusterCount = buildingPolicy.ClusterPlanner?.Clusters.Count ?? 0;
            debugStrategicIntent = strategicIntent.CurrentIntent;
            debugPreviousStrategicIntent = strategicIntent.PreviousIntent;
            debugStrategicIntentAge = strategicIntent.IntentAge(Time.time);
            StrategicIntentScores strategicScores = strategicIntent.Scores;
            debugStrategicScores = $"Economy={strategicScores.DevelopEconomy:0.00} "
                + $"Scout={strategicScores.Scout:0.00} "
                + $"Fortify={strategicScores.Fortify:0.00} "
                + $"Prepare={strategicScores.PrepareAttack:0.00} "
                + $"Attack={strategicScores.Attack:0.00} "
                + $"Recover={strategicScores.Recover:0.00} "
                + $"Mine={strategicScores.SecureGoldMine:0.00}";
            debugAttackReadiness = strategicIntent.AttackReadiness;
            debugStrategicTarget = strategicIntent.HasStrategicTarget
                ? strategicIntent.StrategicTarget.BuildingType + " "
                    + strategicIntent.StrategicTarget.RuntimeId + " score="
                    + strategicIntent.StrategicTarget.Score.ToString("0.00")
                : "None";
            debugStrategicTargetAge = strategicIntent.HasStrategicTarget
                ? Mathf.Max(0f, Time.time
                    - strategicIntent.StrategicTarget.LastSeenTime) : -1f;
            debugRecentStrategicLoss = strategicIntent.Situation.RecentLossRatio;
            debugStrategicReason = strategicIntent.DecisionReason;
            debugStrategicOwnArmyStrength = strategicIntent.Situation.OwnStrength;
            debugStrategicKnownEnemyStrength = strategicIntent.Situation.EnemyStrength;
            debugStrategicEnemyStrengthConfidence =
                strategicIntent.Situation.IntelConfidence;
            debugStrategicTargetType = strategicIntent.HasStrategicTarget
                ? strategicIntent.StrategicTarget.BuildingType.ToString() : "None";
            debugStrategicTargetScore = strategicIntent.HasStrategicTarget
                ? strategicIntent.StrategicTarget.Score : 0f;
            debugStrategicStagingCell = strategicIntent.Situation.StagingCell;
            debugStrategicTransitionTime = strategicIntent.TransitionGameTime;
            GoldMineOpportunity mineOpportunity = strategicIntent.GoldMine.Current;
            debugGoldMineOpportunity = mineOpportunity.IsValid
                ? $"{mineOpportunity.Mine.RuntimeId} score={mineOpportunity.Score:0.00} "
                    + $"guard={mineOpportunity.KnownGuardStrength:0.0} "
                    + $"force={mineOpportunity.OwnStrength:0.0} "
                    + $"commit={mineOpportunity.CanCommit} "
                    + $"safe={mineOpportunity.SafeForMiners} "
                    + mineOpportunity.Reason : "No known viable mine";
            debugKnownGoblins = knowledge.KnownGoblins.Count;
            debugMineAssignments = goldMineWorkers.AssignmentSerial;
            debugMineFailures = strategicIntent.GoldMine.FailedAttemptCount
                + goldMineWorkers.FailedAttemptCount;
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
            BuildingSite constructionSite = buildingPolicy.ActiveSite;
            if (constructionSite == null)
            {
                debugConstructionSite = "None";
                debugConstructionBuilders = "None";
            }
            else
            {
                debugConstructionSite = $"{constructionSite.GetEntityId()} | "
                    + $"{constructionSite.Type} | progress="
                    + $"{constructionSite.ConstructionProgress:0.000} | lastProgress="
                    + $"{constructionSite.LastProgressTime:0.0} | assigned="
                    + $"{constructionSite.ActiveBuilderCount} | working="
                    + $"{constructionSite.WorkingBuilderCount} | reachable="
                    + $"{constructionSite.ReachableBuilderSlotCount} | free="
                    + $"{constructionSite.FreeBuilderSlotCount} | recovery="
                    + $"{constructionSite.RecoveryState} | "
                    + constructionSite.LastRecoveryReason;
                var builderDetails = new StringBuilder(192);
                for (int i = 0; i < knowledge.OwnUnits.Count; i++)
                {
                    UnitController unit = knowledge.OwnUnits[i];
                    PawnBuilder job = unit != null ? unit.GetComponent<PawnBuilder>() : null;
                    if (job == null || job.Target != constructionSite) continue;
                    UnitMovement movement = unit.GetComponent<UnitMovement>();
                    builderDetails.Append(unit.GetEntityId().ToString()).Append(" | role=")
                        .Append(pawnRoles.GetRole(unit)).Append(" | site=")
                        .Append(constructionSite.GetEntityId().ToString()).Append(" | slot=")
                        .Append(job.ReservedSlotId).Append(" | distance=")
                        .Append(job.DistanceToInteraction.ToString("0.00"))
                        .Append(" | moving=").Append(movement != null && movement.IsMoving)
                        .Append(" | working=").Append(job.IsWorking)
                        .Append(" | failures=").Append(job.PathFailureCount)
                        .Append(" | lastBuild=")
                        .Append(job.LastBuildCommandTime.ToString("0.0")).AppendLine();
                }
                debugConstructionBuilders = builderDetails.Length > 0
                    ? builderDetails.ToString() : "None";
            }
            debugProductionDecision = productionPolicy.ActiveProductionDecision?.ToString()
                ?? productionPolicy.PreferredMilitaryUnit?.ToString() ?? "None";
            EnemyCompositionMemory enemy = productionPolicy.EnemyComposition;
            debugEnemyComposition = $"{productionPolicy.IntelSource} "
                + $"confidence={productionPolicy.IntelConfidence:0.00} "
                + $"age={productionPolicy.IntelAge:0.0}s | "
                + $"W{enemy.ObservedCount(UnitType.Warrior)} "
                + $"A{enemy.ObservedCount(UnitType.Archer)} "
                + $"L{enemy.ObservedCount(UnitType.Lancer)} "
                + $"M{enemy.ObservedCount(UnitType.Monk)}";
            debugDesiredComposition = $"W{productionPolicy.DesiredCount(UnitType.Warrior):0.0} "
                + $"A{productionPolicy.DesiredCount(UnitType.Archer):0.0} "
                + $"L{productionPolicy.DesiredCount(UnitType.Lancer):0.0} "
                + $"M{productionPolicy.DesiredCount(UnitType.Monk):0.0}";
            debugProjectedComposition = $"W{productionPolicy.LivingCount(UnitType.Warrior) + productionPolicy.QueuedCount(UnitType.Warrior)} "
                + $"A{productionPolicy.LivingCount(UnitType.Archer) + productionPolicy.QueuedCount(UnitType.Archer)} "
                + $"L{productionPolicy.LivingCount(UnitType.Lancer) + productionPolicy.QueuedCount(UnitType.Lancer)} "
                + $"M{productionPolicy.LivingCount(UnitType.Monk) + productionPolicy.QueuedCount(UnitType.Monk)}";
            debugProductionScores = $"W{productionPolicy.CandidateScore(UnitType.Warrior):0.00} "
                + $"A{productionPolicy.CandidateScore(UnitType.Archer):0.00} "
                + $"L{productionPolicy.CandidateScore(UnitType.Lancer):0.00} "
                + $"M{productionPolicy.CandidateScore(UnitType.Monk):0.00} | "
                + productionPolicy.DecisionReason;
            debugProductionInfrastructureNeed = buildingPolicy.ProductionInfrastructureNeed
                ?.ToString() ?? "None";
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
            TacticalArmyController tactical = militaryPolicy.Tactical;
            ArmyTacticalSnapshot tacticalSnapshot = tactical.Snapshot;
            debugTacticalState = tactical.State;
            debugTacticalArmyAnchor = tactical.ArmyAnchor;
            debugTacticalCohesionRadius = tactical.ArmyCohesionRadius;
            debugTacticalFrontlineCount = tacticalSnapshot.FrontlineCount;
            debugTacticalBacklineCount = tacticalSnapshot.BacklineCount;
            debugTacticalSupportCount = tacticalSnapshot.SupportCount;
            debugTacticalStragglerCount = tactical.StragglerCount;
            debugTacticalVisibleEnemies = tactical.VisibleEnemyCount;
            debugDirectCombatStrength = militaryPolicy.DirectCombatStrength;
            debugSupportStrength = militaryPolicy.SupportStrength;
            debugTacticalAttackCommands = tactical.AttackCommandsIssued;
            debugTacticalRetargets = tactical.RetargetCount;
            debugTacticalFormationUpdates = tactical.FormationUpdateCount;
            debugTacticalRepositions = tactical.RepositionCommandCount;
            debugTacticalUnits = tactical.UnitDebugSummary(militaryPolicy.Army.Members,
                Time.time);
            debugTacticalTargetSaturation = tactical.TargetSaturationSummary();
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
            debugScoutRepositioningDuration = scoutingPolicy.RepositioningDuration(Time.time);
            debugScoutStallRecoveries = scoutingPolicy.StallRecoveryCount;
            debugScoutFailedTargetRecoveries = scoutingPolicy.FailedTargetRecoveryCount;
            debugScoutRejectedDestinations = scoutingPolicy.RejectedDestinationCount;
            debugScoutLastRejectedMove = scoutingPolicy.LastRejectedMoveResult.ToString();
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
                TacticalArmyController tactical = militaryPolicy.Tactical;
                Gizmos.color = new Color(0.15f, 0.7f, 1f, 0.65f);
                Gizmos.DrawWireSphere(tactical.ArmyAnchor,
                    tactical.ArmyCohesionRadius);
                foreach (System.Collections.Generic.KeyValuePair<UnitController, Vector3>
                    assignment in tactical.FormationDestinations)
                {
                    if (assignment.Key == null) continue;
                    Gizmos.DrawLine(assignment.Key.transform.position, assignment.Value);
                    Gizmos.DrawWireSphere(assignment.Value, 0.15f);
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
