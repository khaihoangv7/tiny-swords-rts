using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class ResourcePanelPresenter : MonoBehaviour
    {
        public const float PanelWidth = 220f;
        public const float PanelHeight = 188f;
        public const float RowHeight = 36f;
        public const float IconSize = 34f;
        public const int ValueFontSize = 22;

        private ResourceStockpile stockpile;
        private FactionPopulation population;
        private RectTransform root;
        private Text title;
        private Text populationValue;
        private Text woodValue;
        private Text goldValue;
        private Text foodValue;

        public RectTransform Root => root;
        public bool IsReady => root != null && root.gameObject.activeSelf
            && stockpile != null && population != null;

        public void Configure(ResourceStockpile resources, FactionPopulation cap,
            RtsHudAssetCatalog assets, RectTransform hudParent)
        {
            if (stockpile != null) stockpile.Changed -= Refresh;
            if (population != null) population.Changed -= Refresh;
            stockpile = resources;
            population = cap;
            if (root == null) Build(assets, hudParent);
            ApplyPresentationLayout();
            if (stockpile != null) stockpile.Changed += Refresh;
            if (population != null) population.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (stockpile != null) stockpile.Changed -= Refresh;
            if (population != null) population.Changed -= Refresh;
        }

        private void Build(RtsHudAssetCatalog assets, RectTransform hudParent)
        {
            root = RtsUiFactory.Rect("ResourcePanel", hudParent);
            RtsUiFactory.Anchor(root, Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            RtsUiFactory.BuildPanelFrame("WoodTable", root, assets.WoodTable, 0.38f);

            title = RtsUiFactory.Label("Title", root, "RESOURCES", 18,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RtsUiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(160f, 22f));

            populationValue = AddRow("Population", assets.PopulationIcon, 124f);
            woodValue = AddRow("Wood", assets.WoodIcon, 88f);
            goldValue = AddRow("Gold", assets.GoldIcon, 52f);
            foodValue = AddRow("Food", assets.FoodIcon, 16f);
        }

        private void ApplyPresentationLayout()
        {
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            if (title == null) title = root.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.fontSize = 18;
                RtsUiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, -5f),
                    new Vector2(160f, 22f));
            }
            populationValue = ApplyRowLayout("Population", 124f, populationValue);
            woodValue = ApplyRowLayout("Wood", 88f, woodValue);
            goldValue = ApplyRowLayout("Gold", 52f, goldValue);
            foodValue = ApplyRowLayout("Food", 16f, foodValue);
        }

        private Text ApplyRowLayout(string name, float y, Text value)
        {
            RectTransform row = root.Find(name) as RectTransform;
            if (row == null) return value;
            RtsUiFactory.Anchor(row, Vector2.zero, Vector2.zero,
                new Vector2(18f, y), new Vector2(184f, RowHeight));
            RectTransform icon = row.Find("Icon") as RectTransform;
            if (icon != null)
                RtsUiFactory.Anchor(icon, new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), Vector2.zero,
                    new Vector2(IconSize, IconSize));
            if (value == null) value = row.Find("Value")?.GetComponent<Text>();
            if (value != null)
            {
                value.fontSize = ValueFontSize;
                value.fontStyle = FontStyle.Bold;
                RtsUiFactory.Anchor(value.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(46f, 0f),
                    new Vector2(136f, 34f));
            }
            return value;
        }

        private Text AddRow(string name, Sprite iconSprite, float y)
        {
            RectTransform row = RtsUiFactory.Rect(name, root);
            RtsUiFactory.Anchor(row, Vector2.zero, Vector2.zero,
                new Vector2(18f, y), new Vector2(184f, RowHeight));
            Image icon = RtsUiFactory.Image("Icon", row, iconSprite);
            RtsUiFactory.Anchor(icon.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(IconSize, IconSize));
            Text value = RtsUiFactory.Label("Value", row, "0", ValueFontSize,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            RtsUiFactory.Anchor(value.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(136f, 34f));
            return value;
        }

        private void Refresh()
        {
            if (root == null || stockpile == null || population == null) return;
            root.gameObject.SetActive(true);
            populationValue.text = $"{population.Used}/{population.Cap}";
            woodValue.text = stockpile.GetAmount(ResourceType.Wood).ToString();
            goldValue.text = stockpile.GetAmount(ResourceType.Gold).ToString();
            foodValue.text = stockpile.GetAmount(ResourceType.Food).ToString();
        }
    }
}
