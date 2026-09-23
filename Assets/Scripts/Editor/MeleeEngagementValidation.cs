using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class MeleeEngagementValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Melee Engagement")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before running Validate Melee Engagement.");
                return;
            }
            var data = ScriptableObject.CreateInstance<NavigationGridData>();
            var root = new GameObject("TemporaryMeleeEngagementValidation");
            root.SetActive(false);
            UnitController target = null;
            try
            {
                Configure(data, null);
                GridManager grid = root.AddComponent<GridManager>();
                grid.SetNavigationData(data);
                PathfindingSystem pathfinding = root.AddComponent<PathfindingSystem>();
                pathfinding.SetGridManager(grid);
                UnitCombatData warriorData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    "Assets/Data/WarriorCombatData.asset");
                UnitCombatData lancerData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    "Assets/Data/LancerCombatData.asset");
                UnitCombatData archerData = AssetDatabase.LoadAssetAtPath<UnitCombatData>(
                    "Assets/Data/ArcherCombatData.asset");
                Require(warriorData != null && lancerData != null && archerData != null,
                    "Unit combat data missing.");

                target = Create(root.transform, "Target", Faction.Red, warriorData,
                    new Vector3(4.5f, 4.5f), grid, pathfinding);
                UnitController first = Create(root.transform, "First Warrior", Faction.Blue,
                    warriorData, new Vector3(2.5f, 4.5f), grid, pathfinding);
                UnitController second = Create(root.transform, "Second Warrior", Faction.Blue,
                    warriorData, new Vector3(2.5f, 4.5f), grid, pathfinding);
                UnitController third = Create(root.transform, "Third Warrior", Faction.Blue,
                    warriorData, new Vector3(2.5f, 4.5f), grid, pathfinding);
                UnitController lancer = Create(root.transform, "Lancer", Faction.Blue,
                    lancerData, new Vector3(2.5f, 5.5f), grid, pathfinding);
                UnitController archer = Create(root.transform, "Archer", Faction.Blue,
                    archerData, new Vector3(1.5f, 1.5f), grid, pathfinding);
                root.SetActive(true);

                bool reservedA = MeleeEngagementSlots.TryReserve(first, target, 1f, grid, pathfinding, out var a);
                bool reservedB = MeleeEngagementSlots.TryReserve(second, target, 1f, grid, pathfinding, out var b);
                bool reservedC = MeleeEngagementSlots.TryReserve(third, target, 1f, grid, pathfinding, out var c);
                Require(reservedA && reservedB && reservedC,
                    "Three Warriors could not reserve valid slots.");
                int firstIndex = MeleeEngagementSlots.SlotIndex(first, target);
                Require(firstIndex != MeleeEngagementSlots.SlotIndex(second, target)
                    && firstIndex != MeleeEngagementSlots.SlotIndex(third, target)
                    && MeleeEngagementSlots.SlotIndex(second, target)
                        != MeleeEngagementSlots.SlotIndex(third, target)
                    && a != target.transform.position && b != target.transform.position
                    && c != target.transform.position,
                    "Warriors from the same side selected identical directions or the target center.");
                target.transform.position += Vector3.up;
                Require(MeleeEngagementSlots.TryGet(first, target, 1f, grid, pathfinding,
                        out Vector3 shifted)
                    && MeleeEngagementSlots.SlotIndex(first, target) == firstIndex
                    && Vector3.Distance(shifted, a + Vector3.up) < 0.001f,
                    "A moving target changed the reservation angle.");
                target.transform.position -= Vector3.up;
                MeleeEngagementSlots.Release(first);
                MeleeEngagementSlots.Release(second);
                MeleeEngagementSlots.Release(third);

                var crowd = new List<UnitController> { first, second, third, lancer };
                for (int i = 0; i < 5; i++)
                    crowd.Add(Create(root.transform, $"Extra Warrior {i}", Faction.Blue,
                        warriorData, new Vector3(2.5f, 4.5f), grid, pathfinding));
                for (int i = 0; i < MeleeEngagementSlots.SlotCount; i++)
                    Require(MeleeEngagementSlots.TryReserve(crowd[i], target,
                            crowd[i] == lancer ? 1.5f : 1f, grid, pathfinding, out _),
                        "A valid free engagement slot could not be reserved.");
                int stable = MeleeEngagementSlots.SlotIndex(first, target);
                Require(!MeleeEngagementSlots.TryReserve(crowd[8], target, 1f,
                        grid, pathfinding, out _)
                    && MeleeEngagementSlots.SlotIndex(first, target) == stable,
                    "An extra attacker stole an occupied slot.");
                foreach (UnitController unit in crowd) MeleeEngagementSlots.Release(unit);

                foreach (TerrainType obstacle in new[] { TerrainType.Water, TerrainType.Cliff })
                {
                    Configure(data, obstacle);
                    grid.SetNavigationData(data);
                    first.transform.position = new Vector3(6.5f, 4.5f);
                    Require(MeleeEngagementSlots.TryReserve(first, target, 1f,
                            grid, pathfinding, out Vector3 alternate)
                        && MeleeEngagementSlots.SlotIndex(first, target) != 0
                        && grid.GetCell(grid.WorldToGrid(alternate).x,
                            grid.WorldToGrid(alternate).y).Walkable,
                        $"No alternate slot was found beside {obstacle}.");
                    MeleeEngagementSlots.Release(first);
                }
                Configure(data, null);
                grid.SetNavigationData(data);
                grid.SetOccupied(5, 4, true);
                Require(MeleeEngagementSlots.TryReserve(first, target, 1f,
                        grid, pathfinding, out _)
                    && MeleeEngagementSlots.SlotIndex(first, target) != 0,
                    "A permanent occupied cell was accepted as an engagement slot.");
                MeleeEngagementSlots.Release(first);
                Configure(data, TerrainType.ElevatedGround, true);
                grid.SetNavigationData(data);
                Require(MeleeEngagementSlots.TryReserve(first, target, 1f,
                        grid, pathfinding, out _)
                    && MeleeEngagementSlots.SlotIndex(first, target) != 0,
                    "A stairless elevated cell was accepted as an engagement slot.");
                MeleeEngagementSlots.Release(first);
                Configure(data, null);
                grid.SetNavigationData(data);
                first.transform.position = new Vector3(2.5f, 4.5f);

                Require(first.GetComponent<CombatController>().Attack(target)
                    && MeleeEngagementSlots.SlotIndex(first, target) >= 0,
                    "Attack did not reserve a melee slot.");
                int held = MeleeEngagementSlots.SlotIndex(first, target);
                Require(first.GetComponent<CombatController>().Attack(target)
                    && MeleeEngagementSlots.SlotIndex(first, target) == held,
                    "Reissuing Attack changed a valid reservation.");
                Require(first.GetComponent<UnitMovement>().TryIssueMove(new Vector2Int(2, 2))
                    && MeleeEngagementSlots.SlotIndex(first, target) < 0,
                    "Move command did not release the reservation.");
                Require(first.GetComponent<CombatController>().Attack(target)
                    && first.GetComponent<UnitGuard>().BeginGuard()
                    && first.CurrentState == UnitState.Guarding
                    && MeleeEngagementSlots.SlotIndex(first, target) < 0,
                    "Guard did not cancel combat and release the reservation.");
                first.GetComponent<UnitGuard>().EndGuard();

                Require(MeleeEngagementSlots.TryReserve(first, target, 1f, grid, pathfinding, out _)
                    && MeleeEngagementSlots.TryReserve(lancer, target, 1.5f,
                        grid, pathfinding, out Vector3 lancerPosition)
                    && MeleeEngagementSlots.SlotIndex(first, target)
                        != MeleeEngagementSlots.SlotIndex(lancer, target)
                    && Mathf.Approximately(Vector2.Distance(lancerPosition, target.transform.position),
                        MeleeEngagementSlots.Distance(1.5f)),
                    "Warrior and Lancer did not use distinct angles and their own ranges.");
                MeleeEngagementSlots.Release(first);
                MeleeEngagementSlots.Release(lancer);

                Require(first.GetComponent<CombatController>().Attack(target),
                    "Attacker-death setup failed.");
                int vacated = MeleeEngagementSlots.SlotIndex(first, target);
                first.GetComponent<HealthComponent>().TakeDamage(first.GetComponent<HealthComponent>().MaxHP);
                Require(vacated >= 0 && MeleeEngagementSlots.SlotIndex(first, target) < 0
                    && first.GetComponent<CombatController>().Target == null
                    && MeleeEngagementSlots.TryReserve(second, target, 1f,
                        grid, pathfinding, out _)
                    && MeleeEngagementSlots.SlotIndex(second, target) == vacated,
                    "Attacker death did not free its slot.");
                MeleeEngagementSlots.Release(second);

                Require(archer.GetComponent<CombatController>().Attack(target)
                    && MeleeEngagementSlots.SlotIndex(archer, target) < 0,
                    "Archer received a melee reservation.");
                archer.GetComponent<CombatController>().StopCombat();

                Require(second.GetComponent<CombatController>().Attack(target)
                    && third.GetComponent<CombatController>().Attack(target),
                    "Target-death setup failed.");
                target.GetComponent<HealthComponent>().TakeDamage(target.GetComponent<HealthComponent>().MaxHP);
                Require(MeleeEngagementSlots.SlotIndex(second, target) < 0
                    && MeleeEngagementSlots.SlotIndex(third, target) < 0
                    && second.GetComponent<CombatController>().Target == null
                    && third.GetComponent<CombatController>().Target == null,
                    "Target death left reservations or active combat.");

                Debug.Log("Melee engagement validation passed: distinct angles, same-side approach, Water/Cliff fallback, moving target, Move/Guard cleanup, attacker/target death, mixed melee ranges and Archer exclusion. Visual positioning should also be checked in Play Mode.");
            }
            finally
            {
                if (target != null) MeleeEngagementSlots.ReleaseTarget(target);
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(root);
                    UnityEngine.Object.Destroy(data);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    UnityEngine.Object.DestroyImmediate(data);
                }
            }
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Melee Engagement", true)]
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

        private static void Configure(NavigationGridData data, TerrainType? obstacle,
            bool raised = false)
        {
            var cells = new List<NavigationCellData>();
            for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
            {
                bool blocked = x == 5 && y == 4 && obstacle.HasValue;
                cells.Add(new NavigationCellData(x, y,
                    blocked ? obstacle.Value : TerrainType.FlatGround, blocked && raised ? 1 : 0));
            }
            data.Configure(10, 10, Vector2.zero, 1f, cells);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
