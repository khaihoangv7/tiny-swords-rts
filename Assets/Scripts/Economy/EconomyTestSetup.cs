using TinyKingdom.UI;
using TinyKingdom.Units;
using TinyKingdom.Buildings;
using TinyKingdom.Navigation;
using TinyKingdom.Input;
using TinyKingdom.Presentation;
using TinyKingdom.Vision;
using TinyKingdom.Match;
using TinyKingdom.AI;
using TinyKingdom.Debugging;
using TinyKingdom.Terrain;
using UnityEngine;

namespace TinyKingdom.Economy
{
    // Phase 5A test content. It is attached only to the Map scene.
    public sealed class EconomyTestSetup : MonoBehaviour
    {
        [SerializeField] private BattlefieldLayout battlefieldLayout = null;
        [SerializeField] private PawnAnimationSet pawnAnimations = null;
        [SerializeField] private PawnAnimationSet redPawnAnimations = null;
        [SerializeField] private UnitCombatData pawnCombatData = null;
        [SerializeField] private UnitCombatData warriorCombatData = null;
        [SerializeField] private UnitCombatData lancerCombatData = null;
        [SerializeField] private UnitCombatData archerCombatData = null;
        [SerializeField] private UnitCombatData monkCombatData = null;
        [SerializeField] private GameObject blueWarriorPrefab = null;
        [SerializeField] private GameObject blueLancerPrefab = null;
        [SerializeField] private GameObject blueArcherPrefab = null;
        [SerializeField] private GameObject blueMonkPrefab = null;
        [SerializeField] private GameObject redWarriorPrefab = null;
        [SerializeField] private GameObject redLancerPrefab = null;
        [SerializeField] private GameObject redArcherPrefab = null;
        [SerializeField] private GameObject redMonkPrefab = null;
        [SerializeField] private Sprite treeStumpSprite = null;
        [SerializeField] private Sprite sheepSprite = null;
        [SerializeField] private Sprite treeSprite = null;
        [SerializeField] private Sprite goldSprite = null;
        [SerializeField] private Sprite blueCastleSprite = null;
        [SerializeField] private EconomyConfig economyConfig = null;
        [SerializeField] private Sprite foodIcon = null;
        [SerializeField] private Sprite woodIcon = null;
        [SerializeField] private Sprite goldIcon = null;
        [SerializeField] private Sprite healthBarLeft = null;
        [SerializeField] private Sprite healthBarMiddle = null;
        [SerializeField] private Sprite healthBarRight = null;
        [SerializeField] private Sprite healthBarFill = null;
        [SerializeField] private Shader hitFlashShader = null;
        private Material hitFlashMaterial;
        private bool setupReady;
        private EconomyFeedback feedback;
        private Transform areaTransform;
        private UnitCombatData qlWarriorTrainingData;
        private bool qlGoblinIsland;
        private int producedCount;
        private PresentationAssetCatalog presentation;

