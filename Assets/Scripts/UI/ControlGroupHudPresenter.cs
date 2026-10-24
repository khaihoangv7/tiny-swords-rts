using System.Collections.Generic;
using TinyKingdom.Input;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    public sealed class ControlGroupHudPresenter : MonoBehaviour
    {
        private static readonly UnitType[] Types =
        {
            UnitType.Pawn, UnitType.Warrior, UnitType.Archer,
            UnitType.Lancer, UnitType.Monk
        };
        private readonly List<UnitSelection> buffer = new(32);
        private readonly RectTransform[] cards = new RectTransform[4];
        private readonly Image[] backgrounds = new Image[4];
        private readonly Image[,] icons = new Image[4, 5];
        private readonly Text[,] counts = new Text[4, 5];
        private readonly int[] typeCounts = new int[5];
        private SelectionManager selection;
        private RectTransform root;
        private float nextRefresh;

        public void Configure(SelectionManager manager, RtsHudAssetCatalog catalog)
        {
            if (selection != null) selection.SelectionChanged -= Refresh;
            selection = manager;
            selection.SelectionChanged -= Refresh;
            selection.SelectionChanged += Refresh;
            if (root != null) Destroy(root.gameObject);
            root = RtsUiFactory.Rect("ControlGroups", transform);
            RtsUiFactory.Anchor(root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -130f), new Vector2(108f, 690f));
            for (int group = 0; group < 4; group++)
            {
                int selectedGroup = group;
                RectTransform row = RtsUiFactory.Rect("Group" + group, root);
                RtsUiFactory.Anchor(row, new Vector2(0f, 1f),
                    new Vector2(0f, 1f), Vector2.zero,
                    new Vector2(108f, 40f));
                cards[group] = row;
                RtsUiFactory.BuildPanelFrame("GroupPaper", row,
                    catalog.SpecialPaper, 0.18f);
                Image background = row.Find("GroupPaperSurface")
                    .GetComponent<Image>();
                background.color = new Color(0.85f, 0.82f, 0.72f, 0.94f);
                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.onClick.AddListener(() => selection.SelectControlGroup(selectedGroup));
                backgrounds[group] = background;
                Text number = RtsUiFactory.Label("Number", row,
                    (group + 1).ToString(), 18, TextAnchor.MiddleCenter, FontStyle.Bold);
                number.color = new Color(0.19f, 0.14f, 0.1f);
                number.GetComponent<Outline>().enabled = false;
                RtsUiFactory.Anchor(number.rectTransform, new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(4f, -3f),
                    new Vector2(99f, 25f));
                for (int type = 0; type < Types.Length; type++)
                {
                    Image icon = RtsUiFactory.Image("Type" + type, row,
                        catalog.Avatar(Types[type]));
                    RtsUiFactory.Anchor(icon.rectTransform, new Vector2(0f, 1f),
                        new Vector2(0f, 1f), new Vector2(12f, -28f),
                        new Vector2(32f, 32f));
                    Text count = RtsUiFactory.Label("Count", icon.transform,
                        "", 16, TextAnchor.MiddleLeft, FontStyle.Bold);
                    count.color = number.color;
                    count.GetComponent<Outline>().enabled = false;
                    RtsUiFactory.Anchor(count.rectTransform, new Vector2(1f, 0.5f),
                        new Vector2(0f, 0.5f), new Vector2(5f, 0f),
                        new Vector2(53f, 30f));
                    icons[group, type] = icon;
                    counts[group, type] = count;
                }
            }
            Refresh();
        }

        private void OnDestroy()
        {
            if (selection != null) selection.SelectionChanged -= Refresh;
        }

        private void Update()
        {
            if (root == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }

        private void Refresh()
        {
            if (selection == null || root == null) return;
            root.anchorMin = root.anchorMax = RtsUiFactory.SafeCorner(false, true);
            float top = 0f;
            for (int group = 0; group < 4; group++)
            {
                System.Array.Clear(typeCounts, 0, typeCounts.Length);
                int total = selection.CopyControlGroup(group, buffer);
                for (int i = 0; i < buffer.Count; i++)
                {
                    UnitType? type = buffer[i] != null
                        ? buffer[i].GetComponent<HealthComponent>()?.CombatData?.UnitType
                        : null;
                    for (int j = 0; j < Types.Length; j++)
                        if (type == Types[j]) { typeCounts[j]++; break; }
                }
                int visibleTypes = 0;
                for (int type = 0; type < Types.Length; type++)
                    if (typeCounts[type] > 0) visibleTypes++;
                cards[group].gameObject.SetActive(total > 0 && visibleTypes > 0);
                if (total == 0 || visibleTypes == 0) continue;
                float height = 30f + visibleTypes * 34f;
                cards[group].anchoredPosition = new Vector2(0f, -top);
                cards[group].sizeDelta = new Vector2(108f, height);
                top += height + 7f;
                backgrounds[group].color = selection.ActiveControlGroup == group
                    && total > 0 ? new Color(1f, 0.9f, 0.57f, 1f)
                    : new Color(0.85f, 0.82f, 0.72f, 0.94f);
                int visibleRow = 0;
                for (int type = 0; type < Types.Length; type++)
                {
                    bool present = typeCounts[type] > 0;
                    icons[group, type].gameObject.SetActive(present);
                    if (!present) continue;
                    icons[group, type].rectTransform.anchoredPosition =
                        new Vector2(12f, -28f - visibleRow * 34f);
                    counts[group, type].text = "×" + typeCounts[type];
                    visibleRow++;
                }
            }
            Canvas canvas = GetComponent<Canvas>();
            float available = (canvas != null ? canvas.GetComponent<RectTransform>().rect.height
                : 1080f) - 345f;
            root.localScale = Vector3.one * Mathf.Min(1f,
                Mathf.Max(0.68f, available / Mathf.Max(1f, top)));
        }
    }
}
