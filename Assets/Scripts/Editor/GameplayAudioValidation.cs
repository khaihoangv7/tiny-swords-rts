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
                "Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_01.mp3"),
            new(GameplayAudioEvent.LancerAttack,
                "Assets/Audio/SFX/Units/Lancer/sfx_lancer_attack_01.mp3"),
            new(GameplayAudioEvent.ArcherAttack,
                "Assets/Audio/SFX/Units/Archer/sfx_arrow_sound.mp3"),
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
            new(GameplayAudioEvent.BuildingCompleted,
                "Assets/Audio/SFX/Buildings/Construction/building_completed.mp3"),
            new(GameplayAudioEvent.ArcherArrowImpact,
                "Assets/Audio/SFX/Units/Archer/sfx_arrow_dmg.mp3"),
            new(GameplayAudioEvent.GenericCommandVoice,
                "Assets/Audio/SFX/Commands/units_say_01.mp3"),
            new(GameplayAudioEvent.ArmyCommandVoice,
                "Assets/Audio/SFX/Commands/army_say_01.mp3"),
            new(GameplayAudioEvent.Victory,
                "Assets/Audio/SFX/Match/win.mp3", AudioClipLoadType.CompressedInMemory),
            new(GameplayAudioEvent.Defeat,
                "Assets/Audio/SFX/Match/defeat.mp3", AudioClipLoadType.CompressedInMemory)
        };

        [MenuItem("Tools/Tiny Kingdom/Validate Gameplay Audio")]
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6E.2 Gameplay Audio")]
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6E Audio Expansion")]
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
                ValidateWarriorHitVariations(catalog, ref valid);
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
            ValidateCommandVoicePolicy(ref valid);
            ValidateExpansionHooks(ref valid);

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
            Check(cue.Variations.Length == 1 && cue.Variations[0] != null,
                "PawnHammer second variation is missing.", ref valid);
            if (cue.Variations.Length == 0 || cue.Variations[0] == null) return;
            const string expected = "Assets/Audio/SFX/Units/Pawn/sfx_pawn_building_02.mp3";
            string path = AssetDatabase.GetAssetPath(cue.Variations[0]);
            Check(path == expected, $"PawnHammer variation points to '{path}'.", ref valid);
            ValidateImporter("PawnHammer variation", path, AudioClipLoadType.DecompressOnLoad,
                ref valid);
        }

        private static void ValidateWarriorHitVariations(GameplayAudioCatalog catalog,
            ref bool valid)
        {
            if (!catalog.TryGetCue(GameplayAudioEvent.WarriorAttack,
                    out GameplayAudioCue cue)) return;
            Check(cue.ClipCount == 7,
                "WarriorAttack must contain exactly seven sword-hit clips.", ref valid);
            Check(cue.Variations.Length == 6,
                "WarriorAttack must contain six variations plus its primary clip.", ref valid);
            for (int i = 0; i < cue.Variations.Length; i++)
            {
                string expected = $"Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_{i + 2:00}.mp3";
                string path = cue.Variations[i] != null
                    ? AssetDatabase.GetAssetPath(cue.Variations[i]) : string.Empty;
                Check(path == expected,
                    $"Warrior hit variation {i + 2} points to '{path}'.", ref valid);
                if (!string.IsNullOrEmpty(path))
                    ValidateImporter($"Warrior hit variation {i + 2}", path,
                        AudioClipLoadType.DecompressOnLoad, ref valid);
            }
        }

        private static void ValidateCommandVoicePolicy(ref bool valid)
        {
            Check(Mathf.Approximately(HumanCommandVoiceFeedback.GenericProbability, 0.30f)
                && Mathf.Approximately(HumanCommandVoiceFeedback.ArmyProbability, 0.40f),
                "Command voice probabilities must be 30% generic and 40% army.", ref valid);
            Check(Mathf.Approximately(HumanCommandVoiceFeedback.GenericCooldownSeconds, 30f)
                && Mathf.Approximately(HumanCommandVoiceFeedback.ArmyCooldownSeconds, 10f),
                "Command voice cooldowns must be 30s generic and 10s army.", ref valid);
            Check(!HumanCommandVoiceFeedback.UsesArmyVoice(3)
                && HumanCommandVoiceFeedback.UsesArmyVoice(4),
                "Army voice category must require strictly more than three combat units.",
                ref valid);
            Check(HumanCommandVoiceFeedback.IsArmyCombatUnit(TinyKingdom.Units.UnitType.Warrior)
                && HumanCommandVoiceFeedback.IsArmyCombatUnit(TinyKingdom.Units.UnitType.Lancer)
                && HumanCommandVoiceFeedback.IsArmyCombatUnit(TinyKingdom.Units.UnitType.Archer)
                && !HumanCommandVoiceFeedback.IsArmyCombatUnit(TinyKingdom.Units.UnitType.Pawn)
                && !HumanCommandVoiceFeedback.IsArmyCombatUnit(TinyKingdom.Units.UnitType.Monk),
                "Army voice eligibility must contain only Warrior, Lancer and Archer.",
                ref valid);

            var policy = new HumanCommandVoiceFeedback();
            Check(policy.TryAcknowledgeForValidation(1, 0, 0f, 0f, true)
                && Mathf.Approximately(policy.NextGenericTime, 30f),
                "A successful generic voice did not start its 30s cooldown.", ref valid);
            Check(!policy.TryAcknowledgeForValidation(1, 0, 1f, 0f, true),
                "Generic command voice ignored its cooldown.", ref valid);
            Check(policy.TryAcknowledgeForValidation(4, 4, 1f, 0f, true)
                && Mathf.Approximately(policy.NextArmyTime, 11f)
                && Mathf.Approximately(policy.NextGenericTime, 30f),
                "Army and generic voice cooldowns are not independent.", ref valid);

            var failedPlayback = new HumanCommandVoiceFeedback();
            Check(!failedPlayback.TryAcknowledgeForValidation(1, 0, 5f, 0f, false)
                && Mathf.Approximately(failedPlayback.NextGenericTime, 0f),
                "A failed voice playback incorrectly started a cooldown.", ref valid);

            var failedRoll = new HumanCommandVoiceFeedback();
            Check(!failedRoll.TryAcknowledgeForValidation(1, 0, 5f, 1f, true)
                && Mathf.Approximately(failedRoll.NextGenericTime, 0f),
                "A failed generic roll incorrectly started a cooldown.", ref valid);
            Check(!failedRoll.TryAcknowledgeForValidation(4, 4, 5f, 1f, true)
                && Mathf.Approximately(failedRoll.NextArmyTime, 0f)
                && Mathf.Approximately(failedRoll.NextGenericTime, 0f),
                "A failed army roll started a cooldown or fell back to generic.", ref valid);
        }

        private static void ValidateExpansionHooks(ref bool valid)
        {
            Check(SourceContains("Assets/Scripts/Buildings/BuildingSite.cs",
                    "State = BuildingState.Completed", "completionAudioPlayed",
                    "GameplayAudioEvent.BuildingCompleted"),
                "Building completion is missing its authoritative exactly-once hook.",
                ref valid);
            Check(SourceContains("Assets/Scripts/Units/CombatController.cs",
                    "LaunchProjectile", "PlayArcherAttackSound",
                    "if (damaged) PlayMeleeAttackSound"),
                "Archer release or successful Warrior hit hook is missing.", ref valid);
            Check(SourceContains("Assets/Scripts/Units/CombatProjectile.cs",
                    "bool damaged = target.ReceiveAttack", "damaged && unitTarget",
                    "GameplayAudioEvent.ArcherArrowImpact"),
                "Archer unit-impact audio is not tied to successful projectile damage.",
                ref valid);
            Check(SourceContains("Assets/Scripts/Input/PlayerCommandController.cs",
                    "FinishAcceptedCommand", "HumanCommandVoiceFeedback",
                    "AcceptCommand"),
                "Human command voice is not connected after accepted recipients.",
                ref valid);
            Check(!DirectoryContains("Assets/Scripts/AI", "GenericCommandVoice")
                && !DirectoryContains("Assets/Scripts/AI", "ArmyCommandVoice")
                && !DirectoryContains("Assets/Scripts/AI", "HumanCommandVoiceFeedback"),
                "AI code must not invoke Human command voice feedback.", ref valid);
            Check(!File.ReadAllText("Assets/Scripts/Audio/GameplayAudioService.cs")
                    .Contains("Time.timeScale"),
                "GameplayAudioService must not pitch audio from Time.timeScale.", ref valid);
        }

        private static bool SourceContains(string path, params string[] tokens)
        {
            if (!File.Exists(path)) return false;
            string source = File.ReadAllText(path);
            for (int i = 0; i < tokens.Length; i++)
                if (!source.Contains(tokens[i])) return false;
            return true;
        }

        private static bool DirectoryContains(string path, string token)
        {
            if (!Directory.Exists(path)) return false;
            string[] files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                if (File.ReadAllText(files[i]).Contains(token)) return true;
            return false;
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