        private void Awake()
        {
            if (redPawnAnimations == null && battlefieldLayout != null)
                redPawnAnimations = battlefieldLayout.redPawnAnimations;
            if (pawnAnimations == null || pawnAnimations.Idle.Length == 0
                || redPawnAnimations == null || redPawnAnimations.Idle.Length == 0
                || pawnCombatData == null || hitFlashShader == null
                || foodIcon == null || woodIcon == null || goldIcon == null
                || healthBarLeft == null || healthBarMiddle == null
                || healthBarRight == null || healthBarFill == null
                || treeSprite == null || sheepSprite == null || goldSprite == null
                || treeStumpSprite == null || blueCastleSprite == null || economyConfig == null)
            {
                Debug.LogError("Phase 5A test setup has missing Tiny Swords sprite references.", this);
                return;
            }

            bool ruleBasedDemo = !QlExperiment.Requested && RuleBasedDemo.Selected;
            qlGoblinIsland = QlExperiment.Requested && gameObject.scene.name == "GoblinIsland";
            var area = new GameObject("BlueEconomyTest");
            area.transform.SetParent(transform, false);
            areaTransform = area.transform;
            feedback = area.AddComponent<EconomyFeedback>();
            feedback.Configure(foodIcon, woodIcon, goldIcon);
            ResourceStockpile blue = MakeStockpile(area.transform, Faction.Blue);
            ResourceStockpile red = MakeStockpile(area.transform, Faction.Red);
            BuildingData[] buildings = Resources.LoadAll<BuildingData>("Buildings");
            BuildingData castleData = System.Array.Find(buildings,
                item => item != null && item.Type == BuildingType.Castle);
            GridManager navigation = FindAnyObjectByType<GridManager>();
            presentation = PresentationAssetCatalog.Load();
            if (presentation == null)
                Debug.LogError("Phase 5C2.1 presentation catalog is missing. "
                    + "Allow the Editor asset setup to finish importing.", this);
            CastleDropOff castle = MakeCastle(area.transform, blue,
                battlefieldLayout != null ? (Vector3)battlefieldLayout.blueCastle
                    : new Vector3(4.5f, 10.5f, 0f), castleData, Faction.Blue);
            feedback.Watch(castle);

            if (battlefieldLayout != null)
            {
                foreach (BattlefieldResource resource in battlefieldLayout.resources)
                {
                    if (ruleBasedDemo && !RuleBasedDemo.UsesResource(battlefieldLayout, resource)) continue;
                    int variant = resource.variant;
                    bool wood = resource.type == ResourceType.Wood;
                    bool gold = resource.type == ResourceType.Gold;
                    MakeNode(area.transform, resource.name, resource.type,
                        wood ? economyConfig.TreeCapacity
                            : gold ? economyConfig.GetGoldStoneCapacity(variant)
                                : economyConfig.SheepCapacity,
                        resource.position,
                        wood ? presentation?.Tree(variant) ?? treeSprite
                            : gold ? presentation?.Gold(variant) ?? goldSprite
                                : First(presentation?.SheepIdle) ?? sheepSprite,
                        wood ? 1.5f : 0.65f,
                        wood ? presentation?.Stump(variant) ?? treeStumpSprite : null,
                        gold ? presentation?.GoldHighlight(variant) : null,
                        wood ? presentation?.TreeFrames(variant) : null,
                        gold ? (variant == 5 ? 1.55f : 1.4f)
                            : UnitVisualPresentation.Scale);
                }
                for (int i = 0; i < battlefieldLayout.bluePawns.Length; i++)
                    feedback.Watch(MakePawn(area.transform, $"BluePawn_{i + 1}",
                        battlefieldLayout.bluePawns[i], Faction.Blue, pawnAnimations));
                for (int i = 0; i < battlefieldLayout.redPawns.Length; i++)
                    MakePawn(area.transform, $"RedPawn_{i + 1}",
                        battlefieldLayout.redPawns[i], Faction.Red, redPawnAnimations);
            }
            else
            {
            for (int i = 0; i < 4; i++)
                MakeNode(area.transform, $"Tree_{i + 1}", ResourceType.Wood,
                    economyConfig.TreeCapacity, new Vector3(3.5f + i * 2f, 14.5f, 0f),
                    presentation?.Tree(i) ?? treeSprite, 1.5f,
                    presentation?.Stump(i) ?? treeStumpSprite, null,
                    presentation?.TreeFrames(i));
            MakeNode(area.transform, "Sheep_1", ResourceType.Food, economyConfig.SheepCapacity,
                new Vector3(9.5f, 12.5f, 0f), First(presentation?.SheepIdle) ?? sheepSprite, 0.65f);
            MakeNode(area.transform, "Sheep_2", ResourceType.Food, economyConfig.SheepCapacity,
                new Vector3(11.5f, 13.5f, 0f), First(presentation?.SheepIdle) ?? sheepSprite, 0.65f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 position = new Vector3(9.5f + i % 3 * 2f,
                    16.5f + i / 3 * 2f, 0f);
                MakeNode(area.transform, $"Gold_{i + 1}", ResourceType.Gold,
                    economyConfig.GetGoldStoneCapacity(i), position,
                    presentation?.Gold(i) ?? goldSprite, 0.65f, null,
                    presentation?.GoldHighlight(i), null,
                    i == 5 ? 1.55f : 1.4f);
            }

            // A mirrored, faction-neutral resource cluster gives Red only the
            // resources its own Castle/Pawns can legitimately reveal.
            for (int i = 0; i < 4; i++)
                MakeNode(area.transform, $"RedTree_{i + 1}", ResourceType.Wood,
                    economyConfig.TreeCapacity, new Vector3(35.5f - i * 2f, 14.5f, 0f),
                    presentation?.Tree(i) ?? treeSprite, 1.5f,
                    presentation?.Stump(i) ?? treeStumpSprite, null,
                    presentation?.TreeFrames(i));
            MakeNode(area.transform, "RedSheep_1", ResourceType.Food,
                economyConfig.SheepCapacity, new Vector3(29.5f, 12.5f, 0f),
                First(presentation?.SheepIdle) ?? sheepSprite, 0.65f);
            MakeNode(area.transform, "RedSheep_2", ResourceType.Food,
                economyConfig.SheepCapacity, new Vector3(27.5f, 13.5f, 0f),
                First(presentation?.SheepIdle) ?? sheepSprite, 0.65f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 position = new Vector3(29.5f - i % 3 * 2f,
                    16.5f + i / 3 * 2f, 0f);
                MakeNode(area.transform, $"RedGold_{i + 1}", ResourceType.Gold,
                    economyConfig.GetGoldStoneCapacity(i), position,
                    presentation?.Gold(i) ?? goldSprite, 0.65f, null,
                    presentation?.GoldHighlight(i), null,
                    i == 5 ? 1.55f : 1.4f);
            }

            feedback.Watch(MakePawn(area.transform, "BluePawn_1",
                new Vector3(3.5f, 7.5f, 0f), Faction.Blue, pawnAnimations));
            feedback.Watch(MakePawn(area.transform, "BluePawn_2",
                new Vector3(4.5f, 7.5f, 0f), Faction.Blue, pawnAnimations));
            feedback.Watch(MakePawn(area.transform, "BluePawn_3",
                new Vector3(5.5f, 7.5f, 0f), Faction.Blue, pawnAnimations));
            }

            SelectionSummaryUI summary = FindAnyObjectByType<SelectionSummaryUI>();
            if (summary == null)
                Debug.LogError("Phase 5A needs the existing UI Canvas for the economy HUD.", this);

            SelectionManager selection = FindAnyObjectByType<SelectionManager>();
            PlayerCommandController commands = FindAnyObjectByType<PlayerCommandController>();
            if (buildings.Length < 6 || castleData == null || navigation == null
                || selection == null || commands == null)
            {
                Debug.LogError("Phase 5C2 needs six BuildingData assets and existing grid/input.", this);
                setupReady = true;
                return;
            }
            var populationObject = new GameObject("BluePopulation");
            populationObject.transform.SetParent(area.transform, false);
            FactionPopulation population = populationObject.AddComponent<FactionPopulation>();
            population.Configure(Faction.Blue, 10);
            var redPopulationObject = new GameObject("RedPopulation");
            redPopulationObject.transform.SetParent(area.transform, false);
            FactionPopulation redPopulation = redPopulationObject.AddComponent<FactionPopulation>();
            redPopulation.Configure(Faction.Red, 10);
            BuildingSystem buildingSystem = area.AddComponent<BuildingSystem>();
            buildingSystem.Configure(navigation, blue, population, buildings, economyConfig);
            if (!buildingSystem.IsReady)
            {
                Debug.LogError("Phase 5B1 BuildingData is missing a valid type, sprite or navigation link.", this);
                setupReady = true;
                return;
            }
            BuildingPlacementController placement = commands.gameObject.AddComponent<BuildingPlacementController>();
            placement.Configure(buildingSystem, selection, navigation, Camera.main);

            if (warriorCombatData == null || lancerCombatData == null || archerCombatData == null
                || monkCombatData == null || blueWarriorPrefab == null || blueLancerPrefab == null
                || blueArcherPrefab == null || blueMonkPrefab == null
                || redWarriorPrefab == null || redLancerPrefab == null
                || redArcherPrefab == null || redMonkPrefab == null)
            {
                Debug.LogError("Phase 7B production needs existing unit data and Blue/Red prefabs.", this);
                setupReady = true;
                return;
            }
            if (qlGoblinIsland)
                qlWarriorTrainingData = warriorCombatData.CopyWithTrainingCosts(150, 90);
            UnitCombatData[] unitData =
                { pawnCombatData, qlWarriorTrainingData ?? warriorCombatData,
                    lancerCombatData, archerCombatData, monkCombatData };
            castle.gameObject.AddComponent<UnitProductionQueue>().Configure(
                castle, blue, population, navigation, unitData,
                (type, position) => SpawnProductionUnit(Faction.Blue, type, position));
            SetupDefense(castle, navigation, blue);
            CastleDropOff redCastle = MakeCastle(area.transform, red,
                battlefieldLayout != null ? (Vector3)battlefieldLayout.redCastle
                    : new Vector3(34.5f, 10.5f, 0f), castleData, Faction.Red);
            SetupDefense(redCastle, navigation, red);
            redCastle.gameObject.AddComponent<UnitProductionQueue>().Configure(
                redCastle, red, redPopulation, navigation, unitData,
                (type, position) => SpawnProductionUnit(Faction.Red, type, position));
            if (battlefieldLayout == null)
            {
                MakePawn(area.transform, "RedPawn_1", new Vector3(33.5f, 7.5f, 0f),
                    Faction.Red, redPawnAnimations);
                MakePawn(area.transform, "RedPawn_2", new Vector3(34.5f, 7.5f, 0f),
                    Faction.Red, redPawnAnimations);
                MakePawn(area.transform, "RedPawn_3", new Vector3(35.5f, 7.5f, 0f),
                    Faction.Red, redPawnAnimations);
            }
            BuildingData towerData = System.Array.Find(buildings,
                item => item != null && item.Type == BuildingType.Tower);
            if (battlefieldLayout == null && TryFindOpenBuildingOrigin(towerData, navigation,
                new Vector2Int(31, 18), out Vector2Int redTowerOrigin))
            {
                var redTowerObject = new GameObject("RedTowerDefenseTarget");
                redTowerObject.transform.SetParent(area.transform, false);
                BuildingSite redTower = redTowerObject.AddComponent<BuildingSite>();
                redTower.Configure(towerData, Faction.Red, redTowerOrigin,
                    navigation, null, HouseVariant.A, red, economyConfig);
                redTower.CompleteImmediatelyForTestSetup();
                SetupDefense(redTower, navigation, red);
            }
            else if (battlefieldLayout == null)
                Debug.LogWarning("No safe Map position found for the Red Tower defense target.", this);
            buildingSystem.BuildingPlaced += site =>
            {
                if (site.Type == BuildingType.Barracks || site.Type == BuildingType.Archery
                    || site.Type == BuildingType.Monastery)
                    site.gameObject.AddComponent<UnitProductionQueue>().Configure(
                        site, blue, population, navigation, unitData,
                        (type, position) => SpawnProductionUnit(Faction.Blue, type, position));
                if (site.Type == BuildingType.Tower)
                {
                    SetupDefense(site, navigation, blue);
                    BuildingResourceDropOff tower =
                        site.GetComponent<BuildingResourceDropOff>();
                    if (tower != null) feedback.Watch(tower);
                }
            };
            BuildingSystem redBuildingSystem = area.AddComponent<BuildingSystem>();
            redBuildingSystem.Configure(navigation, red, redPopulation, buildings,
                economyConfig);
            if (ruleBasedDemo || (QlExperiment.Requested
                && gameObject.scene.name == RuleBasedDemo.SceneName))
            {
                buildingSystem.ProtectStraightLaneStairAccess = true;
                redBuildingSystem.ProtectStraightLaneStairAccess = true;
            }
            if (qlGoblinIsland)
            {
                buildingSystem.ProtectGoblinIslandCrossings = true;
                redBuildingSystem.ProtectGoblinIslandCrossings = true;
            }
            redBuildingSystem.BuildingPlaced += site =>
            {
                if (site.Type == BuildingType.Barracks || site.Type == BuildingType.Archery
                    || site.Type == BuildingType.Monastery)
                    site.gameObject.AddComponent<UnitProductionQueue>().Configure(
                        site, red, redPopulation, navigation, unitData,
                        (type, position) => SpawnProductionUnit(Faction.Red, type, position));
                if (site.Type == BuildingType.Tower) SetupDefense(site, navigation, red);
            };
            if (summary != null)
            {
                RtsHudController hud = summary.GetComponent<RtsHudController>()
                    ?? summary.gameObject.AddComponent<RtsHudController>();
                hud.Configure(selection, commands,
                    selection.GetComponent<GuardInputController>(), blue, population,
                    buildingSystem, placement);
            }
            TeamVisionSystem teamVision = TeamVisionBootstrap.Ensure(
                transform, navigation, selection.PlayerFaction);
            SetupStrategicObjective(area.transform, navigation, blue, red);

            MatchManager match = area.AddComponent<MatchManager>();
            match.Configure(selection.PlayerFaction, 0.85f);
            if (!match.RegisterMainCastle(castle) || !match.RegisterMainCastle(redCastle))
                Debug.LogError("Phase 6C could not register both Main Castles.", this);
            RuleBasedCommander redCommander = SetupCommander(area.transform, Faction.Red,
                red, redPopulation, teamVision, redBuildingSystem);
            if (QlExperiment.Requested || ruleBasedDemo)
            {
                RuleBasedCommander blueCommander = SetupCommander(area.transform, Faction.Blue,
                    blue, population, teamVision, buildingSystem);
                if (ruleBasedDemo)
                {
                    area.AddComponent<RuleBasedDemo>().Configure(match, blueCommander, redCommander);
                    placement.enabled = false;
                }
                else area.AddComponent<QlExperiment>().Configure(match, blueCommander, redCommander);
                selection.ClearSelection();
                selection.enabled = false;
                commands.enabled = false;
                GuardInputController guard = selection.GetComponent<GuardInputController>();
                if (guard != null) guard.enabled = false;
                if (summary != null)
                {
                    RtsHudController spectatorHud = summary.GetComponent<RtsHudController>();
                    if (spectatorHud != null) spectatorHud.HideForSpectator();
                }
            }
            else if (redCommander != null)
                RlMlBootstrap.AttachIfRequested(redCommander.gameObject, redCommander,
                    redCommander.GetComponent<RlCommanderBridge>());
            MatchResultAssetCatalog resultAssets = MatchResultAssetCatalog.Load();
            if (summary != null)
            {
                summary.gameObject.AddComponent<MatchResultUI>().Configure(
                    match, selection, resultAssets);
                FpsCounter.Ensure(summary.transform);
            }
            else Debug.LogError("Phase 6C needs the existing screen-space UI Canvas.", this);
            setupReady = true;
        }

