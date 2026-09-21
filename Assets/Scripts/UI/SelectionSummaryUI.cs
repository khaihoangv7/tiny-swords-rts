using System.Collections.Generic;
using TinyKingdom.Input;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class SelectionSummaryUI : MonoBehaviour
    {
        [SerializeField] private SelectionManager selectionManager;
        [SerializeField] private GuardInputController guardInput;
        [SerializeField] private UnitAnimationSet warriorVisuals;
        [SerializeField] private UnitAnimationSet archerVisuals;
        [SerializeField] private UnitAnimationSet lancerVisuals;
        [SerializeField] private Sprite guardIcon;

        private readonly List<GameObject> rows = new();
        private RectTransform panel;
        private Text hpLabel;
        private GameObject guardButton;
        private Image guardBackground;
        private HealthComponent inspectedHealth;
        private Font font;

        public void Configure(SelectionManager selection, GuardInputController input,
            UnitAnimationSet warrior, UnitAnimationSet archer, UnitAnimationSet lancer,
            Sprite shield)
        {
            selectionManager = selection;
            guardInput = input;
            warriorVisuals = warrior;
            archerVisuals = archer;
            lancerVisuals = lancer;
            guardIcon = shield;
        }

        private void Awake()
        {
            if (selectionManager == null) selectionManager = FindAnyObjectByType<SelectionManager>();
            if (guardInput == null) guardInput = FindAnyObjectByType<GuardInputController>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildPanel();
            if (selectionManager != null) selectionManager.SelectionChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (selectionManager != null) selectionManager.SelectionChanged -= Refresh;
            if (inspectedHealth != null) inspectedHealth.HealthChanged -= RefreshHealth;
        }

        private void Update()
        {
            if (guardButton == null || !guardButton.activeSelf) return;
            bool active = false;
            foreach (UnitSelection unit in selectionManager.SelectedUnits)
                if (unit != null && unit.GetComponent<UnitGuard>()?.IsGuarding == true)
                {
                    active = true;
                    break;
                }
            guardBackground.color = active
                ? new Color(0.25f, 0.65f, 0.95f, 0.95f)
                : new Color(0.15f, 0.25f, 0.4f, 0.95f);
        }

        private void BuildPanel()
        {
            panel = MakeRect("SelectionSummary", transform, new Vector2(280, 192),
                new Vector2(12, 12));
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.11f, 0.15f, 0.88f);
            hpLabel = MakeText("HP", panel, "", 16, new Vector2(250, 24), new Vector2(12, 52));

            RectTransform buttonRect = MakeRect("GuardButton", panel, new Vector2(122, 36),
                new Vector2(146, 8));
            guardBackground = buttonRect.gameObject.AddComponent<Image>();
            var hold = buttonRect.gameObject.AddComponent<GuardButtonHold>();
            hold.Configure(guardInput);
            if (guardIcon != null)
            {
                RectTransform icon = MakeRect("Shield", buttonRect, new Vector2(28, 28),
                    new Vector2(5, 4));
                Image image = icon.gameObject.AddComponent<Image>();
                image.sprite = guardIcon;
                image.raycastTarget = false;
            }
            MakeText("Label", buttonRect, "Guard (G)", 14,
                new Vector2(86, 32), new Vector2(34, 2)).raycastTarget = false;
            guardButton = buttonRect.gameObject;
        }

        private void Refresh()
        {
            if (inspectedHealth != null) inspectedHealth.HealthChanged -= RefreshHealth;
            inspectedHealth = null;
            foreach (GameObject row in rows) if (row != null) Destroy(row);
            rows.Clear();
            if (selectionManager == null || selectionManager.SelectedUnits.Count == 0)
            {
                panel.gameObject.SetActive(false);
                return;
            }
            panel.gameObject.SetActive(true);
            int[] counts = new int[3];
            foreach (UnitSelection unit in selectionManager.SelectedUnits)
            {
                UnitCombatData data = unit != null ? unit.GetComponent<HealthComponent>()?.CombatData : null;
                if (data != null) counts[(int)data.UnitType]++;
            }
            for (int i = 0, rowIndex = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                UnitType type = (UnitType)i;
                RectTransform row = MakeRect(type.ToString(), panel, new Vector2(250, 34),
                    new Vector2(12, 148 - rowIndex++ * 38));
                rows.Add(row.gameObject);
                UnitAnimationSet set = type switch
                {
                    UnitType.Warrior => warriorVisuals,
                    UnitType.Archer => archerVisuals,
                    _ => lancerVisuals
                };
                if (set != null && set.Idle != null && set.Idle.Length > 0)
                {
                    RectTransform icon = MakeRect("Portrait", row, new Vector2(30, 30),
                        new Vector2(0, 2));
                    Image portrait = icon.gameObject.AddComponent<Image>();
                    portrait.sprite = set.Idle[0];
                    portrait.preserveAspect = true;
                    portrait.raycastTarget = false;
                }
                MakeText("Count", row, $"{type} x{counts[i]}", 16,
                    new Vector2(200, 32), new Vector2(40, 0)).raycastTarget = false;
            }
            if (selectionManager.SelectedUnits.Count == 1)
            {
                inspectedHealth = selectionManager.SelectedUnits[0].GetComponent<HealthComponent>();
                if (inspectedHealth != null) inspectedHealth.HealthChanged += RefreshHealth;
            }
            RefreshHealth(inspectedHealth);
            guardButton.SetActive(counts[(int)UnitType.Warrior] > 0);
        }

        private void RefreshHealth(HealthComponent health)
        {
            hpLabel.text = health != null && health.IsAlive
                ? $"{health.CombatData.UnitType}  HP {health.CurrentHP}/{health.MaxHP}"
                : "";
        }

        private static RectTransform MakeRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private Text MakeText(string name, Transform parent, string value, int size,
            Vector2 bounds, Vector2 position)
        {
            RectTransform rect = MakeRect(name, parent, bounds, position);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = value;
            return label;
        }
    }
}
