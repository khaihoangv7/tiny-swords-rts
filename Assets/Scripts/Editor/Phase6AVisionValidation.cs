using System;
using System.Collections.Generic;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase6AVisionValidation
    {
        private sealed class TestSource : IVisionSource
        {
            public Faction Faction { get; set; }
            public bool IsVisionActive { get; set; } = true;
            public float VisionRadius { get; set; }
            public Vector3 VisionPosition { get; set; }
            public int VisibleHeightAboveSource { get; set; }
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6A Vision")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 6A Vision validation in Play Mode so runtime sources and fog presentation are active.");

            ValidateGridModel();
            ValidateElevationGridModel();
            ValidateRuntime();
            Debug.Log("Phase 6A automated vision validation passed: faction isolation, "
                + "Visible->Explored memory, overlapping sources, Tower/Castle radii, "
                + "40x28 bounds, source rules, fog renderer and presentation gates.");
        }

        [MenuItem("Tools/Tiny Kingdom/Vision/Display Blue Fog (Debug)")]
        private static void DisplayBlue() => SetDisplayedFaction(Faction.Blue);

        [MenuItem("Tools/Tiny Kingdom/Vision/Display Red Fog (Debug)")]
        private static void DisplayRed() => SetDisplayedFaction(Faction.Red);

        [MenuItem("Tools/Tiny Kingdom/Vision/Toggle Fog Renderer")]
        private static void ToggleFog()
        {
            FogRenderer fog = UnityEngine.Object.FindAnyObjectByType<FogRenderer>();
            if (fog == null) throw new InvalidOperationException("FogRenderer is not active.");
            fog.SetFogVisible(!fog.IsFogVisible);
        }

        private static void SetDisplayedFaction(Faction faction)
        {
            FogRenderer fog = UnityEngine.Object.FindAnyObjectByType<FogRenderer>();
            if (fog == null) throw new InvalidOperationException("FogRenderer is not active.");
            fog.SetDisplayedFaction(faction);
            Debug.Log($"Fog debug display faction: {faction}.");
        }

        private static void ValidateGridModel()
        {
            var blueA = new TestSource
            {
                Faction = Faction.Blue,
                VisionRadius = 2f,
                VisionPosition = new Vector3(10.5f, 10.5f)
            };
            var model = new TeamVisionGrid(40, 28, Vector2.zero, 1f);
            model.Recompute(new IVisionSource[] { blueA });
            Require(model.GetState(Faction.Blue, new Vector2Int(10, 10)) == VisionState.Visible,
                "Blue source did not reveal Blue vision.");
            Require(model.GetState(Faction.Blue, new Vector2Int(12, 10)) == VisionState.Visible,
                "Circular vision did not include a cardinal cell on the radius.");
            Require(model.GetState(Faction.Blue, new Vector2Int(12, 12))
                == VisionState.Unexplored,
                "Vision uses a square footprint instead of Euclidean distance.");
            Require(model.GetState(Faction.Red, new Vector2Int(10, 10)) == VisionState.Unexplored,
                "Blue source leaked into Red vision.");

            var fractional = new TestSource
            {
                Faction = Faction.Red,
                VisionRadius = 3.5f,
                VisionPosition = new Vector3(10.5f, 10.5f)
            };
            model.Recompute(new IVisionSource[] { fractional });
            Require(model.GetState(Faction.Red, new Vector2Int(13, 11)) == VisionState.Visible,
                "Fractional radius 3.5 was truncated before the circle test.");
            Require(model.GetState(Faction.Red, new Vector2Int(13, 12))
                == VisionState.Unexplored,
                "Fractional circular vision included a cell outside radius 3.5.");

            var blueB = new TestSource
            {
                Faction = Faction.Blue,
                VisionRadius = 2f,
                VisionPosition = blueA.VisionPosition
            };
            model.Recompute(new IVisionSource[] { blueA, blueB });
            blueA.IsVisionActive = false;
            model.Recompute(new IVisionSource[] { blueA, blueB });
            Require(model.GetState(Faction.Blue, new Vector2Int(10, 10)) == VisionState.Visible,
                "Overlapping source removal incorrectly cleared a covered cell.");
            blueB.IsVisionActive = false;
            model.Recompute(new IVisionSource[] { blueA, blueB });
            Require(model.GetState(Faction.Blue, new Vector2Int(10, 10)) == VisionState.Explored,
                "A previously visible cell did not become Explored.");

            var tower = new TestSource
            {
                Faction = Faction.Blue,
                VisionRadius = 7f,
                VisionPosition = new Vector3(20.5f, 14.5f)
            };
            model.Recompute(new IVisionSource[] { tower });
            Require(model.GetState(Faction.Blue, new Vector2Int(26, 14)) == VisionState.Visible,
                "Tower radius 7 did not reveal its expected scouting region.");
            tower.IsVisionActive = false;
            model.Recompute(new IVisionSource[] { tower });
            Require(model.GetState(Faction.Blue, new Vector2Int(26, 14)) == VisionState.Explored,
                "Destroyed Tower vision did not fall back to Explored.");

            var edge = new TestSource
            {
                Faction = Faction.Red,
                VisionRadius = 8f,
                VisionPosition = new Vector3(39.5f, 27.5f)
            };
            model.Recompute(new IVisionSource[] { edge });
            Require(model.GetState(Faction.Red, new Vector2Int(39, 27)) == VisionState.Visible,
                "Top-right edge source failed.");
            _ = model.GetState(Faction.Red, new Vector2Int(-1, -1));
            _ = model.GetState(Faction.Red, new Vector2Int(40, 28));
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Elevation Vision Rules")]
        public static void ValidateElevationGridModel()
        {
            NavigationGridData navigation = ScriptableObject.CreateInstance<NavigationGridData>();
            try
            {
                var cells = new List<NavigationCellData>(8 * 5);
                for (int y = 0; y < 5; y++)
                for (int x = 0; x < 8; x++)
                {
                    int level = x >= 5 && y >= 3 ? 2 : x >= 3 && y >= 2 ? 1 : 0;
                    var cell = new NavigationCellData(x, y,
                        level == 0 ? TerrainType.FlatGround : TerrainType.ElevatedGround,
                        level);
                    if (x == 2 && y == 1) cell.heightConnection = new Vector2Int(3, 2);
                    else if (x == 3 && y == 2) cell.heightConnection = new Vector2Int(2, 1);
                    else if (x == 4 && y == 2) cell.heightConnection = new Vector2Int(5, 3);
                    else if (x == 5 && y == 3) cell.heightConnection = new Vector2Int(4, 2);
                    if (cell.heightConnection != default)
                    {
                        cell.terrainType = TerrainType.Stairs;
                        cell.hasHeightConnection = true;
                    }
                    cells.Add(cell);
                }
                navigation.Configure(8, 5, Vector2.zero, 1f, cells);
                var model = new TeamVisionGrid(8, 5, Vector2.zero, 1f, navigation);
                var source = new TestSource
                {
                    Faction = Faction.Blue,
                    VisionRadius = 7f,
                    VisionPosition = new Vector3(2.5f, 1.5f)
                };
                model.Recompute(new IVisionSource[] { source });
                Require(model.GetState(Faction.Blue, new Vector2Int(3, 2))
                    == VisionState.Visible, "Lower observer cannot see the upper stair entrance.");
                Require(model.GetState(Faction.Blue, new Vector2Int(3, 3))
                    == VisionState.Visible, "Stair glimpse did not reach the adjacent landing.");
                Require(model.GetState(Faction.Blue, new Vector2Int(4, 4))
                    == VisionState.Unexplored, "Stair glimpse revealed too much upper ground.");
                Require(model.GetState(Faction.Blue, new Vector2Int(7, 4))
                    == VisionState.Unexplored, "Lower observer revealed the remote top tier.");
                Require(model.GetState(Faction.Blue, new Vector2Int(5, 3))
                    == VisionState.Unexplored, "Lower observer revealed a stair two levels above.");
                Require(model.GetState(Faction.Red, new Vector2Int(3, 2))
                    == VisionState.Unexplored, "Elevation vision leaked across factions.");

                source.VisibleHeightAboveSource = 1; // Tower rule.
                model.Recompute(new IVisionSource[] { source });
                Require(model.GetState(Faction.Blue, new Vector2Int(4, 4))
                    == VisionState.Visible, "Tower did not reveal one higher level.");
                Require(model.GetState(Faction.Blue, new Vector2Int(5, 3))
                    == VisionState.Unexplored, "Tower revealed two higher levels.");

                source.VisibleHeightAboveSource = 0;
                source.VisionPosition = new Vector3(3.5f, 3.5f);
                model.Recompute(new IVisionSource[] { source });
                Require(model.GetState(Faction.Blue, new Vector2Int(5, 3))
                    == VisionState.Visible, "Level-one observer cannot see the next stair.");
                Require(model.GetState(Faction.Blue, new Vector2Int(7, 4))
                    == VisionState.Unexplored, "Level-one observer revealed level-two ground.");
                var elevation = new VisionElevationMap(8, 5, Vector2.zero, 1f,
                    navigation);
                float edgeCoverage = elevation.VisualCoverage(0, 0, 4, 4,
                    new Vector2(4.5f, 4.5f), out bool insideGlimpse);
                Require(edgeCoverage > 0f && edgeCoverage < 1f && !insideGlimpse,
                    "Upper stair fog edge is square instead of feathered.");
                Require(elevation.VisualCoverage(0, 0, 7, 4,
                        new Vector2(7.5f, 4.5f), out _) == 0f,
                    "Fog mask revealed the remote top tier.");
                Require(model.GetState(Faction.Blue, new Vector2Int(2, 1))
                    == VisionState.Visible, "Higher observer lost downward vision.");

                source.IsVisionActive = false;
                model.Recompute(new IVisionSource[] { source });
                Require(model.GetState(Faction.Blue, new Vector2Int(5, 3))
                    == VisionState.Explored, "Upper stair visibility did not become memory.");
                Debug.Log("Elevation vision grid validation passed: lower-level occlusion, "
                    + "stair glimpse, Tower +1, downward vision, faction isolation and memory.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(navigation);
            }
        }

        private static void ValidateRuntime()
        {
            TeamVisionSystem system = UnityEngine.Object.FindAnyObjectByType<TeamVisionSystem>();
            FogRenderer renderer = UnityEngine.Object.FindAnyObjectByType<FogRenderer>();
            FogWorldVisibility presentation = UnityEngine.Object.FindAnyObjectByType<FogWorldVisibility>();
            GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            Require(system != null && system.IsReady, "TeamVisionSystem is not ready.");
            Require(renderer != null && presentation != null, "Fog presentation components are missing.");
            Require(grid != null
                    && grid.Width == TinyKingdom.Terrain.BattlefieldLayout.Width
                    && grid.Height == TinyKingdom.Terrain.BattlefieldLayout.Height,
                "Vision must cover the complete authored logical grid.");
            Require(renderer.SamplesPerCell == FogRenderer.DefaultSamplesPerCell
                && renderer.MaskWidth == grid.Width * FogRenderer.DefaultSamplesPerCell
                && renderer.MaskHeight == grid.Height * FogRenderer.DefaultSamplesPerCell,
                "Fog visual mask must use the configured 4x high-resolution sampling.");
            Require(Mathf.Approximately(renderer.FeatherWidthCells,
                    FogRenderer.DefaultFeatherWidthCells)
                && renderer.MaskFilterMode == FilterMode.Point,
                "Fog visual mask feather/filter settings are invalid.");
            Require(Mathf.Approximately(system.UpdateInterval, 0.10f),
                "Vision update interval is not 0.10 seconds.");

            BuildingData[] data = Resources.LoadAll<BuildingData>("Buildings");
            Require(Radius(data, BuildingType.Tower) == 7f, "Tower VisionRadius must be 7.");
            Require(Radius(data, BuildingType.Castle) == 9f, "Castle VisionRadius must be 9.");
            foreach (BuildingType type in new[] { BuildingType.House, BuildingType.Barracks,
                         BuildingType.Archery, BuildingType.Monastery })
                Require(Radius(data, type) == 5f, $"{type} VisionRadius must be 5.");

            UnitCombatData[] units = AssetDatabase.FindAssets("t:UnitCombatData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UnitCombatData>)
                .Where(item => item != null).ToArray();
            (UnitType Type, float Radius)[] expectedUnitRadii =
            {
                (UnitType.Pawn, 3f),
                (UnitType.Warrior, 3.5f),
                (UnitType.Archer, 4f),
                (UnitType.Lancer, 3.5f)
            };
            foreach ((UnitType type, float radius) in expectedUnitRadii)
                Require(Mathf.Approximately(
                        units.Single(item => item.UnitType == type).VisionRadius, radius),
                    $"{type} VisionRadius must be {radius:0.0}.");

            system.RecomputeNow();
            presentation.RefreshNow();
            foreach (UnitController unit in UnitController.AllUnits)
            {
                if (unit == null) continue;
                UnitStationing stationing = unit.GetComponent<UnitStationing>();
                if (stationing != null && stationing.IsStationed)
                    Require(!unit.IsVisionActive,
                        "Internal or mounted occupant is incorrectly an independent vision source.");
                if (unit.Faction == presentation.PlayerViewFaction) continue;
                FogPresentationGate gate = unit.GetComponent<FogPresentationGate>();
                Require(gate != null, $"Enemy unit {unit.name} has no fog presentation gate.");
                Require(gate.IsPresented == system.IsVisible(presentation.PlayerViewFaction,
                        unit.transform.position),
                    $"Enemy unit presentation disagrees with TeamVisionSystem: {unit.name}.");
            }
            foreach (IVisionSource source in VisionSourceRegistry.ActiveSources)
            {
                if (source is not BuildingVisionSource building || building == null) continue;
                NavigationGridData navigation = grid.NavigationData;
                Vector2Int origin = building.Host.FootprintOrigin;
                Vector2Int size = building.Host.Footprint;
                Vector2 expectedCenter = navigation.WorldOrigin + new Vector2(
                    (origin.x + size.x * 0.5f) * navigation.CellSize,
                    (origin.y + size.y * 0.5f) * navigation.CellSize);
                Require(((Vector2)building.VisionPosition - expectedCenter).sqrMagnitude
                    < 0.0001f, $"Building vision is not centered on its footprint: {building.name}.");
                FogPresentationGate gate = building.GetComponent<FogPresentationGate>();
                Require(gate != null, $"Building {building.name} has no fog presentation gate.");
                if (building.Faction != presentation.PlayerViewFaction)
                    Require(gate.IsPresented == system.IsVisible(presentation.PlayerViewFaction,
                            building.VisionPosition),
                        $"Enemy building presentation disagrees with TeamVisionSystem: {building.name}.");
            }
        }

        private static float Radius(BuildingData[] data, BuildingType type)
        {
            BuildingData item = data.Single(candidate => candidate.Type == type);
            return item.VisionRadius;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