        private void Start()
        {
            if (!setupReady) return;
            hitFlashMaterial = new Material(hitFlashShader);
            // All scene unit animators have completed Awake by Start.
            foreach (HealthComponent health in FindObjectsByType<HealthComponent>())
            {
                DecorateHealth(health);
            }
        }

        private void DecorateHealth(HealthComponent health)
        {
            if (health == null || hitFlashMaterial == null) return;
            DamageTextEmitter text = health.GetComponent<DamageTextEmitter>();
            if (text != null) text.enabled = true;
            if (health.GetComponent<UnitHitFlash>() == null)
                health.gameObject.AddComponent<UnitHitFlash>().Configure(hitFlashMaterial);
            SpriteRenderer body = health.GetComponent<UnitAnimator>()?.VisualRenderer
                ?? health.GetComponent<PawnVisual>()?.VisualRenderer;
            HitSquashStretchPresentation reaction =
                health.GetComponent<HitSquashStretchPresentation>()
                ?? health.gameObject.AddComponent<HitSquashStretchPresentation>();
            reaction.Configure(body, health);
            if (health.GetComponent<UnitHealthBar>() == null)
                health.gameObject.AddComponent<UnitHealthBar>().Configure(
                    healthBarLeft, healthBarMiddle, healthBarRight, healthBarFill);
        }

