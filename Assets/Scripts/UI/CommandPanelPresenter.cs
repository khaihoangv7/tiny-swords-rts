using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Match;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.UI;
using TinyKingdom.Audio;
using TinyKingdom.Presentation;

namespace TinyKingdom.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class CommandPanelPresenter : MonoBehaviour
    {
        private static readonly (BuildingType type, HouseVariant variant, string name)[] BuildActions =
        {
            (BuildingType.House, HouseVariant.A, "House A"),
            (BuildingType.House, HouseVariant.B, "House B"),
            (BuildingType.House, HouseVariant.C, "House C"),
            (BuildingType.Barracks, HouseVariant.A, "Barracks"),
            (BuildingType.Tower, HouseVariant.A, "Tower"),
            (BuildingType.Archery, HouseVariant.A, "Archery"),
            (BuildingType.Monastery, HouseVariant.A, "Monastery")
        };
        private static readonly UnitType[] TrainActions =
        {
            UnitType.Pawn, UnitType.Warrior, UnitType.Lancer,
            UnitType.Archer, UnitType.Monk
        };
        private static readonly FormationType[] FormationActions =
        {
            FormationType.Compact, FormationType.Wedge,
            FormationType.Loose, FormationType.Line
        };
        private static readonly Vector2[] CompactGlyphPoints =
        {
            new(-17f, 10f), new(0f, 10f), new(17f, 10f),
            new(-17f, -10f), new(0f, -10f), new(17f, -10f)
        };
        private static readonly Vector2[] WedgeGlyphPoints =
        {
            new(0f, 20f), new(-11f, 3f), new(11f, 3f),
            new(-21f, -16f), new(0f, -16f), new(21f, -16f)
        };
        private static readonly Vector2[] LooseGlyphPoints =
        {
            new(-24f, 17f), new(24f, 16f), Vector2.zero,
            new(-23f, -17f), new(23f, -18f)
        };
        private static readonly Vector2[] LineGlyphPoints =
        {
            new(-26f, 0f), new(-13f, 0f), Vector2.zero,
            new(13f, 0f), new(26f, 0f)
        };

        public const int ActionViewCount = 14;
        public const int FormationButtonCount = 4;
        public const float MinimumPanelWidth = 240f;
        public const float MaximumPanelWidth = 1288f;
        public const float PanelWidth = MaximumPanelWidth;
        public const float MinimumPanelHeight = 188f;
        public const float MaximumPanelHeight = MinimumPanelHeight;
        public const float LargeButtonWidth = 104f;
        public const float LargeButtonHeight = 112f;
        public const float MinimumButtonWidth = 76f;
        public const float MinimumButtonHeight = 82f;
        public const float ButtonWidth = LargeButtonWidth;
        public const float ButtonHeight = LargeButtonHeight;
        public const float LargeIconSize = 68f;
        public const float MinimumIconSize = 48f;
        public const float IconWidth = LargeIconSize;
        public const float IconHeight = LargeIconSize;
        public const int LargeLabelFontSize = 15;
        public const int MinimumLabelFontSize = 12;
        public const int LargeCostFontSize = 13;
        public const int MinimumCostFontSize = 10;
        public const int LargeHotkeyFontSize = 13;
        public const int MinimumHotkeyFontSize = 10;
        public const int LabelFontSize = LargeLabelFontSize;
        public const int CostFontSize = LargeCostFontSize;
        public const int HotkeyFontSize = LargeHotkeyFontSize;
        public const float HorizontalPadding = 12f;
        public const float HorizontalSpacing = 6f;
        // Resource occupies x=0..220 and Selection x=224..604. Command owns
        // the independent region beginning after the original four-unit gap.
        public const float FixedPanelLeftX = 608f;
        private const float TitleBandHeight = 32f;
        public static readonly Color FormationActiveColor =
            new(0.72f, 0.94f, 0.62f, 1f);

        private sealed class ActionView
        {
            internal Button Button;
            internal Image Background;
            internal Image Icon;
            internal Text Label;
            internal Text Cost;
            internal Text Hotkey;
            internal GuardButtonHold GuardHold;
            internal CommandTooltipTrigger Tooltip;
            internal RectTransform FormationGlyph;
            internal Image[] FormationDots;
            internal FormationType? Formation;
        }

        private readonly ActionView[] views = new ActionView[ActionViewCount];
        private SelectionManager selection;
        private PlayerCommandController commands;
        private GuardInputController guard;
        private BuildingSystem buildingSystem;
        private BuildingPlacementController placement;
        private ResourceStockpile stockpile;
        private RtsHudAssetCatalog assets;
        private Sprite cannonIcon;
        private CommandTooltipPresenter tooltip;
        private RectTransform root;
        private RectTransform content;
        private Text title;
        private float refreshAt;
        private int lastVisibleCount = -1;
        private float lastLayoutWidth = -1f;
        private float lastParentWidth = -1f;
        private float currentButtonWidth = LargeButtonWidth;
        private float currentButtonHeight = LargeButtonHeight;
        private int currentColumns;
        private int currentRows;

        public RectTransform Root => root;
        public RectTransform ContentRoot => content;
        public bool IsVisible => root != null && root.gameObject.activeSelf;
        public int VisibleActionCount => Mathf.Max(0, lastVisibleCount);
        public int CurrentColumnCount => currentColumns;
        public int CurrentRowCount => currentRows;
        public float CurrentButtonWidth => currentButtonWidth;
        public float CurrentButtonHeight => currentButtonHeight;
        public float CurrentPanelHeight => root != null ? root.rect.height : 0f;
        public int TooltipTriggerCount
        {
            get
            {
                int count = 0;
                foreach (ActionView view in views)
                    if (view?.Tooltip != null) count++;
                return count;
            }
        }

        public void Configure(SelectionManager manager, PlayerCommandController commandInput,
            GuardInputController guardInput, BuildingSystem buildings,
            BuildingPlacementController buildingPlacement, ResourceStockpile resources,
            RtsHudAssetCatalog catalog,
            CommandTooltipPresenter tooltipPresenter, RectTransform hudParent)
        {
            if (selection != null) selection.SelectionChanged -= HandleSelectionChanged;
            selection = manager;
            commands = commandInput;
            guard = guardInput;
            buildingSystem = buildings;
            placement = buildingPlacement;
            stockpile = resources;
            assets = catalog;
            Sprite[] dynamite = Phase6C2AssetCatalog.Load()?.Dynamite;
            cannonIcon = dynamite != null && dynamite.Length > 0
                ? dynamite[0] : null;
            tooltip = tooltipPresenter;
            if (root == null) Build(hudParent);
            ApplyPanelPresentation();
            if (selection != null) selection.SelectionChanged += HandleSelectionChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            if (selection != null) selection.SelectionChanged -= HandleSelectionChanged;
            tooltip?.Hide();
        }

        private void Update()
        {
            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 0.10f;
            Refresh();
        }

        private void HandleSelectionChanged()
        {
            tooltip?.Hide();
            refreshAt = 0f;
            Refresh();
        }

        private void Build(RectTransform hudParent)
        {
            root = RtsUiFactory.Rect("CommandPanel", hudParent);
            RtsUiFactory.Anchor(root, Vector2.zero, Vector2.zero,
                new Vector2(FixedPanelLeftX, 0f),
                new Vector2(MinimumPanelWidth, MinimumPanelHeight));
            RtsUiFactory.BuildPanelFrame("SpecialPaper", root, assets.SpecialPaper, 0.5f);
            title = RtsUiFactory.Label("Title", root, "COMMANDS", 18,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RtsUiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(200f, 22f));

            EnsureContentRoot();

            for (int i = 0; i < views.Length; i++)
                views[i] = CreateActionView(i);
            root.gameObject.SetActive(false);
        }

        private void ApplyPanelPresentation()
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(FixedPanelLeftX, 0f);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Clamp(root.rect.width, MinimumPanelWidth, MaximumPanelWidth));
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                MinimumPanelHeight);
            EnsureContentRoot();
            if (title == null)
                title = root.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.fontSize = 18;
                RtsUiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, -5f),
                    new Vector2(200f, 22f));
            }
            foreach (ActionView view in views)
            {
                if (view?.Button == null) continue;
                if (view.Button.transform.parent != content)
                    view.Button.transform.SetParent(content, false);
                ApplyActionPresentation(view);
            }
            lastVisibleCount = -1;
            lastLayoutWidth = -1f;
            lastParentWidth = -1f;
        }

        private ActionView CreateActionView(int index)
        {
            EnsureContentRoot();
            RectTransform rect = RtsUiFactory.Rect("Action" + index, content);
            RtsUiFactory.Anchor(rect, Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(ButtonWidth, ButtonHeight));
            Image background = rect.gameObject.AddComponent<Image>();
            background.sprite = assets.CommandSlot;
            background.preserveAspect = false;
            Button button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.94f, 0.74f, 1f);
            colors.pressedColor = new Color(0.68f, 0.58f, 0.42f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = RtsUiFactory.DisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            Image icon = RtsUiFactory.Image("Icon", rect, null);
            RtsUiFactory.Anchor(icon.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -4f),
                new Vector2(IconWidth, IconHeight));
            CreateFormationGlyph(rect, out RectTransform formationGlyph,
                out Image[] formationDots);
            Text label = RtsUiFactory.Label("Label", rect, "", LabelFontSize,
                TextAnchor.LowerCenter, FontStyle.Bold);
            Text cost = RtsUiFactory.Label("Cost", rect, "", CostFontSize,
                TextAnchor.LowerCenter, FontStyle.Bold);
            Text hotkey = RtsUiFactory.Label("Hotkey", rect, "", HotkeyFontSize,
                TextAnchor.UpperRight, FontStyle.Bold);
            RtsUiFactory.Anchor(hotkey.rectTransform, Vector2.one, Vector2.one,
                new Vector2(-4f, -4f), new Vector2(22f, 20f));
            var view = new ActionView
            {
                Button = button,
                Background = background,
                Icon = icon,
                Label = label,
                Cost = cost,
                Hotkey = hotkey,
                GuardHold = rect.gameObject.AddComponent<GuardButtonHold>(),
                Tooltip = rect.gameObject.AddComponent<CommandTooltipTrigger>(),
                FormationGlyph = formationGlyph,
                FormationDots = formationDots
            };
            view.GuardHold.enabled = false;
            return view;
        }

        private void Refresh()
        {
            if (root == null || selection == null) return;
            if (!PrepareViews())
            {
                root.gameObject.SetActive(false);
                return;
            }
            int shown = 0;
            if (selection.SelectedUnits.Count > 0)
                shown = ShowUnitCommands(shown);
            else if (selection.SelectedBuilding != null)
                shown = ShowBuildingCommands(selection.SelectedBuilding.gameObject,
                    selection.SelectedBuilding.Type, shown);
            else if (selection.SelectedCastle != null)
                shown = ShowBuildingCommands(selection.SelectedCastle.gameObject,
                    BuildingType.Castle, shown);
            else if (selection.SelectedGoldMine != null)
                shown = ShowMineCommands(selection.SelectedGoldMine, shown);
            HideUnusedViews(shown);
            ApplyAdaptiveLayout(shown);
            root.gameObject.SetActive(shown > 0);
        }

        private int ShowUnitCommands(int shown)
        {
            bool combat = false;
            bool canGuard = false;
            bool hasPawn = false;
            UnitController scoutCandidate = null;
            int militaryFormationCount = 0;
            foreach (UnitSelection unit in selection.SelectedUnits)
            {
                if (unit == null) continue;
                combat |= unit.GetComponent<CombatController>()?.CombatData?.AutoAcquireTargets == true;
                canGuard |= unit.GetComponent<UnitGuard>() != null;
                hasPawn |= unit.GetComponent<PawnBuilder>() != null;
                HealthComponent health = unit.GetComponent<HealthComponent>();
                if (scoutCandidate == null && unit.Controller != null
                    && unit.Controller.IsAlive
                    && unit.Controller.Faction == selection.PlayerFaction
                    && health?.CombatData?.UnitType == UnitType.Pawn)
                    scoutCandidate = unit.Controller;
                if (unit.Controller != null && unit.Controller.IsAlive
                    && unit.Controller.Faction == selection.PlayerFaction
                    && unit.Movement != null
                    && MilitaryFormationEligibility.IsFormationMilitaryUnit(unit))
                    militaryFormationCount++;
            }
            if (combat)
            {
                string key = GameInputBindings.Display(GameAction.AttackMove);
                ActionView view = ConfigureClick(shown++, assets.AttackMoveIcon,
                    "Attack Move", key, MatchManager.AllowsGameplay,
                    () => commands != null && commands.BeginAttackMoveTargeting());
                view.Tooltip.ConfigureSimple(tooltip, "Attack Move",
                    "Move toward a target area and engage enemies encountered.", key);
            }
            if (canGuard)
            {
                string key = GameInputBindings.Display(GameAction.Guard);
                ActionView view = ConfigureView(shown++, assets.GuardIcon, "Guard", key,
                    MatchManager.AllowsGameplay, true);
                view.GuardHold.Configure(guard);
                view.Tooltip.ConfigureSimple(tooltip, "Guard",
                    "Hold position and reduce incoming damage while the command is held.", key);
            }
            if (MilitaryFormationEligibility.HasFormationCommandGroup(
                    militaryFormationCount)
                && commands != null)
            {
                for (int i = 0; i < FormationActions.Length; i++)
                {
                    FormationType type = FormationActions[i];
                    ActionView view = ConfigureFormation(shown++, type,
                        MatchManager.AllowsGameplay, commands.CurrentFormation == type,
                        () => commands.SelectFormation(type));
                    ConfigureFormationTooltip(view, type);
                }
            }
            if (scoutCandidate != null)
            {
                PawnScoutEligibility eligibility = PawnScoutPromotion.Evaluate(
                    scoutCandidate, selection.PlayerFaction, stockpile,
                    assets.ScoutFireFrames);
                bool available = eligibility == PawnScoutEligibility.Eligible;
                ActionView view = ConfigureClick(shown++, assets.ScoutIcon,
                    "Scout\n50W", "", available,
                    () => PawnScoutPromotion.TryPromote(scoutCandidate,
                        selection.PlayerFaction, stockpile, assets.ScoutFireFrames));
                view.Tooltip.ConfigureScout(tooltip,
                    PawnScoutPromotion.TooltipDetails(eligibility));
            }
            if (!hasPawn || buildingSystem == null || placement == null) return shown;
            foreach ((BuildingType type, HouseVariant variant, string name) in BuildActions)
            {
                BuildingData data = buildingSystem.GetData(type);
                bool available = data != null && buildingSystem.CanAfford(type)
                    && MatchManager.AllowsGameplay;
                Sprite icon = data != null ? data.GetSprite(selection.PlayerFaction, variant) : null;
                int index = shown++;
                string cost = data != null
                    ? Cost(data.WoodCost, data.GoldCost, data.FoodCost) : "";
                ActionView view = ConfigureClick(index, icon, name + "\n" + cost,
                    "", available, () => placement.Begin(type, variant));
                view.Tooltip.ConfigureBuilding(tooltip, data, name);
            }
            return shown;
        }

        private int ShowBuildingCommands(GameObject host, BuildingType type, int shown)
        {
            BuildingDefenseUpgrade upgrade = host.GetComponent<BuildingDefenseUpgrade>();
            if (upgrade != null && upgrade.Faction == selection.PlayerFaction
                && !upgrade.Purchased)
            {
                Sprite icon = type == BuildingType.Castle
                    ? cannonIcon : host.GetComponent<BuildingDefense>()?.ArrowSprite;
                string title = type == BuildingType.Castle
                    ? "Cannon" : "Auto Arrow";
                ActionView view = ConfigureClick(shown++, icon,
                    title + "\n" + (upgrade.IsUpgrading
                        ? Mathf.CeilToInt(upgrade.SecondsRemaining) + "s ("
                            + Mathf.RoundToInt(upgrade.Progress * 100f) + "%)"
                        : Cost(upgrade.WoodCost, upgrade.GoldCost, 0)),
                    "", upgrade.CanPurchase,
                    () => upgrade.TryPurchase(selection.PlayerFaction));
                view.Tooltip.ConfigureSimple(tooltip, title,
                    upgrade.IsUpgrading ? "Upgrade in progress."
                    : type == BuildingType.Castle
                        ? "Adds a separate explosive cannon attack."
                        : "Adds one automatic arrow to each Tower volley.");
            }
            UnitProductionQueue queue = host.GetComponent<UnitProductionQueue>();
            if (queue != null)
            {
                foreach (UnitType unit in TrainActions)
                {
                    if (!UnitProductionQueue.CanProduce(type, unit)) continue;
                    UnitCombatData data = queue.GetData(unit);
                    bool available = queue.CanTrain(unit);
                    int index = shown++;
                    string cost = data != null
                        ? Cost(data.WoodCost, data.GoldCost, data.FoodCost) : "";
                    ActionView view = ConfigureClick(index, assets.Avatar(unit),
                        "Train " + unit + "\n" + cost, "", available,
                        () => queue.TryTrain(unit));
                    view.Tooltip.ConfigureUnit(tooltip, data);
                }
            }
            BuildingGarrison garrison = host.GetComponent<BuildingGarrison>();
            if (garrison != null)
            {
                ActionView view = ConfigureClick(shown++, assets.EjectAllIcon, "Eject All", "",
                    MatchManager.AllowsGameplay
                        && garrison.InternalCount + garrison.MountedCount > 0,
                    () => garrison.EjectAll() > 0);
                view.Tooltip.ConfigureSimple(tooltip, "Eject All",
                    "Release every internal occupant and mounted Archer from this building.");
            }
            return shown;
        }

        private int ShowMineCommands(GoldMineObjective mine, int shown)
        {
            bool friendly = mine.HasOwner && mine.Faction == selection.PlayerFaction;
            if (friendly)
            {
                ActionView view = ConfigureClick(shown++, assets.EjectAllIcon, "Eject All", "",
                    MatchManager.AllowsGameplay && mine.OccupantCount > 0,
                    () => mine.EjectAll() > 0);
                view.Tooltip.ConfigureSimple(tooltip, "Eject All",
                    "Release every Pawn currently inside the Gold Mine.");
            }
            return shown;
        }

        private bool PrepareViews()
        {
            if (!ViewsAreBound() && !TryRebindViews()) return false;
            foreach (ActionView view in views)
                view.Button.onClick.RemoveAllListeners();
            return true;
        }

        private bool ViewsAreBound()
        {
            foreach (ActionView view in views)
                if (view == null || view.Button == null || view.Background == null
                    || view.Icon == null || view.Label == null || view.Cost == null
                    || view.Hotkey == null
                    || view.GuardHold == null || view.Tooltip == null)
                    return false;
            return true;
        }

        // Unity can preserve the generated HUD hierarchy across a script/domain reload
        // while the non-serialized ActionView wrappers are recreated. Rebind those
        // wrappers to the existing controls instead of duplicating the HUD.
        private bool TryRebindViews()
        {
            if (root == null) return false;
            EnsureContentRoot();
            for (int i = 0; i < views.Length; i++)
            {
                Transform action = FindAction(i);
                if (action == null)
                {
                    views[i] = CreateActionView(i);
                    continue;
                }
                if (action.parent != content)
                    action.SetParent(content, false);
                Transform iconObject = action.Find("Icon");
                Transform labelObject = action.Find("Label");
                Transform costObject = action.Find("Cost");
                if (costObject == null)
                {
                    Text createdCost = RtsUiFactory.Label("Cost", action, "",
                        CostFontSize, TextAnchor.LowerCenter, FontStyle.Bold);
                    costObject = createdCost.transform;
                }
                Transform hotkeyObject = action.Find("Hotkey");
                var rebound = new ActionView
                {
                    Button = action.GetComponent<Button>(),
                    Background = action.GetComponent<Image>(),
                    Icon = iconObject != null ? iconObject.GetComponent<Image>() : null,
                    Label = labelObject != null ? labelObject.GetComponent<Text>() : null,
                    Cost = costObject.GetComponent<Text>(),
                    Hotkey = hotkeyObject != null ? hotkeyObject.GetComponent<Text>() : null,
                    GuardHold = action.GetComponent<GuardButtonHold>(),
                    Tooltip = action.GetComponent<CommandTooltipTrigger>()
                };
                BindOrCreateFormationGlyph(action, rebound);
                if (rebound.Button == null || rebound.Background == null
                    || rebound.Icon == null || rebound.Label == null || rebound.Cost == null
                    || rebound.Hotkey == null || rebound.GuardHold == null
                    || rebound.Tooltip == null || rebound.FormationGlyph == null
                    || !FormationDotsAreBound(rebound.FormationDots))
                    return false;
                ApplyActionPresentation(rebound);
                views[i] = rebound;
            }
            return true;
        }

        private void ApplyAdaptiveLayout(int visibleCount)
        {
            float parentWidth = root.parent is RectTransform parent
                ? parent.rect.width : RtsHudController.HudSize.x;
            if (visibleCount <= 0)
            {
                if (lastVisibleCount == 0
                    && Mathf.Approximately(root.rect.width, MinimumPanelWidth)
                    && Mathf.Approximately(root.rect.height, MinimumPanelHeight)
                    && Mathf.Approximately(lastParentWidth, parentWidth)) return;
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    MinimumPanelWidth);
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    MinimumPanelHeight);
                currentColumns = 0;
                currentRows = 0;
                currentButtonWidth = LargeButtonWidth;
                currentButtonHeight = LargeButtonHeight;
                lastVisibleCount = 0;
                lastLayoutWidth = MinimumPanelWidth;
                lastParentWidth = parentWidth;
                return;
            }

            if (lastVisibleCount == visibleCount
                && Mathf.Approximately(lastParentWidth, parentWidth)
                && Mathf.Approximately(root.rect.width, lastLayoutWidth)
                && Mathf.Approximately(root.rect.height, MinimumPanelHeight))
                return;

            float maximumSafeWidth = CalculateMaximumSafePanelWidth();
            float buttonWidth = CalculateButtonWidth(visibleCount, maximumSafeWidth);
            float buttonHeight = CalculateButtonHeight(buttonWidth);
            float width = CalculatePanelWidth(visibleCount, maximumSafeWidth);
            float height = MinimumPanelHeight;
            if (lastVisibleCount == visibleCount
                && Mathf.Approximately(lastLayoutWidth, width)
                && Mathf.Approximately(lastParentWidth, parentWidth)
                && Mathf.Approximately(currentButtonWidth, buttonWidth)
                && Mathf.Approximately(root.rect.width, width)
                && Mathf.Approximately(root.rect.height, height)) return;

            currentColumns = visibleCount;
            currentRows = 1;
            currentButtonWidth = buttonWidth;
            currentButtonHeight = buttonHeight;
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            float contentWidth = visibleCount * buttonWidth
                + Mathf.Max(0, visibleCount - 1) * HorizontalSpacing;
            float bottom = Mathf.Round((height - TitleBandHeight - buttonHeight) * 0.5f);
            ApplyContentGeometry(bottom, buttonHeight);
            float startX = Mathf.Round((content.rect.width - contentWidth) * 0.5f);
            for (int index = 0; index < visibleCount; index++)
            {
                ActionView view = views[index];
                ApplyActionPresentation(view, buttonWidth, buttonHeight);
                if (view.Formation.HasValue)
                    ConfigureFormationGlyph(view, view.Formation.Value);
                RectTransform rect = view.Button.transform as RectTransform;
                rect.anchoredPosition = new Vector2(
                    startX + index * (buttonWidth + HorizontalSpacing), 0f);
            }

            lastVisibleCount = visibleCount;
            lastLayoutWidth = width;
            lastParentWidth = parentWidth;
        }

        private float CalculateMaximumSafePanelWidth()
        {
            if (root.parent is not RectTransform parent)
                return MaximumPanelWidth;
            return CalculateMaximumSafePanelWidth(parent.rect.width,
                FixedPanelLeftX);
        }

        public static int CalculateUsedColumns(int visibleCount)
            => Mathf.Clamp(visibleCount, 0, ActionViewCount);

        public static float CalculateButtonWidth(int visibleCount,
            float maximumSafePanelWidth)
        {
            if (visibleCount <= 0) return LargeButtonWidth;
            float available = maximumSafePanelWidth - HorizontalPadding * 2f
                - Mathf.Max(0, visibleCount - 1) * HorizontalSpacing;
            return Mathf.Clamp(available / visibleCount,
                MinimumButtonWidth, LargeButtonWidth);
        }

        public static float CalculateButtonHeight(float buttonWidth)
            => Mathf.Lerp(MinimumButtonHeight, LargeButtonHeight,
                CalculateCardScale(buttonWidth));

        public static float CalculatePanelWidth(int visibleCount,
            float maximumSafePanelWidth)
        {
            if (visibleCount <= 0) return MinimumPanelWidth;
            float buttonWidth = CalculateButtonWidth(visibleCount,
                maximumSafePanelWidth);
            float desired = HorizontalPadding * 2f + visibleCount * buttonWidth
                + Mathf.Max(0, visibleCount - 1) * HorizontalSpacing;
            return Mathf.Clamp(desired, MinimumPanelWidth, maximumSafePanelWidth);
        }

        public static float CalculateMaximumSafePanelWidth(float hudWidth,
            float panelLeftX)
            => Mathf.Clamp(hudWidth - panelLeftX,
                MinimumPanelWidth, MaximumPanelWidth);

        private void EnsureContentRoot()
        {
            if (content == null)
                content = root.Find("CommandContent") as RectTransform;
            if (content == null)
                content = RtsUiFactory.Rect("CommandContent", root);
            ApplyContentGeometry(0f, LargeButtonHeight);
        }

        private void ApplyContentGeometry(float bottom, float height)
        {
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.anchoredPosition = new Vector2(0f, bottom);
            content.sizeDelta = new Vector2(-HorizontalPadding * 2f, height);
        }

        private Transform FindAction(int index)
        {
            Transform action = content != null ? content.Find("Action" + index) : null;
            return action != null ? action : root.Find("Action" + index);
        }

        public static int CalculateRequiredRows(int visibleCount, int columns)
            => visibleCount <= 0 ? 0 : 1;

        public static float CalculatePanelHeight(int rowCount)
            => MinimumPanelHeight;

        public static float CalculateIconSize(float buttonWidth)
            => Mathf.Lerp(MinimumIconSize, LargeIconSize,
                CalculateCardScale(buttonWidth));

        public static int CalculateLabelFontSize(float buttonWidth)
            => Mathf.RoundToInt(Mathf.Lerp(MinimumLabelFontSize,
                LargeLabelFontSize, CalculateCardScale(buttonWidth)));

        public static int CalculateCostFontSize(float buttonWidth)
            => Mathf.RoundToInt(Mathf.Lerp(MinimumCostFontSize,
                LargeCostFontSize, CalculateCardScale(buttonWidth)));

        public static int CalculateHotkeyFontSize(float buttonWidth)
            => Mathf.RoundToInt(Mathf.Lerp(MinimumHotkeyFontSize,
                LargeHotkeyFontSize, CalculateCardScale(buttonWidth)));

        private static float CalculateCardScale(float buttonWidth)
            => Mathf.InverseLerp(MinimumButtonWidth, LargeButtonWidth,
                buttonWidth);

        private static void ApplyActionPresentation(ActionView view)
            => ApplyActionPresentation(view, LargeButtonWidth, LargeButtonHeight);

        private static void ApplyActionPresentation(ActionView view, float buttonWidth,
            float buttonHeight)
        {
            RectTransform rect = view.Button.transform as RectTransform;
            rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
            float iconSize = CalculateIconSize(buttonWidth);
            int labelSize = CalculateLabelFontSize(buttonWidth);
            int costSize = CalculateCostFontSize(buttonWidth);
            int hotkeySize = CalculateHotkeyFontSize(buttonWidth);
            RtsUiFactory.Anchor(view.Icon.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -4f),
                new Vector2(iconSize, iconSize));
            view.Label.fontSize = labelSize;
            RtsUiFactory.Anchor(view.Label.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, costSize + 4f),
                new Vector2(buttonWidth - 6f, labelSize + 4f));
            view.Cost.fontSize = costSize;
            view.Cost.color = RtsUiFactory.MutedTextColor;
            RtsUiFactory.Anchor(view.Cost.rectTransform, new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 2f),
                new Vector2(buttonWidth - 6f, costSize + 3f));
            view.Hotkey.fontSize = hotkeySize;
            RtsUiFactory.Anchor(view.Hotkey.rectTransform, Vector2.one, Vector2.one,
                new Vector2(-3f, -3f), new Vector2(hotkeySize + 9f,
                    hotkeySize + 7f));
            RtsUiFactory.Anchor(view.FormationGlyph, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -5f),
                new Vector2(iconSize, iconSize - 4f));
            float dotSize = Mathf.Lerp(5.5f, 8f, CalculateCardScale(buttonWidth));
            foreach (Image dot in view.FormationDots)
                dot.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
        }

        private static bool FormationDotsAreBound(Image[] dots)
        {
            if (dots == null || dots.Length != 6) return false;
            for (int i = 0; i < dots.Length; i++)
                if (dots[i] == null) return false;
            return true;
        }

        private void HideUnusedViews(int shown)
        {
            for (int i = shown; i < views.Length; i++)
            {
                ActionView view = views[i];
                if (view.GuardHold.enabled) view.GuardHold.enabled = false;
                view.Tooltip.Clear();
                view.Button.gameObject.SetActive(false);
            }
        }

        private ActionView ConfigureView(int index, Sprite icon, string label,
            string hotkey, bool available, bool guardAction = false)
        {
            ActionView view = views[index];
            view.Button.gameObject.SetActive(true);
            if (view.GuardHold.enabled != guardAction)
                view.GuardHold.enabled = guardAction;
            view.Button.interactable = available;
            view.Tooltip.ConfigureCommandAudio(available, guardAction);
            view.Background.color = available
                ? RtsUiFactory.EnabledColor : RtsUiFactory.DisabledColor;
            view.Icon.sprite = icon;
            view.Icon.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            view.Icon.gameObject.SetActive(icon != null);
            if (view.FormationGlyph != null)
                view.FormationGlyph.gameObject.SetActive(false);
            view.Formation = null;
            int lineBreak = label.IndexOf('\n');
            view.Label.text = lineBreak >= 0 ? label[..lineBreak] : label;
            view.Cost.text = lineBreak >= 0 ? label[(lineBreak + 1)..] : "";
            view.Cost.gameObject.SetActive(view.Cost.text.Length > 0);
            view.Hotkey.text = hotkey;
            return view;
        }

        private ActionView ConfigureFormation(int index, FormationType type,
            bool available, bool active, System.Func<bool> action)
        {
            ActionView view = ConfigureClick(index, null, FormationLabel(type), "",
                available, action);
            view.Formation = type;
            ConfigureFormationGlyph(view, type);
            view.Background.color = available
                ? active ? FormationActiveColor : RtsUiFactory.EnabledColor
                : RtsUiFactory.DisabledColor;
            return view;
        }

        private void ConfigureFormationTooltip(ActionView view, FormationType type)
        {
            string details = type switch
            {
                FormationType.Wedge =>
                    "Forward-pointing formation. Frontline units occupy the tip.",
                FormationType.Loose =>
                    "Wider spacing between units while preserving battle roles.",
                FormationType.Line =>
                    "One wide rank perpendicular to movement. Frontline roles prefer the center.",
                _ => "Tight rectangular formation. Melee units lead."
            };
            view.Tooltip.ConfigureSimple(tooltip, FormationTitle(type), details);
        }

        private static string FormationLabel(FormationType type)
            => type switch
            {
                FormationType.Wedge => "Wedge",
                FormationType.Loose => "Loose",
                FormationType.Line => "Line",
                _ => "Compact"
            };

        private static string FormationTitle(FormationType type)
            => type switch
            {
                FormationType.Wedge => "Wedge Formation",
                FormationType.Loose => "Loose Formation",
                FormationType.Line => "Line Formation",
                _ => "Compact Formation"
            };

        private static void ConfigureFormationGlyph(ActionView view, FormationType type)
        {
            if (view.FormationGlyph == null || view.FormationDots == null) return;
            view.FormationGlyph.gameObject.SetActive(true);
            for (int i = 0; i < view.FormationDots.Length; i++)
                view.FormationDots[i].gameObject.SetActive(false);

            Vector2[] points = type switch
            {
                FormationType.Wedge => WedgeGlyphPoints,
                FormationType.Loose => LooseGlyphPoints,
                FormationType.Line => LineGlyphPoints,
                _ => CompactGlyphPoints
            };
            float scale = view.FormationGlyph.rect.width / LargeIconSize;
            int count = Mathf.Min(points.Length, view.FormationDots.Length);
            for (int i = 0; i < count; i++)
            {
                Image dot = view.FormationDots[i];
                dot.rectTransform.anchoredPosition = points[i] * scale;
                dot.gameObject.SetActive(true);
            }
        }

        private static void CreateFormationGlyph(Transform parent,
            out RectTransform glyph, out Image[] dots)
        {
            glyph = RtsUiFactory.Rect("FormationGlyph", parent);
            RtsUiFactory.Anchor(glyph, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(68f, 64f));
            dots = new Image[6];
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = RtsUiFactory.Image("Dot" + i, glyph, null, false);
                dots[i].color = RtsUiFactory.TextColor;
                RtsUiFactory.Anchor(dots[i].rectTransform, new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
            }
            glyph.gameObject.SetActive(false);
        }

        private static void BindOrCreateFormationGlyph(Transform action, ActionView view)
        {
            Transform existing = action.Find("FormationGlyph");
            if (existing == null)
            {
                CreateFormationGlyph(action, out RectTransform glyph, out Image[] dots);
                view.FormationGlyph = glyph;
                view.FormationDots = dots;
                return;
            }
            view.FormationGlyph = existing as RectTransform;
            view.FormationDots = new Image[6];
            for (int i = 0; i < view.FormationDots.Length; i++)
            {
                Transform dot = existing.Find("Dot" + i);
                view.FormationDots[i] = dot != null ? dot.GetComponent<Image>() : null;
            }
        }

        private ActionView ConfigureClick(int index, Sprite icon, string label, string hotkey,
            bool available, System.Func<bool> action)
        {
            ActionView view = ConfigureView(index, icon, label, hotkey, available);
            if (action != null)
                view.Button.onClick.AddListener(() =>
                    GameplayAudioService.RequestUi(action()
                        ? GameplayAudioEvent.CommandClick
                        : GameplayAudioEvent.CommandRejected));
            return view;
        }

        private static string Cost(int wood, int gold, int food)
        {
            string value = food > 0 ? food + "F" : "";
            if (wood > 0) value += (value.Length > 0 ? " " : "") + wood + "W";
            if (gold > 0) value += (value.Length > 0 ? " " : "") + gold + "G";
            return value;
        }
    }
}
