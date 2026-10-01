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
        private sealed class SourceSlot
        {
            public AudioSource Source;
            public GameplayAudioEvent EventId;
        }

        private static readonly ProfilerMarker PlayMarker =
            new("TinyKingdom.Audio.PlaySfx");
        private readonly Dictionary<ulong, float> duplicateCooldowns = new();
        private SourceSlot[] sources = Array.Empty<SourceSlot>();
        private GameplayAudioCatalog catalog;
        private MatchManager subscribedMatch;
        private Camera listenerCamera;
        private uint playSerial;
        private int droppedByConcurrency;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

        public static GameplayAudioService Instance { get; private set; }
        public static bool RuntimeBootstrapEnabled => true;
        public int PoolSize => sources.Length;
        public int ActiveSources
        {
            get
            {
                int active = 0;
                for (int i = 0; i < sources.Length; i++)
                    if (sources[i].Source != null && sources[i].Source.isPlaying) active++;
                return active;
            }
        }
        public int DroppedByConcurrency => droppedByConcurrency;
        public bool IsPoolConfigured
        {
            get
            {
                if (catalog == null || sources.Length != catalog.PoolSize) return false;
                for (int i = 0; i < sources.Length; i++)
                {
                    AudioSource source = sources[i].Source;
                    if (source == null || source.playOnAwake || source.loop
                        || !Mathf.Approximately(source.spatialBlend, catalog.SpatialBlend)
                        || source.rolloffMode != AudioRolloffMode.Linear
                        || !Mathf.Approximately(source.minDistance, catalog.MinDistance)
                        || !Mathf.Approximately(source.maxDistance, catalog.MaxDistance))
                        return false;
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
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            listenerCamera ??= Camera.main;
            if (listenerCamera == null) return;
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i].Source;
                if (source == null || !source.isPlaying
                    || IsInsideCameraViewport(listenerCamera, source.transform.position))
                    continue;
                source.Stop();
                source.clip = null;
            }
        }

        public void Configure(GameplayAudioCatalog value)
        {
            catalog = value;
            EnsurePool();
        }

        public static bool Request(GameplayAudioEvent eventId, Vector3 worldPosition,
            Faction? sourceFaction = null, int sourceKey = 0)
            => Instance != null
                && Instance.TryPlay(eventId, worldPosition, sourceFaction, sourceKey);

        public static void StopAllActive()
        {
            if (Instance != null) Instance.StopAll();
        }

        private bool TryPlay(GameplayAudioEvent eventId, Vector3 worldPosition,
            Faction? sourceFaction, int sourceKey)
        {
            using (PlayMarker.Auto())
            {
                if (catalog == null || !catalog.TryGetCue(eventId, out GameplayAudioCue cue)
                    || cue.Clip == null || !CanPlayerHear(worldPosition, sourceFaction))
                    return false;

                if (sourceKey != 0 && cue.DuplicateCooldown > 0f)
                {
                    ulong cooldownKey = ((ulong)(uint)eventId << 32) | (uint)sourceKey;
                    float now = Time.unscaledTime;
                    if (duplicateCooldowns.TryGetValue(cooldownKey, out float nextAllowed)
                        && now < nextAllowed)
                        return false;
                    duplicateCooldowns[cooldownKey] = now + cue.DuplicateCooldown;
                }

                int concurrent = 0;
                SourceSlot available = null;
                for (int i = 0; i < sources.Length; i++)
                {
                    SourceSlot slot = sources[i];
                    if (slot.Source.isPlaying)
                    {
                        if (slot.EventId == eventId) concurrent++;
                    }
                    else if (available == null) available = slot;
                }
                if (concurrent >= cue.MaxConcurrent || available == null)
                {
                    droppedByConcurrency++;
                    return false;
                }

                AudioSource source = available.Source;
                source.transform.position = worldPosition;
                source.clip = cue.Clip;
                source.volume = sfxVolume * cue.Volume;
                source.pitch = NextPitch();
                source.time = Mathf.Min(cue.StartTimeSeconds,
                    Mathf.Max(0f, cue.Clip.length - 0.01f));
                available.EventId = eventId;
                source.Play();
                if (cue.MaxPlaybackSeconds > 0f
                    && cue.MaxPlaybackSeconds < cue.Clip.length)
                    source.SetScheduledEndTime(AudioSettings.dspTime + cue.MaxPlaybackSeconds);
                return true;
            }
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

        private float NextPitch()
        {
            if (catalog == null || !catalog.PitchVariation) return 1f;
            playSerial++;
            float step = ((playSerial * 37u) % 101u) / 100f;
            return Mathf.Lerp(catalog.MinimumPitch, catalog.MaximumPitch, step);
        }

        private void EnsurePool()
        {
            if (catalog == null || sources.Length > 0) return;
            sources = new SourceSlot[catalog.PoolSize];
            for (int i = 0; i < sources.Length; i++)
            {
                var child = new GameObject($"WorldSfx_{i:00}");
                child.transform.SetParent(transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = catalog.SpatialBlend;
                // The listener lives on the orthographic Main Camera at roughly
                // z = -10. MinDistance therefore gives full volume directly below
                // the camera centre; linear rolloff makes screen-distance fading
                // predictable as the camera pans away from the world action.
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = catalog.MinDistance;
                source.maxDistance = catalog.MaxDistance;
                source.dopplerLevel = 0f;
                sources[i] = new SourceSlot { Source = source };
            }
        }

        private void HandleSceneLoaded(Scene _, LoadSceneMode __)
        {
            StopAll();
            listenerCamera = Camera.main;
            UnsubscribeMatch();
            subscribedMatch = MatchManager.Instance;
            if (subscribedMatch != null) subscribedMatch.MatchEnded += HandleMatchEnded;
        }

        private void HandleMatchEnded(MatchResult _) => StopAll();

        private void UnsubscribeMatch()
        {
            if (subscribedMatch != null) subscribedMatch.MatchEnded -= HandleMatchEnded;
            subscribedMatch = null;
        }

        private void StopAll()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i].Source == null) continue;
                sources[i].Source.Stop();
                sources[i].Source.clip = null;
            }
            duplicateCooldowns.Clear();
            droppedByConcurrency = 0;
        }
    }
}
