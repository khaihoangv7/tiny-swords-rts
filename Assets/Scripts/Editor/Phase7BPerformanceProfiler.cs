using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TinyKingdom.AI;
using TinyKingdom.Audio;
using TinyKingdom.Buildings;
using TinyKingdom.Debugging;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Vision;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class Phase7BPerformanceProfiler
    {
        private const string BatchArgument = "-phase7BProfile";
        private const string ScenePath = "Assets/Scenes/Map.unity";
        private const float DefaultCaptureSeconds = 30f;
        private const float DefaultSpikeThresholdMs = 33.3f;
        private const int MaximumSpikeSnapshots = 8;
        private const double SpikeSampleIntervalSeconds = 1d;
        private const double LivenessSampleIntervalSeconds = 5d;
        private const int MaximumLivenessSnapshots = 80;

        private static readonly string[] MarkerNames =
        {
            "TinyKingdom.AI.CommanderTick",
            "TinyKingdom.AI.KnowledgeRefresh",
            "TinyKingdom.AI.RoleSync",
            "TinyKingdom.AI.BuildingPolicy",
            "TinyKingdom.AI.BuildPlacementSearch",
            "TinyKingdom.AI.ProductionPolicy",
            "TinyKingdom.AI.EconomyPolicy",
            "TinyKingdom.AI.SafeResourceSearch",
            "TinyKingdom.AI.ScoutingPolicy",
            "TinyKingdom.AI.ScoutFrontierScoring",
            "TinyKingdom.AI.ReconScoring",
            "TinyKingdom.AI.ObservationPointGeneration",
            "TinyKingdom.AI.MilitaryPolicy",
            "TinyKingdom.AI.StrategicSnapshot.Build",
            "TinyKingdom.AI.StrategicIntent.Evaluate",
            "TinyKingdom.AI.StrategicTarget.Score",
            "TinyKingdom.AI.Staging.Select",
            "TinyKingdom.AI.TacticalArmy.Evaluate",
            "TinyKingdom.AI.TargetAssignment",
            "TinyKingdom.AI.FormationDistribution",
            "TinyKingdom.AI.CohesionEvaluation",
            "TinyKingdom.AI.LocalRegroupEvaluation",
            "TinyKingdom.AI.MilitaryLiveness",
            "TinyKingdom.AI.ThreatEvaluation",
            "TinyKingdom.AI.ArmyRegroup",
            "TinyKingdom.AI.CommandDispatch",
            "TinyKingdom.AI.EconomicDemand",
            "TinyKingdom.AI.WorkerRebalance",
            "TinyKingdom.AI.EconomicClusterRefresh",
            "TinyKingdom.AI.EconomicOutpostEvaluation",
            "TinyKingdom.AI.EnemyCompositionEvaluation",
            "TinyKingdom.AI.DesiredComposition",
            "TinyKingdom.AI.ProductionCandidateScore",
            "TinyKingdom.AI.StrategicSafetyMapUpdate",
            "TinyKingdom.Buildings.TrapValidation",
            "TinyKingdom.Economy.PawnTick",
            "TinyKingdom.Economy.CargoRecovery",
            "TinyKingdom.Audio.PlaySfx",
            "TinyKingdom.Pathfinding.FindPath",
            "TinyKingdom.Fog.TeamVisionRecompute",
            "TinyKingdom.Fog.MemoryRefresh",
            "TinyKingdom.Fog.WorldVisibilityRefresh"
        };

        private static Capture activeCapture;
        private static bool batchRequested;
        private static double batchStartupDeadline;

        static Phase7BPerformanceProfiler()
        {
            batchRequested = HasArgument(BatchArgument);
            if (batchRequested) EditorApplication.delayCall += BeginBatch;
        }

        [MenuItem("Tools/Tiny Kingdom/Profile Phase 7B Commander")]
        public static void RunInteractive()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 7B performance profiling in Play Mode.");
            BeginCapture(DefaultCaptureSeconds, ReportPath());
        }

        [MenuItem("Tools/Tiny Kingdom/Profile Phase 7E.1 x10 Stability (5 minutes)")]
        public static void RunExtendedStabilityInteractive()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Enter Play Mode, enable Observer, select x10, then start this capture.");
            string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Logs/Phase7E1FullMatchStability.json"));
            BeginCapture(300f, path);
        }

        [MenuItem("Tools/Tiny Kingdom/Profile Phase 7E.3 Reactive Production x10 (5 minutes)")]
        public static void RunReactiveProductionInteractive()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Enter Play Mode, enable Observer, select x10, then start this capture.");
            string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Logs/Phase7E3ReactiveProduction.json"));
            BeginCapture(300f, path);
        }

        [MenuItem("Tools/Tiny Kingdom/Profile Phase 7E.5 Liveness x10 (5 minutes)")]
        public static void RunStrategicLivenessInteractive()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Enter Play Mode, enable Observer, select x10, then start this capture.");
            string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Logs/Phase7E5StrategicLiveness.json"));
            BeginCapture(300f, path);
        }

        private static void BeginBatch()
        {
            if (!batchRequested || activeCapture != null) return;
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EditorApplication.playModeStateChanged -= HandlePlayMode;
                EditorApplication.playModeStateChanged += HandlePlayMode;
                EditorApplication.isPlaying = true;
                return;
            }
            if (batchStartupDeadline <= 0d)
                batchStartupDeadline = EditorApplication.timeSinceStartup + 30d;
            if (!ApplyBatchDebugOptions())
            {
                if (EditorApplication.timeSinceStartup >= batchStartupDeadline)
                {
                    Debug.LogError("Batch profile speed controller did not initialize. "
                        + $"Playing={EditorApplication.isPlaying}, "
                        + $"Scene={EditorSceneManager.GetActiveScene().path}, "
                        + $"GameTime={Time.time:0.0}, "
                        + $"Commander={UnityEngine.Object.FindAnyObjectByType<RuleBasedCommander>() != null}.");
                    EditorApplication.Exit(2);
                }
                else EditorApplication.delayCall += BeginBatch;
                return;
            }
            BeginCapture(ReadDuration(), ReportPath());
        }

        private static bool ApplyBatchDebugOptions()
        {
            string speedArgument = ArgumentValue("-phase7BProfileSpeed");
            if (speedArgument == null)
            {
                if (HasArgument("-phase7BProfileObserver"))
                    DebugObserverView.SetEnabled(true);
                return true;
            }
            if (!float.TryParse(speedArgument, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float speed)
                || (speed != 1f && speed != 5f && speed != 10f))
                throw new InvalidOperationException(
                    "-phase7BProfileSpeed must be 1, 5, or 10.");

            MatchSpeedController controller = MatchSpeedController.Instance;
            if (controller == null) return false;
            if (!controller.SetSpeed(speed))
                throw new InvalidOperationException(
                    $"Could not set batch profile speed to x{speed}.");
            if (HasArgument("-phase7BProfileObserver"))
                DebugObserverView.SetEnabled(true);
            return true;
        }

        private static void HandlePlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= HandlePlayMode;
            batchStartupDeadline = EditorApplication.timeSinceStartup + 30d;
            EditorApplication.delayCall += BeginBatch;
        }

        private static void BeginCapture(float seconds, string path)
        {
            if (activeCapture != null) return;
            activeCapture = new Capture(Mathf.Max(5f, seconds), path, batchRequested);
            activeCapture.Start();
        }

        private static string ReportPath()
        {
            string value = ArgumentValue("-phase7BProfileOutput");
            return string.IsNullOrWhiteSpace(value)
                ? Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Logs/Phase7BPerformance.json"))
                : Path.GetFullPath(value);
        }

        private static float ReadDuration()
            => float.TryParse(ArgumentValue("-phase7BProfileSeconds"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value : DefaultCaptureSeconds;

        private static float ReadSpikeThreshold()
            => float.TryParse(ArgumentValue("-phase7BSpikeThresholdMs"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? Mathf.Max(1f, value) : DefaultSpikeThresholdMs;

        private static string ArgumentValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[i + 1];
            return null;
        }

        private static bool HasArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        [Serializable]
        private sealed class PerformanceReport
        {
            public string capturedUtc;
            public float durationSeconds;
            public int frameCount;
            public double averageFrameMs;
            public double worstFrameMs;
            public int commanderFrames;
            public double averageCommanderFrameMs;
            public double worstCommanderFrameMs;
            public long totalPathRequests;
            public double pathRequestsPerSecond;
            public long gameplayPathRequests;
            public long commanderMovePathRequests;
            public long commanderGatherPathRequests;
            public long commanderBuildPathRequests;
            public long commanderRallyPathRequests;
            public long commanderScoutPathRequests;
            public long economyReturnPathRequests;
            public long militaryMovePathRequests;
            public long militaryFormationPathRequests;
            public long militaryCombatPathRequests;
            public long militaryRegroupPathRequests;
            public long resourceFallbackPathRequests;
            public long moveCommandAttempts;
            public long scoutMoveCommandAttempts;
            public long gatherCommandAttempts;
            public long returnResourceCommandAttempts;
            public long buildCommandAttempts;
            public long attackCommandAttempts;
            public long trainCommandAttempts;
            public long rallyCommandAttempts;
            public long constructionSiteBuilderCommands;
            public long deduplicatedConstructionCommands;
            public long constructionSiteBuilderPathRequests;
            public long buildingCandidateEvaluations;
            public long economyReassignments;
            public long economicClusterRefreshes;
            public int economicClusterCount;
            public long productionEnqueueAttempts;
            public long successfulProductionEnqueues;
            public long rejectedOrDedupedProductionAttempts;
            public long productionInfrastructureRequests;
            public double productionEnqueueAttemptsPerSecond;
            public double successfulProductionEnqueuesPerSecond;
            public double rejectedOrDedupedProductionAttemptsPerSecond;
            public double productionInfrastructureRequestsPerSecond;
            public double moveCommandsPerSecond;
            public double scoutMoveCommandsPerSecond;
            public double gatherCommandsPerSecond;
            public double returnResourceCommandsPerSecond;
            public double buildCommandsPerSecond;
            public double attackCommandsPerSecond;
            public double trainCommandsPerSecond;
            public double rallyCommandsPerSecond;
            public double constructionBuilderCommandsPerSecond;
            public double constructionBuilderPathRequestsPerSecond;
            public double buildingCandidatesPerSecond;
            public long maximumPathRequestsInFrame;
            public long maximumAiCommandsInFrame;
            public double commanderPathRequestsPerTick;
            public bool baseFullyDeveloped;
            public double averageGcAllocatedBytes;
            public long maximumGcAllocatedBytes;
            public int finalAudioPoolSize;
            public int maximumAudioPoolSize;
            public int finalActiveAudioSources;
            public int maximumActiveAudioSources;
            public int audioPoolGrowthCount;
            public int knownResourceCount;
            public int knownEnemyBuildingCount;
            public int ownUnitCount;
            public int finalDropOffRegistryCount;
            public int maximumDropOffRegistryCount;
            public int scoutFrontierCandidateCount;
            public int scoutScoredFrontierCount;
            public int scoutStaleCandidateCount;
            public int finalFogPresentationGateCount;
            public int maximumFogPresentationGateCount;
            public float spikeThresholdMs;
            public FrameSpikeSnapshot peakSpike;
            public List<FrameSpikeSnapshot> spikes = new();
            public List<LivenessSnapshot> liveness = new();
            public List<MarkerReport> markers = new();
        }

        [Serializable]
        private sealed class FrameSpikeSnapshot
        {
            public double frameMs;
            public float gameTime;
            public long gcBytes;
            public long pathRequests;
            public long aiCommands;
            public string currentIntent;
            public string tacticalState;
            public int armySize;
            public int knownResources;
            public int knownEnemyBuildings;
            public int scoutCandidates;
            public int tacticalCandidates;
            public int targetAssignments;
            public int audioPoolSize;
            public int activeAudioSources;
            public string largestAiSubsystem;
            public double largestAiSubsystemMs;
            public double strategicMs;
            public double tacticalMs;
            public double scoutMs;
            public double economyMs;
            public double productionMs;
            public double safetyMapMs;
            public double resourceClusterMs;
        }

        [Serializable]
        private sealed class LivenessSnapshot
        {
            public double elapsedRealSeconds;
            public float gameTime;
            public float matchSpeed;
            public long commanderThinkTick;
            public string intent;
            public float intentAge;
            public string armyState;
            public string tacticalState;
            public int armySize;
            public int offensiveParticipants;
            public float armyX;
            public float armyY;
            public bool hasStrategicTarget;
            public string strategicTargetId;
            public bool hasOffensiveTarget;
            public string offensiveTargetId;
            public float targetX;
            public float targetY;
            public float distanceToTarget;
            public float secondsSinceMilitaryProgress;
            public float secondsSinceMeaningfulAction;
            public int stagingRecoveryCount;
            public int objectiveFailureCount;
            public long totalPathRequests;
            public long totalAiCommands;
        }

        [Serializable]
        private sealed class MarkerReport
        {
            public string name;
            public int activeFrames;
            public double averageMilliseconds;
            public double maximumMilliseconds;
        }

        private sealed class Capture : IDisposable
        {
            private readonly float duration;
            private readonly string outputPath;
            private readonly bool exitWhenDone;
            private readonly List<ProfilerRecorder> recorders = new();
            private ProfilerRecorder gcAllocatedRecorder;
            private readonly long[] totals = new long[MarkerNames.Length];
            private readonly long[] maximums = new long[MarkerNames.Length];
            private readonly int[] activeFrames = new int[MarkerNames.Length];
            private double startedAt;
            private double frameTotal;
            private double worstFrame;
            private double commanderFrameTotal;
            private double worstCommanderFrame;
            private int frameCount;
            private int commanderFrames;
            private ulong previousThinkTick;
            private long previousPathRequests;
            private long maximumPathRequestsInFrame;
            private long maximumAiCommandsInFrame;
            private long totalGcAllocated;
            private long maximumGcAllocated;
            private int gcSampleFrames;
            private RuleBasedCommander commander;
            private int maximumAudioPoolSize;
            private int maximumActiveAudioSources;
            private int maximumDropOffRegistryCount;
            private int maximumFogPresentationGateCount;
            private readonly List<FrameSpikeSnapshot> spikes =
                new(MaximumSpikeSnapshots);
            private readonly List<LivenessSnapshot> liveness =
                new(MaximumLivenessSnapshots);
            private FrameSpikeSnapshot peakSpike;
            private double lastSpikeSampleAt = double.NegativeInfinity;
            private double lastLivenessSampleAt = double.NegativeInfinity;
            private long previousCommandAttempts;
            private readonly float spikeThresholdMs = ReadSpikeThreshold();

            public Capture(float seconds, string path, bool exit)
            {
                duration = seconds;
                outputPath = path;
                exitWhenDone = exit;
            }

            public void Start()
            {
                commander = UnityEngine.Object.FindAnyObjectByType<RuleBasedCommander>();
                previousThinkTick = commander != null ? commander.ThinkTick : 0;
                PathfindingSystem.ResetPerformanceCounters();
                FactionCommandService.ResetPerformanceCounters();
                BuildingCommanderPolicy.ResetPerformanceCounters();
                EconomicResourceClusterPlanner.ResetPerformanceCounters();
                ProductionCommanderPolicy.ResetPerformanceCounters();
                BuildingSite.ResetPerformanceCounters();
                previousPathRequests = 0;
                previousCommandAttempts = 0;
                for (int i = 0; i < MarkerNames.Length; i++)
                    recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Scripts,
                        MarkerNames[i], 1,
                        ProfilerRecorderOptions.StartImmediately
                        | ProfilerRecorderOptions.SumAllSamplesInFrame));
                gcAllocatedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory,
                    "GC Allocated In Frame", 1,
                    ProfilerRecorderOptions.StartImmediately
                    | ProfilerRecorderOptions.SumAllSamplesInFrame);
                startedAt = EditorApplication.timeSinceStartup;
                EditorApplication.update += Sample;
            }

            private void Sample()
            {
                if (!EditorApplication.isPlaying)
                {
                    Finish(false);
                    return;
                }
                double frameMilliseconds = Time.unscaledDeltaTime * 1000d;
                frameTotal += frameMilliseconds;
                worstFrame = Math.Max(worstFrame, frameMilliseconds);
                frameCount++;
                long currentPathRequests = PathfindingSystem.TotalRequests;
                long pathRequestsThisFrame = currentPathRequests - previousPathRequests;
                maximumPathRequestsInFrame = Math.Max(maximumPathRequestsInFrame,
                    pathRequestsThisFrame);
                previousPathRequests = currentPathRequests;
                long currentCommandAttempts = TotalCommandAttempts();
                long commandAttemptsThisFrame = currentCommandAttempts
                    - previousCommandAttempts;
                maximumAiCommandsInFrame = Math.Max(maximumAiCommandsInFrame,
                    commandAttemptsThisFrame);
                previousCommandAttempts = currentCommandAttempts;
                GameplayAudioService audio = GameplayAudioService.Instance;
                if (audio != null)
                {
                    maximumAudioPoolSize = Math.Max(maximumAudioPoolSize, audio.PoolSize);
                    maximumActiveAudioSources = Math.Max(maximumActiveAudioSources,
                        audio.ActiveOneShots + audio.ActiveLoops);
                }
                maximumDropOffRegistryCount = Math.Max(maximumDropOffRegistryCount,
                    ResourceDropOffRegistry.Active.Count);
                if (FogWorldVisibility.Instance != null)
                    maximumFogPresentationGateCount = Math.Max(
                        maximumFogPresentationGateCount,
                        FogWorldVisibility.Instance.RegisteredGateCount);
                if (gcAllocatedRecorder.Valid)
                {
                    long allocated = Math.Max(0, gcAllocatedRecorder.LastValue);
                    totalGcAllocated += allocated;
                    maximumGcAllocated = Math.Max(maximumGcAllocated, allocated);
                    gcSampleFrames++;
                }
                bool commanderFrame = commander != null
                    && commander.ThinkTick != previousThinkTick;
                if (commanderFrame)
                {
                    previousThinkTick = commander.ThinkTick;
                    commanderFrames++;
                    commanderFrameTotal += frameMilliseconds;
                    worstCommanderFrame = Math.Max(worstCommanderFrame, frameMilliseconds);
                }
                long largestMarkerValue = 0;
                string largestMarkerName = "None";
                for (int i = 0; i < recorders.Count; i++)
                {
                    long value = recorders[i].Valid ? recorders[i].LastValue : 0;
                    if (value <= 0) continue;
                    totals[i] += value;
                    maximums[i] = Math.Max(maximums[i], value);
                    activeFrames[i]++;
                    // CommanderTick encloses all policy samples, so it cannot
                    // identify the subsystem responsible for a slow frame.
                    if (i != 0 && value > largestMarkerValue)
                    {
                        largestMarkerValue = value;
                        largestMarkerName = MarkerNames[i];
                    }
                }
                if (frameMilliseconds >= spikeThresholdMs)
                    CaptureSpike(frameMilliseconds, allocatedThisFrame(),
                        pathRequestsThisFrame, commandAttemptsThisFrame,
                        largestMarkerName, largestMarkerValue / 1_000_000d);
                double now = EditorApplication.timeSinceStartup;
                if (liveness.Count < MaximumLivenessSnapshots
                    && now - lastLivenessSampleAt >= LivenessSampleIntervalSeconds)
                {
                    CaptureLiveness(now - startedAt, currentPathRequests,
                        currentCommandAttempts);
                    lastLivenessSampleAt = now;
                }
                if (EditorApplication.timeSinceStartup - startedAt >= duration)
                    Finish(true);
                else
                {
                    // A sparse marker keeps its previous LastValue when it did
                    // not run this frame. Clear the capture window so a later
                    // spike cannot be attributed to an old Commander tick.
                    for (int i = 0; i < recorders.Count; i++)
                    {
                        recorders[i].Reset();
                        recorders[i].Start();
                    }
                    gcAllocatedRecorder.Reset();
                    gcAllocatedRecorder.Start();
                }
            }

            private void Finish(bool writeReport)
            {
                EditorApplication.update -= Sample;
                if (writeReport)
                {
                    PerformanceReport report = BuildReport();
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
                    Debug.Log($"Phase 7B performance capture complete: {outputPath}");
                }
                Dispose();
                activeCapture = null;
                if (exitWhenDone) EditorApplication.Exit(writeReport ? 0 : 1);
            }

            private PerformanceReport BuildReport()
            {
                var report = new PerformanceReport
                {
                    capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    durationSeconds = (float)(EditorApplication.timeSinceStartup - startedAt),
                    frameCount = frameCount,
                    averageFrameMs = frameCount > 0 ? frameTotal / frameCount : 0d,
                    worstFrameMs = worstFrame,
                    commanderFrames = commanderFrames,
                    averageCommanderFrameMs = commanderFrames > 0
                        ? commanderFrameTotal / commanderFrames : 0d,
                    worstCommanderFrameMs = worstCommanderFrame,
                    totalPathRequests = PathfindingSystem.TotalRequests,
                    gameplayPathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.Gameplay),
                    commanderMovePathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.CommanderMove),
                    commanderGatherPathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.CommanderGather),
                    commanderBuildPathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.CommanderBuild),
                    commanderRallyPathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.CommanderRally),
                    commanderScoutPathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.CommanderScout),
                    economyReturnPathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.EconomyReturn),
                    militaryMovePathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.MilitaryMove),
                    militaryFormationPathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.MilitaryFormation),
                    militaryCombatPathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.MilitaryCombat),
                    militaryRegroupPathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.MilitaryRegroup),
                    resourceFallbackPathRequests = PathfindingSystem.GetRequestCount(
                        PathfindingRequestSource.ResourceFallback),
                    moveCommandAttempts = FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Move),
                    scoutMoveCommandAttempts = FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.ScoutMove),
                    gatherCommandAttempts = FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Gather),
                    returnResourceCommandAttempts =
                        FactionCommandService.GetCommandAttemptCount(
                            CommanderCommandCategory.ReturnResource),
                    buildCommandAttempts = FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Build),
                    attackCommandAttempts = FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Attack),
                    trainCommandAttempts = FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Train),
                    rallyCommandAttempts = FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Rally),
                    constructionSiteBuilderCommands =
                        BuildingSite.TotalBuilderCommandRequests,
                    deduplicatedConstructionCommands =
                        BuildingSite.TotalDeduplicatedBuilderCommands,
                    constructionSiteBuilderPathRequests =
                        BuildingSite.TotalBuilderPathRequests,
                    buildingCandidateEvaluations =
                        BuildingCommanderPolicy.CandidateEvaluationCount,
                    economyReassignments = commander?.EconomyPolicy != null
                        ? commander.EconomyPolicy.ReassignmentSerial : 0,
                    economicClusterRefreshes = EconomicResourceClusterPlanner.RefreshCount,
                    economicClusterCount = commander?.BuildingPolicy?.ClusterPlanner != null
                        ? commander.BuildingPolicy.ClusterPlanner.Clusters.Count : 0,
                    productionEnqueueAttempts = ProductionCommanderPolicy.EnqueueAttemptCount,
                    successfulProductionEnqueues =
                        ProductionCommanderPolicy.SuccessfulEnqueueCount,
                    rejectedOrDedupedProductionAttempts =
                        ProductionCommanderPolicy.RejectedOrDedupedCount,
                    productionInfrastructureRequests =
                        ProductionCommanderPolicy.InfrastructureRequestCount,
                    maximumPathRequestsInFrame = maximumPathRequestsInFrame,
                    maximumAiCommandsInFrame = maximumAiCommandsInFrame,
                    spikeThresholdMs = spikeThresholdMs,
                    peakSpike = peakSpike,
                    baseFullyDeveloped = commander != null
                        && commander.BuildingPolicy.CompletedCoreInfrastructure >= 3
                        && commander.ProductionPolicy.LivingPawnCount
                            >= commander.ProductionPolicy.TargetPawnCount
                };
                report.pathRequestsPerSecond = report.durationSeconds > 0f
                    ? report.totalPathRequests / report.durationSeconds : 0d;
                if (report.durationSeconds > 0f)
                {
                    report.moveCommandsPerSecond = report.moveCommandAttempts
                        / report.durationSeconds;
                    report.scoutMoveCommandsPerSecond = report.scoutMoveCommandAttempts
                        / report.durationSeconds;
                    report.gatherCommandsPerSecond = report.gatherCommandAttempts
                        / report.durationSeconds;
                    report.returnResourceCommandsPerSecond =
                        report.returnResourceCommandAttempts / report.durationSeconds;
                    report.buildCommandsPerSecond = report.buildCommandAttempts
                        / report.durationSeconds;
                    report.attackCommandsPerSecond = report.attackCommandAttempts
                        / report.durationSeconds;
                    report.trainCommandsPerSecond = report.trainCommandAttempts
                        / report.durationSeconds;
                    report.rallyCommandsPerSecond = report.rallyCommandAttempts
                        / report.durationSeconds;
                    report.constructionBuilderCommandsPerSecond =
                        report.constructionSiteBuilderCommands / report.durationSeconds;
                    report.constructionBuilderPathRequestsPerSecond =
                        report.constructionSiteBuilderPathRequests / report.durationSeconds;
                    report.buildingCandidatesPerSecond = report.buildingCandidateEvaluations
                        / report.durationSeconds;
                    report.productionEnqueueAttemptsPerSecond =
                        report.productionEnqueueAttempts / report.durationSeconds;
                    report.successfulProductionEnqueuesPerSecond =
                        report.successfulProductionEnqueues / report.durationSeconds;
                    report.rejectedOrDedupedProductionAttemptsPerSecond =
                        report.rejectedOrDedupedProductionAttempts
                        / report.durationSeconds;
                    report.productionInfrastructureRequestsPerSecond =
                        report.productionInfrastructureRequests / report.durationSeconds;
                }
                long commanderRequests = report.commanderMovePathRequests
                    + report.commanderGatherPathRequests + report.commanderBuildPathRequests
                    + report.commanderRallyPathRequests + report.commanderScoutPathRequests
                    + report.economyReturnPathRequests
                    + report.militaryMovePathRequests
                    + report.militaryFormationPathRequests
                    + report.militaryCombatPathRequests
                    + report.militaryRegroupPathRequests
                    + report.resourceFallbackPathRequests;
                report.commanderPathRequestsPerTick = report.commanderFrames > 0
                    ? commanderRequests / (double)report.commanderFrames : 0d;
                report.averageGcAllocatedBytes = gcSampleFrames > 0
                    ? totalGcAllocated / (double)gcSampleFrames : 0d;
                report.maximumGcAllocatedBytes = maximumGcAllocated;
                GameplayAudioService audio = GameplayAudioService.Instance;
                report.finalAudioPoolSize = audio != null ? audio.PoolSize : 0;
                report.maximumAudioPoolSize = maximumAudioPoolSize;
                report.finalActiveAudioSources = audio != null
                    ? audio.ActiveOneShots + audio.ActiveLoops : 0;
                report.maximumActiveAudioSources = maximumActiveAudioSources;
                report.audioPoolGrowthCount = audio != null ? audio.PoolGrowthCount : 0;
                report.knownResourceCount = commander?.Knowledge != null
                    ? commander.Knowledge.KnownResources.Count : 0;
                report.knownEnemyBuildingCount = commander?.Knowledge != null
                    ? commander.Knowledge.KnownEnemyBuildings.Count : 0;
                report.ownUnitCount = commander?.Knowledge != null
                    ? commander.Knowledge.OwnUnits.Count : 0;
                report.finalDropOffRegistryCount = ResourceDropOffRegistry.Active.Count;
                report.maximumDropOffRegistryCount = maximumDropOffRegistryCount;
                report.scoutFrontierCandidateCount = commander?.ScoutingPolicy != null
                    ? commander.ScoutingPolicy.FrontierCandidateCount : 0;
                report.scoutScoredFrontierCount = commander?.ScoutingPolicy != null
                    ? commander.ScoutingPolicy.ScoredFrontierCount : 0;
                report.scoutStaleCandidateCount = commander?.ScoutingPolicy != null
                    ? commander.ScoutingPolicy.StaleRevisitCandidateCount : 0;
                report.finalFogPresentationGateCount = FogWorldVisibility.Instance != null
                    ? FogWorldVisibility.Instance.RegisteredGateCount : 0;
                report.maximumFogPresentationGateCount =
                    maximumFogPresentationGateCount;
                report.spikes.AddRange(spikes);
                report.liveness.AddRange(liveness);
                for (int i = 0; i < MarkerNames.Length; i++)
                    report.markers.Add(new MarkerReport
                    {
                        name = MarkerNames[i],
                        activeFrames = activeFrames[i],
                        averageMilliseconds = activeFrames[i] > 0
                            ? totals[i] / (double)activeFrames[i] / 1_000_000d : 0d,
                        maximumMilliseconds = maximums[i] / 1_000_000d
                    });
                return report;
            }

            private long allocatedThisFrame()
                => gcAllocatedRecorder.Valid ? Math.Max(0, gcAllocatedRecorder.LastValue) : 0;

            private static long TotalCommandAttempts()
            {
                return FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Move)
                    + FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.ScoutMove)
                    + FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Gather)
                    + FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.ReturnResource)
                    + FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Build)
                    + FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Attack)
                    + FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Train)
                    + FactionCommandService.GetCommandAttemptCount(
                        CommanderCommandCategory.Rally);
            }

            private void CaptureLiveness(double elapsedRealSeconds, long pathRequests,
                long commandAttempts)
            {
                MilitaryCommanderPolicy military = commander?.MilitaryPolicy;
                StrategicIntentEvaluator strategy = commander?.StrategicIntent;
                CommanderProgressDiagnostics progress = commander?.ProgressDiagnostics;
                Vector3 armyPosition = military != null ? military.ArmyCentroid : default;
                Vector3 targetPosition = default;
                bool hasStrategicTarget = strategy != null && strategy.HasStrategicTarget;
                bool hasOffensiveTarget = military != null && military.HasOffensiveTarget;
                if (hasOffensiveTarget)
                    targetPosition = military.OffensiveTarget.LastKnownPosition;
                else if (hasStrategicTarget)
                    targetPosition = strategy.StrategicTarget.LastKnownPosition;
                liveness.Add(new LivenessSnapshot
                {
                    elapsedRealSeconds = elapsedRealSeconds,
                    gameTime = Time.time,
                    matchSpeed = MatchSpeedController.Instance != null
                        ? MatchSpeedController.Instance.DesiredSpeed : Time.timeScale,
                    commanderThinkTick = commander != null
                        ? unchecked((long)commander.ThinkTick) : 0,
                    intent = strategy != null ? strategy.CurrentIntent.ToString() : "None",
                    intentAge = strategy != null ? strategy.IntentAge(Time.time) : -1f,
                    armyState = military != null ? military.Army.State.ToString() : "None",
                    tacticalState = military != null
                        ? military.Tactical.State.ToString() : "None",
                    armySize = military?.Army?.Members.Count ?? 0,
                    offensiveParticipants = military?.OffensiveParticipantCount ?? 0,
                    armyX = armyPosition.x,
                    armyY = armyPosition.y,
                    hasStrategicTarget = hasStrategicTarget,
                    strategicTargetId = hasStrategicTarget
                        ? strategy.StrategicTarget.RuntimeId.ToString() : "None",
                    hasOffensiveTarget = hasOffensiveTarget,
                    offensiveTargetId = hasOffensiveTarget
                        ? military.OffensiveTarget.RuntimeId.ToString() : "None",
                    targetX = targetPosition.x,
                    targetY = targetPosition.y,
                    distanceToTarget = hasOffensiveTarget || hasStrategicTarget
                        ? Vector3.Distance(armyPosition, targetPosition) : -1f,
                    secondsSinceMilitaryProgress = military != null
                        && military.LastMilitaryProgressTime >= 0f
                        ? Time.time - military.LastMilitaryProgressTime : -1f,
                    secondsSinceMeaningfulAction = progress != null
                        && progress.LastMeaningfulActionTime >= 0f
                        ? Time.time - progress.LastMeaningfulActionTime : -1f,
                    stagingRecoveryCount = military?.StagingRecoveryCount ?? 0,
                    objectiveFailureCount = military?.ObjectiveFailureCount ?? 0,
                    totalPathRequests = pathRequests,
                    totalAiCommands = commandAttempts
                });
            }

            private void CaptureSpike(double frameMs, long gcBytes, long pathRequests,
                long commands, string largestMarker, double largestMarkerMs)
            {
                double now = EditorApplication.timeSinceStartup;
                bool newSample = now - lastSpikeSampleAt >= SpikeSampleIntervalSeconds;
                bool newPeak = peakSpike == null || frameMs > peakSpike.frameMs;
                bool strongerRecentSpike = !newSample && spikes.Count > 0
                    && frameMs > spikes[spikes.Count - 1].frameMs;
                if (!newSample && !newPeak && !strongerRecentSpike) return;

                if (newPeak)
                {
                    peakSpike ??= new FrameSpikeSnapshot();
                    PopulateSpike(peakSpike, frameMs, gcBytes, pathRequests,
                        commands, largestMarker, largestMarkerMs);
                }

                if (newSample)
                {
                    FrameSpikeSnapshot snapshot;
                    if (spikes.Count == MaximumSpikeSnapshots)
                    {
                        snapshot = spikes[0];
                        spikes.RemoveAt(0);
                    }
                    else snapshot = new FrameSpikeSnapshot();
                    PopulateSpike(snapshot, frameMs, gcBytes, pathRequests,
                        commands, largestMarker, largestMarkerMs);
                    spikes.Add(snapshot);
                    lastSpikeSampleAt = now;
                }
                else if (strongerRecentSpike)
                    PopulateSpike(spikes[spikes.Count - 1], frameMs, gcBytes,
                        pathRequests, commands, largestMarker, largestMarkerMs);
            }

            private void PopulateSpike(FrameSpikeSnapshot snapshot, double frameMs,
                long gcBytes, long pathRequests, long commands, string largestMarker,
                double largestMarkerMs)
            {
                GameplayAudioService audio = GameplayAudioService.Instance;
                snapshot.frameMs = frameMs;
                snapshot.gameTime = Time.time;
                snapshot.gcBytes = gcBytes;
                snapshot.pathRequests = pathRequests;
                snapshot.aiCommands = commands;
                snapshot.currentIntent = commander != null
                    ? commander.StrategicIntent.CurrentIntent.ToString() : "None";
                snapshot.tacticalState = commander != null
                    ? commander.MilitaryPolicy.Tactical.State.ToString() : "None";
                snapshot.armySize = commander?.MilitaryPolicy?.Army?.Members.Count ?? 0;
                snapshot.knownResources = commander?.Knowledge?.KnownResources.Count ?? 0;
                snapshot.knownEnemyBuildings =
                    commander?.Knowledge?.KnownEnemyBuildings.Count ?? 0;
                snapshot.scoutCandidates =
                    commander?.ScoutingPolicy?.FrontierCandidateCount ?? 0;
                snapshot.tacticalCandidates = commander?.MilitaryPolicy?.Tactical
                    ?.LocalCandidateCount ?? 0;
                snapshot.targetAssignments = commander?.MilitaryPolicy?.Tactical
                    ?.TargetAssignmentCount ?? 0;
                snapshot.audioPoolSize = audio != null ? audio.PoolSize : 0;
                snapshot.activeAudioSources = audio != null
                    ? audio.ActiveOneShots + audio.ActiveLoops : 0;
                snapshot.largestAiSubsystem = largestMarker;
                snapshot.largestAiSubsystemMs = largestMarkerMs;
                snapshot.strategicMs = LastMarkerMilliseconds(
                    "TinyKingdom.AI.StrategicIntent.Evaluate");
                snapshot.tacticalMs = LastMarkerMilliseconds(
                    "TinyKingdom.AI.TacticalArmy.Evaluate");
                snapshot.scoutMs = LastMarkerMilliseconds("TinyKingdom.AI.ScoutingPolicy");
                snapshot.economyMs = LastMarkerMilliseconds("TinyKingdom.AI.EconomyPolicy");
                snapshot.productionMs = LastMarkerMilliseconds(
                    "TinyKingdom.AI.ProductionPolicy");
                snapshot.safetyMapMs = LastMarkerMilliseconds(
                    "TinyKingdom.AI.StrategicSafetyMapUpdate");
                snapshot.resourceClusterMs = LastMarkerMilliseconds(
                    "TinyKingdom.AI.EconomicClusterRefresh");
            }

            private double LastMarkerMilliseconds(string markerName)
            {
                for (int i = 0; i < MarkerNames.Length; i++)
                    if (MarkerNames[i] == markerName)
                        return recorders[i].Valid
                            ? recorders[i].LastValue / 1_000_000d : 0d;
                return 0d;
            }

            public void Dispose()
            {
                for (int i = 0; i < recorders.Count; i++) recorders[i].Dispose();
                recorders.Clear();
                gcAllocatedRecorder.Dispose();
            }
        }
    }
}
