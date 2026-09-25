using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.Buildings
{
    [RequireComponent(typeof(Canvas))]
    public sealed class ProductionUI : MonoBehaviour
    {
        private static readonly UnitType[] Units =
            { UnitType.Pawn, UnitType.Warrior, UnitType.Lancer, UnitType.Archer, UnitType.Monk };
        private readonly Button[] buttons = new Button[5];
        private readonly Image[] backgrounds = new Image[5];
        private readonly Image[] icons = new Image[5];
        private readonly Text[] labels = new Text[5];
        private readonly Sprite[] unitIcons = new Sprite[5];
        private SelectionManager selection;
        private UnitProductionQueue queue;
        private RectTransform panel;
        private Text title;
        private Text status;

        public void Configure(SelectionManager selected, Sprite pawn, Sprite warrior,
            Sprite lancer, Sprite archer, Sprite monk)
        {
            selection = selected;
            unitIcons[0] = pawn;
            unitIcons[1] = warrior;
            unitIcons[2] = lancer;
            unitIcons[3] = archer;
            unitIcons[4] = monk;
            Build();
            panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            IProductionHost host = selection != null
                ? (IProductionHost)selection.SelectedCastle ?? selection.SelectedBuilding
                : null;
            UnitProductionQueue current = host is Component component
                ? component.GetComponent<UnitProductionQueue>() : null;
            if (current == null)
            {
                queue = null;
                if (panel != null) panel.gameObject.SetActive(false);
                return;
            }
            if (queue != current)
            {
                queue = current;
                panel.gameObject.SetActive(true);
                title.text = host.Type.ToString() + " production";
            }
            int shown = 0;
            for (int i = 0; i < Units.Length; i++)
            {
                bool allowed = UnitProductionQueue.CanProduce(host.Type, Units[i]);
                buttons[i].gameObject.SetActive(allowed);
                if (!allowed) continue;
                UnitCombatData data = queue.GetData(Units[i]);
                bool available = queue.CanTrain(Units[i]);
                buttons[i].interactable = available;
                backgrounds[i].color = available
                    ? new Color(0.2f, 0.34f, 0.28f, 0.95f)
                    : new Color(0.27f, 0.27f, 0.27f, 0.7f);
                RectTransform rect = (RectTransform)buttons[i].transform;
                rect.anchoredPosition = new Vector2(8f + shown++ * 122f, 8f);
                icons[i].sprite = unitIcons[i];
                labels[i].text = data == null ? Units[i].ToString()
                    : $"{Units[i]}\n{Cost(data)}";
            }
            status.text = $"Queue {queue.Count}/{queue.MaxCount}: {queue.QueueSummary()}"
                + (queue.Current.HasValue ? $"  {Mathf.RoundToInt(queue.Progress * 100f)}%" : "");
        }

        private void Build()
        {
            panel = Rect("ProductionCommands", transform,
                new Vector2(622f, 130f), new Vector2(304f, 12f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.11f, 0.15f, 0.9f);
            title = Label("Title", panel, "", 16, new Vector2(602f, 22f), new Vector2(8f, 103f));
            status = Label("Queue", panel, "", 13, new Vector2(602f, 25f), new Vector2(8f, 80f));
            for (int i = 0; i < Units.Length; i++)
            {
                UnitType type = Units[i];
                RectTransform rect = Rect(type + "Button", panel,
                    new Vector2(116f, 65f), new Vector2(8f + i * 122f, 8f));
                backgrounds[i] = rect.gameObject.AddComponent<Image>();
                buttons[i] = rect.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = backgrounds[i];
                buttons[i].onClick.AddListener(() => queue?.TryTrain(type));
                RectTransform icon = Rect("Icon", rect, new Vector2(34f, 42f), new Vector2(3f, 12f));
                icons[i] = icon.gameObject.AddComponent<Image>();
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;
                labels[i] = Label("Cost", rect, "", 11, new Vector2(77f, 60f), new Vector2(38f, 2f));
            }
        }

        private static string Cost(UnitCombatData data)
        {
            string result = data.FoodCost > 0 ? data.FoodCost + " Food" : "";
            if (data.WoodCost > 0) result += (result.Length > 0 ? "\n" : "") + data.WoodCost + " Wood";
            if (data.GoldCost > 0) result += (result.Length > 0 ? "\n" : "") + data.GoldCost + " Gold";
            return result;
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
            Text text = Rect(name, parent, bounds, position).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }
    }
}
