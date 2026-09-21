using System;
using System.Linq;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase4ACombatValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 4A Combat")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Run Phase 4A validation in Play Mode; it damages the temporary test units.");

            GameObject map = GameObject.Find("Map");
            Require(map != null, "Map root is missing.");
            GridManager grid = map.transform.Find("Navigation")?.GetComponent<GridManager>();
            SelectionManager selection = map.transform.Find("PlayerControls")?.GetComponent<SelectionManager>();
            PlayerCommandController commands = map.transform.Find("PlayerControls")?.GetComponent<PlayerCommandController>();
            Transform blues = map.transform.Find("BlueTestUnits");
            Transform reds = map.transform.Find("RedTestUnits");
            Require(grid != null && selection != null && commands != null && blues != null && reds != null,
                "Phase 4A scene references are incomplete.");
            Require(blues.childCount == 3 && reds.childCount == 1, "Test unit counts changed.");
            grid.EnsureInitialized();

            UnitController blue = blues.GetChild(0).GetComponent<UnitController>();
            UnitController red = reds.GetChild(0).GetComponent<UnitController>();
            HealthComponent blueHP = blue.GetComponent<HealthComponent>();
            HealthComponent redHP = red.GetComponent<HealthComponent>();
            CombatController blueCombat = blue.GetComponent<CombatController>();
            CombatController redCombat = red.GetComponent<CombatController>();
            UnitMovement blueMovement = blue.GetComponent<UnitMovement>();
            Require(blueHP != null && redHP != null && blueCombat != null && redCombat != null,
                "A Warrior is missing health or combat.");
            Require(blue.Faction == Faction.Blue && red.Faction == Faction.Red, "Faction setup is incorrect.");
            Require(blueHP.MaxHP == 260 && blueHP.CurrentHP == 260 && redHP.CurrentHP == 260,
                "Warrior must start at 260 HP.");
            UnitCombatData stats = blueCombat.CombatData;
            Require(stats != null && stats.BaseDamage == 28 && stats.AttackRange == 1f
                && Mathf.Approximately(stats.AttackInterval, 1.1f) && stats.MoveSpeed == 3f,
                "Warrior stats differ from GAME_SPEC.md.");
            Require(red.CurrentState == UnitState.Idle && redCombat.Target == null,
                "Red must stay idle until commanded.");

            selection.ClearSelection();
            selection.ApplyClickSelection(red.GetComponent<UnitSelection>(), false);
            Require(selection.SelectedUnits.Count == 0, "Red unit became player-selectable.");
            selection.ApplyClickSelection(blue.GetComponent<UnitSelection>(), false);
            Require(commands.IssueAttackCommand(red) == 1, "Explicit Attack command failed.");
            Require(blue.CurrentState == UnitState.Chasing && blueMovement.IsMoving,
                "Blue Warrior did not begin chasing through A*.");
            Require(redHP.CurrentHP == 260, "Out-of-range attack caused damage.");

            // Drive the existing waypoint movement and combat ticks in one Editor frame.
            for (int i = 0; i < 120 && redHP.CurrentHP == 260; i++)
            {
                blueMovement.TickMovement(0.05f);
                blueCombat.TickCombat(0.05f);
            }
            Require(redHP.CurrentHP == 232, "First valid hit must deal exactly 28 damage.");
            Require(!blueMovement.IsMoving && blue.CurrentState == UnitState.Attacking,
                "Attacker did not stop within attack range.");
            blueCombat.TickCombat(1.09f);
            Require(redHP.CurrentHP == 232, "Warrior attacked before 1.1 seconds.");
            blueCombat.TickCombat(0.02f);
            Require(redHP.CurrentHP == 204, "Second attack after cooldown must deal 28 damage.");
            Require(redCombat.Attack(blue) && blueHP.CurrentHP == 232,
                "Programmatic mutual-combat order did not deal exactly 28 damage.");
            redCombat.StopCombat();
            Require(red.CurrentState == UnitState.Idle,
                "Red did not return to Idle after explicit combat stop.");

            Vector3 reachablePosition = red.transform.position;
            red.transform.position = grid.GridToWorld(18, 15);
            blueCombat.TickCombat(0.5f);
            Require(blue.CurrentState == UnitState.Idle && blueCombat.Target == null
                && !blueMovement.IsMoving, "Unreachable elevated target did not fail safely.");
            red.transform.position = reachablePosition;

            // A fresh explicit order can resume combat, but cannot reset the attack cooldown.
            Require(blueCombat.Attack(red), "Re-issuing a valid Attack failed.");
            for (int i = 0; i < 12 && redHP.IsAlive; i++)
                blueCombat.TickCombat(1.1f);
            Require(redHP.CurrentHP == 0 && !redHP.IsAlive && red.CurrentState == UnitState.Dead,
                "Death or HP clamping failed.");
            Require(!red.GetComponent<SpriteRenderer>().enabled, "Dead Warrior did not disappear.");
            Require(blueCombat.Target == null && blue.CurrentState == UnitState.Idle,
                "Attacker retained a dead target.");
            Require(!redHP.TakeDamage(28) && redHP.CurrentHP == 0,
                "Dead target accepted more damage.");
            Require(!red.GetComponent<UnitMovement>().TryIssueMove(new Vector2Int(12, 6)),
                "Dead Warrior accepted a move command.");
            Require(!redCombat.Attack(blue), "Dead Warrior accepted an Attack command.");
            Require(commands.IssueAttackCommand(red) == 0, "Player issued an order to a dead target.");

            UnitSelection secondBlue = blues.GetChild(1).GetComponent<UnitSelection>();
            selection.ApplyClickSelection(secondBlue, false);
            secondBlue.GetComponent<HealthComponent>().TakeDamage(260);
            Require(selection.SelectedUnits.Count == 0 && !secondBlue.IsSelected,
                "Selected dead Warrior remained selected.");
            selection.ApplyClickSelection(secondBlue, false);
            Require(selection.SelectedUnits.Count == 0,
                "Dead Warrior could be selected again.");
            Require(grid.NavigationData.Width == 40 && grid.NavigationData.Height == 28,
                "Navigation dimensions changed.");
            Require(map.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true)
                .Any(tilemap => tilemap.name == "WaterFoam")
                && map.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true)
                .Any(tilemap => tilemap.name == "Shadow_L1"),
                "Terrain visual layers are missing.");
            Require(Camera.main != null && Camera.main.orthographic,
                "Orthographic camera was changed.");

            Debug.Log("Phase 4A Play Mode validation passed: stats, command, chase, 1.1s damage, unreachable target, death and selection cleanup. Stop Play Mode to reset test units.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
