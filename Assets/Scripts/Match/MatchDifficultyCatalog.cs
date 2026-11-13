using UnityEngine;

namespace TinyKingdom.Match
{
    public enum MatchDifficulty { Easy, Normal, Hard }

    [System.Serializable]
    public sealed class CommanderDifficultyProfile
    {
        [Min(0.1f)] public float thinkInterval = 0.5f;
        [Min(3)] public int targetPawns = 6;
        [Min(6)] public int targetMilitary = 8;
    }

    [CreateAssetMenu(menuName = "Tiny Kingdom/Match/Difficulty Catalog")]
    public sealed class MatchDifficultyCatalog : ScriptableObject
    {
        [SerializeField] private CommanderDifficultyProfile easy = new()
            { thinkInterval = 1.5f, targetPawns = 4, targetMilitary = 6 };
        [SerializeField] private CommanderDifficultyProfile normal = new();
        [SerializeField] private CommanderDifficultyProfile hard = new()
            { thinkInterval = 0.25f, targetPawns = 8, targetMilitary = 12 };

        private const string Preference = "TinyKingdom.MatchDifficulty";
        public static MatchDifficulty Selected
        {
            get => (MatchDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(Preference, 1), 0, 2);
            set { PlayerPrefs.SetInt(Preference, Mathf.Clamp((int)value, 0, 2)); PlayerPrefs.Save(); }
        }

        public CommanderDifficultyProfile Profile(MatchDifficulty difficulty)
            => difficulty switch { MatchDifficulty.Easy => easy, MatchDifficulty.Hard => hard, _ => normal };

        public static MatchDifficultyCatalog Load()
            => Resources.Load<MatchDifficultyCatalog>("MatchDifficulty");
    }
}