        private UnitController SpawnProductionUnit(Faction faction, UnitType type,
            Vector3 position)
        {
            if (areaTransform == null) return null;
            if (type == UnitType.Pawn)
            {
                PawnGatherer pawn = MakePawn(areaTransform,
                    faction + "Pawn_Produced_" + ++producedCount, position,
                    faction, faction == Faction.Red ? redPawnAnimations : pawnAnimations);
                if (faction == Faction.Blue) feedback.Watch(pawn);
                DecorateHealth(pawn.GetComponent<HealthComponent>());
                QlExperiment.Active?.RecordPawnSpawn(faction);
                return pawn.GetComponent<UnitController>();
            }
            GameObject prefab = (faction, type) switch
            {
                (Faction.Blue, UnitType.Warrior) => blueWarriorPrefab,
                (Faction.Blue, UnitType.Lancer) => blueLancerPrefab,
                (Faction.Blue, UnitType.Archer) => blueArcherPrefab,
                (Faction.Blue, UnitType.Monk) => blueMonkPrefab,
                (Faction.Red, UnitType.Warrior) => redWarriorPrefab,
                (Faction.Red, UnitType.Lancer) => redLancerPrefab,
                (Faction.Red, UnitType.Archer) => redArcherPrefab,
                (Faction.Red, UnitType.Monk) => redMonkPrefab,
                _ => null
            };
            if (prefab == null) return null;
            GameObject instance = Instantiate(prefab, position, Quaternion.identity, areaTransform);
            instance.name = faction + type.ToString() + "_Produced_" + ++producedCount;
            UnitController unit = instance.GetComponent<UnitController>();
            if (unit == null) { Destroy(instance); return null; }
            HealthComponent health = instance.GetComponent<HealthComponent>();
            DecorateHealth(health);
            QlExperiment.Active?.RecordMilitarySpawn(faction, type, health);
            return unit;
        }

