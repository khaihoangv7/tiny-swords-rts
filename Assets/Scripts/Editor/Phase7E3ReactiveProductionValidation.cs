#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7E3ReactiveProductionValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.3 Reactive Production")]
        public static void Validate()
        {
            try
            {
                var production = new ProductionCommanderPolicy();
                Require(production.EnemyComposition != null,
                    "Enemy composition memory is not configured.");
                Require(production.EnemyComposition.DecaySeconds > 0f,
                    "Enemy intelligence decay must be configured.");
                Require(production.StrategyReevaluationSeconds > 0f,
                    "Production reevaluation cadence must be positive.");
                Require(production.DesiredCompositionHoldSeconds > 0f
                    && production.MeaningfulDesiredChange > 0f,
                    "Composition hold and change threshold must be configured.");
                Require(production.MaximumReactiveInfluence > 0f
                    && production.MaximumReactiveInfluence < 1f
                    && production.CombatDiversityFloor > 0f,
                    "Reactive cap and composition diversity floor are invalid.");
                Require(production.BaselineWarriorWeight > 0f
                    && production.BaselineArcherWeight > 0f
                    && production.BaselineLancerWeight > 0f,
                    "Configurable mixed baseline composition is invalid.");
                Require(production.MonkSupportRatio > 0f && production.MonkLimit > 0,
                    "Monk support ratio/cap are not configured.");

                Require(Mathf.Approximately(
                        CombatMatchupEvaluator.Effectiveness(UnitType.Lancer,
                            UnitType.Warrior),
                        CombatDamage.Multiplier(UnitType.Lancer, UnitType.Warrior)),
                    "Counter evaluator must reuse CombatDamage.Multiplier.");
                Require(UnitProductionQueue.CanProduce(BuildingType.Barracks,
                        UnitType.Warrior)
                    && UnitProductionQueue.CanProduce(BuildingType.Barracks,
                        UnitType.Lancer)
                    && UnitProductionQueue.CanProduce(BuildingType.Archery,
                        UnitType.Archer)
                    && UnitProductionQueue.CanProduce(BuildingType.Monastery,
                        UnitType.Monk), "Production-building mapping is invalid.");

                var memory = new EnemyCompositionMemory();
                memory.Observe(new MilitaryObservationSnapshot(3, 1, 0, 1, 10f,
                    new Vector3(4f, 2f)), 10f);
                Require(memory.ObservedCount(UnitType.Warrior) == 3
                    && memory.ObservedCount(UnitType.Archer) == 1
                    && memory.ObservedCount(UnitType.Monk) == 1,
                    "Legal visible military snapshot was not copied correctly.");
                memory.Observe(new MilitaryObservationSnapshot(1, 0, 0, 0, 20f,
                    new Vector3(8f, 2f)), 20f);
                Require(memory.ObservedCount(UnitType.Warrior) == 3
                    && memory.ObservedCount(UnitType.Archer) == 1
                    && Mathf.Approximately(memory.LastSeenTimeFor(UnitType.Archer), 10f)
                    && memory.EvidenceCount(UnitType.Warrior, 20f) >= 1f,
                    "A partial sighting illegally erased or refreshed hidden evidence.");
                Require(Mathf.Approximately(memory.ConfidenceAt(10f), 1f)
                    && memory.ConfidenceAt(20f + memory.FullConfidenceSeconds
                        + memory.DecaySeconds + 1f) <= 0f,
                    "Enemy intelligence confidence does not decay as configured.");
                RequireNoHiddenLiveReferences(typeof(EnemyCompositionMemory));

                MethodInfo reactiveTick = typeof(ProductionCommanderPolicy).GetMethod("Tick",
                    new[] { typeof(CommanderDecisionContext),
                        typeof(BuildingCommanderPolicy) });
                Require(reactiveTick != null,
                    "Production policy is not integrated with BuildingCommander.");
                Require(typeof(BuildingCommanderPolicy).GetMethod(
                        "RequestProductionInfrastructure") != null,
                    "Missing production-infrastructure request API.");
                var building = new BuildingCommanderPolicy();
                Require(building.ProductionNeedHoldSeconds > 0f
                    && building.ProductionNeedRequestCooldown > 0f,
                    "Infrastructure request hysteresis is not configured.");
                Require(typeof(ProductionCommanderPolicy).GetMethod(
                        "TryGetPendingEconomicCost") != null,
                    "7E.2 EconomicDemandModel integration is missing.");
                Require(typeof(ProductionCommanderPolicy).GetMethod("QueuedCount") != null,
                    "Projected army cannot expose queued unit counts.");

                string productionSource = File.ReadAllText(
                    "Assets/Scripts/AI/ProductionCommanderPolicy.cs");
                string memorySource = File.ReadAllText(
                    "Assets/Scripts/AI/EnemyCompositionMemory.cs");
                Require(productionSource.Contains("context.Commands.TryTrain"),
                    "Production must route through FactionCommandService.");
                Require(productionSource.Contains("knowledge.VisibleEnemyUnits") == false
                    && productionSource.Contains("FindObjectsOfType") == false
                    && productionSource.Contains("FindObjectsByType") == false,
                    "Production has an unauthorized enemy scene-scan path.");
                Require(memorySource.Contains("LastMilitaryObservation"),
                    "FactionKnowledge is not the enemy composition source.");
                Require(productionSource.Contains("BuildingType.House") == false,
                    "Production policy must not duplicate House planning.");

                Debug.Log("Phase 7E.3 Reactive Production validation PASS: legal "
                    + "value-only enemy memory, confidence decay, authoritative matchup "
                    + "adapter, stable baseline/reactive configuration, queued-unit gap "
                    + "analysis, Monk support cap, infrastructure requests, command-service "
                    + "training and 7E.2 economic demand integration verified.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 7E.3 Reactive Production validation FAILED: "
                    + exception);
                throw;
            }
        }

        private static void RequireNoHiddenLiveReferences(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance
                | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                Type field = fields[i].FieldType;
                Type element = field.IsArray ? field.GetElementType() : null;
                Require(field != typeof(UnitController) && field != typeof(Transform)
                    && field != typeof(GameObject) && element != typeof(UnitController)
                    && element != typeof(Transform) && element != typeof(GameObject),
                    $"{type.Name}.{fields[i].Name} retains a hidden live object.");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
