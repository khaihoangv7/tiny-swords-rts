using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Audio
{
    [Serializable]
    public sealed class GameplayAudioCue
    {
        [SerializeField] private GameplayAudioEvent eventId;
        [SerializeField] private AudioClip clip;
        [SerializeField] private AudioClip[] variations = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Min(0f)] private float startTimeSeconds;
        [SerializeField, Min(0f)] private float maxPlaybackSeconds;

        public GameplayAudioEvent EventId => eventId;
        public AudioClip Clip => clip;
        public AudioClip[] Variations => variations ?? Array.Empty<AudioClip>();
        public float Volume => volume;
        public float StartTimeSeconds => Mathf.Max(0f, startTimeSeconds);
        public float MaxPlaybackSeconds => Mathf.Max(0f, maxPlaybackSeconds);

        public int ClipCount
        {
            get
            {
                int count = clip != null ? 1 : 0;
                if (variations == null) return count;
                for (int i = 0; i < variations.Length; i++)
                    if (variations[i] != null) count++;
                return count;
            }
        }

        public AudioClip GetClip(int variationIndex)
        {
            int count = ClipCount;
            if (count == 0) return null;
            int target = (variationIndex & int.MaxValue) % count;
            if (clip != null)
            {
                if (target == 0) return clip;
                target--;
            }
            for (int i = 0; i < Variations.Length; i++)
            {
                if (variations[i] == null) continue;
                if (target-- == 0) return variations[i];
            }
            return clip;
        }

#if UNITY_EDITOR
        public void AssignClip(AudioClip value) => clip = value;
        public void AssignVariations(AudioClip[] values)
            => variations = values ?? Array.Empty<AudioClip>();
        public void Configure(GameplayAudioEvent id, float volumeMultiplier,
            float playbackStart, float playbackLimit)
        {
            eventId = id;
            volume = Mathf.Clamp01(volumeMultiplier);
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
        [SerializeField, Min(1)] private int poolGrowthSize = 4;
        [SerializeField, Min(32)] private int catastrophicSourceLimit = 256;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField, Min(0.01f)] private float minDistance = 10f;
        [SerializeField, Min(0.01f)] private float maxDistance = 32f;
        [SerializeField] private bool pitchVariation = true;
        [SerializeField, Range(0.5f, 1.5f)] private float minimumPitch = 0.96f;
        [SerializeField, Range(0.5f, 1.5f)] private float maximumPitch = 1.04f;

        public GameplayAudioCue[] Cues => cues;
        public int PoolSize => Mathf.Max(1, poolSize);
        public int PoolGrowthSize => Mathf.Max(1, poolGrowthSize);
        public int CatastrophicSourceLimit => Mathf.Max(32, catastrophicSourceLimit);
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
            var existing = new Dictionary<GameplayAudioEvent, GameplayAudioCue>();
            if (cues != null)
                for (int i = 0; i < cues.Length; i++)
                    if (cues[i] != null) existing[cues[i].EventId] = cues[i];

            Array values = Enum.GetValues(typeof(GameplayAudioEvent));
            cues = new GameplayAudioCue[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                GameplayAudioEvent id = (GameplayAudioEvent)values.GetValue(i);
                if (!existing.TryGetValue(id, out GameplayAudioCue cue))
                    cue = new GameplayAudioCue();
                ConfigureDefault(cue, id);
                cues[i] = cue;
            }
        }

        private static void ConfigureDefault(GameplayAudioCue cue, GameplayAudioEvent id)
        {
            (float volume, float start, float limit) = id switch
            {
                GameplayAudioEvent.WarriorAttack => (0.8f, 0f, 0.8f),
                GameplayAudioEvent.LancerAttack => (0.8f, 0f, 1.25f),
                GameplayAudioEvent.ArcherAttack => (0.75f, 0f, 1.25f),
                GameplayAudioEvent.MonkHeal => (0.75f, 0f, 1.5f),
                GameplayAudioEvent.PawnMining => (0.20f, 0f, 1.25f),
                GameplayAudioEvent.PawnChopping => (0.65f, 3f, 1.25f),
                GameplayAudioEvent.PawnFoodHarvest => (0.65f, 0f, 0f),
                GameplayAudioEvent.SheepHurt => (0.45f, 0f, 0.8f),
                GameplayAudioEvent.BuildingFire => (0.35f, 0f, 0f),
                GameplayAudioEvent.BuildingExplosion => (0.75f, 0f, 0f),
                GameplayAudioEvent.PawnHammer => (0.55f, 0f, 0.7f),
                GameplayAudioEvent.CommandClick => (0.70f, 0f, 0f),
                GameplayAudioEvent.CommandRejected => (0.75f, 0f, 0f),
                GameplayAudioEvent.UnitProductionFinished => (0.80f, 0f, 0f),
                GameplayAudioEvent.ScoutPromotion => (0.75f, 0f, 0f),
                GameplayAudioEvent.Victory or GameplayAudioEvent.Defeat => (0.85f, 0f, 0f),
                _ => (0.75f, 0f, 0f)
            };
            cue.Configure(id, volume, start, limit);
        }

        public bool AssignClip(GameplayAudioEvent eventId, AudioClip clip)
        {
            if (!TryGetCue(eventId, out GameplayAudioCue cue)) return false;
            cue.AssignClip(clip);
            return true;
        }

        public bool AssignVariations(GameplayAudioEvent eventId, AudioClip[] clips)
        {
            if (!TryGetCue(eventId, out GameplayAudioCue cue)) return false;
            cue.AssignVariations(clips);
            return true;
        }
#endif
    }
}
