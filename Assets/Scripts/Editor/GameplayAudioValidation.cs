using System.IO;
using TinyKingdom.Audio;
using TinyKingdom.Buildings;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class GameplayAudioValidation
    {
        private readonly struct ExpectedCue
        {
            public ExpectedCue(GameplayAudioEvent id, string path,
                AudioClipLoadType loadType = AudioClipLoadType.DecompressOnLoad)
            {
                Id = id;
                Path = path;
                LoadType = loadType;
            }
            public GameplayAudioEvent Id { get; }
            public string Path { get; }
            public AudioClipLoadType LoadType { get; }
        }

        private static readonly ExpectedCue[] Required =
        {
            new(GameplayAudioEvent.WarriorAttack,
                "Assets/Audio/SFX/Units/Warrior/sfx_warrior_attack_01.mp3"),
            new(GameplayAudioEvent.LancerAttack,
                "Assets/Audio/SFX/Units/Lancer/sfx_lancer_attack_01.mp3"),
            new(GameplayAudioEvent.ArcherAttack,
                "Assets/Audio/SFX/Units/Archer/sfx_archer_attack_01.mp3"),
            new(GameplayAudioEvent.MonkHeal,
                "Assets/Audio/SFX/Units/Monk/sfx_monk_heal_01.mp3"),
            new(GameplayAudioEvent.PawnMining,
                "Assets/Audio/SFX/Units/Pawn/sfx_pawn_mining_01.mp3"),
            new(GameplayAudioEvent.PawnChopping,
                "Assets/Audio/SFX/Units/Pawn/sfx_pawn_chopping_01.mp3"),
            new(GameplayAudioEvent.CastleSelect,
                "Assets/Audio/SFX/Buildings/Sound Effect/castle_sound.mp3"),
            new(GameplayAudioEvent.HouseSelect,
                "Assets/Audio/SFX/Buildings/Sound Effect/house_sound.mp3"),
            new(GameplayAudioEvent.BarracksSelect,
                "Assets/Audio/SFX/Buildings/Sound Effect/barracks_sound.mp3"),
            new(GameplayAudioEvent.ArcherySelect,
                "Assets/Audio/SFX/Buildings/Sound Effect/archery_sound.mp3"),
            new(GameplayAudioEvent.MonasterySelect,
                "Assets/Audio/SFX/Buildings/Sound Effect/monastery_sound.mp3"),
            new(GameplayAudioEvent.TowerSelect,
                "Assets/Audio/SFX/Buildings/Sound Effect/tower_sound.mp3"),
            new(GameplayAudioEvent.BuildingFire,
                "Assets/Audio/SFX/Buildings/Destruction/fire.mp3", AudioClipLoadType.Streaming),
            new(GameplayAudioEvent.BuildingExplosion,
                "Assets/Audio/SFX/Buildings/Destruction/building_destroy.mp3"),
            new(GameplayAudioEvent.BuildingPlan,
                "Assets/Audio/SFX/Buildings/Construction/building_plan.mp3"),
            new(GameplayAudioEvent.PawnHammer,
                "Assets/Audio/SFX/Units/Pawn/sfx_pawn_building_01.mp3"),
            new(GameplayAudioEvent.CommandClick,
                "Assets/Audio/SFX/UI/Notification/click.mp3"),
            new(GameplayAudioEvent.CommandRejected,
                "Assets/Audio/SFX/UI/Notification/cant_click.mp3"),
            new(GameplayAudioEvent.UnitProductionFinished,
                "Assets/Audio/SFX/UI/Notification/finish_queue.mp3"),
            new(GameplayAudioEvent.ScoutPromotion,
                "Assets/Audio/SFX/UI/Notification/finish_queue.mp3"),
            new(GameplayAudioEvent.Victory,
                "Assets/Audio/SFX/Match/win.mp3", AudioClipLoadType.CompressedInMemory),
            new(GameplayAudioEvent.Defeat,
                "Assets/Audio/SFX/Match/defeat.mp3", AudioClipLoadType.CompressedInMemory)
        };

        [MenuItem("Tools/Tiny Kingdom/Validate Gameplay Audio")]
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6E.2 Gameplay Audio")]
        public static void Run()
        {
            bool valid = true;
            GameplayAudioCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameplayAudioCatalog>(GameplayAudioSetup.CatalogPath);
            Check(catalog != null, "GameplayAudioCatalog is missing.", ref valid);
            if (catalog != null)
            {
                Check(catalog.PoolSize > 0 && catalog.PoolGrowthSize > 0,
                    "AudioSource initial pool/growth size must be positive.", ref valid);
                Check(catalog.CatastrophicSourceLimit >= 128,
                    "Catastrophic source safety limit is too low for legitimate overlap.", ref valid);
                Check(catalog.SpatialBlend > 0f && catalog.MaxDistance > catalog.MinDistance,
                    "World spatialization settings are invalid.", ref valid);
                for (int i = 0; i < Required.Length; i++)
                    ValidateCue(catalog, Required[i], ref valid);
                ValidateHammerVariation(catalog, ref valid);
                if (catalog.TryGetCue(GameplayAudioEvent.PawnMining, out GameplayAudioCue mining))
                    Check(Mathf.Approximately(mining.Volume, 0.20f),
                        "PawnMining volume must remain at 0.20.", ref valid);
                if (catalog.TryGetCue(GameplayAudioEvent.PawnChopping, out GameplayAudioCue chopping))
                    Check(Mathf.Approximately(chopping.Volume, 0.65f),
                        "PawnChopping volume must remain at 0.65.", ref valid);
            }

            Check(GameplayAudioService.RuntimeBootstrapEnabled,
                "GameplayAudioService runtime bootstrap is disabled.", ref valid);
            Check(Mathf.Approximately(BuildingDamagePresentation.FireThreshold, 0.60f),
                "Building fire audio/VFX threshold must be 60 percent.", ref valid);
            Check(!ContainsPerPlayClipLoads(),
                "Resources.Load<AudioClip> was found in runtime scripts.", ref valid);
            Check(!ContainsLegacyDuplicateLimiter(),
                "Legacy per-event concurrency/cooldown code is still active.", ref valid);
            Check(!ContainsAbsoluteRuntimePath(),
                "An absolute Windows audio path exists in runtime code.", ref valid);
            ValidateCameraViewportRule(ref valid);

            if (Application.isPlaying)
            {
                GameplayAudioService[] services = Object.FindObjectsByType<GameplayAudioService>();
                AudioListener[] listeners = Object.FindObjectsByType<AudioListener>();
                Check(services.Length == 1,
                    $"Expected one GameplayAudioService, found {services.Length}.", ref valid);
                Check(listeners.Length == 1,
                    $"Expected one active AudioListener, found {listeners.Length}.", ref valid);
                if (services.Length == 1)
                {
                    Check(services[0].Catalog == catalog,
                        "Runtime service does not use the project catalog.", ref valid);
                    Check(services[0].PoolSize >= catalog.PoolSize && services[0].IsPoolConfigured,
                        "Runtime expandable AudioSource pool is invalid.", ref valid);
                    Check(!services[0].HasPerEventConcurrencyLimit
                        && !services[0].HasSameClipCooldown,
                        "Runtime duplicate-sound limiting is unexpectedly enabled.", ref valid);
                    Check(services[0].SfxVolume is >= 0f and <= 1f,
                        "SfxVolume is outside 0..1.", ref valid);
                    Debug.Log($"Phase 6E.2 audio counters: ActiveOneShots="
                        + $"{services[0].ActiveOneShots}, ActiveLoops={services[0].ActiveLoops}, "
                        + $"PoolSize={services[0].PoolSize}, "
                        + $"PoolGrowthCount={services[0].PoolGrowthCount}, "
                        + $"DroppedBySafetyLimit={services[0].DroppedBySafetyLimit}.");
                }
            }
            else
            {
                string sceneText = File.ReadAllText("Assets/Scenes/Map.unity");
                Check(Count(sceneText, "--- !u!81 ") == 1,
                    "Map scene must serialize exactly one AudioListener.", ref valid);
            }

            if (valid)
                Debug.Log("Phase 6E.2 Gameplay Audio validation passed. Run again in Play Mode; "
                    + "manual listening, overlap, Fog, result and restart tests remain.");
        }

        private static void ValidateCue(GameplayAudioCatalog catalog,
            ExpectedCue expected, ref bool valid)
        {
            bool exists = catalog.TryGetCue(expected.Id, out GameplayAudioCue cue);
            Check(exists && cue.Clip != null, $"Missing AudioClip for {expected.Id}.", ref valid);
            if (!exists || cue.Clip == null) return;
            Check(cue.StartTimeSeconds < cue.Clip.length,
                $"Playback start is outside {expected.Id} clip length.", ref valid);
            string path = AssetDatabase.GetAssetPath(cue.Clip);
            Check(path == expected.Path,
                $"{expected.Id} points to unexpected asset '{path}'.", ref valid);
            ValidateImporter(expected.Id.ToString(), path, expected.LoadType, ref valid);
        }

        private static void ValidateHammerVariation(GameplayAudioCatalog catalog, ref bool valid)
        {
            if (!catalog.TryGetCue(GameplayAudioEvent.PawnHammer, out GameplayAudioCue cue)) return;
            Check(cue.ClipCount == 2, "PawnHammer must contain exactly two clips.", ref valid);
            Check(Mathf.Approximately(cue.MaxPlaybackSeconds, 0.7f),
                "PawnHammer playback must end before the next 0.8-second work cycle.", ref valid);
            Check(cue.Variations.Length == 1 && cue.Variations[0] != null,
                "PawnHammer second variation is missing.", ref valid);
            if (cue.Variations.Length == 0 || cue.Variations[0] == null) return;
            const string expected = "Assets/Audio/SFX/Units/Pawn/sfx_pawn_building_02.mp3";
            string path = AssetDatabase.GetAssetPath(cue.Variations[0]);
            Check(path == expected, $"PawnHammer variation points to '{path}'.", ref valid);
            ValidateImporter("PawnHammer variation", path, AudioClipLoadType.DecompressOnLoad,
                ref valid);
        }

        private static void ValidateImporter(string label, string path,
            AudioClipLoadType loadType, ref bool valid)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
            {
                Check(false, $"{label} has no AudioImporter.", ref valid);
                return;
            }
            Check(importer.defaultSampleSettings.loadType == loadType,
                $"{label} has the wrong load type.", ref valid);
            bool shouldPreload = loadType != AudioClipLoadType.Streaming;
            Check(importer.defaultSampleSettings.preloadAudioData == shouldPreload,
                $"{label} preload setting is incorrect.", ref valid);
        }

        private static void ValidateCameraViewportRule(ref bool valid)
        {
            var cameraObject = new GameObject("GameplayAudioViewportValidation");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.aspect = 1f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                Check(GameplayAudioService.IsInsideCameraViewport(camera, Vector3.zero),
                    "A centered world SFX source was rejected by the camera viewport.", ref valid);
                Check(!GameplayAudioService.IsInsideCameraViewport(camera,
                        new Vector3(20f, 0f, 0f)),
                    "An off-screen world SFX source passed the camera viewport rule.", ref valid);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static bool ContainsPerPlayClipLoads()
            => RuntimeScriptsContain("Resources.Load<AudioClip>");

        private static bool ContainsLegacyDuplicateLimiter()
            => RuntimeScriptsContain("maxConcurrent")
                || RuntimeScriptsContain("duplicateCooldown")
                || RuntimeScriptsContain("DroppedByConcurrency");

        private static bool ContainsAbsoluteRuntimePath()
            => RuntimeScriptsContain("D:\\\\TinyKingdomRL")
                || RuntimeScriptsContain("C:\\\\Users\\\\ASUS");

        private static bool RuntimeScriptsContain(string token)
        {
            string[] files = Directory.GetFiles("Assets/Scripts", "*.cs",
                SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].Contains("Editor")) continue;
                if (File.ReadAllText(files[i]).Contains(token)) return true;
            }
            return false;
        }

        private static int Count(string value, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = value.IndexOf(token, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }
            return count;
        }

        private static void Check(bool condition, string message, ref bool valid)
        {
            if (condition) return;
            valid = false;
            Debug.LogError($"Phase 6E.2 Gameplay Audio validation: {message}");
        }
    }
}
