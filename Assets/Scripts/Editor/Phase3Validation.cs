using System;
using System.Collections.Generic;
using System.Linq;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase3Validation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 3 Units")]
        public static void RunFromMenu()
        {
            Run(true);
        }

        public static void Run(bool logSuccess)
        {
            GameObject map = GameObject.Find("Map");
            Require(map != null, "Map root was not found.");

            Transform navigation = map.transform.Find("Navigation");
            GridManager grid = navigation != null ? navigation.GetComponent<GridManager>() : null;
            PathfindingSystem pathfinding = navigation != null ? navigation.GetComponent<PathfindingSystem>() : null;
            Require(grid != null && pathfinding != null, "Phase 2 navigation is missing.");
            grid.EnsureInitialized();

            Transform controls = map.transform.Find("PlayerControls");
            SelectionManager selection = controls != null ? controls.GetComponent<SelectionManager>() : null;
            PlayerCommandController commands = controls != null ? controls.GetComponent<PlayerCommandController>() : null;
            Require(selection != null && commands != null, "Player selection or command controller is missing.");

            Transform unitRoot = map.transform.Find("BlueTestUnits");
            Require(unitRoot != null, "BlueTestUnits group is missing.");
            UnitSelection[] units = unitRoot.GetComponentsInChildren<UnitSelection>(true)
                .Where(unit => unit.GetComponent<HealthComponent>()?.CombatData?.UnitType == UnitType.Warrior)
                .ToArray();
            Require(units.Length == 3, "Phase 3 requires exactly three Blue test units.");
            Require(units.All(unit => unit.Controller != null && unit.Movement != null),
                "A test unit is missing a modular unit component.");
            Require(units.All(unit => unit.Controller.Faction == Faction.Blue),
                "Every Phase 3 test unit must belong to the Blue faction.");
            Require(units.All(unit => unit.Controller.CurrentState == UnitState.Idle),
                "A generated test unit did not start Idle.");
            Require(units.All(unit => grid.GetCell(grid.WorldToGrid(unit.transform.position).x,
                grid.WorldToGrid(unit.transform.position).y)?.Walkable == true),
                "A test unit was placed on a blocked cell.");

            ValidateSelection(selection, units);
            ValidateDestinationDistribution(grid);
            ValidateCommands(grid, selection, commands, units);
            ValidateWaypointMotion();

            selection.ClearSelection();
            foreach (UnitSelection unit in units)
                unit.Movement.Stop();

            if (logSuccess)
                Debug.Log("Phase 3 validation passed: selection rules, unique destinations, A* commands, invalid targets and frame-independent movement.");
        }

        private static void ValidateSelection(SelectionManager selection, IReadOnlyList<UnitSelection> units)
        {
            selection.ClearSelection();
            selection.ApplyClickSelection(units[0], false);
            Require(selection.SelectedUnits.Count == 1 && units[0].IsSelected,
                "Single-click selection failed.");

            selection.ApplyClickSelection(units[1], true);
            Require(selection.SelectedUnits.Count == 2 && units[1].IsSelected,
                "Shift additive selection failed.");

            selection.ApplyClickSelection(units[0], true);
            Require(selection.SelectedUnits.Count == 1 && !units[0].IsSelected,
                "Shift toggle selection failed.");

            selection.ApplyClickSelection(null, false);
            Require(selection.SelectedUnits.Count == 0, "Empty-click clear selection failed.");

            selection.ApplyBoxSelection(units, false);
            Require(selection.SelectedUnits.Count == 3 && units.All(unit => unit.IsSelected),
                "Box selection failed to select all friendly units.");
        }

        private static void ValidateDestinationDistribution(GridManager grid)
        {
            List<GridCell> destinations = DestinationDistributor.FindNearbyDestinations(
                grid, new Vector2Int(10, 8), 3);
            Require(destinations.Count == 3, "Could not find three nearby destinations.");
            Require(destinations.Distinct().Count() == 3,
                "Destination distribution assigned duplicate cells.");
            Require(destinations.All(cell => cell.Walkable && !cell.Occupied && cell.HeightLevel == 0),
                "Destination distribution returned an invalid cell.");
            Require(DestinationDistributor.FindNearbyDestinations(grid, Vector2Int.zero, 3).Count == 0,
                "Water should not produce command destinations.");
            Require(DestinationDistributor.FindNearbyDestinations(grid, new Vector2Int(16, 12), 3).Count == 0,
                "Cliff should not produce command destinations.");
        }

        private static void ValidateCommands(GridManager grid, SelectionManager selection,
            PlayerCommandController commands, IReadOnlyList<UnitSelection> units)
        {
            selection.ApplyBoxSelection(units, false);
            Vector3 commandWorld = grid.GridToWorld(10, 8);
            Require(commands.CanIssueMoveCommand(commandWorld),
                "Move cursor query rejected a valid group destination.");
            Require(units.All(unit => unit.Controller.CurrentState == UnitState.Idle
                && unit.Movement.CurrentPath.Count == 0),
                "Move cursor query issued a movement command.");
            int issued = commands.IssueMoveCommand(commandWorld);
            Require(issued == 3, "A valid group command was not issued to all three units.");

            Vector2Int[] destinations = units.Select(unit =>
            {
                IReadOnlyList<Vector3> path = unit.Movement.CurrentPath;
                Require(path.Count > 0, "A commanded unit has no A* path.");
                return grid.WorldToGrid(path[path.Count - 1]);
            }).ToArray();
            Require(destinations.Distinct().Count() == 3,
                "Group movement did not use unique destination cells.");
            Require(units.All(unit => unit.Controller.CurrentState == UnitState.Moving),
                "A commanded unit did not enter Moving state.");

            foreach (UnitSelection unit in units)
                unit.Movement.Stop();
            Vector3[] positions = units.Select(unit => unit.transform.position).ToArray();
            Require(!commands.CanIssueMoveCommand(grid.GridToWorld(0, 0))
                && !commands.CanIssueMoveCommand(grid.GridToWorld(16, 12))
                && !commands.CanIssueMoveCommand(grid.GridToWorld(18, 15)),
                "Move cursor query disagreed with blocked terrain or unreachable height.");
            Require(commands.IssueMoveCommand(grid.GridToWorld(0, 0)) == 0,
                "A Water command should fail safely.");
            Require(commands.IssueMoveCommand(grid.GridToWorld(16, 12)) == 0,
                "A Cliff command should fail safely.");
            Require(commands.IssueMoveCommand(grid.GridToWorld(18, 15)) == 0,
                "Stairless ElevatedGround should remain unreachable.");
            Require(units.Select((unit, index) => unit.transform.position == positions[index]).All(equal => equal),
                "An invalid command moved a unit.");
        }

        private static void ValidateWaypointMotion()
        {
            var path = new WaypointPath();
            path.Set(new[] { new Vector3(1f, 0f), new Vector3(2f, 0f) }, Vector3.zero, 0.001f);
            Vector3 position = Vector3.zero;
            bool completed = false;
            for (int i = 0; i < 20 && !completed; i++)
                position = path.Advance(position, 2f, 0.05f, 0.001f, out completed);

            Require(completed && Vector3.Distance(position, new Vector3(2f, 0f)) < 0.001f,
                "Waypoint movement did not finish exactly at the destination.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
