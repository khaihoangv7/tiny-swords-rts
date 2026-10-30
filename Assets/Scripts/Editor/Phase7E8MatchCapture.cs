using System;
using System.Collections.Generic;
using System.IO;
using TinyKingdom.AI;
using TinyKingdom.Buildings;
using TinyKingdom.Debugging;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    // Play Mode observation only. The capture never issues gameplay commands.
    public static class Phase7E8MatchCapture
    {
        private static Capture active;

        [MenuItem("Tools/Phase 7E.8/Start full-match capture x1")]
        public static void StartX1() => Start(1f);

        [MenuItem("Tools/Phase 7E.8/Start full-match capture x2")]
        public static void StartX2() => Start(2f);

        [MenuItem("Tools/Phase 7E.8/Start full-match capture x5")]
        public static void StartX5() => Start(5f);

        [MenuItem("Tools/Phase 7E.8/Start full-match capture x10")]
        public static void StartX10() => Start(10f);

        private static void Start(float speed)
        {
            if (!EditorApplication.isPlaying || MatchManager.Instance == null
                || MatchManager.Instance.State != MatchState.Playing)
                throw new InvalidOperationException("Start a playable Map scene first.");
            if (active != null)
                throw new InvalidOperationException("A full-match capture is already active.");
            MatchSpeedController speedController = MatchSpeedController.Instance;
            if (speedController == null || !speedController.SetSpeed(speed))
                throw new InvalidOperationException("Could not set match speed to x" + speed);
            active = new Capture(speed);
            active.Start();
        }

        [Serializable]
        private sealed class FactionSample
        {
            public string faction;
            public int food;
            public int wood;
            public int gold;
            public int pawns;
            public int military;
            public int activeMiners;
            public int occupiedMineSlots;
            public int buildingsUnderConstruction;
            public int completedBuildings;
            public int cargoRecoveriesOnActivePawns;
            public int dropOffPathFailuresOnActivePawns;
            public int builderPathFailuresOnActivePawns;
        }

        [Serializable]
        private sealed class NewUnitCounts
        {
            public int pawn;
            public int warrior;
            public int lancer;
            public int archer;
            public int monk;

            public void Add(UnitType type)
            {
                switch (type)
                {
                    case UnitType.Pawn: pawn++; break;
                    case UnitType.Warrior: warrior++; break;
                    case UnitType.Lancer: lancer++; break;
                    case UnitType.Archer: archer++; break;
                    case UnitType.Monk: monk++; break;
                }
            }
        }

        [Serializable]
        private sealed class Sample
        {
            public float gameSeconds;
            public double realSeconds;
            public string matchState;
            public string redIntent;
            public long redThinkTicks;
            public long pathRequests;
            public long aiCommandAttempts;
            public int mineRemainingGold;
            public string mineOwner;
            public FactionSample blue;
            public FactionSample red;
        }

        [Serializable]
        private sealed class Report
        {
            public string startedUtc;
            public string finishedUtc;
            public float requestedSpeed;
            public bool completed;
            public string result;
            public float finalGameSeconds;
            public double realSeconds;
            public int runtimeErrors;
            public int runtimeWarnings;
            public int blueCastleGoldDeposits;
            public int redCastleGoldDeposits;
            public int blueCastleGoldAmount;
            public int redCastleGoldAmount;
            public int blueGrossFoodGains;
            public int blueGrossWoodGains;
            public int blueGrossGoldGains;
            public int redGrossFoodGains;
            public int redGrossWoodGains;
            public int redGrossGoldGains;
            public long pathRequestsDuringCapture;
            public long aiCommandAttemptsDuringCapture;
            public long observedFrames;
            public double averageFrameMs;
            public double worstFrameMs;
            public long framesOver33Ms;
            public long framesOver100Ms;
            public int blueNewBuildingsCompleted;
            public int redNewBuildingsCompleted;
            public NewUnitCounts blueNewUnits = new();
            public NewUnitCounts redNewUnits = new();
            public uint redGoldMineFailedAttempts;
            public List<Sample> samples = new();
        }

        private sealed class Capture
        {
            private readonly Report report = new();
            private readonly MatchManager match;
            private readonly ResourceStockpile blueStockpile;
            private readonly ResourceStockpile redStockpile;
            private readonly CastleDropOff blueCastle;
            private readonly CastleDropOff redCastle;
            private readonly RuleBasedCommander redCommander;
            private readonly int[] previousBlue = new int[3];
            private readonly int[] previousRed = new int[3];
            private readonly long startingPaths;
            private readonly long startingCommands;
            private readonly uint startingMineFailures;
            private readonly double startedAt;
            private readonly HashSet<UnitController> seenUnits = new();
            private readonly HashSet<BuildingSite> seenCompletedBuildings = new();
            private double frameTotalMs;
            private double worstFrameMs;
            private long frameCount;
            private double nextSampleAt;
            private bool finished;

            public Capture(float speed)
            {
                match = MatchManager.Instance;
                blueCastle = match.BlueMainCastle;
                redCastle = match.RedMainCastle;
                blueStockpile = blueCastle?.Stockpile;
                redStockpile = redCastle?.Stockpile;
                if (blueStockpile == null || redStockpile == null)
                    throw new InvalidOperationException("Both Castle stockpiles must exist.");
                RuleBasedCommander[] commanders = UnityEngine.Object
                    .FindObjectsByType<RuleBasedCommander>(FindObjectsInactive.Exclude);
                for (int i = 0; i < commanders.Length; i++)
                    if (commanders[i] != null && commanders[i].Faction == Faction.Red)
                    { redCommander = commanders[i]; break; }
                if (redCommander == null)
                    throw new InvalidOperationException("No Red Commander in the match.");
                report.requestedSpeed = speed;
                report.startedUtc = DateTime.UtcNow.ToString("O");
                ReadAmounts(blueStockpile, previousBlue);
                ReadAmounts(redStockpile, previousRed);
                startedAt = EditorApplication.timeSinceStartup;
                startingPaths = PathfindingSystem.TotalRequests;
                startingCommands = TotalCommandAttempts();
                startingMineFailures = redCommander.GoldMineWorkers.FailedAttemptCount;
                SeedObservedObjects();
            }

            public void Start()
            {
                blueStockpile.Changed += OnBlueStockpileChanged;
                redStockpile.Changed += OnRedStockpileChanged;
                blueCastle.Deposited += OnBlueDeposit;
                redCastle.Deposited += OnRedDeposit;
                match.MatchEnded += OnMatchEnded;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
                SampleNow();
                Debug.Log("PHASE_7E8_CAPTURE_STARTED x" + report.requestedSpeed
                    + "; observer mode does not change AI knowledge.");
            }

            private void Tick()
            {
                if (!EditorApplication.isPlaying || match == null)
                { Finish(false); return; }
                double frameMs = Time.unscaledDeltaTime * 1000d;
                frameCount++;
                frameTotalMs += frameMs;
                worstFrameMs = Math.Max(worstFrameMs, frameMs);
                if (frameMs >= 33.3d) report.framesOver33Ms++;
                if (frameMs >= 100d) report.framesOver100Ms++;
                ObserveNewObjects();
                if (EditorApplication.timeSinceStartup < nextSampleAt) return;
                SampleNow();
                if (match.IsMatchEnded) Finish(true);
            }

            private void OnMatchEnded(MatchResult _) => Finish(true);

            private void OnLog(string message, string stack, LogType type)
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert)
                    report.runtimeErrors++;
                else if (type == LogType.Warning) report.runtimeWarnings++;
            }

            private void OnBlueStockpileChanged()
                => RecordGains(blueStockpile, previousBlue, true);

            private void OnRedStockpileChanged()
                => RecordGains(redStockpile, previousRed, false);

            private void RecordGains(ResourceStockpile stockpile, int[] previous,
                bool blue)
            {
                if (stockpile == null) return;
                int food = stockpile.GetAmount(ResourceType.Food);
                int wood = stockpile.GetAmount(ResourceType.Wood);
                int gold = stockpile.GetAmount(ResourceType.Gold);
                if (blue)
                {
                    report.blueGrossFoodGains += Math.Max(0, food - previous[0]);
                    report.blueGrossWoodGains += Math.Max(0, wood - previous[1]);
                    report.blueGrossGoldGains += Math.Max(0, gold - previous[2]);
                }
                else
                {
                    report.redGrossFoodGains += Math.Max(0, food - previous[0]);
                    report.redGrossWoodGains += Math.Max(0, wood - previous[1]);
                    report.redGrossGoldGains += Math.Max(0, gold - previous[2]);
                }
                previous[0] = food; previous[1] = wood; previous[2] = gold;
            }

            private static void ReadAmounts(ResourceStockpile stockpile, int[] amounts)
            {
                amounts[0] = stockpile.GetAmount(ResourceType.Food);
                amounts[1] = stockpile.GetAmount(ResourceType.Wood);
                amounts[2] = stockpile.GetAmount(ResourceType.Gold);
            }

            private void OnBlueDeposit(ResourceType type, int amount)
            {
                if (type != ResourceType.Gold) return;
                report.blueCastleGoldDeposits++;
                report.blueCastleGoldAmount += amount;
            }

            private void OnRedDeposit(ResourceType type, int amount)
            {
                if (type != ResourceType.Gold) return;
                report.redCastleGoldDeposits++;
                report.redCastleGoldAmount += amount;
            }

            private void SampleNow()
            {
                if (match == null) return;
                nextSampleAt = EditorApplication.timeSinceStartup + 5d;
                var sample = new Sample
                {
                    gameSeconds = match.ElapsedGameplaySeconds,
                    realSeconds = EditorApplication.timeSinceStartup - startedAt,
                    matchState = match.State.ToString(),
                    redIntent = redCommander != null
                        ? redCommander.StrategicIntent.CurrentIntent.ToString() : "None",
                    redThinkTicks = redCommander != null
                        ? (long)redCommander.ThinkTick : 0,
                    pathRequests = PathfindingSystem.TotalRequests - startingPaths,
                    aiCommandAttempts = TotalCommandAttempts() - startingCommands,
                    blue = FactionState(Faction.Blue, blueStockpile),
                    red = FactionState(Faction.Red, redStockpile)
                };
                IReadOnlyList<GoldMineObjective> mines = GoldMineObjective.Active;
                for (int i = 0; i < mines.Count; i++)
                {
                    GoldMineObjective mine = mines[i];
                    if (mine == null) continue;
                    sample.mineRemainingGold += mine.RemainingGold;
                    sample.mineOwner = mine.HasOwner ? mine.Faction.ToString()
                        : mine.State.ToString();
                }
                report.samples.Add(sample);
            }

            private void SeedObservedObjects()
            {
                IReadOnlyList<UnitController> units = UnitController.ActiveUnits;
                for (int i = 0; i < units.Count; i++)
                    if (units[i] != null) seenUnits.Add(units[i]);
                IReadOnlyList<BuildingSite> sites = BuildingSite.ActiveSites;
                for (int i = 0; i < sites.Count; i++)
                    if (sites[i] != null && sites[i].State == BuildingState.Completed)
                        seenCompletedBuildings.Add(sites[i]);
            }

            private void ObserveNewObjects()
            {
                IReadOnlyList<UnitController> units = UnitController.ActiveUnits;
                for (int i = 0; i < units.Count; i++)
                {
                    UnitController unit = units[i];
                    if (unit == null || !seenUnits.Add(unit)) continue;
                    UnitType? type = unit.GetComponent<HealthComponent>()
                        ?.CombatData?.UnitType;
                    if (!type.HasValue) continue;
                    if (unit.Faction == Faction.Blue)
                        report.blueNewUnits.Add(type.Value);
                    else if (unit.Faction == Faction.Red)
                        report.redNewUnits.Add(type.Value);
                }
                IReadOnlyList<BuildingSite> sites = BuildingSite.ActiveSites;
                for (int i = 0; i < sites.Count; i++)
                {
                    BuildingSite site = sites[i];
                    if (site == null || site.State != BuildingState.Completed
                        || !seenCompletedBuildings.Add(site)) continue;
                    if (site.Faction == Faction.Blue) report.blueNewBuildingsCompleted++;
                    else if (site.Faction == Faction.Red)
                        report.redNewBuildingsCompleted++;
                }
            }

            private static FactionSample FactionState(Faction faction,
                ResourceStockpile stockpile)
            {
                var result = new FactionSample
                {
                    faction = faction.ToString(),
                    food = stockpile != null ? stockpile.GetAmount(ResourceType.Food) : -1,
                    wood = stockpile != null ? stockpile.GetAmount(ResourceType.Wood) : -1,
                    gold = stockpile != null ? stockpile.GetAmount(ResourceType.Gold) : -1
                };
                IReadOnlyList<UnitController> units = UnitController.ActiveUnits;
                for (int i = 0; i < units.Count; i++)
                {
                    UnitController unit = units[i];
                    if (unit == null || !unit.IsAlive || unit.Faction != faction) continue;
                    PawnGatherer gatherer = unit.GetComponent<PawnGatherer>();
                    if (gatherer != null)
                    {
                        result.pawns++;
                        result.cargoRecoveriesOnActivePawns += gatherer.CargoRecoveryCount;
                        result.dropOffPathFailuresOnActivePawns += gatherer.DropOffPathFailureCount;
                        PawnBuilder builder = unit.GetComponent<PawnBuilder>();
                        if (builder != null)
                            result.builderPathFailuresOnActivePawns += builder.PathFailureCount;
                        if (gatherer.MineReturnTarget != null
                            || gatherer.State == WorkerState.WaitingForMine)
                            result.activeMiners++;
                    }
                    else result.military++;
                }
                IReadOnlyList<BuildingSite> sites = BuildingSite.ActiveSites;
                for (int i = 0; i < sites.Count; i++)
                {
                    BuildingSite site = sites[i];
                    if (site == null || site.Faction != faction || !site.IsAlive) continue;
                    if (site.State == BuildingState.Completed)
                        result.completedBuildings++;
                    else result.buildingsUnderConstruction++;
                }
                IReadOnlyList<GoldMineObjective> mines = GoldMineObjective.Active;
                for (int i = 0; i < mines.Count; i++)
                    if (mines[i] != null && mines[i].HasOwner
                        && mines[i].Faction == faction)
                        result.occupiedMineSlots += mines[i].OccupantCount;
                return result;
            }

            private static long TotalCommandAttempts()
            {
                long total = 0;
                foreach (CommanderCommandCategory category
                    in Enum.GetValues(typeof(CommanderCommandCategory)))
                    total += FactionCommandService.GetCommandAttemptCount(category);
                return total;
            }

            private void Finish(bool completed)
            {
                if (finished) return;
                finished = true;
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                if (match != null) match.MatchEnded -= OnMatchEnded;
                if (blueStockpile != null) blueStockpile.Changed -= OnBlueStockpileChanged;
                if (redStockpile != null) redStockpile.Changed -= OnRedStockpileChanged;
                if (blueCastle != null) blueCastle.Deposited -= OnBlueDeposit;
                if (redCastle != null) redCastle.Deposited -= OnRedDeposit;
                if (EditorApplication.isPlaying && match != null) SampleNow();
                report.completed = completed;
                report.result = match != null ? match.Result.ToString() : "Interrupted";
                report.finalGameSeconds = match != null
                    ? match.ElapsedGameplaySeconds : 0f;
                report.realSeconds = EditorApplication.timeSinceStartup - startedAt;
                report.pathRequestsDuringCapture = PathfindingSystem.TotalRequests
                    - startingPaths;
                report.aiCommandAttemptsDuringCapture = TotalCommandAttempts()
                    - startingCommands;
                report.observedFrames = frameCount;
                report.averageFrameMs = frameCount > 0 ? frameTotalMs / frameCount : 0d;
                report.worstFrameMs = worstFrameMs;
                report.redGoldMineFailedAttempts = redCommander != null
                    ? redCommander.GoldMineWorkers.FailedAttemptCount - startingMineFailures
                    : 0;
                report.finishedUtc = DateTime.UtcNow.ToString("O");
                string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Logs/Phase7E8Match-x" + report.requestedSpeed + "-"
                    + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                active = null;
                Debug.Log("PHASE_7E8_CAPTURE_" + (completed ? "COMPLETE" : "PARTIAL")
                    + ": " + path);
            }
        }
    }
}
