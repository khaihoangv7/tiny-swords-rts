using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class SelectionInfoPanelPresenter : MonoBehaviour
    {
        private static readonly UnitType[] SummaryOrder =
        {
            UnitType.Pawn, UnitType.Warrior, UnitType.Archer,
            UnitType.Lancer, UnitType.Monk
        };

        private readonly int[] counts = new int[5];
        private readonly List<UnitType> queueBuffer = new(5);
        private SelectionManager selection;
        private RtsHudAssetCatalog assets;
        private RectTransform root;
        private Image portrait;
        private Text title;
        private Text details;
        private readonly GameObject[] summaryRows = new GameObject[5];
        private readonly Image[] summaryIcons = new Image[5];
        private readonly Text[] summaryLabels = new Text[5];
        private readonly Image[] queueIcons = new Image[5];
        private RectTransform queueRoot;
        private RectTransform queueFill;
        private float refreshAt;

        public RectTransform Root => root;
        public bool IsVisible => root != null && root.gameObject.activeSelf;

        public void Configure(SelectionManager manager, RtsHudAssetCatalog catalog,
            RectTransform hudParent)
        {
            if (selection != null) selection.SelectionChanged -= HandleSelectionChanged;
            selection = manager;
            assets = catalog;
            if (root == null) Build(hudParent);
            if (selection != null) selection.SelectionChanged += HandleSelectionChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            if (selection != null) selection.SelectionChanged -= HandleSelectionChanged;
        }

        private void Update()
        {
            if (root == null || !root.gameObject.activeSelf
                || Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 0.10f;
            Refresh();
        }

        private void HandleSelectionChanged()
        {
            refreshAt = 0f;
            Refresh();
        }

        private void Build(RectTransform hudParent)
        {
            root = RtsUiFactory.Rect("SelectionInfoPanel", hudParent);
            RtsUiFactory.Anchor(root, Vector2.zero, Vector2.zero,
                new Vector2(224f, 0f), new Vector2(380f, 160f));
            RtsUiFactory.BuildPanelFrame("SpecialPaper", root, assets.SpecialPaper, 0.5f);

            portrait = RtsUiFactory.Image("Portrait", root, null);
            RtsUiFactory.Anchor(portrait.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(18f, 84f), new Vector2(54f, 54f));

            title = RtsUiFactory.Label("Title", root, "", 16,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            RtsUiFactory.Anchor(title.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(82f, 125f), new Vector2(280f, 24f));
            details = RtsUiFactory.Label("Details", root, "", 14,
                TextAnchor.UpperLeft);
            RtsUiFactory.Anchor(details.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(82f, 56f), new Vector2(280f, 66f));

            for (int i = 0; i < summaryRows.Length; i++)
            {
                RectTransform row = RtsUiFactory.Rect("Summary" + i, root);
                RtsUiFactory.Anchor(row, Vector2.zero, Vector2.zero,
                    new Vector2(20f, 122f - i * 28f), new Vector2(340f, 28f));
                summaryRows[i] = row.gameObject;
                summaryIcons[i] = RtsUiFactory.Image("Avatar", row, null);
                RtsUiFactory.Anchor(summaryIcons[i].rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
                summaryLabels[i] = RtsUiFactory.Label("Count", row, "", 14,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                RtsUiFactory.Anchor(summaryLabels[i].rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(38f, 0f), new Vector2(294f, 26f));
            }

            queueRoot = RtsUiFactory.Rect("ProductionQueue", root);
            RtsUiFactory.Anchor(queueRoot, Vector2.zero, Vector2.zero,
                new Vector2(18f, 10f), new Vector2(344f, 40f));
            for (int i = 0; i < queueIcons.Length; i++)
            {
                queueIcons[i] = RtsUiFactory.Image("QueueSlot" + i, queueRoot, null);
                RtsUiFactory.Anchor(queueIcons[i].rectTransform, Vector2.zero, Vector2.zero,
                    new Vector2(i * 36f, 7f), new Vector2(30f, 30f));
            }
            RectTransform bar = RtsUiFactory.Rect("QueueProgress", queueRoot);
            RtsUiFactory.Anchor(bar, Vector2.zero, Vector2.zero,
                new Vector2(0f, 0f), new Vector2(344f, 4f));
            Image barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = new Color(0.08f, 0.08f, 0.09f, 0.85f);
            barImage.raycastTarget = false;
            queueFill = RtsUiFactory.Rect("Fill", bar);
            queueFill.anchorMin = Vector2.zero;
            queueFill.anchorMax = new Vector2(0f, 1f);
            queueFill.pivot = new Vector2(0f, 0.5f);
            queueFill.offsetMin = Vector2.zero;
            queueFill.offsetMax = Vector2.zero;
            Image fillImage = queueFill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.88f, 0.68f, 0.22f, 1f);
            fillImage.raycastTarget = false;
        }

        private void Refresh()
        {
            if (root == null || selection == null) return;
            bool hasSelection = selection.SelectedUnits.Count > 0
                || selection.SelectedBuilding != null || selection.SelectedCastle != null
                || selection.SelectedGoldMine != null || selection.SelectedResourceNode != null;
            root.gameObject.SetActive(hasSelection);
            if (!hasSelection) return;
            HideDynamicRows();

            if (selection.SelectedUnits.Count > 1) ShowUnitSummary();
            else if (selection.SelectedUnits.Count == 1)
                ShowUnit(selection.SelectedUnits[0]);
            else if (selection.SelectedBuilding != null)
                ShowBuilding(selection.SelectedBuilding);
            else if (selection.SelectedCastle != null)
                ShowCastle(selection.SelectedCastle);
            else if (selection.SelectedGoldMine != null)
                ShowGoldMine(selection.SelectedGoldMine);
            else ShowResource(selection.SelectedResourceNode);
        }

        private void HideDynamicRows()
        {
            portrait.gameObject.SetActive(false);
            title.gameObject.SetActive(false);
            details.gameObject.SetActive(false);
            queueRoot.gameObject.SetActive(false);
            foreach (GameObject row in summaryRows) row.SetActive(false);
        }

        private void ShowUnit(UnitSelection selected)
        {
            HealthComponent health = selected != null ? selected.GetComponent<HealthComponent>() : null;
            UnitController controller = selected != null ? selected.Controller : null;
            if (health == null || controller == null) return;
            UnitType type = health.CombatData.UnitType;
            ShowPrimary(assets.Avatar(type), type.ToString());
            PawnGatherer pawn = selected.GetComponent<PawnGatherer>();
            string state = pawn != null ? pawn.State.ToString() : controller.CurrentState.ToString();
            details.text = $"{controller.Faction}\nHP  {health.CurrentHP}/{health.MaxHP}\n{state}";
            if (pawn != null)
                details.text += pawn.CarriedAmount > 0
                    ? $"\nCargo  {pawn.CarriedResourceType} {pawn.CarriedAmount}/{pawn.CarryCapacity}"
                    : $"\nCargo  0/{pawn.CarryCapacity}";
        }

        private void ShowUnitSummary()
        {
            System.Array.Clear(counts, 0, counts.Length);
            foreach (UnitSelection selected in selection.SelectedUnits)
            {
                UnitType? type = selected != null
                    ? selected.GetComponent<HealthComponent>()?.CombatData?.UnitType : null;
                int index = type.HasValue ? System.Array.IndexOf(SummaryOrder, type.Value) : -1;
                if (index >= 0) counts[index]++;
            }
            int row = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] <= 0) continue;
                summaryRows[row].SetActive(true);
                summaryIcons[row].sprite = assets.Avatar(SummaryOrder[i]);
                summaryLabels[row].text = $"{SummaryOrder[i]}  x{counts[i]}";
                row++;
            }
        }

        private void ShowBuilding(BuildingSite site)
        {
            ShowPrimary(site.PresentationRenderer != null ? site.PresentationRenderer.sprite : null,
                site.Type == BuildingType.House ? $"House {site.Variant}" : site.Type.ToString());
            details.text = $"{site.Faction}\nHP  {Mathf.CeilToInt(site.CurrentHP)}/{site.MaxHP}";
            details.text += site.State == BuildingState.UnderConstruction
                ? $"\nConstruction  {Mathf.RoundToInt(site.ConstructionProgress * 100f)}%"
                : "\nCompleted";
            AddGarrisonAndQueue(site.gameObject);
        }

        private void ShowCastle(CastleDropOff castle)
        {
            ShowPrimary(castle.PresentationRenderer != null ? castle.PresentationRenderer.sprite : null,
                "Castle");
            details.text = $"{castle.Faction}\nHP  {Mathf.CeilToInt(castle.CurrentHP)}/{castle.MaxHP}";
            AddGarrisonAndQueue(castle.gameObject);
        }

        private void ShowGoldMine(GoldMineObjective mine)
        {
            SpriteRenderer visual = mine != null ? mine.GetComponentInChildren<SpriteRenderer>() : null;
            ShowPrimary(visual != null ? visual.sprite : null, "Gold Mine");
            string owner = mine.HasOwner ? mine.Faction.ToString() : "Neutral";
            details.text = $"{owner} - {mine.State}\nGold  {mine.RemainingGold}/{GoldMineObjective.MaximumGold}"
                + $"\nHP  {Mathf.CeilToInt(mine.CurrentHP)}/{mine.MaxHP}"
                + $"\nPawns  {mine.OccupantCount}/{GoldMineObjective.PawnCapacity}";
        }

        private void ShowResource(ResourceNode node)
        {
            if (node == null) return;
            ShowPrimary(node.PresentationRenderer != null ? node.PresentationRenderer.sprite : null,
                ResourceName(node.ResourceType));
            details.text = $"{node.ResourceType}  {node.RemainingAmount}/{node.MaximumAmount}";
        }

        private void AddGarrisonAndQueue(GameObject host)
        {
            BuildingGarrison garrison = host.GetComponent<BuildingGarrison>();
            if (garrison != null)
                details.text += $"\nGarrison  {garrison.InternalCount}/{garrison.InternalCapacity}"
                    + $"  Mounted {garrison.MountedCount}/{garrison.MountedCapacity}";
            UnitProductionQueue queue = host.GetComponent<UnitProductionQueue>();
            if (queue == null || queue.Count == 0) return;
            queue.CopyQueue(queueBuffer);
            queueRoot.gameObject.SetActive(true);
            for (int i = 0; i < queueIcons.Length; i++)
            {
                bool active = i < queueBuffer.Count;
                queueIcons[i].gameObject.SetActive(active);
                if (active) queueIcons[i].sprite = assets.Avatar(queueBuffer[i]);
            }
            queueFill.anchorMax = new Vector2(queue.Progress, 1f);
        }

        private void ShowPrimary(Sprite sprite, string name)
        {
            portrait.gameObject.SetActive(sprite != null);
            portrait.sprite = sprite;
            title.gameObject.SetActive(true);
            details.gameObject.SetActive(true);
            title.text = name;
        }

        private static string ResourceName(ResourceType type) => type switch
        {
            ResourceType.Wood => "Tree",
            ResourceType.Food => "Sheep",
            ResourceType.Gold => "Gold Stone",
            _ => type.ToString()
        };
    }
}
