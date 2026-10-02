using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Match;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    public sealed class CommandTooltipPresenter : MonoBehaviour
    {
        public const float MinimumWidth = 240f;
        public const float PreferredWidth = 286f;
        public const float MaximumWidth = 320f;
        public const float HorizontalPadding = 12f;
        public const float VerticalPadding = 10f;
        public const float SectionSpacing = 5f;
        public const float PointerOffset = 20f * GameCursorController.PresentationScale;
        public const float ScreenSafeMargin = 8f;

        private const float CostTitleHeight = 14f;
        private const float CostRowHeight = 26f;
        private const float CostItemWidth = 66f;
        private const float CostItemStep = 68f;

        private sealed class CostView
        {
            internal RectTransform Root;
            internal Image Icon;
            internal Text Value;
        }

        private static readonly Color InsufficientColor = new(1f, 0.48f, 0.38f, 1f);
        private readonly CostView[] costs = new CostView[4];
        private ResourceStockpile stockpile;
        private FactionPopulation population;
        private MatchManager match;
        private Canvas canvas;
        private RectTransform root;
        private RectTransform content;
        private RectTransform costRow;
        private CanvasGroup canvasGroup;
        private Text title;
        private Text costTitle;
        private Text stats;
        private Text details;
        private RtsHudAssetCatalog assets;
        private CommandTooltipTrigger currentOwner;
        private CommandTooltipData currentData;

        public RectTransform Root => root;
        public RectTransform ContentRoot => content;
        public bool IsVisible => root != null && root.gameObject.activeSelf;
        public bool HasValidReferences => root != null && content != null && title != null
            && costRow != null && stats != null && details != null
            && costs[0]?.Icon != null && costs[0].Value != null
            && canvas != null && canvasGroup != null;
        public bool UsesCompactAutoSizing => root != null
            && root.anchorMin == Vector2.zero && root.anchorMax == Vector2.zero
            && root.pivot == Vector2.zero && root.rect.width >= MinimumWidth
            && root.rect.width <= MaximumWidth;
        public bool IsPointerFollowingConfigured => enabled && canvas != null
            && PointerOffset > 0f && ScreenSafeMargin > 0f;
        public bool BlocksRaycasts
        {
            get
            {
                if (root == null) return false;
                if (canvasGroup != null && canvasGroup.blocksRaycasts) return true;
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.raycastTarget) return true;
                return false;
            }
        }

        public void Configure(RtsHudAssetCatalog catalog, ResourceStockpile resources,
            FactionPopulation cap, RectTransform hudParent)
        {
            if (match != null) match.MatchEnded -= HandleMatchEnded;
            if (stockpile != null) stockpile.Changed -= RefreshAffordability;
            if (population != null) population.Changed -= RefreshAffordability;
            assets = catalog;
            stockpile = resources;
            population = cap;
            match = MatchManager.Instance;
            canvas = GetComponent<Canvas>();
            if (root == null) Build(hudParent);
            if (match != null) match.MatchEnded += HandleMatchEnded;
            if (stockpile != null) stockpile.Changed += RefreshAffordability;
            if (population != null) population.Changed += RefreshAffordability;
            root.SetAsLastSibling();
            Hide();
        }

        internal void Show(CommandTooltipTrigger owner, CommandTooltipData data)
        {
            if (root == null || owner == null || data == null) return;
            currentOwner = owner;
            currentData = data;
            title.text = data.Title;
            stats.text = data.Stats ?? "";
            stats.gameObject.SetActive(!string.IsNullOrEmpty(data.Stats));
            details.text = data.Details ?? "";
            if (!string.IsNullOrEmpty(data.Hotkey))
                details.text += "\nHotkey: " + data.Hotkey;
            details.gameObject.SetActive(!string.IsNullOrEmpty(details.text));

            RefreshAffordability();
            root.gameObject.SetActive(true);
            ApplyContentLayout();
            root.SetAsLastSibling();
            UpdatePointerPosition();
        }

        internal void Hide(CommandTooltipTrigger owner)
        {
            if (owner != null && currentOwner != owner) return;
            Hide();
        }

        public void Hide()
        {
            currentOwner = null;
            currentData = null;
            if (root != null) root.gameObject.SetActive(false);
        }

        private void Build(RectTransform hudParent)
        {
            root = RtsUiFactory.Rect("CommandTooltip", hudParent);
            RtsUiFactory.Anchor(root, Vector2.zero, Vector2.zero,
                Vector2.zero, new Vector2(PreferredWidth, 80f));
            RtsUiFactory.BuildPanelFrame("SpecialPaper", root, assets.SpecialPaper, 0.45f);
            canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            content = RtsUiFactory.Rect("Content", root);
            RtsUiFactory.Stretch(content, HorizontalPadding, VerticalPadding,
                HorizontalPadding, VerticalPadding);

            title = RtsUiFactory.Label("Title", content, "", 18,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            ConfigureWrappedText(title);

            costTitle = RtsUiFactory.Label("CostTitle", content, "COST", 12,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            costTitle.color = RtsUiFactory.MutedTextColor;

            costRow = RtsUiFactory.Rect("CostRow", content);
            for (int i = 0; i < costs.Length; i++)
            {
                RectTransform item = RtsUiFactory.Rect("Cost" + i, costRow);
                RtsUiFactory.Anchor(item, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    Vector2.zero, new Vector2(CostItemWidth, CostRowHeight));
                Image icon = RtsUiFactory.Image("Icon", item, null);
                RtsUiFactory.Anchor(icon.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
                Text value = RtsUiFactory.Label("Value", item, "0", 15,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                RtsUiFactory.Anchor(value.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(40f, 24f));
                costs[i] = new CostView { Root = item, Icon = icon, Value = value };
            }

            stats = RtsUiFactory.Label("Stats", content, "", 14,
                TextAnchor.UpperLeft, FontStyle.Bold);
            ConfigureWrappedText(stats);
            details = RtsUiFactory.Label("Details", content, "", 14,
                TextAnchor.UpperLeft, FontStyle.Normal);
            ConfigureWrappedText(details);

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            root.gameObject.SetActive(false);
        }

        private static void ConfigureWrappedText(Text label)
        {
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void ApplyContentLayout()
        {
            float width = DetermineWidth();
            float innerWidth = width - HorizontalPadding * 2f;
            float top = 0f;
            bool hasSection = false;

            PlaceText(title, innerWidth, ref top, ref hasSection);
            if (costRow.gameObject.activeSelf)
            {
                AddSectionSpacing(ref top, hasSection);
                PlaceFixed(costTitle.rectTransform, innerWidth, CostTitleHeight, ref top);
                top += 2f;
                PlaceFixed(costRow, innerWidth, CostRowHeight, ref top);
                LayoutCostItems(innerWidth);
                hasSection = true;
            }
            if (stats.gameObject.activeSelf)
            {
                AddSectionSpacing(ref top, hasSection);
                PlaceText(stats, innerWidth, ref top, ref hasSection);
            }
            if (details.gameObject.activeSelf)
            {
                AddSectionSpacing(ref top, hasSection);
                PlaceText(details, innerWidth, ref top, ref hasSection);
            }

            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Ceil(top + VerticalPadding * 2f));
        }

        private float DetermineWidth()
        {
            bool detailed = costRow.gameObject.activeSelf || stats.gameObject.activeSelf;
            float desired = detailed ? PreferredWidth : MinimumWidth;
            desired = Mathf.Max(desired, title.preferredWidth + HorizontalPadding * 2f);
            int costCount = 0;
            foreach (CostView cost in costs)
                if (cost.Root.gameObject.activeSelf) costCount++;
            if (costCount > 0)
                desired = Mathf.Max(desired,
                    costCount * CostItemStep + HorizontalPadding * 2f);
            return Mathf.Clamp(Mathf.Ceil(desired), MinimumWidth, MaximumWidth);
        }

        private static void AddSectionSpacing(ref float top, bool hasSection)
        {
            if (hasSection) top += SectionSpacing;
        }

        private static void PlaceText(Text label, float width, ref float top,
            ref bool hasSection)
        {
            RtsUiFactory.Anchor(label.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(width, 1f));
            label.SetVerticesDirty();
            float height = Mathf.Ceil(Mathf.Max(label.fontSize + 2f, label.preferredHeight));
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            top += height;
            hasSection = true;
        }

        private static void PlaceFixed(RectTransform rect, float width, float height,
            ref float top)
        {
            RtsUiFactory.Anchor(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -top), new Vector2(width, height));
            top += height;
        }

        private void LayoutCostItems(float innerWidth)
        {
            int count = 0;
            foreach (CostView cost in costs)
                if (cost.Root.gameObject.activeSelf) count++;
            float totalWidth = count > 0 ? CostItemWidth + (count - 1) * CostItemStep : 0f;
            float x = Mathf.Max(0f, (innerWidth - totalWidth) * 0.5f);
            foreach (CostView cost in costs)
            {
                if (!cost.Root.gameObject.activeSelf) continue;
                RtsUiFactory.Anchor(cost.Root, new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(x, 0f),
                    new Vector2(CostItemWidth, CostRowHeight));
                x += CostItemStep;
            }
        }

        private int BindCost(int index, Sprite icon, int amount, bool affordable)
        {
            if (amount <= 0) return index;
            CostView view = costs[index];
            view.Root.gameObject.SetActive(true);
            view.Icon.sprite = icon;
            view.Value.text = amount.ToString();
            view.Value.color = affordable ? RtsUiFactory.TextColor : InsufficientColor;
            return index + 1;
        }

        private bool HasResource(ResourceType type, int amount)
            => amount <= 0 || stockpile == null || stockpile.GetAmount(type) >= amount;

        private bool HasPopulation(int amount)
            => amount <= 0 || population == null
                || population.Used + population.Reserved + amount <= population.Cap;

        private void RefreshAffordability()
        {
            if (currentData == null || root == null) return;
            int shown = 0;
            shown = BindCost(shown, assets.FoodIcon, currentData.FoodCost,
                HasResource(ResourceType.Food, currentData.FoodCost));
            shown = BindCost(shown, assets.WoodIcon, currentData.WoodCost,
                HasResource(ResourceType.Wood, currentData.WoodCost));
            shown = BindCost(shown, assets.GoldIcon, currentData.GoldCost,
                HasResource(ResourceType.Gold, currentData.GoldCost));
            shown = BindCost(shown, assets.PopulationIcon, currentData.PopulationCost,
                HasPopulation(currentData.PopulationCost));
            for (int i = shown; i < costs.Length; i++)
                costs[i].Root.gameObject.SetActive(false);
            bool hasCost = shown > 0;
            costTitle.gameObject.SetActive(hasCost);
            costRow.gameObject.SetActive(hasCost);
        }

        private void UpdatePointerPosition()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || root == null || root.parent is not RectTransform parent) return;
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            Vector2 pointerScreen = mouse.position.ReadValue();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, pointerScreen, eventCamera, out Vector2 pointer)) return;
            if (!TryGetScreenBounds(parent, eventCamera, out Rect bounds)) return;

            float width = root.rect.width;
            float height = root.rect.height;
            float left = pointer.x + PointerOffset;
            float bottom = pointer.y + PointerOffset;
            if (left + width > bounds.xMax - ScreenSafeMargin)
                left = pointer.x - PointerOffset - width;
            if (bottom + height > bounds.yMax - ScreenSafeMargin)
                bottom = pointer.y - PointerOffset - height;

            float minX = bounds.xMin + ScreenSafeMargin;
            float maxX = Mathf.Max(minX, bounds.xMax - ScreenSafeMargin - width);
            float minY = bounds.yMin + ScreenSafeMargin;
            float maxY = Mathf.Max(minY, bounds.yMax - ScreenSafeMargin - height);
            left = Mathf.Clamp(left, minX, maxX);
            bottom = Mathf.Clamp(bottom, minY, maxY);

            Vector2 anchorReference = new(parent.rect.xMin, parent.rect.yMin);
            root.anchoredPosition = new Vector2(
                Mathf.Round(left - anchorReference.x),
                Mathf.Round(bottom - anchorReference.y));
        }

        private static bool TryGetScreenBounds(RectTransform parent, Camera eventCamera,
            out Rect bounds)
        {
            bool hasMin = RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                Vector2.zero, eventCamera, out Vector2 min);
            bool hasMax = RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                new Vector2(Screen.width, Screen.height), eventCamera, out Vector2 max);
            bounds = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
            return hasMin && hasMax;
        }

        private void HandleMatchEnded(MatchResult _) => Hide();

        private void LateUpdate()
        {
            if (root == null || !root.gameObject.activeSelf) return;
            if (!root.parent.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }
            UpdatePointerPosition();
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (match != null) match.MatchEnded -= HandleMatchEnded;
            if (stockpile != null) stockpile.Changed -= RefreshAffordability;
            if (population != null) population.Changed -= RefreshAffordability;
        }
    }
}
