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

namespace TinyKingdom.EditorTools
{
    public static class RtsHudValidation
    {
        private const string WoodTable = "Assets/UI Elements/UI Elements/Wood Table/WoodTable.png";
        private const string Slot = "Assets/UI Elements/UI Elements/Wood Table/WoodTable_Slots.png";
        private const string Paper = "Assets/UI Elements/UI Elements/Papers/SpecialPaper.png";
        private const string Icons = "Assets/UI Elements/UI Elements/Icons/";
        private const string Avatars = "Assets/UI Elements/UI Elements/Human Avatars/";

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
                    Require(hasBuildingGroup && buildingGroup != null
                        && buildingGroup.sortingOrder == 10,
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
                    Require(hasUnitGroup && unitGroup != null && unitGroup.sortingOrder >= 10,
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
