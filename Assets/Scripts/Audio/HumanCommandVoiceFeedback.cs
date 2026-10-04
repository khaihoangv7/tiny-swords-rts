using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Audio
{
    /// <summary>
    /// Presentation-only acknowledgement policy for accepted human commands.
    /// Gameplay command authority stays in PlayerCommandController.
    /// </summary>
    public sealed class HumanCommandVoiceFeedback
    {
        public const float GenericProbability = 0.30f;
        public const float ArmyProbability = 0.40f;
        public const float GenericCooldownSeconds = 30f;
        public const float ArmyCooldownSeconds = 10f;
        public const int ArmyCombatThreshold = 3;

        private readonly System.Random random = new();
        private float nextGenericTime;
        private float nextArmyTime;

        public float NextGenericTime => nextGenericTime;
        public float NextArmyTime => nextArmyTime;

        public static bool UsesArmyVoice(int armyCombatCount)
            => armyCombatCount > ArmyCombatThreshold;

        public static bool IsArmyCombatUnit(UnitType type)
            => type is UnitType.Warrior or UnitType.Lancer or UnitType.Archer;

        public bool TryAcknowledge(int acceptedCount, int armyCombatCount, float gameTime)
            => TryAcknowledgeCore(acceptedCount, armyCombatCount, gameTime,
                (float)random.NextDouble(), null);

#if UNITY_EDITOR
        public bool TryAcknowledgeForValidation(int acceptedCount, int armyCombatCount,
            float gameTime, float roll, bool playbackSucceeded)
            => TryAcknowledgeCore(acceptedCount, armyCombatCount, gameTime, roll,
                playbackSucceeded);
#endif

        private bool TryAcknowledgeCore(int acceptedCount, int armyCombatCount,
            float gameTime, float roll, bool? forcedPlaybackResult)
        {
            if (acceptedCount <= 0) return false;

            bool army = UsesArmyVoice(armyCombatCount);
            float nextAllowed = army ? nextArmyTime : nextGenericTime;
            float probability = army ? ArmyProbability : GenericProbability;
            if (gameTime < nextAllowed || roll >= probability) return false;

            GameplayAudioEvent eventId = army
                ? GameplayAudioEvent.ArmyCommandVoice
                : GameplayAudioEvent.GenericCommandVoice;
            bool played = forcedPlaybackResult
                ?? GameplayAudioService.RequestUi(eventId);
            if (!played) return false;

            if (army) nextArmyTime = gameTime + ArmyCooldownSeconds;
            else nextGenericTime = gameTime + GenericCooldownSeconds;
            return true;
        }
    }
}
