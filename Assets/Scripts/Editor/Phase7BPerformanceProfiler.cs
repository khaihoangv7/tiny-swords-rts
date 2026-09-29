using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TinyKingdom.AI;
using TinyKingdom.Navigation;
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

        private static readonly string[] MarkerNames =
        {
            "TinyKingdom.AI.CommanderTick",
            "TinyKingdom.AI.KnowledgeRefresh",
            "TinyKingdom.AI.RoleSync",
            "TinyKingdom.AI.BuildingPolicy",
            "TinyKingdom.AI.BuildPlacementSearch",
            "TinyKingdom.AI.ProductionPolicy",
            "TinyKingdom.AI.EconomyPolicy",
            "TinyKingdom.AI.ResourceTargetSearch",
            "TinyKingdom.AI.ScoutingPolicy",
            "TinyKingdom.AI.FrontierSearch",
            "TinyKingdom.AI.MilitaryPolicy",
            "TinyKingdom.AI.ThreatEvaluation",
            "TinyKingdom.AI.ArmyRegroup",
            "TinyKingdom.AI.CommandDispatch",
            "TinyKingdom.Pathfinding.FindPath",
            "TinyKingdom.Fog.TeamVisionRecompute",
            "TinyKingdom.Fog.MemoryRefresh"
        };

        private static Capture activeCapture;
        private static bool batchRequested;

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
            BeginCapture(ReadDuration(), ReportPath());
        }

        private static void HandlePlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= HandlePlayMode;
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
            public long maximumPathRequestsInFrame;
            public double commanderPathRequestsPerTick;
            public bool baseFullyDeveloped;
            public double averageGcAllocatedBytes;
            public long maximumGcAllocatedBytes;
            public List<MarkerReport> markers = new();
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
            private long totalGcAllocated;
            private long maximumGcAllocated;
            private int gcSampleFrames;
            private RuleBasedCommander commander;

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
                previousPathRequests = 0;
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
                maximumPathRequestsInFrame = Math.Max(maximumPathRequestsInFrame,
                    currentPathRequests - previousPathRequests);
                previousPathRequests = currentPathRequests;
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
                for (int i = 0; i < recorders.Count; i++)
                {
                    long value = recorders[i].Valid ? recorders[i].LastValue : 0;
                    if (value <= 0) continue;
                    totals[i] += value;
                    maximums[i] = Math.Max(maximums[i], value);
                    activeFrames[i]++;
                }
                if (EditorApplication.timeSinceStartup - startedAt >= duration)
                    Finish(true);
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
                    maximumPathRequestsInFrame = maximumPathRequestsInFrame,
                    baseFullyDeveloped = commander != null
                        && commander.BuildingPolicy.CompletedCoreInfrastructure >= 3
                        && commander.ProductionPolicy.LivingPawnCount
                            >= commander.ProductionPolicy.TargetPawnCount
                };
                report.pathRequestsPerSecond = report.durationSeconds > 0f
                    ? report.totalPathRequests / report.durationSeconds : 0d;
                long commanderRequests = report.commanderMovePathRequests
                    + report.commanderGatherPathRequests + report.commanderBuildPathRequests
                    + report.commanderRallyPathRequests;
                report.commanderPathRequestsPerTick = report.commanderFrames > 0
                    ? commanderRequests / (double)report.commanderFrames : 0d;
                report.averageGcAllocatedBytes = gcSampleFrames > 0
                    ? totalGcAllocated / (double)gcSampleFrames : 0d;
                report.maximumGcAllocatedBytes = maximumGcAllocated;
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

            public void Dispose()
            {
                for (int i = 0; i < recorders.Count; i++) recorders[i].Dispose();
                recorders.Clear();
                gcAllocatedRecorder.Dispose();
            }
        }
    }
}
