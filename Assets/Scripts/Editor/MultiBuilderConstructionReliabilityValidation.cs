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
    public static class MultiBuilderConstructionReliabilityValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Multi-Builder Construction Reliability")]
        public static void Validate()
        {
            GameObject root = null;
            NavigationGridData navigation = null;
            BuildingData buildingData = null;
            Texture2D texture = null;
            Sprite sprite = null;
            try
            {
                ValidateContract();
                root = new GameObject("MultiBuilderConstructionValidation");
                root.SetActive(false);
                GridManager grid = root.AddComponent<GridManager>();
                PathfindingSystem paths = root.AddComponent<PathfindingSystem>();
                paths.SetGridManager(grid);
                navigation = ScriptableObject.CreateInstance<NavigationGridData>();
                navigation.Configure(9, 9, new Vector2(10000f, 10000f), 1f,
                    CreateGrid());
                grid.SetNavigationData(navigation);

                texture = new Texture2D(2, 2);
                sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f),
                    new Vector2(0.5f, 0.5f), 2f);
                buildingData = CreateBuildingData(sprite);
                var siteObject = new GameObject("ValidationSite");
                siteObject.transform.SetParent(root.transform, false);
                BuildingSite site = siteObject.AddComponent<BuildingSite>();
                Vector2Int origin = new(4, 4);
                site.Configure(buildingData, Faction.Red, origin, grid, null);

                PawnBuilder first = CreatePawn(root.transform, "PawnA", Faction.Red,
                    grid, paths, new Vector2Int(1, 4));
                PawnBuilder second = CreatePawn(root.transform, "PawnB", Faction.Red,
                    grid, paths, new Vector2Int(1, 5));
                UnitController scout = CreatePawn(root.transform, "PromotedScout",
                    Faction.Red, grid, paths, new Vector2Int(1, 6)).GetComponent<UnitController>();
                PawnScoutPromotion promotion = scout.gameObject.AddComponent<PawnScoutPromotion>();
                root.SetActive(true);
                typeof(PawnScoutPromotion).GetField("promoted",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(promotion, true);
                typeof(PawnScoutPromotion).GetField("controller",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(promotion, scout);
                Require(promotion.IsPromoted,
                    "Validation fixture failed to activate the promoted Scout state.");

                Require(site.TryReserveInteraction(first, out _)
                    && site.TryReserveInteraction(second, out _),
                    "Two reachable builders failed to reserve construction slots.");
                Require(site.TryGetBuilderReservation(first, out int firstSlot,
                        out Vector2Int firstCell, out _),
                    "The first builder has no authoritative site reservation.");
                Require(site.TryGetBuilderReservation(second, out int secondSlot,
                        out Vector2Int secondCell, out _),
                    "The second builder has no authoritative site reservation.");
                Require(firstSlot != secondSlot && firstCell != secondCell,
                    "Two Pawns own the same construction slot.");
                Require(site.ReservedBuilderSlotCount == 2,
                    "Site reservation count does not match distinct owners.");
                Require(site.TryReserveInteraction(first, out _)
                    && site.TryGetBuilderReservation(first, out int stableSlot,
                        out _, out _) && stableSlot == firstSlot
                    && site.ReservedBuilderSlotCount == 2,
                    "A repeated reservation changed the Pawn slot or duplicated ownership.");

                var seen = new HashSet<Vector2Int>();
                int slotCount = ConstructionSlotUtility.SlotCount(buildingData.Footprint);
                for (int slot = 0; slot < slotCount; slot++)
                {
                    Vector2Int cell = ConstructionSlotUtility.CellAt(origin,
                        buildingData.Footprint, slot);
                    Require(seen.Add(cell), "Generated construction slots are not unique.");
                    Require(!Contains(origin, buildingData.Footprint, cell),
                        "A construction slot is inside the building footprint.");
                    if (!ConstructionSlotUtility.IsValid(grid, origin,
                        buildingData.Footprint, slot)) continue;
                    GridCell navCell = grid.GetCell(cell.x, cell.y);
                    Require(TerrainRules.IsStableStandingCell(navCell)
                        && !TerrainRules.IsConnectorCorridor(navCell),
                        "A valid construction slot is blocked or inside a stairs corridor.");
                }
                Require(!ConstructionSlotUtility.IsValid(grid, origin,
                        buildingData.Footprint, 4),
                    "The stairs-adjacent perimeter cell was accepted as a work slot.");

                var roles = new CommanderPawnRoles();
                Require(roles.TryAssign(scout, CommanderPawnRole.Scout)
                    && !roles.TryAssign(scout, CommanderPawnRole.Builder),
                    "An active promoted Scout can be stolen by the Builder role.");

                Debug.Log("Multi-builder construction reliability validation PASS: "
                    + "unique site-owned reservations, stable re-reservation, perimeter "
                    + "geometry, walkable non-stairs slots, Builder/Scout isolation, "
                    + "command dedupe, hysteresis and bounded watchdog contracts are valid. "
                    + "Same-side crowding and x10 recovery still require Play Mode testing.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Multi-builder construction reliability validation FAILED: "
                    + exception);
                throw;
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (navigation != null) UnityEngine.Object.DestroyImmediate(navigation);
                if (buildingData != null) UnityEngine.Object.DestroyImmediate(buildingData);
                if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ValidateContract()
        {
            Require(PawnBuilder.InteractionArrivalTolerance > 0f
                && PawnBuilder.ExitWorkDistance
                    > PawnBuilder.InteractionArrivalTolerance,
                "Construction working hysteresis is invalid.");
            Require(PawnBuilder.SameSlotRecoveryAttempts > 0
                && PawnBuilder.RecoveryRetrySeconds > 0f
                && BuildingSite.ConstructionStallSeconds > 0f
                && BuildingSite.RecoveryCooldownSeconds > 0f,
                "Builder/site recovery is missing or unbounded.");
            Require(typeof(BuildingSite).GetField("interactionSlots",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType
                    == typeof(BuildingInteractionSlots),
                "BuildingSite is not the authoritative reservation owner.");
            Require(typeof(PawnBuilder).GetMethod("Assign",
                    BindingFlags.Instance | BindingFlags.Public) != null
                && typeof(BuildingSystem).GetMethod("TryAssign",
                    BindingFlags.Instance | BindingFlags.Public) != null
                && typeof(FactionCommandService).GetMethod("TryResumeConstruction",
                    BindingFlags.Instance | BindingFlags.Public) != null,
                "AI construction does not converge on the shared assignment path.");
            Require(typeof(FactionCommandService).GetProperty(
                    "DeduplicatedConstructionCommandCount",
                    BindingFlags.Static | BindingFlags.Public) != null,
                "Construction command dedupe instrumentation is missing.");
            Require(typeof(BuildingSite).GetProperty("LastProgressTime") != null
                && typeof(BuildingSite).GetProperty("RecoveryState") != null
                && typeof(BuildingSite).GetMethod("RecordBuilderPathRequest") != null
                && typeof(BuildingSite).GetMethod(
                    "ReportConstructionAccessFailure") != null,
                "Site progress watchdog or path instrumentation is missing.");
            var policy = new BuildingCommanderPolicy();
            Require(policy.MaximumConcurrentBuilders > 0,
                "AI has no bounded multi-builder capacity.");
        }

        private static IReadOnlyList<NavigationCellData> CreateGrid()
        {
            var cells = new List<NavigationCellData>(81);
            for (int y = 0; y < 9; y++)
            for (int x = 0; x < 9; x++)
                cells.Add(new NavigationCellData(x, y,
                    x == 3 && y == 4 ? TerrainType.Stairs : TerrainType.FlatGround, 0));
            return cells;
        }

        private static BuildingData CreateBuildingData(Sprite sprite)
        {
            BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
            var serialized = new SerializedObject(data);
            serialized.FindProperty("buildingType").enumValueIndex =
                (int)BuildingType.Barracks;
            serialized.FindProperty("footprint").vector2IntValue = new Vector2Int(2, 2);
            serialized.FindProperty("constructionSeconds").floatValue = 5f;
            serialized.FindProperty("maxHP").intValue = 100;
            serialized.FindProperty("blueSprite").objectReferenceValue = sprite;
            serialized.FindProperty("redSprite").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static PawnBuilder CreatePawn(Transform parent, string name,
            Faction faction, GridManager grid, PathfindingSystem paths,
            Vector2Int cell)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.position = grid.GridToWorld(cell.x, cell.y);
            obj.AddComponent<SpriteRenderer>();
            UnitController controller = obj.AddComponent<UnitController>();
            controller.Configure(faction);
            UnitMovement movement = obj.AddComponent<UnitMovement>();
            movement.Configure(grid, paths);
            obj.AddComponent<PawnGatherer>();
            return obj.AddComponent<PawnBuilder>();
        }

        private static bool Contains(Vector2Int origin, Vector2Int size,
            Vector2Int cell)
            => cell.x >= origin.x && cell.x < origin.x + size.x
                && cell.y >= origin.y && cell.y < origin.y + size.y;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
