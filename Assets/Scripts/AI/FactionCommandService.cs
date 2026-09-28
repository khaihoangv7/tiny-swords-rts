using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    // Shared gameplay authority for human, Rule-Based and future RL command sources.
    // Each method revalidates faction ownership and current legal observations.
    public sealed class FactionCommandService : MonoBehaviour
    {
        private static readonly ProfilerMarker CommandDispatchMarker =
            new("TinyKingdom.AI.CommandDispatch");
        [SerializeField] private Faction faction = Faction.Red;
        [SerializeField] private FactionKnowledge knowledge;
        [SerializeField] private BuildingSystem buildingSystem;
        private readonly List<PawnBuilder> singleBuilder = new(1);

        public Faction Faction => faction;
        public FactionKnowledge Knowledge => knowledge;
        public BuildingSystem BuildingSystem => buildingSystem;
        public bool SupportsMilitaryCommands => true;

        public void Configure(Faction owner, FactionKnowledge factionKnowledge)
            => Configure(owner, factionKnowledge, null);

        public void Configure(Faction owner, FactionKnowledge factionKnowledge,
            BuildingSystem factionBuildingSystem)
        {
            faction = owner;
            knowledge = factionKnowledge;
            buildingSystem = factionBuildingSystem;
        }

        public CommanderCommandResult TryMove(UnitController actor, Vector3 destination)
            => TryMove(actor, destination, null);

        public CommanderCommandResult TryMove(UnitController actor, Vector3 destination,
            IPathTraversalPolicy traversalPolicy)
        {
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.CommanderMove);
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            UnitMovement movement = actor.GetComponent<UnitMovement>();
            if (movement == null) return CommanderCommandResult.RejectedInvalidActor;
            bool moved = traversalPolicy == null
                ? movement.TryIssueMove(destination)
                : movement.TryIssueStrategicMove(destination, traversalPolicy);
            if (!moved)
                return CommanderCommandResult.RejectedUnreachable;
            actor.GetComponent<PawnGatherer>()?.CancelJob();
            return CommanderCommandResult.Accepted;
        }

        public CommanderCommandResult TryGather(UnitController actor, ResourceNode target)
            => TryGather(actor, target, null);

        public CommanderCommandResult TryGather(UnitController actor, ResourceNode target,
            StrategicSafetyMap safety)
        {
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.CommanderGather);
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            PawnGatherer gatherer = actor.GetComponent<PawnGatherer>();
            if (gatherer == null) return CommanderCommandResult.RejectedWrongUnitType;
            if (target == null) return CommanderCommandResult.RejectedInvalidTarget;
            if (knowledge == null || !knowledge.IsResourceVisible(target))
                return CommanderCommandResult.RejectedNotObserved;
            if (!target.IsAvailable) return CommanderCommandResult.RejectedInvalidTarget;
            if (safety != null
                && !safety.IsEligibleEconomicPosition(knowledge, target.transform.position))
                return CommanderCommandResult.RejectedUnsafe;
            return (safety == null
                ? gatherer.Gather(target)
                : gatherer.Gather(target,
                    candidate => candidate != null && safety.IsEligibleEconomicPosition(
                        knowledge, candidate.transform.position),
                    safety.CivilianTraversal))
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedUnreachable;
        }

        public CommanderCommandResult TryAttack(UnitController actor, EntityId targetId)
        {
            using var marker = CommandDispatchMarker.Auto();
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            HealthComponent health = actor.GetComponent<HealthComponent>();
            UnitType type = health?.CombatData != null
                ? health.CombatData.UnitType : UnitType.Pawn;
            if (type is not (UnitType.Warrior or UnitType.Archer or UnitType.Lancer))
                return CommanderCommandResult.RejectedWrongUnitType;
            if (knowledge == null
                || !knowledge.TryGetVisibleEnemy(targetId, out VisibleUnitObservation observed))
                return CommanderCommandResult.RejectedNotObserved;

            UnitController target = ResolveCurrentlyVisibleTarget(targetId, observed.Faction);
            if (target == null) return CommanderCommandResult.RejectedInvalidTarget;
            CombatController combat = actor.GetComponent<CombatController>();
            return combat != null && combat.Attack(target)
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedCommandFailed;
        }

        public CommanderCommandResult TryAttackBuilding(UnitController actor,
            EntityId targetId)
        {
            using var marker = CommandDispatchMarker.Auto();
            CommanderCommandResult actorResult = ValidateCombatActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            if (knowledge == null || !knowledge.TryGetVisibleEnemyBuilding(targetId,
                    out BuildingKnowledgeSnapshot observed))
                return CommanderCommandResult.RejectedNotObserved;
            BuildingCombatTarget target = ResolveCurrentlyVisibleBuilding(targetId,
                observed.Faction);
            if (target == null) return CommanderCommandResult.RejectedInvalidTarget;
            CombatController combat = actor.GetComponent<CombatController>();
            return combat != null && combat.Attack(target)
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedCommandFailed;
        }

        public CommanderCommandResult TryAttackMove(UnitController actor,
            Vector3 destination, IPathTraversalPolicy traversalPolicy = null)
        {
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.CommanderMove);
            CommanderCommandResult actorResult = ValidateCombatActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            GridManager grid = knowledge?.TeamVision?.GridManager;
            if (grid == null) return CommanderCommandResult.RejectedInvalidTarget;
            Vector2Int cell = grid.WorldToGrid(destination);
            GridCell navigation = grid.GetCell(cell.x, cell.y);
            if (navigation == null || !navigation.Walkable || navigation.Occupied)
                return CommanderCommandResult.RejectedInvalidTarget;
            CombatController combat = actor.GetComponent<CombatController>();
            return combat != null && combat.AttackMove(cell, destination, traversalPolicy)
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedUnreachable;
        }

        public CommanderCommandResult TryBuild(PawnBuilder builder, BuildingType type,
            Vector2Int origin, out BuildingSite site)
        {
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.CommanderBuild);
            site = null;
            UnitController actor = builder != null ? builder.GetComponent<UnitController>() : null;
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            if (builder == null || actor.GetComponent<PawnGatherer>() == null)
                return CommanderCommandResult.RejectedWrongUnitType;
            if (buildingSystem == null || buildingSystem.Owner != faction)
                return CommanderCommandResult.RejectedInvalidBuilding;
            BuildingData data = buildingSystem.GetData(type);
            if (data == null || type is BuildingType.Castle or BuildingType.GoldMine)
                return CommanderCommandResult.RejectedInvalidBuilding;
            if (!IsFootprintExplored(origin, data.Footprint))
                return CommanderCommandResult.RejectedNotObserved;
            if (!buildingSystem.CanAfford(type))
                return CommanderCommandResult.RejectedInsufficientResources;
            if (!buildingSystem.CanPlace(type, origin))
                return CommanderCommandResult.RejectedInvalidPlacement;
            singleBuilder.Clear();
            singleBuilder.Add(builder);
            return buildingSystem.TryBuild(type, origin, singleBuilder, out site)
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedUnreachable;
        }

        public CommanderCommandResult TryResumeConstruction(PawnBuilder builder,
            BuildingSite site)
        {
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.CommanderBuild);
            UnitController actor = builder != null ? builder.GetComponent<UnitController>() : null;
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            if (builder == null || actor.GetComponent<PawnGatherer>() == null)
                return CommanderCommandResult.RejectedWrongUnitType;
            if (site == null || !site.IsAlive || site.Faction != faction)
                return CommanderCommandResult.RejectedInvalidBuilding;
            if (site.State != BuildingState.UnderConstruction)
                return CommanderCommandResult.RejectedConstructionActive;
            singleBuilder.Clear();
            singleBuilder.Add(builder);
            return buildingSystem != null && buildingSystem.TryAssign(site, singleBuilder)
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedUnreachable;
        }

        public CommanderCommandResult TryTrain(UnitProductionQueue producer, UnitType type)
        {
            using var marker = CommandDispatchMarker.Auto();
            if (!IsPlaying()) return CommanderCommandResult.RejectedMatchNotPlaying;
            IProductionHost host = producer != null ? producer.ProductionHost : null;
            if (producer == null || host == null || !host.IsProductionReady)
                return CommanderCommandResult.RejectedInvalidBuilding;
            if (host.Faction != faction) return CommanderCommandResult.RejectedNotOwned;
            UnitCombatData data = producer.GetData(type);
            if (data == null || !UnitProductionQueue.CanProduce(host.Type, type))
                return CommanderCommandResult.RejectedInvalidTarget;
            if (producer.Count >= producer.MaxCount)
                return CommanderCommandResult.RejectedQueueFull;
            if (knowledge?.Stockpile == null || !knowledge.Stockpile.CanAfford(
                data.FoodCost, data.WoodCost, data.GoldCost))
                return CommanderCommandResult.RejectedInsufficientResources;
            FactionPopulation population = knowledge.Population;
            if (population == null || population.Used + population.Reserved
                + data.PopulationCost > population.Cap)
                return CommanderCommandResult.RejectedPopulationBlocked;
            return producer.TryTrain(type) ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedCommandFailed;
        }

        public CommanderCommandResult TrySetRally(BuildingRallyPoint rally,
            Vector3 destination)
        {
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.CommanderRally);
            if (!IsPlaying()) return CommanderCommandResult.RejectedMatchNotPlaying;
            if (rally == null || rally.ProductionHost == null)
                return CommanderCommandResult.RejectedInvalidBuilding;
            if (rally.ProductionHost.Faction != faction)
                return CommanderCommandResult.RejectedNotOwned;
            var grid = knowledge?.TeamVision?.GridManager;
            if (grid == null || !knowledge.IsCellExplored(grid.WorldToGrid(destination)))
                return CommanderCommandResult.RejectedNotObserved;
            return rally.TrySetRallyPoint(faction, destination)
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedInvalidPlacement;
        }

        private CommanderCommandResult ValidateActor(UnitController actor)
        {
            if (!IsPlaying())
                return CommanderCommandResult.RejectedMatchNotPlaying;
            if (actor == null || !actor.IsAlive || !actor.IsWorldParticipant)
                return CommanderCommandResult.RejectedInvalidActor;
            if (actor.Faction != faction) return CommanderCommandResult.RejectedNotOwned;
            return CommanderCommandResult.Accepted;
        }

        private CommanderCommandResult ValidateCombatActor(UnitController actor)
        {
            CommanderCommandResult result = ValidateActor(actor);
            if (result != CommanderCommandResult.Accepted) return result;
            UnitCombatData data = actor.GetComponent<HealthComponent>()?.CombatData;
            return data != null && data.UnitType is UnitType.Warrior or UnitType.Archer
                or UnitType.Lancer
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedWrongUnitType;
        }

        private UnitController ResolveCurrentlyVisibleTarget(EntityId targetId,
            Faction observedFaction)
        {
            IReadOnlyList<UnitController> units = UnitController.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController target = units[i];
                if (target == null || !target.IsTargetable
                    || target.Faction != observedFaction
                    || !FactionRelations.AreHostile(faction, target.Faction)
                    || target.GetEntityId().CompareTo(targetId) != 0) continue;
                return knowledge.TeamVision != null
                    && knowledge.TeamVision.IsVisible(faction, target.transform.position)
                    ? target : null;
            }
            return null;
        }

        private BuildingCombatTarget ResolveCurrentlyVisibleBuilding(EntityId targetId,
            Faction observedFaction)
        {
            IReadOnlyList<IVisionSource> sources = VisionSourceRegistry.ActiveSources;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] is not BuildingVisionSource source || source == null
                    || source.Host is not Component component
                    || source.Host == null || !source.Host.IsAlive
                    || source.Host.Faction != observedFaction
                    || !FactionRelations.AreHostile(faction, observedFaction)
                    || component.GetEntityId().CompareTo(targetId) != 0) continue;
                if (knowledge.TeamVision == null || !knowledge.TeamVision.IsVisible(
                    faction, source.Host.HostTransform.position)) return null;
                BuildingCombatTarget target = component.GetComponent<BuildingCombatTarget>();
                return target != null && target.IsTargetable ? target : null;
            }
            return null;
        }

        private bool IsFootprintExplored(Vector2Int origin, Vector2Int footprint)
        {
            if (knowledge == null) return false;
            for (int y = 0; y < footprint.y; y++)
            for (int x = 0; x < footprint.x; x++)
                if (!knowledge.IsCellExplored(origin + new Vector2Int(x, y))) return false;
            return true;
        }

        private static bool IsPlaying()
            => MatchManager.Instance != null
                && MatchManager.Instance.State == MatchState.Playing;
    }
}
