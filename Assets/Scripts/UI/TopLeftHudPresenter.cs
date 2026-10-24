using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Match;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace TinyKingdom.UI
{
    // Player presentation only: match sword and global production queue.
    public sealed class TopLeftHudPresenter : MonoBehaviour
    {
        private const int MaximumVisibleJobs = 10;

        private struct Job
        {
            public Component Host;
            public UnitType? Unit;
            public bool Upgrade;
            public int Count;
        }

        private readonly List<UnitType> queueBuffer = new(5);
        private readonly List<Job> jobs = new(24);
        private readonly Button[] jobButtons = new Button[MaximumVisibleJobs];
        private readonly Image[] jobImages = new Image[MaximumVisibleJobs];
        private readonly Text[] jobCounts = new Text[MaximumVisibleJobs];
        private SelectionManager selection;
        private RtsHudAssetCatalog assets;
        private RectTransform root;
        private RectTransform jobsRoot;
        private Text mapName;
        private Text timer;
        private Text overflow;
        private float nextRefresh;
        private int jobPage;

        public void Configure(SelectionManager manager, RtsHudAssetCatalog catalog)
        {
            selection = manager;
            assets = catalog;
            if (root != null) Destroy(root.gameObject);
            if (jobsRoot != null) Destroy(jobsRoot.gameObject);
            Transform oldMap = transform.Find("MinimapBackground");
            if (oldMap != null)
            {
                oldMap.gameObject.SetActive(false);
                Destroy(oldMap.gameObject);
            }
            Build();
            Refresh();
        }

        private void Build()
        {
            root = RtsUiFactory.Rect("TopLeftHud", transform);
            RtsUiFactory.Anchor(root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -12f), new Vector2(390f, 104f));
            // The sheet contains a faction handle, blade and tip. Slicing the
            // blade center avoids the repeating rectangular tile seams.
            RectTransform swordRect = RtsUiFactory.Rect("Sword", root);
            RtsUiFactory.Anchor(swordRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(390f, 104f));
            float row = selection.PlayerFaction == Faction.Red ? 0.6f : 0.8f;
            int piece = selection.PlayerFaction == Faction.Red ? 3 : 1;
            RawImage handle = RtsUiFactory.Rect("SwordHandle", swordRect)
                .gameObject.AddComponent<RawImage>();
            RtsUiFactory.Anchor(handle.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(43f, 0f),
                new Vector2(87f, 104f));
            handle.texture = assets.Swords;
            handle.uvRect = new Rect(22f / 448f, row, 107f / 448f, 0.2f);
            handle.raycastTarget = false;
            Image blade = RtsUiFactory.Image("SwordBlade", swordRect,
                assets.SwordParts[piece], false);
            blade.sprite = RtsUiFactory.CreateSlicedCenter(assets.SwordParts[piece], 8f);
            blade.type = Image.Type.Sliced;
            // The handle crop ends at x=130 and the tip begins at x=314.
            // Keep the blade close to the guard, then extend it beneath the
            // tip to cover the transparent edge pixels on both sprites.
            RtsUiFactory.Anchor(blade.rectTransform, new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(128f, 0f),
                new Vector2(190f, 74f));
            Image tip = RtsUiFactory.Image("SwordTip", swordRect,
                assets.SwordParts[piece + 1]);
            RtsUiFactory.Anchor(tip.rectTransform, new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f), Vector2.zero,
                new Vector2(76f, 73f));

            mapName = RtsUiFactory.Label("MapName", swordRect,
                SceneManager.GetActiveScene().name switch
                {
                    "Map" => "TWO KINGDOMS",
                    "LinearLane" => "STRAIGHT LANE",
                    "GoblinIsland" => "GOBLIN ISLAND",
                    var sceneName => sceneName
                }, 21,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RtsUiFactory.Anchor(mapName.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(115f, -17f),
                new Vector2(260f, 29f));
            mapName.color = new Color(0.15f, 0.13f, 0.12f, 1f);
            mapName.GetComponent<Outline>().enabled = false;
            timer = RtsUiFactory.Label("MatchTimer", swordRect, "00:00", 17,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RtsUiFactory.Anchor(timer.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(115f, -49f),
                new Vector2(260f, 27f));
            timer.color = mapName.color;
            timer.GetComponent<Outline>().enabled = false;

            jobsRoot = RtsUiFactory.Rect("GlobalQueue", transform);
            RtsUiFactory.Anchor(jobsRoot, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(415f, -30f),
                new Vector2(465f, 42f));
            for (int i = 0; i < MaximumVisibleJobs; i++)
            {
                int index = i;
                Image icon = RtsUiFactory.Image("Job" + i, jobsRoot, null);
                RtsUiFactory.Anchor(icon.rectTransform, new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(36f * i, 0f),
                    new Vector2(34f, 34f));
                icon.raycastTarget = true;
                Button button = icon.gameObject.AddComponent<Button>();
                button.targetGraphic = icon;
                button.onClick.AddListener(() => FocusJob(index));
                Text count = RtsUiFactory.Label("Count", icon.transform, "", 12,
                    TextAnchor.LowerRight, FontStyle.Bold);
                RtsUiFactory.Stretch(count.rectTransform);
                jobButtons[i] = button;
                jobImages[i] = icon;
                jobCounts[i] = count;
                icon.gameObject.SetActive(false);
            }
            overflow = RtsUiFactory.Label("Overflow", jobsRoot, "", 12,
                TextAnchor.MiddleRight, FontStyle.Bold);
            RtsUiFactory.Anchor(overflow.rectTransform, new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-10f, -3f),
                new Vector2(75f, 18f));
            overflow.raycastTarget = true;
            Button nextPage = overflow.gameObject.AddComponent<Button>();
            nextPage.targetGraphic = overflow;
            nextPage.onClick.AddListener(() =>
            {
                int pages = Mathf.CeilToInt(jobs.Count / (float)MaximumVisibleJobs);
                if (pages < 2) return;
                jobPage = (jobPage + 1) % pages;
                RefreshJobs();
            });
        }

        private void Update()
        {
            if (root == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }

        private void Refresh()
        {
            if (root == null || selection == null) return;
            Vector2 safeTopLeft = RtsUiFactory.SafeCorner(false, true);
            root.anchorMin = root.anchorMax = safeTopLeft;
            jobsRoot.anchorMin = jobsRoot.anchorMax = safeTopLeft;
            float elapsed = MatchManager.Instance != null
                ? MatchManager.Instance.ElapsedGameplaySeconds : 0f;
            int seconds = Mathf.FloorToInt(elapsed);
            timer.text = seconds >= 3600
                ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}"
                : $"{seconds / 60:00}:{seconds % 60:00}";
            RefreshJobs();
        }

        private void RefreshJobs()
        {
            jobs.Clear();
            foreach (UnitProductionQueue queue in UnitProductionQueue.ActiveQueues)
            {
                if (queue == null || queue.ProductionHost is not Component host
                    || queue.ProductionHost.Faction != selection.PlayerFaction) continue;
                queue.CopyQueue(queueBuffer);
                foreach (UnitType unit in queueBuffer)
                {
                    int existing = -1;
                    for (int i = 0; i < jobs.Count; i++)
                        if (jobs[i].Host == host && jobs[i].Unit == unit)
                        {
                            existing = i;
                            break;
                        }
                    if (existing >= 0)
                    {
                        Job job = jobs[existing];
                        job.Count++;
                        jobs[existing] = job;
                    }
                    else jobs.Add(new Job { Host = host, Unit = unit, Count = 1 });
                }
            }
            foreach (BuildingDefenseUpgrade upgrade in BuildingDefenseUpgrade.ActiveUpgrades)
                if (upgrade != null && upgrade.IsUpgrading
                    && upgrade.Host is Component host
                    && upgrade.Faction == selection.PlayerFaction)
                    jobs.Add(new Job { Host = host, Upgrade = true, Count = 1 });
            int pageCount = Mathf.Max(1,
                Mathf.CeilToInt(jobs.Count / (float)MaximumVisibleJobs));
            if (jobPage >= pageCount) jobPage = pageCount - 1;
            for (int i = 0; i < jobButtons.Length; i++)
            {
                int jobIndex = jobPage * MaximumVisibleJobs + i;
                bool show = jobIndex < jobs.Count;
                jobButtons[i].gameObject.SetActive(show);
                if (!show) continue;
                Job job = jobs[jobIndex];
                if (job.Unit.HasValue) jobImages[i].sprite = assets.Avatar(job.Unit.Value);
                else if (job.Host.GetComponent<CastleDropOff>() != null)
                {
                    Sprite[] dynamite = Phase6C2AssetCatalog.Load()?.Dynamite;
                    jobImages[i].sprite = dynamite != null && dynamite.Length > 0
                        ? dynamite[0] : null;
                }
                else jobImages[i].sprite = job.Host.GetComponent<BuildingDefense>()?.ArrowSprite;
                jobCounts[i].text = job.Count > 1 ? "x" + job.Count : "";
            }
            overflow.text = pageCount > 1
                ? $"{jobPage + 1}/{pageCount} >" : "";
        }

        private void FocusJob(int index)
        {
            int jobIndex = jobPage * MaximumVisibleJobs + index;
            if (jobIndex < 0 || jobIndex >= jobs.Count) return;
            Component host = jobs[jobIndex].Host;
            if (host == null) return;
            BuildingSite site = host.GetComponent<BuildingSite>();
            if (site != null) selection.SelectBuilding(site);
            else
            {
                CastleDropOff castle = host.GetComponent<CastleDropOff>();
                if (castle != null) selection.SelectCastle(castle);
            }
            Camera.main?.GetComponent<MapCameraController>()?
                .FocusWorldPosition(host.transform.position);
        }

    }
}
