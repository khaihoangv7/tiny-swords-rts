using System;
using System.Collections.Concurrent;
using System.Threading;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace TinyKingdom.AI
{
    // ML-Agents transport for the Phase 8.1 bridge. It never reads game state
    // or executes gameplay commands; both remain owned by that bridge.
    public sealed class RlMlAgent : Agent, IRlAsyncPolicy
    {
        private readonly float[] observation = new float[RlObservationProvider.FeatureCount];
        private RlCommanderBridge bridge;
        private Action<int> onAction;
        private bool training;
        private bool finishingEpisode;
        private bool resetRequested;
        // Used by the Editor protocol probe to leave the final terminal step
        // available to Python before that probe closes the Editor.
        public bool StopAfterTerminal { get; set; }

        public bool IsAvailable
        {
            get
            {
                if (training) return !RlMlBootstrap.IsTrainingInterrupted
                    && Academy.Instance.IsCommunicatorOn;
                BehaviorParameters behavior = GetComponent<BehaviorParameters>();
                return behavior != null && behavior.Model != null;
            }
        }

        public void Configure(RlCommanderBridge owner, bool forTraining)
        {
            bridge = owner;
            training = forTraining;
        }

        bool IRlAsyncPolicy.RequestDecision(float[] values, Action<int> callback)
        {
            if (RlMlBootstrap.IsTrainingInterrupted || values == null
                || values.Length != observation.Length || callback == null
                || onAction != null || !IsAvailable) return false;
            Array.Copy(values, observation, values.Length);
            onAction = callback;
            RequestDecision();
            return true;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            for (int i = 0; i < observation.Length; i++)
                sensor.AddObservation(observation[i]);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (bridge == null) return;
            // Wait remains available. Never infer action validity from hidden
            // scene objects or mutate a policy while generating the mask.
            for (int id = 1; id < RlActionRouter.ActionCount; id++)
                actionMask.SetActionEnabled(0, id,
                    bridge.CanChooseAction(id) == RlActionResult.Accepted);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            Action<int> callback = onAction;
            if (callback == null) return;
            onAction = null;
            if (training && RlMlBootstrap.IsTrainingInterrupted) return;
            callback(actions.DiscreteActions.Length == 1
                ? actions.DiscreteActions[0] : -1);
        }

        public override void OnEpisodeBegin()
        {
            onAction = null;
            if (training && RlMlBootstrap.IsTrainingInterrupted) return;
            if (!finishingEpisode) return;
            finishingEpisode = false;
            resetRequested = !StopAfterTerminal;
        }

        public void CompleteEpisode(float terminalReward, bool interrupted)
        {
            if (training && RlMlBootstrap.IsTrainingInterrupted) return;
            if (bridge == null || finishingEpisode || resetRequested) return;
            if (training && !IsAvailable)
            {
                bridge.AbortTraining("Trainer disconnected before terminal reward");
                return;
            }
            // Agent.NotifyAgentDone collects one final vector. Refresh it from
            // the same legal provider so the terminal step is current.
            if (!bridge.TryBuildCurrentObservation(observation))
            {
                bridge.AbortTraining("Terminal observation unavailable");
                return;
            }
            LazyInitialize();
            onAction = null;
            finishingEpisode = true;
            if (terminalReward != 0f) AddReward(terminalReward);
            if (interrupted) EpisodeInterrupted();
            else EndEpisode();
        }

        private void Update()
        {
            if (training && RlMlBootstrap.IsTrainingInterrupted) return;
            if (!resetRequested) return;
            resetRequested = false;
            int nextSeed = RlMlBootstrap.AdvanceSeed();
            if (bridge == null || !bridge.ReloadSceneForNextEpisode(nextSeed))
                bridge?.AbortTraining("Scene reload failed after terminal step");
        }
    }

    // Explicit command-line opt-in preserves the normal Rule-Based game.
    // One Red Agent is attached to the existing Commander root only in these modes.
    public static class RlMlBootstrap
    {
        private static bool parsed;
        private static bool training;
        private static bool inference;
        private static int trainingAborted;
        private static int sessionId;
        private static int seed;

        // SubsystemRegistration runs for each new Play Mode session even when
        // Enter Play Mode Options disable domain reload. Scene reloads do not
        // reach this boundary, so an interrupted session stays interrupted.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginRuntimeSession()
        {
            parsed = training = inference = false;
            Volatile.Write(ref trainingAborted, 0);
            seed = 0;
            Interlocked.Increment(ref sessionId);
        }

        public static bool IsTrainingInterrupted
            => Volatile.Read(ref trainingAborted) != 0;

        internal static int SessionId => Volatile.Read(ref sessionId);

        public static bool MarkTrainingAborted()
            => Interlocked.Exchange(ref trainingAborted, 1) == 0;

        public static int CurrentSeed
        {
            get { ParseArgs(); return seed; }
        }

        public static int AdvanceSeed()
        {
            ParseArgs();
            return ++seed;
        }

        public static bool AttachIfRequested(GameObject root,
            RuleBasedCommander commander, RlCommanderBridge bridge, bool demo = false)
        {
            RlCommunicatorCallbackProbe.RegisterIfTrainingRequested();
            ParseArgs();
            if (!demo && !training && !inference) return false;
            bool useModel = demo || inference;
            bool useTrainer = training && !demo;
            if (useTrainer && IsTrainingInterrupted)
            {
                // An interrupted scene must not register a fresh Trainer Agent
                // or allow the ordinary Commander to take over silently.
                bridge.SetMode(RlCommanderMode.TrainingAborted);
                commander.SetExternalDrive(true);
                return false;
            }
            ModelAsset model = null;
            if (useModel)
            {
                model = Resources.Load<ModelAsset>("RL/TinyKingdomStrategicRed");
                if (model == null)
                {
                    Debug.LogError("RL inference requested but Resources/RL/"
                        + "TinyKingdomStrategicRed model is missing.");
                    return false;
                }
            }
            BehaviorParameters behavior = root.AddComponent<BehaviorParameters>();
            behavior.BrainParameters.VectorObservationSize = RlObservationProvider.FeatureCount;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(
                RlActionRouter.ActionCount);
            behavior.BehaviorName = "TinyKingdomStrategicRed";
            if (useModel) behavior.Model = model;
            behavior.BehaviorType = useModel
                ? BehaviorType.InferenceOnly : BehaviorType.Default;
            RlMlAgent agent = root.AddComponent<RlMlAgent>();
            agent.Configure(bridge, useTrainer);
            agent.StopAfterTerminal = demo;
            bridge.ConfigureAsync(commander, agent);
            bridge.SetMode(useTrainer ? RlCommanderMode.Training : RlCommanderMode.RL);
            return true;
        }

        private static void ParseArgs()
        {
            if (parsed) return;
            parsed = true;
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg == "-tk-rl-training") training = true;
                else if (arg == "-tk-rl-inference") inference = true;
                else if (arg.StartsWith("-tk-rl-seed=", StringComparison.Ordinal)
                    && int.TryParse(arg.Substring(12), out int value)) seed = value;
            }
            if (training && inference)
            {
                Debug.LogError("Choose one RL mode: training or inference.");
                training = inference = false;
            }
            if (training && Academy.Instance.IsCommunicatorOn)
                seed = Mathf.RoundToInt(Academy.Instance.EnvironmentParameters
                    .GetWithDefault("tinykingdom_seed", seed));
        }
    }

    // The factory callback is the training shutdown signal. Detailed lifecycle
    // logging remains opt-in via -tk-rl-callback-probe.
    internal static class RlCommunicatorCallbackProbe
    {
        private struct EventRecord
        {
            public int Sequence;
            public string Name;
            public string Utc;
            public int Thread;
            public bool MainThread;
            public string PlayMode;
            public string Mode;
            public string Episode;
        }

        private static readonly ConcurrentQueue<EventRecord> events = new();
        private static int sequence;
        private static int mainThread;
        private static int registeredSession;
        private static bool registered;
        private static bool verbose;

        internal static void RegisterIfTrainingRequested()
        {
            if (registered) return;
            bool trainingRequested = false;
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg == "-tk-rl-callback-probe") verbose = true;
                if (arg == "-tk-rl-training") trainingRequested = true;
            }
            if (!trainingRequested) return;

            registered = true;
            mainThread = Thread.CurrentThread.ManagedThreadId;
            registeredSession = RlMlBootstrap.SessionId;
            sequence = 0;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
            if (verbose)
            {
#if UNITY_EDITOR
                EditorApplication.update += Flush;
#endif
                Application.logMessageReceivedThreaded += OnLog;
                Application.quitting += OnApplicationQuitting;
#if UNITY_EDITOR
                EditorApplication.quitting += OnEditorQuitting;
#endif
            }

            if (Academy.IsInitialized)
            {
                RlMlBootstrap.MarkTrainingAborted();
                Debug.LogError("RL training interrupted: communicator factory registration too late");
                if (verbose) Record("FactoryRegistrationTooLate");
                Flush();
                return;
            }

            int session = registeredSession;
            CommunicatorFactory.Register<RpcCommunicator>(() =>
            {
                RpcCommunicator communicator = RpcCommunicator.Create();
                communicator.QuitCommandReceived += () => OnCommunicatorQuit(session);
                if (verbose) Record("CommunicatorCreated");
                return communicator;
            });
            if (verbose) Record("CommunicatorFactoryRegistered");
            Flush();
        }

        private static void OnCommunicatorQuit(int session)
        {
            // A callback from a previous Play Mode session must not poison a
            // newly initialized session. The latch itself is thread-safe;
            // Unity objects are only touched if the callback is on main thread.
            if (session != RlMlBootstrap.SessionId) return;
            if (verbose) Record("CommunicatorShutdownRequested");
            if (!RlMlBootstrap.MarkTrainingAborted())
            {
                if (verbose) Record("DuplicateShutdownIgnored");
                Flush();
                return;
            }
            if (verbose) Record("TrainingInterrupted");
            if (Thread.CurrentThread.ManagedThreadId != mainThread) return;
            RlCommanderBridge bridge = UnityEngine.Object
                .FindAnyObjectByType<RlCommanderBridge>();
            bridge?.InterruptTraining("Communicator shutdown requested");
            Flush();
        }

        private static void OnApplicationQuitting()
            => Record("ApplicationQuitting");

