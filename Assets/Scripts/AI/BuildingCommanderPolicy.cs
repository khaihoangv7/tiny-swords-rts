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

        private readonly List<Vector2Int> candidates = new();
        private BuildingSite activeSite;
        private UnitController builder;
        private BuildingType? plannedBuilding;
        private BuildingType candidateType;
        private int candidateIndex;
        private float nextAttemptTime;
        private Vector2Int currentBuildCell = new(-1, -1);

        public int FreePopulationThreshold => freePopulationThreshold;
        public int MinimumEconomyWorkers => minimumEconomyWorkers;
        public BuildingType? PlannedBuilding => plannedBuilding;
        public BuildingSite ActiveSite => activeSite;
        public UnitController CurrentBuilder => builder;
        public Vector2Int CurrentBuildCell => currentBuildCell;
        public int CompletedCoreInfrastructure { get; private set; }

        public void Reset(CommanderPawnRoles roles = null)
        {
            if (builder != null) roles?.Release(builder, CommanderPawnRole.Builder);
            activeSite = null;
            builder = null;
            plannedBuilding = null;
            candidates.Clear();
            candidateIndex = 0;
            nextAttemptTime = 0f;
            currentBuildCell = new Vector2Int(-1, -1);
            CompletedCoreInfrastructure = 0;
        }

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles)
        {
            RefreshInfrastructure(context.Knowledge);
            if (MaintainActiveProject(context, roles)) return;

            BuildingType? need = ChooseNeed(context.Knowledge);
            if (!need.HasValue)
            {
                ClearPlan();
                return;
            }
            if (plannedBuilding != need)
            {
                plannedBuilding = need;
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

            UnitController chosen = ChooseBuilder(context, roles);
            if (chosen == null) return;
            PawnBuilder pawnBuilder = chosen.GetComponent<PawnBuilder>();
            using (PlacementSearchMarker.Auto())
            {
                if (candidates.Count == 0) PrepareCandidates(context.Knowledge, data);

                int attempts = 0;
                while (candidateIndex < candidates.Count && attempts < attemptsPerThink)
                {
                    Vector2Int origin = candidates[candidateIndex++];
                    if (!IsPolicyCandidate(context.Knowledge, data, origin)) continue;
                    attempts++;
                    roles.TryAssign(chosen, CommanderPawnRole.Builder);
                    CommanderCommandResult result = context.Commands.TryBuild(
                        pawnBuilder, need.Value, origin, out BuildingSite site);
                    if (result == CommanderCommandResult.Accepted)
                    {
                        activeSite = site;
                        builder = chosen;
                        currentBuildCell = origin;
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
            CommanderPawnRoles roles)
        {
            if (activeSite == null) activeSite = FindExistingProject(context.Knowledge);
            if (activeSite == null) return false;
            plannedBuilding = activeSite.Type;
            currentBuildCell = activeSite.Origin;

            if (!activeSite.IsAlive)
            {
                ReleaseBuilder(roles);
                activeSite = null;
                ClearPlan();
                return false;
            }
            if (activeSite.State == BuildingState.Completed)
            {
                ReleaseBuilder(roles);
                activeSite = null;
                ClearPlan();
                return false;
            }

            PawnBuilder job = builder != null ? builder.GetComponent<PawnBuilder>() : null;
            if (builder != null && builder.IsAlive && job != null
                && job.HasJob && job.Target == activeSite)
            {
                roles.TryAssign(builder, CommanderPawnRole.Builder);
                return true;
            }

            ReleaseBuilder(roles);
            UnitController replacement = ChooseBuilder(context, roles);
            if (replacement == null) return true;
            roles.TryAssign(replacement, CommanderPawnRole.Builder);
            CommanderCommandResult result = context.Commands.TryResumeConstruction(
                replacement.GetComponent<PawnBuilder>(), activeSite);
            if (result == CommanderCommandResult.Accepted) builder = replacement;
            else roles.Release(replacement, CommanderPawnRole.Builder);
            return true;
        }

        private UnitController ChooseBuilder(CommanderDecisionContext context,
            CommanderPawnRoles roles)
        {
            if (roles == null || roles.EconomyCount <= minimumEconomyWorkers) return null;
            UnitController best = null;
            int bestIdleRank = int.MaxValue;
            float bestDistance = float.PositiveInfinity;
            Vector3 target = activeSite != null ? activeSite.transform.position
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
                float distance = ((Vector2)(unit.transform.position - target)).sqrMagnitude;
                if (best == null || idleRank < bestIdleRank
                    || (idleRank == bestIdleRank && distance < bestDistance - 0.0001f)
                    || (idleRank == bestIdleRank
                        && Mathf.Abs(distance - bestDistance) <= 0.0001f
                        && unit.GetEntityId().CompareTo(best.GetEntityId()) < 0))
                {
                    best = unit;
                    bestIdleRank = idleRank;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private BuildingType? ChooseNeed(FactionKnowledge knowledge)
        {
            FactionPopulation population = knowledge.Population;
            int free = population != null
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
        }

        private void ReleaseBuilder(CommanderPawnRoles roles)
        {
            if (builder != null) roles?.Release(builder, CommanderPawnRole.Builder);
            builder = null;
        }

        private void ClearPlan()
        {
            plannedBuilding = null;
            candidates.Clear();
            candidateIndex = 0;
            currentBuildCell = new Vector2Int(-1, -1);
        }
    }
}
