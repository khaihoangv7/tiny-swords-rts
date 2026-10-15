#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TinyKingdom.AI;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7E5StrategicLivenessValidation
    {
        private const string MilitaryPath =
            "Assets/Scripts/AI/MilitaryCommanderPolicy.cs";
        private const string MovementPath = "Assets/Scripts/Units/UnitMovement.cs";
        private const string StrategyPath =
            "Assets/Scripts/AI/StrategicIntentEvaluator.cs";
        private const string ScoutPath =
            "Assets/Scripts/AI/ScoutingCommanderPolicy.cs";
        private const string TacticalPath =
            "Assets/Scripts/AI/TacticalArmyController.cs";
        private const string ProductionPath =
            "Assets/Scripts/AI/ProductionCommanderPolicy.cs";
        private const string DiagnosticsPath =
            "Assets/Scripts/AI/CommanderProgressDiagnostics.cs";
        private const string ProfilerPath =
            "Assets/Scripts/Editor/Phase7BPerformanceProfiler.cs";

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.5 Strategic Liveness")]
        public static void Validate()
        {
            try
            {
                var strategy = new StrategicIntentEvaluator();
                var military = new MilitaryCommanderPolicy();
                var tactical = new TacticalArmyController();
                var scouting = new ScoutingCommanderPolicy();
                Require(Enum.GetValues(typeof(StrategicIntent)).Length == 6,
                    "All six routine strategic intents must remain defined.");
                Require(strategy.EvaluationInterval > 0f
                    && tactical.EvaluationInterval > 0f,
                    "Strategic and tactical cadences must be positive.");
                Require(military.MilitaryNoProgressSeconds > 0f
                    && military.StagingRecoveryCooldown > 0f
                    && military.AttackIdleTimeout > 0f
                    && military.OffensiveStagingCommandsPerTick > 0,
                    "Military progress and staging recovery bounds are invalid.");
                Require(military.MaximumRecoveryDuration
                    > military.MinimumRecoveryDuration,
                    "Recover must have a bounded fallback exit.");
                Require(tactical.EngagementExitDelay >= 0f,
                    "Engaging no-target exit delay is invalid.");
                Require(scouting.BusyPromotionFallbackSeconds > 0f
                    && scouting.MaxRescoutResourceCandidates > 0,
                    "Scout promotion and memory scoring must be bounded.");

                string movement = File.ReadAllText(MovementPath);
                string militarySource = File.ReadAllText(MilitaryPath);
                string strategySource = File.ReadAllText(StrategyPath);
                string scoutSource = File.ReadAllText(ScoutPath);
                string tacticalSource = File.ReadAllText(TacticalPath);
                string production = File.ReadAllText(ProductionPath);
                string diagnostics = File.ReadAllText(DiagnosticsPath);
                string profiler = File.ReadAllText(ProfilerPath);
                Require(movement.Contains("existingCommandHealthy")
                    && movement.Contains("activePath.HasRemainingWaypoints")
                    && movement.Contains("PathFailureCount"),
                    "Move dedupe does not expose stale-command recovery health.");
                Require(militarySource.Contains("IsAssemblyReady")
                    && militarySource.Contains("HandleStagingNoProgress")
                    && militarySource.Contains("HandleAttackNoProgress")
                    && militarySource.Contains("Recovery timeout; rebuild at staging"),
                    "PrepareAttack, Attack or Recover liveness fallback is missing.");
                Require(production.Contains("IsValidStagingCell")
                    && production.Contains("InvalidateRallyStagingCell")
                    && production.Contains("IsStableStandingCell"),
                    "Invalid/connector staging recovery is missing.");
                Require(strategySource.Contains("RequestImmediateReevaluation")
                    && strategySource.Contains("TargetInvalid")
                    && strategySource.Contains("majorOverride = true"),
                    "Attack null-target fallback is not immediate and bounded.");
                Require(tacticalSource.Contains("engagementExitDelay")
                    && tacticalSource.Contains("severeThreshold"),
                    "Engaging exit or one-straggler tolerance is missing.");
                Require(scoutSource.Contains("ChooseBusyPromotionCandidate")
                    && scoutSource.Contains("FinishPromotedCargoReturn")
                    && scoutSource.Contains("nextRescoutMemoryIndex"),
                    "Scout promotion or memory scoring can stall the Commander.");
                Require(diagnostics.Contains("TryCaptureMilitaryLivenessStall")
                    && diagnostics.Contains("Mathf.Min(32")
                    && !diagnostics.Contains("StartCoroutine"),
                    "Military watchdog is missing or diagnostic capture is unbounded.");
                Require(profiler.Contains("MaximumSpikeSnapshots = 8")
                    && profiler.Contains("FrameSpikeSnapshot")
                    && profiler.Contains("pathRequestsThisFrame")
                    && profiler.Contains("commandAttemptsThisFrame")
                    && profiler.Contains("gcBytes"),
                    "Bounded frame-spike telemetry is incomplete.");
                Require(!Directory.GetFiles("Assets/Scripts/AI", "*.cs")
                        .Any(path => File.ReadAllText(path).Contains("StartCoroutine")),
                    "AI policy layer must not accumulate recurring coroutines.");

                Debug.Log("Phase 7E.5 Strategic Liveness validation PASS: "
                    + "state-aware dedupe, robust staging, bounded Attack/Recover "
                    + "recovery, Scout fallback, no-target exit, watchdog and bounded spike telemetry verified.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 7E.5 Strategic Liveness validation FAILED: "
                    + exception);
                throw;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
