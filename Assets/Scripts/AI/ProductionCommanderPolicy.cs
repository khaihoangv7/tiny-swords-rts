using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.AI
{
    [Serializable]
    public sealed class ProductionCommanderPolicy
    {
        private static readonly UnitType[] CompositionSequence =
        {
            UnitType.Warrior, UnitType.Archer, UnitType.Warrior, UnitType.Lancer,
            UnitType.Archer, UnitType.Warrior, UnitType.Monk, UnitType.Lancer
        };

        [SerializeField, Min(1)] private int targetPawnCount = 6;
        [SerializeField, Min(1)] private int targetMilitaryPopulation = 8;
        [SerializeField, Range(1, 2)] private int desiredQueueDepth = 1;
        [SerializeField, Range(0, 2)] private int monkLimit = 1;
        [SerializeField, Min(0.5f)] private float rejectedCommandRetrySeconds = 2f;

        private readonly List<UnitProductionQueue> producers = new();
        private readonly List<UnitType> queueBuffer = new();
        private readonly int[] livingCounts = new int[7];
        private readonly int[] queuedCounts = new int[7];
        private readonly int[] requiredPrefix = new int[7];
        private float nextProductionAttemptTime;
        private float nextRallyAttemptTime;
        private bool hasStagingCell;
        private Vector2Int stagingCell = new(-1, -1);

        public int TargetPawnCount => targetPawnCount;
        public int TargetMilitaryPopulation => targetMilitaryPopulation;
        public int DesiredQueueDepth => desiredQueueDepth;
        public int MonkLimit => monkLimit;
        public int LivingPawnCount { get; private set; }
        public int QueuedPawnCount => queuedCounts[(int)UnitType.Pawn];
        public int MilitaryPopulation { get; private set; }
        public int QueuedMilitaryPopulation { get; private set; }
        public int EffectiveFreePopulation { get; private set; }
        public bool PopulationBlocked { get; private set; }
        public UnitType? ActiveProductionDecision { get; private set; }
        public UnitType? PendingEconomicDemand { get; private set; }
        public Vector2Int RallyStagingCell => stagingCell;
        public bool HasRallyStagingCell => hasStagingCell;
        public uint SuccessfulTrainSerial { get; private set; }
        public float LastSuccessfulTrainTime { get; private set; } = -1f;

        public int LivingCount(UnitType type) => livingCounts[(int)type];
        public int QueuedCount(UnitType type) => queuedCounts[(int)type];

        public void Reset()
        {
            producers.Clear();
            queueBuffer.Clear();
            Array.Clear(livingCounts, 0, livingCounts.Length);
            Array.Clear(queuedCounts, 0, queuedCounts.Length);
            nextProductionAttemptTime = 0f;
            nextRallyAttemptTime = 0f;
            hasStagingCell = false;
            stagingCell = new Vector2Int(-1, -1);
            LivingPawnCount = MilitaryPopulation = QueuedMilitaryPopulation = 0;
            EffectiveFreePopulation = 0;
            PopulationBlocked = false;
            ActiveProductionDecision = null;
            PendingEconomicDemand = null;
            SuccessfulTrainSerial = 0;
            LastSuccessfulTrainTime = -1f;
        }

        public void Tick(CommanderDecisionContext context)
        {
            RefreshState(context.Knowledge);
            MaintainMilitaryRallies(context);
            ActiveProductionDecision = null;
            PopulationBlocked = false;
            if (context.Time < nextProductionAttemptTime) return;

            if (LivingPawnCount + QueuedPawnCount < targetPawnCount)
            {
                TryRequest(context, UnitType.Pawn);
                return;
            }
            if (MilitaryPopulation + QueuedMilitaryPopulation >= targetMilitaryPopulation)
            {
                PendingEconomicDemand = null;
                return;
            }

            UnitType? desired = NextCompositionUnit();
            if (desired.HasValue) TryRequest(context, desired.Value);
            else PendingEconomicDemand = null;
        }

        private void RefreshState(FactionKnowledge knowledge)
        {
            Array.Clear(livingCounts, 0, livingCounts.Length);
            Array.Clear(queuedCounts, 0, queuedCounts.Length);
            producers.Clear();
            MilitaryPopulation = QueuedMilitaryPopulation = 0;

            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                HealthComponent health = units[i] != null
                    ? units[i].GetComponent<HealthComponent>() : null;
                UnitCombatData data = health?.CombatData;
                if (data == null || (int)data.UnitType >= livingCounts.Length) continue;
                livingCounts[(int)data.UnitType]++;
                if (data.UnitType != UnitType.Pawn
                    && data.UnitType is not (UnitType.GoblinTorch or UnitType.GoblinTNT))
                    MilitaryPopulation += data.PopulationCost;
            }
            LivingPawnCount = livingCounts[(int)UnitType.Pawn];

            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost host = buildings[i];
                if (host == null || !host.IsAlive) continue;
                UnitProductionQueue queue = host.HostTransform.GetComponent<UnitProductionQueue>();
                if (queue == null) continue;
                if (host.IsProductionReady) producers.Add(queue);
                queue.CopyQueue(queueBuffer);
                for (int item = 0; item < queueBuffer.Count; item++)
                {
                    UnitType type = queueBuffer[item];
                    if ((int)type >= queuedCounts.Length) continue;
                    queuedCounts[(int)type]++;
                    UnitCombatData data = queue.GetData(type);
                    if (data != null && type != UnitType.Pawn
                        && type is not (UnitType.GoblinTorch or UnitType.GoblinTNT))
                        QueuedMilitaryPopulation += data.PopulationCost;
                }
            }
            producers.Sort(CompareProducers);
            FactionPopulation population = knowledge.Population;
            EffectiveFreePopulation = population != null
                ? population.Cap - population.Used - population.Reserved : 0;
        }

        private void TryRequest(CommanderDecisionContext context, UnitType type)
        {
            UnitProductionQueue producer = FindProducer(type);
            if (producer == null || producer.Count >= desiredQueueDepth) return;
            UnitCombatData data = producer.GetData(type);
            if (data == null) return;
            PendingEconomicDemand = type;
            if (EffectiveFreePopulation < data.PopulationCost)
            {
                PopulationBlocked = true;
                nextProductionAttemptTime = context.Time + rejectedCommandRetrySeconds;
                return;
            }
            ActiveProductionDecision = type;
            CommanderCommandResult result = context.Commands.TryTrain(producer, type);
            if (result == CommanderCommandResult.Accepted)
            {
                // Payment and population reservation are authoritative at enqueue.
                // Do not count the accepted request again as an unpaid plan.
                PendingEconomicDemand = null;
                SuccessfulTrainSerial++;
                LastSuccessfulTrainTime = context.Time;
                return;
            }
            if (result == CommanderCommandResult.RejectedPopulationBlocked)
                PopulationBlocked = true;
            nextProductionAttemptTime = context.Time + rejectedCommandRetrySeconds;
        }

        public bool TryGetPendingEconomicCost(out int food, out int wood,
            out int gold, out int population)
        {
            food = wood = gold = population = 0;
            if (!PendingEconomicDemand.HasValue) return false;
            UnitProductionQueue producer = FindProducer(PendingEconomicDemand.Value);
            UnitCombatData data = producer != null
                ? producer.GetData(PendingEconomicDemand.Value) : null;
            if (data == null) return false;
            food = data.FoodCost;
            wood = data.WoodCost;
            gold = data.GoldCost;
            population = data.PopulationCost;
            return true;
        }

        private UnitType? NextCompositionUnit()
        {
            Array.Clear(requiredPrefix, 0, requiredPrefix.Length);
            for (int i = 0; i < CompositionSequence.Length; i++)
            {
                UnitType type = CompositionSequence[i];
                requiredPrefix[(int)type]++;
                int actual = livingCounts[(int)type] + queuedCounts[(int)type];
                if (type == UnitType.Monk && actual >= monkLimit) continue;
                if (actual < requiredPrefix[(int)type]) return type;
            }
            return null;
        }

        private UnitProductionQueue FindProducer(UnitType type)
        {
            for (int i = 0; i < producers.Count; i++)
            {
                IProductionHost host = producers[i].ProductionHost;
                if (host != null && UnitProductionQueue.CanProduce(host.Type, type))
                    return producers[i];
            }
            return null;
        }

        private void MaintainMilitaryRallies(CommanderDecisionContext context)
        {
            if (context.Time < nextRallyAttemptTime) return;
            if (!hasStagingCell && !TryFindStagingCell(context.Knowledge, out stagingCell))
            {
                nextRallyAttemptTime = context.Time + rejectedCommandRetrySeconds;
                return;
            }
            hasStagingCell = true;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return;
            Vector3 destination = grid.GridToWorld(stagingCell.x, stagingCell.y);
            for (int i = 0; i < producers.Count; i++)
            {
                IProductionHost host = producers[i].ProductionHost;
                if (host == null || host.Type == BuildingType.Castle) continue;
                BuildingRallyPoint rally = host is Component component
                    ? component.GetComponent<BuildingRallyPoint>() : null;
                if (rally == null || (rally.HasRallyPoint
                    && rally.RallyCell == stagingCell)) continue;
                CommanderCommandResult result = context.Commands.TrySetRally(rally, destination);
                if (result == CommanderCommandResult.Accepted) return;
                hasStagingCell = false;
                nextRallyAttemptTime = context.Time + rejectedCommandRetrySeconds;
                return;
            }
        }

        private static bool TryFindStagingCell(FactionKnowledge knowledge,
            out Vector2Int result)
        {
            result = default;
            GridManager grid = knowledge.TeamVision?.GridManager;
            if (grid == null || !TryGetCastle(knowledge, out IDefensiveBuildingHost castle))
                return false;
            Vector2Int center = castle.FootprintOrigin
                + new Vector2Int(castle.Footprint.x / 2, castle.Footprint.y / 2);
            int direction = center.x >= grid.Width / 2 ? -1 : 1;
            for (int distance = 5; distance <= 8; distance++)
            for (int side = 0; side <= distance; side++)
            for (int sign = side == 0 ? 0 : -1; sign <= 1; sign += 2)
            {
                Vector2Int cellPosition = center + new Vector2Int(direction * distance,
                    side * sign);
                GridCell cell = grid.GetCell(cellPosition.x, cellPosition.y);
                if (cell == null || !cell.Walkable || !knowledge.IsCellExplored(cellPosition)
                    || IsNearKnownResource(knowledge, cellPosition, 2)
                    || IsInsideOwnBuilding(knowledge, cellPosition)) continue;
                result = cellPosition;
                return true;
            }
            return false;
        }

        private static bool IsNearKnownResource(FactionKnowledge knowledge,
            Vector2Int cell, int radius)
        {
            IReadOnlyList<ResourceKnowledgeSnapshot> resources = knowledge.KnownResources;
            for (int i = 0; i < resources.Count; i++)
                if (Mathf.Abs(resources[i].LogicalCell.x - cell.x) <= radius
                    && Mathf.Abs(resources[i].LogicalCell.y - cell.y) <= radius) return true;
            return false;
        }

        private static bool IsInsideOwnBuilding(FactionKnowledge knowledge, Vector2Int cell)
        {
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost host = buildings[i];
                if (host == null || !host.IsAlive) continue;
                Vector2Int origin = host.FootprintOrigin;
                Vector2Int size = host.Footprint;
                if (cell.x >= origin.x && cell.x < origin.x + size.x
                    && cell.y >= origin.y && cell.y < origin.y + size.y) return true;
            }
            return false;
        }

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

        private static int CompareProducers(UnitProductionQueue first,
            UnitProductionQueue second)
        {
            IProductionHost a = first.ProductionHost;
            IProductionHost b = second.ProductionHost;
            int type = a.Type.CompareTo(b.Type);
            if (type != 0) return type;
            int y = a.FootprintOrigin.y.CompareTo(b.FootprintOrigin.y);
            return y != 0 ? y : a.FootprintOrigin.x.CompareTo(b.FootprintOrigin.x);
        }
    }
}
