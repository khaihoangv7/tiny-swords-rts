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
        private ResourcePanelPresenter resources;
        private SelectionInfoPanelPresenter selectionInfo;
        private CommandPanelPresenter commands;
        private CommandTooltipPresenter tooltip;
        private RectTransform hudRoot;

        public RectTransform HudRoot => hudRoot;
        public ResourcePanelPresenter ResourcePanel => resources;
        public SelectionInfoPanelPresenter SelectionPanel => selectionInfo;
        public CommandPanelPresenter CommandPanel => commands;
        public CommandTooltipPresenter CommandTooltip => tooltip;
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
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            DisableLegacyPresenters();
            if (hudRoot == null)
            {
                hudRoot = RtsUiFactory.Rect("RtsBottomHud", transform);
                RtsUiFactory.Anchor(hudRoot, Vector2.zero, Vector2.zero,
                    new Vector2(12f, 12f), new Vector2(1258f, 160f));
            }
            resources = GetComponent<ResourcePanelPresenter>()
                ?? gameObject.AddComponent<ResourcePanelPresenter>();
            selectionInfo = GetComponent<SelectionInfoPanelPresenter>()
                ?? gameObject.AddComponent<SelectionInfoPanelPresenter>();
            commands = GetComponent<CommandPanelPresenter>()
                ?? gameObject.AddComponent<CommandPanelPresenter>();
            tooltip = GetComponent<CommandTooltipPresenter>()
                ?? gameObject.AddComponent<CommandTooltipPresenter>();
            resources.Configure(stockpile, population, catalog, hudRoot);
            selectionInfo.Configure(selection, catalog, hudRoot);
            commands.Configure(selection, commandInput, guardInput,
                buildingSystem, placement, catalog, tooltip, hudRoot);
            tooltip.Configure(catalog, stockpile, population, hudRoot);
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
