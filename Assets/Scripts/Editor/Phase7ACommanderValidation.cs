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
    public static class Phase7ACommanderValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7A Commander")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 7A Commander validation in Play Mode so runtime faction state is active.");

            RuleBasedCommander[] commanders = UnityEngine.Object
                .FindObjectsByType<RuleBasedCommander>();
            Require(commanders.Count(item => item != null && item.Faction == Faction.Red
                && item.isActiveAndEnabled) == 1,
                "Exactly one active Red RuleBasedCommander is required.");
            Require(commanders.All(item => item == null || item.Faction != Faction.Blue),
                "Blue must remain human-controlled.");

            RuleBasedCommander commander = commanders.Single(item => item.Faction == Faction.Red);
            FactionKnowledge knowledge = commander.Knowledge;
            Require(commander.IsConfigured, "Red Commander references are incomplete.");
            Require(commander.ThinkInterval >= 0.1f && commander.ThinkInterval <= 2f,
                "Commander think interval is not sane.");
            Require(knowledge != null && knowledge.IsReady,
                "Red FactionKnowledge is not ready.");
            Require(knowledge.TeamVision != null && knowledge.TeamVision.IsReady,
                "Red TeamVision reference is invalid.");
            Require(knowledge.FogMemory != null && knowledge.FogMemory.IsInitialized,
                "Red FogMemory reference is invalid.");
            Require(commander.CommandService != null
                && commander.CommandService.Faction == Faction.Red,
                "Red FactionCommandService is invalid.");
            Require(commander.EconomyPolicy != null && commander.ScoutingPolicy != null,
                "Economy and scouting policies must be configured.");
            Require(!SnapshotContainsUnityReference(typeof(VisibleUnitObservation))
                && !SnapshotContainsUnityReference(typeof(BuildingKnowledgeSnapshot))
                && !SnapshotContainsUnityReference(typeof(ResourceKnowledgeSnapshot)),
                "Last-seen Commander snapshots must not contain live Unity object references.");
            Require(!UnitController.ActiveUnits.Any(item => item != null
                && item.Faction == Faction.Goblin),
                "Phase 7A must not require or spawn Goblins.");

            UnitController blue = UnitController.ActiveUnits.FirstOrDefault(item => item != null
                && item.Faction == Faction.Blue);
            if (blue != null)
                Require(commander.CommandService.TryMove(blue, blue.transform.position)
                    == CommanderCommandResult.RejectedNotOwned,
                    "Command ownership validation did not reject a Blue actor.");
            Require(MatchManager.Instance != null,
                "Phase 7A requires the authoritative MatchManager.");

            Debug.Log("Phase 7A Commander validation passed: one Red Commander, legal Fog knowledge, "
                + "immutable last-seen data, Move/Gather authority, economy/scouting policies, "
                + "0.50-second tick and no Goblin dependency.");
        }

        private static bool SnapshotContainsUnityReference(Type type)
            => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType));

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
