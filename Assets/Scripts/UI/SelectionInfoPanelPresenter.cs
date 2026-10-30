using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Units;
using TinyKingdom.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class SelectionInfoPanelPresenter : MonoBehaviour
    {
        public const float PanelWidth = 380f;
        public const float PanelHeight = 188f;
        public const float PortraitSize = 96f;
        public const int TitleFontSize = 24;
        public const int DetailFontSize = 16;
        public const float SummaryPreferredIconSize = 52f;
        public const float SummaryMinimumIconSize = 34f;
        public const int SummaryPreferredFontSize = 21;
        public const int SummaryMinimumFontSize = 18;
        public const float SummaryMaximumRowHeight = 54f;
        public const float SummaryContentHeight = 168f;

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
        private Image upgradeIcon;
        private RectTransform queueRoot;
        private RectTransform queueFill;
        private float refreshAt;
        private int currentSummaryRowCount;
        private float currentSummaryRowHeight;
        private float currentSummaryIconSize;
        private int currentSummaryFontSize;

        public RectTransform Root => root;
        public bool IsVisible => root != null && root.gameObject.activeSelf;
        public int CurrentSummaryRowCount => currentSummaryRowCount;
        public float CurrentSummaryRowHeight => currentSummaryRowHeight;
        public float CurrentSummaryIconSize => currentSummaryIconSize;
        public int CurrentSummaryFontSize => currentSummaryFontSize;

        public void Configure(SelectionManager manager, RtsHudAssetCatalog catalog,
            RectTransform hudParent)
        {
            if (selection != null) selection.SelectionChanged -= HandleSelectionChanged;
            selection = manager;
            assets = catalog;
            if (root == null) Build(hudParent);
            ApplyPresentationLayout();
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
                new Vector2(224f, 0f), new Vector2(PanelWidth, PanelHeight));
            RtsUiFactory.BuildPanelFrame("SpecialPaper", root, assets.SpecialPaper, 0.5f);

            portrait = RtsUiFactory.Image("Portrait", root, null);
            RtsUiFactory.Anchor(portrait.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(18f, 80f), new Vector2(PortraitSize, PortraitSize));

            title = RtsUiFactory.Label("Title", root, "", TitleFontSize,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            RtsUiFactory.Anchor(title.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(124f, 148f), new Vector2(238f, 30f));
            details = RtsUiFactory.Label("Details", root, "", DetailFontSize,
                TextAnchor.UpperLeft);
            RtsUiFactory.Anchor(details.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(124f, 56f), new Vector2(238f, 88f));

            for (int i = 0; i < summaryRows.Length; i++)
            {
                RectTransform row = RtsUiFactory.Rect("Summary" + i, root);
                RtsUiFactory.Anchor(row, Vector2.zero, Vector2.zero,
                    new Vector2(18f, 116f - i * SummaryMaximumRowHeight),
                    new Vector2(344f, SummaryMaximumRowHeight));
                summaryRows[i] = row.gameObject;
                summaryIcons[i] = RtsUiFactory.Image("Avatar", row, null);
                RtsUiFactory.Anchor(summaryIcons[i].rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), Vector2.zero,
                    new Vector2(SummaryPreferredIconSize, SummaryPreferredIconSize));
                summaryLabels[i] = RtsUiFactory.Label("Count", row, "",
                    SummaryPreferredFontSize,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                RtsUiFactory.Anchor(summaryLabels[i].rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(62f, 0f),
                    new Vector2(278f, SummaryMaximumRowHeight));
            }

            queueRoot = RtsUiFactory.Rect("ProductionQueue", root);
            RtsUiFactory.Anchor(queueRoot, Vector2.zero, Vector2.zero,
                new Vector2(18f, 10f), new Vector2(344f, 44f));
            for (int i = 0; i < queueIcons.Length; i++)
            {
                queueIcons[i] = RtsUiFactory.Image("QueueSlot" + i, queueRoot, null);
                RtsUiFactory.Anchor(queueIcons[i].rectTransform, Vector2.zero, Vector2.zero,
                    new Vector2(i * 40f, 7f), new Vector2(34f, 34f));
            }
            upgradeIcon = RtsUiFactory.Image("UpgradeSlot", queueRoot, null);
            RtsUiFactory.Anchor(upgradeIcon.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(220f, 7f), new Vector2(34f, 34f));
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

        private void ApplyPresentationLayout()
        {
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            if (portrait == null)
                portrait = root.Find("Portrait")?.GetComponent<Image>();
            if (title == null) title = root.Find("Title")?.GetComponent<Text>();
            if (details == null) details = root.Find("Details")?.GetComponent<Text>();
            if (portrait != null)
                RtsUiFactory.Anchor(portrait.rectTransform, Vector2.zero, Vector2.zero,
                    new Vector2(18f, 80f), new Vector2(PortraitSize, PortraitSize));
            if (title != null)
            {
                title.fontSize = TitleFontSize;
                RtsUiFactory.Anchor(title.rectTransform, Vector2.zero, Vector2.zero,
                    new Vector2(124f, 148f), new Vector2(238f, 30f));
            }
            if (details != null)
            {
                details.fontSize = DetailFontSize;
                RtsUiFactory.Anchor(details.rectTransform, Vector2.zero, Vector2.zero,
                    new Vector2(124f, 56f), new Vector2(238f, 88f));
            }

            for (int i = 0; i < summaryRows.Length; i++)
            {
                RectTransform row = root.Find("Summary" + i) as RectTransform;
                if (row == null) continue;
                summaryRows[i] = row.gameObject;
                summaryIcons[i] = row.Find("Avatar")?.GetComponent<Image>();
                summaryLabels[i] = row.Find("Count")?.GetComponent<Text>();
            }
            ApplySummaryLayout(2);

            queueRoot = root.Find("ProductionQueue") as RectTransform;
            if (queueRoot == null) return;
            RtsUiFactory.Anchor(queueRoot, Vector2.zero, Vector2.zero,
                new Vector2(18f, 10f), new Vector2(344f, 44f));
            for (int i = 0; i < queueIcons.Length; i++)
            {
                queueIcons[i] = queueRoot.Find("QueueSlot" + i)?.GetComponent<Image>();
                if (queueIcons[i] != null)
                    RtsUiFactory.Anchor(queueIcons[i].rectTransform, Vector2.zero,
                        Vector2.zero, new Vector2(i * 40f, 7f), new Vector2(34f, 34f));
            }
            upgradeIcon = queueRoot.Find("UpgradeSlot")?.GetComponent<Image>();
            if (upgradeIcon == null)
            {
                upgradeIcon = RtsUiFactory.Image("UpgradeSlot", queueRoot, null);
            }
            RtsUiFactory.Anchor(upgradeIcon.rectTransform, Vector2.zero, Vector2.zero,
                new Vector2(220f, 7f), new Vector2(34f, 34f));
            for (int i = 0; i < queueIcons.Length; i++)
            {
                if (queueIcons[i] == null) continue;
                int index = i;
                queueIcons[i].raycastTarget = true;
                Button button = queueIcons[i].GetComponent<Button>()
                    ?? queueIcons[i].gameObject.AddComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => CancelProduction(index));
            }
            upgradeIcon.raycastTarget = true;
            Button upgradeButton = upgradeIcon.GetComponent<Button>()
                ?? upgradeIcon.gameObject.AddComponent<Button>();
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(CancelUpgrade);
            queueFill = queueRoot.Find("QueueProgress/Fill") as RectTransform;
        }

        private void Refresh()
        {
            if (root == null || selection == null) return;
            bool hasSelection = selection.SelectedUnits.Count > 0
                || selection.SelectedBuilding != null || selection.SelectedCastle != null
                || selection.SelectedGoldMine != null || selection.SelectedResourceNode != null
                || selection.InspectedEnemyUnit != null
                || selection.InspectedEnemyBuilding != null
                || selection.InspectedEnemyCastle != null;
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
            else if (selection.InspectedEnemyUnit != null)
                ShowUnit(selection.InspectedEnemyUnit);
            else if (selection.InspectedEnemyBuilding != null)
                ShowBuilding(selection.InspectedEnemyBuilding);
            else if (selection.InspectedEnemyCastle != null)
                ShowCastle(selection.InspectedEnemyCastle);
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
                    ? $"\nCargo  {pawn.CarriedResourceType} {pawn.CarriedAmount}/{pawn.DisplayCarryCapacity}"
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
            ApplySummaryLayout(row);
        }

        private void ApplySummaryLayout(int rowCount)
        {
            currentSummaryRowCount = Mathf.Clamp(rowCount, 0, summaryRows.Length);
            if (currentSummaryRowCount <= 0) return;
            currentSummaryRowHeight = CalculateSummaryRowHeight(currentSummaryRowCount);
            currentSummaryIconSize = CalculateSummaryIconSize(currentSummaryRowCount);
            currentSummaryFontSize = CalculateSummaryFontSize(currentSummaryRowCount);
            float totalHeight = currentSummaryRowCount * currentSummaryRowHeight;
            float bottom = Mathf.Round((PanelHeight - totalHeight) * 0.5f);

            for (int i = 0; i < currentSummaryRowCount; i++)
            {
                RectTransform row = summaryRows[i].transform as RectTransform;
                float y = bottom + (currentSummaryRowCount - i - 1)
                    * currentSummaryRowHeight;
                RtsUiFactory.Anchor(row, Vector2.zero, Vector2.zero,
                    new Vector2(18f, y), new Vector2(344f, currentSummaryRowHeight));
                RtsUiFactory.Anchor(summaryIcons[i].rectTransform,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                    new Vector2(currentSummaryIconSize, currentSummaryIconSize));
                summaryLabels[i].fontSize = currentSummaryFontSize;
                RtsUiFactory.Anchor(summaryLabels[i].rectTransform,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(currentSummaryIconSize + 10f, 0f),
                    new Vector2(326f - currentSummaryIconSize,
                        currentSummaryRowHeight));
            }
        }

        public static float CalculateSummaryRowHeight(int rowCount)
            => rowCount <= 0 ? 0f : Mathf.Min(SummaryMaximumRowHeight,
                SummaryContentHeight / Mathf.Clamp(rowCount, 1, 5));

        public static float CalculateSummaryIconSize(int rowCount)
            => Mathf.Clamp(CalculateSummaryRowHeight(rowCount) - 2f,
                SummaryMinimumIconSize, SummaryPreferredIconSize);

        public static int CalculateSummaryFontSize(int rowCount)
            => rowCount <= 2 ? SummaryPreferredFontSize
                : rowCount <= 4 ? 19 : SummaryMinimumFontSize;

        private void ShowBuilding(BuildingSite site)
        {
            ShowPrimary(site.PresentationRenderer != null ? site.PresentationRenderer.sprite : null,
                (site.Type == BuildingType.House ? $"House {site.Variant}" : site.Type.ToString())
                    + (selection.SelectedBuildings.Count > 1
                        ? $" x{selection.SelectedBuildings.Count}" : ""));
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
            BuildingDefenseUpgrade upgrade = host.GetComponent<BuildingDefenseUpgrade>();
            if (upgrade != null)
                details.text += upgrade.Purchased
                    ? upgrade.GetComponent<CastleDropOff>() != null
                        ? "\nCannon upgraded" : "\nAuto arrow upgraded"
                    : upgrade.IsUpgrading
                        ? $"\nUpgrading  {Mathf.RoundToInt(upgrade.Progress * 100f)}%"
                            + $" ({Mathf.CeilToInt(upgrade.SecondsRemaining)}s)"
                        : "";
            UnitProductionQueue queue = host.GetComponent<UnitProductionQueue>();
            if (queue != null) queue.CopyQueue(queueBuffer);
            else queueBuffer.Clear();
            bool showUpgrade = upgrade != null && upgrade.IsUpgrading;
            if (queueBuffer.Count == 0 && !showUpgrade) return;
            queueRoot.gameObject.SetActive(true);
            for (int i = 0; i < queueIcons.Length; i++)
            {
                bool active = i < queueBuffer.Count;
                queueIcons[i].gameObject.SetActive(active);
                if (active) queueIcons[i].sprite = assets.Avatar(queueBuffer[i]);
            }
            upgradeIcon.gameObject.SetActive(showUpgrade);
            if (showUpgrade)
            {
                Sprite[] dynamite = Phase6C2AssetCatalog.Load()?.Dynamite;
                upgradeIcon.sprite = host.GetComponent<CastleDropOff>() != null
                    ? dynamite != null && dynamite.Length > 0 ? dynamite[0] : null
                    : host.GetComponent<BuildingDefense>()?.ArrowSprite;
            }
            queueFill.anchorMax = new Vector2(queueBuffer.Count > 0 ? queue.Progress
                : upgrade.Progress, 1f);
        }

        private GameObject SelectedProductionHost()
            => selection?.SelectedBuilding != null
                ? selection.SelectedBuilding.gameObject
                : selection?.SelectedCastle != null
                    ? selection.SelectedCastle.gameObject : null;

        private void CancelProduction(int index)
        {
            GameObject host = SelectedProductionHost();
            UnitProductionQueue queue = host != null
                ? host.GetComponent<UnitProductionQueue>() : null;
            if (queue == null || !queue.TryCancelAt(index, selection.PlayerFaction)) return;
            Refresh();
        }

        private void CancelUpgrade()
        {
            GameObject host = SelectedProductionHost();
            BuildingDefenseUpgrade upgrade = host != null
                ? host.GetComponent<BuildingDefenseUpgrade>() : null;
            if (upgrade == null || !upgrade.TryCancel(selection.PlayerFaction)) return;
            Refresh();
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
