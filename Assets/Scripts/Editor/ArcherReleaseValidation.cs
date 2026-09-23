using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class ArcherReleaseValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Archer Release")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before running Validate Archer Release.");
                return;
            }

            var data = ScriptableObject.CreateInstance<NavigationGridData>();
            var root = new GameObject("TemporaryArcherReleaseValidation");
            root.SetActive(false);
            try
            {
                var cells = new List<NavigationCellData>();
                for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                    cells.Add(new NavigationCellData(x, y, TerrainType.FlatGround, 0));
                data.Configure(10, 10, Vector2.zero, 1f, cells);
                GridManager grid = root.AddComponent<GridManager>();
                grid.SetNavigationData(data);
                PathfindingSystem pathfinding = root.AddComponent<PathfindingSystem>();
                pathfinding.SetGridManager(grid);
                UnitCombatData archerData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    "Assets/Data/ArcherCombatData.asset");
                UnitCombatData warriorData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    "Assets/Data/WarriorCombatData.asset");
                UnitCombatData lancerData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    "Assets/Data/LancerCombatData.asset");
                Require(archerData != null && warriorData != null && lancerData != null
                    && archerData.AttackDelivery == AttackDelivery.Projectile
                    && Mathf.Approximately(archerData.AttackInterval, 1.2f)
                    && Mathf.Approximately(archerData.RangedReleaseDelay, 0.7f),
                    "Archer gameplay timing data is incorrect.");

                UnitController archer = Create(root.transform, "Archer", Faction.Blue,
                    archerData, new Vector3(2.5f, 4.5f), grid, pathfinding);
                UnitController warrior = Create(root.transform, "Warrior", Faction.Red,
                    warriorData, new Vector3(5.5f, 4.5f), grid, pathfinding);
                root.SetActive(true);
                CombatController combat = archer.GetComponent<CombatController>();
                HealthComponent warriorHealth = warrior.GetComponent<HealthComponent>();
                int cycles = 0;
                combat.AttackPerformed += _ => cycles++;

                Require(combat.Attack(warrior) && cycles == 1 && combat.LastProjectile == null,
                    "Archer released a projectile when the Shoot cycle began.");
                combat.TickCombat(0.69f);
                Require(combat.LastProjectile == null && warriorHealth.CurrentHP == 260,
                    "Projectile appeared before the configured release point.");
                warrior.transform.position += Vector3.right;
                combat.TickCombat(0.02f);
                CombatProjectile first = combat.LastProjectile;
                Require(first != null && warriorHealth.CurrentHP == 260,
                    "A moving valid target prevented release or was damaged before impact.");
                Vector3 launch = first.transform.position;
                Require(Mathf.Abs(launch.x - (archer.transform.position.x + 0.4f)) < 0.01f
                    && Mathf.Abs(launch.y - (archer.transform.position.y + 0.1f)) < 0.01f,
                    "Arrow did not start at the Archer's drawn bow.");
                float halfFlight = Vector2.Distance(launch, warrior.transform.position)
                    / archerData.ProjectileSpeed * 0.5f;
                first.TickProjectile(halfFlight);
                float straightMidpointY = (launch.y + warrior.transform.position.y) * 0.5f;
                Require(!first.IsResolved && first.transform.position.y > straightMidpointY + 0.5f
                    && warriorHealth.CurrentHP == 260,
                    "Arrow did not climb on a visible arc before impact.");
                warrior.transform.position += Vector3.up * 0.25f;
                first.TickProjectile(1f);
                Require(first.IsResolved
                    && Vector2.Distance(first.transform.position, warrior.transform.position) < 0.01f
                    && warriorHealth.CurrentHP == 252,
                    "Arrow did not land on the moving target for exactly 8 damage.");
                warrior.transform.position -= Vector3.right + Vector3.up * 0.25f;

                combat.TickCombat(0.48f);
                Require(cycles == 1 && combat.LastProjectile == first,
                    "The next attack cycle began before the 1.2-second interval.");
                combat.TickCombat(0.02f);
                Require(cycles == 2 && combat.LastProjectile == first,
                    "The next cycle did not begin at 1.2 seconds or released immediately.");
                combat.TickCombat(0.71f);
                CombatProjectile second = combat.LastProjectile;
                Require(second != null && second != first,
                    "Second cycle did not release after its windup.");
                Require(warrior.GetComponent<UnitGuard>().BeginGuard(),
                    "Warrior did not enter Guard after Arrow release.");
                second.TickProjectile(1f);
                Require(warriorHealth.CurrentHP == 248,
                    "Guard was not applied at impact (expected 4 damage).");
                warrior.GetComponent<UnitGuard>().EndGuard();

                combat.StopCombat();
                combat.TickCombat(1.2f);
                Require(combat.Attack(warrior) && cycles == 3, "Cancellation setup failed.");
                combat.StopCombat();
                combat.TickCombat(1f);
                Require(combat.LastProjectile == second, "Cancelled attack spawned a ghost Arrow.");

                combat.TickCombat(0.25f);
                Require(combat.Attack(warrior) && cycles == 4, "Move cancellation setup failed.");
                Require(archer.GetComponent<UnitMovement>().TryIssueMove(new Vector2Int(2, 2)),
                    "Move command failed during windup.");
                combat.TickCombat(1f);
                Require(combat.LastProjectile == second && combat.Target == null,
                    "Move command did not cancel the pending Arrow.");

                UnitController doomedArcher = Create(root.transform, "Doomed Archer", Faction.Blue,
                    archerData, new Vector3(2.5f, 5.5f), grid, pathfinding);
                CombatController doomedCombat = doomedArcher.GetComponent<CombatController>();
                Require(doomedCombat.Attack(warrior) && doomedCombat.LastProjectile == null,
                    "Death cancellation setup failed.");
                doomedArcher.GetComponent<HealthComponent>().TakeDamage(archerData.MaxHP);
                doomedCombat.TickCombat(1f);
                Require(doomedCombat.LastProjectile == null,
                    "Dead Archer spawned a delayed Arrow.");

                UnitController doomedTarget = Create(root.transform, "Doomed Target", Faction.Red,
                    lancerData, new Vector3(5.5f, 5.5f), grid, pathfinding);
                UnitController otherArcher = Create(root.transform, "Other Archer", Faction.Blue,
                    archerData, new Vector3(2.5f, 6.5f), grid, pathfinding);
                CombatController otherCombat = otherArcher.GetComponent<CombatController>();
                Require(otherCombat.Attack(doomedTarget) && otherCombat.LastProjectile == null,
                    "Faction cancellation setup failed.");
                doomedTarget.Configure(Faction.Blue);
                otherCombat.TickCombat(0.85f);
                Require(otherCombat.LastProjectile == null && otherCombat.Target == null,
                    "A friendly target received a delayed Arrow.");
                doomedTarget.Configure(Faction.Red);
                otherCombat.TickCombat(1.2f);
                Require(otherCombat.Attack(doomedTarget) && otherCombat.LastProjectile == null,
                    "Target death setup failed.");
                doomedTarget.GetComponent<HealthComponent>().TakeDamage(lancerData.MaxHP);
                otherCombat.TickCombat(1f);
                Require(otherCombat.LastProjectile == null && otherCombat.Target == null,
                    "Dead target left a pending Arrow.");

                UnitController leftArcher = Create(root.transform, "Left Archer", Faction.Blue,
                    archerData, new Vector3(6.5f, 7.5f), grid, pathfinding);
                UnitController leftTarget = Create(root.transform, "Left Target", Faction.Red,
                    lancerData, new Vector3(4.5f, 7.5f), grid, pathfinding);
                CombatController leftCombat = leftArcher.GetComponent<CombatController>();
                Require(leftCombat.Attack(leftTarget), "Left-facing Archer could not attack.");
                leftCombat.TickCombat(0.71f);
                CombatProjectile leftArrow = leftCombat.LastProjectile;
                Require(leftArrow != null
                    && Mathf.Abs(leftArrow.transform.position.x - 6.1f) < 0.01f
                    && Mathf.Abs(leftArrow.transform.position.y - 7.6f) < 0.01f,
                    "Left-facing Arrow did not start at the mirrored bow offset.");
                leftArrow.TickProjectile(1f);
                Require(leftArrow.IsResolved
                    && leftTarget.GetComponent<HealthComponent>().CurrentHP == 112,
                    "Left-facing Arrow did not land for exactly 13 damage.");

                Debug.Log("Archer release validation passed: 0.7-second gameplay windup, mirrored bow launch offset, arcing flight, moving-target landing, 1.2-second start-to-start interval, 8/4/13 damage, impact-time Guard, cancelled/Move/death/faction shots, and gameplay without UnitAnimator. Check visual alignment in Game View.");
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                UnityEngine.Object.Destroy(data);
            }
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Archer Release", true)]
        private static bool CanRun() => EditorApplication.isPlaying;

        private static UnitController Create(Transform parent, string name, Faction faction,
            UnitCombatData data, Vector3 position, GridManager grid, PathfindingSystem pathfinding)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            UnitController controller = obj.AddComponent<UnitController>();
            controller.Configure(faction);
            obj.AddComponent<HealthComponent>().Configure(data);
            obj.AddComponent<UnitMovement>().Configure(grid, pathfinding);
            obj.AddComponent<CombatController>().Configure(data);
            if (data.UnitType == UnitType.Warrior) obj.AddComponent<UnitGuard>();
            return controller;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
