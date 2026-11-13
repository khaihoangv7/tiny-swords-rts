#if UNITY_EDITOR
using System;
using TinyKingdom.AI;
using TinyKingdom.Match;
using TinyKingdom.UI;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    // A protocol probe, not PPO training. The Python client supplies actions
    // through the real ML-Agents communicator and checks terminal steps.
    [InitializeOnLoad]
    public static class Phase82MlValidation
    {
        private const string Pending = "TinyKingdom.Phase82MlValidation.Pending";
        private static double started;
        private static MatchManager previousMatch;
        private static int episodes;
        private static int decisions;
        private static int lastDecisionCount;
        private static int lastRequestCount;
        private static float lastRequestTime;
        private static bool reloadLocked;
        private static bool completed;
        private static double finishAfter;
        private static bool disconnectProbe;
        private static bool flushFinalTerminal;
        private static double nextProgressLog;
        private static bool enteredPlayMode;
        private static int abortLogCount;

        static Phase82MlValidation() => EditorApplication.update += Tick;

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 8.2 ML-Agents Protocol")]
        public static void RunBatch() => Begin(false, false);

        public static void RunDisconnectBatch() => Begin(true, false);

        // Leaves one more scene cycle so Academy can send the fifth terminal
        // step queued by EndEpisode to the official Trainer.
        public static void RunRewardBatch() => Begin(false, true);

        private static void Begin(bool forDisconnect, bool flushTerminal)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity");
            SessionState.SetBool(Pending, true);
            started = EditorApplication.timeSinceStartup;
            nextProgressLog = started + 5d;
            disconnectProbe = forDisconnect;
            flushFinalTerminal = flushTerminal;
            enteredPlayMode = false;
            abortLogCount = 0;
            Application.logMessageReceived -= CountTrainingAbort;
            Application.logMessageReceived += CountTrainingAbort;
            previousMatch = null;
            episodes = decisions = lastDecisionCount = 0;
            lastRequestCount = 0;
            lastRequestTime = 0f;
            completed = false;
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
            try
            {
                if (EditorApplication.timeSinceStartup >= nextProgressLog)
                {
                    nextProgressLog += 5d;
                    RlCommanderBridge current = UnityEngine.Object
                        .FindAnyObjectByType<RlCommanderBridge>();
                    Debug.Log("PHASE_82_LIFECYCLE: editorPlaying="
                        + EditorApplication.isPlaying + " editorPaused="
                        + EditorApplication.isPaused + " timeScale=" + Time.timeScale
                        + " match=" + MatchManager.Instance?.State
                        + " mode=" + current?.Mode + " decisions="
                        + current?.Episode.DecisionCount + " requests="
                        + current?.DecisionRequestCount + " communicator="
                        + Academy.Instance.IsCommunicatorOn);
                }
                if (EditorApplication.timeSinceStartup - started
                    > (disconnectProbe ? 100d : 180d))
                    throw new InvalidOperationException("Protocol probe timed out");
                if (completed)
                {
                    if (EditorApplication.timeSinceStartup < finishAfter) return;
                    SessionState.SetBool(Pending, false);
                    Application.logMessageReceived -= CountTrainingAbort;
                    UnlockReload();
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                    return;
                }
                if (!EditorApplication.isPlaying)
                {
                    if (enteredPlayMode)
                    {
                        if (disconnectProbe && abortLogCount == 1)
                        {
                            Debug.Log("PHASE_82_INTERRUPTION_PASS: one active "
                                + "episode aborted when Play Mode stopped; "
                                + "verify Trainer exit externally");
                            SessionState.SetBool(Pending, false);
                            Application.logMessageReceived -= CountTrainingAbort;
                            UnlockReload();
                            if (Application.isBatchMode) EditorApplication.Exit(0);
                            return;
                        }
                        throw new InvalidOperationException(
                            "Play Mode stopped before protocol probe completed; "
                            + "abort logs=" + abortLogCount);
                    }
                    return;
                }
                enteredPlayMode = true;
                if (Application.isBatchMode)
                {
                    foreach (TopLeftHudPresenter hud in UnityEngine.Object
                        .FindObjectsByType<TopLeftHudPresenter>()) hud.enabled = false;
                    foreach (ControlGroupHudPresenter hud in UnityEngine.Object
                        .FindObjectsByType<ControlGroupHudPresenter>()) hud.enabled = false;
                    EditorApplication.isPaused = false;
                }
                MatchManager match = MatchManager.Instance;
                RlCommanderBridge bridge = UnityEngine.Object
                    .FindAnyObjectByType<RlCommanderBridge>();
                RlMlAgent agent = UnityEngine.Object.FindAnyObjectByType<RlMlAgent>();
                if (match == null || bridge == null || agent == null) return;
                if (bridge.Mode == RlCommanderMode.TrainingAborted)
                    throw new InvalidOperationException("Training aborted: "
                        + bridge.FallbackReason);
                Require(bridge.Mode == RlCommanderMode.Training,
                    "Red did not enter Training mode");
                Require(Academy.Instance.IsCommunicatorOn,
                    "Python communicator is not connected");
                BehaviorParameters behavior = agent.GetComponent<BehaviorParameters>();
                Require(behavior != null
                    && behavior.BrainParameters.VectorObservationSize == 96
                    && behavior.BrainParameters.NumStackedVectorObservations == 1
                    && behavior.BrainParameters.ActionSpec.NumContinuousActions == 0
                    && behavior.BrainParameters.ActionSpec.NumDiscreteActions == 1
                    && behavior.BrainParameters.ActionSpec.BranchSizes[0] == 19,
                    "Agent observation/action spec mismatch");

                if (!ReferenceEquals(match, previousMatch))
                {
                    previousMatch = match;
                    episodes++;
                    lastDecisionCount = 0;
                    lastRequestCount = 0;
                    lastRequestTime = 0f;
                    Require(bridge.Episode.DecisionCount == 0
                        && bridge.Episode.TerminalReward == 0f
                        && bridge.Episode.LastActionId == -1,
                        "Episode state leaked across scene reload");
                    Debug.Log("PHASE_82_EPISODE_START: " + episodes
                        + " seed=" + RlMlBootstrap.CurrentSeed);
                }
                int requests = bridge.DecisionRequestCount;
                if (requests > lastRequestCount)
                {
                    Require(requests == lastRequestCount + 1,
                        "Multiple requests in one scheduler update");
                    float requestTime = bridge.LastDecisionRequestSimulationTime;
                    if (lastRequestCount > 0)
                    {
                        float interval = requestTime - lastRequestTime;
                        Require(interval >= 1.95f && interval < 3.5f,
                            "Invalid simulation request interval " + interval);
                        Debug.Log("PHASE_82_REQUEST_INTERVAL: " + interval);
                    }
                    lastRequestCount = requests;
                    lastRequestTime = requestTime;
                }
                int count = bridge.Episode.DecisionCount;
                if (count > lastDecisionCount)
                {
                    Require(count == lastDecisionCount + 1,
                        "Duplicate decision in one update");
                    Require(bridge.LastActionResult == RlActionResult.Accepted,
                        "Trainer action did not pass RlActionRouter");
                    RuleBasedCommander commander = bridge.GetComponent<RuleBasedCommander>();
                    Debug.Log("PHASE_82_TRAINER_ACTION: id="
                        + bridge.Episode.LastActionId + " result="
                        + bridge.LastActionResult + " trainSerial="
                        + (commander?.ProductionPolicy.SuccessfulTrainSerial ?? 0)
                        + " buildSerial="
                        + (commander?.BuildingPolicy.SuccessfulCommandSerial ?? 0));
                    Debug.Log("PHASE_82_ACTION_LATENCY: "
                        + (match.ElapsedGameplaySeconds
                            - bridge.LastDecisionRequestSimulationTime));
                    lastDecisionCount = count;
                    decisions++;
                }
                if (disconnectProbe) return;
                if (match.State != MatchState.Playing || count < 3) return;
                if (episodes == 5 && !flushFinalTerminal)
                    agent.StopAfterTerminal = true;
                Require(match.BlueMainCastle != null
                    && match.BlueMainCastle.ReceiveBuildingDamage(
                        Mathf.CeilToInt(match.BlueMainCastle.CurrentHP)),
                    "Could not terminate probe episode");
                Require(bridge.Episode.TerminalReward == 1f,
                    "Terminal reward was not +1 for Red");
                Debug.Log("PHASE_82_EPISODE_END: " + episodes
                    + " reward=" + bridge.Episode.TerminalReward);
                if (episodes < 5) return;
                Debug.Log("PHASE_82_PROTOCOL_PASS: five scene episodes, "
                    + decisions + " trainer actions, 96 observations, 19 actions, "
                    + "terminal reward +1 each.");
                completed = true;
                finishAfter = EditorApplication.timeSinceStartup
                    + (flushFinalTerminal ? 12d : 3d);
            }
            catch (Exception exception)
            {
                SessionState.SetBool(Pending, false);
                Application.logMessageReceived -= CountTrainingAbort;
                Debug.LogError("PHASE_82_PROTOCOL_FAIL: " + exception);
                UnlockReload();
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void UnlockReload()
        {
            if (!reloadLocked) return;
            reloadLocked = false;
            EditorApplication.UnlockReloadAssemblies();
        }

        private static void CountTrainingAbort(string condition,
            string stackTrace, LogType type)
        {
            if (condition.StartsWith("RL training aborted:",
                StringComparison.Ordinal)) abortLogCount++;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
