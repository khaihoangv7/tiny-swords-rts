using System;
using System.Text;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.AI
{
    // Low-frequency observation of Commander progress. It reports stalls but never
    // changes gameplay, grants resources, teleports actors or issues commands.
    [Serializable]
    public sealed class CommanderProgressDiagnostics
    {
        private bool initialized;
        private uint gatherSerial;
        private uint buildSerial;
        private uint trainSerial;
        private uint scoutSerial;
        private uint offensiveSerial;
        private uint targetSerial;
        private ArmyGroupState armyState;
        private int food;
        private int wood;
        private int gold;
        private float nextStallWarningTime;
        private float nextMilitaryLivenessWarningTime;

        public float LastGatherAssignmentTime { get; private set; } = -1f;
        public float LastBuildCommandTime { get; private set; } = -1f;
        public float LastTrainCommandTime { get; private set; } = -1f;
        public float LastScoutDestinationTime { get; private set; } = -1f;
        public float LastArmyStateChangeTime { get; private set; } = -1f;
        public float LastOffensiveLaunchTime { get; private set; } = -1f;
        public float LastTargetCompletionTime { get; private set; } = -1f;
        public float LastResourceIncomeTime { get; private set; } = -1f;
        public float LastMeaningfulActionTime { get; private set; } = -1f;

        public void Reset()
        {
            initialized = false;
            gatherSerial = buildSerial = trainSerial = scoutSerial = 0;
            offensiveSerial = targetSerial = 0;
            armyState = ArmyGroupState.Forming;
            food = wood = gold = 0;
            nextStallWarningTime = 0f;
            nextMilitaryLivenessWarningTime = 0f;
            LastGatherAssignmentTime = LastBuildCommandTime = -1f;
            LastTrainCommandTime = LastScoutDestinationTime = -1f;
            LastArmyStateChangeTime = LastOffensiveLaunchTime = -1f;
            LastTargetCompletionTime = LastResourceIncomeTime = -1f;
            LastMeaningfulActionTime = -1f;
        }

        public void Observe(float time, FactionKnowledge knowledge,
            EconomyCommanderPolicy economy, BuildingCommanderPolicy building,
            ProductionCommanderPolicy production, ScoutingCommanderPolicy scouting,
            MilitaryCommanderPolicy military)
        {
            if (knowledge?.Stockpile == null || economy == null || building == null
                || production == null || scouting == null || military == null) return;

            ResourceStockpile stockpile = knowledge.Stockpile;
            int currentFood = stockpile.GetAmount(ResourceType.Food);
            int currentWood = stockpile.GetAmount(ResourceType.Wood);
            int currentGold = stockpile.GetAmount(ResourceType.Gold);
            if (!initialized)
            {
                initialized = true;
                gatherSerial = economy.SuccessfulAssignmentSerial;
                buildSerial = building.SuccessfulCommandSerial;
                trainSerial = production.SuccessfulTrainSerial;
                scoutSerial = scouting.SuccessfulDestinationSerial;
                offensiveSerial = military.OffensiveLaunchSerial;
                targetSerial = military.TargetCompletionSerial;
                armyState = military.Army.State;
                food = currentFood;
                wood = currentWood;
                gold = currentGold;
                LastArmyStateChangeTime = time;
                LastMeaningfulActionTime = time;
                return;
            }

            if (gatherSerial != economy.SuccessfulAssignmentSerial)
            {
                gatherSerial = economy.SuccessfulAssignmentSerial;
                LastGatherAssignmentTime = economy.LastSuccessfulAssignmentTime;
                MarkMeaningful(LastGatherAssignmentTime);
            }
            if (buildSerial != building.SuccessfulCommandSerial)
            {
                buildSerial = building.SuccessfulCommandSerial;
                LastBuildCommandTime = building.LastSuccessfulCommandTime;
                MarkMeaningful(LastBuildCommandTime);
            }
            if (trainSerial != production.SuccessfulTrainSerial)
            {
                trainSerial = production.SuccessfulTrainSerial;
                LastTrainCommandTime = production.LastSuccessfulTrainTime;
                MarkMeaningful(LastTrainCommandTime);
            }
            if (scoutSerial != scouting.SuccessfulDestinationSerial)
            {
                scoutSerial = scouting.SuccessfulDestinationSerial;
                LastScoutDestinationTime = scouting.LastSuccessfulDestinationTime;
                MarkMeaningful(LastScoutDestinationTime);
            }
            if (armyState != military.Army.State)
            {
                armyState = military.Army.State;
                LastArmyStateChangeTime = time;
                MarkMeaningful(time);
            }
            if (offensiveSerial != military.OffensiveLaunchSerial)
            {
                offensiveSerial = military.OffensiveLaunchSerial;
                LastOffensiveLaunchTime = military.LastOffensiveLaunchTime;
                MarkMeaningful(LastOffensiveLaunchTime);
            }
            if (targetSerial != military.TargetCompletionSerial)
            {
                targetSerial = military.TargetCompletionSerial;
                LastTargetCompletionTime = military.LastTargetCompletionTime;
                MarkMeaningful(LastTargetCompletionTime);
            }
            if (currentFood > food || currentWood > wood || currentGold > gold)
            {
                LastResourceIncomeTime = time;
                MarkMeaningful(time);
            }
            food = currentFood;
            wood = currentWood;
            gold = currentGold;
        }

        public bool TryConsumeStallWarning(float time, float threshold,
            out float stalledSeconds)
        {
            stalledSeconds = initialized && LastMeaningfulActionTime >= 0f
                ? time - LastMeaningfulActionTime : 0f;
            if (!initialized || threshold <= 0f || stalledSeconds < threshold
                || time < nextStallWarningTime) return false;
            nextStallWarningTime = time + threshold;
            return true;
        }

        public bool TryCaptureMilitaryLivenessStall(float time, float threshold,
            FactionKnowledge knowledge, StrategicIntentEvaluator strategy,
            MilitaryCommanderPolicy military, out string snapshot)
        {
            snapshot = null;
            if (knowledge == null || strategy == null || military == null
                || time < nextMilitaryLivenessWarningTime
                || time - military.LastMilitaryProgressTime < threshold
                || !ExpectsMilitaryActivity(strategy.CurrentIntent,
                    military.Army.State)) return false;

            nextMilitaryLivenessWarningTime = time + Mathf.Max(5f, threshold);
            snapshot = BuildMilitarySnapshot(time, knowledge, strategy, military);
            return true;
        }

        private static bool ExpectsMilitaryActivity(StrategicIntent intent,
            ArmyGroupState state)
            => intent is StrategicIntent.PrepareAttack or StrategicIntent.Attack
                || state is ArmyGroupState.Forming or ArmyGroupState.Regrouping
                    or ArmyGroupState.PreparingAttack or ArmyGroupState.Advancing
                    or ArmyGroupState.Assaulting;

        private static string BuildMilitarySnapshot(float time,
            FactionKnowledge knowledge, StrategicIntentEvaluator strategy,
            MilitaryCommanderPolicy military)
        {
            var text = new StringBuilder(2048);
            text.Append("AI MILITARY LIVENESS STALL\nintent=")
                .Append(strategy.CurrentIntent).Append(" age=")
                .Append(strategy.IntentAge(time).ToString("0.0"))
                .Append(" previous=").Append(strategy.PreviousIntent)
                .Append(" reason=").Append(strategy.TransitionReason)
                .Append(" lastStrategicEval=")
                .Append(strategy.LastEvaluationTime.ToString("0.0"))
                .Append("\ntactical=").Append(military.Tactical.State)
                .Append(" lastTacticalEval=")
                .Append(military.Tactical.LastEvaluationTime.ToString("0.0"))
                .Append(" army=").Append(military.Army.Members.Count)
                .Append(" anchor=").Append(military.Tactical.ArmyAnchor)
                .Append(" stragglers=").Append(military.Tactical.StragglerCount)
                .Append(" visibleEnemies=").Append(military.Tactical.VisibleEnemyCount)
                .Append(" staging=").Append(military.HasStagingPosition
                    ? military.StagingPosition.ToString() : "Invalid")
                .Append("\nlastProgress=")
                .Append(military.LastMilitaryProgressTime.ToString("0.0"))
                .Append(" lastAccepted=")
                .Append(military.LastAcceptedMilitaryCommandTime.ToString("0.0"))
                .Append(" lastRejected=")
                .Append(military.LastRejectedMilitaryCommandTime.ToString("0.0"))
                .Append(" rejection=").Append(military.LastMilitaryCommandResult)
                .Append(" lastPathRequest=")
                .Append(military.LastMilitaryPathRequestTime.ToString("0.0"));

            if (strategy.TryGetTarget(out StrategicTargetSnapshot target))
                text.Append("\ntarget=").Append(target.RuntimeId.ToString())
                    .Append(" type=").Append(target.BuildingType)
                    .Append(" cell=").Append(knowledge.TeamVision?.GridManager != null
                        ? knowledge.TeamVision.GridManager.WorldToGrid(
                            target.LastKnownPosition).ToString() : "Unknown")
                    .Append(" age=")
                    .Append(Mathf.Max(0f, time - target.LastSeenTime).ToString("0.0"));
            else text.Append("\ntarget=None");

            int count = Mathf.Min(32, military.Army.Members.Count);
            for (int i = 0; i < count; i++)
            {
                UnitController unit = military.Army.Members[i];
                if (unit == null) continue;
                UnitMovement movement = unit.GetComponent<UnitMovement>();
                CombatController combat = unit.GetComponent<CombatController>();
                UnitType type = unit.GetComponent<HealthComponent>()?.CombatData != null
                    ? unit.GetComponent<HealthComponent>().CombatData.UnitType
                    : UnitType.Pawn;
                text.Append("\nunit=").Append(unit.GetEntityId().ToString())
                    .Append(" type=").Append(type)
                    .Append(" group=").Append(military.Army.State)
                    .Append(" role=").Append(TacticalArmyController.RoleFor(type))
                    .Append(" state=").Append(unit.CurrentState)
                    .Append(" combat=").Append(combat?.Directive.ToString() ?? "None")
                    .Append(" command=").Append(combat?.BuildingTarget != null
                        ? "AttackBuilding" : combat?.Target != null ? "Attack"
                        : movement != null && movement.IsMoving ? "Move" : "Idle")
                    .Append(" target=").Append(combat?.Target != null
                        ? combat.Target.GetEntityId().ToString()
                        : combat?.BuildingTarget != null
                            ? combat.BuildingTarget.GetEntityId().ToString() : "None")
                    .Append(" destination=").Append(movement != null
                        && movement.HasCommandDestination
                            ? movement.CommandDestinationCell.ToString() : "None")
                    .Append(" pathActive=").Append(movement != null && movement.IsMoving)
                    .Append(" pathStale=").Append(movement != null && movement.IsMoving
                        && time - movement.LastMovementProgressTime
                            >= military.MilitaryNoProgressSeconds)
                    .Append(" pathFailures=").Append(movement?.PathFailureCount ?? 0)
                    .Append(" lastMove=")
                    .Append((movement?.LastMovementProgressTime ?? -1f).ToString("0.0"))
                    .Append(" lastCommand=")
                    .Append((movement?.LastAcceptedCommandTime ?? -1f).ToString("0.0"));
                if (movement != null && movement.HasCommandDestination
                    && knowledge.TeamVision?.GridManager != null)
                {
                    Vector3 destination = knowledge.TeamVision.GridManager.GridToWorld(
                        movement.CommandDestinationCell.x,
                        movement.CommandDestinationCell.y);
                    text.Append(" distance=")
                        .Append(Vector2.Distance(unit.transform.position, destination)
                            .ToString("0.00"));
                }
                if (military.TryGetLastCommandResult(unit,
                        out CommanderCommandResult lastResult))
                    text.Append(" lastResult=").Append(lastResult);
                bool targetVisible = combat?.Target != null
                    && knowledge.TryGetVisibleEnemy(combat.Target.GetEntityId(), out _)
                    || combat?.BuildingTarget != null
                    && knowledge.TryGetVisibleEnemyBuilding(
                        combat.BuildingTarget.GetEntityId(), out _);
                text.Append(" committed=")
                    .Append(combat != null && combat.IsMeleeAttackCommitted)
                    .Append(" targetVisible=").Append(targetVisible);
                if (military.Tactical.FormationDestinations.TryGetValue(unit,
                        out Vector3 slot))
                    text.Append(" slot=").Append(slot);
            }
            return text.ToString();
        }

        private void MarkMeaningful(float time)
        {
            if (time < 0f) return;
            LastMeaningfulActionTime = time;
            nextStallWarningTime = 0f;
        }
    }
}
