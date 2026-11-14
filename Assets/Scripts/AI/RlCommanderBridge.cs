using System;
using TinyKingdom.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyKingdom.AI
{
    public enum RlCommanderMode { RuleBased, RL, Fallback, Training, TrainingAborted }

    // A policy supplies one high-level action ID from the documented schema.
    // An ML-Agents Agent may implement this interface in a later phase.
    public interface IRlPolicy
    {
        bool TryChooseAction(float[] observation, out int actionId);
    }

    // ML-Agents delivers its action on a later Academy step. The bridge keeps
    // exactly one request pending and never substitutes a Rule-Based action.
    public interface IRlAsyncPolicy
    {
        bool IsAvailable { get; }
        bool RequestDecision(float[] observation, Action<int> onAction);
        void CompleteEpisode(float terminalReward, bool interrupted);
    }

    // Opt-in facade on the existing Commander object. It never issues direct
    // movement, combat, gathering or scene-object mutations.
    public sealed class RlCommanderBridge : MonoBehaviour
    {
        [SerializeField] private RuleBasedCommander commander;
        [SerializeField] private MonoBehaviour policySource = null;
        [SerializeField] private RlCommanderMode mode = RlCommanderMode.RuleBased;
        [SerializeField] private int episodeSeed;
        [SerializeField, Min(1f)] private float episodeLimitSeconds = 1800f;
        [SerializeField, Min(1f)] private float policyResponseTimeoutSeconds = 60f;

        private readonly RlObservationProvider observations = new();
        private readonly RlActionRouter actions = new();
        private readonly RlDecisionScheduler scheduler = new();
        private readonly RlEpisode episode = new();
        private readonly float[] observation = new float[RlObservationProvider.FeatureCount];
        private IRlPolicy injectedPolicy;
        private IRlAsyncPolicy injectedAsyncPolicy;
        private MatchManager subscribedMatch;
        private MatchManager episodeWorld;
        private bool pendingDecision;
        private bool pendingActionReady;
        private int pendingActionId;
        private float pendingSinceRealtime;
        private bool awaitingWorldReset;

        public RlCommanderMode Mode => mode;
        public RlEpisode Episode => episode;
        public RlDecisionScheduler Scheduler => scheduler;
        public int DecisionRequestCount { get; private set; }
        public float LastDecisionRequestSimulationTime { get; private set; }
        public RlActionResult LastActionResult { get; private set; }
        public string FallbackReason { get; private set; }

        public RlActionResult CanChooseAction(int id)
            => actions.CanChoose(id, commander, MatchManager.Instance);

        public bool TryBuildCurrentObservation(float[] destination)
        {
            MatchManager match = MatchManager.Instance;
            if (commander == null || match == null || destination == null) return false;
            commander.Knowledge.Refresh();
            return observations.TryBuild(commander.Knowledge, match, destination);
        }

        public void AbortTraining(string reason)
        {
            if (mode == RlCommanderMode.Training) HandlePolicyFailure(reason);
        }

        // The communicator can close before Academy exits Play Mode. This is
        // also used by OnDisable, so either path can win without a second abort.
        public void InterruptTraining(string reason)
        {
            if (mode != RlCommanderMode.Training) return;
            RlMlBootstrap.MarkTrainingAborted();
            FallbackReason = reason;
            pendingDecision = false;
            pendingActionReady = false;
            awaitingWorldReset = false;
            mode = RlCommanderMode.TrainingAborted;
            if (commander != null) commander.SetExternalDrive(true);
            if (episode.Abort())
                Debug.LogError("RL training aborted: " + reason
                    + " configuredSeed=" + RlMlBootstrap.CurrentSeed
                    + " decisions=" + episode.DecisionCount
                    + " reward=" + episode.TerminalReward, this);
            else
                Debug.Log("RL training interrupted: " + reason
                    + " episode=" + episode.EndKind, this);
        }

        public void Configure(RuleBasedCommander owner, IRlPolicy policy = null)
        {
            commander = owner;
            injectedPolicy = policy;
            injectedAsyncPolicy = null;
        }

        public void ConfigureAsync(RuleBasedCommander owner, IRlAsyncPolicy policy)
        {
            commander = owner;
            injectedPolicy = null;
            injectedAsyncPolicy = policy;
        }

        public void SetMode(RlCommanderMode requested)
        {
            if (RlMlBootstrap.IsTrainingInterrupted
                && requested != RlCommanderMode.TrainingAborted) return;
            if (mode == requested) return;
            if (episode.Active) episode.Abort();
            mode = requested;
            pendingDecision = false;
            pendingActionReady = false;
            awaitingWorldReset = false;
            if (commander != null)
            {
                commander.SetExternalDrive(false);
                commander.ProductionPolicy.ClearExternalUnit();
                commander.BuildingPolicy.ClearExternalBuilding();
                commander.EconomicDemand.ClearExternalPriority();
                commander.StrategicIntent.RequestImmediateReevaluation();
            }
            FallbackReason = null;
        }

        public bool BeginEpisode(int seed)
        {
            MatchManager match = MatchManager.Instance;
            if (RlMlBootstrap.IsTrainingInterrupted
                || mode != RlCommanderMode.RL && mode != RlCommanderMode.Training
                || match == null
                || match.State != MatchState.Playing || commander == null
                || !commander.IsConfigured || !HasPolicy()
                || ReferenceEquals(match, episodeWorld)) return false;
            episodeSeed = seed;
            episodeWorld = match;
            episode.Start(seed);
            scheduler.Reset(match.ElapsedGameplaySeconds);
            Array.Clear(observation, 0, observation.Length);
            DecisionRequestCount = 0;
            LastDecisionRequestSimulationTime = 0f;
            pendingDecision = false;
            pendingActionReady = false;
            awaitingWorldReset = false;
            commander.SetExternalDrive(true);
            Subscribe(match);
            return true;
        }

        // A new world requires a full scene bootstrap. Clearing only RL counters
        // would retain units, reservations, knowledge and match callbacks.
        public bool ReloadSceneForNextEpisode(int nextSeed)
        {
            if (RlMlBootstrap.IsTrainingInterrupted
                || SceneManager.GetActiveScene().buildIndex < 0) return false;
            if (episode.Active) episode.Abort();
            UnityEngine.Random.InitState(nextSeed);
            episodeSeed = nextSeed;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex,
                LoadSceneMode.Single);
            return true;
        }

        private void Awake()
        {
            if (commander == null) commander = GetComponent<RuleBasedCommander>();
        }

        private void Update()
        {
            if (RlMlBootstrap.IsTrainingInterrupted)
            {
                if (mode == RlCommanderMode.Training)
                    InterruptTraining("Communicator shutdown requested");
                return;
            }
            if (mode != RlCommanderMode.RL && mode != RlCommanderMode.Training) return;
            MatchManager match = MatchManager.Instance;
            if (match == null || commander == null) return;
            if (!HasPolicy())
            {
                HandlePolicyFailure("Policy unavailable");
                return;
            }
            if (match.State == MatchState.Ended)
            {
                if (episode.EndMatch(commander.Faction, match.Result))
                    CompleteAsyncEpisode(match.Result == MatchResult.Timeout);
                commander.SetExternalDrive(false);
                return;
            }
            if (match.State != MatchState.Playing || Time.timeScale <= 0f) return;
            if (awaitingWorldReset) return;
            if (!episode.Active && !BeginEpisode(episodeSeed))
            {
                HandlePolicyFailure("World reset required before another episode");
                return;
            }
            IRlAsyncPolicy asyncPolicy = ResolveAsyncPolicy();
            if (asyncPolicy != null && !asyncPolicy.IsAvailable)
            {
                HandlePolicyFailure("Trainer or inference model unavailable");
                return;
            }
            if (!QlExperiment.IsActive && match.ElapsedGameplaySeconds >= episodeLimitSeconds)
            {
                episode.Truncate();
                if (asyncPolicy != null)
                {
                    awaitingWorldReset = true;
                    CompleteAsyncEpisode(true);
                }
                else
                {
                    mode = RlCommanderMode.Fallback;
                    FallbackReason = "Episode time limit";
                }
                commander.SetExternalDrive(false);
                return;
            }
            if (pendingDecision)
            {
                if (pendingActionReady)
                {
                    pendingDecision = false;
                    pendingActionReady = false;
                    ApplyAction(pendingActionId, match);
                }
                else if (Time.realtimeSinceStartup - pendingSinceRealtime
                    >= policyResponseTimeoutSeconds)
                    HandlePolicyFailure("Policy response timeout");
                return;
            }
            if (!scheduler.TryConsume(match.ElapsedGameplaySeconds, true)) return;
            commander.Knowledge.Refresh();
            if (!observations.TryBuild(commander.Knowledge, match, observation))
            {
                HandlePolicyFailure("Invalid observation");
                return;
            }
            DecisionRequestCount++;
            LastDecisionRequestSimulationTime = match.ElapsedGameplaySeconds;
            if (asyncPolicy != null)
            {
                pendingDecision = true;
                pendingSinceRealtime = Time.realtimeSinceStartup;
                try
                {
                    if (!asyncPolicy.RequestDecision(observation, ReceiveAsyncAction))
                        HandlePolicyFailure("Policy could not request a decision");
                }
                catch (Exception exception)
                {
                    HandlePolicyFailure("Policy exception: " + exception.GetType().Name);
                }
                return;
            }
            int id;
            try
            {
                if (!ResolvePolicy().TryChooseAction(observation, out id))
                {
                    HandlePolicyFailure("Policy timeout or unavailable");
                    return;
                }
            }
            catch (Exception exception)
            {
                HandlePolicyFailure("Policy exception: " + exception.GetType().Name);
                return;
            }
            ApplyAction(id, match);
        }

        private void ReceiveAsyncAction(int actionId)
        {
            if (RlMlBootstrap.IsTrainingInterrupted
                || !pendingDecision || pendingActionReady) return;
            pendingActionId = actionId;
            pendingActionReady = true;
        }

        private void ApplyAction(int id, MatchManager match)
        {
            if (RlMlBootstrap.IsTrainingInterrupted) return;
            uint before = commander.ProductionPolicy.SuccessfulTrainSerial;
            LastActionResult = actions.TryPrepare(id, commander, match,
                out StrategicIntent? intent);
            if (LastActionResult != RlActionResult.Accepted)
            {
                QlExperiment.Active?.RecordAction(commander, id, LastActionResult, false);
                if (QlExperiment.IsActive && id >= 0 && id < RlActionRouter.ActionCount)
                    return;
                HandlePolicyFailure("Invalid action: " + LastActionResult);
                return;
            }
            var context = new CommanderDecisionContext(commander.Faction,
                Time.time, commander.Knowledge, commander.CommandService);
            if (!commander.ThinkExternal(context, intent))
            {
                LastActionResult = RlActionResult.PolicyRejected;
                QlExperiment.Active?.RecordAction(commander, id, LastActionResult, false);
                if (QlExperiment.IsActive) return;
                HandlePolicyFailure("Strategic intent rejected");
                return;
            }
            episode.RecordDecision(id);
            QlExperiment.Active?.RecordAction(commander, id, LastActionResult,
                commander.ProductionPolicy.SuccessfulTrainSerial != before);
        }

        private IRlPolicy ResolvePolicy() => injectedPolicy ?? policySource as IRlPolicy;
        private IRlAsyncPolicy ResolveAsyncPolicy()
            => injectedAsyncPolicy ?? policySource as IRlAsyncPolicy;
        private bool HasPolicy() => ResolveAsyncPolicy() != null || ResolvePolicy() != null;

        private void CompleteAsyncEpisode(bool interrupted)
        {
            if (RlMlBootstrap.IsTrainingInterrupted) return;
            IRlAsyncPolicy policy = ResolveAsyncPolicy();
            if (policy == null) return;
            try { policy.CompleteEpisode(episode.TerminalReward, interrupted); }
            catch (Exception exception)
            { HandlePolicyFailure("Episode completion failed: " + exception.GetType().Name); }
        }

        private void HandlePolicyFailure(string reason)
        {
            if (mode != RlCommanderMode.Training)
            {
                EnterFallback(reason);
                return;
            }
            FallbackReason = reason;
            if (episode.Active) episode.Abort();
            pendingDecision = false;
            pendingActionReady = false;
            mode = RlCommanderMode.TrainingAborted;
            RlMlBootstrap.MarkTrainingAborted();
            // Keep the normal Commander suppressed: a Rule-Based action would
            // contaminate the failed training episode.
            if (commander != null) commander.SetExternalDrive(true);
            Debug.LogError("RL training aborted: " + reason, this);
            if (Application.isBatchMode) Application.Quit(1);
            else Debug.Break();
        }

        private void EnterFallback(string reason)
        {
            FallbackReason = reason;
            if (episode.Active) episode.Abort();
            pendingDecision = false;
            pendingActionReady = false;
            mode = RlCommanderMode.Fallback;
            if (commander == null) return;
            commander.ProductionPolicy.ClearExternalUnit();
            commander.BuildingPolicy.ClearExternalBuilding();
            commander.EconomicDemand.ClearExternalPriority();
            commander.StrategicIntent.RequestImmediateReevaluation();
            commander.SetExternalDrive(false);
        }

        private void Subscribe(MatchManager match)
        {
            if (subscribedMatch == match) return;
            if (subscribedMatch != null) subscribedMatch.MatchEnded -= OnMatchEnded;
            subscribedMatch = match;
            subscribedMatch.MatchEnded += OnMatchEnded;
        }

        private void OnMatchEnded(MatchResult result)
        {
            if (RlMlBootstrap.IsTrainingInterrupted) return;
            if (commander != null && episode.EndMatch(commander.Faction, result))
                CompleteAsyncEpisode(result == MatchResult.Timeout);
        }

        private void OnDisable()
        {
            if (subscribedMatch != null) subscribedMatch.MatchEnded -= OnMatchEnded;
            subscribedMatch = null;
            if (mode == RlCommanderMode.Training && episode.Active)
            {
                InterruptTraining("Training object disabled before episode termination");
                return;
            }
            if (episode.Active) episode.Abort();
            if (commander != null && mode != RlCommanderMode.TrainingAborted)
                commander.SetExternalDrive(false);
        }
    }
}
