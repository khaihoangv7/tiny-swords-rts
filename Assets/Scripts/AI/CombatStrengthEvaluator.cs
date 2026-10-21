using System;
using System.Collections.Generic;
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
        [SerializeField, Min(0f)] private float goblinTorchValue = 1f;
        [SerializeField, Min(0f)] private float goblinTntValue = 1.2f;
        [SerializeField, Min(0f)] private float goblinBarrelValue = 1.4f;

        public float WarriorValue => warriorValue > 0f ? warriorValue : 1f;
        public float ArcherValue => archerValue > 0f ? archerValue : 0.75f;
        public float LancerValue => lancerValue > 0f ? lancerValue : 0.60f;
        public float MonkSupportValue => monkSupportValue > 0f ? monkSupportValue : 0.45f;
        public float TowerThreatValue => towerThreatValue > 0f ? towerThreatValue : 2f;
        public float CastleThreatValue => castleThreatValue > 0f ? castleThreatValue : 3f;
        public float GoblinTorchValue => goblinTorchValue > 0f ? goblinTorchValue : 1f;
        public float GoblinTntValue => goblinTntValue > 0f ? goblinTntValue : 1.2f;
        public float GoblinBarrelValue => goblinBarrelValue > 0f ? goblinBarrelValue : 1.4f;

        public float UnitBaseValue(UnitType type)
            => type switch
            {
                UnitType.Warrior => WarriorValue,
                UnitType.Archer => ArcherValue,
                UnitType.Lancer => LancerValue,
                UnitType.Monk => MonkSupportValue,
                UnitType.GoblinTorch => GoblinTorchValue,
                UnitType.GoblinTNT => GoblinTntValue,
                UnitType.GoblinBarrel => GoblinBarrelValue,
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
        public static float OwnArmy(IReadOnlyList<UnitController> members,
            CombatStrengthTuning tuning, out float directStrength,
            out float supportStrength)
        {
            directStrength = 0f;
            supportStrength = 0f;
            if (members == null || tuning == null) return 0f;
            for (int i = 0; i < members.Count; i++)
            {
                UnitController member = members[i];
                HealthComponent health = member != null
                    ? member.GetComponent<HealthComponent>() : null;
                if (health?.CombatData == null || !health.IsAlive) continue;
                if (health.CombatData.UnitType == UnitType.Monk)
                    supportStrength += MonkSupport(member, members, tuning);
                else directStrength += OwnUnit(member, tuning);
            }
            return directStrength + supportStrength;
        }

        public static float MonkSupport(UnitController monk,
            IReadOnlyList<UnitController> army, CombatStrengthTuning tuning)
        {
            HealthComponent monkHealth = monk != null
                ? monk.GetComponent<HealthComponent>() : null;
            if (monkHealth?.CombatData == null || !monkHealth.IsAlive
                || monkHealth.CombatData.UnitType != UnitType.Monk
                || army == null || tuning == null) return 0f;

            int nearbyCombatUnits = 0;
            float nearbyMissingHealth = 0f;
            float radiusSquared = MonkHealer.AutoDetectionRange
                * MonkHealer.AutoDetectionRange;
            for (int i = 0; i < army.Count; i++)
            {
                UnitController ally = army[i];
                HealthComponent allyHealth = ally != null
                    ? ally.GetComponent<HealthComponent>() : null;
                UnitType? type = allyHealth?.CombatData?.UnitType;
                if (ally == monk || allyHealth == null || !allyHealth.IsAlive
                    || type is not (UnitType.Warrior or UnitType.Lancer
                        or UnitType.Archer)
                    || ((Vector2)(ally.transform.position
                        - monk.transform.position)).sqrMagnitude > radiusSquared) continue;
                nearbyCombatUnits++;
                nearbyMissingHealth += 1f - HealthRatio(allyHealth.CurrentHP,
                    allyHealth.MaxHP);
            }

            float coverage = Mathf.Clamp01(nearbyCombatUnits / 3f);
            float injuryNeed = nearbyCombatUnits > 0
                ? Mathf.Clamp01(nearbyMissingHealth / nearbyCombatUnits) : 0f;
            float throughput = Mathf.Clamp01((MonkHealer.HealAmount
                / MonkHealer.HealInterval) / 10f);
            float context = 0.15f + coverage * 0.55f
                + injuryNeed * 0.20f + throughput * 0.10f;
            return tuning.MonkSupportValue
                * HealthRatio(monkHealth.CurrentHP, monkHealth.MaxHP)
                * Mathf.Clamp01(context);
        }

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