        private void OnDestroy()
        {
            if (qlWarriorTrainingData != null) Destroy(qlWarriorTrainingData);
            if (hitFlashMaterial != null) Destroy(hitFlashMaterial);
        }

        private void SetupStrategicObjective(Transform parent, GridManager navigation,
            ResourceStockpile blue, ResourceStockpile red)
        {
            if (navigation == null) return;
            Vector2Int preferred = new(StrategicObjectiveMapLayout.MineOriginX,
                StrategicObjectiveMapLayout.MineOriginY);
            if (battlefieldLayout != null) preferred = battlefieldLayout.mineOrigin;
            Vector2Int footprint = new(StrategicObjectiveMapLayout.MineWidth,
                StrategicObjectiveMapLayout.MineHeight);
            if (!TryFindOpenFootprint(navigation, preferred, footprint,
                out Vector2Int origin))
            {
                Debug.LogError("Phase 6C.2 could not find a safe central Gold Mine footprint.", this);
                return;
            }
            Phase6C2AssetCatalog assets = Phase6C2AssetCatalog.Load();
            if (assets == null || assets.GoldMineInactive == null)
            {
                Debug.LogError("Phase 6C.2 asset catalog is missing.", this);
                return;
            }
            var mineObject = new GameObject("CentralGoldMineObjective");
            mineObject.transform.SetParent(parent, false);
            Vector3 first = navigation.GridToWorld(origin.x, origin.y);
            mineObject.transform.position = first + new Vector3(0.5f, 0.5f, 0f);
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(mineObject.transform, false);
            visualRoot.transform.localPosition = Vector3.up * GoldMineObjective.VisualGroundOffsetY;
            visualRoot.transform.localScale = Vector3.one * GoldMineObjective.VisualScale;
            SpriteRenderer mineVisual = MakeVisual(visualRoot.transform,
                assets?.GoldMineInactive, true);
            mineVisual.sortingOrder = 9;
            BoxCollider2D mineCollider = mineObject.AddComponent<BoxCollider2D>();
            mineCollider.size = new Vector2(1.75f, 1.25f);
            mineCollider.offset = new Vector2(0f, 0.1f);
            GoldMineObjective mine = mineObject.AddComponent<GoldMineObjective>();
            mine.Configure(origin, navigation, blue, red, feedback);
        }

