using System;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase4B1CombatValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 4B1 Combat")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Run Phase 4B1 validation immediately after entering Play Mode.");

            GameObject map = GameObject.Find("Map");
            Transform combatGroup = map != null ? map.transform.Find("CombatTestUnits") : null;
            Transform blueGroup = map != null ? map.transform.Find("BlueTestUnits") : null;
            Transform redGroup = map != null ? map.transform.Find("RedTestUnits") : null;
            GridManager grid = map != null ? map.transform.Find("Navigation")?.GetComponent<GridManager>() : null;
            Require(combatGroup != null && combatGroup.childCount == 2 && blueGroup != null
                && blueGroup.childCount == 3 && redGroup != null && redGroup.childCount == 1
                && grid != null, "Map test setup is incomplete or Phase 3/4A units changed.");

            ValidatePrefab("BlueWarriorTest", Faction.Blue, UnitType.Warrior, 260);
            ValidatePrefab("RedWarriorTest", Faction.Red, UnitType.Warrior, 260);
            ValidatePrefab("BlueArcherTest", Faction.Blue, UnitType.Archer, 60);
            ValidatePrefab("RedArcherTest", Faction.Red, UnitType.Archer, 60);
            ValidatePrefab("BlueLancerTest", Faction.Blue, UnitType.Lancer, 125);
            ValidatePrefab("RedLancerTest", Faction.Red, UnitType.Lancer, 125);

            UnitController archer = combatGroup.Find("BlueArcherTest_1")?.GetComponent<UnitController>();
            UnitController lancer = combatGroup.Find("RedLancerTest_1")?.GetComponent<UnitController>();
            UnitController warrior = blueGroup.GetChild(0).GetComponent<UnitController>();
            Require(archer != null && lancer != null && warrior != null, "Combat test units are missing.");
            HealthComponent archerHP = archer.GetComponent<HealthComponent>();
            HealthComponent lancerHP = lancer.GetComponent<HealthComponent>();
            CombatController archerCombat = archer.GetComponent<CombatController>();
            Require(archerHP.MaxHP == 60 && lancerHP.MaxHP == 125 && warrior.GetComponent<HealthComponent>().MaxHP == 260,
                "Unit HP data is incorrect.");
            Require(archerCombat.CombatData.UnitType == UnitType.Archer
                && lancer.GetComponent<CombatController>().CombatData.UnitType == UnitType.Lancer,
                "Gameplay unit types are incorrect.");
            Require(lancer.CurrentState == UnitState.Idle, "Red Lancer should start idle.");
            Require(!archerCombat.Attack(warrior), "Archer accepted a friendly target.");
            var friendlyProjectileObject = new GameObject("FriendlyProjectileValidation");
            CombatProjectile friendlyProjectile = friendlyProjectileObject.AddComponent<CombatProjectile>();
            int friendlyHP = warrior.GetComponent<HealthComponent>().CurrentHP;
            friendlyProjectile.Initialize(Faction.Blue, warrior, 13, UnitType.Archer, 8f);
            friendlyProjectile.TickProjectile(1f);
            Require(friendlyProjectile.IsResolved
                && warrior.GetComponent<HealthComponent>().CurrentHP == friendlyHP,
                "Projectile damaged a friendly unit.");

            Vector3 reachablePosition = lancer.transform.position;
            lancer.transform.position = grid.GridToWorld(18, 15);
            Require(!archerCombat.Attack(lancer) && archer.CurrentState == UnitState.Idle,
                "Unreachable elevated target did not fail safely.");
            lancer.transform.position = reachablePosition;

            Require(CombatDamage.Calculate(28, UnitType.Warrior, UnitType.Archer) == 35
                && CombatDamage.Calculate(10, UnitType.Archer, UnitType.Warrior) == 8
                && CombatDamage.Calculate(10, UnitType.Archer, UnitType.Lancer) == 13
                && CombatDamage.Calculate(18, UnitType.Lancer, UnitType.Archer) == 14
                && CombatDamage.Calculate(18, UnitType.Lancer, UnitType.Warrior) == 27
                && CombatDamage.Calculate(28, UnitType.Warrior, UnitType.Lancer) == 28,
                "Counter damage calculation failed.");

            Require(archerCombat.Attack(lancer), "Archer Attack order failed.");
            Require(archerCombat.LastProjectile == null,
                "Archer launched before the Shoot windup.");
            archerCombat.TickCombat(0.69f);
            Require(archerCombat.LastProjectile == null,
                "Archer launched before the configured 0.7-second release.");
            archerCombat.TickCombat(0.02f);
            CombatProjectile first = archerCombat.LastProjectile;
            Require(first != null && first.GetComponent<SpriteRenderer>().sprite != null,
                "Archer did not launch a visible projectile.");
            Require(lancerHP.CurrentHP == 125, "Projectile dealt damage before reaching target.");
            first.TickProjectile(1f);
            Require(first.IsResolved && lancerHP.CurrentHP == 112,
                "Projectile did not deliver exactly 13 counter damage.");
            first.TickProjectile(1f);
            Require(lancerHP.CurrentHP == 112, "Projectile damaged target twice.");

            archerCombat.TickCombat(0.50f);
            Require(archerCombat.LastProjectile == first,
                "The next attack cycle released an Arrow immediately.");
            archerCombat.TickCombat(0.71f);
            CombatProjectile second = archerCombat.LastProjectile;
            Require(second != null && second != first, "Attack cooldown did not launch a new projectile.");
            lancerHP.TakeDamage(125);
            second.TickProjectile(1f);
            Require(second.IsResolved && lancerHP.CurrentHP == 0,
                "Projectile did not cancel after target died.");
            Require(archerCombat.Target == null && archer.CurrentState == UnitState.Idle,
                "Archer retained a dead target.");
            Require(!lancer.GetComponent<UnitMovement>().TryIssueMove(new Vector2Int(10, 9)),
                "Dead Lancer accepted movement.");

            Require(grid.NavigationData.Width == 40 && grid.NavigationData.Height == 28
                && grid.GetCell(0, 0).TerrainType == TerrainType.Water
                && !grid.GetCell(16, 12).Walkable,
                "Water/Cliff navigation data changed.");
            Require(map.transform.Find("Grid/WaterFoam") != null
                && map.transform.Find("Grid/Shadow_L1") != null,
                "Terrain visual Tilemaps changed.");
            Require(Camera.main != null && Camera.main.orthographic,
                "Camera setup changed.");

            Debug.Log("Phase 4B1 Play Mode validation passed: Archer projectile, exact counter damage, friendly rejection, death cleanup, and Phase 1-4A scene structure. Stop Play Mode to reset units.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void ValidatePrefab(string name, Faction faction, UnitType type, int maxHP)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/Units/{name}.prefab");
            Require(prefab != null, $"{name} prefab is missing.");
            UnitController unit = prefab.GetComponent<UnitController>();
            HealthComponent health = prefab.GetComponent<HealthComponent>();
            CombatController combat = prefab.GetComponent<CombatController>();
            SpriteRenderer sprite = prefab.GetComponent<SpriteRenderer>();
            Require(unit != null && health != null && combat != null && sprite != null
                && sprite.sprite != null, $"{name} is missing unit components or idle sprite.");
            Require(unit.Faction == faction && health.CombatData != null
                && health.CombatData == combat.CombatData && health.CombatData.UnitType == type
                && health.CombatData.MaxHP == maxHP,
                $"{name} faction, unit type or data reference is incorrect.");
            if (type == UnitType.Archer)
                Require(new SerializedObject(combat).FindProperty("projectileSprite").objectReferenceValue is Sprite,
                    $"{name} is missing its Arrow sprite.");
        }
    }
}
