using TinyKingdom.Input;
using TinyKingdom.Economy;
using TinyKingdom.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.Buildings
{
    [RequireComponent(typeof(Canvas))]
    public sealed class GarrisonUI : MonoBehaviour
    {
        private SelectionManager selection;
        private BuildingGarrison current;
        private GoldMineObjective currentMine;
        private RectTransform panel;
        private Text status;
        private Button eject;

        public void Configure(SelectionManager manager)
        {
            selection = manager;
            Build();
            selection.SelectionChanged += RefreshSelection;
            RefreshSelection();
        }

        private void OnDestroy()
        {
            if (selection != null) selection.SelectionChanged -= RefreshSelection;
            if (current != null) current.Changed -= Refresh;
            if (currentMine != null) currentMine.Changed -= Refresh;
        }

        private void Update()
        {
            if (current == null && currentMine == null && panel != null && panel.gameObject.activeSelf)
                panel.gameObject.SetActive(false);
            else if (current != null || currentMine != null) Refresh();
        }

        private void RefreshSelection()
        {
            if (current != null) current.Changed -= Refresh;
            if (currentMine != null) currentMine.Changed -= Refresh;
            current = selection.SelectedBuilding != null
                ? selection.SelectedBuilding.GetComponent<BuildingGarrison>()
                : selection.SelectedCastle != null
                    ? selection.SelectedCastle.GetComponent<BuildingGarrison>() : null;
            currentMine = selection.SelectedGoldMine;
            if (current != null) current.Changed += Refresh;
            if (currentMine != null) currentMine.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            bool visible = current != null || currentMine != null;
            panel.gameObject.SetActive(visible);
            if (!visible) return;
            if (currentMine != null)
            {
                status.text = $"Gold Mine - {currentMine.State}"
                    + $" ({(currentMine.HasOwner ? currentMine.Faction.ToString() : "Neutral")})"
                    + $"\nHP: {Mathf.CeilToInt(currentMine.CurrentHP)} / {currentMine.MaxHP}"
                    + $"\nPawns: {currentMine.OccupantCount} / {GoldMineObjective.PawnCapacity}"
                    + $"  Gold: {currentMine.RemainingGold}";
                eject.interactable = currentMine.OccupantCount > 0;
            }
            else
            {
                status.text = $"HP: {Mathf.CeilToInt(current.CurrentHP)} / {current.MaxHP}"
                    + $"\nInternal Garrison: {current.InternalCount} / {current.InternalCapacity}"
                    + $"\nMounted Archers: {current.MountedCount} / {current.MountedCapacity}";
                eject.interactable = current.InternalCount + current.MountedCount > 0;
            }
        }

        private void Build()
        {
            panel = Rect("GarrisonPanel", transform, new Vector2(260f, 112f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-12f, -12f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.11f, 0.15f, 0.9f);
            status = Label("Counts", panel, new Vector2(242f, 68f), new Vector2(9f, 38f));
            RectTransform buttonRect = Rect("EjectAll", panel, new Vector2(110f, 28f));
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = Vector2.zero;
            buttonRect.anchoredPosition = new Vector2(9f, 7f);
            Image background = buttonRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.2f, 0.34f, 0.28f, 0.95f);
            eject = buttonRect.gameObject.AddComponent<Button>();
            eject.targetGraphic = background;
            eject.onClick.AddListener(() =>
            {
                if (currentMine != null) currentMine.EjectAll(); else current?.EjectAll();
            });
            Sprite ejectIcon = RtsHudAssetCatalog.Load()?.EjectAllIcon;
            if (ejectIcon != null)
            {
                RectTransform iconRect = Rect("Icon", buttonRect, new Vector2(22f, 22f));
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = Vector2.zero;
                iconRect.anchoredPosition = new Vector2(4f, 3f);
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = ejectIcon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            Text label = Label("Label", buttonRect, new Vector2(82f, 28f),
                new Vector2(27f, 0f));
            label.text = "Eject All";
            label.alignment = TextAnchor.MiddleCenter;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }

        private static Text Label(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform rect = Rect(name, parent, size);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }
    }
}
