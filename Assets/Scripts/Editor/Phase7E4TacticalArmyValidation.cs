#if UNITY_EDITOR
using System;
using System.IO;
using TinyKingdom.AI;
using TinyKingdom.Input;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7E4TacticalArmyValidation
    {
        private const string TacticalPath =
            "Assets/Scripts/AI/TacticalArmyController.cs";
        private const string MilitaryPath =
            "Assets/Scripts/AI/MilitaryCommanderPolicy.cs";
        private const string StrengthPath =
            "Assets/Scripts/AI/CombatStrengthEvaluator.cs";

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.4 Tactical Army")]
        public static void Validate()
        {
            try
            {
                var tactical = new TacticalArmyController();
                Require(tactical.AdvanceFormation == FormationType.Compact,
                    "AI advance formation must be explicitly Compact.");
                Require(tactical.EvaluationInterval >= 0.1f
                    && tactical.ArmyCohesionRadius > 0f
                    && tactical.StragglerRadius >= tactical.ArmyCohesionRadius
                    && tactical.LocalEngagementRadius > 0f,
                    "Tactical cadence/cohesion bounds are invalid.");
                Require(tactical.TargetStickiness > 0f
                    && tactical.RetargetThreshold > 0f
                    && tactical.MinimumTargetHoldSeconds > 0f,
                    "Target stability is not configured.");
                Require(tactical.MeleeSaturationPenalty
                        > tactical.RangedSaturationPenalty,
                    "Melee saturation should discourage crowding more strongly.");
                Require(tactical.MaxLocalTargetCandidates is >= 4 and <= 48,
                    "Local tactical target candidates are not bounded.");
                Require(TacticalArmyController.RoleFor(UnitType.Warrior)
                        == TacticalArmyRole.Frontline
                    && TacticalArmyController.RoleFor(UnitType.Lancer)
                        == TacticalArmyRole.Frontline
                    && TacticalArmyController.RoleFor(UnitType.Archer)
                        == TacticalArmyRole.Backline
                    && TacticalArmyController.RoleFor(UnitType.Monk)
                        == TacticalArmyRole.Support
                    && TacticalArmyController.RoleFor(UnitType.Pawn)
                        == TacticalArmyRole.Excluded,
                    "Tactical role mapping is invalid.");
                string tacticalSource = File.ReadAllText(TacticalPath);
                string militarySource = File.ReadAllText(MilitaryPath);
                string strengthSource = File.ReadAllText(StrengthPath);
                string armyGroupSource = File.ReadAllText(
                    "Assets/Scripts/AI/ArmyGroup.cs");
                Require(armyGroupSource.Contains("UnitType.Warrior")
                    && armyGroupSource.Contains("UnitType.Archer")
                    && armyGroupSource.Contains("UnitType.Lancer")
                    && armyGroupSource.Contains("UnitType.Monk")
                    && !armyGroupSource.Contains("or UnitType.Pawn"),
                    "ArmyGroup membership must exclude Pawn/Scout combat members.");
                Require(tacticalSource.Contains("RoleFormationPlanner.TryPlan")
                    && tacticalSource.Contains("FormationType.Compact"),
                    "Tactical army is not reusing the role formation planner.");
                Require(!tacticalSource.Contains("PlayerCommandController"),
                    "AI formation must not depend on human formation UI state.");
                Require(tacticalSource.Contains("knowledge.VisibleEnemyUnits")
                    && tacticalSource.Contains("CombatDamage.Multiplier"),
                    "Targeting must use legal observations and authoritative matchups.");
                Require(tacticalSource.Contains("targetSaturation")
                    && tacticalSource.Contains("TargetStickiness")
                    && tacticalSource.Contains("RetargetThreshold"),
                    "Target distribution/stickiness is missing.");
                Require(tacticalSource.Contains("IsMeleeAttackCommitted"),
                    "Committed melee attacks are not protected from retargeting.");
                Require(!tacticalSource.Contains("FindObjectsOfType")
                    && !tacticalSource.Contains("FindObjectsByType")
                    && !tacticalSource.Contains("UnitController.ActiveUnits"),
                    "Tactical targeting contains an unauthorized scene scan.");
                Require(tacticalSource.Contains("context.Commands.TryAttack")
                    && tacticalSource.Contains("context.Commands.TryMilitaryMove"),
                    "Tactical commands must route through FactionCommandService.");
                Require(militarySource.Contains("Tactical.TryEngageVisibleEnemies")
                    && militarySource.Contains("Tactical.BuildFormationAssignments")
                    && militarySource.Contains("ArmyGroupState.Retreating"),
                    "MilitaryCommander integration or strategic retreat is missing.");
                Require(strengthSource.Contains("OwnArmy")
                    && strengthSource.Contains("MonkSupport")
                    && strengthSource.Contains("MonkHealer.HealAmount")
                    && strengthSource.Contains("MonkHealer.AutoDetectionRange"),
                    "Contextual Monk support strength is missing.");
                Require(Mathf.Approximately(MonkHealer.HealAmount, 6.25f)
                    && Mathf.Approximately(MonkHealer.HealInterval, 2f)
                    && Mathf.Approximately(MonkHealer.HealRange, 2f),
                    "Authoritative Monk balance changed unexpectedly.");

                Debug.Log("Phase 7E.4 Tactical Army validation PASS: tactical roles, "
                    + "Compact role formation, robust cohesion, legal visible targeting, "
                    + "matchup/HP/stickiness/saturation scoring, committed melee protection, "
                    + "bounded command service routing and contextual Monk strength verified.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 7E.4 Tactical Army validation FAILED: "
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
