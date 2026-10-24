#if UNITY_EDITOR
using System;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase5C2DefenseValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 5C2 Defense")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Run Phase 5C2 Defense validation in Play Mode.");

            BuildingData tower = Resources.Load<BuildingData>("Buildings/Tower");
            BuildingData castle = Resources.Load<BuildingData>("Buildings/Castle");
            Require(tower != null && castle != null, "Tower/Castle BuildingData is missing.");
            Require(tower.MaxHP == 300 && tower.WoodCost == 125 && tower.GoldCost == 85,
                "Tower HP/cost regression.");
            Require(tower.InternalGarrisonCapacity == 3 && tower.MountedArcherCapacity == 1,
                "Tower capacities must be internal 3, mounted 1.");
            Require(castle.InternalGarrisonCapacity == 5 && castle.MountedArcherCapacity == 3,
                "Castle capacities must be internal 5, mounted 3.");
            Require(tower.DefensiveArrowDamage == 5 && castle.DefensiveArrowDamage == 5,
                "Generic defensive arrow damage must be 5.");
            Require(Mathf.Approximately(tower.DefensiveVolleyInterval, 1.5f)
                && Mathf.Approximately(castle.DefensiveVolleyInterval, 1.5f),
                "Defensive volley interval must be the provisional 1.5 seconds.");
            Require(Mathf.Approximately(tower.MountedDamageMultiplier, 1.2f)
                && Mathf.Approximately(castle.MountedRangeMultiplier, 1.25f),
                "Mounted Archer modifiers are incorrect.");

            BuildingGarrison blueCastle = UnityEngine.Object.FindObjectsByType<BuildingGarrison>()
                .FirstOrDefault(item => item.Faction == Faction.Blue
                    && item.GetComponent<CastleDropOff>() != null);
            Require(blueCastle != null && blueCastle.InternalCapacity == 5
                && blueCastle.MountedCapacity == 3, "Blue Castle garrison setup is missing.");
            Require(blueCastle.GetComponent<BuildingDefense>() != null
                && blueCastle.GetComponent<BuildingCombatTarget>() != null
                && blueCastle.GetComponent<BuildingVisionSource>() != null,
                "Blue Castle defense components are incomplete.");
            Require(UnityEngine.Object.FindObjectsByType<BuildingCombatTarget>()
                .Any(item => item.Faction == Faction.Red),
                "Map needs an enemy building combat target.");

            int genericGuarded = CombatDamage.Calculate(5, UnitType.Archer, UnitType.Warrior,
                AttackDelivery.Projectile, true, 1f, false);
            int normalArcher = CombatDamage.Calculate(10, UnitType.Archer, UnitType.Warrior,
                AttackDelivery.Projectile, false);
            int mountedArcher = CombatDamage.Calculate(10, UnitType.Archer, UnitType.Warrior,
                AttackDelivery.Projectile, false, 1.2f, true);
            Require(genericGuarded == 3,
                "Generic arrow must ignore counters and retain projectile Guard mitigation.");
            Require(normalArcher == 8 && mountedArcher == 10,
                "Mounted damage must multiply normal counter-adjusted Archer damage by 1.20.");
            Require(VisionSourceRegistry.ActiveSources.Count > 0,
                "Vision source registry has no active sources.");

            Debug.Log("Phase 5C2 defense validation passed: Tower 3+1, Castle 5+3, "
                + "5-damage generic arrows, 1.5s volleys, mounted x1.20 damage/x1.25 range, "
                + "building targets and vision sources.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
