using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class ResourcePanelPresenter : MonoBehaviour
    {
        private ResourceStockpile stockpile;
        private FactionPopulation population;
        private RectTransform root;
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
                Vector2.zero, new Vector2(220f, 160f));
            RtsUiFactory.BuildPanelFrame("WoodTable", root, assets.WoodTable, 0.38f);

            Text title = RtsUiFactory.Label("Title", root, "RESOURCES", 16,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RtsUiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(150f, 18f));

            populationValue = AddRow("Population", assets.PopulationIcon, 102f);
            woodValue = AddRow("Wood", assets.WoodIcon, 72f);
            goldValue = AddRow("Gold", assets.GoldIcon, 42f);
            foodValue = AddRow("Food", assets.FoodIcon, 12f);
        }

        private Text AddRow(string name, Sprite iconSprite, float y)
        {
            RectTransform row = RtsUiFactory.Rect(name, root);
            RtsUiFactory.Anchor(row, Vector2.zero, Vector2.zero,
                new Vector2(18f, y), new Vector2(184f, 30f));
            Image icon = RtsUiFactory.Image("Icon", row, iconSprite);
            RtsUiFactory.Anchor(icon.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
            Text value = RtsUiFactory.Label("Value", row, "0", 18,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            RtsUiFactory.Anchor(value.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(38f, 0f), new Vector2(140f, 28f));
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
