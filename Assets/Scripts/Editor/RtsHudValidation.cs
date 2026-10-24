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
        private const string Swords = "Assets/UI Elements/UI Elements/Swords/Swords.png";
        private const string BigRibbons = "Assets/UI Elements/UI Elements/Ribbons/BigRibbons.png";
        private const string SmallRibbons = "Assets/UI Elements/UI Elements/Ribbons/SmallRibbons.png";
        private const string Icons = "Assets/UI Elements/UI Elements/Icons/";
        private const string Avatars = "Assets/UI Elements/UI Elements/Human Avatars/";
        private const string HandCursor = "Assets/UI Elements/UI Elements/Cursors/Cursor_02.png";
        private const string SelectionCorners =
            "Assets/UI Elements/UI Elements/Cursors/Cursor_04.png";

        [MenuItem("Tools/Tiny Kingdom/Validate RTS HUD")]
        public static void Run()
        {
            Check(WoodTable, 448, 448, 9);
            Check(Slot, 192, 192, 1);
            Check(Paper, 320, 320, 9);
            Check(Swords, 448, 640, 11);
            Check(BigRibbons, 448, 640, 15);
            Check(SmallRibbons, 320, 640, 30);
            Check(SelectionCorners, 128, 128, 4);
            foreach (int number in new[] { 1, 2, 3, 4, 5, 6, 9 })
                Check(Icons + $"Icon_{number:00}.png", 64, 64, 1);
            foreach (int number in new[] { 10, 11, 12 })
                Check(Icons + $"Icon_{number:00}.png", 64, 64, 1);
            for (int number = 1; number <= 5; number++)
                Check(Avatars + $"Avatars_{number:00}.png", 256, 256, 1);

            RtsHudAssetCatalog catalog = RtsHudAssetCatalog.Load();
            Require(catalog != null && catalog.IsComplete,
                "RTS HUD catalog is missing or incomplete. Run Prepare RTS HUD.");
            Require(catalog.WoodTable.Length == 9 && catalog.SpecialPaper.Length == 9,
                "RTS HUD nine-piece panel references are incomplete.");
            Require(catalog.Banner != null && catalog.Banner.Length == 9
                && catalog.Swords != null && catalog.SwordParts.Length == 11
                && catalog.BigRibbons.Length == 15
                && catalog.SmallRibbons.Length == 30
                && catalog.SelectionCorners.Length == 4
                && catalog.InfoIcon != null
                && catalog.MusicIcon != null && catalog.SettingsIcon != null,
                "Top HUD sword, Settings banner or top-right icons are missing.");
            ValidateSelectionCornerDepth();
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
                Rect safe = Screen.safeArea;
                Vector2 safeMin = new(safe.xMin / Mathf.Max(1f, Screen.width),
                    safe.yMin / Mathf.Max(1f, Screen.height));
                Vector2 safeTopLeft = new(safeMin.x,
                    safe.yMax / Mathf.Max(1f, Screen.height));
                Vector2 safeBottomRight = new(safe.xMax / Mathf.Max(1f, Screen.width),
                    safeMin.y);
                Require(Mathf.Approximately(hud.HudRoot.rect.height,
                        RtsHudController.HudHeight)
                    && hud.HudRoot.anchorMin == safeMin
                    && hud.HudRoot.anchorMax == safeBottomRight
                    && hud.HudRoot.pivot == Vector2.zero
                    && hud.HudRoot.anchoredPosition.y >= 8f,
                    "Bottom HUD root must stretch through the safe horizontal viewport.");
                Require(RectMatches(hud.ResourcePanel.Root, new Vector2(
                        ResourcePanelPresenter.PanelWidth, ResourcePanelPresenter.PanelHeight))
                    && hud.ResourcePanel.Root.anchoredPosition.y >= 0f,
                    "Resource panel must be readable, baseline-aligned and fully on-screen.");
                Require(RectMatches(hud.SelectionPanel.Root, new Vector2(
                        SelectionInfoPanelPresenter.PanelWidth,
                        SelectionInfoPanelPresenter.PanelHeight))
                    && hud.CommandPanel.Root.rect.width
                        is >= CommandPanelPresenter.MinimumPanelWidth
                    && hud.CommandPanel.Root.rect.width <= hud.HudRoot.rect.width
                    && Mathf.Approximately(hud.CommandPanel.Root.rect.height,
                        CommandPanelPresenter.MinimumPanelHeight),
                    "Selection and command panels do not match the adaptive HUD layout.");
                CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
                Require(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                    && scaler.referenceResolution == RtsHudController.ReferenceResolution
                    && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight
                    && Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f),
                    "RTS HUD CanvasScaler configuration is invalid.");
                RectTransform action = hud.CommandPanel.ContentRoot?.Find("Action0")
                    as RectTransform;
                Require(action != null
                    && action.rect.width is >= CommandPanelPresenter.MinimumButtonWidth
                        and <= CommandPanelPresenter.LargeButtonWidth
                    && action.rect.height is >= CommandPanelPresenter.MinimumButtonHeight
                        and <= CommandPanelPresenter.LargeButtonHeight,
                    "Command button dimensions are outside the Phase 6D.5 adaptive range.");
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
                SelectionManager selection = UnityEngine.Object.FindAnyObjectByType<SelectionManager>();
                Require(selection != null, "SelectionManager is missing.");
                Require(hud.transform.Find("MinimapBackground") == null
                    && hud.transform.Find("TopLeftHud/Sword/MapName")
                        ?.GetComponent<Text>() != null
                    && hud.transform.Find("TopLeftHud/Sword/MatchTimer")
                        ?.GetComponent<Text>() != null
                    && hud.transform.Find("GlobalQueue") != null
                    && hud.transform.Find("ControlGroups") != null,
                    "Sword, queue or control groups are missing, or minimap remains.");
                Require(hud.transform.Find("TopLeftHud/Sword/SwordBlade")
                        ?.GetComponent<Image>()?.type == Image.Type.Sliced
                    && hud.transform.Find("TopLeftHud/Sword/SwordTip") != null
                    && hud.transform.Find("GlobalQueue/QueuePaperSurface") == null
                    && hud.transform.Find("DebugMatchControls") == null,
                    "Sword pieces, frameless queue or Settings-only speed controls are invalid.");
                RectTransform groups = hud.transform.Find("ControlGroups")
                    as RectTransform;
                Require(groups != null && groups.anchorMin == safeTopLeft
                    && groups.Find("Group0") != null,
                    "Control group cards must be anchored on the left edge.");
                SettingsPanelPresenter settings = hud.GetComponent<SettingsPanelPresenter>();
                Require(settings != null && hud.transform.Find("SettingsIcons") != null,
                    "Settings presenter or top-right icons are missing.");
                settings.Open();
                try
                {
                    Require(settings.IsOpen && GameInputBindings.ModalOpen,
                        "Settings did not block gameplay input when opened.");
                    Transform panel = hud.transform.Find("SettingsModal/SettingsPanel");
                    Require(panel != null && panel.Find("TitleRibbon") != null
                        && panel.Find("Tab4") != null && panel.Find("Tab5") == null
                        && panel.Find("BannerSurface")?.GetComponent<Image>()?.type
                            == Image.Type.Simple,
                        "Settings must use faction ribbons and exactly five tabs.");
                }
                finally { settings.Close(); }
                Require(!settings.IsOpen && !GameInputBindings.ModalOpen,
                    "Settings did not release gameplay input when closed.");
            }

            Debug.Log("RTS HUD validation passed: pixel imports, panel assets, resource icons, "
                + "unit avatars and authoritative presenter configuration verified. "
                + (Application.isPlaying ? "Runtime HUD hierarchy is present."
                    : "Run again in Play Mode for live panel checks."));
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6D.5 HUD Scale")]
        public static void RunPhase6D5HudScale()
        {
            Require(CommandPanelPresenter.ActionViewCount == 14,
                "Phase 6D.5 must preserve fourteen reusable command actions.");
            Require(CommandPanelPresenter.FormationButtonCount == 4,
                "All four Phase 6G.2 formation actions must remain registered.");
            Require(Mathf.Approximately(CommandPanelPresenter.LargeButtonWidth, 104f)
                && Mathf.Approximately(CommandPanelPresenter.LargeButtonHeight, 112f)
                && Mathf.Approximately(CommandPanelPresenter.MinimumButtonWidth, 76f)
                && Mathf.Approximately(CommandPanelPresenter.MinimumButtonHeight, 82f),
                "Command card maximum/minimum dimensions are invalid.");
            Require(Mathf.Approximately(CommandPanelPresenter.LargeIconSize, 68f)
                && Mathf.Approximately(CommandPanelPresenter.MinimumIconSize, 48f)
                && CommandPanelPresenter.LargeLabelFontSize == 15
                && CommandPanelPresenter.MinimumLabelFontSize == 12
                && CommandPanelPresenter.LargeCostFontSize == 13
                && CommandPanelPresenter.MinimumCostFontSize == 10
                && CommandPanelPresenter.LargeHotkeyFontSize == 13
                && CommandPanelPresenter.MinimumHotkeyFontSize == 10,
                "Adaptive icon or text ranges are invalid.");

            float safeWidth = CommandPanelPresenter.CalculateMaximumSafePanelWidth(
                RtsHudController.HudSize.x,
                CommandPanelPresenter.FixedPanelLeftX);
            Require(Mathf.Approximately(safeWidth,
                    CommandPanelPresenter.MaximumPanelWidth),
                "Reference command safe width does not match HUD geometry.");
            Require(Mathf.Approximately(CommandPanelPresenter.FixedPanelLeftX,
                    224f + SelectionInfoPanelPresenter.PanelWidth + 4f),
                "Command panel no longer begins after the fixed Selection region.");
            foreach (int count in new[] { 1, 2, 5, 6, 7, 13, 14 })
            {
                Require(CommandPanelPresenter.CalculateRequiredRows(count, count) == 1
                    && CommandPanelPresenter.CalculateUsedColumns(count) == count,
                    $"{count} commands must use exactly one row.");
                float panelWidth = CommandPanelPresenter.CalculatePanelWidth(count,
                    safeWidth);
                float buttonWidth = CommandPanelPresenter.CalculateButtonWidth(count,
                    safeWidth);
                float requiredContent = count * buttonWidth
                    + Mathf.Max(0, count - 1)
                        * CommandPanelPresenter.HorizontalSpacing;
                Require(requiredContent <= panelWidth
                        - CommandPanelPresenter.HorizontalPadding * 2f + 0.01f,
                    $"{count} commands exceed the calculated inner content width.");
            }
            Require(Mathf.Approximately(CommandPanelPresenter.CalculatePanelHeight(1),
                    SelectionInfoPanelPresenter.PanelHeight)
                && Mathf.Approximately(CommandPanelPresenter.CalculatePanelHeight(14),
                    SelectionInfoPanelPresenter.PanelHeight),
                "Command panel height must remain stable for every action count.");
            Require(Mathf.Approximately(CommandPanelPresenter.CalculatePanelWidth(1,
                    safeWidth), CommandPanelPresenter.MinimumPanelWidth)
                && Mathf.Approximately(CommandPanelPresenter.CalculatePanelWidth(2,
                    safeWidth), 240f)
                && Mathf.Approximately(CommandPanelPresenter.CalculatePanelWidth(5,
                    safeWidth), 568f)
                && Mathf.Approximately(CommandPanelPresenter.CalculatePanelWidth(6,
                    safeWidth), 678f)
                && Mathf.Approximately(CommandPanelPresenter.CalculatePanelWidth(7,
                    safeWidth), 788f)
                && Mathf.Approximately(CommandPanelPresenter.CalculatePanelWidth(13,
                    safeWidth), safeWidth)
                && Mathf.Approximately(CommandPanelPresenter.CalculatePanelWidth(14,
                    safeWidth), safeWidth),
                "Representative one-row panel widths are invalid.");
            Require(Mathf.Approximately(CommandPanelPresenter.CalculateButtonWidth(7,
                    safeWidth), CommandPanelPresenter.LargeButtonWidth)
                && CommandPanelPresenter.CalculateButtonWidth(13, safeWidth)
                    < CommandPanelPresenter.LargeButtonWidth
                && CommandPanelPresenter.CalculateButtonWidth(14, safeWidth)
                    < CommandPanelPresenter.CalculateButtonWidth(13, safeWidth)
                && CommandPanelPresenter.CalculateButtonWidth(14, safeWidth)
                    >= CommandPanelPresenter.MinimumButtonWidth,
                "Cards must stay large through seven actions and shrink only when required.");
            Require(Mathf.Approximately(SelectionInfoPanelPresenter.PanelWidth, 380f)
                && Mathf.Approximately(SelectionInfoPanelPresenter.PanelHeight, 188f),
                "Selection outer dimensions changed during the content-only hotfix.");
            foreach (int count in new[] { 0, 2, 7, 13, 14 })
                Require(Mathf.Approximately(ResourcePanelPresenter.PanelWidth, 220f)
                        && Mathf.Approximately(ResourcePanelPresenter.PanelHeight, 188f)
                        && Mathf.Approximately(SelectionInfoPanelPresenter.PanelWidth, 380f)
                        && Mathf.Approximately(SelectionInfoPanelPresenter.PanelHeight, 188f),
                    $"Fixed Resource/Selection dimensions changed for the {count}-command fixture.");
            Require(SelectionInfoPanelPresenter.PortraitSize > 76f
                && SelectionInfoPanelPresenter.TitleFontSize > 20
                && SelectionInfoPanelPresenter.DetailFontSize > 14,
                "Selection portrait or typography was not enlarged.");
            Require(Mathf.Approximately(
                    SelectionInfoPanelPresenter.CalculateSummaryRowHeight(2), 54f)
                && Mathf.Approximately(
                    SelectionInfoPanelPresenter.CalculateSummaryIconSize(2), 52f)
                && SelectionInfoPanelPresenter.CalculateSummaryFontSize(2) == 21
                && Mathf.Approximately(
                    SelectionInfoPanelPresenter.CalculateSummaryRowHeight(4), 42f)
                && Mathf.Approximately(
                    SelectionInfoPanelPresenter.CalculateSummaryIconSize(4), 40f)
                && SelectionInfoPanelPresenter.CalculateSummaryFontSize(4) == 19,
                "Adaptive multi-selection content sizing is invalid.");
            Require(ResourcePanelPresenter.PanelHeight > 160f
                && ResourcePanelPresenter.IconSize > 28f
                && ResourcePanelPresenter.ValueFontSize > 18,
                "Resource panel, icons or values were not enlarged.");

            RtsHudAssetCatalog catalog = RtsHudAssetCatalog.Load();
            Require(catalog != null && catalog.IsComplete,
                "RTS HUD catalog is missing or incomplete.");

            if (Application.isPlaying)
            {
                RtsHudController hud = UnityEngine.Object.FindAnyObjectByType<RtsHudController>();
                Require(hud != null && hud.IsReady,
                    "Runtime RTS HUD is missing or incomplete.");
                Require(Mathf.Approximately(hud.HudRoot.rect.height,
                        RtsHudController.HudHeight)
                    && hud.HudRoot.anchorMin == Vector2.zero
                    && hud.HudRoot.anchorMax == new Vector2(1f, 0f)
                    && hud.HudRoot.pivot == Vector2.zero,
                    "RtsBottomHud must span the safe horizontal canvas region.");
                Require(RectMatches(hud.ResourcePanel.Root, new Vector2(
                        ResourcePanelPresenter.PanelWidth, ResourcePanelPresenter.PanelHeight)),
                    "Runtime Resources panel dimensions are invalid.");
                Require(RectMatches(hud.SelectionPanel.Root, new Vector2(
                        SelectionInfoPanelPresenter.PanelWidth,
                        SelectionInfoPanelPresenter.PanelHeight)),
                    "Runtime Selection panel dimensions are invalid.");
                Require(hud.ResourcePanel.Root.anchorMin == Vector2.zero
                        && hud.ResourcePanel.Root.anchorMax == Vector2.zero
                        && hud.ResourcePanel.Root.pivot == Vector2.zero
                        && hud.ResourcePanel.Root.anchoredPosition == Vector2.zero,
                    "Resources outer position must remain fixed at HUD x=0.");
                Require(hud.SelectionPanel.Root.anchorMin == Vector2.zero
                        && hud.SelectionPanel.Root.anchorMax == Vector2.zero
                        && hud.SelectionPanel.Root.pivot == Vector2.zero
                        && hud.SelectionPanel.Root.anchoredPosition
                            == new Vector2(224f, 0f),
                    "Selection outer position must remain fixed at HUD x=224.");

                RectTransform portrait = hud.SelectionPanel.Root.Find("Portrait")
                    as RectTransform;
                Text selectionTitle = hud.SelectionPanel.Root.Find("Title")
                    ?.GetComponent<Text>();
                Text selectionDetails = hud.SelectionPanel.Root.Find("Details")
                    ?.GetComponent<Text>();
                Require(portrait != null && RectMatches(portrait,
                        Vector2.one * SelectionInfoPanelPresenter.PortraitSize)
                    && selectionTitle != null
                    && selectionTitle.fontSize == SelectionInfoPanelPresenter.TitleFontSize
                    && selectionDetails != null
                    && selectionDetails.fontSize == SelectionInfoPanelPresenter.DetailFontSize,
                    "Runtime Selection portrait or text scale is invalid.");

                foreach (string rowName in new[] { "Population", "Wood", "Gold", "Food" })
                {
                    Transform row = hud.ResourcePanel.Root.Find(rowName);
                    RectTransform icon = row?.Find("Icon") as RectTransform;
                    Text value = row?.Find("Value")?.GetComponent<Text>();
                    Require(row != null && icon != null && RectMatches(icon,
                            Vector2.one * ResourcePanelPresenter.IconSize)
                        && value != null
                        && value.fontSize == ResourcePanelPresenter.ValueFontSize
                        && value.fontStyle == FontStyle.Bold
                        && value.rectTransform.rect.width >= 130f,
                        $"{rowName} resource row scale or typography is invalid.");
                }

                int activeActions = 0;
                RectTransform commandContent = hud.CommandPanel.ContentRoot;
                Require(commandContent != null
                    && commandContent.parent == hud.CommandPanel.Root
                    && commandContent.anchorMin == new Vector2(0f, 0f)
                    && commandContent.anchorMax == new Vector2(1f, 0f)
                    && commandContent.pivot == new Vector2(0.5f, 0f),
                    "Command content root is missing or parented outside its panel.");
                for (int i = 0; i < CommandPanelPresenter.ActionViewCount; i++)
                {
                    string actionName = "Action" + i;
                    Require(CountNamed(hud.CommandPanel.Root, actionName) == 1,
                        $"Expected exactly one reusable {actionName} object.");
                    RectTransform action = commandContent.Find(actionName)
                        as RectTransform;
                    RectTransform icon = action?.Find("Icon") as RectTransform;
                    Text label = action?.Find("Label")?.GetComponent<Text>();
                    Text cost = action?.Find("Cost")?.GetComponent<Text>();
                    Text hotkey = action?.Find("Hotkey")?.GetComponent<Text>();
                    CommandTooltipTrigger trigger = action
                        ?.GetComponent<CommandTooltipTrigger>();
                    Require(action != null && icon != null && label != null
                        && cost != null && hotkey != null && trigger != null,
                        $"{actionName} is missing an adaptive command-card element.");
                    if (action.gameObject.activeSelf) activeActions++;
                }

                Require(activeActions == hud.CommandPanel.VisibleActionCount,
                    "Active command hierarchy and adaptive visible-action count disagree.");
                if (activeActions > 0)
                {
                    int columns = CommandPanelPresenter.CalculateUsedColumns(activeActions);
                    int rows = CommandPanelPresenter.CalculateRequiredRows(
                        activeActions, columns);
                    float buttonWidth = hud.CommandPanel.CurrentButtonWidth;
                    float buttonHeight = hud.CommandPanel.CurrentButtonHeight;
                    Require(hud.CommandPanel.CurrentColumnCount == columns
                        && columns == activeActions
                        && hud.CommandPanel.CurrentRowCount == 1 && rows == 1
                        && Mathf.Approximately(hud.CommandPanel.CurrentPanelHeight,
                            CommandPanelPresenter.MinimumPanelHeight)
                        && buttonWidth is >= CommandPanelPresenter.MinimumButtonWidth
                            and <= CommandPanelPresenter.LargeButtonWidth
                        && buttonHeight is >= CommandPanelPresenter.MinimumButtonHeight
                            and <= CommandPanelPresenter.LargeButtonHeight,
                        "Runtime command panel did not preserve one-row adaptive geometry.");

                    float rowY = float.NaN;
                    float previousRight = float.NegativeInfinity;
                    for (int i = 0; i < activeActions; i++)
                    {
                        RectTransform action = commandContent.Find("Action" + i)
                            as RectTransform;
                        RectTransform icon = action.Find("Icon") as RectTransform;
                        Text label = action.Find("Label").GetComponent<Text>();
                        Text cost = action.Find("Cost").GetComponent<Text>();
                        Text hotkey = action.Find("Hotkey").GetComponent<Text>();
                        float expectedIcon = CommandPanelPresenter.CalculateIconSize(
                            buttonWidth);
                        Require(RectMatches(action, new Vector2(buttonWidth, buttonHeight))
                            && RectMatches(icon, Vector2.one * expectedIcon)
                            && label.fontSize == CommandPanelPresenter
                                .CalculateLabelFontSize(buttonWidth)
                            && cost.fontSize == CommandPanelPresenter
                                .CalculateCostFontSize(buttonWidth)
                            && hotkey.fontSize == CommandPanelPresenter
                                .CalculateHotkeyFontSize(buttonWidth),
                            $"Action{i} internals do not match the shared adaptive size.");
                        if (i == 0) rowY = action.anchoredPosition.y;
                        Require(Mathf.Approximately(action.anchoredPosition.y, rowY)
                            && action.anchoredPosition.x + 0.01f >= previousRight
                            && action.anchoredPosition.x >= -0.01f
                            && action.anchoredPosition.x + action.rect.width
                                <= commandContent.rect.width + 0.01f
                            && action.anchoredPosition.y >= -0.01f
                            && action.anchoredPosition.y + action.rect.height
                                <= commandContent.rect.height + 0.01f,
                            $"Action{i} is wrapped or overlaps another command.");
                        previousRight = action.anchoredPosition.x + action.rect.width;
                    }
                    Require(Mathf.Approximately(commandContent.rect.width,
                            hud.CommandPanel.Root.rect.width
                                - CommandPanelPresenter.HorizontalPadding * 2f)
                        && commandContent.rect.xMin >= hud.CommandPanel.Root.rect.xMin
                        && commandContent.rect.xMax <= hud.CommandPanel.Root.rect.xMax,
                        "Command content bounds are not contained by the current panel.");
                }

                RectTransform commandSurface = hud.CommandPanel.Root
                    .Find("SpecialPaperSurface") as RectTransform;
                Text commandTitle = hud.CommandPanel.Root.Find("Title")
                    ?.GetComponent<Text>();
                float panelLeft = CommandPanelPresenter.FixedPanelLeftX;
                float panelRight = panelLeft + hud.CommandPanel.Root.rect.width;
                Require(hud.CommandPanel.Root.anchorMin == Vector2.zero
                    && hud.CommandPanel.Root.anchorMax == Vector2.zero
                    && hud.CommandPanel.Root.pivot == Vector2.zero
                    && hud.CommandPanel.Root.anchoredPosition
                        == new Vector2(CommandPanelPresenter.FixedPanelLeftX, 0f)
                    && panelLeft >= -0.01f
                    && panelRight <= hud.HudRoot.rect.width + 0.01f
                    && commandSurface != null
                    && RectMatches(commandSurface, hud.CommandPanel.Root.rect.size)
                    && commandTitle != null
                    && commandTitle.rectTransform.anchorMin.x == 0.5f
                    && commandTitle.rectTransform.anchorMax.x == 0.5f,
                    "Adaptive command frame/raycast region does not follow its actual panel bounds.");
                Rect resourceBounds = RectInAncestor(hud.ResourcePanel.Root,
                    hud.HudRoot);
                Rect selectionBounds = RectInAncestor(hud.SelectionPanel.Root,
                    hud.HudRoot);
                Rect commandBounds = RectInAncestor(hud.CommandPanel.Root,
                    hud.HudRoot);
                Require(resourceBounds.xMax <= selectionBounds.xMin + 0.01f,
                    "Resources and Selection panel bounds overlap.");
                Require(selectionBounds.xMax <= commandBounds.xMin + 0.01f,
                    "Selection and Command panel bounds overlap.");
                foreach (string piece in new[]
                {
                    "Top", "Bottom", "Left", "Right", "TopLeft", "TopRight",
                    "BottomLeft", "BottomRight"
                })
                    Require(CountNamed(hud.CommandPanel.Root,
                            "SpecialPaper" + piece) == 1,
                        "Command decorative frame piece is missing or duplicated: " + piece);
                Require(hud.ResourcePanel.Root.Find("WoodTableSurface")
                        ?.GetComponent<Image>()?.raycastTarget == true
                    && hud.SelectionPanel.Root.Find("SpecialPaperSurface")
                        ?.GetComponent<Image>()?.raycastTarget == true
                    && hud.CommandPanel.Root.Find("SpecialPaperSurface")
                        ?.GetComponent<Image>()?.raycastTarget == true,
                    "HUD surfaces must retain world-input raycast blocking.");
                CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
                Require(scaler != null
                    && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                    && scaler.referenceResolution == RtsHudController.ReferenceResolution
                    && Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f),
                    "Phase 6D.5 must not change the global CanvasScaler policy.");
            }

            Debug.Log("Phase 6D.5 HUD Scale validation passed: one-row adaptive command "
                + "bar, 104x112 to 76x82 shared card range, larger Selection content, "
                + "188-unit Resources panel, fourteen-action pool, formation registration, "
                + "panel art and CanvasScaler policy verified."
                + (Application.isPlaying
                    ? " Runtime hierarchy and raycast blocking verified."
                    : " Run again in Play Mode for runtime hierarchy checks."));
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
                    && hud.CommandPanel.TooltipTriggerCount
                        == CommandPanelPresenter.ActionViewCount,
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
                    SelectionCursorMarker marker = unit.GetComponent<SelectionCursorMarker>();
                    Require(selection != null && marker != null && marker.IsConfigured
                        && Mathf.Approximately(marker.LocalY,
                            selection.MarkerYOffset),
                        $"{unit.name}: Cursor_04 marker is not aligned to scaled feet.");
                }

                foreach (ResourceNode resource in ResourceNode.ActiveNodes)
                {
                    SelectionCursorMarker marker = null;
                    bool hasMarker = resource != null && resource.TryGetComponent(out marker);
                    Require(hasMarker && marker != null && marker.IsConfigured,
                        $"{resource?.name}: resource Cursor_04 marker is missing.");
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
                    float expectedMarkerY = resource.ResourceType switch
                    {
                        ResourceType.Wood => 0f,
                        ResourceType.Gold => -0.20f,
                        _ => -0.37f
                    };
                    Require(Mathf.Approximately(marker.LocalY, expectedMarkerY),
                        $"{resource.name}: Cursor_04 ground offset is invalid.");
                }

                foreach (GoldMineObjective mine in GoldMineObjective.Active)
                {
                    SelectionCursorMarker marker = null;
                    bool hasMarker = mine != null && mine.TryGetComponent(out marker);
                    Require(hasMarker && marker != null && marker.IsConfigured,
                        $"{mine?.name}: Gold Mine Cursor_04 marker is missing.");
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
                + "1.4x Gold Stones, 1.55x Gold Stone 6, aligned Cursor_04 markers and gentle vertical building click "
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
                    Require(!builder.IsWorking || visual.IsBuildFeedbackActive,
                        $"{unit.name}: active builder has no one-shot Hammer feedback state.");
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
                            + $"loop={visual.IsLooping}, swing={visual.BuildSwingRemaining:F2}, "
                            + $"recovery={visual.BuildRecoveryRemaining:F2}, "
                            + $"queued={visual.HasQueuedBuildSwing}, repairing={builder.IsRepairing}, "
                            + $"movement={unit.CurrentState}, moving={unit.GetComponent<UnitMovement>()?.IsMoving}");
                }

                foreach (BuildingSite site in UnityEngine.Object.FindObjectsByType<BuildingSite>())
                    Require(site.ReservedBuilderSlotCount >= site.ActiveBuilderCount,
                        $"{site.name}: assigned builders do not all own interaction slots.");
            }

            Debug.Log("Phase 6D.4 interaction polish validation passed: sprite-bound query-only "
                + "selection areas, unchanged gameplay colliders, Hand cursor assignment and "
                + "work-event Hammer swing/recovery presentation verified."
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

        private static void ValidateSelectionCornerDepth()
        {
            var root = new GameObject("TemporarySelectionCornerValidation");
            try
            {
                SpriteRenderer body = root.AddComponent<SpriteRenderer>();
                body.sortingOrder = 9;
                SelectionCursorMarker marker =
                    root.AddComponent<SelectionCursorMarker>();
                const float expectedCenterX = 0.35f;
                Require(marker.Configure(3f, 2f, 0f, body, 1f, true,
                    expectedCenterX),
                    "Building selection corners could not be configured.");
                SpriteRenderer[] corners = root.GetComponentsInChildren<SpriteRenderer>();
                Require(corners.Length == 5,
                    "Building selection marker must contain four corners.");
                for (int i = 0; i < 4; i++)
                {
                    SpriteRenderer corner = root.transform
                        .Find("SelectionCursorCorner" + i)?.GetComponent<SpriteRenderer>();
                    Require(corner != null && corner.sortingOrder ==
                        body.sortingOrder + (i < 2 ? -1 : 1),
                        "Rear corners must draw behind and front corners ahead of the building.");
                }
                Require(root.transform.Find("SelectionCursorCorner0").localPosition.y
                    > root.transform.Find("SelectionCursorCorner2").localPosition.y,
                    "Rear corners must span the back edge of the building footprint.");
                SpriteRenderer left = root.transform.Find("SelectionCursorCorner0")
                    .GetComponent<SpriteRenderer>();
                SpriteRenderer right = root.transform.Find("SelectionCursorCorner1")
                    .GetComponent<SpriteRenderer>();
                float leftEdge = left.transform.localPosition.x
                    - left.sprite.pivot.x / left.sprite.pixelsPerUnit;
                float rightEdge = right.transform.localPosition.x
                    + (right.sprite.rect.width - right.sprite.pivot.x)
                        / right.sprite.pixelsPerUnit;
                Require(Mathf.Abs((leftEdge + rightEdge) * 0.5f
                    - expectedCenterX) < 0.001f,
                    "Selection corners must center on the building visual, including sprite pivots.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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

        private static Rect RectInAncestor(RectTransform rect, RectTransform ancestor)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 first = ancestor.InverseTransformPoint(corners[0]);
            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 local = ancestor.InverseTransformPoint(corners[i]);
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, local.y);
                maxY = Mathf.Max(maxY, local.y);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

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