        private static bool TryFindOpenFootprint(GridManager navigation, Vector2Int preferred,
            Vector2Int size, out Vector2Int origin)
        {
            origin = default;
            for (int radius = 0; radius < 12; radius++)
            for (int y = preferred.y - radius; y <= preferred.y + radius; y++)
            for (int x = preferred.x - radius; x <= preferred.x + radius; x++)
            {
                if (Mathf.Max(Mathf.Abs(x - preferred.x), Mathf.Abs(y - preferred.y)) != radius)
                    continue;
                int height = -1; bool valid = true;
                for (int fy = 0; fy < size.y && valid; fy++)
                for (int fx = 0; fx < size.x; fx++)
                {
                    GridCell cell = navigation.GetCell(x + fx, y + fy);
                    if (!TerrainRules.IsStableStandingCell(cell)
                        || (height >= 0 && height != cell.HeightLevel)) { valid = false; break; }
                    height = cell.HeightLevel;
                }
                if (!valid) continue;
                origin = new Vector2Int(x, y); return true;
            }
            return false;
        }

        private static ResourceStockpile MakeStockpile(Transform parent, Faction faction)
        {
            var obj = new GameObject(faction + "Stockpile");
            obj.transform.SetParent(parent, false);
            ResourceStockpile stockpile = obj.AddComponent<ResourceStockpile>();
            stockpile.Configure(faction, 200, 200, 100);
            return stockpile;
        }

        private CastleDropOff MakeCastle(Transform parent, ResourceStockpile stockpile,
            Vector3 position, BuildingData castleData, Faction faction)
        {
            var obj = new GameObject(faction + "CastleDropOff");
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            SpriteRenderer visual = MakeVisual(obj.transform,
                castleData.GetSprite(faction), true);
            visual.gameObject.name = "VisualRoot";
            BuildingVisualPresentation.Apply(visual, BuildingType.Castle,
                visual.transform.localPosition);
            GroundSorting.Ensure(obj, false,
                GroundSorting.GroundOffsetFromVisual(obj.transform, visual));
            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(3f, 2f);
            collider.offset = new Vector2(0f, 0.5f);
            CastleDropOff castle = obj.AddComponent<CastleDropOff>();
            castle.Configure(faction, stockpile, castleData);
            return castle;
        }

