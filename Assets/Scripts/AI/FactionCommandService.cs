using System;
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
    public enum CommanderCommandCategory
    {
        Move,
        ScoutMove,
        Gather,
        ReturnResource,
        Build,
        Attack,
        Train,
        Rally,
        EnterGoldMine
    }

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
        private static readonly long[] CommandAttempts = new long[
            System.Enum.GetValues(typeof(CommanderCommandCategory)).Length];
        public static long DeduplicatedConstructionCommandCount { get; private set; }

        public Faction Faction => faction;
        public FactionKnowledge Knowledge => knowledge;
        public BuildingSystem BuildingSystem => buildingSystem;
        public bool SupportsMilitaryCommands => true;
        public bool SupportsRetreatCommands => true;
        public bool SupportsScoutPromotion => true;

        public static long GetCommandAttemptCount(CommanderCommandCategory category)
            => CommandAttempts[(int)category];

        public static void ResetPerformanceCounters()
        {
            System.Array.Clear(CommandAttempts, 0, CommandAttempts.Length);
            DeduplicatedConstructionCommandCount = 0;
        }

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
            => TryMoveInternal(actor, destination, traversalPolicy,
                CommanderCommandCategory.Move, PathfindingRequestSource.CommanderMove);

        public CommanderCommandResult TryScoutMove(UnitController actor,
            Vector3 destination, IPathTraversalPolicy traversalPolicy = null)
            => TryMoveInternal(actor, destination, traversalPolicy,
                CommanderCommandCategory.ScoutMove,
                PathfindingRequestSource.CommanderScout);

        public CommanderCommandResult TryMilitaryMove(UnitController actor,
            Vector3 destination, IPathTraversalPolicy traversalPolicy = null,
            bool regroup = false)
            => TryMoveInternal(actor, destination, traversalPolicy,
                CommanderCommandCategory.Move, regroup
                    ? PathfindingRequestSource.MilitaryRegroup
                    : PathfindingRequestSource.MilitaryFormation);

        public CommanderCommandResult TryMilitaryDirectMove(UnitController actor,
            Vector3 destination, IPathTraversalPolicy traversalPolicy = null)
            => TryMoveInternal(actor, destination, traversalPolicy,
                CommanderCommandCategory.Move, PathfindingRequestSource.MilitaryMove);

        public CommanderCommandResult TryResourceFallbackMove(UnitController actor,
            Vector3 destination, IPathTraversalPolicy traversalPolicy = null)
            => TryMoveInternal(actor, destination, traversalPolicy,
                CommanderCommandCategory.Move,
                PathfindingRequestSource.ResourceFallback);

        private CommanderCommandResult TryMoveInternal(UnitController actor,
            Vector3 destination, IPathTraversalPolicy traversalPolicy,
            CommanderCommandCategory category, PathfindingRequestSource pathSource)
        {
            Count(category);
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(pathSource);
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

        public CommanderCommandResult TryRetreatMove(UnitController actor,
            Vector3 destination, IPathTraversalPolicy traversalPolicy = null)
        {
            Count(CommanderCommandCategory.Move);
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.MilitaryRegroup);
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            UnitMovement movement = actor.GetComponent<UnitMovement>();
            CombatController combat = actor.GetComponent<CombatController>();
            if (movement == null || combat == null)
                return CommanderCommandResult.RejectedInvalidActor;
            combat.BeginRetreat();
            bool moved = traversalPolicy == null
                ? movement.TryIssueMove(destination)
                : movement.TryIssueStrategicMove(destination, traversalPolicy);
            if (!moved) return CommanderCommandResult.RejectedUnreachable;
            combat.ConfirmRetreatMovement();
            actor.GetComponent<PawnGatherer>()?.CancelJob();
            return CommanderCommandResult.Accepted;
        }

        public CommanderCommandResult TryGather(UnitController actor, ResourceNode target)
            => TryGather(actor, target, null);

        public CommanderCommandResult TryGather(UnitController actor, ResourceNode target,
            StrategicSafetyMap safety)
            => TryGather(actor, target, safety, null);

        internal CommanderCommandResult TryGather(UnitController actor, ResourceNode target,
            StrategicSafetyMap safety, Predicate<Vector3> extendedFoodArea)
        {
            Count(CommanderCommandCategory.Gather);
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
            Predicate<Vector3> eligible = null;
            if (safety != null)
            {
                bool extendFood = target.ResourceType == ResourceType.Food && extendedFoodArea != null;
                eligible = position => safety.IsEligibleEconomicPosition(knowledge, position)
                    || (extendFood && extendedFoodArea(position)
                        && safety.IsExploredSafeEconomicPosition(knowledge, position));
            }
            if (eligible != null && !eligible(target.transform.position))
                return CommanderCommandResult.RejectedUnsafe;
            // The Commander already searches alternate known nodes and applies a
            // retry cooldown. A nested gatherer search can issue many A* probes
            // in this same decision tick. Keep normal in-job depletion retargeting.
            return (safety == null
                ? gatherer.Gather(target, null, null, null, false)
                : gatherer.Gather(target,
                    candidate => candidate != null && eligible(candidate.transform.position),
                    eligible,
                    safety.CivilianTraversal, false))
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedUnreachable;
        }

        public CommanderCommandResult TryReturnResources(UnitController actor,
            StrategicSafetyMap safety = null)
        {
            Count(CommanderCommandCategory.ReturnResource);
            using var marker = CommandDispatchMarker.Auto();
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            PawnGatherer gatherer = actor.GetComponent<PawnGatherer>();
            if (gatherer == null) return CommanderCommandResult.RejectedWrongUnitType;
            if (gatherer.CarriedAmount <= 0)
                return CommanderCommandResult.RejectedInvalidTarget;
            bool recovered = safety == null
                ? gatherer.TryRecoverCargoReturn(null, null, null)
                : gatherer.TryRecoverCargoReturn(
                    candidate => candidate != null && safety.IsEligibleEconomicPosition(
                        knowledge, candidate.transform.position),
                    position => safety.IsEligibleEconomicPosition(knowledge, position),
                    safety.CivilianTraversal);
            return recovered ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedUnreachable;
        }

        public CommanderCommandResult TryEnterGoldMine(UnitController actor,
            EntityId mineId, StrategicSafetyMap safety = null)
        {
            Count(CommanderCommandCategory.EnterGoldMine);
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.CommanderGather);
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            if (actor.GetComponent<PawnGatherer>() == null)
                return CommanderCommandResult.RejectedWrongUnitType;
            if (knowledge == null) return CommanderCommandResult.RejectedNotObserved;
            bool observed = false;
            IReadOnlyList<GoldMineKnowledgeSnapshot> visible = knowledge.VisibleGoldMines;
            for (int i = 0; i < visible.Count; i++)
                if (visible[i].RuntimeId.CompareTo(mineId) == 0)
                { observed = true; break; }
            if (!observed) return CommanderCommandResult.RejectedNotObserved;

            IReadOnlyList<GoldMineObjective> mines = GoldMineObjective.Active;
            for (int i = 0; i < mines.Count; i++)
            {
                GoldMineObjective mine = mines[i];
                if (mine == null || mine.GetEntityId().CompareTo(mineId) != 0)
                    continue;
                if (!mine.CanAccept(actor))
                    return CommanderCommandResult.RejectedInvalidTarget;
                if (safety != null && safety.RiskAt(mine.transform.position,
                        StrategicPathKind.Civilian) >= StrategicRisk.Dangerous)
                    return CommanderCommandResult.RejectedUnsafe;
                return mine.TryEnter(actor, safety?.CivilianTraversal)
                    ? CommanderCommandResult.Accepted
                    : CommanderCommandResult.RejectedUnreachable;
            }
            return CommanderCommandResult.RejectedInvalidTarget;
        }

        public CommanderCommandResult TryPromoteScout(UnitController actor)
        {
            using var marker = CommandDispatchMarker.Auto();
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;

            PawnScoutEligibility eligibility = PawnScoutPromotion.EvaluateForFaction(
                actor, faction, knowledge?.Stockpile);
            CommanderCommandResult eligibilityResult = MapScoutEligibility(eligibility);
            if (eligibilityResult != CommanderCommandResult.Accepted)
                return eligibilityResult;

            if (!PawnScoutPromotion.TryPromoteForFaction(actor, faction,
                    knowledge.Stockpile))
                return CommanderCommandResult.RejectedCommandFailed;
            // Promotion may deliberately select a working Pawn when scouting
            // has been blocked for a long time. Release its old economy route
            // only after the authoritative promotion succeeds.
            actor.GetComponent<PawnGatherer>()?.CancelJob();
            actor.GetComponent<UnitMovement>()?.Stop();
            return CommanderCommandResult.Accepted;
        }

        public CommanderCommandResult TryAttack(UnitController actor, EntityId targetId)
        {
            Count(CommanderCommandCategory.Attack);
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
            EntityId targetId, bool searchFullPerimeter = false)
        {
            Count(CommanderCommandCategory.Attack);
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
            return combat != null && combat.Attack(target, searchFullPerimeter)
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedCommandFailed;
        }

        internal bool CanReachVisibleBuildingToAttack(UnitController actor,
            BuildingKnowledgeSnapshot observed)
        {
            BuildingCombatTarget target = ResolveCurrentlyVisibleBuilding(observed.RuntimeId,
                observed.Faction);
            GridManager grid = knowledge?.TeamVision?.GridManager;
            CombatController combat = actor.GetComponent<CombatController>();
            return target != null && combat != null && grid != null
                && (target.DistanceFrom(actor.transform.position) <= combat.EffectiveAttackRange
                    || target.TryFindAttackPosition(actor, combat.EffectiveAttackRange,
                        grid, out _, searchFullPerimeter: true));
        }

        public CommanderCommandResult TryAttackMove(UnitController actor,
            Vector3 destination, IPathTraversalPolicy traversalPolicy = null)
        {
            Count(CommanderCommandCategory.Attack);
            using var marker = CommandDispatchMarker.Auto();
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.MilitaryCombat);
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
            Count(CommanderCommandCategory.Build);
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
            if (!buildingSystem.TryBuild(type, origin, singleBuilder, out site))
                return CommanderCommandResult.RejectedUnreachable;
            site.RecordBuilderCommand(false);
            return CommanderCommandResult.Accepted;
        }

        public CommanderCommandResult TryResumeConstruction(PawnBuilder builder,
            BuildingSite site)
        {
            Count(CommanderCommandCategory.Build);
            using var marker = CommandDispatchMarker.Auto();
            UnitController actor = builder != null ? builder.GetComponent<UnitController>() : null;
            CommanderCommandResult actorResult = ValidateActor(actor);
            if (actorResult != CommanderCommandResult.Accepted) return actorResult;
            if (builder == null || actor.GetComponent<PawnGatherer>() == null)
                return CommanderCommandResult.RejectedWrongUnitType;
            if (site == null || !site.IsAlive || site.Faction != faction)
                return CommanderCommandResult.RejectedInvalidBuilding;
            if (site.State != BuildingState.UnderConstruction)
                return CommanderCommandResult.RejectedConstructionActive;
            if (builder.HasJob && builder.Target == site && site.IsAssigned(builder)
                && site.HasBuilderReservation(builder))
            {
                site.RecordBuilderCommand(true);
                DeduplicatedConstructionCommandCount++;
                return CommanderCommandResult.Accepted;
            }
            site.RecordBuilderCommand(false);
            using var source = PathfindingSystem.TrackCommanderRequest(
                PathfindingRequestSource.CommanderBuild);
            singleBuilder.Clear();
            singleBuilder.Add(builder);
            return buildingSystem != null && buildingSystem.TryAssign(site, singleBuilder)
                ? CommanderCommandResult.Accepted
                : CommanderCommandResult.RejectedUnreachable;
        }

        public CommanderCommandResult TryTrain(UnitProductionQueue producer, UnitType type)
        {
            Count(CommanderCommandCategory.Train);
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
            Count(CommanderCommandCategory.Rally);
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

        private static CommanderCommandResult MapScoutEligibility(
            PawnScoutEligibility eligibility)
            => eligibility switch
            {
                PawnScoutEligibility.Eligible => CommanderCommandResult.Accepted,
                PawnScoutEligibility.InvalidPawn => CommanderCommandResult.RejectedWrongUnitType,
                PawnScoutEligibility.WrongFaction => CommanderCommandResult.RejectedNotOwned,
                PawnScoutEligibility.MatchNotPlaying =>
                    CommanderCommandResult.RejectedMatchNotPlaying,
                PawnScoutEligibility.InsufficientWood =>
                    CommanderCommandResult.RejectedInsufficientResources,
                PawnScoutEligibility.MissingPresentation =>
                    CommanderCommandResult.RejectedInvalidTarget,
                _ => CommanderCommandResult.RejectedCommandFailed
            };

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
                if (!knowledge.CanObserveBuilding(source.Host)) return null;
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

        private static void Count(CommanderCommandCategory category)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            CommandAttempts[(int)category]++;
#endif
        }
    }
}
