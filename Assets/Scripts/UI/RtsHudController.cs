using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class RtsHudController : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
        public const float SafeScreenMargin = 12f;
        public const float HudHeight = 188f;
        public static readonly Vector2 HudSize = new(1896f, HudHeight);

        private ResourcePanelPresenter resources;
        private SelectionInfoPanelPresenter selectionInfo;
        private CommandPanelPresenter commands;
        private CommandTooltipPresenter tooltip;
        private DebugMatchControlsPresenter debugControls;
        private TopLeftHudPresenter topLeft;
        private ControlGroupHudPresenter controlGroups;
        private SettingsPanelPresenter settings;
        private RectTransform hudRoot;
        private Rect lastSafeArea;

        public RectTransform HudRoot => hudRoot;
        public ResourcePanelPresenter ResourcePanel => resources;
        public SelectionInfoPanelPresenter SelectionPanel => selectionInfo;
        public CommandPanelPresenter CommandPanel => commands;
        public CommandTooltipPresenter CommandTooltip => tooltip;
        public DebugMatchControlsPresenter DebugControls => debugControls;
        public bool IsReady => resources != null && resources.IsReady
            && selectionInfo != null && commands != null
            && tooltip != null && tooltip.HasValidReferences;

        public void Configure(SelectionManager selection,
            PlayerCommandController commandInput, GuardInputController guardInput,
            ResourceStockpile stockpile, FactionPopulation population,
            BuildingSystem buildingSystem, BuildingPlacementController placement)
        {
            RtsHudAssetCatalog catalog = RtsHudAssetCatalog.Load();
            if (catalog == null || !catalog.IsComplete)
            {
                Debug.LogError("RTS HUD asset catalog is missing or incomplete. "
                    + "Run Tools > Tiny Kingdom > Prepare RTS HUD.", this);
                return;
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            DisableLegacyPresenters();
            if (hudRoot == null)
            {
                hudRoot = RtsUiFactory.Rect("RtsBottomHud", transform);
            }
            ApplyHudGeometry();
            resources = GetComponent<ResourcePanelPresenter>()
                ?? gameObject.AddComponent<ResourcePanelPresenter>();
            selectionInfo = GetComponent<SelectionInfoPanelPresenter>()
                ?? gameObject.AddComponent<SelectionInfoPanelPresenter>();
            commands = GetComponent<CommandPanelPresenter>()
                ?? gameObject.AddComponent<CommandPanelPresenter>();
            tooltip = GetComponent<CommandTooltipPresenter>()
                ?? gameObject.AddComponent<CommandTooltipPresenter>();
            debugControls = GetComponent<DebugMatchControlsPresenter>()
                ?? gameObject.AddComponent<DebugMatchControlsPresenter>();
            topLeft = GetComponent<TopLeftHudPresenter>()
                ?? gameObject.AddComponent<TopLeftHudPresenter>();
            controlGroups = GetComponent<ControlGroupHudPresenter>()
                ?? gameObject.AddComponent<ControlGroupHudPresenter>();
            settings = GetComponent<SettingsPanelPresenter>()
                ?? gameObject.AddComponent<SettingsPanelPresenter>();
            resources.Configure(stockpile, population, catalog, hudRoot);
            selectionInfo.Configure(selection, catalog, hudRoot);
            commands.Configure(selection, commandInput, guardInput,
                buildingSystem, placement, stockpile, catalog, tooltip, hudRoot);
            tooltip.Configure(catalog, stockpile, population, hudRoot);
            debugControls.Configure(catalog);
            topLeft.Configure(selection, catalog);
            controlGroups.Configure(selection, catalog);
            settings.Configure(selection, stockpile, debugControls, catalog);
        }

        private void ApplyHudGeometry()
        {
            // The bottom HUD owns the full safe horizontal canvas region. Individual
            // panels still size their real surfaces, so empty HUD space never blocks
            // world input.
            Vector2 safeLeft = RtsUiFactory.SafeCorner(false, false);
            Vector2 safeRight = RtsUiFactory.SafeCorner(true, false);
            hudRoot.anchorMin = safeLeft;
            hudRoot.anchorMax = new Vector2(safeRight.x, safeLeft.y);
            hudRoot.pivot = Vector2.zero;
            hudRoot.offsetMin = new Vector2(SafeScreenMargin, SafeScreenMargin);
            hudRoot.offsetMax = new Vector2(-SafeScreenMargin,
                SafeScreenMargin + HudHeight);
            lastSafeArea = Screen.safeArea;
        }

        private void Update()
        {
            if (hudRoot != null && Screen.safeArea != lastSafeArea)
                ApplyHudGeometry();
        }

        private void DisableLegacyPresenters()
        {
            Disable<SelectionSummaryUI>();
            Disable<EconomyHUD>();
            Disable<BuildingCommandUI>();
            Disable<ProductionUI>();
            Disable<GarrisonUI>();
            foreach (string childName in new[]
            {
                "SelectionSummary", "EconomyHUD", "BuildCommands",
                "BuildingSelection", "ProductionCommands", "GarrisonPanel", "Population"
            })
            {
                Transform child = transform.Find(childName);
                if (child != null) child.gameObject.SetActive(false);
            }
        }

        private void Disable<T>() where T : Behaviour
        {
            T component = GetComponent<T>();
            if (component == null) return;
            component.enabled = false;
            Destroy(component);
        }
    }
}
