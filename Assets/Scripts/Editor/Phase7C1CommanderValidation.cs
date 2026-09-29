#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Match;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7C1CommanderValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7C.1 Commander")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 7C.1 Commander validation in Play Mode so runtime army state is active.");

            RuleBasedCommander[] commanders = UnityEngine.Object
                .FindObjectsByType<RuleBasedCommander>();
            Require(commanders.Count(item => item != null && item.Faction == Faction.Red
                && item.isActiveAndEnabled) == 1,
                "Exactly one active Red RuleBasedCommander is required.");
            Require(commanders.All(item => item == null || item.Faction != Faction.Blue),
                "Blue must remain human-controlled.");

            RuleBasedCommander commander = commanders.Single(item => item.Faction == Faction.Red);
            Require(commander.IsConfigured, "Red Commander references are incomplete.");
            Require(commander.BuildingPolicy != null && commander.ProductionPolicy != null
                && commander.EconomyPolicy != null && commander.ScoutingPolicy != null,
                "Phase 7A/7B policies must remain configured.");
            MilitaryCommanderPolicy military = commander.MilitaryPolicy;
            Require(military != null, "MilitaryCommanderPolicy is missing.");
            Require(military.BaseDefenseRadius > 0f,
                "BaseDefenseRadius must be positive.");
            Require(military.DefenseLeashRadius >= military.BaseDefenseRadius,
                "DefenseLeashRadius must be at least BaseDefenseRadius.");
            Require(military.HasBaseAnchor,
                "Military policy has no valid living Red Castle anchor.");
            Require(military.HasStagingPosition
                && commander.ProductionPolicy.HasRallyStagingCell,
                "Military policy must reuse the Phase 7B Rally staging point.");

            FactionKnowledge knowledge = commander.Knowledge;
            Require(knowledge != null && knowledge.IsReady,
                "Red legal FactionKnowledge is not ready.");
            Require(knowledge.OwnBuildings.Any(item => item != null && item.IsAlive
                && item.Type == BuildingType.Castle),
                "Red Castle is missing from authoritative own-building knowledge.");
            Require(!typeof(VisibleUnitObservation).GetFields(BindingFlags.Instance
                    | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)),
                "Visible threat snapshots must not contain live Unity references.");

            FactionCommandService commands = commander.CommandService;
            Require(commands != null && commands.SupportsMilitaryCommands
                && HasCommand(nameof(FactionCommandService.TryAttack)),
                "FactionCommandService military command authority is missing.");
            UnitController blue = UnitController.ActiveUnits.FirstOrDefault(item => item != null
                && item.Faction == Faction.Blue);
            if (blue != null)
                Require(commands.TryAttack(blue, default)
                    == CommanderCommandResult.RejectedNotOwned,
                    "Military command ownership validation did not reject a Blue actor.");

            UnitController scout = commander.CurrentScout;
            Require(military.Army.Members.All(item => item != null && item.IsAlive
                && item.IsWorldParticipant && item != scout
                && item.GetComponent<HealthComponent>()?.CombatData?.UnitType
                    is UnitType.Warrior or UnitType.Archer or UnitType.Lancer or UnitType.Monk
                && !(item.GetComponent<UnitStationing>()?.IsStationed ?? false)),
                "ArmyGroup contains an ineligible, stationed, dead or active Scout unit.");
            if (military.HasCurrentThreat)
                Require(knowledge.TryGetVisibleEnemy(military.CurrentThreatId, out _),
                    "Current strategic threat is not in current legal Red vision.");
            Require(!UnitController.ActiveUnits.Any(item => item != null
                && item.Faction == Faction.Goblin),
                "Phase 7C.1 must not depend on or re-enable Goblins.");
            Require(MatchManager.Instance != null && MatchManager.Instance.State == MatchState.Playing,
                "Phase 7C.1 validation requires MatchState.Playing.");

            Debug.Log("Phase 7C.1 Commander validation passed: one Red defensive ArmyGroup, "
                + "legal visible threat snapshots, Castle anchor, Phase 7B staging, military "
                + "command authority, Scout separation and no Goblin dependency.");
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