#if UNITY_EDITOR
        private static void OnEditorQuitting()
            => Record("EditorQuitting");

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (verbose && state == PlayModeStateChange.ExitingPlayMode)
                Record("PlayModeExiting");
            else if (verbose && state == PlayModeStateChange.EnteredEditMode)
                Record("PlayModeEnteredEdit");
            Flush();
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= Flush;
            Application.logMessageReceivedThreaded -= OnLog;
            Application.quitting -= OnApplicationQuitting;
            EditorApplication.quitting -= OnEditorQuitting;
            registered = false;
            verbose = false;
        }
#endif

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (condition.StartsWith("RL training aborted:",
                StringComparison.Ordinal)) Record("EpisodeAbort");
        }

        private static void Record(string name)
        {
            int thread = Thread.CurrentThread.ManagedThreadId;
            bool onMainThread = thread == mainThread;
            string playMode = "unknown";
            string mode = "unknown";
            string episode = "unknown";
            if (onMainThread)
            {
                playMode = Application.isPlaying.ToString();
                RlCommanderBridge bridge = UnityEngine.Object
                    .FindAnyObjectByType<RlCommanderBridge>();
                if (bridge != null)
                {
                    mode = bridge.Mode.ToString();
                    episode = "active=" + bridge.Episode.Active
                        + ",internalSeed=" + bridge.Episode.Seed
                        + ",configuredSeed="
                        + (Academy.IsInitialized
                            ? RlMlBootstrap.CurrentSeed.ToString() : "unknown")
                        + ",decisions=" + bridge.Episode.DecisionCount;
                }
            }
            events.Enqueue(new EventRecord
            {
                Sequence = Interlocked.Increment(ref sequence),
                Name = name,
                Utc = DateTime.UtcNow.ToString("O"),
                Thread = thread,
                MainThread = onMainThread,
                PlayMode = playMode,
                Mode = mode,
                Episode = episode
            });
        }

        private static void Flush()
        {
            if (!verbose || Thread.CurrentThread.ManagedThreadId != mainThread) return;
            while (events.TryDequeue(out EventRecord item))
                Debug.Log("PHASE_82_CALLBACK_PROBE seq=" + item.Sequence
                    + " utc=" + item.Utc + " event=" + item.Name
                    + " thread=" + item.Thread + " main=" + item.MainThread
                    + " playing=" + item.PlayMode + " mode=" + item.Mode
                    + " episode=" + item.Episode);
        }
    }
}
