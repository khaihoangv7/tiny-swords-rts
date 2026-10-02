using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Match;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.UI;
using TinyKingdom.Audio;

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

        private sealed class ActionView
        {
            internal Button Button;
            internal Image Background;
            internal Image Icon;
            internal Text Label;
            internal Text Hotkey;
            internal GuardButtonHold GuardHold;
            internal CommandTooltipTrigger Tooltip;
        }

        private readonly ActionView[] views = new ActionView[12];
        private SelectionManager selection;
        private PlayerCommandController commands;
        private GuardInputController guard;
        private BuildingSystem buildingSystem;
        private BuildingPlacementController placement;
        private RtsHudAssetCatalog assets;
        private CommandTooltipPresenter tooltip;
        private RectTransform root;
        private Text title;
        private float refreshAt;

        public RectTransform Root => root;
        public bool IsVisible => root != null && root.gameObject.activeSelf;
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
            BuildingPlacementController buildingPlacement, RtsHudAssetCatalog catalog,
            CommandTooltipPresenter tooltipPresenter, RectTransform hudParent)
        {
            if (selection != null) selection.SelectionChanged -= HandleSelectionChanged;
            selection = manager;
            commands = commandInput;
            guard = guardInput;
            buildingSystem = buildings;
            placement = buildingPlacement;
            assets = catalog;
            tooltip = tooltipPresenter;
            if (root == null) Build(hudParent);
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
                new Vector2(608f, 0f), new Vector2(650f, 160f));
            RtsUiFactory.BuildPanelFrame("SpecialPaper", root, assets.SpecialPaper, 0.5f);
            title = RtsUiFactory.Label("Title", root, "COMMANDS", 16,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RtsUiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(180f, 18f));

            for (int i = 0; i < views.Length; i++)
            {
                RectTransform rect = RtsUiFactory.Rect("Action" + i, root);
                RtsUiFactory.Anchor(rect, Vector2.zero, Vector2.zero,
                    new Vector2(22f + (i % 7) * 68f, 76f - (i / 7) * 66f),
                    new Vector2(60f, 58f));
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
                    new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(30f, 30f));
                Text label = RtsUiFactory.Label("Label", rect, "", 9,
                    TextAnchor.LowerCenter, FontStyle.Bold);
                RtsUiFactory.Stretch(label.rectTransform, 2f, 2f, 2f, 36f);
                Text hotkey = RtsUiFactory.Label("Hotkey", rect, "", 10,
                    TextAnchor.UpperRight, FontStyle.Bold);
                RtsUiFactory.Anchor(hotkey.rectTransform, Vector2.one, Vector2.one,
                    new Vector2(-2f, -2f), new Vector2(18f, 16f));
                views[i] = new ActionView
                {
                    Button = button,
                    Background = background,
                    Icon = icon,
                    Label = label,
                    Hotkey = hotkey,
                    GuardHold = rect.gameObject.AddComponent<GuardButtonHold>(),
                    Tooltip = rect.gameObject.AddComponent<CommandTooltipTrigger>()
                };
                views[i].GuardHold.enabled = false;
            }
            root.gameObject.SetActive(false);
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
            root.gameObject.SetActive(shown > 0);
        }

        private int ShowUnitCommands(int shown)
        {
            bool combat = false;
            bool canGuard = false;
            bool hasPawn = false;
            foreach (UnitSelection unit in selection.SelectedUnits)
            {
                if (unit == null) continue;
                combat |= unit.GetComponent<CombatController>()?.CombatData?.AutoAcquireTargets == true;
                canGuard |= unit.GetComponent<UnitGuard>() != null;
                hasPawn |= unit.GetComponent<PawnBuilder>() != null;
            }
            if (combat)
            {
                ActionView view = ConfigureClick(shown++, assets.AttackMoveIcon,
                    "Attack Move", "A", MatchManager.AllowsGameplay,
                    () => commands != null && commands.BeginAttackMoveTargeting());
                view.Tooltip.ConfigureSimple(tooltip, "Attack Move",
                    "Move toward a target area and engage enemies encountered.", "A");
            }
            if (canGuard)
            {
                ActionView view = ConfigureView(shown++, assets.GuardIcon, "Guard", "G",
                    MatchManager.AllowsGameplay, true);
                view.GuardHold.Configure(guard);
                view.Tooltip.ConfigureSimple(tooltip, "Guard",
                    "Hold position and reduce incoming damage while the command is held.", "G");
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
                    || view.Icon == null || view.Label == null || view.Hotkey == null
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
            for (int i = 0; i < views.Length; i++)
            {
                Transform action = root.Find("Action" + i);
                if (action == null) return false;
                Transform iconObject = action.Find("Icon");
                Transform labelObject = action.Find("Label");
                Transform hotkeyObject = action.Find("Hotkey");
                var rebound = new ActionView
                {
                    Button = action.GetComponent<Button>(),
                    Background = action.GetComponent<Image>(),
                    Icon = iconObject != null ? iconObject.GetComponent<Image>() : null,
                    Label = labelObject != null ? labelObject.GetComponent<Text>() : null,
                    Hotkey = hotkeyObject != null ? hotkeyObject.GetComponent<Text>() : null,
                    GuardHold = action.GetComponent<GuardButtonHold>(),
                    Tooltip = action.GetComponent<CommandTooltipTrigger>()
                };
                if (rebound.Button == null || rebound.Background == null
                    || rebound.Icon == null || rebound.Label == null
                    || rebound.Hotkey == null || rebound.GuardHold == null
                    || rebound.Tooltip == null)
                    return false;
                views[i] = rebound;
            }
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
            view.Icon.gameObject.SetActive(icon != null);
            view.Label.text = label;
            view.Hotkey.text = hotkey;
            return view;
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
