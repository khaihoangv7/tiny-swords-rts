#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7C1AStrategicSafetyValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7C.1A Strategic Safety")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 7C.1A validation in Play Mode so legal Fog knowledge is populated.");

            RuleBasedCommander[] commanders = UnityEngine.Object
                .FindObjectsByType<RuleBasedCommander>();
            Require(commanders.Count(item => item != null && item.Faction == Faction.Red
                && item.isActiveAndEnabled) == 1,
                "Exactly one active Red RuleBasedCommander is required.");
            Require(commanders.All(item => item == null || item.Faction != Faction.Blue),
                "Blue must remain human-controlled.");
            RuleBasedCommander commander = commanders.Single(item => item != null
                && item.Faction == Faction.Red && item.isActiveAndEnabled);
            StrategicSafetyMap safety = commander.StrategicSafety;
            FactionKnowledge knowledge = commander.Knowledge;
            Require(safety != null && knowledge != null && knowledge.IsReady,
                "StrategicSafetyMap or legal Red FactionKnowledge is unavailable.");
            safety.Refresh(knowledge);
            Require(safety.HasHomeAnchor, "Red Castle home anchor is missing.");
            Require(Mathf.Approximately(safety.HomeEconomicRadius, 11f),
                "HomeEconomicRadius must remain 11 world/logical units for the 40x28 map.");
            Require(safety.CivilianRetreatTraversal != null
                && safety.ScoutRetreatTraversal != null
                && !ReferenceEquals(safety.CivilianTraversal,
                    safety.CivilianRetreatTraversal)
                && !ReferenceEquals(safety.ScoutTraversal,
                    safety.ScoutRetreatTraversal),
                "Retreat must use a dedicated escape traversal policy.");
            Require(knowledge.TeamVision?.GridManager != null
                && knowledge.TeamVision.GridManager.Width == 40
                && knowledge.TeamVision.GridManager.Height == 28,
                "Phase 7C.1A balance validation expects the verified 40x28 map.");

            Require(typeof(IPathTraversalPolicy).IsInterface,
                "Optional strategic traversal policy is missing.");
            Require(typeof(PathfindingSystem).GetMethod(nameof(PathfindingSystem.FindPath),
                    new[] { typeof(Vector2Int), typeof(Vector2Int),
                        typeof(IPathTraversalPolicy) }) != null,
                "Existing A* does not expose the optional AI traversal hook.");
            Require(typeof(PawnGatherer).GetMethod(nameof(PawnGatherer.Gather),
                    new[] { typeof(ResourceNode) }) != null,
                "The original human-compatible Gather API was removed.");
            Require(typeof(PawnGatherer).GetMethod(nameof(PawnGatherer.Gather),
                    new[] { typeof(ResourceNode), typeof(Predicate<ResourceNode>),
                        typeof(IPathTraversalPolicy) }) != null,
                "AI Gather does not provide a chaining eligibility filter.");

            Require(!ContainsUnityReference(typeof(VisibleUnitObservation)),
                "Visible unit observations must remain value snapshots without live references.");
            Require(!ContainsUnityReference(typeof(BuildingKnowledgeSnapshot)),
                "Remembered building danger must use value snapshots without live references.");
            Require(safety.VisibleDynamicThreatCount <= knowledge.VisibleEnemyUnits.Count,
                "Dynamic danger exceeds the currently visible legal enemy set.");

            foreach (BuildingKnowledgeSnapshot building in knowledge.KnownEnemyBuildings)
            {
                if (!building.WasCompleted || building.LastSeenHealthRatio <= 0f
                    || building.BuildingType is not (BuildingType.Tower or BuildingType.Castle))
                    continue;
                Require(safety.RiskAt(building.LastSeenPosition,
                    StrategicPathKind.Civilian) == StrategicRisk.ForbiddenForCivilian,
                    "Known defensive building does not forbid civilian work at its center.");
            }
            foreach (VisibleUnitObservation unit in knowledge.VisibleEnemyUnits)
            {
                if (unit.UnitType is not (UnitType.Warrior or UnitType.Archer or UnitType.Lancer))
                    continue;
                Require(safety.RiskAt(unit.Position, StrategicPathKind.Civilian)
                    == StrategicRisk.ForbiddenForCivilian,
                    "Visible hostile military does not create immediate civilian danger.");
            }

            Require(commander.ScoutingPolicy != null
                && Enum.IsDefined(typeof(ScoutSafetyState),
                    commander.ScoutingPolicy.SafetyState),
                "Scout safety state is invalid.");
            Require(!UnitController.ActiveUnits.Any(item => item != null
                && item.Faction == Faction.Goblin),
                "Phase 7C.1A must not enable Goblins.");
            Require(UnityEngine.Object.FindAnyObjectByType<GoldMineObjective>() != null,
                "Central Gold Mine runtime objective is missing.");
            Require(MatchManager.Instance != null
                && MatchManager.Instance.State == MatchState.Playing,
                "Validation requires MatchState.Playing.");

            Debug.Log("Phase 7C.1A strategic safety validation passed: 40x28 home radius, "
                + "legal snapshot-only danger, optional AI A*, safe Gather chaining API, "
                + "escape traversal, central Gold Mine, Scout safety state, "
                + "Blue human API preservation and no Goblins.");
        }

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
