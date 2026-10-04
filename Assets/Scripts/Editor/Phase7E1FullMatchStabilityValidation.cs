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
                data.Configure(7, 7, new Vector2(10000f, 10000f), 1f,
                    CreateTrapGrid());
                grid.SetNavigationData(data);

                var pawnObject = new GameObject("RedPawnInProspectivePocket");
                pawnObject.transform.SetParent(root.transform, false);
                pawnObject.transform.position = grid.GridToWorld(3, 3);
                pawnObject.AddComponent<SpriteRenderer>();
                UnitController pawn = pawnObject.AddComponent<UnitController>();
                pawn.Configure(Faction.Red);
                root.SetActive(true);

                var validator = new BuildingTrapValidator();
                Require(validator.WouldTrapFriendlyUnits(grid, Faction.Red,
                        new Vector2Int(3, 5), Vector2Int.one, out int trapped)
                    && trapped == 1,
                    "A candidate closing the only exit did not reject the trapped Pawn.");
                Require(!validator.WouldTrapFriendlyUnits(grid, Faction.Red,
                        new Vector2Int(2, 5), Vector2Int.one, out trapped)
                    && trapped == 0,
                    "A nearby candidate that leaves the exit open was over-rejected.");

                Debug.Log("Phase 7E.1 full-match stability validation PASS: shared "
                    + "hypothetical-footprint trap rejection, legal close placement, "
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
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static IReadOnlyList<NavigationCellData> CreateTrapGrid()
        {
            var cells = new List<NavigationCellData>(49);
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 7; x++)
            {
                bool corridor = x == 3 && y >= 3;
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
