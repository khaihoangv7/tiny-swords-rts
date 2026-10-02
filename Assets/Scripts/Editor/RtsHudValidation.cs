#if UNITY_EDITOR
using System;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.UI;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;

namespace TinyKingdom.EditorTools
{
    public static class RtsHudValidation
    {
        private const string WoodTable = "Assets/UI Elements/UI Elements/Wood Table/WoodTable.png";
        private const string Slot = "Assets/UI Elements/UI Elements/Wood Table/WoodTable_Slots.png";
        private const string Paper = "Assets/UI Elements/UI Elements/Papers/SpecialPaper.png";
        private const string Icons = "Assets/UI Elements/UI Elements/Icons/";
        private const string Avatars = "Assets/UI Elements/UI Elements/Human Avatars/";
        private const string HandCursor = "Assets/UI Elements/UI Elements/Cursors/Cursor_02.png";

        [MenuItem("Tools/Tiny Kingdom/Validate RTS HUD")]
        public static void Run()
        {
            Check(WoodTable, 448, 448, 9);
            Check(Slot, 192, 192, 1);
            Check(Paper, 320, 320, 9);
            foreach (int number in new[] { 1, 2, 3, 4, 5, 6, 9 })
                Check(Icons + $"Icon_{number:00}.png", 64, 64, 1);
            for (int number = 1; number <= 5; number++)
                Check(Avatars + $"Avatars_{number:00}.png", 256, 256, 1);

            RtsHudAssetCatalog catalog = RtsHudAssetCatalog.Load();
            Require(catalog != null && catalog.IsComplete,
                "RTS HUD catalog is missing or incomplete. Run Prepare RTS HUD.");
            Require(catalog.WoodTable.Length == 9 && catalog.SpecialPaper.Length == 9,
                "RTS HUD nine-piece panel references are incomplete.");
            RequireSprite(catalog.PopulationIcon, Icons + "Icon_01.png", "Icon_01_0");
            RequireSprite(catalog.WoodIcon, Icons + "Icon_02.png", "Icon_02_0");
            RequireSprite(catalog.GoldIcon, Icons + "Icon_03.png", "Icon_03_0");
            RequireSprite(catalog.FoodIcon, Icons + "Icon_04.png", "Icon_04_0");
            RequireSprite(catalog.AttackMoveIcon, Icons + "Icon_05.png", "Icon_05_0");
            RequireSprite(catalog.EjectAllIcon, Icons + "Icon_09.png", "Icon_09_0");
            RequireSprite(catalog.Avatar(UnitType.Warrior), Avatars + "Avatars_01.png", "Avatars_01_0");
            RequireSprite(catalog.Avatar(UnitType.Lancer), Avatars + "Avatars_02.png", "Avatars_02_0");
            RequireSprite(catalog.Avatar(UnitType.Archer), Avatars + "Avatars_03.png", "Avatars_03_0");
            RequireSprite(catalog.Avatar(UnitType.Monk), Avatars + "Avatars_04.png", "Avatars_04_0");
            RequireSprite(catalog.Avatar(UnitType.Pawn), Avatars + "Avatars_05.png", "Avatars_05_0");

            if (Application.isPlaying)
            {
                RtsHudController hud = UnityEngine.Object.FindAnyObjectByType<RtsHudController>();
                Require(hud != null && hud.IsReady, "Runtime RTS HUD is missing or incomplete.");
                Require(hud.ResourcePanel.Root.gameObject.activeInHierarchy,
                    "Resource panel must always remain visible during gameplay.");
                Require(RectMatches(hud.HudRoot, new Vector2(1258f, 160f))
                    && hud.HudRoot.anchorMin == Vector2.zero
                    && hud.HudRoot.anchorMax == Vector2.zero
                    && hud.HudRoot.pivot == Vector2.zero
                    && hud.HudRoot.anchoredPosition.y >= 8f,
                    "Bottom HUD root must be bottom-left anchored, 1258x160 and inside the viewport.");
                Require(RectMatches(hud.ResourcePanel.Root, new Vector2(220f, 160f))
                    && hud.ResourcePanel.Root.anchoredPosition.y >= 0f,
                    "Resource panel must be compact, baseline-aligned and fully on-screen.");
                Require(RectMatches(hud.SelectionPanel.Root, new Vector2(380f, 160f))
                    && RectMatches(hud.CommandPanel.Root, new Vector2(650f, 160f)),
                    "Selection and command panels do not match the compact HUD layout.");
                CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
                Require(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                    && scaler.referenceResolution == new Vector2(1920f, 1080f)
                    && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight
                    && Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f),
                    "RTS HUD CanvasScaler configuration is invalid.");
                RectTransform action = hud.CommandPanel.Root.Find("Action0") as RectTransform;
                Require(action != null && action.rect.width >= 52f && action.rect.width <= 60f
                    && action.rect.height >= 52f && action.rect.height <= 60f,
                    "Command button dimensions must remain within the intended 52-60 px range.");
                Require(CountNamed(hud.ResourcePanel.Root, "WoodTableSurface") == 1
                    && CountNamed(hud.SelectionPanel.Root, "SpecialPaperSurface") == 1
                    && CountNamed(hud.CommandPanel.Root, "SpecialPaperSurface") == 1,
                    "Each panel must have exactly one continuous surface background.");
                Require(hud.ResourcePanel.Root.Find("WoodTableSurface")
                        .GetComponent<Image>().raycastTarget
                    && hud.SelectionPanel.Root.Find("SpecialPaperSurface")
                        .GetComponent<Image>().raycastTarget
                    && hud.CommandPanel.Root.Find("SpecialPaperSurface")
                        .GetComponent<Image>().raycastTarget,
                    "Visible HUD panel surfaces must block world clicks underneath.");
                Require(!UnityEngine.Object.FindObjectsByType<UnitController>()
                        .Any(unit => unit != null && unit.Faction == Faction.Goblin),
                    "Goblin gameplay must remain disabled during the UI/UX phase.");
                SelectionManager selection = UnityEngine.Object.FindAnyObjectByType<SelectionManager>();
                Require(selection != null, "SelectionManager is missing.");
            }

