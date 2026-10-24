#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7E2AdaptiveEconomyValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.2 Adaptive Economy")]
        public static void Validate()
        {
            try
            {
                BuildingData house = Resources.Load<BuildingData>("Buildings/House");
                BuildingData tower = Resources.Load<BuildingData>("Buildings/Tower");
                Require(house != null && house.WoodCost == 80,
                    "House authoritative cost must remain exactly 80 Wood.");
                Require(tower != null && tower.WoodCost == 125 && tower.GoldCost == 85,
                    "Economic outposts must use the authoritative Tower cost.");
                Require(typeof(BuildingResourceDropOff).GetInterface(
                        nameof(IResourceDropOff)) != null,
                    "Completed Towers must use the generic resource drop-off contract.");

                MethodInfo adaptiveTick = typeof(EconomyCommanderPolicy).GetMethod("Tick",
                    new[] { typeof(CommanderDecisionContext), typeof(CommanderPawnRoles),
                        typeof(StrategicSafetyMap), typeof(EconomicDemandModel) });
                Require(adaptiveTick != null,
                    "Economy policy is not consuming the centralized demand model.");
                MethodInfo outpostTick = typeof(BuildingCommanderPolicy).GetMethod("Tick",
                    new[] { typeof(CommanderDecisionContext), typeof(CommanderPawnRoles),
                        typeof(StrategicSafetyMap), typeof(EconomicDemandModel),
                        typeof(EconomyCommanderPolicy) });
                Require(outpostTick != null,
                    "Building policy is not consuming demand and worker opportunity cost.");

                var demand = new EconomicDemandModel();
                demand.DesiredCounts(11, out int baseFood, out int baseWood,
                    out int baseGold);
                Require(baseFood + baseWood + baseGold == 11,
                    "Worker allocation lost a worker in balanced demand.");
                SetAutoProperty(demand, "WoodDeficit", 240);
                demand.DesiredCounts(11, out int woodFood, out int woodWorkers,
                    out int woodGold);
                Require(woodFood + woodWorkers + woodGold == 11
                    && woodWorkers > baseWood,
                    "A severe Wood deficit must increase Wood allocation deterministically.");
                SetAutoProperty(demand, "WoodDeficit", 0);
                SetAutoProperty(demand, "FoodDeficit", 240);
                demand.DesiredCounts(11, out int foodWorkers, out _, out _);
                Require(foodWorkers > baseFood,
                    "A severe Food deficit must increase Food allocation.");

                RequireNoLiveResourceMemory(typeof(EconomicClusterSnapshot));
                RequireNoLiveResourceMemory(typeof(EconomicResourceClusterPlanner));
                Require(typeof(EconomicResourceClusterPlanner).GetProperty("Clusters") != null
                    && typeof(EconomicResourceClusterPlanner).GetProperty("Best") != null,
                    "Bounded resource-cluster diagnostics are unavailable.");
                Require(typeof(EconomyCommanderPolicy).GetProperty("ReassignmentSerial") != null
                    && typeof(EconomyCommanderPolicy).GetProperty(
                        "LastReassignmentReason") != null,
                    "Worker reassignment diagnostics are unavailable.");
                Require(typeof(RuleBasedCommander).GetProperty("EconomicDemand") != null,
                    "RuleBasedCommander does not expose centralized economic diagnostics.");

                Debug.Log("Phase 7E.2 Adaptive Economy validation PASS: authoritative "
                    + "80W House and Tower costs, centralized deficits, deterministic "
                    + "worker allocation, bounded reassignments, value-only resource "
                    + "clusters, economic Tower outposts and drop-off integration verified.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 7E.2 Adaptive Economy validation FAILED: " + exception);
                throw;
            }
        }

        private static void SetAutoProperty(object target, string property, int value)
        {
            FieldInfo field = target.GetType().GetField($"<{property}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, $"Missing demand diagnostic {property}.");
            field.SetValue(target, value);
        }

        private static void RequireNoLiveResourceMemory(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance
                | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                Type fieldType = fields[i].FieldType;
                Type element = fieldType.IsArray ? fieldType.GetElementType() : null;
                if (fieldType.IsGenericType)
                {
                    Type[] args = fieldType.GetGenericArguments();
                    if (args.Length == 1) element = args[0];
                }
                Require(fieldType != typeof(ResourceNode)
                    && fieldType != typeof(Transform)
                    && fieldType != typeof(GameObject)
                    && element != typeof(ResourceNode)
                    && element != typeof(Transform)
                    && element != typeof(GameObject),
                    $"{type.Name}.{fields[i].Name} illegally retains a live hidden object.");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
