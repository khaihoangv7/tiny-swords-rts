#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase6H4CombatSupportValidation
    {
        private const string DataRoot = "Assets/Data/";

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6H.4 Combat Support")]
        public static void Validate()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before running Phase 6H.4 validation.");
                return;
            }

            UnitCombatData monk = Load("MonkCombatData");
            UnitCombatData warrior = Load("WarriorCombatData");
            UnitCombatData archer = Load("ArcherCombatData");
            UnitCombatData lancer = Load("LancerCombatData");
            Require(Mathf.Approximately(MonkHealer.HealInterval, 2f)
                && Mathf.Approximately(MonkHealer.HealAmount, 6.25f)
                && Mathf.Approximately(monk.AttackInterval, 2f),
                "Monk must heal for 6.25 HP every 2 seconds.");
            Require(warrior.CommitsMeleeAttack
                && Mathf.Approximately(warrior.CommittedMeleeHitDelay, 0.2f)
                && Mathf.Approximately(warrior.CommittedMeleeDuration, 0.4f),
                "Warrior committed-attack timing is not configured.");
            Require(!archer.CommitsMeleeAttack && !lancer.CommitsMeleeAttack,
                "Warrior commitment must not alter Archer or Lancer attack delivery.");

            ValidateFractionalHealing(warrior);
            ValidateFleeingTarget(warrior);
            ValidateDeadTargetCancellation(warrior);
            ValidateDeadAttackerCancellation(warrior);

            Debug.Log("Phase 6H.4 Combat Support validation PASS: Monk 2-second/6.25-HP "
                + "healing preserves fractional value; Warrior starts only in range, hits "
                + "once at 0.2 seconds, holds its committed attack through 0.4 seconds, "
                + "then chases; dead target/attacker cancellation is safe; Archer and "
                + "Lancer commitment remain disabled.");
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6H.4 Combat Support", true)]
        private static bool CanValidate() => EditorApplication.isPlaying;

        private static void ValidateFractionalHealing(UnitCombatData warrior)
        {
            GameObject root = new("TemporaryPhase6H4Healing");
            try
            {
                UnitController target = CreateHealthOnly(root.transform, warrior);
                HealthComponent health = target.GetComponent<HealthComponent>();
                Require(health.TakeDamage(30), "Healing test target could not be injured.");
                int total = 0;
                int[] expected = { 6, 6, 6, 7 };
                for (int i = 0; i < expected.Length; i++)
                {
                    int restored = health.Heal(MonkHealer.HealAmount);
                    Require(restored == expected[i],
                        $"Fractional heal cycle {i + 1} restored {restored}, expected {expected[i]}.");
                    total += restored;
                }
                Require(total == 25,
                    "Four 6.25-HP heals must restore exactly 25 HP without truncation loss.");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private static void ValidateFleeingTarget(UnitCombatData warrior)
        {
            using var scenario = new CombatScenario(warrior);
            CombatController combat = scenario.Attacker.GetComponent<CombatController>();
            HealthComponent targetHealth = scenario.Target.GetComponent<HealthComponent>();
            int cycles = 0;
            combat.AttackPerformed += _ => cycles++;

            Require(combat.Attack(scenario.Target), "In-range Warrior attack was rejected.");
            Require(combat.IsMeleeAttackCommitted && cycles == 1
                && targetHealth.CurrentHP == warrior.MaxHP,
                "Warrior did not begin one delayed committed attack in range.");
            scenario.Target.transform.position = scenario.Point(8, 2);
            combat.TickCombat(0.19f);
            Require(combat.IsMeleeAttackCommitted
                && scenario.Attacker.CurrentState == UnitState.Attacking
                && targetHealth.CurrentHP == warrior.MaxHP,
                "Fleeing target cancelled the Warrior windup or received an early hit.");
            combat.TickCombat(0.02f);
            Require(combat.IsMeleeAttackCommitted && combat.HasAppliedCommittedMeleeHit
                && targetHealth.CurrentHP == warrior.MaxHP - warrior.BaseDamage,
                "Committed hit did not land exactly once at the configured hit time.");
            combat.TickCombat(0.19f);
            Require(!combat.IsMeleeAttackCommitted && combat.Target == scenario.Target
                && scenario.Attacker.CurrentState == UnitState.Chasing,
                "Warrior did not finish recovery and resume chasing the surviving target.");
            combat.TickCombat(0.2f);
            Require(cycles == 1
                && targetHealth.CurrentHP == warrior.MaxHP - warrior.BaseDamage,
                "One committed attack applied damage or presentation more than once.");
        }

        private static void ValidateDeadTargetCancellation(UnitCombatData warrior)
        {
            using var scenario = new CombatScenario(warrior);
            CombatController combat = scenario.Attacker.GetComponent<CombatController>();
            Require(combat.Attack(scenario.Target) && combat.IsMeleeAttackCommitted,
                "Dead-target cancellation setup failed.");
            scenario.Target.GetComponent<HealthComponent>().TakeDamage(warrior.MaxHP);
            combat.TickCombat(0.4f);
            Require(!combat.IsMeleeAttackCommitted && combat.Target == null,
                "Dead target left a pending Warrior committed hit.");
        }

        private static void ValidateDeadAttackerCancellation(UnitCombatData warrior)
        {
            using var scenario = new CombatScenario(warrior);
            CombatController combat = scenario.Attacker.GetComponent<CombatController>();
            HealthComponent targetHealth = scenario.Target.GetComponent<HealthComponent>();
            Require(combat.Attack(scenario.Target) && combat.IsMeleeAttackCommitted,
                "Dead-attacker cancellation setup failed.");
            scenario.Attacker.GetComponent<HealthComponent>().TakeDamage(warrior.MaxHP);
            combat.TickCombat(0.4f);
            Require(!combat.IsMeleeAttackCommitted
                && targetHealth.CurrentHP == warrior.MaxHP,
                "Dead Warrior delivered a pending committed hit.");
        }

        private static UnitController CreateHealthOnly(Transform parent, UnitCombatData data)
        {
            var obj = new GameObject("HealingTarget");
            obj.transform.SetParent(parent, false);
            UnitController controller = obj.AddComponent<UnitController>();
            controller.Configure(Faction.Blue);
            obj.AddComponent<HealthComponent>().Configure(data);
            return controller;
        }

        private static UnitCombatData Load(string name)
        {
            UnitCombatData data = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                DataRoot + name + ".asset");
            Require(data != null, $"Missing {name}.asset.");
            return data;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class CombatScenario : IDisposable
        {
            private readonly NavigationGridData data;
            private readonly GameObject root;
            private readonly GridManager grid;
            private readonly PathfindingSystem pathfinding;

            public UnitController Attacker { get; }
            public UnitController Target { get; }

            public CombatScenario(UnitCombatData warrior)
            {
                data = ScriptableObject.CreateInstance<NavigationGridData>();
                var cells = new List<NavigationCellData>();
                for (int y = 0; y < 6; y++)
                for (int x = 0; x < 12; x++)
                    cells.Add(new NavigationCellData(x, y, TerrainType.FlatGround, 0));
                data.Configure(12, 6, new Vector2(1000f, 1000f), 1f, cells);
                root = new GameObject("TemporaryPhase6H4Combat");
                root.SetActive(false);
                grid = root.AddComponent<GridManager>();
                grid.SetNavigationData(data);
                pathfinding = root.AddComponent<PathfindingSystem>();
                pathfinding.SetGridManager(grid);
                Attacker = Create("Warrior", Faction.Goblin, warrior, Point(2, 2));
                Target = Create("Target", Faction.Blue, warrior,
                    Point(2, 2) + Vector3.right * 0.9f);
                root.SetActive(true);
            }

            public Vector3 Point(int x, int y) => grid.GridToWorld(x, y);

            private UnitController Create(string name, Faction faction,
                UnitCombatData combatData, Vector3 position)
            {
                var obj = new GameObject(name);
                obj.SetActive(false);
                obj.transform.SetParent(root.transform);
                obj.transform.position = position;
                UnitController controller = obj.AddComponent<UnitController>();
                controller.Configure(faction);
                obj.AddComponent<HealthComponent>().Configure(combatData);
                obj.AddComponent<UnitMovement>().Configure(grid, pathfinding);
                obj.AddComponent<CombatController>().Configure(combatData);
                obj.SetActive(true);
                return controller;
            }

            public void Dispose()
            {
                root.SetActive(false);
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(data);
            }
        }
    }
}
#endif
