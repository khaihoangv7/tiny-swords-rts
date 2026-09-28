using System.IO;
using TinyKingdom.Audio;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class GameplayAudioValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Gameplay Audio")]
        public static void Run()
        {
            bool valid = true;
            GameplayAudioCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameplayAudioCatalog>(GameplayAudioSetup.CatalogPath);
            Check(catalog != null, "GameplayAudioCatalog is missing.", ref valid);
            if (catalog != null)
            {
                Check(catalog.PoolSize > 0, "AudioSource pool size must be positive.", ref valid);
                Check(catalog.SpatialBlend > 0f && catalog.MaxDistance > catalog.MinDistance,
                    "World spatialization settings are invalid.", ref valid);
                ValidateCue(catalog, GameplayAudioEvent.WarriorAttack, ref valid);
                ValidateCue(catalog, GameplayAudioEvent.LancerAttack, ref valid);
                ValidateCue(catalog, GameplayAudioEvent.ArcherAttack, ref valid);
                ValidateCue(catalog, GameplayAudioEvent.MonkHeal, ref valid);
                ValidateCue(catalog, GameplayAudioEvent.PawnMining, ref valid);
                ValidateCue(catalog, GameplayAudioEvent.PawnChopping, ref valid);
                ValidateCue(catalog, GameplayAudioEvent.SheepHurt, ref valid);
            }

            Check(GameplayAudioService.RuntimeBootstrapEnabled,
                "GameplayAudioService runtime bootstrap is disabled.", ref valid);
            Check(!ContainsPerPlayClipLoads(),
                "Resources.Load<AudioClip> was found in runtime scripts.", ref valid);

            if (Application.isPlaying)
            {
                GameplayAudioService[] services =
                    Object.FindObjectsByType<GameplayAudioService>();
                AudioListener[] listeners = Object.FindObjectsByType<AudioListener>();
                Check(services.Length == 1,
                    $"Expected one GameplayAudioService, found {services.Length}.", ref valid);
                Check(listeners.Length == 1,
                    $"Expected one active AudioListener, found {listeners.Length}.", ref valid);
                if (services.Length == 1)
                {
                    Check(services[0].Catalog == catalog,
                        "Runtime service does not use the project catalog.", ref valid);
                    Check(services[0].PoolSize > 0 && services[0].IsPoolConfigured,
                        "Runtime AudioSource pool is invalid.", ref valid);
                    Check(services[0].SfxVolume >= 0f && services[0].SfxVolume <= 1f,
                        "SfxVolume is outside 0..1.", ref valid);
                }
            }
            else
            {
                string sceneText = File.ReadAllText("Assets/Scenes/Map.unity");
                Check(Count(sceneText, "--- !u!81 ") == 1,
                    "Map scene must serialize exactly one AudioListener.", ref valid);
            }

            if (valid)
                Debug.Log("Gameplay Audio validation passed. Run again in Play Mode to verify "
                    + "the live service, pool and active listener; manual listening/Fog tests remain.");
        }

        private static void ValidateCue(GameplayAudioCatalog catalog,
            GameplayAudioEvent eventId, ref bool valid)
        {
            bool exists = catalog.TryGetCue(eventId, out GameplayAudioCue cue);
            Check(exists && cue.Clip != null, $"Missing AudioClip for {eventId}.", ref valid);
            if (!exists || cue.Clip == null) return;
            Check(cue.MaxConcurrent > 0, $"Invalid concurrency for {eventId}.", ref valid);
            Check(cue.StartTimeSeconds < cue.Clip.length,
                $"Playback start is outside {eventId} clip length.", ref valid);
            string path = AssetDatabase.GetAssetPath(cue.Clip);
            Check(path == ExpectedPath(eventId),
                $"{eventId} points to unexpected asset '{path}'.", ref valid);
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                Check(importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad,
                    $"{eventId} is not Decompress On Load.", ref valid);
                Check(importer.defaultSampleSettings.preloadAudioData,
                    $"{eventId} does not preload audio data.", ref valid);
            }
            else Check(false, $"{eventId} has no AudioImporter.", ref valid);
        }

        private static string ExpectedPath(GameplayAudioEvent eventId) => eventId switch
        {
            GameplayAudioEvent.WarriorAttack =>
                "Assets/Audio/SFX/Units/Warrior/sfx_warrior_attack_01.mp3",
            GameplayAudioEvent.LancerAttack =>
                "Assets/Audio/SFX/Units/Lancer/sfx_lancer_attack_01.mp3",
            GameplayAudioEvent.ArcherAttack =>
                "Assets/Audio/SFX/Units/Archer/sfx_archer_attack_01.mp3",
            GameplayAudioEvent.MonkHeal =>
                "Assets/Audio/SFX/Units/Monk/sfx_monk_heal_01.mp3",
            GameplayAudioEvent.PawnMining =>
                "Assets/Audio/SFX/Units/Pawn/sfx_pawn_mining_01.mp3",
            GameplayAudioEvent.PawnChopping =>
                "Assets/Audio/SFX/Units/Pawn/sfx_pawn_chopping_01.mp3",
            GameplayAudioEvent.SheepHurt =>
                "Assets/Audio/SFX/Economy/Food/sfx_sheep_hurt_01.mp3",
            _ => string.Empty
        };

        private static bool ContainsPerPlayClipLoads()
        {
            string[] files = Directory.GetFiles("Assets/Scripts", "*.cs",
                SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].Contains("Editor")) continue;
                if (File.ReadAllText(files[i]).Contains("Resources.Load<AudioClip>")) return true;
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
            Debug.LogError($"Gameplay Audio validation: {message}");
        }
    }
}
