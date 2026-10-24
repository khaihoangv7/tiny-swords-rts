using TinyKingdom.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class EconomyHUD : MonoBehaviour
    {
        private ResourceStockpile stockpile;
        private Text totals;

        public void Configure(ResourceStockpile resources)
        {
            if (stockpile != null) stockpile.Changed -= Refresh;
            stockpile = resources;
            if (stockpile != null) stockpile.Changed += Refresh;
            if (totals == null) Build();
            Refresh();
        }

        private void OnDestroy()
        {
            if (stockpile != null) stockpile.Changed -= Refresh;
        }

        private void Build()
        {
            var panelObject = new GameObject("EconomyHUD", typeof(RectTransform));
            RectTransform panel = (RectTransform)panelObject.transform;
            panel.SetParent(transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(12f, -12f);
            panel.sizeDelta = new Vector2(285f, 38f);
            Image background = panelObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.11f, 0.15f, 0.88f);
            background.raycastTarget = false;

            var textObject = new GameObject("Totals", typeof(RectTransform));
            RectTransform rect = (RectTransform)textObject.transform;
            rect.SetParent(panel, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(8f, 0f);
            rect.sizeDelta = new Vector2(270f, 32f);
            totals = textObject.AddComponent<Text>();
            totals.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            totals.fontSize = 16;
            totals.color = Color.white;
            totals.alignment = TextAnchor.MiddleLeft;
            totals.raycastTarget = false;
            UiLanguage.Bind(totals);
        }

        private void Refresh()
        {
            if (totals == null || stockpile == null) return;
            totals.text = $"Food {stockpile.GetAmount(ResourceType.Food)}   "
                + $"Wood {stockpile.GetAmount(ResourceType.Wood)}   "
                + $"Gold {stockpile.GetAmount(ResourceType.Gold)}";
        }
    }
}
