using TinyKingdom.Economy;
using TinyKingdom.Input;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.Buildings
{
    [RequireComponent(typeof(Canvas))]
    public sealed class BuildingCommandUI : MonoBehaviour
    {
        private static readonly (BuildingType type, HouseVariant variant, string label)[] Commands =
        {
            (BuildingType.House, HouseVariant.A, "House A"),
            (BuildingType.House, HouseVariant.B, "House B"),
            (BuildingType.House, HouseVariant.C, "House C"),
            (BuildingType.Barracks, HouseVariant.A, "Barracks"),
            (BuildingType.Tower, HouseVariant.A, "Tower"),
            (BuildingType.Archery, HouseVariant.A, "Archery"),
            (BuildingType.Monastery, HouseVariant.A, "Monastery")
        };
        private BuildingSystem system;
        private BuildingPlacementController placement;
        private SelectionManager selection;
        private ResourceStockpile stockpile;
        private FactionPopulation population;
        private RectTransform commandsPanel;
        private RectTransform buildingPanel;
        private readonly Button[] buttons = new Button[Commands.Length];
        private readonly Image[] backgrounds = new Image[Commands.Length];
        private Text buildingLabel;
        private Text populationLabel;

        public void Configure(BuildingSystem authority, BuildingPlacementController input,
            SelectionManager selected, ResourceStockpile resources, FactionPopulation pop)
        {
            system = authority;
            placement = input;
            selection = selected;
            stockpile = resources;
            population = pop;
            Build();
            selection.SelectionChanged += Refresh;
            stockpile.Changed += Refresh;
            population.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (selection != null) selection.SelectionChanged -= Refresh;
            if (stockpile != null) stockpile.Changed -= Refresh;
            if (population != null) population.Changed -= Refresh;
        }

        private void Update()
        {
            if (buildingPanel != null && buildingPanel.gameObject.activeSelf)
                RefreshBuildingInfo();
        }

        private void Build()
        {
            commandsPanel = Panel("BuildCommands", new Vector2(522f, 130f), new Vector2(304f, 12f));
            for (int i = 0; i < Commands.Length; i++)
            {
                (BuildingType type, HouseVariant variant, string title) = Commands[i];
                BuildingData data = system.GetData(type);
                RectTransform rect = Rect(title, commandsPanel,
                    new Vector2(122f, 54f), new Vector2(7f + i % 4 * 127f, 68f - i / 4 * 60f));
                Image background = rect.gameObject.AddComponent<Image>();
                background.color = new Color(0.2f, 0.34f, 0.28f, 0.95f);
                backgrounds[i] = background;
                Button button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener(() => placement.Begin(type, variant));
                buttons[i] = button;
                RectTransform icon = Rect("Icon", rect, new Vector2(36f, 42f), new Vector2(3f, 6f));
                Image image = icon.gameObject.AddComponent<Image>();
                image.sprite = data.GetSprite(selection.PlayerFaction, variant);
                image.preserveAspect = true;
                image.raycastTarget = false;
                Label(title + "Text", rect, $"{title}\n{Cost(data)}", 11,
                    new Vector2(81f, 54f), new Vector2(40f, 0f));
            }

            buildingPanel = Panel("BuildingSelection", new Vector2(280f, 96f),
                new Vector2(12f, 12f));
            buildingLabel = Label("BuildingStatus", buildingPanel, "", 16,
                new Vector2(260f, 62f), new Vector2(10f, 23f));
            populationLabel = Label("Population", transform, "", 16,
                new Vector2(190f, 30f), new Vector2(306f, -12f));
            RectTransform popRect = (RectTransform)populationLabel.transform;
            popRect.anchorMin = popRect.anchorMax = popRect.pivot = new Vector2(0f, 1f);
        }

        private void Refresh()
        {
            if (commandsPanel == null || selection == null) return;
            bool hasPawn = false;
            foreach (var unit in selection.SelectedUnits)
                if (unit != null && unit.GetComponent<PawnBuilder>() != null)
                {
                    hasPawn = true;
                    break;
                }
            commandsPanel.gameObject.SetActive(hasPawn);
            for (int i = 0; i < buttons.Length; i++)
            {
                bool affordable = system.CanAfford(Commands[i].type);
                buttons[i].interactable = affordable;
                backgrounds[i].color = affordable
                    ? new Color(0.2f, 0.34f, 0.28f, 0.95f)
                    : new Color(0.28f, 0.28f, 0.28f, 0.65f);
            }
            bool showBuilding = selection.SelectedBuilding != null;
            buildingPanel.gameObject.SetActive(showBuilding);
            if (showBuilding) RefreshBuildingInfo();
            populationLabel.text = $"Pop {population.Used}+{population.Reserved}/{population.Cap}";
        }

        private void RefreshBuildingInfo()
        {
            BuildingSite site = selection.SelectedBuilding;
            if (site == null)
            {
                buildingPanel.gameObject.SetActive(false);
                return;
            }
            buildingLabel.text = $"{site.Type}{(site.Type == BuildingType.House ? $" {site.Variant}" : "")}  HP {Mathf.CeilToInt(site.CurrentHP)}/{site.MaxHP}"
                + (site.State == BuildingState.UnderConstruction
                    ? $"\nConstruction {Mathf.RoundToInt(site.ConstructionProgress * 100f)}%"
                    : "\nCompleted");
        }

        private static string Cost(BuildingData data)
            => $"{data.WoodCost} Wood" + (data.GoldCost > 0 ? $"\n{data.GoldCost} Gold" : "");

        private RectTransform Panel(string name, Vector2 size, Vector2 position)
        {
            RectTransform panel = Rect(name, transform, size, position);
            panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.11f, 0.15f, 0.9f);
            return panel;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Text Label(string name, Transform parent, string value, int size,
            Vector2 bounds, Vector2 position)
        {
            RectTransform rect = Rect(name, parent, bounds, position);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = value;
            label.raycastTarget = false;
            return label;
        }
    }
}
