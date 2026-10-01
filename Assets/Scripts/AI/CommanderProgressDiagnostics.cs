using System;
using TinyKingdom.Economy;

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

        private void MarkMeaningful(float time)
        {
            if (time < 0f) return;
            LastMeaningfulActionTime = time;
            nextStallWarningTime = 0f;
        }
    }
}
