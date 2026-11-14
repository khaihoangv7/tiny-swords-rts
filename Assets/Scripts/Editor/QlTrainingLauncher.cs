using System;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class QlTrainingLauncher
    {
        private const string Pending = "TinyKingdom.QlTrainingLauncher";

        static QlTrainingLauncher() => EditorApplication.update += EnterPlayMode;

        public static void Run()
        {
            const string mapOption = "-tk-ql-map=";
            string option = Array.Find(Environment.GetCommandLineArgs(),
                value => value.StartsWith(mapOption, StringComparison.Ordinal));
            string scene = option switch
            {
                null or "-tk-ql-map=2" => "LinearLane",
                "-tk-ql-map=3" => "GoblinIsland",
                _ => throw new ArgumentException("Q-learning map must be 2 or 3.")
            };
            EditorSceneManager.OpenScene($"Assets/Scenes/{scene}.unity");
            SessionState.SetBool(Pending, true);
        }

        private static void EnterPlayMode()
        {
            if (!SessionState.GetBool(Pending, false) || EditorApplication.isCompiling
                || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Pending, false);
            // Batch training keeps live, nonserialized state across frames and scene resets.
            // A domain reload would disconnect the policy mid-episode.
            EditorApplication.LockReloadAssemblies();
            EditorApplication.EnterPlaymode();
        }
    }
}
