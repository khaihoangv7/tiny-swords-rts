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
            RedLogistics, BlockedRedEntry, BlueClaim, UnreachableRedEntry
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
        private static CastleDropOff blueCastle;

        static Phase7E7GoldLogisticsValidation()
            => EditorApplication.update += Tick;

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.7 Gold Logistics Play Mode")]
        [MenuItem("Tools/Validate Phase 7E.7 Gold Logistics")]
        public static void Run() => Start(Scenario.RedLogistics);

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.7 Blocked Mine Entry")]
        public static void RunBlockedEntry() => Start(Scenario.BlockedRedEntry);

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.7 Blue Mine Claim")]
        public static void RunBlueClaim() => Start(Scenario.BlueClaim);

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 7E.7 Unreachable Mine")]
        public static void RunUnreachableEntry()
            => Start(Scenario.UnreachableRedEntry);

        private static void Start(Scenario selected)
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Start this validation from Edit Mode.");
            SessionState.SetBool(Pending, true);
            SessionState.SetInt(ScenarioKey, (int)selected);
            EditorPrefs.SetInt(Pending, System.Diagnostics.Process.GetCurrentProcess().Id);
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
            blueCastle = null;
            goldDepositsOfLoad = 0;
            mineDepositApplied = true;
            Debug.Log("PHASE_7E7_GOLD_LOGISTICS_PLAY_STARTED");
        }

        public static void RunBatch() => Run();

        public static void ValidateStructural()
        {
            Phase7E2AdaptiveEconomyValidation.Validate();
            Phase7E6ContestedGoldMineValidation.Validate();
            Debug.Log("PHASE_7E7_GOLD_LOGISTICS_STRUCTURAL_PASSED: existing "
                + "economy and contested-mine invariants hold. Live hauling "
                + "still requires the separate Play Mode validator.");
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
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
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
            }
            catch (Exception error) { Finish(error); }
        }

        private static void SetUp()
        {
            if (MatchManager.Instance == null
                || MatchManager.Instance.State != MatchState.Playing)
            {
                if (EditorApplication.timeSinceStartup - playStarted > 15d)
                    throw new InvalidOperationException(
                        "The Map scene did not initialize a playable match "
                        + "in this Play Mode environment.");
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
            if (scenario == Scenario.BlueClaim)
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
            // A real Red Scout supplies vision; no fog setting or hidden Blue
            // state is changed. Economy Pawns remain under the Commander.
            var units = UnitController.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
                if (units[i] != null && units[i].Faction == Faction.Red
                    && units[i].GetComponent<PawnScoutPromotion>()?.IsPromoted == true)
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

        private static void ObserveBlueClaim()
        {
            if (!mine.HasOwner || mine.Faction != Faction.Blue
                || !ReferenceEquals(blueMiner?.GetComponent<UnitStationing>()?.Host,
                    mine)) return;
            Require(mine.OccupantCount > 0,
                "Blue Pawn did not occupy the claimed mine.");
            stage = 8;
            deadline = Time.time + 90f;
            Debug.Log("PHASE_7E7_BLUE_MINE_CLAIMED");
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
            Debug.Log("PHASE_7E7_BLUE_CLAIM_PASSED: Blue Pawn entered, claimed, "
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

        private static string Diagnose()
        {
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
            if (mine == null) return result + ", mine=none";
            result += ", mine=" + mine.GetEntityId() + ", state=" + mine.State
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
            Time.timeScale = 1f;
            if (error != null) Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
