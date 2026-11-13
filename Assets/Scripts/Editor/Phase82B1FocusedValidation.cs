#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.UI;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class Phase82B1FocusedValidation
    {
        private const string Pending = "TinyKingdom.Phase82B1Focused.Pending";
        private static double started;
        private static int count;

        private sealed class Host : IProductionHost
        {
            public Host(BuildingType type) => Type = type;
            public BuildingType Type { get; }
            public Faction Faction => Faction.Red;
            public Vector2Int FootprintOrigin => Vector2Int.zero;
            public Vector2Int Footprint => Vector2Int.one;
            public bool IsProductionReady => true;
        }

        private sealed class MaskRecorder : IDiscreteActionMask
        {
            public readonly bool[] Enabled = new bool[RlActionRouter.ActionCount];
            public MaskRecorder() => Array.Fill(Enabled, true);
            public void SetActionEnabled(int branch, int index, bool enabled)
            {
                if (branch != 0 || index < 0 || index >= Enabled.Length)
                    throw new InvalidOperationException("Invalid mask choice");
                Enabled[index] = enabled;
            }
        }

        static Phase82B1FocusedValidation() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity");
            SessionState.SetBool(Pending, true);
            started = EditorApplication.timeSinceStartup;
            count = 0;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (EditorApplication.timeSinceStartup - started > 180d)
            { Finish("PHASE_82B1_FOCUSED_BLOCKED: timeout", 1); return; }
            if (!EditorApplication.isPlaying) return;
            foreach (TopLeftHudPresenter hud in UnityEngine.Object
                .FindObjectsByType<TopLeftHudPresenter>()) hud.enabled = false;
            foreach (ControlGroupHudPresenter hud in UnityEngine.Object
                .FindObjectsByType<ControlGroupHudPresenter>()) hud.enabled = false;
            foreach (SheepPresentation sheep in UnityEngine.Object
                .FindObjectsByType<SheepPresentation>()) sheep.enabled = false;
            EditorApplication.isPaused = false;
            MatchManager match = MatchManager.Instance
                ?? UnityEngine.Object.FindAnyObjectByType<MatchManager>();
            RuleBasedCommander commander = UnityEngine.Object
                .FindAnyObjectByType<RuleBasedCommander>();
            if (commander?.Knowledge?.TeamVision != null
                && !commander.Knowledge.TeamVision.IsReady
                && commander.Knowledge.TeamVision.GridManager?.NavigationData != null)
                commander.Knowledge.TeamVision.Configure(
                    commander.Knowledge.TeamVision.GridManager);
            if (match == null || match.State != MatchState.Playing
                || commander == null || !commander.IsConfigured
                || commander.Knowledge == null || !commander.Knowledge.IsReady) return;
            try
            {
                Time.timeScale = 0f;
                commander.enabled = false;
                typeof(MatchManager).GetField("<Instance>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, match);
                var stock = commander.Knowledge.Stockpile;
                var pop = commander.Knowledge.Population;
                pop.AddCap(100);
                commander.Knowledge.Refresh();
                var router = new RlActionRouter();
                var bridge = commander.GetComponent<RlCommanderBridge>();
                Require(bridge != null, "Bridge missing");
                var agentRoot = new GameObject("Phase82B1MaskRecorder");
                agentRoot.AddComponent<BehaviorParameters>();
                var agent = agentRoot.AddComponent<RlMlAgent>();
                agent.Configure(bridge, false);

                Case("resource-first", commander, match, router, agent,
                    stock, pop, 1000, 1, 1, 1, false, RlActionResult.Accepted, 1);
                Case("resource-second", commander, match, router, agent,
                    stock, pop, 1, 1000, 1, 1, false, RlActionResult.Accepted, 0);
                Case("population-first", commander, match, router, agent,
                    stock, pop, 1, 1, 10000, 1, false, RlActionResult.Accepted, 1);
                Case("population-second", commander, match, router, agent,
                    stock, pop, 1, 1, 1, 10000, false, RlActionResult.Accepted, 0);
                Case("queue-full-first", commander, match, router, agent,
                    stock, pop, 1, 1, 1, 1, true, RlActionResult.Accepted, 1);
                Case("queue-full-second", commander, match, router, agent,
                    stock, pop, 1, 1, 1, 1, false, RlActionResult.Accepted, 0,
                    true);
                Case("both-resource", commander, match, router, agent,
                    stock, pop, 1000, 1000, 1, 1, false,
                    RlActionResult.InsufficientResources, -1);
                Case("both-population", commander, match, router, agent,
                    stock, pop, 1, 1, 10000, 10000, false,
                    RlActionResult.PopulationBlocked, -1);
                Case("no-producer", commander, match, router, agent,
                    stock, pop, -1, -1, 1, 1, false,
                    RlActionResult.MissingProducer, -1);
                Case("no-data", commander, match, router, agent,
                    stock, pop, -2, -2, 1, 1, false,
                    RlActionResult.PolicyRejected, -1);
                Case("both-eligible", commander, match, router, agent,
                    stock, pop, 1, 1, 1, 1, false, RlActionResult.Accepted, 0);
                Case("mixed-failure", commander, match, router, agent,
                    stock, pop, 1, 1000, 10000, 1, false,
                    RlActionResult.PopulationBlocked, -1);
                Remaining(commander, match, router, agent, stock, pop);
                UnityEngine.Object.DestroyImmediate(agentRoot);
                Finish("PHASE_82B1_FOCUSED_PASS: " + count
                    + " cases, Router, policy enqueue and local mask", 0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Finish("PHASE_82B1_FOCUSED_FAIL: " + ex.Message, 1);
            }
        }

        private static void Remaining(RuleBasedCommander commander,
            MatchManager match, RlActionRouter router, RlMlAgent agent,
            ResourceStockpile stock, FactionPopulation pop)
        {
            stock.Configure(Faction.Red, 10000, 10000, 10000);
            UnitProductionQueue barracks = NewQueueFor(BuildingType.Barracks,
                stock, pop, Data(1, 1, UnitType.Warrior),
                Data(1, 1, UnitType.Lancer));
            UnitProductionQueue archery = NewQueueFor(BuildingType.Archery,
                stock, pop, Data(1, 1, UnitType.Archer));
            UnitProductionQueue monastery = NewQueueFor(BuildingType.Monastery,
                stock, pop, Data(1, 1, UnitType.Monk));
            var knownBuildings = (List<BuildingKnowledgeSnapshot>)typeof(FactionKnowledge)
                .GetField("knownEnemyBuildings", BindingFlags.Instance
                    | BindingFlags.NonPublic).GetValue(commander.Knowledge);
            var knownMines = (List<GoldMineKnowledgeSnapshot>)typeof(FactionKnowledge)
                .GetField("knownGoldMines", BindingFlags.Instance
                    | BindingFlags.NonPublic).GetValue(commander.Knowledge);
            var targetField = typeof(StrategicIntentEvaluator).GetField("target",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var currentField = typeof(GoldMineStrategyModel).GetField(
                "<Current>k__BackingField", BindingFlags.Instance
                    | BindingFlags.NonPublic);
            var siteField = typeof(BuildingCommanderPolicy).GetField("activeSite",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var ownUnits = (List<UnitController>)typeof(FactionKnowledge).GetField(
                "ownUnits", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(commander.Knowledge);
            var ownBuildings = (List<IDefensiveBuildingHost>)typeof(FactionKnowledge)
                .GetField("ownBuildings", BindingFlags.Instance
                    | BindingFlags.NonPublic).GetValue(commander.Knowledge);
            var mine = new GoldMineKnowledgeSnapshot(default, Vector3.zero,
                GoldMineState.Neutral, Faction.Goblin, false, 1000, 0, 1f,
                Time.time);
            try
            {
                Require(ownUnits.Count > 0, "No own Pawn in scene");
                for (int id = 1; id <= 5; id++)
                    Require(router.CanChoose(id, commander, match)
                        == RlActionResult.Accepted,
                        "Training ID " + id + " not eligible");
                for (int id = 6; id <= 10; id++)
                    Require(router.CanChoose(id, commander, match)
                        == RlActionResult.Accepted,
                        "Building ID " + id + " not eligible");
                knownBuildings.Clear();
                knownBuildings.Add(new BuildingKnowledgeSnapshot(default,
                    BuildingType.Castle, Faction.Blue, Vector3.zero,
                    Vector2Int.zero, Vector2Int.one, true, 1f, 1f, 0f,
                    Time.time));
                targetField.SetValue(commander.StrategicIntent,
                    new StrategicTargetSnapshot(default, BuildingType.Castle,
                        Faction.Blue, Vector3.zero, 1f, Time.time, true));
                knownMines.Clear();
                knownMines.Add(mine);
                currentField.SetValue(commander.StrategicIntent.GoldMine,
                    new GoldMineOpportunity(mine, 1f, 0f, 10f, true,
                        true, false, "Test"));
                bool[] all = Mask(agent, commander, match);
                for (int id = 0; id < 19; id++)
                    Require(all[id], "Action " + id + " not enabled");
                Debug.Log("PHASE_82B1_SCENARIO_PASS: all 19 IDs enabled in "
                    + "controlled eligible fixture");
                count++;

                var existingHouseObject = new GameObject("Phase82B1ExistingHouse");
                BuildingSite existingHouse = existingHouseObject.AddComponent<BuildingSite>();
                typeof(BuildingSite).GetField("data", BindingFlags.Instance
                    | BindingFlags.NonPublic).SetValue(existingHouse,
                    commander.CommandService.BuildingSystem.GetData(
                        BuildingType.House));
                typeof(BuildingSite).GetField("currentHP", BindingFlags.Instance
                    | BindingFlags.NonPublic).SetValue(existingHouse, 1f);
                typeof(BuildingSite).GetField("<Faction>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(existingHouse, Faction.Red);
                typeof(BuildingSite).GetField("<State>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(existingHouse, BuildingState.Completed);
                ownBuildings.Add(existingHouse);
                Require(Mask(agent, commander, match)[6],
                    "Existing House masked BuildHouse");
                ownBuildings.Remove(existingHouse);
                UnityEngine.Object.DestroyImmediate(existingHouseObject);
                Debug.Log("PHASE_82B1_SCENARIO_PASS: existing completed "
                    + "same-type House does not mask BuildHouse");
                count++;

                FogRenderer fog = UnityEngine.Object.FindAnyObjectByType<FogRenderer>();
                Require(fog != null, "FogRenderer missing");
                bool wasFog = fog.IsFogVisible;
                var provider = new RlObservationProvider();
                var before = new float[96];
                var after = new float[96];
                Require(provider.TryBuild(commander.Knowledge, match, before),
                    "Fog baseline observation missing");
                fog.SetFogVisible(!wasFog);
                bool[] fogMask = Mask(agent, commander, match);
                Require(provider.TryBuild(commander.Knowledge, match, after),
                    "Fog toggled observation missing");
                for (int i = 0; i < 19; i++)
                    Require(all[i] == fogMask[i], "Fog changed mask " + i);
                for (int i = 0; i < 96; i++)
                    Require(before[i] == after[i], "Fog changed observation " + i);
                fog.SetFogVisible(wasFog);
                Debug.Log("PHASE_82B1_SCENARIO_PASS: Fog local 96/19 invariance");
                count++;

                stock.Configure(Faction.Red, 0, 0, 0);
                bool[] noFunds = Mask(agent, commander, match);
                for (int id = 1; id <= 10; id++)
                    Require(!noFunds[id], "No-funds ID " + id + " enabled");
                Require(noFunds[0] && noFunds[11] && noFunds[12]
                    && noFunds[13] && noFunds[14] && noFunds[16]
                    && noFunds[17], "No-funds baseline actions missing");
                stock.Configure(Faction.Red, 10000, 10000, 10000);
                bool[] fundsRestored = Mask(agent, commander, match);
                for (int id = 1; id <= 10; id++)
                    Require(fundsRestored[id], "Restored funds ID " + id
                        + " still masked");
                Debug.Log("PHASE_82B1_SCENARIO_PASS: resource transition "
                    + "and repeated local mask callback");
                count++;

                int cap = pop.Cap;
                pop.AddCap(pop.Used + pop.Reserved - cap);
                bool[] capped = Mask(agent, commander, match);
                for (int id = 1; id <= 5; id++)
                    Require(!capped[id], "Population cap ID " + id + " enabled");
                pop.AddCap(cap - pop.Cap);
                Debug.Log("PHASE_82B1_SCENARIO_PASS: population cap");
                count++;

                var siteRoot = new GameObject("Phase82B1ActiveSite");
                siteField.SetValue(commander.BuildingPolicy,
                    siteRoot.AddComponent<BuildingSite>());
                bool[] siteMask = Mask(agent, commander, match);
                for (int id = 6; id <= 10; id++)
                    Require(!siteMask[id], "Active site build ID " + id + " enabled");
                siteField.SetValue(commander.BuildingPolicy, null);
                UnityEngine.Object.DestroyImmediate(siteRoot);
                Debug.Log("PHASE_82B1_SCENARIO_PASS: active construction site");
                count++;

                currentField.SetValue(commander.StrategicIntent.GoldMine,
                    new GoldMineOpportunity(mine, 1f, 0f, 10f, true,
                        false, false, "Not ready"));
                Require(!Mask(agent, commander, match)[18],
                    "Unready mine enabled");
                currentField.SetValue(commander.StrategicIntent.GoldMine,
                    new GoldMineOpportunity(mine, 1f, 0f, 10f, true,
                        true, false, "Ready"));
                Require(Mask(agent, commander, match)[18],
                    "Ready mine masked");
                Debug.Log("PHASE_82B1_SCENARIO_PASS: cached mine eligibility");
                count++;

                Require(Mask(agent, commander, match)[15],
                    "Remembered target masked");
                targetField.SetValue(commander.StrategicIntent,
                    new StrategicTargetSnapshot(default, BuildingType.Castle,
                        Faction.Blue, Vector3.zero, 1f, Time.time, false));
                Require(Mask(agent, commander, match)[15],
                    "Hidden remembered target masked");
                Debug.Log("PHASE_82B1_SCENARIO_PASS: hidden remembered target");
                count++;
                targetField.SetValue(commander.StrategicIntent,
                    default(StrategicTargetSnapshot));
                knownBuildings.Clear();
                Require(!Mask(agent, commander, match)[15],
                    "Cleared target enabled");
                Debug.Log("PHASE_82B1_SCENARIO_PASS: target memory cleared");
                count++;

                bool[] beforeStale = Mask(agent, commander, match);
                Require(beforeStale[2], "Stale-action baseline unavailable");
                stock.Configure(Faction.Red, 0, 0, 0);
                RlActionResult stale = router.TryPrepare(2, commander,
                    match, out _);
                Require(stale == RlActionResult.InsufficientResources,
                    "Stale action accepted: " + stale);
                stock.Configure(Faction.Red, 10000, 10000, 10000);
                Debug.Log("PHASE_82B1_SCENARIO_PASS: Router stale-action "
                    + "revalidation; bridge abort not exercised");
                count++;

                UnityEngine.Object.DestroyImmediate(barracks.gameObject);
                Require(!Mask(agent, commander, match)[2],
                    "Destroyed producer still enabled");
                Debug.Log("PHASE_82B1_SCENARIO_PASS: producer destroyed");
                count++;

                var redPawns = new List<UnitController>();
                for (int i = 0; i < ownUnits.Count; i++)
                {
                    UnitController unit = ownUnits[i];
                    if (unit != null && unit.GetComponent<HealthComponent>()?
                            .CombatData?.UnitType == UnitType.Pawn)
                        redPawns.Add(unit);
                }
                Require(redPawns.Count > 0, "No Red Pawn to destroy");
                for (int i = 0; i < redPawns.Count; i++)
                    UnityEngine.Object.DestroyImmediate(redPawns[i].gameObject);
                commander.Knowledge.Refresh();
                bool[] noPawn = Mask(agent, commander, match);
                for (int id = 6; id <= 10; id++)
                    Require(!noPawn[id], "No-Pawn build ID " + id + " enabled");
                Debug.Log("PHASE_82B1_SCENARIO_PASS: last own Pawn destroyed");
                count++;

                stock.Configure(Faction.Red, 0, 0, 0);
                UnityEngine.Object.DestroyImmediate(archery.gameObject);
                UnityEngine.Object.DestroyImmediate(monastery.gameObject);
                for (int i = UnitProductionQueue.ActiveQueues.Count - 1; i >= 0; i--)
                {
                    UnitProductionQueue queue = UnitProductionQueue.ActiveQueues[i];
                    if (queue != null && queue.ProductionHost?.Faction == Faction.Red)
                        queue.enabled = false;
                }
                knownMines.Clear();
                currentField.SetValue(commander.StrategicIntent.GoldMine,
                    default(GoldMineOpportunity));
                bool[] noMine = Mask(agent, commander, match);
                Require(!noMine[18] && !noMine[15],
                    "No-target/no-mine state invalid");
                for (int id = 1; id <= 10; id++)
                    Require(!noMine[id], "No-capability ID " + id + " enabled");
                for (int id = 2; id <= 5; id++)
                    Require(router.CanChoose(id, commander, match)
                        == RlActionResult.MissingProducer,
                        "Missing producer ID " + id + " wrong reason");
                Debug.Log("PHASE_82B1_SCENARIO_PASS: no resources, producer, "
                    + "Pawn, target or mine; all training producer mappings");
                count++;

                agent.Configure(null, false);
                var nullMask = new MaskRecorder();
                agent.WriteDiscreteActionMask(nullMask);
                for (int id = 0; id < 19; id++)
                    Require(nullMask.Enabled[id],
                        "Null-Bridge callback unexpectedly masked " + id);
                Require(!((IRlAsyncPolicy)agent).RequestDecision(new float[96],
                    _ => { }), "Null-Bridge Agent requested decision");
                agent.Configure(commander.GetComponent<RlCommanderBridge>(), false);
                Debug.Log("PHASE_82B1_SCENARIO_PASS: null Bridge local callback "
                    + "is unmasked, but RequestDecision unavailable without model");
                count++;

                var matchState = typeof(MatchManager).GetField(
                    "<State>k__BackingField", BindingFlags.Instance
                        | BindingFlags.NonPublic);
                matchState.SetValue(match, MatchState.Ended);
                for (int id = 0; id < 19; id++)
                    Require(router.CanChoose(id, commander, match)
                        == RlActionResult.MatchStopped,
                        "Terminal Router ID " + id + " accepted");
                bool[] terminal = Mask(agent, commander, match);
                Require(terminal[0], "Terminal Wait unexpectedly masked");
                for (int id = 1; id < 19; id++)
                    Require(!terminal[id], "Terminal ID " + id + " enabled");
                Debug.Log("PHASE_82B1_SCENARIO_PASS: terminal Router and "
                    + "local callback; no actual decision sent");
                count++;
            }
            finally
            {
                if (barracks != null) UnityEngine.Object.DestroyImmediate(barracks.gameObject);
                if (archery != null) UnityEngine.Object.DestroyImmediate(archery.gameObject);
                if (monastery != null) UnityEngine.Object.DestroyImmediate(monastery.gameObject);
            }
        }

        private static bool[] Mask(RlMlAgent agent, RuleBasedCommander commander,
            MatchManager match)
        {
            var mask = new MaskRecorder();
            agent.WriteDiscreteActionMask(mask);
            if (match.State == MatchState.Playing)
                for (int id = 0; id < RlActionRouter.ActionCount; id++)
                    Require(mask.Enabled[id] == (commander.GetComponent<
                        RlCommanderBridge>().CanChooseAction(id)
                        == RlActionResult.Accepted),
                        "Full mask mismatch ID " + id);
            return mask.Enabled;
        }

        private static void Case(string label, RuleBasedCommander commander,
            MatchManager match, RlActionRouter router, RlMlAgent agent,
            ResourceStockpile stock, FactionPopulation pop,
            int foodA, int foodB, int popA, int popB, bool fillA,
            RlActionResult expected, int expectedSelected, bool fillB = false)
        {
            stock.Configure(Faction.Red, 100, 100, 100);
            UnitProductionQueue first = foodA == -1 ? null
                : NewQueue(foodA >= 0 ? Data(foodA, popA) : null, stock, pop);
            UnitProductionQueue second = foodB == -1 ? null
                : NewQueue(foodB >= 0 ? Data(foodB, popB) : null, stock, pop);
            try
            {
                if (fillA)
                {
                    Require(first != null, label + " missing first queue");
                    for (int i = 0; i < first.MaxCount; i++)
                        Require(first.TryTrain(UnitType.Warrior),
                            label + " could not fill queue " + i);
                }
                if (fillB)
                {
                    Require(second != null, label + " missing second queue");
                    for (int i = 0; i < second.MaxCount; i++)
                        Require(second.TryTrain(UnitType.Warrior),
                            label + " could not fill second queue " + i);
                }
                bool a = first != null && first.CanTrain(UnitType.Warrior);
                bool b = second != null && second.CanTrain(UnitType.Warrior);
                RlActionResult result = router.CanChoose((int)RlAction.TrainWarrior,
                    commander, match);
                Require(result == expected, label + " Router " + result
                    + " expected " + expected);
                var mask = new MaskRecorder();
                agent.WriteDiscreteActionMask(mask);
                Require(mask.Enabled[0] && mask.Enabled[2]
                    == (expected == RlActionResult.Accepted),
                    label + " local mask ID2 mismatch");
                for (int id = 1; id < RlActionRouter.ActionCount; id++)
                    Require(mask.Enabled[id] == (router.CanChoose(id, commander,
                        match) == RlActionResult.Accepted),
                        label + " mask mismatch ID " + id);

                var policy = new ProductionCommanderPolicy();
                var list = (List<UnitProductionQueue>)typeof(ProductionCommanderPolicy)
                    .GetField("producers", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(policy);
                if (first != null) list.Add(first);
                if (second != null) list.Add(second);
                var method = typeof(ProductionCommanderPolicy).GetMethod(
                    "FindProducer", BindingFlags.Instance | BindingFlags.NonPublic);
                var selected = (UnitProductionQueue)method.Invoke(policy,
                    new object[] { UnitType.Warrior, true });
                int index = ReferenceEquals(selected, first) && first != null ? 0
                    : ReferenceEquals(selected, second) && second != null ? 1 : -1;
                Require(index == expectedSelected, label + " policy selected "
                    + index + " expected " + expectedSelected);
                int firstBefore = first?.Count ?? 0;
                int secondBefore = second?.Count ?? 0;
                typeof(ProductionCommanderPolicy).GetField(
                    "<EffectiveFreePopulation>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(policy,
                    pop.Cap - pop.Used - pop.Reserved);
                var request = typeof(ProductionCommanderPolicy).GetMethod(
                    "TryRequest", BindingFlags.Instance | BindingFlags.NonPublic);
                var context = new CommanderDecisionContext(Faction.Red, Time.time,
                    commander.Knowledge, commander.CommandService);
                request.Invoke(policy, new object[] { context, UnitType.Warrior, true });
                bool command = policy.SuccessfulTrainSerial == 1;
                Require(command == (expectedSelected >= 0),
                    label + " production command=" + command);
                if (expectedSelected == 0)
                    Require(first.Count == firstBefore + 1, label + " first not enqueued");
                if (expectedSelected == 1)
                    Require(second.Count == secondBefore + 1, label + " second not enqueued");
                Debug.Log("PHASE_82B1_FOCUSED_CASE_PASS: " + label
                    + " canTrain=" + a + "," + b + " selected=" + index
                    + " router=" + result + " command=" + command
                    + " mask2=" + mask.Enabled[2]);
                count++;
            }
            finally
            {
                if (first != null) UnityEngine.Object.DestroyImmediate(first.gameObject);
                if (second != null) UnityEngine.Object.DestroyImmediate(second.gameObject);
            }
        }

        private static UnitProductionQueue NewQueue(UnitCombatData data,
            ResourceStockpile stock, FactionPopulation pop)
        {
            var go = new GameObject("Phase82B1Producer");
            var queue = go.AddComponent<UnitProductionQueue>();
            queue.Configure(new Host(BuildingType.Barracks), stock, pop, null,
                data != null ? new[] { data } : Array.Empty<UnitCombatData>(), null);
            return queue;
        }

        private static UnitProductionQueue NewQueueFor(BuildingType type,
            ResourceStockpile stock, FactionPopulation pop,
            params UnitCombatData[] definitions)
        {
            var go = new GameObject("Phase82B1" + type);
            var queue = go.AddComponent<UnitProductionQueue>();
            queue.Configure(new Host(type), stock, pop, null, definitions, null);
            return queue;
        }

        private static UnitCombatData Data(int food, int population,
            UnitType type = UnitType.Warrior)
        {
            var data = ScriptableObject.CreateInstance<UnitCombatData>();
            Set(data, "unitType", type);
            Set(data, "foodCost", food);
            Set(data, "goldCost", 0);
            Set(data, "populationCost", population);
            return data;
        }

        private static void Set(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) throw new InvalidOperationException(field);
            info.SetValue(target, value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(string message, int code)
        {
            SessionState.SetBool(Pending, false);
            Debug.Log(message);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
#endif
