using TinyKingdom.AI;
using TinyKingdom.Debugging;
using TinyKingdom.Match;
using TinyKingdom.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class RuleBasedDemoValidation
    {
        private const string Pending = "TinyKingdom.RuleBasedDemoValidation";

        static RuleBasedDemoValidation() => EditorApplication.update += Update;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MapSelection.unity");
            SessionState.SetInt(Pending, 1);
            EditorApplication.EnterPlaymode();
        }

        private static void Update()
        {
            int stage = SessionState.GetInt(Pending, 0);
            if (stage == 0 || !EditorApplication.isPlaying) return;
            if (stage == 1)
            {
                var menu = Object.FindAnyObjectByType<MapSelectionMenu>();
                if (menu == null || !menu.TryStartRuleBasedDemoValidation()) return;
                SessionState.SetInt(Pending, 2);
                return;
            }
            MatchManager match = MatchManager.Instance;
            if (match == null || !match.HasBothMainCastles) return;
            if (stage == 2)
            {
                if (MatchSpeedController.Instance == null) return;
                MatchSpeedController.Instance.SetSpeed(10);
                SessionState.SetInt(Pending, 3);
            }
            if (!match.IsMatchEnded && match.ElapsedGameplaySeconds < 1200) return;
            SessionState.EraseInt(Pending);
            Debug.Log($"DEMO_RB01 validation finished t={match.ElapsedGameplaySeconds:F1} result={match.Result}");
            EditorApplication.Exit(0);
        }
    }
}
