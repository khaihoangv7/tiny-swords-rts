using System.Collections.Generic;
using TinyKingdom.Input;
using TinyKingdom.Economy;
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
        private Text cargoLabel;
        private PawnGatherer inspectedPawn;
        private GameObject guardButton;
        private Image guardBackground;
        private HealthComponent inspectedHealth;
        private Font font;
        private bool subscribed;

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
            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            if (panel != null) Subscribe();
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            if (inspectedHealth != null) inspectedHealth.HealthChanged -= RefreshHealth;
        }

        private void Subscribe()
        {
            if (subscribed || selectionManager == null) return;
            selectionManager.SelectionChanged += Refresh;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || selectionManager == null) return;
            selectionManager.SelectionChanged -= Refresh;
            subscribed = false;
        }

        private void Update()
        {
            if (cargoLabel != null && inspectedPawn != null)
                cargoLabel.text = inspectedPawn.CarriedAmount > 0
                    ? $"Cargo: {inspectedPawn.CarriedResourceType} "
                        + $"{inspectedPawn.CarriedAmount}/{inspectedPawn.CarryCapacity}"
                    : $"Cargo: 0/{inspectedPawn.CarryCapacity}";
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
            panel = MakeRect("SelectionSummary", transform, new Vector2(280, 244),
                new Vector2(12, 12));
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.11f, 0.15f, 0.88f);
            hpLabel = MakeText("HP", panel, "", 16, new Vector2(250, 24), new Vector2(12, 52));
            cargoLabel = MakeText("Cargo", panel, "", 16,
                new Vector2(250, 24), new Vector2(12, 26));

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
            inspectedPawn = null;
            foreach (GameObject row in rows) if (row != null) Destroy(row);
            rows.Clear();
            if (selectionManager == null || selectionManager.SelectedUnits.Count == 0)
            {
                panel.gameObject.SetActive(false);
                return;
            }
            panel.gameObject.SetActive(true);
            int[] counts = new int[3];
            int pawnCount = 0;
            Sprite pawnPortrait = null;
            foreach (UnitSelection unit in selectionManager.SelectedUnits)
            {
                PawnGatherer pawn = unit != null ? unit.GetComponent<PawnGatherer>() : null;
                if (pawn != null)
                {
                    pawnCount++;
                    pawnPortrait ??= unit.GetComponent<SpriteRenderer>()?.sprite;
                }
                UnitCombatData data = unit != null ? unit.GetComponent<HealthComponent>()?.CombatData : null;
                if (data != null && data.UnitType != UnitType.Pawn)
                    counts[(int)data.UnitType]++;
            }
            int rowIndex = 0;
            if (pawnCount > 0)
            {
                RectTransform row = MakeRect("Pawn", panel, new Vector2(250, 34),
                    new Vector2(12, 200 - rowIndex++ * 38));
                rows.Add(row.gameObject);
                AddPortrait(row, pawnPortrait);
                MakeText("Count", row, $"Pawn x{pawnCount}", 16,
                    new Vector2(200, 32), new Vector2(40, 0)).raycastTarget = false;
            }
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                UnitType type = (UnitType)i;
                RectTransform row = MakeRect(type.ToString(), panel, new Vector2(250, 34),
                    new Vector2(12, 200 - rowIndex++ * 38));
                rows.Add(row.gameObject);
                UnitAnimationSet set = type switch
                {
                    UnitType.Warrior => warriorVisuals,
                    UnitType.Archer => archerVisuals,
                    _ => lancerVisuals
                };
                if (set != null && set.Idle != null && set.Idle.Length > 0)
                {
                    AddPortrait(row, set.Idle[0]);
                }
                MakeText("Count", row, $"{type} x{counts[i]}", 16,
                    new Vector2(200, 32), new Vector2(40, 0)).raycastTarget = false;
            }
            if (selectionManager.SelectedUnits.Count == 1)
            {
                inspectedHealth = selectionManager.SelectedUnits[0].GetComponent<HealthComponent>();
                inspectedPawn = selectionManager.SelectedUnits[0].GetComponent<PawnGatherer>();
                if (inspectedHealth != null) inspectedHealth.HealthChanged += RefreshHealth;
            }
            RefreshHealth(inspectedHealth);
            cargoLabel.gameObject.SetActive(inspectedPawn != null);
            guardButton.SetActive(counts[(int)UnitType.Warrior] > 0);
        }

        private void RefreshHealth(HealthComponent health)
        {
            hpLabel.text = health != null && health.IsAlive
                ? $"{health.CombatData.UnitType}  HP {health.CurrentHP}/{health.MaxHP}"
                : "";
        }

        private static void AddPortrait(RectTransform row, Sprite sprite)
        {
            if (sprite == null) return;
            RectTransform icon = MakeRect("Portrait", row, new Vector2(30, 30),
                new Vector2(0, 2));
            Image portrait = icon.gameObject.AddComponent<Image>();
            portrait.sprite = sprite;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
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