        private static bool TryFindOpenBuildingOrigin(BuildingData data, GridManager navigation,
            Vector2Int preferred, out Vector2Int origin)
        {
            origin = default;
            if (data == null || navigation == null) return false;
            for (int radius = 0; radius < 12; radius++)
            for (int y = preferred.y - radius; y <= preferred.y + radius; y++)
            for (int x = preferred.x - radius; x <= preferred.x + radius; x++)
            {
                if (Mathf.Max(Mathf.Abs(x - preferred.x), Mathf.Abs(y - preferred.y)) != radius)
                    continue;
                int height = -1;
                bool valid = true;
                for (int fy = 0; fy < data.Footprint.y && valid; fy++)
                for (int fx = 0; fx < data.Footprint.x; fx++)
                {
                    GridCell cell = navigation.GetCell(x + fx, y + fy);
                    if (!TerrainRules.IsStableStandingCell(cell)
                        || (height >= 0 && cell.HeightLevel != height))
                    {
                        valid = false;
                        break;
                    }
                    height = cell.HeightLevel;
                }
                if (!valid) continue;
                foreach (UnitController unit in UnitController.ActiveUnits)
                {
                    Vector2Int cell = navigation.WorldToGrid(unit.transform.position);
                    if (cell.x >= x && cell.x < x + data.Footprint.x
                        && cell.y >= y && cell.y < y + data.Footprint.y)
                    {
                        valid = false;
                        break;
                    }
                }
                if (!valid) continue;
                origin = new Vector2Int(x, y);
                return true;
            }
            return false;
        }

        private void SetupDefense(IDefensiveBuildingHost host, GridManager navigation,
            ResourceStockpile stockpile)
        {
            MonoBehaviour component = host as MonoBehaviour;
            if (component == null || host.DefenseData == null) return;
            BuildingGarrison garrison = component.GetComponent<BuildingGarrison>()
                ?? component.gameObject.AddComponent<BuildingGarrison>();
            garrison.Configure(host, navigation);
            BuildingCombatTarget target = component.GetComponent<BuildingCombatTarget>()
                ?? component.gameObject.AddComponent<BuildingCombatTarget>();
            target.Configure(host);
            GameObject archerPrefab = host.Faction == Faction.Red
                ? redArcherPrefab : blueArcherPrefab;
            CombatController archer = archerPrefab.GetComponent<CombatController>();
            (component.GetComponent<BuildingDefenseUpgrade>()
                ?? component.gameObject.AddComponent<BuildingDefenseUpgrade>())
                .Configure(host, stockpile);
            BuildingDefense defense = component.GetComponent<BuildingDefense>()
                ?? component.gameObject.AddComponent<BuildingDefense>();
            defense.Configure(host, garrison, archer.ProjectileSprite,
                archerCombatData.ProjectileSpeed, archer.ProjectileArcHeight);
            (component.GetComponent<BuildingVisionSource>()
                ?? component.gameObject.AddComponent<BuildingVisionSource>()).Configure(host);
            if (host is CastleDropOff castle && BuildingRallyPoint.Supports(host.Type))
                (component.GetComponent<BuildingRallyPoint>()
                    ?? component.gameObject.AddComponent<BuildingRallyPoint>()).Configure(
                        castle, navigation, FindAnyObjectByType<SelectionManager>());
            if (component.GetComponent<BuildingDamagePresentation>() == null)
                component.gameObject.AddComponent<BuildingDamagePresentation>().Configure(host);
            if (host.Type == BuildingType.Castle
                && component.GetComponent<BuildingHealthBar>() == null)
                component.gameObject.AddComponent<BuildingHealthBar>().Configure(host,
                    BuildingVisualPresentation.TopLocalY(host.PresentationRenderer) + 0.3f);
        }

