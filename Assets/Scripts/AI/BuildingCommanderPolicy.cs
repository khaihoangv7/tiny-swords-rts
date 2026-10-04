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
    public sealed class BuildingCommanderPolicy
    {
        private static readonly ProfilerMarker PlacementSearchMarker =
            new("TinyKingdom.AI.BuildPlacementSearch");
        [SerializeField, Min(0)] private int freePopulationThreshold = 2;
        [SerializeField, Min(0)] private int minimumEconomyWorkers = 2;
        [SerializeField, Min(1)] private int barracksPawnThreshold = 4;
        [SerializeField, Min(1)] private int archeryPawnThreshold = 5;
        [SerializeField, Min(1)] private int monasteryPawnThreshold = 6;
        [SerializeField, Min(1)] private int minimumPlacementRadius = 3;
        [SerializeField, Min(2)] private int maximumPlacementRadius = 10;
        [SerializeField, Min(1)] private int attemptsPerThink = 4;
        [SerializeField, Min(0.5f)] private float retrySeconds = 2f;
        [SerializeField, Min(0)] private int foodSafetyReserve = 50;
        [SerializeField, Range(1, 8)] private int maximumConcurrentBuilders = 3;
        [SerializeField, Min(0.25f)] private float builderAssignmentCooldown = 0.5f;
        [SerializeField, Min(10f)] private float economicOutpostCooldown = 45f;
        [SerializeField, Min(2)] private int outpostPlacementRadius = 4;
        [SerializeField, Min(5f)] private float productionNeedHoldSeconds = 20f;
        [SerializeField, Min(1f)] private float productionNeedRequestCooldown = 8f;

        private readonly List<Vector2Int> candidates = new();
        [SerializeField] private EconomicResourceClusterPlanner clusterPlanner = new();
        private BuildingSite activeSite;
        private UnitController builder;
        private BuildingType? plannedBuilding;
        private BuildingType candidateType;
        private int candidateIndex;
        private float nextAttemptTime;
        private float nextBuilderAssignmentTime;
        private Vector2Int currentBuildCell = new(-1, -1);
        private bool planningEconomicOutpost;
        private Vector2Int outpostClusterCell;
        private float lastOutpostTime = -999f;
        private string lastOutpostDecision = "Not evaluated";
        private BuildingType? productionInfrastructureNeed;
        private float productionInfrastructureNeedUntil;
        private float lastProductionNeedRequestTime = -999f;

        public int FreePopulationThreshold => freePopulationThreshold;
        public int MinimumEconomyWorkers => minimumEconomyWorkers;
        public BuildingType? PlannedBuilding => plannedBuilding;
        public BuildingSite ActiveSite => activeSite;
        public UnitController CurrentBuilder => builder;
        public Vector2Int CurrentBuildCell => currentBuildCell;
        public int CompletedCoreInfrastructure { get; private set; }
        public int MaximumConcurrentBuilders => maximumConcurrentBuilders;
        public uint SuccessfulCommandSerial { get; private set; }
        public float LastSuccessfulCommandTime { get; private set; } = -1f;
        public static long CandidateEvaluationCount { get; private set; }
        public EconomicResourceClusterPlanner ClusterPlanner => clusterPlanner;
        public bool PlanningEconomicOutpost => planningEconomicOutpost;
        public float LastOutpostTime => lastOutpostTime;
        public string OutpostDecision => planningEconomicOutpost
            ? $"Tower near {outpostClusterCell}; score={clusterPlanner.Best.Score:0.0}"
            : lastOutpostDecision;
        public BuildingType? ProductionInfrastructureNeed => productionInfrastructureNeed;
        public float ProductionNeedHoldSeconds => productionNeedHoldSeconds;
        public float ProductionNeedRequestCooldown => productionNeedRequestCooldown;

        public static void ResetPerformanceCounters() => CandidateEvaluationCount = 0;

        public void Reset(CommanderPawnRoles roles = null)
        {
            if (builder != null) roles?.Release(builder, CommanderPawnRole.Builder);
            activeSite = null;
            builder = null;
            plannedBuilding = null;
            candidates.Clear();
            candidateIndex = 0;
            nextAttemptTime = 0f;
            nextBuilderAssignmentTime = 0f;
            currentBuildCell = new Vector2Int(-1, -1);
            CompletedCoreInfrastructure = 0;
            SuccessfulCommandSerial = 0;
            LastSuccessfulCommandTime = -1f;
            planningEconomicOutpost = false;
            outpostClusterCell = new Vector2Int(-1, -1);
            lastOutpostTime = -999f;
            lastOutpostDecision = "Reset";
            productionInfrastructureNeed = null;
            productionInfrastructureNeedUntil = 0f;
            lastProductionNeedRequestTime = -999f;
            clusterPlanner?.Reset();
        }

        public bool RequestProductionInfrastructure(BuildingType type, float now)
        {
            if (type is not (BuildingType.Barracks or BuildingType.Archery
                or BuildingType.Monastery)) return false;
            if (productionInfrastructureNeed.HasValue
                && now <= productionInfrastructureNeedUntil) return false;
            if (now - lastProductionNeedRequestTime < productionNeedRequestCooldown)
                return false;
            productionInfrastructureNeed = type;
            productionInfrastructureNeedUntil = now + productionNeedHoldSeconds;
            lastProductionNeedRequestTime = now;
            return true;
        }

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles)
            => Tick(context, roles, null);

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles,
            StrategicSafetyMap safety)
            => Tick(context, roles, safety, null, null);

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles,
            StrategicSafetyMap safety, EconomicDemandModel demand,
            EconomyCommanderPolicy economy)
        {
            RefreshInfrastructure(context.Knowledge);
            if (MaintainActiveProject(context, roles, safety)) return;

            clusterPlanner ??= new EconomicResourceClusterPlanner();
            clusterPlanner.Refresh(context, safety);

            BuildingType? need = ChooseNeed(context.Knowledge, demand);
            bool outpost = false;
            if (!need.HasValue && CanPlanEconomicOutpost(context, safety, demand,
                out EconomicClusterSnapshot cluster))
            {
                need = BuildingType.Tower;
                outpost = true;
                outpostClusterCell = cluster.CenterCell;
            }
            if (!need.HasValue)
            {
                ClearPlan();
                return;
            }
            if (plannedBuilding != need || planningEconomicOutpost != outpost)
            {
                plannedBuilding = need;
                planningEconomicOutpost = outpost;
                candidates.Clear();
                candidateIndex = 0;
                currentBuildCell = new Vector2Int(-1, -1);
            }
            if (context.Time < nextAttemptTime) return;

            BuildingSystem system = context.Commands.BuildingSystem;
            BuildingData data = system != null ? system.GetData(need.Value) : null;
            if (data == null) { nextAttemptTime = context.Time + retrySeconds; return; }
            if (need.Value != BuildingType.House
                && context.Knowledge.Stockpile.GetAmount(ResourceType.Food) < foodSafetyReserve)
                return;
            if (!system.CanAfford(need.Value))
            {
                nextAttemptTime = context.Time + retrySeconds;
                return;
            }
            if (outpost && demand != null && !demand.CanExecuteReservedPlan()) return;

            UnitController chosen = ChooseBuilder(context, roles, demand, economy);
            if (chosen == null) return;
            PawnBuilder pawnBuilder = chosen.GetComponent<PawnBuilder>();
            using (PlacementSearchMarker.Auto())
            {
                if (candidates.Count == 0)
                {
                    if (outpost) PrepareOutpostCandidates(data, outpostClusterCell);
                    else PrepareCandidates(context.Knowledge, data);
                }

                int attempts = 0;
                while (candidateIndex < candidates.Count && attempts < attemptsPerThink)
                {
                    Vector2Int origin = candidates[candidateIndex++];
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                    CandidateEvaluationCount++;
#endif
                    if (!IsPolicyCandidate(context.Knowledge, data, origin)
                        || !IsFootprintSafe(context.Knowledge, data, origin, safety))
                        continue;
                    attempts++;
                    roles.TryAssign(chosen, CommanderPawnRole.Builder);
                    CommanderCommandResult result = context.Commands.TryBuild(
                        pawnBuilder, need.Value, origin, out BuildingSite site);
                    if (result == CommanderCommandResult.Accepted)
                    {
                        activeSite = site;
                        builder = chosen;
                        currentBuildCell = origin;
                        SuccessfulCommandSerial++;
                        LastSuccessfulCommandTime = context.Time;
                        if (outpost)
                        {
                            lastOutpostTime = context.Time;
                            lastOutpostDecision = $"Accepted Tower at {origin}";
                        }
                        return;
                    }
                    roles.Release(chosen, CommanderPawnRole.Builder);
                    if (result == CommanderCommandResult.RejectedInsufficientResources)
                        break;
                }
            }

            nextAttemptTime = context.Time + retrySeconds;
            if (candidateIndex >= candidates.Count)
            {
                candidates.Clear();
                candidateIndex = 0;
            }
        }

        private bool MaintainActiveProject(CommanderDecisionContext context,
            CommanderPawnRoles roles, StrategicSafetyMap safety)
        {
            if (activeSite == null) activeSite = FindExistingProject(context.Knowledge);
            if (activeSite == null) return false;
            plannedBuilding = activeSite.Type;
            currentBuildCell = activeSite.Origin;

            if (safety != null && IsActiveProjectUnsafe(activeSite, safety))
            {
                CancelSiteBuilders(context.Knowledge, roles, activeSite);
                builder = null;
                // The paused site is preserved. Economy policy runs later in the
                // same Commander tick and retreats the now-economy Pawn if needed.
                return true;
            }

            if (!activeSite.IsAlive)
            {
                ReleaseSiteBuilderRoles(context.Knowledge, roles, activeSite);
                builder = null;
                activeSite = null;
                ClearPlan();
                return false;
            }
            if (activeSite.State == BuildingState.Completed)
            {
                ReleaseSiteBuilderRoles(context.Knowledge, roles, activeSite);
                builder = null;
                activeSite = null;
                ClearPlan();
                return false;
            }

            if (activeSite.RecoveryState == ConstructionRecoveryState.AccessFailure
                && activeSite.ActiveBuilderCount == 0)
                return true;

            int assigned = SynchronizeSiteBuilders(context.Knowledge, roles, activeSite);
            if (assigned > 0)
            {
                if (context.Time >= nextBuilderAssignmentTime
                    && assigned < maximumConcurrentBuilders
                    && activeSite.FreeBuilderSlotCount > 0
                    && roles.EconomyCount > minimumEconomyWorkers)
                    TryAssignAdditionalBuilder(context, roles, null, null);
                return true;
            }

            ReleaseBuilder(roles);
            TryAssignAdditionalBuilder(context, roles, null, null);
            return true;
        }

        private void TryAssignAdditionalBuilder(CommanderDecisionContext context,
            CommanderPawnRoles roles, EconomicDemandModel demand,
            EconomyCommanderPolicy economy)
        {
            nextBuilderAssignmentTime = context.Time + builderAssignmentCooldown;
            if (activeSite == null || activeSite.FreeBuilderSlotCount <= 0
                || roles.EconomyCount <= minimumEconomyWorkers) return;
            UnitController replacement = ChooseBuilder(context, roles, demand, economy);
            if (replacement == null || !roles.TryAssign(replacement,
                CommanderPawnRole.Builder)) return;
            CommanderCommandResult result = context.Commands.TryResumeConstruction(
                replacement.GetComponent<PawnBuilder>(), activeSite);
            if (result == CommanderCommandResult.Accepted)
            {
                builder ??= replacement;
                SuccessfulCommandSerial++;
                LastSuccessfulCommandTime = context.Time;
            }
            else
            {
                nextBuilderAssignmentTime = context.Time + retrySeconds;
                roles.Release(replacement, CommanderPawnRole.Builder);
            }
        }

        private int SynchronizeSiteBuilders(FactionKnowledge knowledge,
            CommanderPawnRoles roles, BuildingSite site)
        {
            int count = 0;
            builder = null;
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                PawnBuilder job = unit != null ? unit.GetComponent<PawnBuilder>() : null;
                if (unit == null || !unit.IsAlive || job == null || !job.HasJob
                    || job.Target != site || !site.HasBuilderReservation(job)) continue;
                roles.TryAssign(unit, CommanderPawnRole.Builder);
                builder ??= unit;
                count++;
            }
            return count;
        }

        private static void CancelSiteBuilders(FactionKnowledge knowledge,
            CommanderPawnRoles roles, BuildingSite site)
        {
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                PawnBuilder job = unit != null ? unit.GetComponent<PawnBuilder>() : null;
                if (job == null || job.Target != site) continue;
                job.CancelJob();
                unit.GetComponent<UnitMovement>()?.Stop();
                roles.Release(unit, CommanderPawnRole.Builder);
            }
        }

        private static void ReleaseSiteBuilderRoles(FactionKnowledge knowledge,
            CommanderPawnRoles roles, BuildingSite site)
        {
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                PawnBuilder job = unit != null ? unit.GetComponent<PawnBuilder>() : null;
                if (job != null && job.Target == site) job.CancelJob();
                if (roles.GetRole(unit) == CommanderPawnRole.Builder
                    && (job == null || !job.HasJob))
                    roles.Release(unit, CommanderPawnRole.Builder);
            }
        }

        private static bool IsActiveProjectUnsafe(BuildingSite site,
            StrategicSafetyMap safety)
        {
            if (site == null || safety == null) return false;
            for (int y = 0; y < site.Footprint.y; y++)
            for (int x = 0; x < site.Footprint.x; x++)
                if (safety.RiskAt(site.Origin + new Vector2Int(x, y),
                    StrategicPathKind.Civilian) >= StrategicRisk.Dangerous)
                    return true;
            return false;
        }

        private static bool IsFootprintSafe(FactionKnowledge knowledge, BuildingData data,
            Vector2Int origin, StrategicSafetyMap safety)
        {
            if (safety == null) return true;
            GridManager grid = knowledge.TeamVision?.GridManager;
            if (grid == null) return false;
            for (int y = 0; y < data.Footprint.y; y++)
            for (int x = 0; x < data.Footprint.x; x++)
                if (safety.RiskAt(origin + new Vector2Int(x, y),
                    StrategicPathKind.Civilian) >= StrategicRisk.Dangerous)
                    return false;
            return true;
        }

        private UnitController ChooseBuilder(CommanderDecisionContext context,
            CommanderPawnRoles roles, EconomicDemandModel demand,
            EconomyCommanderPolicy economy)
        {
            if (roles == null || roles.EconomyCount <= minimumEconomyWorkers) return null;
            UnitController best = null;
            int bestIdleRank = int.MaxValue;
            int bestCargo = int.MaxValue;
            int bestDemandPenalty = int.MaxValue;
            float bestDistance = float.PositiveInfinity;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            Vector3 target = activeSite != null ? activeSite.transform.position
                : planningEconomicOutpost && grid != null
                    ? grid.GridToWorld(outpostClusterCell.x, outpostClusterCell.y)
                    : BaseWorldPosition(context.Knowledge);
            IReadOnlyList<UnitController> units = context.Knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit == null || !unit.IsAlive || !unit.IsWorldParticipant
                    || roles.GetRole(unit) != CommanderPawnRole.Economy
                    || unit.GetComponent<PawnBuilder>() == null) continue;
                PawnGatherer gatherer = unit.GetComponent<PawnGatherer>();
                UnitMovement movement = unit.GetComponent<UnitMovement>();
                int idleRank = unit.CurrentState == UnitState.Idle
                    && (gatherer == null || gatherer.State == WorkerState.Idle)
                    && (movement == null || !movement.IsMoving) ? 0 : 1;
                int cargo = gatherer != null ? gatherer.CarriedAmount : 0;
                int demandPenalty = economy != null
                    && economy.TryGetAssignedType(unit, out ResourceType type)
                    && demand != null ? demand.Deficit(type) : 0;
                float distance = ((Vector2)(unit.transform.position - target)).sqrMagnitude;
                if (best == null || idleRank < bestIdleRank
                    || (idleRank == bestIdleRank && cargo < bestCargo)
                    || (idleRank == bestIdleRank && cargo == bestCargo
                        && demandPenalty < bestDemandPenalty)
                    || (idleRank == bestIdleRank && cargo == bestCargo
                        && demandPenalty == bestDemandPenalty
                        && distance < bestDistance - 0.0001f)
                    || (idleRank == bestIdleRank && cargo == bestCargo
                        && demandPenalty == bestDemandPenalty
                        && Mathf.Abs(distance - bestDistance) <= 0.0001f
                        && unit.GetEntityId().CompareTo(best.GetEntityId()) < 0))
                {
                    best = unit;
                    bestIdleRank = idleRank;
                    bestCargo = cargo;
                    bestDemandPenalty = demandPenalty;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private BuildingType? ChooseNeed(FactionKnowledge knowledge,
            EconomicDemandModel demand)
        {
            FactionPopulation population = knowledge.Population;
            int free = demand != null ? demand.PopulationHeadroom : population != null
                ? population.Cap - population.Used - population.Reserved : 0;
            if (free <= freePopulationThreshold && !HasUnderConstruction(
                knowledge, BuildingType.House)) return BuildingType.House;

            int pawns = CountLivingPawns(knowledge);
            if (pawns >= barracksPawnThreshold && !HasBuilding(knowledge, BuildingType.Barracks))
                return BuildingType.Barracks;
            if (pawns >= archeryPawnThreshold && !HasBuilding(knowledge, BuildingType.Archery))
                return BuildingType.Archery;
            if (pawns >= monasteryPawnThreshold && !HasBuilding(knowledge, BuildingType.Monastery))
                return BuildingType.Monastery;
            if (productionInfrastructureNeed.HasValue
                && Time.time <= productionInfrastructureNeedUntil
                && !HasBuilding(knowledge, productionInfrastructureNeed.Value))
                return productionInfrastructureNeed.Value;
            return null;
        }

        private void PrepareCandidates(FactionKnowledge knowledge, BuildingData data)
        {
            candidates.Clear();
            candidateIndex = 0;
            if (!TryGetCastle(knowledge, out IDefensiveBuildingHost castle)) return;
            Vector2Int center = castle.FootprintOrigin
                + new Vector2Int(castle.Footprint.x / 2, castle.Footprint.y / 2);
            for (int radius = minimumPlacementRadius; radius <= maximumPlacementRadius; radius++)
            {
                for (int x = -radius; x <= radius; x++) AddCandidate(center, data, x, radius);
                for (int y = radius - 1; y >= -radius; y--) AddCandidate(center, data, radius, y);
                for (int x = radius - 1; x >= -radius; x--) AddCandidate(center, data, x, -radius);
                for (int y = -radius + 1; y < radius; y++) AddCandidate(center, data, -radius, y);
            }
        }

        private void PrepareOutpostCandidates(BuildingData data, Vector2Int center)
        {
            candidates.Clear();
            candidateIndex = 0;
            for (int radius = 2; radius <= outpostPlacementRadius; radius++)
            {
                for (int x = -radius; x <= radius; x++) AddCandidate(center, data, x, radius);
                for (int y = radius - 1; y >= -radius; y--) AddCandidate(center, data, radius, y);
                for (int x = radius - 1; x >= -radius; x--) AddCandidate(center, data, x, -radius);
                for (int y = -radius + 1; y < radius; y++) AddCandidate(center, data, -radius, y);
            }
        }

        private bool CanPlanEconomicOutpost(CommanderDecisionContext context,
            StrategicSafetyMap safety, EconomicDemandModel demand,
            out EconomicClusterSnapshot cluster)
        {
            cluster = default;
            if (clusterPlanner == null || !clusterPlanner.HasBest)
            {
                lastOutpostDecision = clusterPlanner?.LastRejectionReason
                    ?? "No cluster planner";
                return false;
            }
            if (context.Time - lastOutpostTime < economicOutpostCooldown)
            {
                lastOutpostDecision = "Rejected: outpost cooldown";
                return false;
            }
            if (HasUnderConstruction(context.Knowledge, BuildingType.Tower))
            {
                lastOutpostDecision = "Rejected: Tower project already pending";
                return false;
            }
            BuildingData tower = context.Commands.BuildingSystem?.GetData(BuildingType.Tower);
            bool canFund = demand != null && (planningEconomicOutpost
                && plannedBuilding == BuildingType.Tower
                    ? demand.CanExecuteReservedPlan()
                    : demand.CanSpendAfterBuffers(tower));
            if (tower == null || !canFund)
            {
                lastOutpostDecision = "Rejected: cannot afford beyond economic reserves";
                return false;
            }
            cluster = clusterPlanner.Best;
            if (safety != null && safety.RiskAt(cluster.CenterCell,
                StrategicPathKind.Civilian) >= StrategicRisk.Dangerous)
            {
                lastOutpostDecision = "Rejected: known danger at cluster";
                return false;
            }
            IReadOnlyList<IDefensiveBuildingHost> buildings = context.Knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost item = buildings[i];
                if (item == null || !item.IsAlive || item.Type != BuildingType.Tower) continue;
                if (Vector2.Distance(item.HostTransform.position,
                    cluster.CenterPosition) <= 9f)
                {
                    lastOutpostDecision = "Rejected: existing Tower/drop-off nearby";
                    return false;
                }
            }
            lastOutpostDecision = $"Candidate score {cluster.Score:0.0}";
            return true;
        }

        private void AddCandidate(Vector2Int center, BuildingData data, int x, int y)
            => candidates.Add(center + new Vector2Int(x - data.Footprint.x / 2,
                y - data.Footprint.y / 2));

        private static bool IsPolicyCandidate(FactionKnowledge knowledge,
            BuildingData data, Vector2Int origin)
        {
            GridManager grid = knowledge.TeamVision?.GridManager;
            if (grid == null) return false;
            int height = -1;
            for (int y = 0; y < data.Footprint.y; y++)
            for (int x = 0; x < data.Footprint.x; x++)
            {
                Vector2Int position = origin + new Vector2Int(x, y);
                GridCell cell = grid.GetCell(position.x, position.y);
                if (cell == null || !cell.Walkable || !knowledge.IsCellExplored(position)
                    || cell.TerrainType is not (TerrainType.FlatGround
                        or TerrainType.ElevatedGround)
                    || (height >= 0 && cell.HeightLevel != height)) return false;
                height = cell.HeightLevel;
            }
            if (TouchesOwnBuilding(knowledge, origin, data.Footprint, 1)) return false;
            if (TouchesKnownResource(knowledge, origin, data.Footprint, 1)) return false;
            if (TouchesOwnRally(knowledge, origin, data.Footprint, 2)) return false;
            return !BlocksCastleApproach(knowledge, origin, data.Footprint);
        }

        private static bool TouchesOwnBuilding(FactionKnowledge knowledge, Vector2Int origin,
            Vector2Int footprint, int gap)
        {
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost host = buildings[i];
                if (host == null || !host.IsAlive) continue;
                if (Overlaps(origin, footprint, host.FootprintOrigin, host.Footprint, gap))
                    return true;
            }
            return false;
        }

        private static bool TouchesKnownResource(FactionKnowledge knowledge, Vector2Int origin,
            Vector2Int footprint, int gap)
        {
            IReadOnlyList<ResourceKnowledgeSnapshot> resources = knowledge.KnownResources;
            for (int i = 0; i < resources.Count; i++)
            {
                Vector2Int cell = resources[i].LogicalCell;
                if (cell.x >= origin.x - gap && cell.x < origin.x + footprint.x + gap
                    && cell.y >= origin.y - gap && cell.y < origin.y + footprint.y + gap)
                    return true;
            }
            return false;
        }

        private static bool TouchesOwnRally(FactionKnowledge knowledge, Vector2Int origin,
            Vector2Int footprint, int gap)
        {
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost host = buildings[i];
                BuildingRallyPoint rally = host?.HostTransform != null
                    ? host.HostTransform.GetComponent<BuildingRallyPoint>() : null;
                if (rally == null || !rally.HasRallyPoint) continue;
                Vector2Int cell = rally.RallyCell;
                if (cell.x >= origin.x - gap && cell.x < origin.x + footprint.x + gap
                    && cell.y >= origin.y - gap && cell.y < origin.y + footprint.y + gap)
                    return true;
            }
            return false;
        }

        private static bool BlocksCastleApproach(FactionKnowledge knowledge, Vector2Int origin,
            Vector2Int footprint)
        {
            if (!TryGetCastle(knowledge, out IDefensiveBuildingHost castle)) return true;
            Vector2Int approachOrigin = castle.FootprintOrigin + new Vector2Int(-1, -3);
            Vector2Int approachSize = new(castle.Footprint.x + 2, 3);
            return Overlaps(origin, footprint, approachOrigin, approachSize, 0);
        }

        private static bool Overlaps(Vector2Int firstOrigin, Vector2Int firstSize,
            Vector2Int secondOrigin, Vector2Int secondSize, int gap)
            => firstOrigin.x < secondOrigin.x + secondSize.x + gap
                && firstOrigin.x + firstSize.x + gap > secondOrigin.x
                && firstOrigin.y < secondOrigin.y + secondSize.y + gap
                && firstOrigin.y + firstSize.y + gap > secondOrigin.y;

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

        private static Vector3 BaseWorldPosition(FactionKnowledge knowledge)
            => TryGetCastle(knowledge, out IDefensiveBuildingHost castle)
                ? castle.HostTransform.position : Vector3.zero;

        private static int CountLivingPawns(FactionKnowledge knowledge)
        {
            int count = 0;
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
                if (units[i] != null && units[i].IsAlive
                    && units[i].GetComponent<PawnGatherer>() != null) count++;
            return count;
        }

        private static bool HasBuilding(FactionKnowledge knowledge, BuildingType type)
        {
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i] != null && buildings[i].IsAlive
                    && buildings[i].Type == type) return true;
            return false;
        }

        private static bool HasUnderConstruction(FactionKnowledge knowledge, BuildingType type)
        {
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i] is BuildingSite site && site.IsAlive
                    && site.Type == type && site.State == BuildingState.UnderConstruction)
                    return true;
            return false;
        }

        private static BuildingSite FindExistingProject(FactionKnowledge knowledge)
        {
            BuildingSite best = null;
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] is not BuildingSite site || !site.IsAlive
                    || site.State != BuildingState.UnderConstruction) continue;
                if (best == null || site.Origin.y < best.Origin.y
                    || (site.Origin.y == best.Origin.y && site.Origin.x < best.Origin.x))
                    best = site;
            }
            return best;
        }

        private void RefreshInfrastructure(FactionKnowledge knowledge)
        {
            CompletedCoreInfrastructure = 0;
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i] != null && buildings[i].IsAlive
                    && buildings[i].IsProductionReady
                    && buildings[i].Type is BuildingType.Barracks
                        or BuildingType.Archery or BuildingType.Monastery)
                    CompletedCoreInfrastructure++;
            if (productionInfrastructureNeed.HasValue
                && (Time.time > productionInfrastructureNeedUntil
                    || HasBuilding(knowledge, productionInfrastructureNeed.Value)))
                productionInfrastructureNeed = null;
        }

        private void ReleaseBuilder(CommanderPawnRoles roles)
        {
            if (builder != null) roles?.Release(builder, CommanderPawnRole.Builder);
            builder = null;
        }

        private void ClearPlan()
        {
            plannedBuilding = null;
            planningEconomicOutpost = false;
            outpostClusterCell = new Vector2Int(-1, -1);
            candidates.Clear();
            candidateIndex = 0;
            currentBuildCell = new Vector2Int(-1, -1);
        }
    }
}
