#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7BCommanderValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7B Commander")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 7B Commander validation in Play Mode so runtime queues and faction state are active.");

            RuleBasedCommander[] commanders = UnityEngine.Object
                .FindObjectsByType<RuleBasedCommander>();
            Require(commanders.Count(item => item != null && item.Faction == Faction.Red
                && item.isActiveAndEnabled) == 1,
                "Exactly one active Red RuleBasedCommander is required.");
            Require(commanders.All(item => item == null || item.Faction != Faction.Blue),
                "Blue must remain human-controlled.");

            RuleBasedCommander commander = commanders.Single(item => item.Faction == Faction.Red);
            Require(commander.IsConfigured, "Red Commander references are incomplete.");
            Require(commander.BuildingPolicy != null && commander.ProductionPolicy != null,
                "Phase 7B building and production policies are required.");
            Require(commander.PawnRoles != null,
                "Phase 7B Pawn role authority is required.");
            Require(commander.ProductionPolicy.TargetPawnCount == 6,
                "Phase 7B TargetPawnCount must be 6.");
            Require(commander.ProductionPolicy.TargetMilitaryPopulation == 8,
                "Phase 7B TargetMilitaryPopulation must be 8.");
            Require(commander.ProductionPolicy.DesiredQueueDepth is >= 1 and <= 2,
                "AI production queue depth must remain shallow.");
            Require(commander.ProductionPolicy.MonkLimit <= 1,
                "Phase 7B Monk limit must be at most one.");
            Require(commander.BuildingPolicy.MinimumEconomyWorkers >= 2,
                "Builder selection must preserve at least two economy workers.");
            Require(commander.ScoutingPolicy.MinimumPawnsForWorkerScout >= 4,
                "Pawn scouting must not drain the three-worker opening economy.");

            FactionCommandService commands = commander.CommandService;
            Require(commands != null && commands.BuildingSystem != null
                && commands.BuildingSystem.Owner == Faction.Red,
                "Red FactionCommandService needs the Red BuildingSystem.");
            Require(HasCommand(nameof(FactionCommandService.TryBuild))
                && HasCommand(nameof(FactionCommandService.TryTrain))
                && HasCommand(nameof(FactionCommandService.TrySetRally)),
                "FactionCommandService does not expose all Phase 7B commands.");

            foreach (BuildingType type in new[]
                     {
                         BuildingType.House, BuildingType.Barracks,
                         BuildingType.Archery, BuildingType.Monastery
                     })
                Require(commands.BuildingSystem.GetData(type) != null,
                    "Missing authoritative BuildingData for " + type + ".");

            CastleDropOff redCastle = UnityEngine.Object.FindObjectsByType<CastleDropOff>()
                .SingleOrDefault(item => item != null && item.Faction == Faction.Red);
            Require(redCastle != null && redCastle.IsProductionReady,
                "Red Castle production host is invalid.");
            Require(redCastle.GetComponent<UnitProductionQueue>() != null,
                "Red Castle needs the normal UnitProductionQueue.");
            Require(commander.Knowledge.Population != null
                && commander.Knowledge.Population.Faction == Faction.Red,
                "Red population authority is invalid.");
            Require(!UnitController.ActiveUnits.Any(item => item != null
                && item.Faction == Faction.Goblin),
                "Phase 7B must not require or spawn Goblins.");

            Debug.Log("Phase 7B Commander validation passed: Red build/train/rally authority, "
                + "Pawn role arbitration, authoritative building data, population/queue targets, "
                + "shallow production and no Goblin dependency.");
        }

        private static bool HasCommand(string name)
            => typeof(FactionCommandService).GetMethods(BindingFlags.Instance
                | BindingFlags.Public).Any(method => method.Name == name);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
