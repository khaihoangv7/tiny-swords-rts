#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TinyKingdom.AI;
using TinyKingdom.Match;
using TinyKingdom.UI;
using TinyKingdom.Units;
using TinyKingdom.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class MainMenuValidation
    {
        private const string Pending = "TinyKingdom.MainMenuValidation";
        private static readonly List<RaycastResult> Hits = new();
        private static readonly string[] Scenes = { "Map", "LinearLane", "GoblinIsland" };
        private static double nextStep;
        private static double deadline;
        private static int stage;
        private static int matchIndex;
        private static bool runtimeError;
        private static Sprite[] initialGuardFrames;
        private static AmbientCloudDrift sampledCloud;
        private static Vector3 initialCloudPosition;

        static MainMenuValidation() => EditorApplication.update += Tick;

        // Run with Unity -batchmode -force-d3d11 -executeMethod ...RunBatch (no -quit).
        public static void RunBatch()
        {
            SessionState.SetInt(Pending + ".difficulty", PlayerPrefs.GetInt("TinyKingdom.MatchDifficulty", 1));
            SessionState.SetInt(Pending + ".map", PlayerPrefs.GetInt("TinyKingdom.SelectedMap", 0));
            SessionState.SetBool(Pending + ".hadDifficulty", PlayerPrefs.HasKey("TinyKingdom.MatchDifficulty"));
            SessionState.SetBool(Pending + ".hadMap", PlayerPrefs.HasKey("TinyKingdom.SelectedMap"));
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene("Assets/Scenes/MapSelection.unity");
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (!EditorApplication.isPlaying)
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating
                    && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.EnterPlaymode();
                return;
            }
            if (deadline == 0)
            {
                deadline = EditorApplication.timeSinceStartup + 150;
                nextStep = EditorApplication.timeSinceStartup + 2;
                Application.logMessageReceived += HandleLog;
            }
            if (EditorApplication.timeSinceStartup > deadline)
            { Finish(new TimeoutException("Main menu validation timed out.")); return; }
            if (EditorApplication.timeSinceStartup < nextStep) return;
            nextStep = EditorApplication.timeSinceStartup + 1;
            try
            {
                Require(!runtimeError, "A runtime exception was logged during menu validation.");
                MapSelectionMenu menu = UnityEngine.Object.FindAnyObjectByType<MapSelectionMenu>();
                if (stage == 0)
                {
                    Require(menu != null && menu.CurrentPage == 0, "The game did not open on Home.");
                    Require(MainMenuAssetCatalog.Load().mapPreviews.All(sprite => sprite != null), "Missing map illustrations.");
                    MainMenuAssetCatalog art = MainMenuAssetCatalog.Load();
                    Require(art.titleFont != null && art.menuFont != null, "The embedded menu fonts are missing.");
                    MenuBackdropActor[] guards = UnityEngine.Object.FindObjectsByType<MenuBackdropActor>();
                    Require(guards.Length == 8 && guards.All(guard => guard.State == UnitState.Idle
                        && guard.Visual != null && guard.Visual.sprite != null), "The live backdrop guards are missing or in the wrong state.");
                    Require(UnityEngine.Object.FindAnyObjectByType<MatchManager>() == null
                        && UnityEngine.Object.FindObjectsByType<UnitController>().Length == 0,
                        "The menu backdrop accidentally started gameplay.");
                    initialGuardFrames = guards.Select(guard => guard.Visual.sprite).ToArray();
                    sampledCloud = UnityEngine.Object.FindAnyObjectByType<AmbientCloudDrift>();
                    Require(sampledCloud != null, "The live backdrop has no drifting clouds.");
                    initialCloudPosition = sampledCloud.transform.position;
                    Click(Find("Settings"));
                }
                else if (stage is >= 1 and <= 5)
                {
                    if (stage == 1)
                    {
                        Require(sampledCloud != null && (sampledCloud.transform.position - initialCloudPosition).sqrMagnitude > 0.000001f,
                            "Clouds are not moving behind the menu.");
                        MenuBackdropActor[] guards = UnityEngine.Object.FindObjectsByType<MenuBackdropActor>();
                        Require(guards.Where((guard, i) => guard.Visual.sprite != initialGuardFrames[i]).Any(),
                            "The stationary guards are not playing their Idle frames.");
                    }
                    SettingsPanelPresenter settings = UnityEngine.Object.FindAnyObjectByType<SettingsPanelPresenter>();
                    Require(settings != null && settings.IsOpen, "Settings did not open from Home.");
                    Click(Find("Tab" + (stage - 1)));
                }
                else if (stage == 6)
                {
                    UnityEngine.Object.FindAnyObjectByType<SettingsPanelPresenter>().Close();
                    Click(Find("Play Game"));
                    Require(menu.CurrentPage == 1, "Play did not open difficulty.");
                }
                else if (stage == 7)
                {
                    Click(Find("Difficulty HARD"));
                    Require(menu.SelectedDifficulty == MatchDifficulty.Hard, "Difficulty selection was ignored.");
                    Click(Find("Continue"));
                    Require(menu.CurrentPage == 2, "Continue did not open map selection.");
                    Click(Find("GOBLIN ISLAND"));
                    Require(menu.SelectedScene == "GoblinIsland" && SceneManager.GetActiveScene().name == "MapSelection",
                        "Choosing a card started a match before Start.");
                    Click(Find("Back"));
                    Require(menu.CurrentPage == 1 && menu.SelectedDifficulty == MatchDifficulty.Hard,
                        "Back lost setup state.");
                    Click(Find("Close"));
                    Require(menu.CurrentPage == 0 && menu.SelectedScene == "GoblinIsland", "Close lost the map choice.");
                }
                else if (stage == 8)
                {
                    Click(Find("Play Game"));
                    Click(Find("Difficulty " + ((MatchDifficulty)matchIndex).ToString().ToUpperInvariant()));
                    Click(Find("Continue"));
                    string[] names = { "TWO KINGDOMS", "STRAIGHT LANE", "GOBLIN ISLAND" };
                    Click(Find(names[matchIndex]));
                    Click(Find("Start Game"));
                    nextStep += 3;
                }
                else if (stage == 9)
                {
                    Require(SceneManager.GetActiveScene().name == Scenes[matchIndex], "The wrong battlefield loaded.");
                    RuleBasedCommander commander = UnityEngine.Object.FindObjectsByType<RuleBasedCommander>()
                        .FirstOrDefault(item => item.Faction == Faction.Red);
                    Require(commander != null, "The selected battlefield has no Red Commander.");
                    CommanderDifficultyProfile profile = MatchDifficultyCatalog.Load().Profile((MatchDifficulty)matchIndex);
                    Require(Mathf.Approximately(commander.ThinkInterval, profile.thinkInterval)
                        && commander.ProductionPolicy.TargetPawnCount == profile.targetPawns
                        && commander.ProductionPolicy.TargetMilitaryPopulation >= profile.targetMilitary,
                        "Difficulty was not applied to the real Commander.");
                    Require(MatchManager.Instance != null && MatchManager.Instance.HasBothMainCastles,
                        "The selected map did not create a playable match.");
                    matchIndex++;
                    if (matchIndex == 3) { Finish(null); return; }
                    SceneManager.LoadScene("MapSelection");
                    stage = 7;
                    nextStep += 2;
                }
                stage++;
            }
            catch (Exception error) { Finish(error); }
        }

        private static Button Find(string name)
            => UnityEngine.Object.FindObjectsByType<Button>().FirstOrDefault(button => button.name == name)
                ?? throw new InvalidOperationException("Visible menu button missing: " + name);

        private static void Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
            };
            Hits.Clear();
            EventSystem.current.RaycastAll(pointer, Hits);
            Require(Hits.Count > 0 && Hits[0].gameObject.GetComponentInParent<Button>() == button,
                "A visible menu button is blocked by another graphic: " + button.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void HandleLog(string text, string stack, LogType type)
        { if (type is LogType.Error or LogType.Exception or LogType.Assert) runtimeError = true; }

        private static void Require(bool valid, string message)
        { if (!valid) throw new InvalidOperationException(message); }

        private static void Finish(Exception error)
        {
            SessionState.SetBool(Pending, false);
            Application.logMessageReceived -= HandleLog;
            RestorePreference("TinyKingdom.MatchDifficulty", ".difficulty", ".hadDifficulty");
            RestorePreference("TinyKingdom.SelectedMap", ".map", ".hadMap");
            PlayerPrefs.Save();
            if (error == null) Debug.Log("MAIN_MENU_PLAY_VALIDATION_PASSED: Home, Settings tabs, raycastable buttons, Back/Close, all maps and all difficulty profiles.");
            else Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }

        private static void RestorePreference(string key, string value, string existed)
        {
            if (SessionState.GetBool(Pending + existed, false))
                PlayerPrefs.SetInt(key, SessionState.GetInt(Pending + value, 0));
            else PlayerPrefs.DeleteKey(key);
        }
    }
}
#endif
