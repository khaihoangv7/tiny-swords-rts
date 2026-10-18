#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase7E1FullMatchStabilityValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.1 Full-Match Stability")]
        public static void Validate()
        {
            GameObject root = null;
            NavigationGridData data = null;
            UnitController pawn = null;
            List<UnitController> fixtureWorldUnits = null;
            try
            {
                Require(BuildingTrapValidator.ConnectivityMargin >= 4,
                    "Building trap validation region is too small.");
                Require(typeof(BuildingSystem).GetField("trapValidator",
                        BindingFlags.Instance | BindingFlags.NonPublic) != null,
                    "BuildingSystem is not using the shared trap validator.");
                Require(typeof(FactionCommandService).GetMethod("TryBuild",
                        BindingFlags.Instance | BindingFlags.Public) != null
                    && typeof(BuildingSystem).GetMethod("CanPlace",
                        BindingFlags.Instance | BindingFlags.Public) != null,
                    "AI and player placement must converge on BuildingSystem.CanPlace.");
                Require(typeof(FactionCommandService).GetMethod("TryReturnResources",
                        BindingFlags.Instance | BindingFlags.Public) != null,
                    "AI cargo recovery is not exposed through FactionCommandService.");
                Require(PawnGatherer.CargoRecoveryTimeoutSeconds > 0f
                    && PawnGatherer.DropOffRetryBaseSeconds > 0f
                    && PawnGatherer.DropOffRetryMaximumSeconds
                        >= PawnGatherer.DropOffRetryBaseSeconds,
                    "Cargo watchdog/retry timing is invalid or unbounded.");
                Require(Enum.IsDefined(typeof(PathfindingRequestSource),
                        PathfindingRequestSource.EconomyReturn)
                    && Enum.IsDefined(typeof(PathfindingRequestSource),
                        PathfindingRequestSource.CommanderScout),
                    "Required x10 path request categories are missing.");

                root = new GameObject("Phase7E1_FullMatchStability_Validation");
                root.SetActive(false);
                GridManager grid = root.AddComponent<GridManager>();
                data = ScriptableObject.CreateInstance<NavigationGridData>();
                data.Configure(21, 21, new Vector2(10000f, 10000f), 1f,
                    CreateTrapGrid());
                grid.SetNavigationData(data);

                var pawnObject = new GameObject("RedPawnInProspectivePocket");
                pawnObject.transform.SetParent(root.transform, false);
                pawnObject.transform.position = grid.GridToWorld(10, 9);
                pawnObject.AddComponent<SpriteRenderer>();
                pawn = pawnObject.AddComponent<UnitController>();
                pawn.Configure(Faction.Red);
                root.SetActive(true);
                // Edit Mode batch execution does not call the normal runtime
                // registration hook, so register this fixture explicitly.
                fixtureWorldUnits = typeof(UnitController).GetField("RegisteredWorldUnits",
                    BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
                    as List<UnitController>;
                Require(fixtureWorldUnits != null,
                    "Could not register the synthetic Pawn in Edit Mode.");
                if (!fixtureWorldUnits.Contains(pawn)) fixtureWorldUnits.Add(pawn);

                var validator = new BuildingTrapValidator();
                bool rejectsOccupiedPocket = validator.WouldTrapFriendlyUnits(grid,
                    Faction.Red, new Vector2Int(10, 12), Vector2Int.one,
                    out int trapped, out int occupiedPocketCells);
                Require(rejectsOccupiedPocket && trapped == 1,
                    "A candidate closing the only exit did not reject the trapped Pawn: "
                    + $"registered={UnitController.ActiveUnits.Count}, "
                    + $"cell={grid.WorldToGrid(pawn.transform.position)}, "
                    + $"trapped={trapped}, sealed={occupiedPocketCells}.");
                Require(!validator.WouldTrapFriendlyUnits(grid, Faction.Red,
                        new Vector2Int(9, 12), Vector2Int.one, out trapped)
                    && trapped == 0,
                    "A nearby candidate that leaves the exit open was over-rejected.");

                // The same gap must be rejected before production puts a unit in it.
                fixtureWorldUnits.Remove(pawn);
                UnityEngine.Object.DestroyImmediate(pawnObject);
                Require(!validator.WouldTrapFriendlyUnits(grid, Faction.Red,
                        new Vector2Int(10, 12), Vector2Int.one,
                        out trapped, out int sealedCells)
                    && trapped == 0 && sealedCells == 4,
                    "An empty future spawn pocket was not detected.");
                Require(!validator.WouldTrapFriendlyUnits(grid, Faction.Red,
                        new Vector2Int(9, 12), Vector2Int.one,
                        out trapped, out sealedCells)
                    && sealedCells == 0,
                    "A candidate that leaves the spawn exit open was over-rejected.");
                Require(!validator.WouldTrapFriendlyUnits(grid, Faction.Red,
                        new Vector2Int(20, 12), Vector2Int.one,
                        out trapped, out sealedCells)
                    && sealedCells == 4,
                    "The map edge was incorrectly treated as an exit from a pocket.");
                grid.SetOccupied(10, 12, true);
                Require(!validator.WouldTrapFriendlyUnits(grid, Faction.Red,
                        new Vector2Int(10, 13), Vector2Int.one,
                        out trapped, out sealedCells)
                    && sealedCells == 0,
                    "A pre-existing disconnected pocket was retroactively rejected.");

                Debug.Log("Phase 7E.1 full-match stability validation PASS: shared "
                    + "hypothetical-footprint trap and empty-spawn-pocket rejection, "
                    + "legal close placement, "
                    + "bounded cargo recovery and x10 diagnostic categories are configured. "
                    + "Runtime FPS and long-match behavior still require Play Mode profiling.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 7E.1 full-match stability validation FAILED: "
                    + exception);
                throw;
            }
            finally
            {
                if (fixtureWorldUnits != null && pawn != null)
                    fixtureWorldUnits.Remove(pawn);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static IReadOnlyList<NavigationCellData> CreateTrapGrid()
        {
            var cells = new List<NavigationCellData>(21 * 21);
            for (int y = 0; y < 21; y++)
            for (int x = 0; x < 21; x++)
            {
                bool corridor = (x == 10 || x == 20) && y >= 8;
                cells.Add(new NavigationCellData(x, y,
                    corridor ? TerrainType.FlatGround : TerrainType.Water, 0));
            }
            return cells;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
