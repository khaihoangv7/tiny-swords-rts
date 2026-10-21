#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TinyKingdom.AI;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7E5StrategicDecisionValidation
    {
        private const string StrategyPath =
            "Assets/Scripts/AI/StrategicIntentEvaluator.cs";
        private const string CommanderPath =
            "Assets/Scripts/AI/RuleBasedCommander.cs";
        private const string MilitaryPath =
            "Assets/Scripts/AI/MilitaryCommanderPolicy.cs";
        private const string ScoutingPath =
            "Assets/Scripts/AI/ScoutingCommanderPolicy.cs";

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.5 Strategic Decision Layer")]
        public static void Validate()
        {
            try
            {
                string[] required =
                {
                    "DevelopEconomy", "Scout", "Fortify", "PrepareAttack",
                    "Attack", "Recover"
                };
                string[] configured = Enum.GetNames(typeof(StrategicIntent));
                Require(required.All(configured.Contains),
                    "Strategic intent model lost a Phase 7E.5 intent.");

                var evaluator = new StrategicIntentEvaluator();
                Require(evaluator.EvaluationInterval is >= 1f and <= 3f,
                    "Strategic cadence must be a bounded 1-3 game seconds.");
                Require(evaluator.MinimumIntentHold > evaluator.EvaluationInterval
                    && evaluator.SwitchMargin > 0f,
                    "Intent hold/switch hysteresis is not configured.");
                Require(evaluator.AttackReadinessThreshold > 0f
                    && evaluator.AttackReadinessThreshold < 1f,
                    "Attack readiness threshold is invalid.");
                Require(evaluator.AttackCommitmentSeconds > 0f
                    && evaluator.FailedAttackRecoverySeconds > 0f,
                    "Attack commitment/failure recovery memory is not bounded.");
                Require(evaluator.MaximumTargetCandidates is >= 4 and <= 48,
                    "Strategic target candidate scoring is not bounded.");

                var scores = new StrategicIntentScores(0.2f, 0.3f, 0.1f,
                    0.6f, 0.4f, 0.1f);
                Require(StrategicIntentEvaluator.ChooseForValidation(scores)
                        == StrategicIntent.PrepareAttack,
                    "Normalized intent selection is not deterministic.");
                Require(typeof(StrategicSituationSnapshot).IsValueType
                    && typeof(StrategicTargetSnapshot).IsValueType,
                    "Strategic situation and target memories must be value-only snapshots.");
                Require(!ContainsUnityObjectReference(typeof(StrategicSituationSnapshot))
                    && !ContainsUnityObjectReference(typeof(StrategicTargetSnapshot)),
                    "Strategic snapshots contain a hidden live Unity object reference.");

                string strategy = File.ReadAllText(StrategyPath);
                string commander = File.ReadAllText(CommanderPath);
                string military = File.ReadAllText(MilitaryPath);
                string scouting = File.ReadAllText(ScoutingPath);
                Require(strategy.Contains("FactionKnowledge")
                    && strategy.Contains("KnownEnemyBuildings")
                    && strategy.Contains("EnemyCompositionMemory")
                    && strategy.Contains("CombatStrengthEvaluator.OwnArmy"),
                    "Strategy does not reuse legal knowledge and combat strength.");
                Require(!strategy.Contains("FindObjectsOfType")
                    && !strategy.Contains("FindObjectsByType")
                    && !strategy.Contains("UnitController.AllUnits")
                    && !strategy.Contains("context.Commands")
                    && !strategy.Contains("TryMove(")
                    && !strategy.Contains("TryGather(")
                    && !strategy.Contains("TryTrain("),
                    "Strategy scans hidden live state or duplicates policy commands.");
                Require(strategy.Contains("maximumTargetCandidates")
                    && strategy.Contains("failedLaunchSerial")
                    && strategy.Contains("PreviousIntent")
                    && strategy.Contains("TransitionGameTime"),
                    "Bounded target/failure/transition metadata is incomplete.");
                Require(commander.Contains("MatchManager.Instance.State != MatchState.Playing")
                    && commander.Contains("StrategicIntentMarker")
                    && commander.Contains("strategicIntent.Tick"),
                    "Commander MatchEnd gate or strategic integration is missing.");
                int defense = military.IndexOf("TryRetainCurrentThreat",
                    StringComparison.Ordinal);
                int strategyGate = military.IndexOf(
                    "strategy.CurrentIntent is StrategicIntent.Attack",
                    StringComparison.Ordinal);
                Require(defense >= 0 && strategyGate > defense
                    && military.Contains("or StrategicIntent.SecureGoldMine")
                    && military.Contains("ArmyGroupState.Retreating")
                    && military.Contains("ArmyGroupState.Recovering"),
                    "BaseDefense/Retreat must remain above routine strategic intent.");
                Require(military.Contains("TryResolveStrategicTarget")
                    && military.Contains("Strategic preparation at staging")
                    && commander.Contains("productionPolicy.RallyStagingCell"),
                    "PrepareAttack/target/staging integration is incomplete.");
                Require(scouting.Contains("StrategicInformationPriority")
                    && scouting.Contains("StrategicIntent.Scout")
                    && commander.Contains("buildingPolicy, strategicIntent"),
                    "Scout intent is not communicated through the existing Scout policy.");

                Debug.Log("Phase 7E.5 Strategic Decision Layer validation PASS: "
                    + "six intents, legal value-only snapshots, normalized readiness, "
                    + "bounded hysteresis/targets/failure memory, emergency overrides, "
                    + "existing policy ownership and staging integration verified.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 7E.5 Strategic Decision Layer validation FAILED: "
                    + exception);
                throw;
            }
        }

        private static bool ContainsUnityObjectReference(Type type)
        {
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public
                | BindingFlags.Instance);
            for (int i = 0; i < properties.Length; i++)
                if (typeof(UnityEngine.Object).IsAssignableFrom(
                        properties[i].PropertyType)) return true;
            return false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
