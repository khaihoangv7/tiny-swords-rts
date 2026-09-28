#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Match;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7C3CommanderValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7C.3 Commander")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 7C.3 Commander validation in Play Mode.");

            RuleBasedCommander[] commanders = UnityEngine.Object
                .FindObjectsByType<RuleBasedCommander>();
            Require(commanders.Count(item => item != null && item.Faction == Faction.Red
                && item.isActiveAndEnabled) == 1,
                "Exactly one active Red RuleBasedCommander is required.");
            Require(commanders.All(item => item == null || item.Faction != Faction.Blue),
                "Blue must remain human-controlled.");
            RuleBasedCommander commander = commanders.Single(item => item != null
                && item.Faction == Faction.Red && item.isActiveAndEnabled);
            Require(commander.IsConfigured, "Red Commander references are incomplete.");
            Require(commander.Knowledge != null && commander.Knowledge.IsReady,
                "FactionKnowledge is unavailable.");
            Require(commander.Knowledge.TeamVision != null
                && commander.Knowledge.TeamVision.IsReady,
                "TeamVisionSystem is unavailable.");
            Require(commander.StrategicSafety?.ArmyTraversal != null,
                "StrategicSafety Army traversal is unavailable.");
            Require(commander.ProductionPolicy?.HasRallyStagingCell == true,
                "The existing military Rally staging point is invalid.");

            MilitaryCommanderPolicy military = commander.MilitaryPolicy;
            Require(military != null && military.Army != null,
                "MilitaryCommanderPolicy or ArmyGroup is missing.");
            Require(military.MinOffensiveMilitaryPopulation > 0,
                "MinOffensiveMilitaryPopulation must be positive.");
            Require(military.CombatAssessmentRadius > 0f,
                "CombatAssessmentRadius must be positive.");
            Require(military.CombatStrength != null
                && military.CombatStrength.WarriorValue > 0f
                && military.CombatStrength.ArcherValue > 0f
                && military.CombatStrength.LancerValue > 0f
                && military.CombatStrength.MonkSupportValue > 0f,
                "Combat-strength tuning values are missing.");
            Require(military.CombatStrength.WarriorValue
                > military.CombatStrength.LancerValue,
                "AI strength must preserve the current Warrior/Lancer relationship.");
            Require(military.RetreatRemainingForceRatio is > 0f and < 1f,
                "RetreatRemainingForceRatio is invalid.");
            Require(military.RetreatAverageHealthRatio is > 0f and < 1f,
                "RetreatAverageHealthRatio is invalid.");
            Require(military.RetreatEnemyStrengthRatio > 1f,
                "RetreatEnemyStrengthRatio must be greater than one.");
            Require(military.RetreatStableEvaluations >= 1,
                "Retreat hysteresis count is invalid.");
            Require(military.RetreatCompletionFraction is >= 0.5f and <= 1f,
                "Retreat completion fraction is invalid.");
            Require(military.RetreatCompletionTimeout > 0f,
                "Retreat completion timeout is invalid.");
            Require(military.MinimumRecoveryDuration >= 0f,
                "MinimumRecoveryDuration is invalid.");
            Require(military.MaximumRecoveryDuration
                > military.MinimumRecoveryDuration,
                "MaximumRecoveryDuration must exceed the minimum.");
            Require(military.RecoveryAverageHealthRatio is > 0f and <= 1f,
                "RecoveryAverageHealthRatio is invalid.");
            Require(military.RecoveryAssemblyFraction is >= 0.5f and <= 1f,
                "RecoveryAssemblyFraction is invalid.");
            Require(military.RecoveryMinimumEffectiveStrength > 0f,
                "Recovery effective-strength threshold is invalid.");
            Require(military.FailedObjectiveCooldown > 0f,
                "Failed-objective cooldown is invalid.");
            Require(military.HasBaseAnchor && military.HasStagingPosition,
                "Castle anchor or staging point is invalid.");
            Require(Enum.IsDefined(typeof(ArmyGroupState), ArmyGroupState.Retreating)
                && Enum.IsDefined(typeof(ArmyGroupState), ArmyGroupState.Recovering),
                "Phase 7C.3 military states are missing.");

            FactionCommandService commands = commander.CommandService;
            Require(commands != null && commands.SupportsMilitaryCommands
                && commands.SupportsRetreatCommands,
                "FactionCommandService retreat authority is missing.");
            Require(HasCommand(nameof(FactionCommandService.TryRetreatMove)),
                "Validated Commander retreat movement is unavailable.");
            Require(!ContainsUnityReference(typeof(VisibleUnitObservation)),
                "Visible enemy observations must remain copied values.");
            Require(!UnitController.ActiveUnits.Any(item => item != null
                && item.Faction == Faction.Goblin),
                "Phase 7C.3 must not enable Goblins.");
            Require(MatchManager.Instance != null
                && MatchManager.Instance.State == MatchState.Playing,
                "Validation requires MatchState.Playing.");

            Debug.Log("Phase 7C.3 Commander validation passed: legal local combat "
                + "strength, campaign casualty baseline, Retreat directive, distributed "
                + "staging recovery, shared command authority and no Goblins.");
        }

        private static bool HasCommand(string name)
            => typeof(FactionCommandService).GetMethods(BindingFlags.Instance
                | BindingFlags.Public).Any(method => method.Name == name);

        private static bool ContainsUnityReference(Type type)
            => type.GetFields(BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic)
                .Any(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType));

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
