using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.AI
{
    [Serializable]
    internal sealed class QlTable
    {
        public const int SchemaVersion = 2;
        public int version;
        public int actions;
        public int episodes;
        public QlRow[] rows;
    }

    [Serializable]
    internal sealed class QlRow
    {
        public string state;
        public float[] values;
    }

    internal sealed class QlFrozenPolicy : IRlPolicy
    {
        private readonly RlCommanderBridge bridge;
        private readonly QlStrategy strategy;
        private readonly Dictionary<string, float[]> rows;
        private readonly System.Random tieBreaker = new(RlMlBootstrap.CurrentSeed);

        public QlFrozenPolicy(RlCommanderBridge bridge, RuleBasedCommander commander, QlTable table)
        {
            this.bridge = bridge;
            strategy = new QlStrategy(commander);
            rows = table.rows.ToDictionary(row => row.state, row => row.values);
        }

        public bool TryChooseAction(float[] observation, out int actionId)
        {
            int[] legal = strategy.GetActions(bridge, observation, out string state);
            if (legal.Length == 0)
            {
                actionId = -1;
                return false;
            }
            rows.TryGetValue(state, out float[] values);
            float best = legal.Max(id => values?[id] ?? 0f);
            int[] ties = Array.FindAll(legal, id => (values?[id] ?? 0f) == best);
            actionId = ties[tieBreaker.Next(ties.Length)];
            return true;
        }
    }

    // The same action constraints apply during Python training and the frozen demo.
    internal sealed class QlStrategy
    {
        private const int WorkerTarget = 6;
        private const int OpeningWorkers = 4;
        private const int OpeningCombatUnits = 2;
        private const float AttackOrderInterval = 6f;
        private const float WarriorSavingSeconds = 90f;
        private const float WarriorSavingRetrySeconds = 60f;
        private static readonly UnitType[] TrainedTypes =
            { UnitType.Pawn, UnitType.Warrior, UnitType.Archer, UnitType.Lancer, UnitType.Monk };
        private readonly RuleBasedCommander commander;
        private readonly UnitCombatData[] trainingData = new UnitCombatData[TrainedTypes.Length];
        private readonly int[] queued = new int[TrainedTypes.Length];
        private readonly List<UnitType> queueItems = new();
        private float nextAttackOrder;
        private float nextArcheryOrder;
        private bool fieldedCombatUnit;
        private float saveUntil;
        private float nextSaveAttempt;

        internal QlStrategy(RuleBasedCommander commander) => this.commander = commander;

        internal int[] GetActions(RlCommanderBridge bridge, float[] o, out string state)
        {
            Array.Clear(queued, 0, queued.Length);
            bool barracks = false;
            bool archery = false;
            bool buildingInProgress = commander.BuildingPolicy.ActiveSite != null;
            foreach (UnitProductionQueue queue in UnitProductionQueue.ActiveQueues)
            {
                if (queue == null || queue.ProductionHost is not { } host
                    || host.Faction != commander.Faction || !host.IsProductionReady) continue;
                barracks |= host.Type == BuildingType.Barracks;
                archery |= host.Type == BuildingType.Archery;
                for (int i = 0; i < trainingData.Length; i++)
                    trainingData[i] ??= queue.GetData(TrainedTypes[i]);
                queue.CopyQueue(queueItems);
                foreach (UnitType type in queueItems)
                {
                    int index = Array.IndexOf(TrainedTypes, type);
                    if (index >= 0) queued[index]++;
                }
            }

            int workers = Mathf.RoundToInt(50f * o[16]) + queued[0];
            int warriors = Mathf.RoundToInt(50f * o[20]) + queued[1];
            int archers = Mathf.RoundToInt(50f * o[24]) + queued[2];
            int lancers = Mathf.RoundToInt(50f * o[28]) + queued[3];
            int available = 0;
            int availablePopulation = 0;
            int castleAttackers = 0;
            foreach (UnitController unit in commander.Knowledge.OwnUnits)
            {
                if (!ArmyGroup.IsEligible(unit, commander.Faction, commander.ScoutingPolicy.Scout))
                    continue;
                UnitCombatData data = unit.GetComponent<HealthComponent>()?.CombatData;
                if (data == null || data.UnitType == UnitType.Monk) continue;
                available++;
                availablePopulation += data.PopulationCost;
                if (HarassmentRuleBasedCommander.IsReadyCastleAttacker(unit)) castleAttackers++;
            }
            bool qlIsland = QlExperiment.UsesGoblinIslandStrategy(commander);
            fieldedCombatUnit |= available > 0;
            bool ready = qlIsland
                ? castleAttackers >= MilitaryCommanderPolicy.IslandAssaultMinimum
                : available >= OpeningCombatUnits
                    && availablePopulation >= commander.MilitaryPolicy.MinOffensiveMilitaryPopulation;
            bool canAttack = bridge.CanChooseAction((int)RlAction.Attack) == RlActionResult.Accepted;
            bool castleKnown = qlIsland && commander.Knowledge.KnownEnemyBuildings.Any(
                item => item.BuildingType == BuildingType.Castle && item.WasCompleted
                    && item.LastSeenHealthRatio > 0f);
            bool holdingMine = qlIsland && commander.MilitaryPolicy.HasOffensiveTarget
                && commander.MilitaryPolicy.OffensiveTarget.BuildingType == BuildingType.GoldMine;
            ArmyGroupState armyState = commander.MilitaryPolicy.Army.State;
            bool committed = armyState is ArmyGroupState.PreparingAttack
                or ArmyGroupState.Advancing or ArmyGroupState.Assaulting;
            bool canAdvance = armyState is ArmyGroupState.Forming or ArmyGroupState.Ready
                or ArmyGroupState.Regrouping;
            var stock = commander.Knowledge.Stockpile;
            UnitCombatData warrior = trainingData[1];
            bool canFundWarrior = warrior != null
                && (stock.GetAmount(ResourceType.Gold) >= warrior.GoldCost
                    || commander.Knowledge.KnownResources.Any(resource =>
                        resource.ResourceType == ResourceType.Gold && resource.LastSeenRemainingAmount > 0));
            bool needsWarrior = barracks && warriors < (qlIsland ? 2 : 1)
                && archers + lancers >= OpeningCombatUnits && canFundWarrior
                && (o[6] > 0f || buildingInProgress);
            if (!needsWarrior) saveUntil = 0f;
            else if (Time.time >= nextSaveAttempt)
            {
                saveUntil = Time.time + WarriorSavingSeconds;
                nextSaveAttempt = saveUntil + WarriorSavingRetrySeconds;
            }
            bool saving = needsWarrior && Time.time < saveUntil;
            int shortage = !saving ? 0 : stock.GetAmount(ResourceType.Gold) < warrior.GoldCost ? 13
                : stock.GetAmount(ResourceType.Food) < warrior.FoodCost ? 11
                : stock.GetAmount(ResourceType.Wood) < warrior.WoodCost ? 12 : 0;
            bool foodForConstruction = stock.GetAmount(ResourceType.Food)
                >= commander.BuildingPolicy.FoodSafetyReserve;
            if (!barracks && !foodForConstruction) shortage = 11;
            BuildingData archerBuilding = qlIsland && barracks && !archery && fieldedCombatUnit
                && !buildingInProgress
                ? commander.CommandService.BuildingSystem.GetData(BuildingType.Archery) : null;
            bool preparingArchery = archerBuilding != null && Time.time >= nextArcheryOrder;
            int archeryShortage = !preparingArchery ? 0
                : !foodForConstruction ? (int)RlAction.PrioritizeFood
                : stock.GetAmount(ResourceType.Wood) < archerBuilding.WoodCost
                    ? (int)RlAction.PrioritizeWood : 0;

            state = $"{(barracks ? 1 : 0)}{(archery ? 1 : 0)}{(workers < OpeningWorkers ? 0 : workers < WorkerTarget ? 1 : 2)}"
                + $"{(warriors > 0 ? 1 : 0)}{(archers > 0 ? 1 : 0)}{(lancers > 0 ? 1 : 0)}"
                + $"{(ready ? 2 : available >= OpeningCombatUnits ? 1 : 0)}{(saving ? 1 : 0)}"
                + $"{(canAttack ? 1 : 0)}{(o[6] < 0.039f ? 1 : 0)}-{(committed ? 1 : 0)}-{shortage}";

            int[] legal = Enumerable.Range(0, RlActionRouter.ActionCount)
                .Where(id => bridge.CanChooseAction(id) == RlActionResult.Accepted).ToArray();
            if (castleKnown && ready && canAttack && (canAdvance || holdingMine)
                && Time.time >= nextAttackOrder)
            {
                nextAttackOrder = Time.time + AttackOrderInterval;
                return new[] { (int)RlAction.Attack };
            }
            if (qlIsland && barracks && available < 12 && o[6] < 0.059f
                && legal.Contains((int)RlAction.BuildHouse))
                return new[] { (int)RlAction.BuildHouse };
            if (preparingArchery && archeryShortage == 0
                && legal.Contains((int)RlAction.BuildArchery))
            {
                nextArcheryOrder = Time.time + 30f;
                return new[] { (int)RlAction.BuildArchery };
            }
            if (archeryShortage != 0 && legal.Contains(archeryShortage))
                return new[] { archeryShortage };
            if (qlIsland && archery && archers == 0 && legal.Contains((int)RlAction.TrainArcher))
                return new[] { (int)RlAction.TrainArcher };
            if (ready && canAdvance && Time.time >= nextAttackOrder
                && (!canAttack || (qlIsland && !castleKnown)))
            {
                nextAttackOrder = Time.time + AttackOrderInterval;
                // Keep mine operations eligible while scouting for a building target.
                if (!legal.Contains((int)RlAction.SecureGoldMine))
                    return new[] { (int)RlAction.Scout };
            }
            if (saving && legal.Contains((int)RlAction.TrainWarrior))
                return new[] { (int)RlAction.TrainWarrior };
            if (barracks && !ready && o[6] < 0.039f && legal.Contains((int)RlAction.BuildHouse))
                return new[] { (int)RlAction.BuildHouse };

            return Array.FindAll(legal, id =>
            {
                RlAction action = (RlAction)id;
                if (action == RlAction.TrainPawn && (workers >= WorkerTarget || queued[0] > 0
                    || (!barracks && !buildingInProgress && workers >= OpeningWorkers)))
                    return false;
                if (action == RlAction.BuildHouse)
                    return o[6] < 0.039f && (barracks || workers == 0);
                if (action == RlAction.BuildBarracks) return !barracks && foodForConstruction;
                if (action == RlAction.BuildArchery) return barracks && !archery;
                if (action == RlAction.SecureGoldMine && (castleKnown || qlIsland && !ready))
                    return false;
                if (shortage != 0 && action is >= RlAction.PrioritizeFood and <= RlAction.PrioritizeGold)
                    return id == shortage;
                if (!barracks && (action is RlAction.BuildMonastery or RlAction.BuildTower
                    or RlAction.TrainArcher or RlAction.TrainMonk))
                    return false;
                if (committed && (action is RlAction.Scout or RlAction.Retreat
                    or RlAction.Defend or RlAction.SecureGoldMine))
                    return false;
                if (action == RlAction.Attack)
                    return (!qlIsland || castleKnown) && (ready || committed);
                if (!saving || action == RlAction.TrainWarrior) return true;
                UnitCombatData unit = id >= 1 && id <= trainingData.Length ? trainingData[id - 1] : null;
                if (unit == null) return true;
                int food = stock.GetAmount(ResourceType.Food);
                int wood = stock.GetAmount(ResourceType.Wood);
                int gold = stock.GetAmount(ResourceType.Gold);
                return unit.FoodCost <= Mathf.Max(0, food - warrior.FoodCost)
                    && unit.WoodCost <= Mathf.Max(0, wood - warrior.WoodCost)
                    && unit.GoldCost <= Mathf.Max(0, gold - warrior.GoldCost);
            });
        }
    }

    // A local JSON exchange keeps Unity responsive while Python learns.
    public sealed class QlPolicy : MonoBehaviour, IRlAsyncPolicy
    {
        private const float ShapedOutcomeRewardScale = 3f;
        // Exceeds the capped development and Castle-progress bonuses (0.955).
        private const float ShapedTimeoutReward = -1f;
        private const float CohesionShapedTimeoutReward = -1.5f;

        [Serializable]
        private sealed class Request
        {
            public int sequence;
            public int processed;
            public string state;
            public float[] observation;
            public int[] legal;
            public float reward;
            public string result;
            public bool terminal;
        }

        [Serializable]
        private sealed class Response
        {
            public int sequence;
            public int action;
            public bool stop;
        }

        private static int sequence;
        private RlCommanderBridge bridge;
        private QlStrategy strategy;
        private string folder;
        private int current;
        private int processed;
        private Action<int> callback;
        private bool terminal;
        private float sentAt;
        private float nextPoll;
        private float reward;
        private bool shapingEnabled;
        private float shapedTimeoutReward;

        public bool IsAvailable => Directory.Exists(folder);

        internal void Configure(RlCommanderBridge owner, RuleBasedCommander commander,
            string exchangeFolder, bool shaping, bool cohesionShaping)
        {
            bridge = owner;
            strategy = new QlStrategy(commander);
            folder = exchangeFolder;
            shapingEnabled = shaping;
            shapedTimeoutReward = cohesionShaping ? CohesionShapedTimeoutReward : ShapedTimeoutReward;
        }

        internal void AddReward(float value) => reward += value;
        internal void MarkActionProcessed() => processed = current;

        public bool RequestDecision(float[] observation, Action<int> onAction)
        {
            if (callback != null || terminal || !IsAvailable) return false;
            int[] legal = strategy.GetActions(bridge, observation, out string state);
            if (legal.Length == 0) return false;
            callback = onAction;
            Send(new Request { state = state,
                observation = observation, legal = legal, reward = reward });
            reward = 0f;
            return true;
        }

        public void CompleteEpisode(float terminalReward, bool interrupted)
        {
            callback = null;
            terminal = true;
            float outcomeReward = shapingEnabled
                ? (interrupted ? shapedTimeoutReward : terminalReward * ShapedOutcomeRewardScale)
                : terminalReward;
            Send(new Request { terminal = true, reward = outcomeReward + reward,
                result = interrupted ? "timeout" : terminalReward > 0f ? "win" : "loss" });
        }

        private void Send(Request request)
        {
            current = ++sequence;
            request.sequence = current;
            request.processed = processed;
            string path = Path.Combine(folder, $"request-{current}.json");
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(request));
            File.Move(temporary, path);
            sentAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if ((!terminal && callback == null) || Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + 0.02f;
            if (terminal && Time.realtimeSinceStartup - sentAt >= 60f)
            {
                terminal = false;
                QlExperiment.Active.Fail("Python did not acknowledge the terminal step");
                return;
            }
            string path = Path.Combine(folder, $"response-{current}.json");
            if (!File.Exists(path)) return;
            try
            {
                Response response = JsonUtility.FromJson<Response>(File.ReadAllText(path));
                if (response == null || response.sequence != current) return;
                File.Delete(path);
                if (terminal)
                {
                    terminal = false;
                    if (response.stop) QlExperiment.Active.FinishRun();
                    else if (!bridge.ReloadSceneForNextEpisode(RlMlBootstrap.AdvanceSeed()))
                        QlExperiment.Active.Fail("The training scene could not reset");
                    return;
                }
                Action<int> complete = callback;
                callback = null;
                complete(response.action);
            }
            catch (IOException) { } // Python may briefly hold a file open on Windows.
        }
    }
}