        private void MakeNode(Transform parent, string name, ResourceType type,
            int capacity, Vector3 position, Sprite sprite, float clickSize,
            Sprite depletedSprite = null, Sprite[] highlightFrames = null,
            Sprite[] ambientFrames = null,
            float visualScale = UnitVisualPresentation.Scale)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            SpriteRenderer renderer = MakeVisual(obj.transform, sprite,
                type != ResourceType.Wood);
            UnitVisualPresentation.ApplyAlignedScale(renderer,
                renderer.transform.localPosition, visualScale);
            var collider = obj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(clickSize, type == ResourceType.Wood ? 2f : clickSize);
            collider.offset = type == ResourceType.Wood ? new Vector2(0f, 0.9f) : Vector2.zero;
            collider.isTrigger = type == ResourceType.Food;
            obj.AddComponent<ResourceInteractionSlots>().Configure(1);
            if (type == ResourceType.Food)
                obj.AddComponent<ResourceHarvestFlash>().Configure(renderer);
            ResourceNode node = obj.AddComponent<ResourceNode>();
            node.Configure(type, capacity, renderer, depletedSprite, visualScale);
            if (type == ResourceType.Food)
            {
                if (presentation != null)
                    obj.AddComponent<SheepPresentation>().Configure(node, renderer,
                        presentation.SheepIdle, presentation.SheepGrass,
                        presentation.SheepMove);
                obj.AddComponent<HitSquashStretchPresentation>().Configure(renderer);
            }
            if (type == ResourceType.Gold && highlightFrames != null
                && highlightFrames.Length > 0)
                obj.AddComponent<GoldHarvestHighlight>().Configure(node, sprite, highlightFrames);
            if (type == ResourceType.Wood && ambientFrames != null
                && ambientFrames.Length > 0)
                obj.AddComponent<TreePresentation>().Configure(node, ambientFrames);
        }

        private static Sprite First(Sprite[] sprites)
            => sprites != null && sprites.Length > 0 ? sprites[0] : null;

        private PawnGatherer MakePawn(Transform parent, string name, Vector3 position,
            Faction faction, PawnAnimationSet animations)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = animations.Idle[0];
            renderer.sortingOrder = 10;
            obj.AddComponent<UnitController>().Configure(faction);
            obj.AddComponent<HealthComponent>().Configure(pawnCombatData);
            obj.AddComponent<UnitMovement>();
            obj.AddComponent<UnitSelection>().ConfigureMarkerYOffset(-0.35f);
            PawnGatherer gatherer = obj.AddComponent<PawnGatherer>();
            float harvestSpeed = qlGoblinIsland ? 1.5f : 1f;
            gatherer.ConfigureGathering(economyConfig.GatherSecondsPerUnit / harvestSpeed,
                economyConfig.GatherImpactDelay / harvestSpeed, economyConfig.AmountPerStep,
                economyConfig.PawnCarryCapacity);
            obj.AddComponent<CombatController>().Configure(pawnCombatData);
            obj.AddComponent<PawnBuilder>();
            obj.AddComponent<PawnVisual>().Configure(animations);
            return gatherer;
        }

        private static RuleBasedCommander SetupCommander(Transform parent, Faction faction,
            ResourceStockpile stockpile, FactionPopulation population, TeamVisionSystem vision,
            BuildingSystem buildingSystem)
        {
            if (vision == null || FogMemorySystem.Instance == null)
            {
                Debug.LogError("Phase 7A requires TeamVisionSystem and FogMemorySystem.");
                return null;
            }
            var root = new GameObject(faction + "RuleBasedCommander");
            root.transform.SetParent(parent, false);
            FactionKnowledge knowledge = root.AddComponent<FactionKnowledge>();
            knowledge.Configure(faction, vision, FogMemorySystem.Instance, stockpile, population);
            FactionCommandService commands = root.AddComponent<FactionCommandService>();
            commands.Configure(faction, knowledge, buildingSystem);
            RuleBasedCommander commander = root.AddComponent<RuleBasedCommander>();
            commander.Configure(faction, knowledge, commands, 0.50f);
            // Opt-in Phase 8.1 adapter; RuleBased is the serialized default.
            RlCommanderBridge bridge = root.AddComponent<RlCommanderBridge>();
            bridge.Configure(commander);
            return commander;
        }

        private static SpriteRenderer MakeVisual(Transform parent, Sprite sprite, bool centerY)
        {
            var child = new GameObject("Visual");
            child.transform.SetParent(parent, false);
            // Use the imported pivot. The Tiny Swords slices use Center alignment;
            // the serialized pivot field in their .meta files is not a bottom-left pivot.
            child.transform.localPosition = new Vector3(
                (sprite.pivot.x - sprite.rect.width * 0.5f) / sprite.pixelsPerUnit,
                (sprite.pivot.y - (centerY ? sprite.rect.height * 0.5f : 0f))
                    / sprite.pixelsPerUnit,
                0f);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 10;
            return renderer;
        }
    }
}
