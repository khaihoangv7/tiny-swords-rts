#if UNITY_EDITOR
using System;
using System.Linq;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Match;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7DFullMatchValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7D Full Match")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 7D Full Match validation in Play Mode.");

            RuleBasedCommander[] commanders = UnityEngine.Object
                .FindObjectsByType<RuleBasedCommander>();
            Require(commanders.Count(item => item != null && item.isActiveAndEnabled
                && item.Faction == Faction.Red) == 1,
                "Exactly one active Red RuleBasedCommander is required.");
            Require(commanders.All(item => item == null || item.Faction != Faction.Blue),
                "Blue must remain human-controlled.");
            RuleBasedCommander commander = commanders.Single(item => item != null
                && item.isActiveAndEnabled && item.Faction == Faction.Red);

            Require(commander.IsConfigured && commander.Knowledge?.IsReady == true,
                "Red Commander/FactionKnowledge configuration is incomplete.");
            Require(commander.CommandService != null
                && commander.CommandService.Faction == Faction.Red
                && commander.CommandService.Knowledge == commander.Knowledge,
                "Red FactionCommandService is invalid.");
            Require(commander.EconomyPolicy != null && commander.ScoutingPolicy != null
                && commander.BuildingPolicy != null && commander.ProductionPolicy != null
                && commander.MilitaryPolicy != null && commander.PawnRoles != null,
                "One or more Phase 7A-7C policies are missing.");
            Require(commander.StrategicSafety != null
                && commander.StrategicSafety.CivilianTraversal != null
                && commander.StrategicSafety.ScoutTraversal != null
                && commander.StrategicSafety.ArmyTraversal != null,
                "Strategic Safety traversal profiles are incomplete.");
            Require(commander.ProgressDiagnostics != null
                && commander.StallWarningSeconds >= 20f,
                "Phase 7D progress diagnostics are invalid.");

            Require(commander.Knowledge.Stockpile != null
                && commander.Knowledge.Population != null
                && commander.Knowledge.Population.Faction == Faction.Red,
                "Red resource/population authorities are invalid.");
            BuildingSystem buildings = commander.CommandService.BuildingSystem;
            Require(buildings != null && buildings.Owner == Faction.Red,
                "Red BuildingSystem is invalid.");
            foreach (BuildingType type in new[]
                     {
                         BuildingType.House, BuildingType.Barracks,
                         BuildingType.Archery, BuildingType.Monastery
                     })
                Require(buildings.GetData(type) != null,
                    "Missing authoritative BuildingData for " + type + ".");

            MatchManager match = MatchManager.Instance;
            Require(match != null && match.State == MatchState.Playing,
                "Validation requires MatchState.Playing.");
            Require(match.HasBothMainCastles && match.BlueMainCastle != null
                && match.RedMainCastle != null,
                "Both Main Castles must be registered with MatchManager.");
            Require(match.RedMainCastle.GetComponent<UnitProductionQueue>() != null,
                "Red Castle production queue is missing.");

            ProductionCommanderPolicy production = commander.ProductionPolicy;
            Require(production.TargetPawnCount == 6
                && production.TargetMilitaryPopulation == 8
                && production.DesiredQueueDepth is >= 1 and <= 2
                && production.MonkLimit <= 1,
                "Phase 7D production baseline is invalid.");
            Require(production.HasRallyStagingCell,
                "Red military Rally/staging cell is unavailable.");

            MilitaryCommanderPolicy military = commander.MilitaryPolicy;
            Require(military.Army != null && military.HasBaseAnchor
                && military.HasStagingPosition,
                "ArmyGroup, Red base anchor or staging position is invalid.");
            Require(military.AssembledFraction is >= 0.5f and <= 1f
                && military.TimeoutAssemblyFraction is >= 0.5f
                && military.TimeoutAssemblyFraction <= military.AssembledFraction
                && military.PreparationTimeoutSeconds > 0f,
                "Attack assembly/deadlock tuning is invalid.");
            Require(military.CombatAssessmentRadius > 0f
                && military.RetreatRemainingForceRatio is > 0f and < 1f
                && military.RetreatAverageHealthRatio is > 0f and < 1f
                && military.RetreatEnemyStrengthRatio > 1f
                && military.RetreatCompletionTimeout > 0f,
                "Combat/retreat tuning is invalid.");
            Require(military.MaximumRecoveryDuration > military.MinimumRecoveryDuration
                && military.RecoveryAssemblyFraction is >= 0.5f and <= 1f
                && military.FailedObjectiveCooldown > 0f,
                "Recovery/cooldown tuning is invalid.");
            Require(military.CombatStrength != null
                && military.CombatStrength.WarriorValue > 0f
                && military.CombatStrength.ArcherValue > 0f
                && military.CombatStrength.LancerValue > 0f
                && military.CombatStrength.MonkSupportValue > 0f,
                "Combat-strength configuration is incomplete.");

            UnitController scout = commander.CurrentScout;
            if (scout != null)
            {
                Require(!commander.MilitaryPolicy.Army.Members.Contains(scout),
                    "The active Scout must not also belong to ArmyGroup.");
                if (scout.GetComponent<TinyKingdom.Economy.PawnGatherer>() != null)
                    Require(commander.PawnRoles.IsRole(scout, CommanderPawnRole.Scout),
                        "A Pawn Scout must own exactly the Scout role.");
            }
            Require(!UnitController.ActiveUnits.Any(item => item != null
                && item.Faction == Faction.Goblin),
                "Phase 7D must not enable Goblins.");

            Debug.Log("Phase 7D Full Match validator passed: policy wiring, legal "
                + "knowledge/commands, economy/population, infrastructure, staging, "
                + "Army deadlock bounds, retreat/recovery tuning, MatchManager and "
                + "progress diagnostics are valid. Full-match and Profiler checks "
                + "remain manual gates.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
