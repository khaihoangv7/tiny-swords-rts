using System;
using System.Linq;
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
    internal sealed class RuleBasedDemo : MonoBehaviour
    {
        internal const string Label = "RULE-BASED AI VS RULE-BASED AI";
        internal const string SceneName = "LinearLane";
        internal static bool Selected { get; set; }
        internal static RuleBasedDemo Active { get; private set; }
        private MatchManager match;
        private RuleBasedCommander blue;
        private RuleBasedCommander red;
        private HarassmentRuleBasedCommander harassment;
        private GUIStyle label;
        private StrategicIntent redIntent = StrategicIntent.DevelopEconomy;
        private uint blueLaunch;
        private uint redLaunch;
        private bool observerReady;
        private readonly bool[] collected = new bool[2];
        private readonly bool[] built = new bool[2];
        private readonly bool[] recruited = new bool[2];

        internal static bool UsesResource(BattlefieldLayout layout, BattlefieldResource resource)
        {
            // Straight Lane has six extra west-side trees. Both sides use only
            // paired trees in this demo. Food and gold already have equal totals.
            if (resource.type != ResourceType.Wood) return true;
            float mirrorX = layout.blueCastle.x + layout.redCastle.x - resource.position.x;
            return layout.resources.Any(other => other.type == ResourceType.Wood
                && Mathf.Approximately(other.position.x, mirrorX)
                && Mathf.Approximately(other.position.y, resource.position.y));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSelection() => Selected = false;

        internal void Configure(MatchManager manager, RuleBasedCommander blueCommander,
            RuleBasedCommander redCommander)
        {
            if (blueCommander == null || redCommander == null
                || blueCommander.Faction != Faction.Blue || redCommander.Faction != Faction.Red
                || !blueCommander.IsConfigured || !redCommander.IsConfigured)
            {
                Debug.LogError("DEMO_RB01 invalid commander configuration.");
                enabled = false;
                return;
            }
            Active = this;
            match = manager;
            blue = blueCommander;
            red = redCommander;
            harassment = new HarassmentRuleBasedCommander(blue);
            red.ConfigureDemo(MatchDifficulty.Normal);
            blue.ConfigureDemo(MatchDifficulty.Normal, harassment);
            match.MatchEnded += OnMatchEnded;
            Debug.Log($"DEMO_RB01 start map={SceneManager.GetActiveScene().name} "
                + "Blue=Harassment Red=Balanced difficulty=Normal");
        }

        private void Update()
        {
            if (!observerReady)
            {
                observerReady = true;
                DebugObserverView.SetEnabled(true);
                FindAnyObjectByType<FogRenderer>()?.SetFogVisible(false);
                if (MatchSpeedController.Instance == null) gameObject.AddComponent<MatchSpeedController>();
            }
            if (match.State != MatchState.Playing) return;
            ObserveProgress(blue, 0);
            ObserveProgress(red, 1);
            if (redIntent != red.StrategicIntent.CurrentIntent)
            {
                redIntent = red.StrategicIntent.CurrentIntent;
                Log($"Red intent={redIntent}");
            }
            ObserveLaunch(blue, ref blueLaunch);
            ObserveLaunch(red, ref redLaunch);
        }

        private void ObserveProgress(RuleBasedCommander commander, int index)
        {
            if (!collected[index] && commander.ProgressDiagnostics.LastResourceIncomeTime >= 0f)
            {
                collected[index] = true;
                Log($"{commander.Faction} first resource income");
            }
            if (!built[index] && commander.Knowledge.OwnBuildings.Any(building =>
                    building.IsAlive && building.IsProductionReady && building.Type != Buildings.BuildingType.Castle
                    && commander.BuildingPolicy.SuccessfulCommandSerial > 0))
            {
                built[index] = true;
                Log($"{commander.Faction} first completed building");
            }
            if (!recruited[index] && commander.MilitaryPolicy.Army.Members.Count > 0)
            {
                recruited[index] = true;
                Log($"{commander.Faction} first military unit available");
            }
        }

        private void ObserveLaunch(RuleBasedCommander commander, ref uint lastLaunch)
        {
            uint launches = commander.MilitaryPolicy.OffensiveLaunchSerial;
            if (launches == lastLaunch) return;
            lastLaunch = launches;
            Log($"{commander.Faction} full army launched troops={commander.MilitaryPolicy.OffensiveParticipantCount}");
        }

        private void OnMatchEnded(MatchResult result) => Log($"result={result}");

        private void Log(string message)
            => Debug.Log($"DEMO_RB01 t={match.ElapsedGameplaySeconds:F1} {message}");

        private void OnGUI()
        {
            if (match == null) return;
            label ??= new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = true };
            float width = Mathf.Min(590f, Screen.width);
            var rect = new Rect((Screen.width - width) / 2f, 8f, width, 62f);
            GUI.Box(rect, GUIContent.none);
            string time = TimeSpan.FromSeconds(match.ElapsedGameplaySeconds).ToString(@"mm\:ss");
            GUI.Label(rect, $"BLUE  Harassment · {harassment.CurrentPhase}   |   RED  Balanced · {redIntent}\n"
                + $"{harassment.Status}   |   {time} · {Time.timeScale:0}×", label);
        }

        private void OnDestroy()
        {
            if (match != null) match.MatchEnded -= OnMatchEnded;
            if (observerReady) DebugObserverView.SetEnabled(false);
            if (Active == this) Active = null;
        }
    }
}
