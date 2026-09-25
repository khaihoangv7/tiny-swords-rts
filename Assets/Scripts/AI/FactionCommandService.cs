using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    // Shared gameplay authority for human/Rule-Based/future RL command sources.
    // Phase 7A exposes only the Move and Gather actions needed by its policies.
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
        {
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.CommanderMove);
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            UnitMovement movement = actor.GetComponent<UnitMovement>();
            if (movement == null) return CommanderCommandResult.RejectedInvalidActor;
            if (!movement.TryIssueMove(destination))
                return CommanderCommandResult.RejectedUnreachable;
            actor.GetComponent<PawnGatherer>()?.CancelJob();
            return CommanderCommandResult.Accepted;
        }

        public CommanderCommandResult TryGather(UnitController actor, ResourceNode target)
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
            return gatherer.Gather(target)
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
