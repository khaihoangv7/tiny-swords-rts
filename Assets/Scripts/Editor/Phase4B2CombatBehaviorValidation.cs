using System;
using System.Collections.Generic;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase4B2CombatBehaviorValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 4B2 Combat Behavior")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before running Phase 4B2 combat validation.");
                return;
            }

            UnitCombatData warrior = Load("Warrior");
            UnitCombatData archer = Load("Archer");
            UnitCombatData lancer = Load("Lancer");
            Require(warrior.AcquisitionRadius == 4f && archer.AcquisitionRadius == 5.5f
                && lancer.AcquisitionRadius == 4f && warrior.AcquisitionInterval > 0f
                && warrior.AutoChaseLimit > warrior.AcquisitionRadius,
                "Phase 4B2 acquisition configuration is missing.");

            ValidateEmptyAttackMove(warrior);
            ValidateTwoTargetsAndResume(warrior, archer);
            ValidateIdleBoundary(warrior, archer);
            ValidateIdleAndMovePriority(warrior, archer);
            ValidateReachabilityAndTieBreak(warrior, archer);
            ValidateLostRoute(warrior, archer);
            ValidateInvalidTarget(warrior, archer);
            ValidateGuardAndSlots(warrior, archer);
            ValidateArcherAttackMove(archer, lancer);
            ValidateLeash(warrior, archer);
            ValidatePlayerCommandApi(warrior);
            Debug.Log("Phase 4B2 combat behavior validation passed: empty and interrupted AttackMove, sequential targets, Idle aggro, normal Move, explicit priority, reachability, deterministic tie, Guard, melee slots, Archer windup/projectile, chase leashes and player command API. Visually check A + left-click in Game View.");
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 4B2 Combat Behavior", true)]
        private static bool CanRun() => EditorApplication.isPlaying;

        private static void ValidateEmptyAttackMove(UnitCombatData warrior)
        {
            using var scene = new Scenario();
            UnitController unit = scene.Create("Empty route", Faction.Blue, warrior, 2, 2);
            CombatController combat = unit.GetComponent<CombatController>();
            Require(combat.AttackMove(new Vector2Int(8, 2)), "Empty AttackMove rejected a valid destination.");
            for (int i = 0; i < 40; i++) scene.Step(unit, 0.1f);
            Require(!combat.HasAttackMoveOrder && !unit.GetComponent<UnitMovement>().IsMoving
                && unit.CurrentState == UnitState.Idle
                && Vector2.Distance(unit.transform.position, scene.Point(8, 2)) < 0.05f,
                "Empty AttackMove did not finish at its destination.");
        }

        private static void ValidateTwoTargetsAndResume(UnitCombatData warrior,
            UnitCombatData archer)
        {
            using var scene = new Scenario();
            UnitController unit = scene.Create("Attack mover", Faction.Blue, warrior, 2, 2);
            UnitController first = scene.Create("First enemy", Faction.Red, archer, 6, 2);
            UnitController second = scene.Create("Second enemy", Faction.Red, archer, 10, 2);
            CombatController combat = unit.GetComponent<CombatController>();
            Require(combat.AttackMove(new Vector2Int(15, 2)), "AttackMove rejected its route.");
            for (int i = 0; i < 450 && (combat.HasAttackMoveOrder || combat.Target != null); i++)
                scene.Step(unit, 0.1f);
            Require(!first.IsAlive && !second.IsAlive && combat.Target == null
                && !combat.HasAttackMoveOrder && unit.CurrentState == UnitState.Idle
                && Vector2.Distance(unit.transform.position, scene.Point(15, 2)) < 0.05f,
                "AttackMove did not defeat sequential enemies and resume its original destination.");
        }

        private static void ValidateIdleAndMovePriority(UnitCombatData warrior,
            UnitCombatData archer)
        {
            using var scene = new Scenario();
            UnitController unit = scene.Create("Idle defender", Faction.Blue, warrior, 2, 3);
            UnitController near = scene.Create("Near enemy", Faction.Red, archer, 4, 3);
            UnitController far = scene.Create("Far enemy", Faction.Red, archer, 16, 3);
            CombatController combat = unit.GetComponent<CombatController>();
            combat.TickCombat(0.25f);
            Require(combat.Target == near, "Idle unit did not acquire its nearby enemy.");
            Require(combat.Attack(far) && combat.Target == far,
                "Explicit Attack did not override an automatically acquired target.");
            Require(unit.GetComponent<UnitMovement>().TryIssueMove(new Vector2Int(12, 3))
                && combat.Target == null, "Normal Move did not override combat.");
            for (int i = 0; i < 40 && unit.GetComponent<UnitMovement>().IsMoving; i++)
            {
                scene.Step(unit, 0.1f);
                Require(combat.Target == null, "Normal Move acquired an unrelated enemy en route.");
            }
            Require(Vector2.Distance(unit.transform.position, scene.Point(12, 3)) < 0.05f,
                "Normal Move failed to reach its destination.");
        }

        private static void ValidateIdleBoundary(UnitCombatData warrior,
            UnitCombatData archer)
        {
            using var scene = new Scenario();
            UnitController unit = scene.Create("Boundary defender", Faction.Blue, warrior, 2, 2);
            UnitController enemy = scene.Create("Entering enemy", Faction.Red, archer, 9, 2);
            CombatController combat = unit.GetComponent<CombatController>();
            combat.TickCombat(0.25f);
            Require(combat.Target == null && unit.CurrentState == UnitState.Idle,
                "Idle unit acquired an enemy outside its aggro radius.");
            enemy.transform.position = scene.Point(5, 2);
            combat.TickCombat(0.25f);
            Require(combat.Target == enemy,
                "Idle unit did not acquire an enemy entering its aggro radius.");
            enemy.GetComponent<HealthComponent>().TakeDamage(archer.MaxHP);
            Require(combat.Target == null && unit.CurrentState == UnitState.Idle,
                "Idle unit remained stuck on a dead target.");
        }

        private static void ValidateReachabilityAndTieBreak(UnitCombatData warrior,
            UnitCombatData archer)
        {
            using (var scene = new Scenario())
            {
                UnitController unit = scene.Create("Tie breaker", Faction.Blue, warrior, 2, 2);
                scene.Create("Right enemy", Faction.Red, archer, 4, 2);
                UnitController upper = scene.Create("Upper enemy", Faction.Red, archer, 2, 4);
                unit.GetComponent<CombatController>().TickCombat(0.25f);
                Require(unit.GetComponent<CombatController>().Target == upper,
                    "Equal-distance target selection did not use the stable coordinate tie-break.");
            }
            using (var scene = new Scenario(true))
            {
                UnitController unit = scene.Create("Blocked defender", Faction.Blue, warrior, 2, 2);
                scene.Create("Unreachable enemy", Faction.Red, archer, 5, 2);
                unit.GetComponent<CombatController>().TickCombat(0.25f);
                Require(unit.GetComponent<CombatController>().Target == null,
                    "Auto acquisition crossed an unreachable Water barrier.");
            }
        }

        private static void ValidateGuardAndSlots(UnitCombatData warrior,
            UnitCombatData archer)
        {
            using var scene = new Scenario();
            UnitController first = scene.Create("First Warrior", Faction.Blue, warrior, 2, 5);
            UnitController second = scene.Create("Second Warrior", Faction.Blue, warrior, 2, 6);
            UnitController enemy = scene.Create("Shared enemy", Faction.Red, archer, 5, 5);
            CombatController a = first.GetComponent<CombatController>();
            CombatController b = second.GetComponent<CombatController>();
            a.TickCombat(0.25f);
            b.TickCombat(0.25f);
            int firstSlot = MeleeEngagementSlots.SlotIndex(first, enemy);
            int secondSlot = MeleeEngagementSlots.SlotIndex(second, enemy);
            Require(a.Target == enemy && b.Target == enemy && firstSlot >= 0
                && secondSlot >= 0 && firstSlot != secondSlot,
                "Auto-acquired melee units did not reserve distinct existing engagement slots.");
            Require(first.GetComponent<UnitGuard>().BeginGuard(), "Guard rejected a living Warrior.");
            a.TickCombat(1f);
            Require(a.Target == null && first.CurrentState == UnitState.Guarding
                && MeleeEngagementSlots.SlotIndex(first, enemy) < 0,
                "Guard did not cancel auto combat and release the engagement slot.");
            Require(a.AttackMove(new Vector2Int(12, 5))
                && first.CurrentState != UnitState.Guarding && a.HasAttackMoveOrder,
                "AttackMove did not exit Guard and take command priority.");
        }

        private static void ValidateLostRoute(UnitCombatData warrior,
            UnitCombatData archer)
        {
            using var scene = new Scenario();
            UnitController unit = scene.Create("Route defender", Faction.Blue, warrior, 2, 2);
            scene.Create("Route target", Faction.Red, archer, 6, 2);
            CombatController combat = unit.GetComponent<CombatController>();
            combat.TickCombat(0f);
            Require(combat.Target != null, "Route target was not initially acquired.");
            for (int y = 0; y < 20; y++) scene.Grid.SetOccupied(4, y, true);
            combat.TickCombat(0.25f);
            Require(combat.Target == null,
                "Auto-acquired target remained locked after its route became unreachable.");
        }

        private static void ValidateInvalidTarget(UnitCombatData warrior,
            UnitCombatData archer)
        {
            using var scene = new Scenario();
            UnitController unit = scene.Create("Invalid-target mover", Faction.Blue, warrior, 2, 2);
            UnitController enemy = scene.Create("Disabled target", Faction.Red, archer, 5, 2);
            CombatController combat = unit.GetComponent<CombatController>();
            Require(combat.AttackMove(new Vector2Int(12, 2)), "Invalid-target route was rejected.");
            combat.TickCombat(0f);
            Require(combat.Target == enemy, "AttackMove did not initially acquire target.");
            enemy.gameObject.SetActive(false);
            combat.TickCombat(0.25f);
            Require(combat.Target == null && combat.HasAttackMoveOrder
                && unit.GetComponent<UnitMovement>().IsMoving,
                "AttackMove did not resume after its target became inactive.");
        }

        private static void ValidateArcherAttackMove(UnitCombatData archer,
            UnitCombatData lancer)
        {
            using var scene = new Scenario();
            UnitController unit = scene.Create("Ranged attack mover", Faction.Blue, archer, 2, 8);
            UnitController enemy = scene.Create("Ranged target", Faction.Red, lancer, 6, 8);
            CombatController combat = unit.GetComponent<CombatController>();
            Require(combat.AttackMove(new Vector2Int(13, 8)), "Archer AttackMove was rejected.");
            combat.TickCombat(0f);
            Require(combat.Target == enemy && unit.CurrentState == UnitState.Attacking
                && combat.LastProjectile == null,
                "Archer did not stop in range and begin a delayed Shoot cycle.");
            combat.TickCombat(0.69f);
            Require(combat.LastProjectile == null, "Archer AttackMove released early.");
            combat.TickCombat(0.02f);
            CombatProjectile arrow = combat.LastProjectile;
            Require(arrow != null, "Archer AttackMove failed to release its projectile.");
            arrow.TickProjectile(1f);
            Require(enemy.GetComponent<HealthComponent>().CurrentHP == 112,
                "Archer AttackMove projectile did not apply the existing counter damage.");
            enemy.GetComponent<HealthComponent>().TakeDamage(lancer.MaxHP);
            Require(combat.Target == null && combat.HasAttackMoveOrder
                && unit.GetComponent<UnitMovement>().IsMoving,
                "Archer did not resume AttackMove after its target died.");
        }

        private static void ValidateLeash(UnitCombatData warrior, UnitCombatData archer)
        {
            using (var scene = new Scenario())
            {
                UnitController unit = scene.Create("Idle leashed", Faction.Blue, warrior, 2, 12);
                UnitController enemy = scene.Create("Fleeing enemy", Faction.Red, archer, 5, 12);
                CombatController combat = unit.GetComponent<CombatController>();
                combat.TickCombat(0f);
                scene.Step(unit, 0.5f);
                enemy.transform.position = scene.Point(13, 12);
                combat.TickCombat(0.25f);
                Require(combat.Target == null, "Idle defender chased outside its leash.");
                for (int i = 0; i < 30; i++) scene.Step(unit, 0.1f);
                Require(Vector2.Distance(unit.transform.position, scene.Point(2, 12)) < 0.05f
                    && unit.CurrentState == UnitState.Idle,
                    "Idle defender did not return to its acquisition position.");
            }
            using (var scene = new Scenario())
            {
                UnitController unit = scene.Create("Route leashed", Faction.Blue, warrior, 2, 14);
                UnitController enemy = scene.Create("Route fleeing enemy", Faction.Red, archer, 5, 14);
                CombatController combat = unit.GetComponent<CombatController>();
                Require(combat.AttackMove(new Vector2Int(15, 14)), "Leash route was rejected.");
                scene.Step(unit, 0.5f);
                enemy.transform.position = scene.Point(13, 19);
                combat.TickCombat(0.25f);
                Require(combat.Target == null && combat.HasAttackMoveOrder,
                    "AttackMove did not resume after target escaped its leash.");
            }
            using (var scene = new Scenario())
            {
                UnitController unit = scene.Create("Explicit pursuer", Faction.Blue, warrior, 2, 12);
                UnitController enemy = scene.Create("Explicit target", Faction.Red, archer, 5, 12);
                CombatController combat = unit.GetComponent<CombatController>();
                Require(combat.Attack(enemy), "Explicit Attack was rejected.");
                enemy.transform.position = scene.Point(13, 12);
                combat.TickCombat(0.25f);
                Require(combat.Target == enemy,
                    "Automatic chase leash incorrectly cancelled explicit Attack.");
            }
        }

        private static void ValidatePlayerCommandApi(UnitCombatData warrior)
        {
            using var scene = new Scenario();
            UnitController first = scene.Create("Selected first", Faction.Blue, warrior, 2, 2);
            UnitController second = scene.Create("Selected second", Faction.Blue, warrior, 2, 3);
            first.gameObject.AddComponent<UnitSelection>();
            second.gameObject.AddComponent<UnitSelection>();
            SelectionManager selection = scene.Root.AddComponent<SelectionManager>();
            selection.Configure(null, Faction.Blue);
            selection.ApplyBoxSelection(new[] {
                first.GetComponent<UnitSelection>(), second.GetComponent<UnitSelection>() }, false);
            PlayerCommandController commands = scene.Root.AddComponent<PlayerCommandController>();
            commands.Configure(null, selection, scene.Grid);
            Require(commands.IssueAttackMoveCommand(scene.Point(12, 2)) == 2
                && first.GetComponent<CombatController>().HasAttackMoveOrder
                && second.GetComponent<CombatController>().HasAttackMoveOrder,
                "Player AttackMove API did not issue orders to both selected combat units.");
        }

        private static UnitCombatData Load(string name)
        {
            UnitCombatData data = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                $"Assets/Data/{name}CombatData.asset");
            Require(data != null, $"Missing {name} combat data.");
            return data;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Scenario : IDisposable
        {
            private readonly NavigationGridData data;
            public readonly GameObject Root;
            public readonly GridManager Grid;
            private readonly PathfindingSystem pathfinding;

            public Scenario(bool waterBarrier = false)
            {
                data = ScriptableObject.CreateInstance<NavigationGridData>();
                var cells = new List<NavigationCellData>();
                for (int y = 0; y < 20; y++)
                for (int x = 0; x < 20; x++)
                    cells.Add(new NavigationCellData(x, y,
                        waterBarrier && x == 4 ? TerrainType.Water : TerrainType.FlatGround, 0));
                data.Configure(20, 20, new Vector2(1000f, 1000f), 1f, cells);
                Root = new GameObject("TemporaryPhase4B2Validation");
                Root.SetActive(false);
                Grid = Root.AddComponent<GridManager>();
                Grid.SetNavigationData(data);
                pathfinding = Root.AddComponent<PathfindingSystem>();
                pathfinding.SetGridManager(Grid);
                Root.SetActive(true);
            }

            public Vector3 Point(int x, int y) => Grid.GridToWorld(x, y);

            public UnitController Create(string name, Faction faction, UnitCombatData combatData,
                int x, int y)
            {
                var obj = new GameObject(name);
                obj.SetActive(false);
                obj.transform.SetParent(Root.transform);
                obj.transform.position = Point(x, y);
                UnitController controller = obj.AddComponent<UnitController>();
                controller.Configure(faction);
                obj.AddComponent<HealthComponent>().Configure(combatData);
                obj.AddComponent<UnitMovement>().Configure(Grid, pathfinding);
                obj.AddComponent<CombatController>().Configure(combatData);
                if (combatData.UnitType == UnitType.Warrior) obj.AddComponent<UnitGuard>();
                obj.SetActive(true);
                return controller;
            }

            public void Step(UnitController unit, float deltaTime)
            {
                unit.GetComponent<CombatController>().TickCombat(deltaTime);
                unit.GetComponent<UnitMovement>().TickMovement(deltaTime);
            }

            public void Dispose()
            {
                Root.SetActive(false);
                UnityEngine.Object.Destroy(Root);
                UnityEngine.Object.Destroy(data);
            }
        }
    }
}
