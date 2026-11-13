using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.AI
{
    // Schema 1. Numeric IDs are a serialized contract for future PPO models.
    public enum RlAction
    {
        Wait = 0,
        TrainPawn = 1, TrainWarrior = 2, TrainArcher = 3,
        TrainLancer = 4, TrainMonk = 5,
        BuildHouse = 6, BuildBarracks = 7, BuildArchery = 8,
        BuildTower = 9, BuildMonastery = 10,
        PrioritizeFood = 11, PrioritizeWood = 12, PrioritizeGold = 13,
        Scout = 14, Attack = 15, Defend = 16,
        Retreat = 17, SecureGoldMine = 18
    }

    public enum RlActionResult
    {
        Accepted, InvalidId, InvalidContext, MatchStopped,
        MissingProducer, InsufficientResources, PopulationBlocked,
        MissingWorkers, MissingTarget, PolicyRejected
    }

    // Validates only own state and faction knowledge. The existing policies
    // remain responsible for placement, production and individual commands.
    public sealed class RlActionRouter
    {
        public const int ActionCount = 19;

        public RlActionResult TryPrepare(int id, RuleBasedCommander commander,
            MatchManager match, out StrategicIntent? intent)
        {
            intent = null;
            RlActionResult eligibility = CanChoose(id, commander, match);
            if (eligibility != RlActionResult.Accepted) return eligibility;
            RlAction action = (RlAction)id;
            if (action is >= RlAction.TrainPawn and <= RlAction.TrainMonk)
            {
                UnitType type = TrainingType(action);
                commander.ProductionPolicy.RequestExternalUnit(type, Time.time);
                return RlActionResult.Accepted;
            }
            if (action is >= RlAction.BuildHouse and <= RlAction.BuildMonastery)
                return commander.BuildingPolicy.RequestExternalBuilding(
                    BuildingFor(action), Time.time)
                    ? RlActionResult.Accepted : RlActionResult.PolicyRejected;
            if (action is >= RlAction.PrioritizeFood and <= RlAction.PrioritizeGold)
            {
                commander.EconomicDemand.SetExternalPriority(ResourceFor(action),
                    Time.time);
                return RlActionResult.Accepted;
            }
            intent = action switch
            {
                RlAction.Wait => commander.StrategicIntent.CurrentIntent,
                RlAction.Scout => StrategicIntent.Scout,
                RlAction.Attack => StrategicIntent.Attack,
                RlAction.Defend => StrategicIntent.Fortify,
                RlAction.Retreat => StrategicIntent.Recover,
                RlAction.SecureGoldMine => StrategicIntent.SecureGoldMine,
                _ => null
            };
            return RlActionResult.Accepted;
        }

        // Pure eligibility check shared by the router and ML-Agents mask.
        // It only reads the faction's own state and legally acquired knowledge.
        public RlActionResult CanChoose(int id, RuleBasedCommander commander,
            MatchManager match)
        {
            if (id < 0 || id >= ActionCount) return RlActionResult.InvalidId;
            if (commander == null || !commander.IsConfigured
                || commander.Knowledge == null || !commander.Knowledge.IsReady
                || commander.CommandService == null)
                return RlActionResult.InvalidContext;
            if (match == null || match.State != MatchState.Playing)
                return RlActionResult.MatchStopped;

            FactionKnowledge knowledge = commander.Knowledge;
            RlAction action = (RlAction)id;
            if (action is >= RlAction.TrainPawn and <= RlAction.TrainMonk)
            {
                UnitType type = TrainingType(action);
                bool hasProducer = false;
                bool populationBlocked = false;
                bool insufficientResources = false;
                var queues = UnitProductionQueue.ActiveQueues;
                for (int i = 0; i < queues.Count; i++)
                {
                    UnitProductionQueue queue = queues[i];
                    if (queue == null || queue.ProductionHost == null
                        || queue.ProductionHost.Faction != commander.Faction
                        || !UnitProductionQueue.CanProduce(queue.ProductionHost.Type,
                            type)) continue;
                    hasProducer = true;
                    UnitCombatData data = queue.GetData(type);
                    if (data == null) continue;
                    if (knowledge.Population.Used + knowledge.Population.Reserved
                        + data.PopulationCost > knowledge.Population.Cap)
                    {
                        populationBlocked = true;
                        continue;
                    }
                    if (!knowledge.Stockpile.CanAfford(data.FoodCost,
                        data.WoodCost, data.GoldCost))
                    {
                        insufficientResources = true;
                        continue;
                    }
                    if (!queue.CanTrain(type)) continue;
                    return RlActionResult.Accepted;
                }
                if (!hasProducer) return RlActionResult.MissingProducer;
                // Stable precedence when every compatible queue fails.
                if (populationBlocked) return RlActionResult.PopulationBlocked;
                if (insufficientResources) return RlActionResult.InsufficientResources;
                return RlActionResult.PolicyRejected;
            }

            if (action is >= RlAction.BuildHouse and <= RlAction.BuildMonastery)
            {
                BuildingType type = BuildingFor(action);
                BuildingData data = commander.CommandService.BuildingSystem?.GetData(type);
                if (data == null) return RlActionResult.InvalidContext;
                if (!knowledge.Stockpile.CanAfford(data.FoodCost,
                    data.WoodCost, data.GoldCost))
                    return RlActionResult.InsufficientResources;
                bool worker = false;
                for (int i = 0; i < knowledge.OwnUnits.Count; i++)
                {
                    UnitController unit = knowledge.OwnUnits[i];
                    HealthComponent health = unit != null
                        ? unit.GetComponent<HealthComponent>() : null;
                    if (health?.CombatData?.UnitType == UnitType.Pawn)
                    { worker = true; break; }
                }
                if (!worker) return RlActionResult.MissingWorkers;
                if (commander.BuildingPolicy.ActiveSite != null)
                    return RlActionResult.PolicyRejected;
                return RlActionResult.Accepted;
            }

            if (action is >= RlAction.PrioritizeFood and <= RlAction.PrioritizeGold)
                return RlActionResult.Accepted;

            switch (action)
            {
                case RlAction.Wait:
                case RlAction.Scout:
                case RlAction.Defend:
                case RlAction.Retreat:
                    return RlActionResult.Accepted;
                case RlAction.Attack:
                    return knowledge.KnownEnemyBuildings.Count > 0
                        && commander.StrategicIntent.HasStrategicTarget
                        ? RlActionResult.Accepted : RlActionResult.MissingTarget;
                case RlAction.SecureGoldMine:
                    return knowledge.KnownGoldMines.Count > 0
                        && commander.StrategicIntent.GoldMine.Current.CanCommit
                        ? RlActionResult.Accepted : RlActionResult.MissingTarget;
                default:
                    return RlActionResult.InvalidId;
            }
        }

        private static UnitType TrainingType(RlAction action) => action switch
        {
            RlAction.TrainPawn => UnitType.Pawn,
            RlAction.TrainWarrior => UnitType.Warrior,
            RlAction.TrainArcher => UnitType.Archer,
            RlAction.TrainLancer => UnitType.Lancer,
            _ => UnitType.Monk
        };

        private static BuildingType BuildingFor(RlAction action) => action switch
        {
            RlAction.BuildHouse => BuildingType.House,
            RlAction.BuildBarracks => BuildingType.Barracks,
            RlAction.BuildArchery => BuildingType.Archery,
            RlAction.BuildTower => BuildingType.Tower,
            _ => BuildingType.Monastery
        };

        private static ResourceType ResourceFor(RlAction action) => action switch
        {
            RlAction.PrioritizeFood => ResourceType.Food,
            RlAction.PrioritizeWood => ResourceType.Wood,
            _ => ResourceType.Gold
        };
    }
}
