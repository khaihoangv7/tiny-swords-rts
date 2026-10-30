#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using TinyKingdom.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class PlayerBuildingOrdersValidation
    {
        private const string Pending = "TinyKingdom.PlayerBuildingOrdersValidation";
        private static double readySince;
        private static double playStarted;

        static PlayerBuildingOrdersValidation() => EditorApplication.update += Tick;

        [MenuItem("Tools/Tiny Kingdom/Validate Player Building Orders")]
        public static void RunBatch()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before starting validation.");
            EditorSceneManager.OpenScene("Assets/Scenes/Map.unity", OpenSceneMode.Single);
            readySince = 0;
            playStarted = 0;
            SessionState.SetBool(Pending, true);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    readySince = 0;
                    return;
                }
                if (readySince == 0) readySince = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - readySince > 5
                    && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.EnterPlaymode();
                return;
            }
            if (playStarted == 0) playStarted = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - playStarted < 5) return;
            SessionState.SetBool(Pending, false);
            try
            {
                Validate();
                Debug.Log("PLAYER_BUILDING_ORDERS_VALIDATION_PASSED");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                else EditorApplication.ExitPlaymode();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void Validate()
        {
            Require(GameInputBindings.Default(GameAction.EnterBuilding) == Key.H,
                "Enter Building must default to H + right click.");
            PlayerCommandController commands = UnityEngine.Object
                .FindAnyObjectByType<PlayerCommandController>();
            BuildingPlacementController placement = commands != null
                ? commands.GetComponent<BuildingPlacementController>() : null;
            SelectionManager selection = UnityEngine.Object
                .FindAnyObjectByType<SelectionManager>();
            BuildingSystem system = placement != null ? placement.System : null;
            GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            Require(commands != null && selection != null && system != null
                && grid != null && system.Owner == Faction.Blue,
                "Player building command authority is not configured for Blue.");

            UnitSelection[] pawns = UnitSelection.ActiveUnits
                .Where(unit => unit != null && unit.Controller.Faction == Faction.Blue
                    && unit.GetComponent<PawnBuilder>() != null).Take(2).ToArray();
            Require(pawns.Length == 2, "Two Blue Pawns are needed for validation.");
            Require(pawns[0].GetComponent<HealthComponent>()?.CombatData?.FoodCost == 70,
                "Pawn production must cost 70 Food.");
            PawnBuilder builder = pawns[0].GetComponent<PawnBuilder>();
            selection.ApplyClickSelection(pawns[0], false);
            BuildingSite site = null;
            var candidates = new List<PawnBuilder> { builder };
            for (int y = 8; y <= 27 && site == null; y++)
            for (int x = 5; x <= 28 && site == null; x++)
            {
                Vector2Int origin = new(x, y);
                if (system.CanPlace(BuildingType.Tower, origin))
                    system.TryBuild(BuildingType.Tower, origin, candidates, out site);
            }
            Require(site != null && site.State == BuildingState.UnderConstruction
                && builder.Target == site,
                "Could not establish an unfinished Tower with its first builder.");

            bool moved = false;
            for (int y = 8; y <= 20 && !moved; y++)
            for (int x = 5; x <= 20 && !moved; x++)
                moved = commands.IssueMoveCommand(grid.GridToWorld(x, y)) > 0;
            Require(moved && !builder.HasJob,
                "A different player move order did not release the first build job.");
            Require(commands.IssueBuildingCommand(site) > 0 && builder.Target == site
                && site.IsAssigned(builder),
                "Right-clicking the unfinished Tower did not resume construction.");

            builder.CancelJob();
            site.CompleteImmediatelyForTestSetup();
            site.TakeDamage(80f);
            BuildingGarrison towerGarrison = site.GetComponent<BuildingGarrison>();
            Require(towerGarrison != null && towerGarrison.IsReady
                && site.NeedsRepair, "Damaged Tower is not ready for both commands.");
            Require(commands.IssueFriendlyBuildingCommand(towerGarrison, false) > 0
                && builder.Target == site && builder.IsRepairing,
                "Normal right-click did not prioritize Tower repair for the Pawn.");
            Require(commands.IssueFriendlyBuildingCommand(towerGarrison, true) > 0
                && !builder.HasJob
                && pawns[0].GetComponent<UnitStationing>()?.IsPending == true,
                "Explicit Enter Building did not override repair and queue garrison.");
            pawns[0].GetComponent<UnitStationing>()?.CancelPending();

            selection.ApplyClickSelection(pawns[1], false);
            CastleDropOff castle = MatchManager.Instance?.BlueMainCastle;
            Require(castle != null && castle.ReceiveBuildingDamage(50)
                && castle.NeedsRepair, "Castle damage setup failed.");
            BuildingGarrison castleGarrison = castle.GetComponent<BuildingGarrison>();
            PawnBuilder castleBuilder = pawns[1].GetComponent<PawnBuilder>();
            Require(commands.IssueFriendlyBuildingCommand(castleGarrison, false) > 0
                && castleBuilder.RepairCastle == castle && castleBuilder.IsRepairing,
                "Normal right-click did not start Castle repair.");
            float before = castle.CurrentHP;
            Require(castle.Repair(PawnBuilder.WorkCycleSeconds) > 0f
                && castle.CurrentHP > before,
                "Castle repair did not restore authoritative HP.");
            castleBuilder.CancelJob();
            ValidatePendingGroupAndObserver(placement, system, grid, pawns);
        }

        private static void ValidatePendingGroupAndObserver(
            BuildingPlacementController placement, BuildingSystem system,
            GridManager grid, UnitSelection[] pawns)
        {
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            FogWorldVisibility presentation = FogWorldVisibility.Instance;
            Require(vision != null && presentation != null,
                "Fog systems are required for pending-plan validation.");
            ResourceStockpile stockpile = UnityEngine.Object
                .FindObjectsByType<ResourceStockpile>()
                .FirstOrDefault(resources => resources.Faction == Faction.Blue);
            stockpile?.Add(ResourceType.Wood, 100); // Play Mode fixture only.

            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = typeof(BuildingPlacementController);
            var selected = (List<PawnBuilder>)type.GetField("builders", flags)
                .GetValue(placement);
            var destinations = (Dictionary<PawnBuilder, Vector2Int>)type
                .GetField("pendingDestinations", flags).GetValue(placement);
            MethodInfo begin = type.GetMethod("BeginPending", flags);
            MethodInfo process = type.GetMethod("ProcessPending", flags);
            MethodInfo cancel = type.GetMethod("CancelPending", flags);
            PawnBuilder first = pawns[0].GetComponent<PawnBuilder>();
            PawnBuilder second = pawns[1].GetComponent<PawnBuilder>();
            Vector2Int center = grid.WorldToGrid(first.transform.position);
            bool groupMoved = false;
            Vector3 hiddenPosition = default;
            for (int radius = 4; radius <= 22 && !groupMoved; radius++)
            for (int y = center.y - radius; y <= center.y + radius && !groupMoved; y++)
            for (int x = center.x - radius; x <= center.x + radius && !groupMoved; x++)
            {
                if (Mathf.Max(Mathf.Abs(x - center.x), Mathf.Abs(y - center.y))
                    != radius) continue;
                Vector2Int origin = new(x, y);
                Vector3 firstCell = grid.GridToWorld(x, y);
                Vector3 lastCell = grid.GridToWorld(x + 1, y + 1);
                if (vision.IsVisible(Faction.Blue, firstCell)
                    || vision.IsVisible(Faction.Blue, lastCell)) continue;
                var candidates = new List<PawnBuilder> { first, second };
                if (!system.CanStartConstruction(BuildingType.House, origin,
                    candidates)) continue;
                selected.Clear();
                selected.AddRange(candidates);
                Require((bool)begin.Invoke(placement,
                    new object[] { BuildingType.House, HouseVariant.A, origin }),
                    "Could not create a pending construction order.");
                process.Invoke(placement, null);
                groupMoved = destinations.Count == 2
                    && destinations[first] != destinations[second]
                    && first.GetComponent<UnitMovement>().HasCommandDestination
                    && second.GetComponent<UnitMovement>().HasCommandDestination;
                hiddenPosition = firstCell;
                cancel.Invoke(placement, new object[] { true });
            }
            Require(groupMoved,
                "Selected Pawns did not receive distinct approach orders outside Fog.");

            bool observerWasEnabled = DebugObserverView.IsEnabled;
            try
            {
                DebugObserverView.SetEnabled(false);
                Require(!vision.IsVisible(Faction.Blue, hiddenPosition)
                    && !presentation.CanObserveResource(hiddenPosition),
                    "The hidden test location unexpectedly became visible.");
                DebugObserverView.SetEnabled(true);
                Require(presentation.CanObserveResource(hiddenPosition)
                    && !vision.IsVisible(Faction.Blue, hiddenPosition),
                    "Fog OFF should allow human inspection without changing team vision.");
            }
            finally { DebugObserverView.SetEnabled(observerWasEnabled); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
