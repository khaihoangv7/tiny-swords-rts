#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    // This is a live, bounded smoke test. It uses an actual Red vision source,
    // Commander assignment, mine work timer, cargo route and Castle deposit.
    [InitializeOnLoad]
    public static class Phase7E7GoldLogisticsValidation
    {
        private const string Pending = "Phase7E7.GoldLogistics.PlayValidation";
        private const string ScenarioKey = "Phase7E7.GoldLogistics.Scenario";
        private const string ScenePath = "Assets/Scenes/Map.unity";
        private enum Scenario
        {
            RedLogistics, BlockedRedEntry, BlueClaim, UnreachableRedEntry,
            BlueContestsRed, RedRecapturesBlue, ActiveBlueAssault
        }
        private static double started;
        private static float deadline;
        private static int stage;
        private static RuleBasedCommander commander;
        private static GoldMineObjective mine;
        private static CastleDropOff castle;
        private static ResourceStockpile stockpile;
        private static UnitController observer;
        private static UnitController miner;
        private static int carriedLoad;
        private static int goldAtExit;
        private static int goldAtFirstExit;
        private static int goldDepositsOfLoad;
        private static int lastObservedGold;
        private static int lastGoldChange;
        private static bool mineDepositApplied;
        private static float firstExitTime;
        private static bool playLogged;
        private static double playStarted;
        private static bool clearedFixtureGuards;
        private static bool claimObserved;
        private static readonly List<Vector3> observerRoute = new();
        private static int observerRouteIndex;
        private static float nextObserverStep;
        private static bool observedRouteReady;
        private static Scenario scenario;
        private static GridManager blockedGrid;
        private static Vector2Int blockedCell;
        private static bool entryBlocked;
        private static readonly List<Vector2Int> blockedEntryCells = new();
        private static UnitController blueMiner;
        private static UnitController blueContestingWarrior;
        private static UnitController redContestingWarrior;
        private static readonly List<UnitController> assaultRed = new();
        private static readonly List<UnitController> assaultBlue = new();
        private static int assaultRedHP;
        private static int assaultBlueHP;
        private static int assaultGoldAtDefense;
        private static bool assaultCommandIssued;
        private static float nextAssaultCommandAttempt;
        private static bool blueContestRetreatIssued;
        private static readonly List<UnitMovement> heldRecaptureMovements = new();
        private static CastleDropOff blueCastle;
        private static bool batchReloadLocked;
        private static bool batchSawEditorStartupError;
        private static string batchUnexpectedError;
        private static int batchStartupResumeAttempts;
        private static double nextBatchStartupResume;

        static Phase7E7GoldLogisticsValidation()
            => EditorApplication.update += Tick;

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.7 Gold Logistics Play Mode")]
        [MenuItem("Tools/Validate Phase 7E.7 Gold Logistics")]
        public static void Run() => Start(Scenario.RedLogistics);

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.7 Blocked Mine Entry")]
        [MenuItem("Tools/Validate Phase 7E.7 Blocked Mine Entry")]
        public static void RunBlockedEntry() => Start(Scenario.BlockedRedEntry);

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.7 Blue Mine Claim")]
        [MenuItem("Tools/Validate Phase 7E.7 Blue Mine Claim")]
        public static void RunBlueClaim() => Start(Scenario.BlueClaim);

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.7 Unreachable Mine")]
        [MenuItem("Tools/Validate Phase 7E.7 Unreachable Mine")]
        public static void RunUnreachableEntry()
            => Start(Scenario.UnreachableRedEntry);

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.8 Blue Contests Red Mine")]
        public static void RunBlueContestsRed() => Start(Scenario.BlueContestsRed);

        public static void RunBatchBlueContestsRed()
            => RunBlueContestsRed();

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.8 Red Recaptures Blue Mine")]
        public static void RunRedRecapturesBlue() => Start(Scenario.RedRecapturesBlue);

        public static void RunBatchRedRecapturesBlue()
            => RunRedRecapturesBlue();

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.8 Active Blue Mine Assault")]
        [MenuItem("Tools/Validate Phase 7E.8 Active Blue Mine Assault")]
        public static void RunActiveBlueAssault() => Start(Scenario.ActiveBlueAssault);

        public static void RunBatchActiveBlueAssault() => RunActiveBlueAssault();

        private static void Start(Scenario selected)
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Start this validation from Edit Mode.");
            SessionState.SetBool(Pending, true);
            SessionState.SetInt(ScenarioKey, (int)selected);
            EditorPrefs.SetInt(Pending, System.Diagnostics.Process.GetCurrentProcess().Id);
            if (Application.isBatchMode)
            {
                batchSawEditorStartupError = false;
                batchUnexpectedError = null;
                batchStartupResumeAttempts = 0;
                nextBatchStartupResume = 0d;
                Application.logMessageReceived -= ObserveBatchLog;
                Application.logMessageReceived += ObserveBatchLog;
                EditorApplication.LockReloadAssemblies();
                batchReloadLocked = true;
            }
            started = EditorApplication.timeSinceStartup;
            playLogged = false;
            playStarted = 0d;
            stage = 0;
            clearedFixtureGuards = false;
            claimObserved = false;
            miner = null;
            observer = null;
            observerRoute.Clear();
            observerRouteIndex = 0;
            observedRouteReady = false;
            scenario = selected;
            blockedGrid = null;
            entryBlocked = false;
            blockedEntryCells.Clear();
            blueMiner = null;
            blueContestingWarrior = null;
            redContestingWarrior = null;
            assaultRed.Clear();
            assaultBlue.Clear();
            assaultRedHP = assaultBlueHP = 0;
            assaultGoldAtDefense = 0;
            assaultCommandIssued = false;
            nextAssaultCommandAttempt = 0f;
            blueContestRetreatIssued = false;
            heldRecaptureMovements.Clear();
            blueCastle = null;
            goldDepositsOfLoad = 0;
            mineDepositApplied = true;
            Debug.Log("PHASE_7E7_GOLD_LOGISTICS_PLAY_STARTED");
        }

        public static void RunBatch() => Run();

        [MenuItem("Tools/Validate Phase 7E.7 Gold Logistics Structure")]
        public static void ValidateStructural()
        {
            Phase7E2AdaptiveEconomyValidation.Validate();
            Phase7E6ContestedGoldMineValidation.Validate();
            Debug.Log("PHASE_7E7_GOLD_LOGISTICS_STRUCTURAL_PASSED: existing "
                + "economy and contested-mine invariants hold. Live hauling "
                + "still requires the separate Play Mode validator.");
        }

        [MenuItem("Tools/Validate Phase 7E.8 Structural Regression")]
        public static void ValidateStructuralRegression()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run structural regression from Edit Mode.");

            int failed = 0;
            RunStructuralCheck("7E.1 Scout", Phase7E1ScoutExplorationValidation.Validate, ref failed);
            RunStructuralCheck("7E.1 Full Match Stability", Phase7E1FullMatchStabilityValidation.Validate, ref failed);
            RunStructuralCheck("7E.2 Economy", Phase7E2AdaptiveEconomyValidation.Validate, ref failed);
            RunStructuralCheck("7E.3 Production", Phase7E3ReactiveProductionValidation.Validate, ref failed);
            RunStructuralCheck("7E.4 Tactical Army", Phase7E4TacticalArmyValidation.Validate, ref failed);
            RunStructuralCheck("7E.5 Strategy", Phase7E5StrategicDecisionValidation.Validate, ref failed);
            RunStructuralCheck("7E.5 Liveness", Phase7E5StrategicLivenessValidation.Validate, ref failed);
            RunStructuralCheck("7E.6 Gold Mine", Phase7E6ContestedGoldMineValidation.Validate, ref failed);
            RunStructuralCheck("6H.1 Worker Economy", Phase6H1EconomyValidation.Run, ref failed);
            RunStructuralCheck("6H.2 Stairs & Sheep", Phase6H2NavigationSheepValidation.Run, ref failed);
            RunStructuralCheck("Elevation Vision", Phase6AVisionValidation.ValidateElevationGridModel, ref failed);
            RunStructuralCheck("Castle & Tower Defense", CastleTowerDefenseUpgradeValidation.Validate, ref failed);
            RunStructuralCheck("RTS HUD", RtsHudValidation.Run, ref failed);
            RunStructuralCheck("Gameplay Audio", GameplayAudioValidation.Run, ref failed);
            Debug.Log("PHASE_7E8_STRUCTURAL_REGRESSION: " + (14 - failed)
                + "/14 passed, " + failed + " failed. These are structural checks, "
                + "not full-match gameplay evidence.");
        }

        private static void RunStructuralCheck(string name, Action check, ref int failed)
        {
            int errors = 0;
            var existingDeathVfx = new HashSet<UnitDeathVfx>();
            UnitDeathVfx[] before = UnityEngine.Object.FindObjectsByType<UnitDeathVfx>(
                FindObjectsInactive.Include);
            for (int i = 0; i < before.Length; i++)
                if (before[i] != null) existingDeathVfx.Add(before[i]);
            Application.LogCallback countErrors = (message, trace, type) =>
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert)
                    errors++;
            };
            Application.logMessageReceived += countErrors;
            try { check(); }
            catch (Exception exception)
            {
                errors++;
                Debug.LogException(exception);
            }
            finally
            {
                Application.logMessageReceived -= countErrors;
                // Some Edit Mode validators exercise unit death. Unity does not
                // advance its presentation timer until Play Mode, so remove only
                // VFX instances created during this synchronous check.
                UnitDeathVfx[] after = UnityEngine.Object.FindObjectsByType<UnitDeathVfx>(
                    FindObjectsInactive.Include);
                for (int i = 0; i < after.Length; i++)
                    if (after[i] != null
                        && !existingDeathVfx.Contains(after[i]))
                        UnityEngine.Object.DestroyImmediate(after[i].gameObject);
            }

            if (errors > 0) failed++;
            Debug.Log("PHASE_7E8_STRUCTURAL_CHECK: " + name + " "
                + (errors == 0 ? "PASS" : "FAIL") + " (errors=" + errors + ")");
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)
                && EditorPrefs.GetInt(Pending, -1)
                    != System.Diagnostics.Process.GetCurrentProcess().Id) return;
            if (started == 0d) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 240d)
            {
                Finish(new TimeoutException("Gold logistics live validation timed out."));
                return;
            }
            if (batchUnexpectedError != null)
            {
                Finish(new InvalidOperationException("Unexpected batch log error: "
                    + batchUnexpectedError));
                return;
            }
            if (Application.isBatchMode && EditorApplication.isPlaying
                && EditorApplication.isPaused && Time.time < 0.5f)
            {
                if (!batchSawEditorStartupError || batchStartupResumeAttempts >= 3)
                {
                    Finish(new InvalidOperationException("Batch Play Mode paused "
                        + "during startup without a recoverable Editor error."));
                    return;
                }
                if (EditorApplication.timeSinceStartup >= nextBatchStartupResume)
                {
                    batchStartupResumeAttempts++;
                    nextBatchStartupResume = EditorApplication.timeSinceStartup + 1d;
                    Debug.Log("PHASE_7E7_BATCH_EDITOR_PAUSE_RESUME: attempt="
                        + batchStartupResumeAttempts);
                    EditorApplication.isPaused = false;
                }
            }
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode
                    || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    return;
                EditorSceneManager.OpenScene(ScenePath);
                EditorApplication.isPlaying = true;
                return;
            }
            if (Time.time < 0.5f) return;
            scenario = (Scenario)SessionState.GetInt(ScenarioKey, 0);
            if (!playLogged)
            {
                playLogged = true;
                playStarted = EditorApplication.timeSinceStartup;
                Debug.Log("PHASE_7E7_GOLD_LOGISTICS_PLAY_RUNNING");
            }
            try
            {
                if (stage == 0) SetUp();
                else if (Time.time > deadline)
                    throw new TimeoutException("Gold logistics stage " + stage
                        + " stalled. " + Diagnose());
                else if (stage == 1)
                {
                    if (observer == null && commander.ProductionPolicy.LivingPawnCount >= 6)
                        PlaceLegalObserver();
                    if (observer != null && !observedRouteReady)
                        AdvanceObserverRoute();
                    if (observedRouteReady
                        && scenario == Scenario.UnreachableRedEntry)
                    {
                        stage = 9;
                        deadline = Time.time + 45f;
                    }
                    if (observedRouteReady
                        && scenario != Scenario.UnreachableRedEntry
                        && commander.GoldMineWorkers.AssignmentSerial > 0)
                    {
                        stage = 2;
                        deadline = Time.time + 120f;
                    }
                }
                else if (stage == 2) ObserveClaim();
                else if (stage == 3) ObserveFirstLoad();
                else if (stage == 4) ObserveDeposit();
                else if (stage == 5) ObserveReentry();
                else if (stage == 6) ObserveSecondLoad();
                else if (stage == 7) ObserveBlueClaim();
                else if (stage == 8) ObserveBlueLoad();
                else if (stage == 9) ObserveUnreachableRecovery();
                else if (stage == 10) ObserveContestEjection();
                else if (stage == 11) ObserveContestedBlueClaim();
                else if (stage == 12) ObserveRedContestEjection();
                else if (stage == 13) ObserveRedRecapture();
                else if (stage == 14) ObserveActiveBlueAssault();
                else if (stage == 15) ObserveActiveBlueAssaultRecovery();
            }
            catch (Exception error) { Finish(error); }
        }

        private static void SetUp()
        {
            if (MatchManager.Instance == null
                || MatchManager.Instance.State != MatchState.Playing)
            {
                if (EditorApplication.timeSinceStartup - playStarted > 15d)
                {
                    EconomyTestSetup setup = UnityEngine.Object
                        .FindAnyObjectByType<EconomyTestSetup>();
                    bool setupReady = setup != null && (bool)(typeof(EconomyTestSetup)
                        .GetField("setupReady", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.GetValue(setup) ?? false);
                    BuildingSystem buildingSystem = UnityEngine.Object
                        .FindAnyObjectByType<BuildingSystem>();
                    string buildingStatus = "none";
                    if (buildingSystem != null)
                    {
                        var details = new List<string>();
                        foreach (BuildingType type in Enum.GetValues(typeof(BuildingType)))
                        {
                            BuildingData data = buildingSystem.GetData(type);
                            details.Add(type + ":" + (data != null)
                                + "/sprite=" + (data?.GetSprite(buildingSystem.Owner) != null));
                        }
                        buildingStatus = buildingSystem.IsReady + ", owner="
                            + buildingSystem.Owner + ", paths="
                            + (buildingSystem.Grid?.GetComponent<PathfindingSystem>() != null)
                            + ", definitions=" + (((System.Collections.IDictionary)
                                typeof(BuildingSystem).GetField("definitions",
                                    BindingFlags.Instance | BindingFlags.NonPublic)
                                    ?.GetValue(buildingSystem))?.Count ?? -1)
                            + ", data=[" + string.Join(";", details) + "]";
                    }
                    throw new InvalidOperationException(
                        "The Map scene did not initialize a playable match "
                        + "in this Play Mode environment. Scene="
                        + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path
                        + ", setup=" + (setup != null)
                        + ", setupActive=" + (setup != null
                            && setup.isActiveAndEnabled)
                        + ", setupReady=" + setupReady
                        + ", setupChildren=" + (setup != null
                            ? setup.transform.childCount : 0)
                        + ", units=" + UnitController.ActiveUnits.Count
                        + ", buildingData="
                        + Resources.LoadAll<BuildingData>("Buildings").Length
                        + ", grid=" + (UnityEngine.Object
                            .FindAnyObjectByType<GridManager>() != null)
                        + ", selection=" + (UnityEngine.Object
                            .FindAnyObjectByType<TinyKingdom.Input.SelectionManager>() != null)
                        + ", commands=" + (UnityEngine.Object
                            .FindAnyObjectByType<TinyKingdom.Input.PlayerCommandController>() != null)
                        + ", buildingSystem=" + buildingStatus
                        + ", match=" + (MatchManager.Instance != null
                            ? MatchManager.Instance.State.ToString() : "none")
                        + ", gameTime=" + Time.time.ToString("0.0"));
                }
                return;
            }
            commander = UnityEngine.Object.FindAnyObjectByType<RuleBasedCommander>();
            mine = UnityEngine.Object.FindAnyObjectByType<GoldMineObjective>();
            if (commander == null || mine == null || !commander.IsConfigured
                || commander.Knowledge == null || !commander.Knowledge.IsReady)
            {
                if (EditorApplication.timeSinceStartup - playStarted > 15d)
                    throw new InvalidOperationException("Red Commander, Gold Mine "
                        + "or FactionKnowledge is not ready in the Map fixture.");
                return;
            }
            castle = MatchManager.Instance?.RedMainCastle;
            stockpile = commander.Knowledge?.Stockpile;
            if (castle == null || stockpile == null || !castle.IsAvailable)
            {
                if (EditorApplication.timeSinceStartup - playStarted > 15d)
                    throw new InvalidOperationException("Red Castle or stockpile "
                        + "is unavailable: castle=" + (castle != null)
                        + ", alive=" + (castle != null && castle.IsAlive)
                        + ", stockpile=" + (stockpile != null));
                return;
            }
            if (scenario == Scenario.ActiveBlueAssault)
            {
                Phase6CMatchFlowValidation.Run();
                ValidateRuntimeBuildingOccupancy();
            }
            castle.Deposited += OnCastleDeposit;
            mine.Changed += OnMineChanged;
            typeof(GoldMineWorkerPolicy).GetField("maximumAssignedMiners",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(commander.GoldMineWorkers, 1);
            stockpile.Add(ResourceType.Food, 600); // Fixture only: allow Pawn training.
            lastObservedGold = stockpile.GetAmount(ResourceType.Gold);
            lastGoldChange = 0;
            stockpile.Changed += OnStockpileChanged;
            Time.timeScale = 10f;
            if (scenario is Scenario.BlueClaim or Scenario.RedRecapturesBlue)
            {
                PrepareBlueClaim();
                return;
            }
            if (scenario == Scenario.BlockedRedEntry)
                BlockNearestRedEntry();
            stage = 1;
            deadline = Time.time + 120f;
            Debug.Log("PHASE_7E7_GOLD_LOGISTICS_FIXTURE_READY");
        }

        private static void PlaceLegalObserver()
        {
            if (observer != null) return;
            // A real Red unit supplies vision; no fog setting or hidden Blue
            // state is changed. Other economy Pawns remain under the Commander.
            var units = UnitController.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
                if (units[i] != null && units[i].Faction == Faction.Red
                    && units[i].GetComponent<PawnScoutPromotion>()?.IsPromoted == true)
                { observer = units[i]; break; }
            if (observer == null && scenario == Scenario.RedRecapturesBlue)
                for (int i = 0; i < units.Count; i++)
                    if (units[i] != null && units[i].Faction == Faction.Red
                        && units[i] != redContestingWarrior
                        && units[i].name.StartsWith("Phase7E8_RedRecapturingWarrior_",
                            StringComparison.Ordinal))
                    { observer = units[i]; break; }
            if (observer == null) return;
            // This fixture begins after a successful guard clear. It checks
            // logistics, while Phase 7E.6 tests the separate assault rules.
            ClearFixtureGuards();
            observer.GetComponent<PawnGatherer>()?.CancelJob();
            UnitMovement movement = observer.GetComponent<UnitMovement>();
            movement.Stop();
            GridManager navigation = movement.GridManager;
            PathfindingSystem paths = navigation?.GetComponent<PathfindingSystem>();
            if (navigation == null || paths == null)
                throw new InvalidOperationException("Fixture has no navigation for the Red scout.");
            var entries = new List<Vector3>();
            BuildingSpawnPoint.TryFindMany(mine, navigation, 12, entries, 3);
            for (int i = 0; i < entries.Count; i++)
            {
                List<Vector3> route = paths.FindWorldPath(observer.transform.position,
                    entries[i]);
                if (route.Count == 0 || observerRoute.Count > 0
                    && route.Count >= observerRoute.Count) continue;
                observerRoute.Clear();
                observerRoute.AddRange(route);
            }
            if (observerRoute.Count == 0)
                throw new InvalidOperationException("No navigation-valid Red scout route "
                    + "from the current position to the mine fixture. " + Diagnose());
            if (scenario == Scenario.UnreachableRedEntry)
                BlockAllMineEntries(navigation);
            observerRouteIndex = 0;
            nextObserverStep = Time.time;
            movement.enabled = false;
            Debug.Log("PHASE_7E7_GOLD_LOGISTICS_SCOUT_ROUTE: "
                + observerRoute.Count + " navigable cells");
        }

        private static void ClearFixtureGuards()
        {
            if (clearedFixtureGuards) return;
            for (int i = UnitController.ActiveUnits.Count - 1; i >= 0; i--)
            {
                UnitController unit = UnitController.ActiveUnits[i];
                if (unit != null && unit.Faction == Faction.Goblin
                    && Vector2.Distance(unit.transform.position,
                        mine.transform.position) < 12f)
                    unit.gameObject.SetActive(false);
            }
            clearedFixtureGuards = true;
        }

        private static void BlockNearestRedEntry()
        {
            blockedGrid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            if (blockedGrid == null)
                throw new InvalidOperationException("Blocked-entry fixture has no grid.");
            var positions = new List<Vector3>();
            BuildingSpawnPoint.TryFindMany(mine, blockedGrid, 12, positions, 3);
            if (positions.Count < 2)
                throw new InvalidOperationException("Blocked-entry fixture needs "
                    + "at least two legal mine standing cells.");
            Vector3 nearest = positions[0];
            float best = Vector2.SqrMagnitude(nearest - castle.transform.position);
            for (int i = 1; i < positions.Count; i++)
            {
                float distance = Vector2.SqrMagnitude(
                    positions[i] - castle.transform.position);
                if (distance >= best) continue;
                nearest = positions[i];
                best = distance;
            }
            blockedCell = blockedGrid.WorldToGrid(nearest);
            if (!blockedGrid.SetOccupied(blockedCell.x, blockedCell.y, true))
                throw new InvalidOperationException("Could not block nearest entry.");
            entryBlocked = true;
            Debug.Log("PHASE_7E7_BLOCKED_ENTRY_FIXTURE: cell=" + blockedCell);
        }

        private static void BlockAllMineEntries(GridManager navigation)
        {
            blockedGrid = navigation;
            Vector2Int origin = mine.FootprintOrigin;
            Vector2Int size = mine.Footprint;
            int height = navigation.GetCell(origin.x, origin.y).HeightLevel;
            for (int y = origin.y - 3; y < origin.y + size.y + 3; y++)
            for (int x = origin.x - 3; x < origin.x + size.x + 3; x++)
            {
                if (x >= origin.x && x < origin.x + size.x
                    && y >= origin.y && y < origin.y + size.y) continue;
                GridCell cell = navigation.GetCell(x, y);
                if (!TerrainRules.IsStableStandingCell(cell)
                    || cell.HeightLevel != height || cell.Occupied) continue;
                navigation.SetOccupied(x, y, true);
                blockedEntryCells.Add(new Vector2Int(x, y));
            }
            Require(blockedEntryCells.Count > 0,
                "Unreachable fixture found no entrance cells to block.");
            Debug.Log("PHASE_7E7_UNREACHABLE_FIXTURE: blocked="
                + blockedEntryCells.Count + " entry-area cells");
        }

        private static void ObserveUnreachableRecovery()
        {
            if (commander.GoldMineWorkers.FailedAttemptCount < 3
                || commander.StrategicIntent.GoldMine.FailedAttemptCount == 0)
                return;
            Require(commander.GoldMineWorkers.LastAttemptResult
                    == CommanderCommandResult.RejectedUnreachable
                && commander.GoldMineWorkers.PendingCount == 0
                && mine.PendingEntryCount == 0
                && !mine.HasOwner,
                "Unreachable mine left a pending or claimed worker. " + Diagnose());
            Debug.Log("PHASE_7E7_UNREACHABLE_RECOVERY_PASSED: three failed "
                + "routes triggered the existing strategic cooldown without "
                + "stranding a Pawn at the mine.");
            Finish(null);
        }

        private static void PrepareBlueClaim()
        {
            ClearFixtureGuards();
            blueCastle = MatchManager.Instance?.BlueMainCastle;
            if (blueCastle == null || !blueCastle.IsAvailable)
                throw new InvalidOperationException("Blue Castle is unavailable.");
            var units = UnitController.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit == null || unit.Faction != Faction.Blue
                    || unit.GetComponent<PawnGatherer>() is not PawnGatherer gatherer
                    || gatherer.CarriedAmount > 0) continue;
                if (!mine.TryEnter(unit)) continue;
                blueMiner = unit;
                break;
            }
            if (blueMiner == null)
                throw new InvalidOperationException("No Blue Pawn could receive "
                    + "a navigation-valid mine entry command. " + Diagnose());
            stage = 7;
            deadline = Time.time + 120f;
            Debug.Log("PHASE_7E7_BLUE_ENTRY_ISSUED: pawn="
                + blueMiner.GetEntityId());
        }

        private static void PrepareBlueContest()
        {
            Require(mine.HasOwner && mine.Faction == Faction.Red
                && mine.OccupantCount > 0,
                "Blue contest began without an active Red mining operation.");
            blueCastle = MatchManager.Instance?.BlueMainCastle;
            EconomyTestSetup setup = UnityEngine.Object
                .FindAnyObjectByType<EconomyTestSetup>();
            GameObject prefab = setup == null ? null
                : typeof(EconomyTestSetup).GetField("blueWarriorPrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(setup) as GameObject;
            GridManager navigation = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            if (prefab == null || navigation == null || blueCastle == null)
                throw new InvalidOperationException("Blue contest fixture is missing "
                    + "its existing Warrior prefab, navigation, or Castle.");
            var candidates = new List<Vector3>();
            BuildingSpawnPoint.TryFindMany(mine, navigation, 12, candidates, 3);
            Vector3? chosen = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 point = candidates[i];
                if (point.x >= mine.transform.position.x
                    || Vector2.Distance(point, mine.transform.position)
                        >= GoldMineObjective.ContestRadius - 1f) continue;
                bool occupied = false;
                for (int j = 0; j < UnitController.ActiveUnits.Count; j++)
                {
                    UnitController unit = UnitController.ActiveUnits[j];
                    if (unit != null && Vector2.Distance(point,
                        unit.transform.position) < 0.7f)
                    { occupied = true; break; }
                }
                if (!occupied) { chosen = point; break; }
            }
            if (!chosen.HasValue)
                throw new InvalidOperationException("No legal Blue military contest "
                    + "position was available beside the mine.");
            GameObject instance = UnityEngine.Object.Instantiate(prefab,
                chosen.Value, Quaternion.identity);
            instance.name = "Phase7E8_BlueContestingWarrior";
            blueContestingWarrior = instance.GetComponent<UnitController>();
            instance.GetComponent<UnitMovement>()?.Configure(navigation,
                navigation.GetComponent<PathfindingSystem>());
            Require(blueContestingWarrior != null
                && blueContestingWarrior.Faction == Faction.Blue
                && blueContestingWarrior.IsTargetable,
                "The existing Blue Warrior prefab did not spawn as a valid "
                    + "military contestant.");
            stage = 10;
            deadline = Time.time + 20f;
            Debug.Log("PHASE_7E8_BLUE_CONTEST_STARTED: Red occupant="
                + miner.GetEntityId() + ", Blue Warrior="
                + blueContestingWarrior.GetEntityId());
        }

        private static void ValidateRuntimeBuildingOccupancy()
        {
            BuildingSystem blueBuildings = null;
            BuildingSystem[] systems = UnityEngine.Object
                .FindObjectsByType<BuildingSystem>();
            for (int i = 0; i < systems.Length; i++)
                if (systems[i] != null && systems[i].Owner == Faction.Blue
                    && systems[i].IsReady)
                { blueBuildings = systems[i]; break; }
            BuildingData house = blueBuildings?.GetData(BuildingType.House);
            GridManager grid = blueBuildings?.Grid;
            Require(house != null && grid != null,
                "Runtime occupancy test lacks a configured Blue House or grid.");
            Vector2Int footprint = house.Footprint;
            Vector2Int origin = default;
            bool found = false;
            for (int y = 0; y <= grid.Height - footprint.y && !found; y++)
            for (int x = 0; x <= grid.Width - footprint.x && !found; x++)
            {
                int height = grid.GetCell(x, y)?.HeightLevel ?? -1;
                if (height < 0) continue;
                bool clear = true;
                for (int fy = 0; fy < footprint.y && clear; fy++)
                for (int fx = 0; fx < footprint.x; fx++)
                {
                    GridCell cell = grid.GetCell(x + fx, y + fy);
                    if (cell == null || cell.Occupied
                        || !BuildingSystem.IsValidPlacementCell(cell, height))
                    { clear = false; break; }
                }
                if (!clear) continue;
                origin = new Vector2Int(x, y);
                found = true;
            }
            Require(found, "Runtime occupancy test found no clear House footprint.");
            var temporary = new GameObject("Phase7E8_RuntimeOccupancyProbe");
            try
            {
                BuildingSite site = temporary.AddComponent<BuildingSite>();
                site.Configure(house, Faction.Blue, origin, grid, null);
                for (int y = 0; y < footprint.y; y++)
                for (int x = 0; x < footprint.x; x++)
                    Require(grid.GetCell(origin.x + x, origin.y + y).Occupied,
                        "New BuildingSite did not reserve its footprint.");
                site.TakeDamage(1f);
                for (int y = 0; y < footprint.y; y++)
                for (int x = 0; x < footprint.x; x++)
                    Require(!grid.GetCell(origin.x + x, origin.y + y).Occupied,
                        "Destroyed BuildingSite left a blocked navigation cell.");
                Debug.Log("PHASE_7E8_BUILDING_DESTRUCTION_OCCUPANCY_PASSED");
            }
            finally
            {
                if (temporary != null) UnityEngine.Object.Destroy(temporary);
            }
        }

        private static void PrepareActiveBlueAssault()
        {
            EconomyTestSetup setup = UnityEngine.Object
                .FindAnyObjectByType<EconomyTestSetup>();
            Require(setup != null,
                "Active contest fixture lacks EconomyTestSetup.");
            Type setupType = typeof(EconomyTestSetup);
            GameObject redPrefab = setupType.GetField("redWarriorPrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(setup) as GameObject;
            GameObject bluePrefab = setupType.GetField("blueWarriorPrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(setup) as GameObject;
            GridManager navigation = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            Require(redPrefab != null && bluePrefab != null && navigation != null,
                "Active contest fixture lacks Warrior prefabs or navigation.");
            var candidates = new List<Vector3>();
            BuildingSpawnPoint.TryFindMany(mine, navigation, 32, candidates, 4);
            // Supply a ready defending army so the fixture tests contested
            // control rather than an intentional insufficient-force retreat.
            SpawnAssaultForce(redPrefab, Faction.Red, 6, true,
                navigation, candidates, assaultRed);
            SpawnAssaultForce(bluePrefab, Faction.Blue, 2, false,
                navigation, candidates, assaultBlue);
            assaultRedHP = AssaultHealth(assaultRed);
            assaultBlueHP = AssaultHealth(assaultBlue);
            Require(assaultRedHP > 0 && assaultBlueHP > 0,
                "Active contest Warriors have no valid combat health.");
            stage = 14;
            deadline = Time.time + 90f;
            nextAssaultCommandAttempt = Time.time + 0.5f;
            Debug.Log("PHASE_7E8_ACTIVE_ASSAULT_STARTED: Red defenders="
                + assaultRed.Count + ", Blue attackers=" + assaultBlue.Count
                + ", Red miner=" + miner.GetEntityId());
        }

        private static void SpawnAssaultForce(GameObject prefab, Faction faction,
            int count, bool rightSide, GridManager navigation,
            List<Vector3> candidates, List<UnitController> result)
        {
            for (int i = 0; i < candidates.Count && result.Count < count; i++)
            {
                Vector3 point = candidates[i];
                if (rightSide != (point.x > mine.transform.position.x)
                    || Vector2.Distance(point, mine.transform.position)
                        >= GoldMineObjective.ContestRadius - 0.5f) continue;
                bool occupied = false;
                for (int j = 0; j < UnitController.ActiveUnits.Count; j++)
                {
                    UnitController unit = UnitController.ActiveUnits[j];
                    if (unit != null && Vector2.Distance(point,
                        unit.transform.position) < 0.8f)
                    { occupied = true; break; }
                }
                if (occupied) continue;
                GameObject instance = UnityEngine.Object.Instantiate(prefab,
                    point, Quaternion.identity);
                instance.name = "Phase7E8_ActiveMineAssault_" + faction + "_"
                    + (result.Count + 1);
                instance.GetComponent<UnitMovement>()?.Configure(navigation,
                    navigation.GetComponent<PathfindingSystem>());
                UnitController warrior = instance.GetComponent<UnitController>();
                Require(warrior != null && warrior.Faction == faction
                    && warrior.IsTargetable
                    && instance.GetComponent<CombatController>() != null,
                    "Active contest Warrior did not spawn with valid combat.");
                result.Add(warrior);
            }
            Require(result.Count == count, "Active contest has only "
                + result.Count + "/" + count + " legal " + faction
                + " positions around the mine.");
        }

        private static int AssaultHealth(List<UnitController> force)
        {
            int total = 0;
            for (int i = 0; i < force.Count; i++)
                if (force[i] != null && force[i].IsAlive)
                    total += force[i].GetComponent<HealthComponent>()?.CurrentHP ?? 0;
            return total;
        }

        private static int LivingAssaultUnits(List<UnitController> force)
        {
            int count = 0;
            for (int i = 0; i < force.Count; i++)
                if (force[i] != null && force[i].IsAlive) count++;
            return count;
        }

        private static void ObserveActiveBlueAssault()
        {
            if (!assaultCommandIssued && Time.time >= nextAssaultCommandAttempt)
            {
                nextAssaultCommandAttempt = Time.time + 0.5f;
                for (int i = 0; i < assaultBlue.Count; i++)
                {
                    UnitController blue = assaultBlue[i];
                    if (blue == null || !blue.IsAlive) continue;
                    for (int j = 0; j < assaultRed.Count; j++)
                    {
                        UnitController red = assaultRed[j];
                        if (red == null || !red.IsAlive) continue;
                        if (blue.GetComponent<CombatController>().Attack(red))
                        {
                            assaultCommandIssued = true;
                            break;
                        }
                    }
                }
                if (assaultCommandIssued)
                    Debug.Log("PHASE_7E8_ACTIVE_ASSAULT_COMMAND_ACCEPTED");
            }
            if (!assaultCommandIssued) return;
            int redHP = AssaultHealth(assaultRed);
            int blueHP = AssaultHealth(assaultBlue);
            if (redHP >= assaultRedHP || blueHP >= assaultBlueHP) return;
            bool redWon = LivingAssaultUnits(assaultBlue) == 0
                && LivingAssaultUnits(assaultRed) > 0;
            bool blueWon = LivingAssaultUnits(assaultRed) == 0
                && LivingAssaultUnits(assaultBlue) > 0;
            if (redWon)
            {
                Require(mine.HasOwner && mine.Faction == Faction.Red,
                    "Red won the assault but lost mine ownership. " + Diagnose());
                assaultGoldAtDefense = mine.RemainingGold;
                stage = 15;
                deadline = Time.time + 120f;
                Debug.Log("PHASE_7E8_ACTIVE_ASSAULT_DEFENDED: both factions dealt "
                    + "combat damage; Red defenders held the mine. "
                    + "Waiting for mining to resume.");
            }
            else if (blueWon && mine.OccupantCount == 0)
            {
                Require(!ReferenceEquals(miner?.GetComponent<UnitStationing>()?.Host,
                        mine),
                    "Blue won the assault but Red miner remained in the mine.");
                Debug.Log("PHASE_7E8_ACTIVE_ASSAULT_PASSED: both factions dealt "
                    + "combat damage; Blue force displaced Red mining.");
                Finish(null);
            }
        }

        private static void ObserveActiveBlueAssaultRecovery()
        {
            Require(mine != null && mine.HasOwner
                && mine.Faction == Faction.Red,
                "Red lost the mine after repelling Blue. " + Diagnose());
            if (mine.RemainingGold >= assaultGoldAtDefense) return;
            Debug.Log("PHASE_7E8_ACTIVE_ASSAULT_PASSED: Red repelled Blue "
                + "and extracted more Gold afterward.");
            Finish(null);
        }

        private static void ObserveContestEjection()
        {
            if (mine.OccupantCount > 0
                || ReferenceEquals(miner?.GetComponent<UnitStationing>()?.Host,
                    mine)) return;
            Require(miner != null && miner.IsAlive
                && mine.HasOwner && mine.Faction == Faction.Red,
                "Military contest did not safely eject the Red miner while "
                    + "retaining the previous owner until a new Pawn enters. "
                    + Diagnose());
            for (int i = 0; i < UnitController.ActiveUnits.Count; i++)
            {
                UnitController unit = UnitController.ActiveUnits[i];
                if (unit == null || unit.Faction != Faction.Blue
                    || unit.GetComponent<PawnGatherer>() is not PawnGatherer gatherer
                    || gatherer.CarriedAmount > 0) continue;
                if (!mine.TryEnter(unit)) continue;
                blueMiner = unit;
                break;
            }
            Require(blueMiner != null,
                "The unopposed Blue force did not open a legal Pawn entry. "
                    + Diagnose());
            stage = 11;
            deadline = Time.time + 120f;
            Debug.Log("PHASE_7E8_RED_MINER_EJECTED: Blue Pawn entry issued");
        }

        private static void ObserveContestedBlueClaim()
        {
            if (!mine.HasOwner || mine.Faction != Faction.Blue
                || !ReferenceEquals(blueMiner?.GetComponent<UnitStationing>()?.Host,
                    mine)) return;
            Require(mine.OccupantCount > 0
                && !ReferenceEquals(miner?.GetComponent<UnitStationing>()?.Host,
                    mine),
                "Blue claim left the Red miner inside the contested mine.");
            stage = 8;
            deadline = Time.time + 90f;
            Debug.Log("PHASE_7E8_CONTESTED_MINE_BLUE_CLAIMED");
        }

        private static void ObserveBlueClaim()
        {
            if (!mine.HasOwner || mine.Faction != Faction.Blue
                || !ReferenceEquals(blueMiner?.GetComponent<UnitStationing>()?.Host,
                    mine)) return;
            Require(mine.OccupantCount > 0,
                "Blue Pawn did not occupy the claimed mine.");
            if (scenario == Scenario.RedRecapturesBlue)
            {
                PrepareRedContest();
                return;
            }
            stage = 8;
            deadline = Time.time + 90f;
            Debug.Log("PHASE_7E7_BLUE_MINE_CLAIMED");
        }

        private static void PrepareRedContest()
        {
            EconomyTestSetup setup = UnityEngine.Object
                .FindAnyObjectByType<EconomyTestSetup>();
            GameObject prefab = setup == null ? null
                : typeof(EconomyTestSetup).GetField("redWarriorPrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(setup) as GameObject;
            GridManager navigation = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            if (prefab == null || navigation == null)
                throw new InvalidOperationException("Red recapture fixture lacks "
                    + "the existing Warrior prefab or navigation.");
            var candidates = new List<Vector3>();
            BuildingSpawnPoint.TryFindMany(mine, navigation, 24, candidates, 3);
            Vector3? localGuardPosition = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 point = candidates[i];
                if (point.x <= mine.transform.position.x
                    || Vector2.Distance(point, mine.transform.position)
                        >= GoldMineObjective.ContestRadius - 1f) continue;
                bool occupied = false;
                for (int j = 0; j < UnitController.ActiveUnits.Count; j++)
                {
                    UnitController unit = UnitController.ActiveUnits[j];
                    if (unit != null && Vector2.Distance(point,
                        unit.transform.position) < 0.7f)
                    { occupied = true; break; }
                }
                if (!occupied) { localGuardPosition = point; break; }
            }
            Require(localGuardPosition.HasValue,
                "The recapture fixture has no legal local guard position.");
            redContestingWarrior = SpawnRedRecaptureWarrior(prefab, navigation,
                localGuardPosition.Value, 1, true);
            int required = commander.MilitaryPolicy.MinOffensiveMilitaryPopulation;
            var rearPositions = new List<Vector3>();
            BuildingSpawnPoint.TryFindMany(castle, navigation, required - 1,
                rearPositions, 4);
            Require(rearPositions.Count >= required - 1,
                "The recapture fixture has no safe reserve positions at "
                    + "the Red Castle.");
            for (int i = 0; i < required - 1; i++)
                SpawnRedRecaptureWarrior(prefab, navigation,
                    rearPositions[i], i + 2, false);
            stage = 12;
            deadline = Time.time + 120f;
            Debug.Log("PHASE_7E8_RED_RECAPTURE_CONTEST_STARTED: Blue occupant="
                + blueMiner.GetEntityId() + ", Red Warriors=" + required
                + " (one holding mine approach)");
        }

        private static UnitController SpawnRedRecaptureWarrior(GameObject prefab,
            GridManager navigation, Vector3 position, int number, bool holdPosition)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab,
                position, Quaternion.identity);
            instance.name = "Phase7E8_RedRecapturingWarrior_" + number;
            UnitController warrior = instance.GetComponent<UnitController>();
            UnitMovement movement = instance.GetComponent<UnitMovement>();
            movement?.Configure(navigation,
                navigation.GetComponent<PathfindingSystem>());
            Require(warrior != null && warrior.Faction == Faction.Red
                && warrior.IsTargetable,
                "The existing Red Warrior prefab did not spawn as a valid "
                    + "military contestant.");
            if (holdPosition && movement != null)
            {
                movement.enabled = false;
                heldRecaptureMovements.Add(movement);
            }
            return warrior;
        }

        private static void ObserveRedContestEjection()
        {
            if (mine.OccupantCount > 0
                || ReferenceEquals(blueMiner?.GetComponent<UnitStationing>()?.Host,
                    mine)) return;
            Require(mine.HasOwner && mine.Faction == Faction.Blue
                && redContestingWarrior != null
                && redContestingWarrior.IsTargetable,
                "Red military contest did not eject the Blue miner while "
                    + "preserving the previous owner. " + Diagnose());
            if (!blueContestRetreatIssued && blueMiner != null
                && blueMiner.IsAlive)
            {
                GridManager navigation = UnityEngine.Object
                    .FindAnyObjectByType<GridManager>();
                var exits = new List<Vector3>();
                BuildingSpawnPoint.TryFindMany(blueCastle, navigation, 12,
                    exits, 3);
                UnitMovement movement = blueMiner.GetComponent<UnitMovement>();
                for (int i = 0; i < exits.Count; i++)
                    if (movement != null && movement.TryIssueMove(exits[i]))
                    { blueContestRetreatIssued = true; break; }
                Require(blueContestRetreatIssued,
                    "Blue miner could not retreat after losing mine control. "
                        + Diagnose());
            }
            bool mineVisible = false;
            IReadOnlyList<GoldMineKnowledgeSnapshot> visible =
                commander.Knowledge.VisibleGoldMines;
            for (int i = 0; i < visible.Count; i++)
                if (visible[i].RuntimeId.CompareTo(mine.GetEntityId()) == 0)
                { mineVisible = true; break; }
            if (!mineVisible || blueMiner != null && blueMiner.IsAlive
                && Vector2.Distance(blueMiner.transform.position,
                    mine.transform.position) <= commander.StrategicIntent
                        .GoldMine.GuardRegionRadius) return;
            stage = 13;
            deadline = Time.time + 120f;
            Debug.Log("PHASE_7E8_BLUE_MINER_EJECTED: Red force observes mine");
        }

        private static void ObserveRedRecapture()
        {
            if (observer == null && commander.ProductionPolicy.LivingPawnCount >= 6)
                PlaceLegalObserver();
            if (observer != null && !observedRouteReady)
                AdvanceObserverRoute();
            if (!mine.HasOwner || mine.Faction != Faction.Red
                || mine.OccupantCount == 0 || !claimObserved) return;
            Require(miner != null
                && ReferenceEquals(miner.GetComponent<UnitStationing>()?.Host,
                    mine)
                && !ReferenceEquals(blueMiner?.GetComponent<UnitStationing>()?.Host,
                    mine)
                && commander.GoldMineWorkers.AssignmentSerial > 0,
                "Red recapture did not use its existing AI miner assignment. "
                    + Diagnose());
            Debug.Log("PHASE_7E8_RED_RECAPTURE_PASSED: Red military ejected "
                + "the Blue miner and the Red Commander reclaimed the mine "
                + "through its normal worker policy.");
            Finish(null);
        }

        private static void ObserveBlueLoad()
        {
            PawnGatherer gatherer = blueMiner?.GetComponent<PawnGatherer>();
            if (gatherer == null || gatherer.CarriedAmount <= 0) return;
            Require(gatherer.MineReturnTarget == mine
                && gatherer.CarriedAmount == GoldMineObjective.MineGoldPerTrip(gatherer)
                && gatherer.HasDropOffTarget
                && gatherer.DropOffTargetId.CompareTo(blueCastle.RuntimeId) == 0,
                "Blue mine load or Castle return route is invalid. " + Diagnose());
            Debug.Log((scenario == Scenario.BlueContestsRed
                    ? "PHASE_7E8_BLUE_CONTEST_PASSED: "
                    : "PHASE_7E7_BLUE_CLAIM_PASSED: ")
                + "Blue Pawn entered, claimed, "
                + "mined and carried its load toward the Blue Castle.");
            Finish(null);
        }

        private static void AdvanceObserverRoute()
        {
            if (Time.time < nextObserverStep) return;
            if (observerRouteIndex < observerRoute.Count)
            {
                // Test fixture: move only the Red vision source over a valid
                // navigation route, allowing regular vision updates to reveal
                // the corridor. The miner still receives a real path command.
                observer.transform.position = observerRoute[observerRouteIndex++];
                nextObserverStep = Time.time + Mathf.Max(0.35f,
                    (TeamVisionSystem.Instance?.UpdateInterval ?? 0.2f) * 2f);
                return;
            }
            observedRouteReady = true;
            Debug.Log("PHASE_7E7_GOLD_LOGISTICS_SCOUT_OBSERVING: route="
                + observerRoute.Count + " cells");
        }

        private static void ObserveClaim()
        {
            if (!claimObserved) return;
            Require(miner != null, "A Red claim was observed without its Pawn.");
            if (scenario == Scenario.BlueContestsRed)
            {
                PrepareBlueContest();
                return;
            }
            if (scenario == Scenario.ActiveBlueAssault)
            {
                PrepareActiveBlueAssault();
                return;
            }
            if (scenario == Scenario.BlockedRedEntry)
                Require(entryBlocked && blockedGrid != null
                    && blockedGrid.GetCell(blockedCell.x, blockedCell.y)?.Occupied == true,
                    "The blocked entrance was not active when Red claimed.");
            stage = 3;
            deadline = Time.time + 90f;
        }

        private static void OnMineChanged()
        {
            if (claimObserved || mine == null || !mine.HasOwner
                || mine.Faction != Faction.Red || mine.OccupantCount <= 0) return;
            var units = UnitController.AllUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit == null || unit.Faction != Faction.Red
                    || !ReferenceEquals(unit.GetComponent<UnitStationing>()?.Host,
                        mine)) continue;
                miner = unit;
                claimObserved = true;
                Debug.Log("PHASE_7E7_GOLD_LOGISTICS_CLAIMED: pawn="
                    + unit.GetEntityId() + ", mine=" + mine.GetEntityId()
                    + ", gameTime=" + Time.time.ToString("0.0"));
                return;
            }
        }

        private static void ObserveFirstLoad()
        {
            Require(mine.IsAlive, "Mine became invalid before extraction.");
            PawnGatherer gatherer = miner?.GetComponent<PawnGatherer>();
            if (gatherer != null && gatherer.MineReturnTarget == mine
                && gatherer.CarriedAmount > 0)
            {
                carriedLoad = gatherer.CarriedAmount;
                goldAtExit = stockpile.GetAmount(ResourceType.Gold);
                goldAtFirstExit = mine.RemainingGold;
                firstExitTime = Time.time;
                Debug.Log("PHASE_7E7_GOLD_LOGISTICS_FIRST_LOAD: " + carriedLoad);
                Require(claimObserved && mine.HasOwner
                    && mine.Faction == Faction.Red,
                    "Red claimed the mine, but control changed before extraction. "
                        + Diagnose());
                Require(carriedLoad == GoldMineObjective.MineGoldPerTrip(gatherer),
                    "Mine cargo does not match the configured Pawn load.");
                Require(gatherer.HasDropOffTarget
                    && gatherer.DropOffTargetId.CompareTo(castle.RuntimeId) == 0,
                    "Mine cargo was not routed to the Red Castle.");
                stage = 4;
                deadline = Time.time + 120f;
            }
        }

        private static void ObserveDeposit()
        {
            PawnGatherer gatherer = miner?.GetComponent<PawnGatherer>();
            Require(gatherer != null && miner.IsAlive,
                "Mine carrier died before the Castle deposit.");
            if (gatherer.CarriedAmount > 0) return;
            // The Commander can spend Gold while the Pawn walks home, so the
            // later balance cannot prove this deposit. Check the synchronous
            // Castle deposit event and the exact stockpile delta it caused.
            Require(goldDepositsOfLoad == 1 && mineDepositApplied,
                "Mine load was not deposited exactly once at the Castle. "
                    + Diagnose());
            stage = 5;
            deadline = Time.time + 60f;
        }

        private static void ObserveReentry()
        {
            if (!ReferenceEquals(miner?.GetComponent<UnitStationing>()?.Host,
                    mine)) return;
            Require(mine.OccupantCount > 0,
                "Same Red miner did not resume its reserved mine slot.");
            stage = 6;
            deadline = Time.time + 60f;
        }

        private static void ObserveSecondLoad()
        {
            PawnGatherer gatherer = miner?.GetComponent<PawnGatherer>();
            if (gatherer == null || gatherer.CarriedAmount <= 0) return;
            Require(mine.RemainingGold < goldAtFirstExit
                && gatherer.MineReturnTarget == mine,
                "The automatic second extraction did not continue.");
            Debug.Log((scenario == Scenario.BlockedRedEntry
                    ? "PHASE_7E7_BLOCKED_ENTRY_PLAY_PASSED: "
                    : "PHASE_7E7_GOLD_LOGISTICS_PLAY_PASSED: ")
                + "Red AI assigned a miner; "
                + "mine cargo reached its own Castle once and the same Pawn "
                + "completed another mining cycle. This is a focused smoke test, "
                + "not a full-match validation.");
            Finish(null);
        }

        private static void OnCastleDeposit(ResourceType type, int amount)
        {
            if (stage >= 4 && Time.time >= firstExitTime
                && type == ResourceType.Gold && amount == carriedLoad)
            {
                goldDepositsOfLoad++;
                mineDepositApplied &= lastGoldChange == carriedLoad;
            }
        }

        private static void OnStockpileChanged()
        {
            if (stockpile == null) return;
            int current = stockpile.GetAmount(ResourceType.Gold);
            lastGoldChange = current - lastObservedGold;
            lastObservedGold = current;
        }

        private static string AssaultUnitDiagnostics(List<UnitController> units)
        {
            string result = "[";
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (i > 0) result += ";";
                if (unit == null) { result += "destroyed"; continue; }
                CombatController combat = unit.GetComponent<CombatController>();
                result += unit.transform.position.ToString()
                    + "/hp=" + (unit.GetComponent<HealthComponent>()?.CurrentHP ?? 0)
                    + "/target=" + (combat?.Target != null
                        ? combat.Target.GetEntityId().ToString() : "none")
                    + "/moving=" + (unit.GetComponent<UnitMovement>()?.IsMoving ?? false);
            }
            return result + "]";
        }

        private static string Diagnose()
        {
            GoldMineOpportunity opportunity = commander?.StrategicIntent
                ?.GoldMine?.Current ?? default;
            string result = "gameTime=" + Time.time.ToString("0.0")
                + ", match=" + (MatchManager.Instance?.State.ToString() ?? "none")
                + ", assigned=" + (commander?.GoldMineWorkers.AssignmentSerial ?? 0)
                + ", failedAssignments="
                + (commander?.GoldMineWorkers.FailedAttemptCount ?? 0)
                + ", lastAssignmentResult="
                + commander?.GoldMineWorkers.LastAttemptResult
                + ", scoutRoute=" + observerRouteIndex + "/"
                + observerRoute.Count
                + ", scenario=" + scenario
                + ", blockedEntry=" + entryBlocked
                + ", castleDeposits=" + goldDepositsOfLoad
                + ", lastGoldDelta=" + lastGoldChange
                + ", depositApplied=" + mineDepositApplied
                + ", goldAtExit=" + goldAtExit
                + ", currentGold=" + stockpile?.GetAmount(ResourceType.Gold)
                + ", claimObserved=" + claimObserved;
            result += ", opportunity=" + (opportunity.IsValid
                    ? opportunity.Reason + "/safe=" + opportunity.SafeForMiners
                        + "/commit=" + opportunity.CanCommit
                        + "/guard="
                        + opportunity.KnownGuardStrength.ToString("0.0")
                        + "/own=" + opportunity.OwnStrength.ToString("0.0")
                    : "none")
                + ", intent=" + commander?.StrategicIntent.CurrentIntent
                + ", armyPopulation="
                + (commander?.MilitaryPolicy?.AvailableMilitaryPopulation ?? -1)
                + ", militaryThreat="
                + (commander?.MilitaryPolicy?.HasCurrentThreat ?? false)
                + ", visibleMines="
                + (commander?.Knowledge?.VisibleGoldMines.Count ?? -1)
                + ", livingPawns="
                + (commander?.ProductionPolicy?.LivingPawnCount ?? -1)
                + ", economyPawns="
                + (commander?.PawnRoles?.EconomyCount ?? -1)
                + ", visibleEnemyUnits="
                + (commander?.Knowledge?.VisibleEnemyUnits.Count ?? -1)
                + ", redContestingWarrior="
                + (redContestingWarrior != null
                    ? redContestingWarrior.transform.position.ToString()
                        + "/alive=" + redContestingWarrior.IsAlive
                    : "none");
            if (scenario == Scenario.ActiveBlueAssault)
            {
                result += ", assaultCommand=" + assaultCommandIssued
                    + ", redHP=" + AssaultHealth(assaultRed) + "/" + assaultRedHP
                    + ", blueHP=" + AssaultHealth(assaultBlue) + "/" + assaultBlueHP
                    + ", redUnits=" + AssaultUnitDiagnostics(assaultRed)
                    + ", blueUnits=" + AssaultUnitDiagnostics(assaultBlue);
            }
            if (mine == null) return result + ", mine=none";
            result += ", mine=" + mine.GetEntityId()
                + ", minePosition=" + mine.transform.position
                + ", state=" + mine.State
                + ", owner=" + (mine.HasOwner ? mine.Faction.ToString() : "none")
                + ", occupants=" + mine.OccupantCount
                + ", pending=" + mine.PendingEntryCount;
            UnitController candidate = miner ?? blueMiner
                ?? commander?.GoldMineWorkers.LastAttemptWorker;
            if (candidate == null)
            {
                var units = UnitController.AllUnits;
                for (int i = 0; i < units.Count; i++)
                    if (units[i] != null && units[i].Faction == Faction.Red
                        && mine.TryGetPendingEntry(units[i], out _, out _))
                    { candidate = units[i]; break; }
            }
            if (candidate == null) return result + ", miner=none";
            UnitMovement movement = candidate.GetComponent<UnitMovement>();
            UnitStationing station = candidate.GetComponent<UnitStationing>();
            PawnGatherer gatherer = candidate.GetComponent<PawnGatherer>();
            result += ", pawn=" + candidate.GetEntityId()
                + ", position=" + candidate.transform.position
                + ", state=" + candidate.CurrentState
                + ", stationPending=" + (station?.IsPending ?? false)
                + ", internal=" + ReferenceEquals(station?.Host, mine)
                + ", worker=" + gatherer?.State
                + ", cargo=" + gatherer?.CarriedAmount
                + ", moving=" + (movement?.IsMoving ?? false)
                + ", destination=" + movement?.CommandDestinationCell
                + ", lastProgressAge="
                + (movement == null ? -1f : Time.time
                    - movement.LastMovementProgressTime).ToString("0.0")
                + ", pathFailures=" + movement?.PathFailureCount;
            if (mine.TryGetPendingEntry(candidate, out Vector3 entry,
                out int attempts))
                result += ", entry=" + entry + ", entryDistance="
                    + Vector2.Distance(candidate.transform.position, entry)
                        .ToString("0.00") + ", entryAttempts=" + attempts;
            return result;
        }

        private static void Finish(Exception error)
        {
            Application.logMessageReceived -= ObserveBatchLog;
            SessionState.SetBool(Pending, false);
            SessionState.EraseInt(ScenarioKey);
            EditorPrefs.DeleteKey(Pending);
            if (entryBlocked && blockedGrid != null)
                blockedGrid.SetOccupied(blockedCell.x, blockedCell.y, false);
            if (blockedGrid != null)
                for (int i = 0; i < blockedEntryCells.Count; i++)
                    blockedGrid.SetOccupied(blockedEntryCells[i].x,
                        blockedEntryCells[i].y, false);
            blockedEntryCells.Clear();
            entryBlocked = false;
            if (castle != null) castle.Deposited -= OnCastleDeposit;
            if (stockpile != null) stockpile.Changed -= OnStockpileChanged;
            if (mine != null) mine.Changed -= OnMineChanged;
            if (observer != null)
                observer.GetComponent<UnitMovement>().enabled = true;
            for (int i = 0; i < heldRecaptureMovements.Count; i++)
                if (heldRecaptureMovements[i] != null)
                    heldRecaptureMovements[i].enabled = true;
            heldRecaptureMovements.Clear();
            Time.timeScale = 1f;
            if (batchReloadLocked)
            {
                EditorApplication.UnlockReloadAssemblies();
                batchReloadLocked = false;
            }
            if (error != null) Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void ObserveBatchLog(string message, string stack, LogType type)
        {
            if (type is not (LogType.Error or LogType.Exception or LogType.Assert))
                return;
            if (stack.Contains("UnityEditor.Search.SearchDatabase")
                || stack.Contains("UnityEditor.Search.SearchInit")
                || (message.StartsWith("No graphic device is available to",
                        StringComparison.Ordinal)))
            {
                batchSawEditorStartupError = true;
                return;
            }
            batchUnexpectedError ??= message;
        }
    }
}
#endif
