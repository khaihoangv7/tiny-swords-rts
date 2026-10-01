using System;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinyKingdom.EditorTools
{
    // Run in an isolated editor: this smoke test issues real construction orders.
    [InitializeOnLoad]
    public static class BuildingTerraceValidation
    {
        private const string Pending = "TwoKingdoms.TerraceConstructionValidation";
        private static double readySince;
        private static double playStarted;
        private static BuildingSite[] sites;
        private static PawnBuilder[] builders;
        private static ResourceStockpile[] stockpiles;
        private static FactionPopulation[] populations;
        private static int[] startingWood;
        private static int[] startingCap;

        static BuildingTerraceValidation() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity");
            SessionState.SetBool(Pending, true);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    { readySince = 0; return; }
                    if (readySince == 0) readySince = EditorApplication.timeSinceStartup;
                    if (EditorApplication.timeSinceStartup - readySince > 8
                        && !EditorApplication.isPlayingOrWillChangePlaymode)
                        EditorApplication.EnterPlaymode();
                    return;
                }
                if (playStarted == 0) playStarted = EditorApplication.timeSinceStartup;
                double elapsed = EditorApplication.timeSinceStartup - playStarted;
                if (elapsed < 5) return;
                if (sites == null)
                {
                    BattlefieldMapValidation.ValidatePlayMode();
                    StartConstruction();
                    return;
                }
                GridManager grid = Object.FindAnyObjectByType<GridManager>();
                for (int side = 0; side < 2; side++)
                {
                    if (!builders[side].IsWorking) continue;
                    Vector2Int p = grid.WorldToGrid(builders[side].transform.position);
                    Require(grid.GetCell(p.x, p.y).HeightLevel == 2,
                        "A Pawn must climb to the upper court before building.");
                }
                if (sites.All(site => site != null && site.State == BuildingState.Completed))
                {
                    for (int side = 0; side < 2; side++)
                    {
                        Require(stockpiles[side].GetAmount(ResourceType.Wood)
                            == startingWood[side] - sites[side].Data.WoodCost,
                            "Construction must charge the House cost exactly once.");
                        Require(populations[side].Cap == startingCap[side] + sites[side].Data.PopulationBonus,
                            "Completed House must apply its population bonus.");
                    }
                    Debug.Log("TERRACE_CONSTRUCTION_VALIDATION_PASSED: both factions built Houses on level 2 using Pawns from level 1; placement, ramps, resource cost and population verified.");
                    Finish(0);
                }
                else Require(elapsed < 60, "Pawns did not complete their upper-court Houses in time.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(1);
            }
        }

        private static void StartConstruction()
        {
            GridManager grid = Object.FindAnyObjectByType<GridManager>();
            BuildingSystem blue = Object.FindAnyObjectByType<BuildingSystem>();
            stockpiles = new ResourceStockpile[2];
            populations = new FactionPopulation[2];
            startingWood = new int[2];
            startingCap = new int[2];
            builders = new PawnBuilder[2];
            sites = new BuildingSite[2];
            Vector2Int[] upper = { new(15, 15), new(50, 31) };
            Vector2Int[] lower = { new(9, 8), new(43, 24) };
            Vector2Int[] pawnStart = { new(17, 9), new(43, 25) };
            BuildingType[] types = { BuildingType.House, BuildingType.Barracks, BuildingType.Tower,
                BuildingType.Archery, BuildingType.Monastery };
            for (int side = 0; side < 2; side++)
            {
                Faction faction = side == 0 ? Faction.Blue : Faction.Red;
                stockpiles[side] = Object.FindObjectsByType<ResourceStockpile>().Single(s => s.Faction == faction);
                populations[side] = Object.FindObjectsByType<FactionPopulation>().Single(p => p.Faction == faction);
                BuildingSystem system = blue;
                if (side == 1)
                {
                    system = new GameObject("RedConstructionValidation").AddComponent<BuildingSystem>();
                    system.Configure(grid, stockpiles[side], populations[side], Resources.LoadAll<BuildingData>("Buildings"));
                }
                foreach (BuildingType type in types)
                {
                    Require(system.CanPlace(type, upper[side]), $"{faction} {type} rejected on level 2.");
                    Require(system.CanPlace(type, lower[side]), $"{faction} {type} rejected on level 1.");
                    Require(system.CanPlace(type, new(27, 18)), $"{type} rejected on flat ground.");
                }
                foreach (Vector2Int blocked in new Vector2Int[]
                    { new(6, 17), new(19, 16), new(12, 11), new(28, 37), new(11, 16), new(-1, 10) })
                    Require(!system.CanPlace(BuildingType.House, blocked),
                        "House accepted across heights, on a ramp/cliff/water, over the Castle or outside the map: " + blocked);
                builders[side] = Object.FindObjectsByType<PawnBuilder>()
                    .First(p => p.GetComponent<UnitController>().Faction == faction);
                builders[side].GetComponent<UnitMovement>().Stop();
                builders[side].transform.position = grid.GridToWorld(pawnStart[side].x, pawnStart[side].y);
                Physics2D.SyncTransforms();
                var candidates = new[] { builders[side] };
                Require(system.CanStartConstruction(BuildingType.House, upper[side], candidates),
                    "Construction preview rejected a reachable upper court.");
                startingWood[side] = stockpiles[side].GetAmount(ResourceType.Wood);
                startingCap[side] = populations[side].Cap;
                Require(system.TryBuild(BuildingType.House, upper[side], candidates, out sites[side]),
                    "Failed to issue the upper-court House order.");
                Require(!system.CanPlace(BuildingType.House, upper[side]),
                    "Another building can overlap the new construction site.");
            }
            Debug.Log("Terrace placement accepted on levels 0/1/2; both Pawns are climbing to build on their Castle courts.");
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(code);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
