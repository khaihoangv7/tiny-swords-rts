using System;
using System.Collections.Generic;
using System.Linq;
using TinyKingdom.AI;
using TinyKingdom.Audio;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.UI;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class MovementPolishValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Movement Polish")]
        public static void Run()
        {
            NavigationGridData data = ScriptableObject.CreateInstance<NavigationGridData>();
            var objectWithGrid = new GameObject("MovementPolishValidationGrid");
            try
            {
                GridManager grid = objectWithGrid.AddComponent<GridManager>();
                Configure(data, 8, 8, (x, y) => TerrainType.FlatGround);
                grid.SetNavigationData(data);

                List<GridCell> diagonal = Path(grid, 0, 0, 3, 3);
                Require(diagonal.Count == 4 && diagonal.Skip(1).Select((cell, i) =>
                    Mathf.Abs(cell.GridX - diagonal[i].GridX) == 1
                    && Mathf.Abs(cell.GridY - diagonal[i].GridY) == 1).All(value => value),
                    "Open ground did not use a shortest diagonal route.");
                double cost = diagonal.Skip(1).Select((cell, i) =>
                    AStarPathfinder.StepCost(diagonal[i], cell)).Sum();
                Require(Math.Abs(cost - 3d * Math.Sqrt(2d)) < 1e-9,
                    "Diagonal cost is not sqrt(2).");

                Configure(data, 2, 2, (x, y) =>
                    (x == 1 && y == 0) ? TerrainType.Water : TerrainType.FlatGround);
                grid.SetNavigationData(data);
                Require(!AStarPathfinder.CanTraverseStep(grid, grid.GetCell(0, 0), grid.GetCell(1, 1))
                    && Path(grid, 0, 0, 1, 1).Count == 3,
                    "Water corner was cut diagonally.");

                Configure(data, 2, 2, (x, y) =>
                    (x == 1 && y == 0) ? TerrainType.Cliff : TerrainType.FlatGround);
                grid.SetNavigationData(data);
                Require(!AStarPathfinder.CanTraverseStep(grid, grid.GetCell(0, 0), grid.GetCell(1, 1)),
                    "Cliff corner was cut diagonally.");

                Configure(data, 2, 2, (x, y) => TerrainType.FlatGround);
                grid.SetNavigationData(data);
                grid.SetOccupied(1, 0, true);
                Require(!AStarPathfinder.CanTraverseStep(grid, grid.GetCell(0, 0), grid.GetCell(1, 1)),
                    "Occupied building corner was cut diagonally.");
                Require(UnitMovement.BlockedPathRepathInterval >= 0.2f
                    && UnitMovement.BlockedPathRepathInterval <= 0.5f,
                    "Blocked-route repath interval is outside the safe responsive range.");
                Require(UnitSeparation.IsSafeSegment(grid,
                        grid.GetCell(1, 0).WorldPosition,
                        grid.GetCell(0, 0).WorldPosition),
                    "A unit already overlapped by a moving blocker cannot escape its cell.");
                Require(!UnitSeparation.IsSafeSegment(grid,
                        grid.GetCell(0, 0).WorldPosition,
                        grid.GetCell(1, 0).WorldPosition),
                    "A unit can still enter a newly occupied resource cell.");

                var heightCells = new List<NavigationCellData>();
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                    heightCells.Add(new NavigationCellData(x, y,
                        x == 1 && y == 1 ? TerrainType.ElevatedGround : TerrainType.FlatGround,
                        x == 1 && y == 1 ? 1 : 0));
                data.Configure(2, 2, Vector2.zero, 1f, heightCells);
                grid.SetNavigationData(data);
                Require(!AStarPathfinder.CanTraverseStep(grid, grid.GetCell(0, 0), grid.GetCell(1, 1))
                    && Path(grid, 0, 0, 1, 1).Count == 0,
                    "Diagonal bypassed a stairless height transition.");

                var diagonalStairCells = new List<NavigationCellData>();
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                {
                    var cell = new NavigationCellData(x, y,
                        x == y ? TerrainType.Stairs : TerrainType.Cliff,
                        x == 1 && y == 1 ? 1 : 0);
                    if (x == 0 && y == 0)
                    {
                        cell.hasHeightConnection = true;
                        cell.heightConnection = new Vector2Int(1, 1);
                    }
                    else if (x == 1 && y == 1)
                    {
                        cell.hasHeightConnection = true;
                        cell.heightConnection = new Vector2Int(0, 0);
                    }
                    diagonalStairCells.Add(cell);
                }
                data.Configure(2, 2, Vector2.zero, 1f, diagonalStairCells);
                grid.SetNavigationData(data);
                Require(AStarPathfinder.CanTraverseStep(grid,
                        grid.GetCell(0, 0), grid.GetCell(1, 1))
                    && Path(grid, 0, 0, 1, 1).Count == 2,
                    "Authored diagonal stair connector was not traversable.");

                NavigationGridData map = AssetDatabase.LoadAssetAtPath<NavigationGridData>(
                    Phase2NavigationBuilder.DataPath);
                Require(map != null, "Map navigation data missing.");
                Phase2NavigationValidation.Run(map, false);

                Configure(data, 8, 8, (x, y) => TerrainType.FlatGround);
                grid.SetNavigationData(data);
                List<GridCell> destinations = DestinationDistributor.FindNearbyDestinations(
                    grid, new Vector2Int(4, 4), 3);
                Require(destinations.Count == 3 && destinations.Distinct().Count() == 3,
                    "Group destinations are not distinct.");

                var path = new WaypointPath();
                path.Set(new[] { new Vector3(1.5f, 1.5f) }, new Vector3(0.5f, 0.5f), 0.01f);
                Vector3 intermediate = path.Advance(new Vector3(0.5f, 0.5f), 1f, 0.1f,
                    0.01f, out bool completed);
                Require(!completed && intermediate.x > 0.5f && intermediate.x < 1.5f
                    && Mathf.Abs(intermediate.x - intermediate.y) < 0.0001f,
                    "Diagonal waypoint motion snapped to a tile.");

                ValidateLocalSeparation(grid, data);
                Debug.Log("Movement polish validation passed: octile paths, sqrt(2) cost, Water/Cliff/occupied corners, height, three destinations, continuous diagonal waypoints and local separation. Play Mode interaction remains manual.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(objectWithGrid);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static void ValidateLocalSeparation(GridManager grid, NavigationGridData data)
        {
            List<GridCell> goals = DestinationDistributor.FindNearbyDestinations(
                grid, new Vector2Int(4, 4), 3);
            var paths = new WaypointPath[3];
            Vector3[] moving = { new(3.5f, 3.5f), new(3.5f, 3.5f), new(3.5f, 3.5f) };
            for (int i = 0; i < moving.Length; i++)
            {
                paths[i] = new WaypointPath();
                paths[i].Set(new[] { goals[i].WorldPosition }, moving[i], 0.02f);
            }
            for (int tick = 0; tick < 100; tick++)
            for (int i = 0; i < moving.Length; i++)
            {
                Vector3 desired = paths[i].Advance(moving[i], 3f, 0.05f, 0.02f, out _);
                var neighbors = new List<UnitSeparation.Neighbor>();
                for (int j = 0; j < moving.Length; j++)
                    if (i != j) neighbors.Add(new UnitSeparation.Neighbor(moving[j], 0.35f, j + 1));
                moving[i] = UnitSeparation.Resolve(desired, 0.35f, 1.2f,
                    0.05f, i + 1, neighbors, grid);
            }
            Require(PairsSeparated(moving, 0.25f)
                && moving.Select((position, index) =>
                    Vector3.Distance(position, goals[index].WorldPosition) < 0.75f).All(value => value),
                "Units with distinct nearby destinations ended stacked or missed their goals.");

            Vector3[] positions = { new(3.5f, 3.5f), new(3.5f, 3.5f), new(3.5f, 3.5f) };
            for (int tick = 0; tick < 30; tick++)
            for (int i = 0; i < positions.Length; i++)
            {
                var neighbors = new List<UnitSeparation.Neighbor>();
                for (int j = 0; j < positions.Length; j++)
                    if (i != j) neighbors.Add(new UnitSeparation.Neighbor(positions[j], 0.35f, j + 1));
                positions[i] = UnitSeparation.Resolve(positions[i], 0.35f, 1.2f,
                    0.05f, i + 1, neighbors, grid);
            }
            Require(PairsSeparated(positions, 0.25f),
                "Three living units remained stacked after local separation.");

            Vector3 target = new(4.5f, 4.5f);
            Vector3[] attackers = { target, target, target };
            for (int tick = 0; tick < 30; tick++)
            for (int i = 0; i < attackers.Length; i++)
            {
                var neighbors = new List<UnitSeparation.Neighbor>
                {
                    new(target, 0.35f, 10)
                };
                for (int j = 0; j < attackers.Length; j++)
                    if (i != j) neighbors.Add(new UnitSeparation.Neighbor(attackers[j], 0.35f, j + 1));
                attackers[i] = UnitSeparation.Resolve(attackers[i], 0.35f, 1.2f,
                    0.05f, i + 1, neighbors, grid);
            }
            Require(PairsSeparated(attackers, 0.1f)
                && attackers.All(attacker => Vector3.Distance(attacker, target) <= 1f),
                "Melee attackers stacked or left Warrior attack range.");

            Configure(data, 8, 8, (x, y) => x == 5 && y == 4
                ? TerrainType.Water : TerrainType.FlatGround);
            grid.SetNavigationData(data);
            Vector3 nearEdge = new(4.95f, 4.5f);
            Vector3 corrected = UnitSeparation.Resolve(nearEdge, 0.35f, 1.2f, 0.05f, 1,
                new[] { new UnitSeparation.Neighbor(new Vector2(4.7f, 4.5f), 0.35f, 2) }, grid);
            Require(grid.WorldToGrid(corrected) == new Vector2Int(4, 4),
                "Separation pushed a unit into Water.");

            Configure(data, 8, 8, (x, y) => x == 5 && y == 4
                ? TerrainType.Cliff : TerrainType.FlatGround);
            grid.SetNavigationData(data);
            corrected = UnitSeparation.Resolve(nearEdge, 0.35f, 1.2f, 0.05f, 1,
                new[] { new UnitSeparation.Neighbor(new Vector2(4.7f, 4.5f), 0.35f, 2) }, grid);
            Require(grid.WorldToGrid(corrected) == new Vector2Int(4, 4),
                "Separation pushed a unit into Cliff.");

            var elevated = new List<NavigationCellData>();
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                elevated.Add(new NavigationCellData(x, y, TerrainType.FlatGround,
                    x == 5 && y == 4 ? 1 : 0));
            data.Configure(8, 8, Vector2.zero, 1f, elevated);
            grid.SetNavigationData(data);
            corrected = UnitSeparation.Resolve(nearEdge, 0.35f, 1.2f, 0.05f, 1,
                new[] { new UnitSeparation.Neighbor(new Vector2(4.7f, 4.5f), 0.35f, 2) }, grid);
            Require(grid.WorldToGrid(corrected) == new Vector2Int(4, 4),
                "Separation bypassed a stairless height transition.");
        }

        private static bool PairsSeparated(Vector3[] points, float minimum)
        {
            for (int i = 0; i < points.Length; i++)
            for (int j = i + 1; j < points.Length; j++)
                if (Vector3.Distance(points[i], points[j]) <= minimum) return false;
            return true;
        }

        private static void Configure(NavigationGridData data, int width, int height,
            Func<int, int, TerrainType> terrain)
        {
            var cells = new List<NavigationCellData>();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                cells.Add(new NavigationCellData(x, y, terrain(x, y), 0));
            data.Configure(width, height, Vector2.zero, 1f, cells);
        }

        private static List<GridCell> Path(IGridCellProvider grid, int x0, int y0, int x1, int y1)
            => AStarPathfinder.FindPath(grid, grid.GetCell(x0, y0), grid.GetCell(x1, y1));

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }

    public static class Phase6G1RoleFormationValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6G.1 Role Formation")]
        public static void Run()
        {
            ValidatePriorityTable();
            Require(!RoleFormationPlanner.RequiresFormation(1)
                && RoleFormationPlanner.RequiresFormation(2),
                "Single-unit movement must bypass formation planning.");
            Require(RoleFormationPlanner.DefaultSpacing
                    >= RoleFormationPlanner.MinimumSpacing,
                "Default formation spacing is invalid.");
            Require(typeof(PlayerCommandController).GetProperty("FormationSpacing") != null,
                "PlayerCommandController is not exposing its formation configuration.");
            Require(typeof(FactionCommandService).GetMethod("TryMove",
                    new[] { typeof(UnitController), typeof(Vector3) }) != null,
                "The existing AI single-actor movement path is missing.");

            NavigationGridData data = ScriptableObject.CreateInstance<NavigationGridData>();
            var root = new GameObject("Phase6G1RoleFormationValidation");
            var definitions = new List<UnitCombatData>();
            var units = new List<UnitSelection>();
            try
            {
                GridManager grid = root.AddComponent<GridManager>();
                Configure(data, 24, 20);
                grid.SetNavigationData(data);

                // Deliberately scramble selection order. The role table, not selection
                // order, must define front-to-back placement.
                units.Add(CreateUnit(root.transform, UnitType.Pawn, new Vector3(3.5f, 8.5f), definitions));
                units.Add(CreateUnit(root.transform, UnitType.Archer, new Vector3(4.5f, 11.5f), definitions));
                units.Add(CreateUnit(root.transform, UnitType.Warrior, new Vector3(3.5f, 10.5f), definitions));
                units.Add(CreateUnit(root.transform, UnitType.Monk, new Vector3(4.5f, 8.5f), definitions));
                units.Add(CreateUnit(root.transform, UnitType.Lancer, new Vector3(3.5f, 11.5f), definitions));

                var assignments = new List<FormationAssignment>();
                Vector3 rightTarget = new(16.5f, 10.5f);
                Require(RoleFormationPlanner.TryPlan(grid, units, rightTarget,
                        RoleFormationPlanner.DefaultSpacing, 4, 8, false, assignments),
                    "Could not plan a right-facing role formation.");
                ValidateAssignments(assignments, units.Count);
                ValidateRolePriority(assignments, Vector2.right);

                var repeated = new List<FormationAssignment>();
                units.Reverse();
                Require(RoleFormationPlanner.TryPlan(grid, units, rightTarget,
                        RoleFormationPlanner.DefaultSpacing, 4, 8, false, repeated),
                    "Reordered selection could not be planned.");
                Require(SameAssignments(assignments, repeated),
                    "Selection order changed deterministic role assignments.");

                var left = new List<FormationAssignment>();
                Vector3 leftTarget = new(2.5f, 10.5f);
                Require(RoleFormationPlanner.TryPlan(grid, units, leftTarget,
                        RoleFormationPlanner.DefaultSpacing, 4, 8, false, left),
                    "Could not plan a left-facing role formation.");
                ValidateRolePriority(left, Vector2.left);

                Vector2 diagonal = RoleFormationPlanner.ResolveForward(
                    new Vector3(2f, 2f), new Vector3(8f, 8f));
                Require(Vector2.Dot(diagonal, new Vector2(1f, 1f).normalized) > 0.999f,
                    "Formation facing does not preserve arbitrary diagonal direction.");
                Require(RoleFormationPlanner.ResolveForward(Vector3.one, Vector3.one)
                        == Vector2.right,
                    "Near-zero movement does not use the deterministic fallback.");

                Vector2Int occupied = grid.WorldToGrid(rightTarget);
                grid.SetOccupied(occupied.x, occupied.y, true);
                var corrected = new List<FormationAssignment>();
                Require(RoleFormationPlanner.TryPlan(grid, units, rightTarget,
                        RoleFormationPlanner.DefaultSpacing, 4, 8, true, corrected),
                    "Formation could not correct an occupied center slot.");
                Require(corrected.All(item => item.Cell != null && item.Cell.Walkable
                    && !item.Cell.Occupied),
                    "Formation correction retained a blocked destination.");

                Debug.Log("Phase 6G.1 role formation validation passed: canonical priorities, "
                    + "rotated basis, ordered tactical slots, deterministic assignment, occupied-slot "
                    + "correction, single-unit bypass and unchanged AI command API verified. "
                    + "Complete the directional Play Mode checklist before marking PASS.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                for (int i = 0; i < definitions.Count; i++)
                    UnityEngine.Object.DestroyImmediate(definitions[i]);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static void ValidatePriorityTable()
        {
            Require(FormationRolePriority.For(UnitType.Warrior) == 0
                && FormationRolePriority.For(UnitType.Lancer) == 1
                && FormationRolePriority.For(UnitType.Archer) == 2
                && FormationRolePriority.For(UnitType.Monk) == 3
                && FormationRolePriority.For(UnitType.Pawn) == 4,
                "Canonical player-unit role priorities are invalid.");
            Require(FormationRolePriority.For(UnitType.GoblinTorch)
                    == FormationRolePriority.SafeDefault
                && FormationRolePriority.For(UnitType.GoblinTNT)
                    == FormationRolePriority.SafeDefault,
                "Future/non-player unit types lack a safe fallback priority.");
        }

        private static void ValidateAssignments(List<FormationAssignment> assignments,
            int expectedCount)
        {
            Require(assignments.Count == expectedCount,
                "Planner did not assign every eligible unit.");
            Require(assignments.Select(item => item.Unit).Distinct().Count() == expectedCount,
                "A unit received more than one formation slot.");
            Require(assignments.Select(item => item.Cell).Distinct().Count() == expectedCount,
                "Formation contains duplicate destination cells.");
        }

        private static void ValidateRolePriority(List<FormationAssignment> assignments,
            Vector2 forward)
        {
            int previousSlot = -1;
            float previousDepth = float.PositiveInfinity;
            for (int priority = FormationRolePriority.Warrior;
                priority <= FormationRolePriority.Pawn; priority++)
            {
                FormationAssignment assignment = assignments.Single(
                    item => item.RolePriority == priority);
                float depth = Vector2.Dot(assignment.DesiredPosition, forward);
                Require(assignment.SlotIndex > previousSlot,
                    "Role priority is not receiving progressively later tactical slots.");
                Require(depth <= previousDepth + 0.0001f,
                    "Role priority moved a later role in front of an earlier role.");
                previousSlot = assignment.SlotIndex;
                previousDepth = depth;
            }
        }

        private static bool SameAssignments(List<FormationAssignment> first,
            List<FormationAssignment> second)
        {
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++)
            {
                FormationAssignment match = second.SingleOrDefault(
                    item => item.Unit == first[i].Unit);
                if (match.Unit == null || match.Cell != first[i].Cell) return false;
            }
            return true;
        }

        internal static UnitSelection CreateUnit(Transform parent, UnitType type,
            Vector3 position, List<UnitCombatData> definitions)
        {
            var go = new GameObject($"Formation_{type}");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            UnitController controller = go.AddComponent<UnitController>();
            go.AddComponent<UnitMovement>();
            HealthComponent health = go.AddComponent<HealthComponent>();
            UnitSelection selection = go.AddComponent<UnitSelection>();
            UnitCombatData definition = ScriptableObject.CreateInstance<UnitCombatData>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("unitType").enumValueIndex = (int)type;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            definitions.Add(definition);
            health.Configure(definition);
            controller.Configure(Faction.Blue);
            go.SetActive(true);
            return selection;
        }

        internal static void Configure(NavigationGridData data, int width, int height)
        {
            var cells = new List<NavigationCellData>(width * height);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                cells.Add(new NavigationCellData(x, y, TerrainType.FlatGround, 0));
            data.Configure(width, height, Vector2.zero, 1f, cells);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }

    public static class Phase6G2FormationCommandsValidation
    {
        private const float GeometryTolerance = 0.001f;

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6G.2 Formation Commands")]
        public static void Run()
        {
            ValidateRegistrationAndUiContract();

            NavigationGridData data = ScriptableObject.CreateInstance<NavigationGridData>();
            var root = new GameObject("Phase6G2FormationCommandsValidation");
            var definitions = new List<UnitCombatData>();
            var selectedUnits = new List<UnitSelection>();
            try
            {
                GridManager grid = root.AddComponent<GridManager>();
                Phase6G1RoleFormationValidation.Configure(data, 72, 56);
                grid.SetNavigationData(data);
                AddRolePair(root.transform, selectedUnits, definitions,
                    UnitType.Warrior, 6f);
                AddRolePair(root.transform, selectedUnits, definitions,
                    UnitType.Lancer, 8f);
                AddRolePair(root.transform, selectedUnits, definitions,
                    UnitType.Archer, 10f);
                AddRolePair(root.transform, selectedUnits, definitions,
                    UnitType.Monk, 12f);
                for (int i = 0; i < 5; i++)
                    selectedUnits.Add(Phase6G1RoleFormationValidation.CreateUnit(
                        root.transform, UnitType.Pawn,
                        new Vector3(58f + i, 42f + i), definitions));

                List<UnitSelection> militaryUnits = selectedUnits
                    .Where(MilitaryFormationEligibility.IsFormationMilitaryUnit)
                    .ToList();
                ValidateMilitaryPartition(grid, selectedUnits, militaryUnits,
                    root.transform, definitions);

                Vector3 anchor = new(35.5f, 27.5f);
                Vector2 forward = new Vector2(1f, 0.45f).normalized;
                Dictionary<FormationType, List<FormationAssignment>> plans = new();
                foreach (FormationType type in Enum.GetValues(typeof(FormationType)))
                {
                    List<FormationAssignment> plan = Plan(grid, militaryUnits, anchor,
                        type, forward, 12);
                    ValidateCommon(plan, militaryUnits.Count, anchor);
                    ValidateRoleSlots(plan, type, forward);
                    Require(plan.All(item =>
                            MilitaryFormationEligibility.IsFormationMilitaryUnit(item.Unit)),
                        $"{type} assigned a non-military formation slot.");
                    plans.Add(type, plan);
                }

                ValidateCompact(plans[FormationType.Compact], forward);
                ValidateWedge(plans[FormationType.Wedge], forward);
                ValidateLoose(plans[FormationType.Compact],
                    plans[FormationType.Loose], forward);
                ValidateLine(plans[FormationType.Line], anchor, forward, true);
                ValidateLineRotations(grid, militaryUnits, anchor);
                ValidateOddAndTwoUnitShapes(grid, militaryUnits, anchor);
                ValidateLargeLine(grid, root.transform, militaryUnits,
                    definitions, anchor);
                ValidateObstacleCorrection(grid, militaryUnits, anchor, forward);

                Debug.Log("Phase 6G.2 formation command validation passed: exact formation "
                    + "registration, Compact default, four shared HUD commands, procedural "
                    + "glyph contract, shared click/reject audio, rotated Compact/Wedge/Loose/Line "
                    + "geometry, military-only eligibility/centroid/reform partition, symmetric "
                    + "even/odd one-rank Line assignment, role priority, large-group extent and "
                    + "legal-cell correction verified. Complete the Play "
                    + "Mode checklist before marking Phase 6G.2 PASS.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                for (int i = 0; i < definitions.Count; i++)
                    UnityEngine.Object.DestroyImmediate(definitions[i]);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static void ValidateRegistrationAndUiContract()
        {
            string[] names = Enum.GetNames(typeof(FormationType));
            string[] expected = { "Compact", "Wedge", "Loose", "Line" };
            Require(names.SequenceEqual(expected),
                "FormationType must contain exactly Compact, Wedge, Loose and Line.");
            Require(!names.Any(name => name.IndexOf("Column",
                    StringComparison.OrdinalIgnoreCase) >= 0),
                "Obsolete Column/SingleColumn formation registration exists.");
            Require(MilitaryFormationEligibility.IsFormationMilitaryType(UnitType.Warrior)
                && MilitaryFormationEligibility.IsFormationMilitaryType(UnitType.Lancer)
                && MilitaryFormationEligibility.IsFormationMilitaryType(UnitType.Archer)
                && MilitaryFormationEligibility.IsFormationMilitaryType(UnitType.Monk),
                "One or more intended military types are not formation eligible.");
            Require(!MilitaryFormationEligibility.IsFormationMilitaryType(UnitType.Pawn)
                && !MilitaryFormationEligibility.IsFormationMilitaryType(UnitType.GoblinTorch)
                && !MilitaryFormationEligibility.IsFormationMilitaryType(UnitType.GoblinTNT),
                "Pawn or an unknown/future unit entered military formation eligibility.");
            Require(!MilitaryFormationEligibility.HasFormationCommandGroup(0)
                && !MilitaryFormationEligibility.HasFormationCommandGroup(1)
                && MilitaryFormationEligibility.HasFormationCommandGroup(2),
                "Formation command visibility must require at least two eligible military units.");
            Require(FormationRolePriority.For(UnitType.Warrior)
                    < FormationRolePriority.For(UnitType.Lancer)
                && FormationRolePriority.For(UnitType.Lancer)
                    < FormationRolePriority.For(UnitType.Archer)
                && FormationRolePriority.For(UnitType.Archer)
                    < FormationRolePriority.For(UnitType.Monk)
                && FormationRolePriority.For(UnitType.Monk)
                    < FormationRolePriority.For(UnitType.Pawn),
                "Phase 6G.1 role priority is not shared by the formation planner.");

            var commandObject = new GameObject("FormationDefaultValidation");
            commandObject.SetActive(false);
            try
            {
                PlayerCommandController controller =
                    commandObject.AddComponent<PlayerCommandController>();
                Require(controller.CurrentFormation == FormationType.Compact,
                    "Player formation default must be Compact.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(commandObject);
            }

            Require(typeof(PlayerCommandController).GetMethod("SelectFormation",
                    new[] { typeof(FormationType) })?.ReturnType == typeof(bool),
                "PlayerCommandController formation command API is missing.");
            Require(CommandPanelPresenter.FormationButtonCount == 4
                && CommandPanelPresenter.ActionViewCount >= 13,
                "Command Panel does not reserve four formation command buttons.");
            Require(CommandPanelPresenter.FormationActiveColor
                    != Color.white,
                "Active formation does not have a distinct visual state.");

            var field = typeof(CommandPanelPresenter).GetField("FormationActions",
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static);
            FormationType[] actions = field?.GetValue(null) as FormationType[];
            Require(actions != null && actions.SequenceEqual(
                    new[] { FormationType.Compact, FormationType.Wedge,
                        FormationType.Loose, FormationType.Line }),
                "Command Panel formation button order or registration is invalid.");
            Require(typeof(CommandPanelPresenter).GetMethod("ConfigureFormationTooltip",
                    System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance) != null,
                "Formation buttons are missing tooltip integration.");
            Require(Enum.IsDefined(typeof(GameplayAudioEvent), "CommandClick")
                && Enum.IsDefined(typeof(GameplayAudioEvent), "CommandRejected"),
                "Existing Phase 6E command click/reject audio hooks are unavailable.");
        }

        private static void ValidateMilitaryPartition(GridManager grid,
            List<UnitSelection> selectedUnits, List<UnitSelection> militaryUnits,
            Transform root, List<UnitCombatData> definitions)
        {
            Require(militaryUnits.Count == 8
                && militaryUnits.All(
                    MilitaryFormationEligibility.IsFormationMilitaryUnit),
                "Mixed selection did not isolate the eight military members.");
            Require(selectedUnits.Count(unit =>
                    MilitaryFormationEligibility.IsFormationMilitaryUnit(unit)) == 8
                && selectedUnits.Count - militaryUnits.Count == 5,
                "Pawn selection partition is inconsistent.");

            List<UnitSelection> warriors = militaryUnits.Where(unit =>
                unit.GetComponent<HealthComponent>()?.CombatData?.UnitType
                    == UnitType.Warrior).ToList();
            List<UnitSelection> archers = militaryUnits.Where(unit =>
                unit.GetComponent<HealthComponent>()?.CombatData?.UnitType
                    == UnitType.Archer).ToList();
            List<UnitSelection> pawns = selectedUnits.Where(unit =>
                unit.GetComponent<HealthComponent>()?.CombatData?.UnitType
                    == UnitType.Pawn).ToList();

            Require(!MilitaryFormationEligibility.HasFormationCommandGroup(
                    pawns.Count(unit =>
                        MilitaryFormationEligibility.IsFormationMilitaryUnit(unit))),
                "Pawn-only selection would expose formation commands.");
            var oneMilitaryAndPawns = new List<UnitSelection> { warriors[0] };
            oneMilitaryAndPawns.AddRange(pawns);
            Require(!MilitaryFormationEligibility.HasFormationCommandGroup(
                    oneMilitaryAndPawns.Count(
                        MilitaryFormationEligibility.IsFormationMilitaryUnit)),
                "One military unit plus Pawns would expose formation commands.");
            Require(MilitaryFormationEligibility.HasFormationCommandGroup(
                    warriors.Count),
                "Two Warriors would not expose formation commands.");
            Require(MilitaryFormationEligibility.HasFormationCommandGroup(
                    new[] { warriors[0], archers[0] }.Count(
                        MilitaryFormationEligibility.IsFormationMilitaryUnit)),
                "Warrior plus Archer would not expose formation commands.");

            var twoWarriorThreePawn = new List<UnitSelection>
                { warriors[0], warriors[1], pawns[0], pawns[1], pawns[2] };
            List<UnitSelection> twoMilitary = twoWarriorThreePawn.Where(
                MilitaryFormationEligibility.IsFormationMilitaryUnit).ToList();
            List<FormationAssignment> twoSlots = Plan(grid, twoMilitary,
                new Vector3(35.5f, 27.5f), FormationType.Compact,
                Vector2.right, 12);
            Require(twoSlots.Count == 2 && twoSlots.All(item =>
                    MilitaryFormationEligibility.IsFormationMilitaryUnit(item.Unit)),
                "Two Warriors plus three Pawns did not produce exactly two military slots.");

            var reformSelection = new List<UnitSelection>
                { warriors[0], warriors[1], archers[0], pawns[0], pawns[1] };
            List<UnitSelection> reformMilitary = reformSelection.Where(
                MilitaryFormationEligibility.IsFormationMilitaryUnit).ToList();
            List<FormationAssignment> reform = Plan(grid, reformMilitary,
                RoleFormationPlanner.CalculateGroupOrigin(reformMilitary),
                FormationType.Wedge, Vector2.right, 12);
            Require(reform.Count == 3 && reform.All(item =>
                    MilitaryFormationEligibility.IsFormationMilitaryUnit(item.Unit)),
                "Mixed Wedge reform assigned a Pawn or omitted a military unit.");

            Vector3 militaryCenter = RoleFormationPlanner.CalculateGroupOrigin(militaryUnits);
            Vector3 mixedCenter = RoleFormationPlanner.CalculateGroupOrigin(selectedUnits);
            Require(militaryCenter.x < 10f
                && Vector2.Distance(militaryCenter, mixedCenter) > 15f,
                "Distant Pawns affected the military-only formation centroid test.");

            // Keep this signature explicitly tied to the same helper used by runtime;
            // future unit types remain excluded until deliberately registered.
            UnitSelection future = Phase6G1RoleFormationValidation.CreateUnit(root,
                UnitType.GoblinTorch, new Vector3(64f, 48f), definitions);
            Require(!MilitaryFormationEligibility.IsFormationMilitaryUnit(future),
                "An unregistered future/non-player unit entered formation eligibility.");
        }

        private static List<FormationAssignment> Plan(GridManager grid,
            IReadOnlyList<UnitSelection> units, Vector3 anchor,
            FormationType type, Vector2 forward, int searchRadius,
            bool searchAroundOccupiedCenter = false)
        {
            var result = new List<FormationAssignment>();
            Require(RoleFormationPlanner.TryPlan(grid, units, anchor,
                    RoleFormationPlanner.DefaultSpacing, 4, searchRadius,
                    searchAroundOccupiedCenter, type, forward, result),
                $"Could not plan {type} formation.");
            return result;
        }

        private static void ValidateCommon(List<FormationAssignment> plan,
            int expectedCount, Vector3 anchor)
        {
            Require(plan.Count == expectedCount,
                "Formation did not assign every eligible unit.");
            Require(plan.Select(item => item.Unit).Distinct().Count() == expectedCount,
                "A unit received multiple formation assignments.");
            Require(plan.Select(item => item.Cell).Distinct().Count() == expectedCount,
                "Formation resolved two units to the same logical cell.");
            Require(plan.All(item => item.Cell != null && item.Cell.Walkable
                    && !item.Cell.Occupied),
                "Formation retained an invalid final logical cell.");
            Vector3 center = Vector3.zero;
            for (int i = 0; i < plan.Count; i++) center += plan[i].DesiredPosition;
            center /= plan.Count;
            Require(Vector2.Distance(center, anchor) <= GeometryTolerance,
                "Desired formation geometry is not centered on the command anchor.");
        }

        private static void ValidateRoleSlots(List<FormationAssignment> plan,
            FormationType type, Vector2 forward)
        {
            int previousMaximumSlot = -1;
            float previousDepth = float.PositiveInfinity;
            float previousRadius = -1f;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < plan.Count; i++) center += plan[i].DesiredPosition;
            center /= plan.Count;
            foreach (IGrouping<int, FormationAssignment> role in plan
                .GroupBy(item => item.RolePriority).OrderBy(group => group.Key))
            {
                int minimumSlot = role.Min(item => item.SlotIndex);
                int maximumSlot = role.Max(item => item.SlotIndex);
                Require(minimumSlot > previousMaximumSlot,
                    $"{type} role assignment does not follow canonical tactical slot order.");
                previousMaximumSlot = maximumSlot;

                if (type == FormationType.Line)
                {
                    float minimumRadius = role.Min(item =>
                        Mathf.Abs(Vector2.Dot(item.DesiredPosition - center,
                            RightOf(forward))));
                    Require(minimumRadius + GeometryTolerance >= previousRadius,
                        "Line role assignment is not ordered center-to-wing.");
                    previousRadius = role.Max(item =>
                        Mathf.Abs(Vector2.Dot(item.DesiredPosition - center,
                            RightOf(forward))));
                }
                else
                {
                    float maximumDepth = role.Max(item =>
                        Vector2.Dot(item.DesiredPosition, forward));
                    Require(maximumDepth <= previousDepth + GeometryTolerance,
                        $"{type} moved a later role ahead of an earlier role.");
                    previousDepth = role.Min(item =>
                        Vector2.Dot(item.DesiredPosition, forward));
                }
            }
        }

        private static void ValidateCompact(List<FormationAssignment> plan,
            Vector2 forward)
        {
            Require(DistinctDepths(plan, forward).Count > 1,
                "Compact did not create a multi-row block.");
            Require(Span(plan, RightOf(forward)) > 0f && Span(plan, forward) > 0f,
                "Compact block has no lateral or row extent.");
        }

        private static void ValidateWedge(List<FormationAssignment> plan,
            Vector2 forward)
        {
            List<IGrouping<int, FormationAssignment>> rows = plan.GroupBy(item =>
                    Mathf.RoundToInt(Vector2.Dot(item.DesiredPosition, forward) * 1000f))
                .OrderByDescending(group => group.Key).ToList();
            var expected = new List<int>();
            int remaining = plan.Count;
            int width = 1;
            while (remaining > 0)
            {
                int rowCount = Mathf.Min(width++, remaining);
                expected.Add(rowCount);
                remaining -= rowCount;
            }
            Require(rows.Count == expected.Count
                && rows.Select(row => row.Count()).SequenceEqual(expected),
                "Wedge rows do not widen correctly or center the partial final row.");
            foreach (IGrouping<int, FormationAssignment> row in rows)
                ValidateLateralSymmetry(row.ToList(), RightOf(forward));
        }

        private static void ValidateLoose(List<FormationAssignment> compact,
            List<FormationAssignment> loose, Vector2 forward)
        {
            Require(Span(loose, RightOf(forward))
                    > Span(compact, RightOf(forward)) * 1.4f
                && Span(loose, forward) > Span(compact, forward) * 1.4f,
                "Loose does not expand both Compact spacing axes sufficiently.");
            Require(RoleFormationPlanner.LooseSpacingMultiplier > 1f,
                "Loose spacing multiplier must exceed Compact spacing.");
        }

        private static void ValidateLine(List<FormationAssignment> plan,
            Vector3 anchor, Vector2 forward, bool even)
        {
            List<float> depths = plan.Select(item =>
                Vector2.Dot(item.DesiredPosition - anchor, forward)).ToList();
            Require(depths.Max() - depths.Min() <= GeometryTolerance,
                "Line created more than one intended forward rank.");
            List<float> lateral = plan.Select(item =>
                    Vector2.Dot(item.DesiredPosition - anchor, RightOf(forward)))
                .OrderBy(value => value).ToList();
            for (int i = 0; i < lateral.Count; i++)
                Require(Mathf.Abs(lateral[i] + lateral[lateral.Count - 1 - i])
                        <= GeometryTolerance,
                    "Line is not symmetric around its destination center.");
            if (even)
                Require(lateral.All(value => Mathf.Abs(value) > GeometryTolerance),
                    "Even Line should use symmetric half-spacing without a center slot.");
            else
                Require(lateral.Any(value => Mathf.Abs(value) <= GeometryTolerance),
                    "Odd Line requires one exact center slot.");
        }

        private static void ValidateLineRotations(GridManager grid,
            IReadOnlyList<UnitSelection> units, Vector3 anchor)
        {
            Vector2[] directions =
            {
                Vector2.right, Vector2.left, Vector2.up, Vector2.down,
                new Vector2(1f, 1f).normalized,
                new Vector2(-1f, 1f).normalized,
                new Vector2(1f, -1f).normalized,
                new Vector2(-1f, -1f).normalized
            };
            for (int i = 0; i < directions.Length; i++)
                ValidateLine(Plan(grid, units, anchor, FormationType.Line,
                    directions[i], 12), anchor, directions[i], true);
        }

        private static void ValidateOddAndTwoUnitShapes(GridManager grid,
            List<UnitSelection> units, Vector3 anchor)
        {
            List<UnitSelection> odd = units.Take(5).ToList();
            ValidateLine(Plan(grid, odd, anchor, FormationType.Line,
                Vector2.up, 12), anchor, Vector2.up, false);

            List<UnitSelection> pair = units.Take(2).ToList();
            Dictionary<FormationType, List<FormationAssignment>> pairPlans = new();
            foreach (FormationType type in Enum.GetValues(typeof(FormationType)))
            {
                List<FormationAssignment> plan = Plan(grid, pair, anchor, type,
                    Vector2.right, 12);
                ValidateCommon(plan, 2, anchor);
                pairPlans.Add(type, plan);
            }
            ValidateLine(pairPlans[FormationType.Line], anchor, Vector2.right, true);
            Require(Span(pairPlans[FormationType.Loose], Vector2.up)
                    > Span(pairPlans[FormationType.Compact], Vector2.up),
                "Two-unit Loose formation is not more spread out than Compact.");
            Require(Span(pairPlans[FormationType.Wedge], Vector2.right) > 0f,
                "Two-unit Wedge does not preserve a forward-oriented shape.");
        }

        private static void ValidateLargeLine(GridManager grid, Transform root,
            List<UnitSelection> units, List<UnitCombatData> definitions, Vector3 anchor)
        {
            while (units.Count < 20)
            {
                int index = units.Count;
                units.Add(Phase6G1RoleFormationValidation.CreateUnit(root,
                    UnitType.Monk, new Vector3(5f + index % 5, 16f + index / 5),
                    definitions));
            }
            List<FormationAssignment> large = Plan(grid, units, anchor,
                FormationType.Line, Vector2.up, 8);
            ValidateCommon(large, 20, anchor);
            ValidateLine(large, anchor, Vector2.up, true);
            Require(Span(large, Vector2.right) >= 19f - GeometryTolerance,
                "Twenty-unit Line was silently compressed or converted into rows.");
        }

        private static void ValidateObstacleCorrection(GridManager grid,
            IReadOnlyList<UnitSelection> units, Vector3 anchor, Vector2 forward)
        {
            Vector2Int center = grid.WorldToGrid(anchor);
            grid.SetOccupied(center.x, center.y, true);
            try
            {
                foreach (FormationType type in Enum.GetValues(typeof(FormationType)))
                {
                    List<FormationAssignment> corrected = Plan(grid, units.Take(10).ToList(),
                        anchor, type, forward, 12, true);
                    Require(corrected.All(item => item.Cell != null && item.Cell.Walkable
                            && !item.Cell.Occupied),
                        $"{type} obstacle correction retained a blocked cell.");
                }
            }
            finally
            {
                grid.SetOccupied(center.x, center.y, false);
            }
        }

        private static void AddRolePair(Transform root, List<UnitSelection> units,
            List<UnitCombatData> definitions, UnitType type, float y)
        {
            units.Add(Phase6G1RoleFormationValidation.CreateUnit(root, type,
                new Vector3(6f, y), definitions));
            units.Add(Phase6G1RoleFormationValidation.CreateUnit(root, type,
                new Vector3(8f, y), definitions));
        }

        private static List<float> DistinctDepths(
            List<FormationAssignment> plan, Vector2 axis)
            => plan.Select(item => Mathf.Round(
                    Vector2.Dot(item.DesiredPosition, axis) * 1000f) / 1000f)
                .Distinct().ToList();

        private static float Span(List<FormationAssignment> plan, Vector2 axis)
        {
            IEnumerable<float> values = plan.Select(item =>
                Vector2.Dot(item.DesiredPosition, axis));
            return values.Max() - values.Min();
        }

        private static void ValidateLateralSymmetry(
            List<FormationAssignment> row, Vector2 lateralAxis)
        {
            float center = row.Average(item =>
                Vector2.Dot(item.DesiredPosition, lateralAxis));
            List<float> offsets = row.Select(item =>
                    Vector2.Dot(item.DesiredPosition, lateralAxis) - center)
                .OrderBy(value => value).ToList();
            for (int i = 0; i < offsets.Count; i++)
                Require(Mathf.Abs(offsets[i] + offsets[offsets.Count - 1 - i])
                        <= GeometryTolerance,
                    "Wedge partial/full row is not laterally symmetric.");
        }

        private static Vector2 RightOf(Vector2 forward)
            => new(forward.y, -forward.x);

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
