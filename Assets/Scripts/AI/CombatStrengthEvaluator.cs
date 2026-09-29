using System;
using TinyKingdom.Buildings;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.AI
{
    public enum RetreatReason
    {
        None,
        HeavyCasualties,
        LowAverageHealth,
        EnemyStrengthOverwhelming,
        ArmyFragmented,
        CriticalSupportLoss,
        OperationalFailure
    }

    [Serializable]
    public sealed class CombatStrengthTuning
    {
        [SerializeField, Min(0f)] private float warriorValue = 1f;
        [SerializeField, Min(0f)] private float archerValue = 0.75f;
        [SerializeField, Min(0f)] private float lancerValue = 0.60f;
        [SerializeField, Min(0f)] private float monkSupportValue = 0.45f;
        [SerializeField, Min(0f)] private float towerThreatValue = 2f;
        [SerializeField, Min(0f)] private float castleThreatValue = 3f;

        public float WarriorValue => warriorValue > 0f ? warriorValue : 1f;
        public float ArcherValue => archerValue > 0f ? archerValue : 0.75f;
        public float LancerValue => lancerValue > 0f ? lancerValue : 0.60f;
        public float MonkSupportValue => monkSupportValue > 0f ? monkSupportValue : 0.45f;
        public float TowerThreatValue => towerThreatValue > 0f ? towerThreatValue : 2f;
        public float CastleThreatValue => castleThreatValue > 0f ? castleThreatValue : 3f;

        public float UnitBaseValue(UnitType type)
            => type switch
            {
                UnitType.Warrior => WarriorValue,
                UnitType.Archer => ArcherValue,
                UnitType.Lancer => LancerValue,
                UnitType.Monk => MonkSupportValue,
                _ => 0f
            };

        public float BuildingBaseValue(BuildingType type)
            => type switch
            {
                BuildingType.Tower => TowerThreatValue,
                BuildingType.Castle => CastleThreatValue,
                _ => 0f
            };
    }

    public static class CombatStrengthEvaluator
    {
        public static float OwnUnit(UnitController unit, CombatStrengthTuning tuning)
        {
            HealthComponent health = unit != null ? unit.GetComponent<HealthComponent>() : null;
            if (health?.CombatData == null || !health.IsAlive || tuning == null) return 0f;
            return tuning.UnitBaseValue(health.CombatData.UnitType)
                * HealthRatio(health.CurrentHP, health.MaxHP);
        }

        public static float VisibleEnemyUnit(VisibleUnitObservation unit,
            CombatStrengthTuning tuning)
            => tuning == null ? 0f : tuning.UnitBaseValue(unit.UnitType)
                * HealthRatio(unit.CurrentHP, unit.MaxHP);

        public static float VisibleDefensiveBuilding(BuildingKnowledgeSnapshot building,
            CombatStrengthTuning tuning)
            => tuning == null ? 0f : tuning.BuildingBaseValue(building.BuildingType)
                * Mathf.Clamp01(building.LastSeenHealthRatio);

        public static float HealthRatio(int current, int maximum)
            => maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
    }
}
