using System;
using System.Collections.Generic;
using TinyKingdom.Match;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyKingdom.Audio
{
    [DefaultExecutionOrder(-115)]
    public sealed class GameplayAudioService : MonoBehaviour
    {
        private readonly struct AudioLoopKey : IEquatable<AudioLoopKey>
        {
            public AudioLoopKey(EntityId ownerId, GameplayAudioEvent eventId)
            {
                OwnerId = ownerId;
                EventId = eventId;
            }

            private EntityId OwnerId { get; }
            private GameplayAudioEvent EventId { get; }
            public bool Equals(AudioLoopKey other)
                => OwnerId.Equals(other.OwnerId) && EventId == other.EventId;
            public override bool Equals(object obj)
                => obj is AudioLoopKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { return (OwnerId.GetHashCode() * 397) ^ (int)EventId; }
            }
        }

        private sealed class SourceSlot
        {
            public AudioSource Source;
            public GameplayAudioEvent EventId;
            public Faction? SourceFaction;
            public bool Spatial;
        }

        private static readonly ProfilerMarker PlayMarker =
            new("TinyKingdom.Audio.PlaySfx");
        private readonly List<SourceSlot> oneShots = new();
        private readonly List<SourceSlot> loopSources = new();
        private readonly Dictionary<AudioLoopKey, SourceSlot> activeLoops = new();
        private readonly System.Random presentationRandom = new();
        private GameplayAudioCatalog catalog;
        private MatchManager subscribedMatch;
        private Camera listenerCamera;
        private int poolGrowthCount;
        private int droppedBySafetyLimit;
        private bool resultAudioPlayed;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        public static GameplayAudioService Instance { get; private set; }
        public static bool RuntimeBootstrapEnabled => true;
        public int PoolSize => oneShots.Count;
        public int ActiveOneShots => CountActive(oneShots);
        public int ActiveLoops => activeLoops.Count;
        public int PoolGrowthCount => poolGrowthCount;
        public int DroppedBySafetyLimit => droppedBySafetyLimit;
        public bool HasPerEventConcurrencyLimit => false;
        public bool HasSameClipCooldown => false;
        public bool IsPoolConfigured
        {
            get
            {
                if (catalog == null || oneShots.Count < catalog.PoolSize) return false;
                for (int i = 0; i < oneShots.Count; i++)
                {
                    AudioSource source = oneShots[i].Source;
                    if (source == null || source.playOnAwake || source.loop) return false;
                }
                for (int i = 0; i < loopSources.Count; i++)
                {
                    AudioSource source = loopSources[i].Source;
                    if (source == null || source.playOnAwake || !source.loop) return false;
                }
                return true;
            }
        }
        public float SfxVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }
        public GameplayAudioCatalog Catalog => catalog;

        public static bool IsInsideCameraViewport(Camera camera, Vector3 worldPosition)
        {
            if (camera == null || !camera.isActiveAndEnabled) return false;
            Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var root = new GameObject("GameplayAudioService");
            DontDestroyOnLoad(root);
            GameplayAudioService service = root.AddComponent<GameplayAudioService>();
            service.Configure(GameplayAudioCatalog.Load());
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeMatch();
            StopAllSources();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            listenerCamera ??= Camera.main;
            if (subscribedMatch == null && MatchManager.Instance != null)
            {
                subscribedMatch = MatchManager.Instance;
                subscribedMatch.MatchEnded += HandleMatchEnded;
            }
            StopWorldSourcesThatBecameInaudible(oneShots);
            StopWorldSourcesThatBecameInaudible(loopSources);
        }

        public void Configure(GameplayAudioCatalog value)
        {
            catalog = value;
            EnsureInitialPool();
        }

        public static bool Request(GameplayAudioEvent eventId, Vector3 worldPosition,
            Faction? sourceFaction = null)
            => Instance != null && Instance.TryPlay(eventId, worldPosition, sourceFaction, true);

        public static bool RequestUi(GameplayAudioEvent eventId)
            => Instance != null && Instance.TryPlay(eventId, Vector3.zero, null, false);

        public static bool CanHearWorld(Vector3 worldPosition, Faction? sourceFaction = null)
            => Instance != null && Instance.CanPlayerHear(worldPosition, sourceFaction);

        public static bool StartLoop(UnityEngine.Object owner, GameplayAudioEvent eventId,
            Vector3 worldPosition, Faction? sourceFaction = null)
            => Instance != null && owner != null
                && Instance.TryStartLoop(owner, eventId, worldPosition, sourceFaction);

        public static bool IsLoopActive(UnityEngine.Object owner, GameplayAudioEvent eventId)
            => Instance != null && owner != null
                && Instance.activeLoops.ContainsKey(LoopKey(owner, eventId));

        public static void StopLoop(UnityEngine.Object owner, GameplayAudioEvent eventId)
        {
            if (Instance != null && owner != null)
                Instance.StopLoopInternal(owner, eventId);
        }

        public static void StopAllActive()
        {
            if (Instance != null) Instance.StopAllSources();
        }

        private bool TryPlay(GameplayAudioEvent eventId, Vector3 worldPosition,
            Faction? sourceFaction, bool spatial)
        {
            using (PlayMarker.Auto())
            {
                if (catalog == null || !catalog.TryGetCue(eventId, out GameplayAudioCue cue)
                    || cue.ClipCount == 0 || (spatial && !CanPlayerHear(worldPosition, sourceFaction)))
                    return false;

                SourceSlot slot = AcquireOneShot();
                if (slot == null) return false;
                AudioClip clip = cue.GetClip(presentationRandom.Next());
                if (clip == null) return false;
                ConfigureSource(slot.Source, spatial, false);
                slot.EventId = eventId;
                slot.SourceFaction = sourceFaction;
                slot.Spatial = spatial;
                AudioSource source = slot.Source;
                source.transform.position = spatial ? worldPosition : transform.position;
                source.clip = clip;
                source.volume = sfxVolume * cue.Volume;
                source.pitch = NextPitch();
                source.time = Mathf.Min(cue.StartTimeSeconds,
                    Mathf.Max(0f, clip.length - 0.01f));
                source.Play();
                if (cue.MaxPlaybackSeconds > 0f && cue.MaxPlaybackSeconds < clip.length)
                    source.SetScheduledEndTime(AudioSettings.dspTime + cue.MaxPlaybackSeconds);
                return true;
            }
        }

        private bool TryStartLoop(UnityEngine.Object owner, GameplayAudioEvent eventId,
            Vector3 worldPosition, Faction? sourceFaction)
        {
            AudioLoopKey key = LoopKey(owner, eventId);
            if (activeLoops.TryGetValue(key, out SourceSlot existing))
            {
                existing.Source.transform.position = worldPosition;
                if (existing.Source.isPlaying) return true;
                activeLoops.Remove(key);
                StopSlot(existing);
            }
            if (catalog == null || !catalog.TryGetCue(eventId, out GameplayAudioCue cue)
                || cue.ClipCount == 0 || !CanPlayerHear(worldPosition, sourceFaction)) return false;
            SourceSlot slot = AcquireLoop();
            if (slot == null) return false;
            AudioClip clip = cue.GetClip(presentationRandom.Next());
            if (clip == null) return false;
            ConfigureSource(slot.Source, true, true);
            slot.EventId = eventId;
            slot.SourceFaction = sourceFaction;
            slot.Spatial = true;
            slot.Source.transform.position = worldPosition;
            slot.Source.clip = clip;
            slot.Source.volume = sfxVolume * cue.Volume;
            slot.Source.pitch = 1f;
            slot.Source.Play();
            activeLoops.Add(key, slot);
            return true;
        }

        private void StopLoopInternal(UnityEngine.Object owner, GameplayAudioEvent eventId)
        {
            AudioLoopKey key = LoopKey(owner, eventId);
            if (!activeLoops.Remove(key, out SourceSlot slot)) return;
            StopSlot(slot);
        }

        private SourceSlot AcquireOneShot()
        {
            for (int i = 0; i < oneShots.Count; i++)
                if (!oneShots[i].Source.isPlaying) return oneShots[i];
            int before = oneShots.Count;
            if (!Grow(oneShots, false, catalog.PoolGrowthSize)) return null;
            return oneShots[before];
        }

        private SourceSlot AcquireLoop()
        {
            for (int i = 0; i < loopSources.Count; i++)
                if (!loopSources[i].Source.isPlaying) return loopSources[i];
            int growth = Mathf.Min(2, catalog.PoolGrowthSize);
            int before = loopSources.Count;
            if (!Grow(loopSources, true, growth)) return null;
            return loopSources[before];
        }

        private bool Grow(List<SourceSlot> target, bool loop, int requested)
        {
            int total = oneShots.Count + loopSources.Count;
            int count = Mathf.Min(requested, catalog.CatastrophicSourceLimit - total);
            if (count <= 0)
            {
                droppedBySafetyLimit++;
                if (droppedBySafetyLimit == 1)
                    Debug.LogError("Gameplay audio reached its catastrophic source safety limit. "
                        + "Inspect event hooks for frame spam.", this);
                return false;
            }
            for (int i = 0; i < count; i++) target.Add(CreateSlot(loop, target.Count));
            poolGrowthCount++;
            return true;
        }

        private SourceSlot CreateSlot(bool loop, int index)
        {
            var child = new GameObject(loop ? $"WorldLoop_{index:00}" : $"OneShot_{index:00}");
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            ConfigureSource(source, true, loop);
            return new SourceSlot { Source = source, Spatial = true };
        }

        private void ConfigureSource(AudioSource source, bool spatial, bool loop)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatial ? catalog.SpatialBlend : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = catalog.MinDistance;
            source.maxDistance = catalog.MaxDistance;
            source.dopplerLevel = 0f;
        }

        private bool CanPlayerHear(Vector3 position, Faction? sourceFaction)
        {
            listenerCamera ??= Camera.main;
            if (!IsInsideCameraViewport(listenerCamera, position)) return false;
            Faction playerFaction = PlayerViewRelation.PlayerViewFaction;
            if (sourceFaction.HasValue && sourceFaction.Value == playerFaction) return true;
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            return vision == null || !vision.IsReady || vision.IsVisible(playerFaction, position);
        }

        private void StopWorldSourcesThatBecameInaudible(List<SourceSlot> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                SourceSlot slot = slots[i];
                if (!slot.Spatial || slot.Source == null || !slot.Source.isPlaying
                    || CanPlayerHear(slot.Source.transform.position, slot.SourceFaction)) continue;
                if (slot.Source.loop) RemoveLoopOwnership(slot);
                StopSlot(slot);
            }
        }

        private void RemoveLoopOwnership(SourceSlot slot)
        {
            AudioLoopKey found = default;
            bool hasKey = false;
            foreach (KeyValuePair<AudioLoopKey, SourceSlot> pair in activeLoops)
            {
                if (!ReferenceEquals(pair.Value, slot)) continue;
                found = pair.Key;
                hasKey = true;
                break;
            }
            if (hasKey) activeLoops.Remove(found);
        }

        private float NextPitch()
        {
            if (catalog == null || !catalog.PitchVariation) return 1f;
            return Mathf.Lerp(catalog.MinimumPitch, catalog.MaximumPitch,
                (float)presentationRandom.NextDouble());
        }

        private void EnsureInitialPool()
        {
            if (catalog == null || oneShots.Count >= catalog.PoolSize) return;
            int count = catalog.PoolSize - oneShots.Count;
            for (int i = 0; i < count; i++) oneShots.Add(CreateSlot(false, oneShots.Count));
        }

        private void HandleSceneLoaded(Scene _, LoadSceneMode __)
        {
            StopAllSources();
            listenerCamera = Camera.main;
            resultAudioPlayed = false;
            UnsubscribeMatch();
            subscribedMatch = MatchManager.Instance;
            if (subscribedMatch != null) subscribedMatch.MatchEnded += HandleMatchEnded;
        }

        private void HandleMatchEnded(MatchResult result)
        {
            StopAllLoops();
            if (resultAudioPlayed) return;
            resultAudioPlayed = true;
            RequestUi(result == MatchResult.BlueVictory
                ? GameplayAudioEvent.Victory : GameplayAudioEvent.Defeat);
        }

        private void UnsubscribeMatch()
        {
            if (subscribedMatch != null) subscribedMatch.MatchEnded -= HandleMatchEnded;
            subscribedMatch = null;
        }

        private void StopAllSources()
        {
            for (int i = 0; i < oneShots.Count; i++) StopSlot(oneShots[i]);
            StopAllLoops();
            droppedBySafetyLimit = 0;
        }

        private void StopAllLoops()
        {
            for (int i = 0; i < loopSources.Count; i++) StopSlot(loopSources[i]);
            activeLoops.Clear();
        }

        private static void StopSlot(SourceSlot slot)
        {
            if (slot?.Source == null) return;
            slot.Source.Stop();
            slot.Source.clip = null;
            slot.SourceFaction = null;
        }

        private static int CountActive(List<SourceSlot> slots)
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Source != null && slots[i].Source.isPlaying) count++;
            return count;
        }

        private static AudioLoopKey LoopKey(UnityEngine.Object owner, GameplayAudioEvent eventId)
            => new(owner.GetEntityId(), eventId);
    }
}
