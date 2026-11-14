using System;
using System.Globalization;
using System.IO;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Debugging;
using TinyKingdom.Economy;
using TinyKingdom.Match;
using TinyKingdom.Terrain;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyKingdom.AI
{
    public sealed class QlExperiment : MonoBehaviour
    {
        private const float MatchLimit = 1800f;
        private const float TimePenaltyPerSecond = 0.0001f;
        private const float ArmyCasualtyPenalty = 0.04f;
        private const int PenalizedCasualtyLimit = 10;
        private const float GroupRewardRadius = 4.5f;
        private const float MaxArmyLossPenalty = 0.65f;
        private const float IslandFoodSearchRadius = 42f;
        private const int FoodExplorationThreshold = 120;
        private static QlTable demoTable;
        private static bool swap;
        private readonly int[] military = new int[2];
        private readonly int[] blueUnitsTrained = new int[5];
        private readonly int[] blueLivingCombatTypes = new int[3];
        private int blueLivingCombatants;
        private int bluePeakCombatants;
        private int blueCombatLosses;
        private int blueIsolatedIslandLosses;
        private int blueHealthyGroupPeak;
        private float nextGroupSample;
        private float armyLossPenalty;
        private bool rewardedCastleLaunch;
        private bool rewardedMixedArmy;
        private float armyReward;
        private readonly float[] firstMilitary = { -1f, -1f };
        private readonly float[] firstAttack = { -1f, -1f };
        private readonly uint[] seenLaunches = new uint[2];
        private readonly int[] recruits = new int[2];
        private int bluePawnOrders;
        private int bluePawnsTrained;
        private int blueMilitaryOrders;
        private int blueBarracksOrders;
        private float firstBluePawn = -1f;
        private readonly int[] orders = new int[2];
        private readonly int[] rejected = new int[2];
        private int[] blueActionCounts;
        private int[] blueAcceptedActionCounts;
        private MatchManager match;
        private RuleBasedCommander blue;
        private RuleBasedCommander red;
        private RuleBasedCommander foodForagingCommander;
        private bool seekingFoodFrontier;
        private RlCommanderBridge learner;
        private RlCommanderBridge ppo;
        private QlPolicy trainer;
        private string exchange;
        private float limit;
        private string failure;
        private string outcome;
        private bool observing;
        private bool shaping;
        private bool armyShaping;
        private bool cohesionShaping;
        private bool blueBarracksReady;
        private bool blueBarracksStarted;
        private float lowestRedCastleHp;
        private float lowestBlueCastleHp;
        private float blueCastleMaxHp;
        private float redCastleMaxHp;
        private float castleReward;
        private float shapingReward;
        private float timePenalty;
        private bool bootstrapPending;
        private float firstCastleSight = -1f;
        private GUIStyle style;

        public static QlExperiment Active { get; private set; }
        public static bool IsActive => Active != null;
        public static bool Requested => demoTable != null || Arg("-tk-ql-train=") != null;
        public static string DefaultTablePath => Path.Combine(Application.persistentDataPath, "q_table_v2.json");
        public string Pairing => swap ? "BLUE / PPO     RED / Q-learning" : "BLUE / Q-learning     RED / PPO";
        internal bool Controls(RuleBasedCommander commander) => commander == (swap ? red : blue);
        internal static bool UsesGoblinIslandStrategy(RuleBasedCommander commander)
            => Active != null && Active.Controls(commander)
                && SceneManager.GetActiveScene().name == "GoblinIsland";
        internal static bool IsGoblinIslandTrainingOpponent(RuleBasedCommander commander)
            => Active != null && Active.exchange != null && Active.red == commander
                && SceneManager.GetActiveScene().name == "GoblinIsland";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => ClearDemo();

        public static void ClearDemo()
        {
            demoTable = null;
            swap = false;
        }

        public static bool TrySelectDemo(bool swapSides, out string error)
        {
            try
            {
                string path = Arg("-tk-ql-table=") ?? DefaultTablePath;
                if (!File.Exists(path))
                {
                    error = "Q-TABLE MISSING. TRAIN FIRST.";
                    return false;
                }
                QlTable table = JsonUtility.FromJson<QlTable>(File.ReadAllText(path));
                if (table == null || table.version != QlTable.SchemaVersion || table.actions != RlActionRouter.ActionCount
                    || table.episodes < 1 || table.rows == null || table.rows.Length == 0
                    || table.rows.Any(row => row == null || string.IsNullOrEmpty(row.state)
                        || row.values == null || row.values.Length != table.actions
                        || row.values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                    || table.rows.Select(row => row.state).Distinct().Count() != table.rows.Length)
                {
                    error = "Q-TABLE INVALID.";
                    return false;
                }
                demoTable = table;
                swap = swapSides;
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or ArgumentException
                or UnauthorizedAccessException)
            {
                error = "Q-TABLE COULD NOT BE READ.";
                return false;
            }
        }

        internal static string Arg(string prefix)
        {
            string arg = Array.Find(Environment.GetCommandLineArgs(), value => value.StartsWith(prefix,
                StringComparison.Ordinal));
            return arg?.Substring(prefix.Length);
        }

        public void Configure(MatchManager authority, RuleBasedCommander blueCommander,
            RuleBasedCommander redCommander)
        {
            Active = this;
            match = authority;
            blue = blueCommander;
            red = redCommander;
            exchange = Arg("-tk-ql-train=");
            if (exchange != null && Arg("-tk-ql-eda") != null)
            {
                blueActionCounts = new int[RlActionRouter.ActionCount];
                blueAcceptedActionCounts = new int[RlActionRouter.ActionCount];
            }
            shaping = exchange != null && Arg("-tk-ql-shaping") != null;
            armyShaping = shaping && SceneManager.GetActiveScene().name == "GoblinIsland";
            cohesionShaping = armyShaping && Arg("-tk-ql-cohesion-shaping") != null;
            lowestRedCastleHp = match.RedMainCastle.CurrentHP;
            lowestBlueCastleHp = match.BlueMainCastle.CurrentHP;
            redCastleMaxHp = match.RedMainCastle.MaxHP;
            blueCastleMaxHp = match.BlueMainCastle.MaxHP;
            limit = MatchLimit;
            if (exchange != null && float.TryParse(Arg("-tk-ql-limit="), NumberStyles.Float,
                CultureInfo.InvariantCulture, out float seconds) && seconds >= 10 && seconds <= MatchLimit)
                limit = seconds;
            match.MatchEnded += OnMatchEnded;
            if (blue == null || red == null)
            {
                Fail("Both factions need a configured Commander");
                return;
            }
            RuleBasedCommander owner = swap ? red : blue;
            if (exchange != null && UsesGoblinIslandStrategy(owner)
                && Arg("-tk-ql-island-guidance") != null)
            {
                foodForagingCommander = owner;
                owner.EconomyPolicy.SetExtendedFoodArea(IsIslandFoodForagingPosition);
            }
            learner = owner.GetComponent<RlCommanderBridge>();
            owner.ProductionPolicy.RequireExternalUnit = true;
            owner.BuildingPolicy.RequireExternalBuilding = true;
            if (exchange != null)
            {
                trainer = owner.gameObject.AddComponent<QlPolicy>();
                trainer.Configure(learner, owner, exchange, shaping, cohesionShaping);
                learner.ConfigureAsync(owner, trainer);
                learner.SetMode(RlCommanderMode.Training);
            }
            else
            {
                learner.Configure(owner, new QlFrozenPolicy(learner, owner, demoTable));
                learner.SetMode(RlCommanderMode.RL);
                RuleBasedCommander opponent = swap ? blue : red;
                ppo = opponent.GetComponent<RlCommanderBridge>();
                if (!RlMlBootstrap.AttachIfRequested(opponent.gameObject, opponent, ppo, true))
                {
                    Fail("PPO model could not load");
                    return;
                }
                observing = true;
            }
            bootstrapPending = true;
            Debug.Log($"QL_MATCH_START map={SceneManager.GetActiveScene().name} "
                + $"mode={(exchange == null ? Pairing : "BLUE / Q-learning     RED / Rule-Based")} "
                + $"limit={limit}");
        }

        private void Update()
        {
            if (failure != null || match == null || match.State != MatchState.Playing) return;
            if (bootstrapPending)
            {
                bootstrapPending = false;
                if (exchange != null)
                {
                    observing = Arg("-tk-ql-watch") != null;
                    float speed = observing ? MatchSpeedController.DefaultSpeed : 10f;
                    if (int.TryParse(Arg("-tk-ql-speed="), out int requested)
                        && MatchSpeedController.IsSupported(requested))
                        speed = requested;
                    MatchSpeedController speedController = MatchSpeedController.Instance
                        ?? gameObject.AddComponent<MatchSpeedController>();
                    speedController.SetSpeed(speed);
                    Debug.Log($"QL_MATCH_SPEED scale={Time.timeScale:0}");
                }
                if (observing)
                {
                    DebugObserverView.SetEnabled(true);
                    FindAnyObjectByType<FogRenderer>()?.SetFogVisible(false);
                }
            }
            if (learner.Mode is RlCommanderMode.Fallback or RlCommanderMode.TrainingAborted
                || ppo != null && ppo.Mode == RlCommanderMode.Fallback)
            {
                Fail(learner.FallbackReason ?? ppo?.FallbackReason ?? "A policy became unavailable");
                return;
            }
            TrackAttack(blue);
            TrackAttack(red);
            TrackMilitaryProgress();
            if (cohesionShaping) TrackArmyCohesion();
            if (foodForagingCommander != null)
                UpdateFoodExploration();
            if (firstCastleSight < 0f && blue.Knowledge.KnownEnemyBuildings.Any(
                    item => item.BuildingType == BuildingType.Castle && item.Faction == Faction.Red))
            {
                firstCastleSight = match.ElapsedGameplaySeconds;
                Debug.Log($"QL_RED_CASTLE_DISCOVERED time={firstCastleSight:F2}");
            }
            ApplyTimePenalty();
            if (match.ElapsedGameplaySeconds >= limit) match.EndAtTimeLimit();
        }

        internal void RecordAction(RuleBasedCommander owner, int id, RlActionResult result, bool enqueued)
        {
            if (owner == blue) trainer?.MarkActionProcessed();
            int index = owner.Faction == Faction.Blue ? 0 : 1;
            orders[index]++;
            if (result != RlActionResult.Accepted) rejected[index]++;
            if (index == 0 && blueActionCounts != null && (uint)id < (uint)blueActionCounts.Length)
            {
                blueActionCounts[id]++;
                if (result == RlActionResult.Accepted) blueAcceptedActionCounts[id]++;
            }
            if (orders[index] == 1)
                Debug.Log($"QL_FIRST_ACTION faction={owner.Faction} id={id} result={result}");
            if (index == 0 && id == (int)RlAction.Attack
                && (result != RlActionResult.Accepted || !owner.MilitaryPolicy.HasOffensiveTarget))
                Debug.Log($"QL_ATTACK_PENDING time={match.ElapsedGameplaySeconds:F2} result={result}"
                    + $" intent={owner.StrategicIntent.CurrentIntent} army={owner.MilitaryPolicy.Army.State}"
                    + $" reason={owner.MilitaryPolicy.ObjectiveReason}");
            if (index == 0 && id == (int)RlAction.BuildBarracks && ++blueBarracksOrders == 1)
                Debug.Log($"QL_FIRST_BARRACKS_ORDER result={result}");
            if (id == (int)RlAction.TrainPawn && index == 0)
            {
                if (enqueued && ++bluePawnOrders <= 3) Award(0.03f);
                Debug.Log($"QL_TRAIN faction={owner.Faction} id={id} result={result} enqueued={enqueued}");
            }
            if (id is >= 2 and <= 5)
            {
                if (enqueued)
                {
                    recruits[index]++;
                    if (!armyShaping && index == 0 && id != (int)RlAction.TrainMonk
                        && ++blueMilitaryOrders <= 8)
                        Award(0.025f);
                }
                Debug.Log($"QL_TRAIN faction={owner.Faction} id={id} result={result} enqueued={enqueued}");
            }
        }

        internal void RecordPawnSpawn(Faction faction)
        {
            if (faction != Faction.Blue) return;
            if (++bluePawnsTrained <= 3) Award(0.015f);
            if (firstBluePawn < 0f) firstBluePawn = match.ElapsedGameplaySeconds;
        }

        public void RecordMilitarySpawn(Faction faction, UnitType type, HealthComponent health)
        {
            if (UsesGoblinIslandStrategy(faction == Faction.Blue ? blue : red)
                && health != null && health.TryGetComponent(out CombatController combat))
                combat.HoldPositionWhenIdle = true;
            int index = faction == Faction.Blue ? 0 : 1;
            military[index]++;
            if (index == 0 && (int)type < blueUnitsTrained.Length)
            {
                int completed = ++blueUnitsTrained[(int)type];
                if (type is (UnitType.Warrior or UnitType.Archer or UnitType.Lancer))
                {
                    if (armyShaping && health != null)
                        RegisterArmySpawn(type, health);
                    else if (!armyShaping)
                    {
                        if (completed == 1) Award(type == UnitType.Warrior ? 0.08f : 0.025f);
                        if (blueUnitsTrained[(int)UnitType.Warrior] + blueUnitsTrained[(int)UnitType.Archer]
                            + blueUnitsTrained[(int)UnitType.Lancer] <= 8)
                            Award(0.02f);
                    }
                }
            }
            if (firstMilitary[index] >= 0f) return;
            firstMilitary[index] = match.ElapsedGameplaySeconds;
            Debug.Log($"QL_FIRST_MILITARY faction={faction} type={type} time={firstMilitary[index]:F2}");
        }

        private void RegisterArmySpawn(UnitType type, HealthComponent health)
        {
            health.Died += RecordArmyLoss;
            blueLivingCombatTypes[(int)type]++;
            blueLivingCombatants++;
            if (cohesionShaping)
            {
                bluePeakCombatants = Mathf.Max(bluePeakCombatants, blueLivingCombatants);
                return;
            }
            float earned = 0f;
            if (blueLivingCombatants > bluePeakCombatants)
            {
                bluePeakCombatants = blueLivingCombatants;
                if (bluePeakCombatants <= 6) earned += 0.04f;
                else if (bluePeakCombatants <= 12) earned += 0.0125f;
            }
            if (!rewardedMixedArmy && blueLivingCombatants >= 6
                && blueLivingCombatTypes[(int)UnitType.Warrior] > 0
                && blueLivingCombatTypes[(int)UnitType.Archer] > 0
                && blueLivingCombatTypes[(int)UnitType.Lancer] > 0)
            {
                rewardedMixedArmy = true;
                earned += 0.035f;
            }
            armyReward += earned;
            Award(earned);
        }

        private void RecordArmyLoss(HealthComponent health)
        {
            health.Died -= RecordArmyLoss;
            blueLivingCombatTypes[(int)health.CombatData.UnitType]--;
            blueLivingCombatants--;
            blueCombatLosses++;
            if (match.State != MatchState.Playing) return;
            MilitaryCommanderPolicy policy = blue.MilitaryPolicy;
            bool isolatedIslandLoss = cohesionShaping && IsIsolatedIslandLoss(health, policy);
            if (isolatedIslandLoss) blueIsolatedIslandLosses++;
            Debug.Log($"QL_BLUE_COMBAT_LOSS time={match.ElapsedGameplaySeconds:F2}"
                + $" unit={health.CombatData.UnitType} army={policy.Army.State}"
                + $" position={health.transform.position} alive={blueLivingCombatants}"
                + $" isolatedIsland={isolatedIslandLoss} reason={policy.ObjectiveReason}");
            float penalty = blueCombatLosses <= PenalizedCasualtyLimit ? ArmyCasualtyPenalty : 0f;
            if (cohesionShaping)
            {
                float cost = isolatedIslandLoss ? 0.075f : IsSiegingRedCastle(policy) ? 0.015f : 0.025f;
                penalty = Mathf.Min(cost, Mathf.Max(0f, MaxArmyLossPenalty - armyLossPenalty));
                armyLossPenalty += penalty;
            }
            AwardArmyProgress(-penalty);
        }

        private bool IsIsolatedIslandLoss(HealthComponent health, MilitaryCommanderPolicy policy)
        {
            var grid = blue.Knowledge.TeamVision?.GridManager;
            if (grid == null || IsSiegingRedCastle(policy)) return false;
            Vector3 position = health.transform.position;
            Vector2Int cell = grid.WorldToGrid(position);
            if (!GoblinIslandLayout.IsIsland(cell.x, cell.y)) return false;
            int nearby = 0;
            foreach (UnitController unit in blue.Knowledge.OwnUnits)
            {
                if (!ArmyGroup.IsEligible(unit, Faction.Blue, blue.CurrentScout)
                    || !HarassmentRuleBasedCommander.IsReadyCastleAttacker(unit)
                    || ((Vector2)(unit.transform.position - position)).sqrMagnitude
                        > GroupRewardRadius * GroupRewardRadius) continue;
                if (++nearby >= 2) return false;
            }
            return true;
        }

        private void TrackArmyCohesion()
        {
            float now = match.ElapsedGameplaySeconds;
            if (now < nextGroupSample || blueHealthyGroupPeak >= 8) return;
            nextGroupSample = now + 1f;
            var members = blue.MilitaryPolicy.Army.Members;
            int largest = 0;
            foreach (UnitController center in members)
            {
                if (!center || !center.IsAlive || !HarassmentRuleBasedCommander.IsReadyCastleAttacker(center))
                    continue;
                int nearby = 0;
                foreach (UnitController unit in members)
                {
                    if (!unit || !unit.IsAlive || !HarassmentRuleBasedCommander.IsReadyCastleAttacker(unit)
                        || ((Vector2)(unit.transform.position - center.transform.position)).sqrMagnitude
                            > GroupRewardRadius * GroupRewardRadius) continue;
                    nearby++;
                }
                largest = Mathf.Max(largest, nearby);
            }
            if (largest <= blueHealthyGroupPeak) return;
            float earned = GroupMilestoneReward(largest) - GroupMilestoneReward(blueHealthyGroupPeak);
            blueHealthyGroupPeak = largest;
            if (earned <= 0f) return;
            AwardArmyProgress(earned);
            Debug.Log($"QL_ARMY_GROUP time={now:F2} healthy={largest} reward={earned:F3}");
        }

        private static float GroupMilestoneReward(int count)
            => (count >= 3 ? 0.06f : 0f)
                + (count >= MilitaryCommanderPolicy.IslandAssaultMinimum ? 0.24f : 0f)
                + (count >= 8 ? 0.08f : 0f);

        private static bool IsSiegingRedCastle(MilitaryCommanderPolicy policy)
            => policy.HasOffensiveTarget
                && policy.OffensiveTarget.BuildingType == BuildingType.Castle
                && policy.Army.State is ArmyGroupState.Advancing or ArmyGroupState.Assaulting;

        private void AwardArmyProgress(float value)
        {
            armyReward += value;
            Award(value);
        }

        private void TrackAttack(RuleBasedCommander owner)
        {
            int index = owner.Faction == Faction.Blue ? 0 : 1;
            MilitaryCommanderPolicy policy = owner.MilitaryPolicy;
            uint launches = policy.OffensiveLaunchSerial;
            if (seenLaunches[index] == launches) return;
            seenLaunches[index] = launches;
            if (!policy.HasOffensiveTarget
                || policy.OffensiveTarget.BuildingType == BuildingType.GoldMine)
                return;
            float time = match.ElapsedGameplaySeconds;
            if (firstAttack[index] < 0f) firstAttack[index] = time;
            if (index == 0 && cohesionShaping && !rewardedCastleLaunch
                && policy.OffensiveTarget.BuildingType == BuildingType.Castle
                && policy.OffensiveParticipantCount >= MilitaryCommanderPolicy.IslandAssaultMinimum)
            {
                rewardedCastleLaunch = true;
                AwardArmyProgress(0.10f);
            }
            Debug.Log($"QL_ARMY_LAUNCHED faction={owner.Faction} target={policy.OffensiveTarget.BuildingType}"
                + $" troops={policy.OffensiveParticipantCount} time={time:F2}");
        }

        private void TrackMilitaryProgress()
        {
            if (!blueBarracksReady)
            {
                if (!blueBarracksStarted && blue.BuildingPolicy.ActiveSite is { Type: BuildingType.Barracks } site)
                {
                    blueBarracksStarted = true;
                    Debug.Log($"QL_BLUE_BARRACKS_STARTED time={match.ElapsedGameplaySeconds:F2} origin={site.Origin}");
                }
                foreach (UnitProductionQueue queue in UnitProductionQueue.ActiveQueues)
                {
                    if (queue == null || queue.ProductionHost is not { } host
                        || host.Faction != Faction.Blue || host.Type != BuildingType.Barracks
                        || !host.IsProductionReady) continue;
                    blueBarracksReady = true;
                    Debug.Log($"QL_BLUE_BARRACKS_READY time={match.ElapsedGameplaySeconds:F2}");
                    Award(0.12f);
                    break;
                }
            }
            float redLow = match.RedMainCastle == null ? lowestRedCastleHp
                : Mathf.Min(lowestRedCastleHp, Mathf.Max(0f, match.RedMainCastle.CurrentHP));
            float blueLow = match.BlueMainCastle == null ? lowestBlueCastleHp
                : Mathf.Min(lowestBlueCastleHp, Mathf.Max(0f, match.BlueMainCastle.CurrentHP));
            if (armyShaping)
            {
                float progress = (cohesionShaping ? 0.55f : 0.35f)
                    * (lowestRedCastleHp - redLow) / redCastleMaxHp
                    - 0.25f * (lowestBlueCastleHp - blueLow) / blueCastleMaxHp;
                castleReward += progress;
                Award(progress);
            }
            lowestRedCastleHp = redLow;
            lowestBlueCastleHp = blueLow;
        }

        private void Award(float value)
        {
            if (!shaping || trainer == null) return;
            shapingReward += value;
            trainer.AddReward(value);
        }

        private void ApplyTimePenalty()
        {
            if (!shaping || trainer == null) return;
            float elapsed = Mathf.Min(match.ElapsedGameplaySeconds, limit);
            float totalPenalty = elapsed * TimePenaltyPerSecond;
            float increment = totalPenalty - timePenalty;
            if (increment <= 0f) return;
            timePenalty = totalPenalty;
            trainer.AddReward(-increment);
        }

        private void OnMatchEnded(MatchResult result)
        {
            TrackMilitaryProgress();
            ApplyTimePenalty();
            outcome = result == MatchResult.Timeout ? "TIMEOUT" : result == MatchResult.BlueVictory
                ? "BLUE WINS" : "RED WINS";
            string metrics = FormattableString.Invariant($"QL_RESULT map={SceneManager.GetActiveScene().name} result={result} seconds={match.ElapsedGameplaySeconds:F2} blueOrders={orders[0]} redOrders={orders[1]} blueRejected={rejected[0]} redRejected={rejected[1]} blueRecruits={recruits[0]} redRecruits={recruits[1]} blueMilitary={military[0]} redMilitary={military[1]} blueFirstMilitary={firstMilitary[0]:F2} redFirstMilitary={firstMilitary[1]:F2} blueFirstAttack={firstAttack[0]:F2} redFirstAttack={firstAttack[1]:F2} blueCastleHp={match.BlueMainCastle?.CurrentHP ?? 0} redCastleHp={match.RedMainCastle?.CurrentHP ?? 0}");
            metrics += FormattableString.Invariant($" bluePawnsTrained={bluePawnsTrained} blueFirstPawn={firstBluePawn:F2} blueBarracksStarted={(blueBarracksStarted ? 1 : 0)} blueBarracksReady={(blueBarracksReady ? 1 : 0)} redCastleLowestHp={lowestRedCastleHp:F0} shapingReward={shapingReward:F3} timePenalty={-timePenalty:F4}");
            metrics += $" blueWarriors={blueUnitsTrained[(int)UnitType.Warrior]}"
                + $" blueArchers={blueUnitsTrained[(int)UnitType.Archer]}"
                + $" blueLancers={blueUnitsTrained[(int)UnitType.Lancer]}"
                + FormattableString.Invariant($" blueFirstCastleSight={firstCastleSight:F2}");
            if (armyShaping)
                metrics += FormattableString.Invariant($" blueArmyPeak={bluePeakCombatants} blueArmyAlive={blueLivingCombatants} blueCombatLosses={blueCombatLosses} armyReward={armyReward:F3} castleReward={castleReward:F3}");
            if (cohesionShaping)
                metrics += FormattableString.Invariant($" blueHealthyGroupPeak={blueHealthyGroupPeak} blueIsolatedIslandLosses={blueIsolatedIslandLosses} castleGroupLaunch={(rewardedCastleLaunch ? 1 : 0)}");
            if (blueActionCounts != null)
                metrics += FormattableString.Invariant($" seed={RlMlBootstrap.CurrentSeed}")
                    + $" blueActionCounts={string.Join(",", blueActionCounts)}"
                    + $" blueAcceptedActionCounts={string.Join(",", blueAcceptedActionCounts)}";
            Debug.Log(metrics);
            if (exchange != null) File.AppendAllText(Path.Combine(exchange, "matches.log"), metrics + Environment.NewLine);
        }

        internal void Fail(string reason)
        {
            failure = reason;
            Debug.LogError("QL_MATCH_ABORT: " + reason);
            if (exchange != null) Stop(1);
            else Time.timeScale = 0f;
        }

        internal void FinishRun() => Stop(0);

        private static void Stop(int code)
        {
#if UNITY_EDITOR
            if (Application.isBatchMode || Arg("-tk-ql-watch") != null)
                UnityEditor.EditorApplication.Exit(code);
            else UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit(code);
#endif
        }

        private void OnGUI()
        {
            if (!observing && failure == null) return;
            style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 19, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            float width = Mathf.Min(Screen.width, 530f);
            var rect = new Rect((Screen.width - width) / 2f, 12f, width, 46f);
            GUI.Box(rect, GUIContent.none);
            string status = failure ?? outcome ?? TimeSpan.FromSeconds(match.ElapsedGameplaySeconds).ToString(@"mm\:ss");
            if (failure == null && outcome == null) status += $"  |  {Time.timeScale:0}×";
            string pairing = exchange == null ? Pairing : "BLUE / Python     RED / Rule-Based";
            GUI.Label(rect, pairing + "  |  " + status, style);
        }

        private bool IsIslandFoodForagingPosition(Vector3 position)
        {
            var grid = foodForagingCommander.Knowledge.TeamVision?.GridManager;
            var safety = foodForagingCommander.StrategicSafety;
            if (grid == null || !safety.HasHomeAnchor) return false;
            Vector2Int cell = grid.WorldToGrid(position);
            Vector2Int home = grid.WorldToGrid(safety.HomeAnchor);
            // Reach the western meadow and hills on our side without targeting
            // the guarded central island or sending farmers to the enemy bank.
            return safety.RiskAt(cell, StrategicPathKind.Civilian) < StrategicRisk.Dangerous
                && (cell.y < GoblinIslandLayout.Height / 2)
                    == (home.y < GoblinIslandLayout.Height / 2)
                && !GoblinIslandLayout.IsIsland(cell.x, cell.y)
                && !GoblinIslandLayout.IsBridge(cell.x, cell.y)
                && Vector2.Distance(position, safety.HomeAnchor) <= IslandFoodSearchRadius;
        }

        private void UpdateFoodExploration()
        {
            ResourceStockpile stockpile = foodForagingCommander.Knowledge.Stockpile;
            bool needsFoodScout = stockpile != null
                && stockpile.GetAmount(ResourceType.Food) < FoodExplorationThreshold
                && foodForagingCommander.EconomyPolicy.FoodAssignments == 0;
            if (needsFoodScout == seekingFoodFrontier) return;
            seekingFoodFrontier = needsFoodScout;
            foodForagingCommander.ScoutingPolicy.SetFoodExplorationArea(
                needsFoodScout ? IsIslandFoodForagingPosition : null);
        }

        private void OnDestroy()
        {
            if (match != null) match.MatchEnded -= OnMatchEnded;
            if (foodForagingCommander != null)
            {
                foodForagingCommander.EconomyPolicy.SetExtendedFoodArea(null);
                foodForagingCommander.ScoutingPolicy.SetFoodExplorationArea(null);
            }
            if (observing) DebugObserverView.SetEnabled(false);
            if (Active == this) Active = null;
        }
    }
}
