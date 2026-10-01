using System;
using UnityEngine;

namespace TinyKingdom.Audio
{
    [Serializable]
    public sealed class GameplayAudioCue
    {
        [SerializeField] private GameplayAudioEvent eventId;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Min(1)] private int maxConcurrent = 2;
        [SerializeField, Min(0f)] private float duplicateCooldown;
        [SerializeField, Min(0f)] private float startTimeSeconds;
        [SerializeField, Min(0f)] private float maxPlaybackSeconds;

        public GameplayAudioEvent EventId => eventId;
        public AudioClip Clip => clip;
        public float Volume => volume;
        public int MaxConcurrent => Mathf.Max(1, maxConcurrent);
        public float DuplicateCooldown => Mathf.Max(0f, duplicateCooldown);
        public float StartTimeSeconds => Mathf.Max(0f, startTimeSeconds);
        public float MaxPlaybackSeconds => Mathf.Max(0f, maxPlaybackSeconds);

#if UNITY_EDITOR
        public void AssignClip(AudioClip value) => clip = value;
        public void Configure(GameplayAudioEvent id, float volumeMultiplier,
            int concurrency, float cooldown, float playbackStart, float playbackLimit)
        {
            eventId = id;
            volume = Mathf.Clamp01(volumeMultiplier);
            maxConcurrent = Mathf.Max(1, concurrency);
            duplicateCooldown = Mathf.Max(0f, cooldown);
            startTimeSeconds = Mathf.Max(0f, playbackStart);
            maxPlaybackSeconds = Mathf.Max(0f, playbackLimit);
        }
#endif
    }

    [CreateAssetMenu(menuName = "Tiny Kingdom/Audio/Gameplay Audio Catalog")]
    public sealed class GameplayAudioCatalog : ScriptableObject
    {
        public const string ResourcePath = "Audio/GameplayAudioCatalog";

        [SerializeField] private GameplayAudioCue[] cues = Array.Empty<GameplayAudioCue>();
        [SerializeField, Min(1)] private int poolSize = 12;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.01f)] private float minDistance = 10f;
        [SerializeField, Min(0.01f)] private float maxDistance = 32f;
        [SerializeField] private bool pitchVariation = true;
        [SerializeField, Range(0.5f, 1.5f)] private float minimumPitch = 0.96f;
        [SerializeField, Range(0.5f, 1.5f)] private float maximumPitch = 1.04f;

        public GameplayAudioCue[] Cues => cues;
        public int PoolSize => Mathf.Max(1, poolSize);
        public float SpatialBlend => Mathf.Clamp01(spatialBlend);
        public float MinDistance => Mathf.Max(0.01f, minDistance);
        public float MaxDistance => Mathf.Max(MinDistance, maxDistance);
        public bool PitchVariation => pitchVariation;
        public float MinimumPitch => Mathf.Min(minimumPitch, maximumPitch);
        public float MaximumPitch => Mathf.Max(minimumPitch, maximumPitch);

        public static GameplayAudioCatalog Load()
            => Resources.Load<GameplayAudioCatalog>(ResourcePath);

        public bool TryGetCue(GameplayAudioEvent eventId, out GameplayAudioCue cue)
        {
            if (cues != null)
            {
                for (int i = 0; i < cues.Length; i++)
                {
                    if (cues[i] != null && cues[i].EventId == eventId)
                    {
                        cue = cues[i];
                        return true;
                    }
                }
            }
            cue = null;
            return false;
        }

#if UNITY_EDITOR
        public void EnsureDefaultCues()
        {
            if (cues != null && cues.Length == 8) return;
            cues = new GameplayAudioCue[8];
            for (int i = 0; i < cues.Length; i++) cues[i] = new GameplayAudioCue();
            cues[0].Configure(GameplayAudioEvent.WarriorAttack, 0.8f, 3, 0f, 0f, 0.8f);
            cues[1].Configure(GameplayAudioEvent.LancerAttack, 0.8f, 3, 0f, 0f, 1.25f);
            cues[2].Configure(GameplayAudioEvent.ArcherAttack, 0.75f, 3, 0f, 0f, 1.25f);
            cues[3].Configure(GameplayAudioEvent.MonkHeal, 0.75f, 2, 0f, 0f, 1.5f);
            cues[4].Configure(GameplayAudioEvent.PawnMining, 0.20f, 2, 0f, 0f, 1.25f);
            cues[5].Configure(GameplayAudioEvent.PawnChopping, 0.65f, 2, 0f, 3f, 1.25f);
            cues[6].Configure(GameplayAudioEvent.PawnFoodHarvest, 0.65f, 2, 0f, 0f, 0f);
            cues[7].Configure(GameplayAudioEvent.SheepHurt, 0.45f, 2, 0.15f, 0f, 0.8f);
        }

        public bool AssignClip(GameplayAudioEvent eventId, AudioClip clip)
        {
            if (!TryGetCue(eventId, out GameplayAudioCue cue)) return false;
            cue.AssignClip(clip);
            return true;
        }
#endif
    }
}
