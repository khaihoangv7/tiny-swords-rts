#if UNITY_EDITOR
using System;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Debugging;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.UI;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class Phase81RlValidation
    {
        private const string Pending = "TinyKingdom.Phase81RlValidation.Pending";
        private static double started;
        private static double lastProgress;
        private static bool reloadLocked;
        private static int batchStage;
        private static int speedIndex;
        private static int lastDecisions;
        private static ulong baselineTickStart;
        private static float baselineTimeStart;
        private static double pauseUntil;
        private static float pausedSimulationTime;
        private static int pausedDecisions;
        private static float lastDecisionTime;
        private static float firstEpisodeStart;
        private static MatchManager firstMatch;
        private static FactionKnowledge firstKnowledge;
        private static RuleBasedCommander batchCommander;
        private static RlCommanderBridge batchBridge;
        private static int secondStartTrainSerial;
        private static readonly float[] Speeds = { 1f, 2f, 5f, 10f };

        private sealed class WaitPolicy : IRlPolicy
        {
            public bool TryChooseAction(float[] observation, out int actionId)
            { actionId = (int)RlAction.Wait; return true; }
        }

        private sealed class InvalidPolicy : IRlPolicy
        {
            public bool TryChooseAction(float[] observation, out int actionId)
            { actionId = 99; return true; }
        }

        private sealed class EconomyPolicy : IRlPolicy
        {
            private int calls;
            public bool TryChooseAction(float[] observation, out int actionId)
            {
                actionId = calls++ == 0 ? (int)RlAction.TrainPawn
                    : (int)RlAction.PrioritizeWood;
                return true;
            }
        }

        static Phase81RlValidation() => EditorApplication.update += Tick;

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 8.1 RL Interface")]
        public static void Run()
        {
            ValidateStructural();
            if (EditorApplication.isPlaying) ValidatePlayMode();
            else Debug.LogWarning("PHASE_81_RUNTIME_NOT_RUN: enter Map Play Mode for hidden-state and Fog tests.");
        }

        public static void RunBatch()
        {
            ValidateStructural();
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity");
            SessionState.SetBool(Pending, true);
            started = EditorApplication.timeSinceStartup;
            lastProgress = started;
            batchStage = 0;
            if (Application.isBatchMode)
            {
                EditorApplication.LockReloadAssemblies();
                reloadLocked = true;
            }
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (started == 0d) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - lastProgress > 8d)
            {
                lastProgress = EditorApplication.timeSinceStartup;
                Debug.Log("PHASE_81_PROGRESS stage=" + batchStage
                    + " match=" + MatchManager.Instance?.ElapsedGameplaySeconds
                    + " mode=" + batchBridge?.Mode
                    + " playing=" + EditorApplication.isPlaying
                    + " paused=" + EditorApplication.isPaused
                    + " bridgeEnabled=" + (batchBridge != null
                        && batchBridge.isActiveAndEnabled)
                    + " decisions=" + batchBridge?.Episode.DecisionCount
                    + " next=" + batchBridge?.Scheduler.NextDecisionTime
                    + " timeScale=" + Time.timeScale);
            }
            if (EditorApplication.timeSinceStartup - started > 90d)
            {
                SessionState.SetBool(Pending, false);
                Debug.LogError("PHASE_81_RUNTIME_BLOCKED: integration stage "
                    + batchStage + " timed out after 90s.");
                UnlockReload();
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (!EditorApplication.isPlaying || MatchManager.Instance == null
                || MatchManager.Instance.State != MatchState.Playing) return;
            try
            {
                if (batchStage == 0)
                {
                    ValidatePlayMode();
                    DisableHeadlessPresentationErrors();
                    batchCommander = UnityEngine.Object
                        .FindAnyObjectByType<RuleBasedCommander>();
                    batchBridge = UnityEngine.Object
                        .FindAnyObjectByType<RlCommanderBridge>();
                    Require(batchCommander != null && batchBridge != null
                        && batchBridge.Mode == RlCommanderMode.RuleBased,
                        "RuleBased default mode missing");
                    baselineTickStart = batchCommander.ThinkTick;
                    baselineTimeStart = MatchManager.Instance.ElapsedGameplaySeconds;
                    firstMatch = MatchManager.Instance;
                    firstKnowledge = batchCommander.Knowledge;
                    firstEpisodeStart = MatchManager.Instance.ElapsedGameplaySeconds;
                    batchStage = 6;
                    return;
                }
                if (batchStage == 6)
                {
                    if (batchCommander.ThinkTick < baselineTickStart + 2) return;
                    Require(batchBridge.Mode == RlCommanderMode.RuleBased
                        && batchBridge.Episode.DecisionCount == 0
                        && MatchManager.Instance.ElapsedGameplaySeconds
                            > baselineTimeStart,
                        "RuleBased baseline did not run independently");
                    Debug.Log("PHASE_81_RULE_BASED_PASS: default mode advances without RL decisions.");
                    BeginIntegrationEpisode(111);
                    batchStage = 1;
                    return;
                }
                if (batchStage == 1)
                {
                    if (batchBridge.Mode != RlCommanderMode.RL)
                        throw new InvalidOperationException("RL bridge fell back: "
                            + batchBridge.FallbackReason);
                    int decisions = batchBridge.Episode.DecisionCount;
                    if (decisions == lastDecisions) return;
                    Require(decisions == lastDecisions + 1,
                        "Duplicate RL decision in one scheduler update");
                    float now = MatchManager.Instance.ElapsedGameplaySeconds;
                    if (lastDecisions > 0)
                    {
                        float interval = now - lastDecisionTime;
                        Require(interval >= 1.95f && interval < 3.5f,
                            "Runtime decision interval " + interval);
                    }
                    lastDecisions = decisions;
                    lastDecisionTime = now;
                    if (speedIndex == 0)
                    {
                        pausedSimulationTime = now;
                        pausedDecisions = decisions;
                        pauseUntil = EditorApplication.timeSinceStartup + 0.6d;
                        Time.timeScale = 0f;
                        batchStage = 7;
                        return;
                    }
                    if (speedIndex < Speeds.Length - 1)
                    {
                        speedIndex++;
                        Require(MatchSpeedController.Instance.SetSpeed(Speeds[speedIndex]),
                            "Cannot set x" + Speeds[speedIndex]);
                        return;
                    }
                    Require(decisions >= 4, "Did not test four speed settings");
                    Require(batchBridge.Episode.Seed == 111,
                        "First episode seed lost");
                    CastleDropOff enemyCastle = MatchManager.Instance.BlueMainCastle;
                    Require(enemyCastle != null && enemyCastle.ReceiveBuildingDamage(
                        Mathf.CeilToInt(enemyCastle.CurrentHP)),
                        "Could not resolve Red victory through Castle authority");
                    Require(MatchManager.Instance.Result == MatchResult.RedVictory
                        && batchBridge.Episode.EndKind == RlEpisodeEnd.Termination
                        && batchBridge.Episode.TerminalReward == 1f
                        && !batchBridge.Episode.EndMatch(Faction.Red,
                            MatchResult.RedVictory),
                        "Authoritative match reward missing or duplicated");
                    Require(batchBridge.ReloadSceneForNextEpisode(222),
                        "Full scene reset unavailable");
                    batchStage = 2;
                    return;
                }
                if (batchStage == 7)
                {
                    if (EditorApplication.timeSinceStartup < pauseUntil) return;
                    Require(Mathf.Abs(MatchManager.Instance.ElapsedGameplaySeconds
                            - pausedSimulationTime) < 0.001f
                        && batchBridge.Episode.DecisionCount == pausedDecisions,
                        "Pause advanced simulation or RL decisions");
                    Debug.Log("PHASE_81_PAUSE_PASS: 0.6s wall pause produced no simulation or RL decision.");
                    speedIndex = 1;
                    Require(MatchSpeedController.Instance.SetSpeed(Speeds[speedIndex]),
                        "Cannot resume at x2");
                    batchStage = 1;
                    return;
                }
                if (batchStage == 2)
                {
                    if (ReferenceEquals(MatchManager.Instance, firstMatch))
                        return;
                    RuleBasedCommander secondCommander = UnityEngine.Object
                        .FindAnyObjectByType<RuleBasedCommander>();
                    Require(secondCommander != null && !ReferenceEquals(
                            secondCommander.Knowledge, firstKnowledge),
                        "Stale knowledge reference after scene reset");
                    Require(MatchManager.Instance.ElapsedGameplaySeconds <
                        firstEpisodeStart + 2f, "Second world clock did not reset");
                    Scene currentScene = MatchManager.Instance.gameObject.scene;
                    foreach (UnitController unit in UnitController.ActiveUnits)
                        Require(unit != null && unit.gameObject.scene == currentScene,
                            "Unit registry retained prior-scene object");
                    foreach (BuildingSite site in BuildingSite.ActiveSites)
                        Require(site != null && site.gameObject.scene == currentScene,
                            "Builder reservation retained prior-scene site");
                    foreach (UnitProductionQueue queue in UnitProductionQueue.ActiveQueues)
                        Require(queue != null && queue.gameObject.scene == currentScene,
                            "Production queue retained prior-scene object");
                    BeginIntegrationEpisode(222, new EconomyPolicy());
                    secondStartTrainSerial = (int)batchCommander
                        .ProductionPolicy.SuccessfulTrainSerial;
                    Require(batchBridge.Episode.DecisionCount == 0
                        && batchBridge.Episode.LastActionId == -1
                        && batchBridge.Episode.TerminalReward == 0f,
                        "Second episode retained session state");
                    batchStage = 3;
                    return;
                }
                if (batchStage == 3)
                {
                    if (batchBridge.Mode != RlCommanderMode.RL)
                        throw new InvalidOperationException("Second episode fallback: "
                            + batchBridge.FallbackReason);
                    if (batchBridge.Episode.DecisionCount < 2) return;
                    Require(batchBridge.Episode.Seed == 222,
                        "Second episode seed lost");
                    Require(batchBridge.Episode.LastActionId
                            == (int)RlAction.PrioritizeWood
                        && batchCommander.ProductionPolicy.SuccessfulTrainSerial
                            > secondStartTrainSerial
                        && batchCommander.EconomicDemand.DemandWeight(
                            ResourceType.Wood) >= 2.5f,
                        "TrainPawn or PrioritizeWood did not reach existing policies: "
                        + "lastAction=" + batchBridge.Episode.LastActionId
                        + " trainSerial=" + batchCommander.ProductionPolicy.SuccessfulTrainSerial
                        + " startSerial=" + secondStartTrainSerial
                        + " woodWeight=" + batchCommander.EconomicDemand.DemandWeight(
                            ResourceType.Wood));
                    Require(!batchBridge.BeginEpisode(333)
                        && batchBridge.Episode.Seed == 222,
                        "Same-world episode restart was accepted");
                    Require(!ReferenceEquals(MatchManager.Instance, firstMatch),
                        "MatchManager reused after scene reset");
                    batchBridge.Configure(batchCommander, new InvalidPolicy());
                    batchStage = 4;
                    return;
                }
                if (batchStage == 4)
                {
                    if (batchBridge.Mode == RlCommanderMode.RL) return;
                    Require(batchBridge.Mode == RlCommanderMode.Fallback
                        && batchBridge.LastActionResult == RlActionResult.InvalidId
                        && batchBridge.Episode.EndKind == RlEpisodeEnd.Abort,
                        "Invalid action fallback failed");
                    batchBridge.Configure(batchCommander, null);
                    batchBridge.SetMode(RlCommanderMode.RL);
                    batchStage = 5;
                    return;
                }
                if (batchStage == 5)
                {
                    if (batchBridge.Mode == RlCommanderMode.RL) return;
                    Require(batchBridge.Mode == RlCommanderMode.Fallback
                        && batchBridge.FallbackReason == "Policy unavailable",
                        "Missing trainer fallback failed");
                    SessionState.SetBool(Pending, false);
                    Debug.Log("PHASE_81_RUNTIME_PASS: hidden state Blue/Red, Fog, RL x1/x2/x5/x10, two scene episodes, invalid/trainer fallback.");
                    UnlockReload();
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                SessionState.SetBool(Pending, false);
                Debug.LogException(exception);
                Debug.LogError("PHASE_81_RUNTIME_FAIL: " + exception.Message);
                UnlockReload();
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void UnlockReload()
        {
            if (!reloadLocked) return;
            EditorApplication.UnlockReloadAssemblies();
            reloadLocked = false;
        }

        private static void BeginIntegrationEpisode(int seed,
            IRlPolicy policy = null)
        {
            batchCommander = UnityEngine.Object.FindAnyObjectByType<RuleBasedCommander>();
            batchBridge = UnityEngine.Object.FindAnyObjectByType<RlCommanderBridge>();
            Require(batchCommander != null && batchBridge != null,
                "RL adapter or Commander missing");
            batchBridge.Configure(batchCommander, policy ?? new WaitPolicy());
            DisableHeadlessPresentationErrors();
            batchBridge.SetMode(RlCommanderMode.RL);
            Require(batchBridge.BeginEpisode(seed), "Could not begin episode " + seed);
            speedIndex = 0;
            lastDecisions = 0;
            lastDecisionTime = 0f;
            Require(MatchSpeedController.Instance != null
                && MatchSpeedController.Instance.SetSpeed(Speeds[0]),
                "Could not set x1 speed");
        }

        private static void DisableHeadlessPresentationErrors()
        {
            if (!Application.isBatchMode) return;
            // The headless clone has missing presentation references; these
            // HUD exceptions trigger Editor Error Pause before time advances.
            foreach (TopLeftHudPresenter hud in UnityEngine.Object
                .FindObjectsByType<TopLeftHudPresenter>()) hud.enabled = false;
            foreach (ControlGroupHudPresenter hud in UnityEngine.Object
                .FindObjectsByType<ControlGroupHudPresenter>()) hud.enabled = false;
            EditorApplication.isPaused = false;
        }

        private static void ValidateStructural()
        {
            Require(RlObservationProvider.FeatureCount == 96, "Observation length");
            Require(RlObservationProvider.SchemaVersion == 1, "Schema version");
            Require(RlActionRouter.ActionCount == 19, "Action count");
            for (int i = 0; i < 19; i++)
                Require(Enum.IsDefined(typeof(RlAction), i), "Missing action " + i);

            foreach (float speed in new[] { 1f, 2f, 5f, 10f })
            {
                var scheduler = new RlDecisionScheduler();
                scheduler.Reset();
                Require(scheduler.TryConsume(0f, true), "Initial decision x" + speed);
                Require(!scheduler.TryConsume(1.99f, true), "Early decision x" + speed);
                Require(scheduler.TryConsume(2f, true), "2 simulated seconds x" + speed);
                Require(!scheduler.TryConsume(20f, false), "Pause decision x" + speed);
                Require(scheduler.TryConsume(20f, true), "Resume decision x" + speed);
                Require(scheduler.SkippedDeadlines > 0, "Long frame drift x" + speed);
                Require(!scheduler.TryConsume(20f, true), "Burst after long frame x" + speed);
                Require(!scheduler.TryConsume(100f, false), "Match end x" + speed);
            }

            foreach (Faction owner in new[] { Faction.Blue, Faction.Red })
            {
                var episode = new RlEpisode();
                episode.Start(123);
                episode.RecordDecision(18);
                MatchResult win = owner == Faction.Blue
                    ? MatchResult.BlueVictory : MatchResult.RedVictory;
                MatchResult loss = owner == Faction.Blue
                    ? MatchResult.RedVictory : MatchResult.BlueVictory;
                Require(episode.EndMatch(owner, win) && episode.TerminalReward == 1f,
                    "Win perspective " + owner);
                Require(!episode.EndMatch(owner, loss) && episode.TerminalReward == 1f,
                    "Duplicate terminal reward " + owner);
                episode.Start(124);
                Require(episode.DecisionCount == 0 && episode.LastActionId == -1
                    && episode.TerminalReward == 0f && episode.EndKind == RlEpisodeEnd.None,
                    "Episode bookkeeping reset " + owner);
                Require(episode.EndMatch(owner, loss) && episode.TerminalReward == -1f,
                    "Loss perspective " + owner);
                episode.Start(125);
                Require(episode.EndDraw() && episode.TerminalReward == 0f
                    && episode.EndKind == RlEpisodeEnd.Termination, "Draw " + owner);
                episode.Start(126);
                Require(episode.Truncate() && episode.TerminalReward == 0f
                    && episode.EndKind == RlEpisodeEnd.Truncation, "Timeout " + owner);
                episode.Start(127);
                Require(episode.Abort() && episode.TerminalReward == 0f
                    && episode.EndKind == RlEpisodeEnd.Abort, "Abort " + owner);
            }
            Debug.Log("PHASE_81_STRUCTURAL_PASS: 96 features, 19 IDs, scheduler x1/x2/x5/x10, reward perspective/once, session reset.");
        }

        private static void ValidatePlayMode()
        {
            MatchManager match = MatchManager.Instance;
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            FogMemorySystem memory = FogMemorySystem.Instance;
            Require(match != null && vision != null && memory != null,
                "Map/vision/memory unavailable");
            RuleBasedCommander red = UnityEngine.Object
                .FindAnyObjectByType<RuleBasedCommander>();
            Require(red != null && red.Faction == Faction.Red, "Red commander missing");
            FactionPopulation bluePop = null;
            foreach (FactionPopulation pop in UnityEngine.Object
                .FindObjectsByType<FactionPopulation>())
                if (pop.Faction == Faction.Blue) { bluePop = pop; break; }
            Require(bluePop != null, "Blue population missing");
            var blueObject = new GameObject("Phase81TemporaryBlueKnowledge");
            float priorScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                FactionKnowledge blue = blueObject.AddComponent<FactionKnowledge>();
                blue.Configure(Faction.Blue, vision, memory,
                    match.BlueMainCastle.Stockpile, bluePop);
                ValidateHidden(Faction.Blue, blue, match, vision);
                ValidateHidden(Faction.Red, red.Knowledge, match, vision);
                Debug.Log("PHASE_81_HIDDEN_STATE_PASS: Blue and Red position/HP/order/target/combat mutations and Fog presentation toggle.");
            }
            finally
            {
                Time.timeScale = priorScale;
                UnityEngine.Object.DestroyImmediate(blueObject);
            }
        }

        private static void ValidateHidden(Faction owner, FactionKnowledge knowledge,
            MatchManager match, TeamVisionSystem vision)
        {
            knowledge.Refresh();
            UnitController hidden = null;
            foreach (UnitController unit in UnitController.ActiveUnits)
                if (unit != null && unit.IsAlive
                    && unit.Faction != owner
                    && FactionRelations.IsPlayable(unit.Faction)
                    && !vision.IsVisible(owner, unit.transform.position))
                { hidden = unit; break; }
            Require(hidden != null, "No hidden opponent for " + owner);
            var provider = new RlObservationProvider();
            var before = new float[96];
            var after = new float[96];
            Require(provider.TryBuild(knowledge, match, before),
                "Cannot observe " + owner);
            FogRenderer fog = UnityEngine.Object.FindAnyObjectByType<FogRenderer>();
            bool wasVisible = fog != null && fog.IsFogVisible;
            if (fog != null) fog.SetFogVisible(false);
            try
            {
                Require(provider.TryBuild(knowledge, match, after),
                    "Fog OFF observation failed " + owner);
                Equal(before, after, "Fog toggle " + owner);
            }
            finally { if (fog != null) fog.SetFogVisible(wasVisible); }

            HealthComponent health = hidden.GetComponent<HealthComponent>();
            CombatController combat = hidden.GetComponent<CombatController>();
            Require(health != null && combat != null, "Hidden test actor lacks combat");
            FieldInfo hpField = Field(typeof(HealthComponent), "currentHP");
            FieldInfo orderField = Field(typeof(CombatController), "order");
            FieldInfo targetField = Field(typeof(CombatController), "target");
            FieldInfo directiveField = Field(typeof(CombatController), "directive");
            FieldInfo stateField = Field(typeof(UnitController), "currentState");
            object hp = hpField.GetValue(health), order = orderField.GetValue(combat);
            object target = targetField.GetValue(combat);
            object directive = directiveField.GetValue(combat);
            object state = stateField.GetValue(hidden);
            Vector3 position = hidden.transform.position;
            try
            {
                hidden.transform.position = position + new Vector3(0.05f, 0.05f);
                hpField.SetValue(health, Math.Max(1, (int)hp - 1));
                orderField.SetValue(combat, Enum.ToObject(orderField.FieldType, 1));
                targetField.SetValue(combat, knowledge.OwnUnits.Count > 0
                    ? knowledge.OwnUnits[0] : null);
                directiveField.SetValue(combat, CombatDirective.Retreat);
                stateField.SetValue(hidden, UnitState.Moving);
                Require(!vision.IsVisible(owner, hidden.transform.position),
                    "Mutated opponent became visible " + owner);
                knowledge.Refresh();
                Require(provider.TryBuild(knowledge, match, after),
                    "Hidden mutation observation failed " + owner);
                Equal(before, after, "Hidden mutation " + owner);
            }
            finally
            {
                hidden.transform.position = position;
                hpField.SetValue(health, hp);
                orderField.SetValue(combat, order);
                targetField.SetValue(combat, target);
                directiveField.SetValue(combat, directive);
                stateField.SetValue(hidden, state);
                knowledge.Refresh();
            }
        }

        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, type.Name + "." + name + " missing");
            return field;
        }

        private static void Equal(float[] left, float[] right, string label)
        {
            Require(left.Length == right.Length, label + " length");
            for (int i = 0; i < left.Length; i++)
                Require(left[i] == right[i], label + " differs at index " + i);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
