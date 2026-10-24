#if UNITY_EDITOR
using System;
using TinyKingdom.AI;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7E6ContestedGoldMineValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.6 Contested Gold Mine Control")]
        public static void Validate()
        {
            Require(GoldMineObjective.PawnCapacity == 3
                && GoldMineObjective.MaximumGold == 2000
                && Mathf.Approximately(GoldMineObjective.MineLoadMultiplier, 2.5f)
                && GoldMineObjective.MineGoldPerTrip(20) == 50
                && Mathf.Approximately(GoldMineObjective.MiningSeconds(20, 2, 1.5f), 15f),
                "Mine extraction duration, load, or capacity is inconsistent.");
            Require(Enum.IsDefined(typeof(StrategicIntent),
                    StrategicIntent.SecureGoldMine),
                "SecureGoldMine is not an integrated strategic intent.");
            var mine = new GoldMineStrategyModel();
            Require(mine.MaximumCandidates is >= 1 and <= 8,
                "Mine candidate scoring is not bounded.");
            Require(new CombatStrengthTuning().UnitBaseValue(
                    UnitType.GoblinTorch) > 0f
                && new CombatStrengthTuning().UnitBaseValue(
                    UnitType.GoblinTNT) > 0f
                && new CombatStrengthTuning().UnitBaseValue(
                    UnitType.GoblinBarrel) > 0f,
                "Goblin strength is missing from the existing evaluator.");
            Require(!GoldMineStrategyModel.HasSufficientForce(1f, 4f,
                    1.35f, 8, 6)
                && !GoldMineStrategyModel.HasSufficientForce(9f, 4f,
                    1.35f, 4, 6)
                && GoldMineStrategyModel.HasSufficientForce(9f, 4f,
                    1.35f, 8, 6),
                "Known guard strength and available population do not gate assault.");
            var production = new ProductionCommanderPolicy();
            int ordinaryTarget = production.TargetMilitaryPopulation;
            production.RequestStrategicMilitaryPopulation(ordinaryTarget + 4, 10f);
            Require(production.TargetMilitaryPopulation == ordinaryTarget + 4,
                "Known guards cannot request bounded reinforcements.");
            production.Reset();
            Require(production.TargetMilitaryPopulation == ordinaryTarget,
                "Temporary reinforcement target survives a commander reset.");
            Require(!GoldMineStrategyModel.IsSafeForMiners(false,
                    1f, 0.65f, false, false, true)
                && !GoldMineStrategyModel.IsSafeForMiners(true,
                    1f, 0.65f, true, false, true)
                && !GoldMineStrategyModel.IsSafeForMiners(true,
                    1f, 0.65f, false, true, true)
                && !GoldMineStrategyModel.IsSafeForMiners(true,
                    0.3f, 0.65f, false, false, true)
                && GoldMineStrategyModel.IsSafeForMiners(true,
                    1f, 0.65f, false, false, true),
                "Miner safety gate does not distinguish observation and threat.");
            Debug.Log("Phase 7E.6 structural validation PASS: preserved mine "
                + "authority, bounded candidate evaluation, Goblin combat values, "
                + "strength readiness and miner safety gates. Play Mode behavior "
                + "requires separate validation.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
