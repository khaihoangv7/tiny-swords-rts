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
    public static class Phase7C2CommanderValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7C.2 Commander")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 7C.2 Commander validation in Play Mode.");

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
            Require(commander.StrategicSafety?.ArmyTraversal != null,
                "The knowledge-derived Army traversal profile is missing.");
            Require(commander.ProductionPolicy != null
                && commander.ProductionPolicy.HasRallyStagingCell,
                "Phase 7B military Rally staging is unavailable.");

            MilitaryCommanderPolicy military = commander.MilitaryPolicy;
            Require(military != null, "MilitaryCommanderPolicy is missing.");
            Require(military.Army != null, "ArmyGroup is missing.");
            Require(military.MinOffensiveMilitaryPopulation > 0,
                "MinOffensiveMilitaryPopulation must be positive.");
            Require(military.AssembledFraction is >= 0.5f and <= 1f,
                "Pre-attack assembled fraction is invalid.");
            Require(military.HasBaseAnchor && military.HasStagingPosition,
                "Castle anchor or staging point is invalid.");
            Require(Enum.IsDefined(typeof(ArmyGroupState), ArmyGroupState.PreparingAttack)
                && Enum.IsDefined(typeof(ArmyGroupState), ArmyGroupState.Advancing)
                && Enum.IsDefined(typeof(ArmyGroupState), ArmyGroupState.Assaulting),
                "Phase 7C.2 offensive military states are missing.");

            FactionCommandService commands = commander.CommandService;
            Require(commands != null && commands.SupportsMilitaryCommands,
                "FactionCommandService military authority is missing.");
            Require(HasCommand(nameof(FactionCommandService.TryAttackMove)),
                "Validated Commander AttackMove is unavailable.");
            Require(HasCommand(nameof(FactionCommandService.TryAttackBuilding)),
                "Validated visible-building Attack is unavailable.");
            Require(!ContainsUnityReference(typeof(OffensiveTargetSnapshot)),
                "Offensive target snapshots must not hold live Unity references.");
            if (military.HasOffensiveTarget)
            {
                Require(commander.Knowledge.TryGetKnownEnemyBuilding(
                    military.OffensiveTarget.RuntimeId, out _),
                    "Current offensive objective is absent from legal Red knowledge.");
                if (!military.IsOffensiveTargetVisible)
                    Require(!commander.Knowledge.TryGetVisibleEnemyBuilding(
                        military.OffensiveTarget.RuntimeId, out _),
                        "Hidden objective visibility state is inconsistent.");
            }

            UnitController scout = commander.CurrentScout;
            Require(military.Army.Members.All(item => item != null && item != scout),
                "The active Scout was added to ArmyGroup.");
            Require(!UnitController.ActiveUnits.Any(item => item != null
                && item.Faction == Faction.Goblin),
                "Phase 7C.2 must not enable Goblins.");
            Require(MatchManager.Instance != null
                && MatchManager.Instance.State == MatchState.Playing,
                "Validation requires MatchState.Playing.");

            Debug.Log("Phase 7C.2 Commander validation passed: legal snapshot-only targets, "
                + "readiness/regroup states, shared AttackMove and visible-building Attack, "
                + "Army risk profile, Scout separation, Red-only Commander and no Goblins.");
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