            Debug.Log("RTS HUD validation passed: pixel imports, panel assets, resource icons, "
                + "unit avatars and authoritative presenter configuration verified. "
                + (Application.isPlaying ? "Runtime HUD is present and Goblins are disabled."
                    : "Run again in Play Mode for live panel checks."));
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6D.3 UI")]
        public static void RunPhase6D3()
        {
            RtsHudAssetCatalog catalog = RtsHudAssetCatalog.Load();
            Require(catalog != null && catalog.IsComplete,
                "RTS HUD catalog or one of its shared resource icons is missing.");

            UnitType[] requiredUnits =
            {
                UnitType.Pawn, UnitType.Warrior, UnitType.Archer,
                UnitType.Lancer, UnitType.Monk
            };
            UnitCombatData[] unitData = AssetDatabase.FindAssets("t:UnitCombatData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UnitCombatData>)
                .Where(item => item != null).ToArray();
            foreach (UnitType type in requiredUnits)
                Require(unitData.Count(item => item.UnitType == type) == 1,
                    $"Expected one authoritative UnitCombatData asset for {type}.");

            BuildingType[] requiredBuildings =
            {
                BuildingType.House, BuildingType.Barracks, BuildingType.Archery,
                BuildingType.Monastery, BuildingType.Tower
            };
            BuildingData[] buildingData = Resources.LoadAll<BuildingData>("Buildings");
            foreach (BuildingType type in requiredBuildings)
                Require(buildingData.Count(item => item != null && item.Type == type) == 1,
                    $"Expected one authoritative BuildingData asset for {type}.");

            Require(CombatDamage.Multiplier(UnitType.Warrior, UnitType.Archer) > 1f
                && CombatDamage.Multiplier(UnitType.Lancer, UnitType.Warrior) > 1f
                && CombatDamage.Multiplier(UnitType.Archer, UnitType.Lancer) > 1f,
                "Authoritative Warrior/Archer/Lancer matchup cycle is incomplete.");

            EventSystem[] eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>();
            Require(eventSystems.Length == 1,
                "Expected exactly one active EventSystem for command hover input.");

            GameCursorController cursor = UnityEngine.Object.FindAnyObjectByType<GameCursorController>();
            Require(cursor != null, "GameCursorController is missing from the active scene.");
            SerializedObject cursorObject = new(cursor);
            Texture2D handCursor = cursorObject.FindProperty("handCursor")?.objectReferenceValue
                as Texture2D;
            Require(handCursor != null && AssetDatabase.GetAssetPath(handCursor) == HandCursor,
                "GameCursorController Hand cursor reference is missing.");

            if (Application.isPlaying)
            {
                RtsHudController hud = UnityEngine.Object.FindAnyObjectByType<RtsHudController>();
                Require(hud != null && hud.IsReady, "Runtime RTS HUD is missing or incomplete.");
                Require(hud.CommandTooltip != null && hud.CommandTooltip.HasValidReferences,
                    "CommandTooltipPresenter hierarchy or text/cost references are incomplete.");
                Require(hud.CommandTooltip.UsesCompactAutoSizing
                    && hud.CommandTooltip.ContentRoot != null,
                    "Command tooltip must use non-stretched compact auto sizing with a Content root.");
                Require(hud.CommandTooltip.IsPointerFollowingConfigured,
                    "Command tooltip pointer following or screen-edge safety is not configured.");
                Require(!hud.CommandTooltip.BlocksRaycasts,
                    "Command tooltip graphics must not block pointer raycasts.");
                Require(UnityEngine.Object.FindObjectsByType<CommandTooltipPresenter>().Length == 1,
                    "Exactly one reusable CommandTooltipPresenter must be active.");
                Require(hud.CommandPanel != null
                    && hud.CommandPanel.TooltipTriggerCount == 12,
                    "Every reusable command button must own one tooltip trigger.");
                foreach (CommandTooltipTrigger trigger in hud.CommandPanel.Root
                    .GetComponentsInChildren<CommandTooltipTrigger>(false))
                    Require(trigger.HasTooltipData,
                        $"Active command button {trigger.name} has no tooltip data.");
            }

            Debug.Log("Phase 6D.3 UI validation passed: shared tooltip assets, authoritative "
                + "unit/building data, combat matchup source, Hand cursor and EventSystem "
                + "integrity verified. " + (Application.isPlaying
                    ? "Runtime tooltip hierarchy, raycasts and command triggers verified."
                    : "Run again in Play Mode for runtime tooltip hierarchy checks."));
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Building Presentation Hotfix")]
        public static void RunBuildingPresentationHotfix()
        {
            RtsHudAssetCatalog catalog = RtsHudAssetCatalog.Load();
            Require(catalog != null && catalog.IsComplete,
                "RTS HUD catalog is missing or incomplete. Run Prepare RTS HUD.");
            RequireSprite(catalog.AttackMoveIcon, Icons + "Icon_05.png", "Icon_05_0");
            RequireSprite(catalog.EjectAllIcon, Icons + "Icon_09.png", "Icon_09_0");
            Require(Mathf.Approximately(BuildingVisualPresentation.NormalBuildingScale, 1.7f),
                "Normal faction building visual scale must remain 1.7x.");
            Require(CastleDropOff.NavigationSidePadding == 1,
                "Main Castle must keep one navigation-clearance cell on each side.");

            foreach (BuildingData data in Resources.LoadAll<BuildingData>("Buildings"))
            {
                if (data == null || data.Type == BuildingType.GoldMine) continue;
                Vector2Int expected = data.Type switch
                {
                    BuildingType.House or BuildingType.Tower => new Vector2Int(2, 2),
                    BuildingType.Barracks or BuildingType.Archery
                        or BuildingType.Monastery => new Vector2Int(3, 3),
                    BuildingType.Castle => new Vector2Int(3, 2),
                    _ => data.Footprint
                };
                Require(data.Footprint == expected,
                    $"{data.name}: logical footprint changed unexpectedly.");
            }

            if (Application.isPlaying)
            {
                foreach (BuildingSite site in UnityEngine.Object.FindObjectsByType<BuildingSite>())
                {
                    SpriteRenderer visual = site.PresentationRenderer;
                    Require(site.transform.localScale == Vector3.one && visual != null
                        && visual.gameObject.name == "VisualRoot"
                        && Mathf.Approximately(visual.transform.localScale.x, 1.7f)
                        && Mathf.Approximately(visual.transform.localScale.y, 1.7f),
                        $"{site.name}: gameplay root or visual-only scale is invalid.");
                    BoxCollider2D hit = site.GetComponent<BoxCollider2D>();
                    Require(hit != null && Vector2.Distance(hit.size,
                        new Vector2(site.Footprint.x, site.Footprint.y)) < 0.01f,
                        $"{site.name}: collider no longer matches logical footprint.");
                    bool hasBuildingGroup = site.TryGetComponent(out SortingGroup buildingGroup);
                    ElevationSortingGroup buildingSorter = site.GetComponent<ElevationSortingGroup>();
                    Require(hasBuildingGroup && buildingGroup != null && buildingSorter != null
                        && buildingGroup.sortingOrder == buildingSorter.CalculatedOrder,
                        $"{site.name}: ground sorting group is missing.");
                    Transform progress = site.transform.Find("ConstructionProgressBar");
                    Require(progress == null || !progress.IsChildOf(visual.transform),
                        $"{site.name}: construction UI must not inherit visual scale.");
                }

                foreach (CastleDropOff castle in CastleDropOff.Active)
                {
                    SpriteRenderer visual = castle.PresentationRenderer;
                    BoxCollider2D hit = castle.GetComponent<BoxCollider2D>();
                    Require(visual != null && Mathf.Approximately(
                        visual.transform.localScale.x, 1.7f),
                        $"{castle.name}: Castle visual is not 1.7x.");
                    Require(hit != null && Vector2.Distance(hit.size, new Vector2(3f, 2f)) < 0.01f
                        && Vector2.Distance(hit.offset, new Vector2(0f, 0.5f)) < 0.01f,
                        $"{castle.name}: Castle collider must match its 3x2 footprint.");
                    GridManager navigation = UnityEngine.Object.FindAnyObjectByType<GridManager>();
                    Require(navigation != null, "GridManager is missing.");
                    Vector2Int origin = castle.NavigationFootprintOrigin;
                    Vector2Int size = castle.NavigationFootprint;
                    Require(size == new Vector2Int(5, 2),
                        $"{castle.name}: Castle navigation clearance must be 5x2.");
                    for (int y = 0; y < size.y; y++)
                    for (int x = 0; x < size.x; x++)
                        Require(navigation.GetCell(origin.x + x, origin.y + y)?.Occupied == true,
                            $"{castle.name}: Castle navigation clearance has an open cell.");
                }

                foreach (GoldMineObjective mine in GoldMineObjective.Active)
                    Require(mine.PresentationRenderer != null && Mathf.Approximately(
                        mine.PresentationRenderer.transform.localScale.x,
                        GoldMineObjective.VisualScale),
                        "Gold Mine must use its independent 2.60 presentation scale.");

                foreach (UnitController unit in UnitController.AllUnits)
                {
                    SortingGroup unitGroup = null;
                    bool hasUnitGroup = unit != null
                        && unit.TryGetComponent(out unitGroup);
                    ElevationSortingGroup unitSorter = unit != null
                        ? unit.GetComponent<ElevationSortingGroup>() : null;
                    Require(hasUnitGroup && unitGroup != null && unitSorter != null
                        && unitSorter.TrackMovement
                        && unitGroup.sortingOrder == unitSorter.CalculatedOrder,
                        $"{unit?.name}: unit ground sorting group is missing.");
                }
            }

            Debug.Log("Building presentation hotfix validation passed: Icon_05 AttackMove, "
                + "Icon_09 Eject All, 1.7x ground-anchored building visuals, unchanged logical "
                + "footprints, Y sorting and independent Gold Mine scale verified."
                + (Application.isPlaying ? " Runtime objects verified." :
                    " Run again in Play Mode for runtime object checks."));
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Selection And Unit Scale Polish")]
        public static void RunSelectionAndUnitScalePolish()
        {
            Require(Mathf.Approximately(UnitVisualPresentation.Scale, 1.6f),
                "Unit, Tree and Sheep presentation scale must remain 1.6x.");
            Require(Mathf.Approximately(GameCursorController.PresentationScale, 1.5f),
                "Gameplay cursors must retain their 1.5x pixel-art presentation scale.");
            Require(Mathf.Approximately(MoveCommandFeedback.PresentationScale, 1.35f),
                "Move command feedback must retain its 1.35x presentation scale.");
            Require(ResourceNode.DefaultInteractionOffsetFactor > 0f
                && ResourceNode.GoldInteractionOffsetFactor > 0f
                && ResourceNode.LargeGoldInteractionOffsetFactor < 0f,
                "Normal Gold Stones must be approached closely while Gold Stone 6 keeps extra clearance.");
            Require(PawnBuilder.RepairImpactDelay > 0f
                && PawnBuilder.RepairImpactDelay < PawnBuilder.WorkCycleSeconds,
                "Repair impact timing must occur inside the authoritative hammer cycle.");
            Require(BuildingSelectionPulse.SquashScaleY >
                    HitSquashStretchPresentation.DefaultSquashScaleY
                && BuildingSelectionPulse.StretchScaleY <
                    HitSquashStretchPresentation.DefaultStretchScaleY,
                "Building click pulse must remain gentler than unit damage reaction.");

            if (Application.isPlaying)
            {
                foreach (UnitController unit in UnitController.AllUnits)
                {
                    if (unit == null) continue;
                    SpriteRenderer body = unit.GetComponent<UnitAnimator>()?.VisualRenderer
                        ?? unit.GetComponent<PawnVisual>()?.VisualRenderer;
                    Require(unit.transform.localScale == Vector3.one && body != null
                        && Mathf.Approximately(body.transform.localScale.x, 1.6f)
                        && Mathf.Approximately(body.transform.localScale.y, 1.6f),
                        $"{unit.name}: gameplay root or 1.6x visual scale is invalid.");
                    UnitSelection selection = unit.GetComponent<UnitSelection>();
                    Transform ring = unit.transform.Find("SelectionIndicator");
                    Require(selection != null && ring != null
                        && Mathf.Approximately(ring.localPosition.y,
                            selection.MarkerYOffset),
                        $"{unit.name}: selection ring is not aligned to scaled feet.");
                }

                foreach (ResourceNode resource in ResourceNode.ActiveNodes)
                {
                    ResourceSelectionIndicator ring = null;
                    bool hasRing = resource != null && resource.TryGetComponent(out ring);
                    Require(hasRing && ring != null && ring.IsConfigured,
                        $"{resource?.name}: resource selection ring is missing.");
                    float expectedScale = resource.ResourceType == ResourceType.Gold
                        ? (resource.name.EndsWith("_6", System.StringComparison.Ordinal)
                            ? 1.55f : 1.4f)
                        : 1.6f;
                    Require(resource.PresentationRenderer != null
                        && Mathf.Approximately(resource.PresentationScale, expectedScale)
                        && Mathf.Approximately(
                            resource.PresentationRenderer.transform.localScale.x,
                            expectedScale),
                        $"{resource.name}: resource visual scale is not {expectedScale:0.##}x.");
                    float expectedRingY = resource.ResourceType switch
                    {
                        ResourceType.Wood => 0f,
                        ResourceType.Gold => -0.20f,
                        _ => -0.37f
                    };
                    Require(Mathf.Approximately(ring.LocalY, expectedRingY),
                        $"{resource.name}: selection ring ground offset is invalid.");
                }

                foreach (GoldMineObjective mine in GoldMineObjective.Active)
                {
                    ResourceSelectionIndicator ring = null;
                    bool hasRing = mine != null && mine.TryGetComponent(out ring);
                    Require(hasRing && ring != null && ring.IsConfigured,
                        $"{mine?.name}: Gold Mine selection ring is missing.");
                }

                foreach (BuildingSite site in UnityEngine.Object.FindObjectsByType<BuildingSite>())
                {
                    BuildingSelectionPulse pulse = null;
                    bool hasPulse = site != null && site.TryGetComponent(out pulse);
                    Require(hasPulse && pulse != null && pulse.IsConfigured,
                        $"{site?.name}: building click pulse is missing.");
                }

                foreach (CastleDropOff castle in CastleDropOff.Active)
                {
                    BuildingSelectionPulse pulse = null;
                    bool hasPulse = castle != null && castle.TryGetComponent(out pulse);
                    Require(hasPulse && pulse != null && pulse.IsConfigured,
                        $"{castle?.name}: Castle click pulse is missing.");
                }
            }

            Debug.Log("Selection and unit scale polish validation passed: 1.6x units/Trees/Sheep, "
                + "1.4x Gold Stones, 1.55x Gold Stone 6, aligned selection rings and gentle vertical building click "
                + "pulse verified."
                + (Application.isPlaying ? " Runtime objects verified." :
                    " Run again in Play Mode for runtime object checks."));
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6D.4 Interaction Polish")]
        public static void RunPhase6D4InteractionPolish()
        {
            PawnAnimationSet bluePawn = AssetDatabase.LoadAssetAtPath<PawnAnimationSet>(
                "Assets/UnitAnimations/BluePawn.asset");
            PawnAnimationSet redPawn = AssetDatabase.LoadAssetAtPath<PawnAnimationSet>(
                "Assets/UnitAnimations/RedPawn.asset");
            Require(bluePawn != null && bluePawn.Hammer.Length > 1,
                "Blue Pawn Hammer animation mapping is missing.");
            Require(redPawn != null && redPawn.Hammer.Length > 1,
                "Red Pawn Hammer animation mapping is missing.");
            PresentationAssetCatalog presentation = PresentationAssetCatalog.Load();
            Require(presentation != null && presentation.SheepIdle.Length == 6
                && presentation.SheepGrass.Length == 12
                && presentation.SheepMove.Length == 4
                && presentation.SheepIdle.Concat(presentation.SheepGrass)
                    .Concat(presentation.SheepMove).All(frame => frame != null
                        && frame.rect.width is >= 47f and <= 49f
                        && frame.rect.height is >= 42f and <= 49f),
                "Verified Sheep animation frames or their expected trimmed bounds changed.");

            GameCursorController cursor = UnityEngine.Object.FindAnyObjectByType<GameCursorController>();
            Require(cursor != null, "GameCursorController is missing from the active scene.");
            SerializedProperty hand = new SerializedObject(cursor).FindProperty("handCursor");
            Require(hand?.objectReferenceValue != null, "GameCursorController Hand cursor is missing.");
            SerializedObject cursorData = new(cursor);
            Require(cursorData.FindProperty("defaultHotspot").vector2Value == new Vector2(22f, 17f)
                && cursorData.FindProperty("handHotspot").vector2Value == new Vector2(24f, 18f)
                && cursorData.FindProperty("forbiddenHotspot").vector2Value == new Vector2(32f, 31f),
                "Cursor hotspots no longer match the verified 64x64 cursor artwork.");
            Require(UnityEngine.Object.FindAnyObjectByType<SelectionManager>() != null,
                "SelectionManager is missing from the active scene.");
            Require(cursor.HoverQueryUsesTriggers,
                "Cursor overlap query must include pointer-only trigger hit areas.");
            Require(PawnBuilder.InteractionArrivalTolerance >= 0.2f,
                "Builder arrival tolerance is not compatible with terminal separation offset.");

            if (Application.isPlaying)
            {
                foreach (CastleDropOff castle in CastleDropOff.Active)
                    ValidateSelectionHitArea(castle, castle.PresentationRenderer,
                        castle.GetComponent<BoxCollider2D>(), "Castle");

                int sheepCount = 0;
                int treeCount = 0;
                foreach (ResourceNode resource in ResourceNode.ActiveNodes)
                {
                    if (resource == null) continue;
                    ValidateSelectionHitArea(resource, resource.PresentationRenderer,
                        resource.GetComponent<BoxCollider2D>(), resource.ResourceType.ToString());
                    WorldSelectionHitArea resourceArea = resource
                        .GetComponentInChildren<WorldSelectionHitArea>(true);
                    if (resource.ResourceType == ResourceType.Food)
                        Require(resourceArea != null && Mathf.Approximately(
                            resourceArea.SelectionPadding, ResourceNode.SheepSelectionPadding)
                            && resourceArea.SelectionPadding is >= 0.05f and <= 0.12f,
                            $"{resource.name}: Sheep selection padding is missing or excessive.");
                    else Require(resourceArea != null
                            && Mathf.Approximately(resourceArea.SelectionPadding, 0f),
                        $"{resource.name}: Sheep-only selection padding leaked to another resource.");
                    Require(cursor.HoverQueryIncludesLayer(resource.gameObject.layer),
                        $"{resource.name}: resource layer is excluded from cursor overlap query.");
                    if (resource.ResourceType == ResourceType.Food) sheepCount++;
                    if (resource.ResourceType == ResourceType.Wood) treeCount++;
                }
                Require(sheepCount > 0, "No runtime Sheep resource exists for hit-area validation.");
                Require(treeCount > 0, "No runtime Tree resource exists for hit-area validation.");

                foreach (GoldMineObjective mine in GoldMineObjective.Active)
                    ValidateSelectionHitArea(mine, mine.PresentationRenderer,
                        mine.GetComponent<BoxCollider2D>(), "Gold Mine");

                foreach (BuildingSite site in UnityEngine.Object.FindObjectsByType<BuildingSite>())
                    ValidateSelectionHitArea(site, site.PresentationRenderer,
                        site.GetComponent<BoxCollider2D>(), site.Type.ToString());

                var pawnVisuals = new System.Collections.Generic.HashSet<SpriteRenderer>();
                foreach (UnitController unit in UnitController.AllUnits)
                {
                    if (unit?.GetComponent<HealthComponent>()?.CombatData?.UnitType
                        != UnitType.Pawn) continue;
                    PawnBuilder builder = unit.GetComponent<PawnBuilder>();
                    PawnVisual visual = unit.GetComponent<PawnVisual>();
                    Require(builder != null && visual != null && visual.AnimationSet != null
                        && visual.AnimationSet.Hammer.Length > 1,
                        $"{unit.name}: Pawn construction presentation is incomplete.");
                    Require(!builder.IsWorking || visual.IsBuildAnimationActive,
                        $"{unit.name}: active builder is not looping its Hammer animation.");
                    Require(visual.VisualRenderer != null
                        && visual.VisualRenderer.transform.parent == unit.transform
                        && pawnVisuals.Add(visual.VisualRenderer),
                        $"{unit.name}: PawnVisual does not own an independent visual renderer.");
                    if (builder.HasJob)
                        Debug.Log($"Builder {unit.name}#{unit.GetEntityId()}: "
                            + $"site={builder.Target?.name}, slot={builder.InteractionPosition}, "
                            + $"distance={builder.DistanceToInteraction:F3}, "
                            + $"atSlot={builder.IsAtInteraction}, working={builder.IsWorking}, "
                            + $"contributor={builder.IsWorking}, visual={visual.CurrentAnimationName}, "
                            + $"frame={visual.CurrentFrameIndex}, normalized={visual.CurrentFrameNormalizedTime:F2}, "
                            + $"loop={visual.IsLooping}, repairing={builder.IsRepairing}, "
                            + $"movement={unit.CurrentState}, moving={unit.GetComponent<UnitMovement>()?.IsMoving}");
                }

                foreach (BuildingSite site in UnityEngine.Object.FindObjectsByType<BuildingSite>())
                    Require(site.ReservedBuilderSlotCount >= site.ActiveBuilderCount,
                        $"{site.name}: assigned builders do not all own interaction slots.");
            }

            Debug.Log("Phase 6D.4 interaction polish validation passed: sprite-bound query-only "
                + "selection areas, unchanged gameplay colliders, Hand cursor assignment and "
                + "continuous active-builder Hammer presentation verified."
                + (Application.isPlaying ? " Runtime objects verified." :
                    " Run again in Play Mode for runtime hit-area and active-builder checks."));
        }

        private static void ValidateSelectionHitArea(Component target, SpriteRenderer visual,
            BoxCollider2D gameplayCollider, string label)
        {
            WorldSelectionHitArea area = target != null
                ? target.GetComponentInChildren<WorldSelectionHitArea>(true) : null;
            Require(area != null && area.TargetRoot == target.transform
                && area.HitCollider != null && area.HitCollider.enabled
                && area.HitCollider.isTrigger,
                $"{target?.name}: {label} query-only selection hit area is missing.");
            Require(area.gameObject.layer == target.gameObject.layer,
                $"{target?.name}: {label} selection hit area is on the wrong layer.");
            Require(gameplayCollider != null && !gameplayCollider.isTrigger,
                $"{target?.name}: {label} gameplay collider was replaced or changed to a trigger.");
            Require(area.Covers(visual),
                $"{target?.name}: {label} selection hit area does not cover its visual bounds.");
        }

        private static void Check(string path, int expectedWidth, int expectedHeight,
            int expectedSprites)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null && importer.textureType == TextureImporterType.Sprite,
                path + ": expected Sprite texture type.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Require(width == expectedWidth && height == expectedHeight,
                $"{path}: expected {expectedWidth}x{expectedHeight}, got {width}x{height}.");
            Require(importer.spriteImportMode == SpriteImportMode.Multiple
                && AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == expectedSprites,
                $"{path}: expected {expectedSprites} verified sprites.");
            Require(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.wrapMode == TextureWrapMode.Clamp,
                path + ": unsafe pixel-art import settings.");
            Require(importer.maxTextureSize >= Mathf.Max(width, height),
                path + ": source texture is downscaled.");
        }

        private static void RequireSprite(Sprite sprite, string path, string name)
            => Require(sprite != null && AssetDatabase.GetAssetPath(sprite) == path
                && sprite.name == name, $"Expected {name} from {path}.");

        private static bool RectMatches(RectTransform rect, Vector2 expected)
            => rect != null && Vector2.Distance(rect.rect.size, expected) < 0.01f;

        private static int CountNamed(RectTransform root, string name)
            => root.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name == name);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
