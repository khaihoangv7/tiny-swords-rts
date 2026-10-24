using System;
using System.Text;
using TinyKingdom.Audio;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class GameplayAudioSetup
    {
        public const string CatalogPath = "Assets/Resources/Audio/GameplayAudioCatalog.asset";

        private enum ImportProfile { ShortSfx, ResultTheme, StreamingLoop }

        private readonly struct ClipMapping
        {
            public ClipMapping(GameplayAudioEvent eventId, string path,
                ImportProfile profile = ImportProfile.ShortSfx)
            {
                EventId = eventId;
                Path = path;
                Profile = profile;
            }

            public GameplayAudioEvent EventId { get; }
            public string Path { get; }
            public ImportProfile Profile { get; }
        }

        private static readonly ClipMapping[] Clips =
        {
            new(GameplayAudioEvent.WarriorAttack,
                "Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_01.mp3"),
            new(GameplayAudioEvent.WarriorAttackAlternate,
                "Assets/Audio/SFX/Units/Warrior/sfx_warrior_attack_01.mp3"),
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
                "Assets/Audio/SFX/Buildings/Destruction/fire.mp3", ImportProfile.StreamingLoop),
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
            new(GameplayAudioEvent.BuildingCompleted,
                "Assets/Audio/SFX/Buildings/Construction/building_completed.mp3"),
            new(GameplayAudioEvent.ArcherArrowImpact,
                "Assets/Audio/SFX/Units/Archer/sfx_arrow_dmg.mp3"),
            new(GameplayAudioEvent.GenericCommandVoice,
                "Assets/Audio/SFX/Commands/units_say_01.mp3"),
            new(GameplayAudioEvent.ArmyCommandVoice,
                "Assets/Audio/SFX/Commands/army_say_01.mp3"),
            new(GameplayAudioEvent.Victory,
                "Assets/Audio/SFX/Match/win.mp3", ImportProfile.ResultTheme),
            new(GameplayAudioEvent.Defeat,
                "Assets/Audio/SFX/Match/defeat.mp3", ImportProfile.ResultTheme)
        };

        private const string HammerVariationPath =
            "Assets/Audio/SFX/Units/Pawn/sfx_pawn_building_02.mp3";

        private static readonly string[] WarriorHitVariationPaths =
        {
            "Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_02.mp3",
            "Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_03.mp3",
            "Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_04.mp3",
            "Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_05.mp3",
            "Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_06.mp3",
            "Assets/Audio/SFX/Units/Warrior/sfx_sword_hit_07.mp3"
        };

        [MenuItem("Tools/Tiny Kingdom/Prepare Gameplay Audio")]
        public static void Prepare()
        {
            EnsureFolder("Assets/Resources", "Audio");
            GameplayAudioCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameplayAudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GameplayAudioCatalog>();
                catalog.EnsureDefaultCues();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.EnsureDefaultCues();
            catalog.SetMusicClip(null);
            var report = new StringBuilder("Gameplay Audio prepared:\n");
            for (int i = 0; i < Clips.Length; i++)
            {
                ClipMapping mapping = Clips[i];
                AudioClip clip = LoadRequired(mapping.Path);
                if (!catalog.AssignClip(mapping.EventId, clip))
                    throw new InvalidOperationException($"Catalog has no {mapping.EventId} cue.");
                ConfigureImporter(mapping.Path, mapping.Profile);
                AppendObservation(report, mapping.EventId.ToString(), mapping.Path, clip);
            }

            AudioClip hammerVariation = LoadRequired(HammerVariationPath);
            catalog.AssignVariations(GameplayAudioEvent.PawnHammer,
                new[] { hammerVariation });
            ConfigureImporter(HammerVariationPath, ImportProfile.ShortSfx);
            AppendObservation(report, "PawnHammer variation 2", HammerVariationPath,
                hammerVariation);

            var warriorVariations = new AudioClip[WarriorHitVariationPaths.Length];
            for (int i = 0; i < WarriorHitVariationPaths.Length; i++)
            {
                string path = WarriorHitVariationPaths[i];
                warriorVariations[i] = LoadRequired(path);
                ConfigureImporter(path, ImportProfile.ShortSfx);
                AppendObservation(report, $"Warrior hit variation {i + 2}", path,
                    warriorVariations[i]);
            }
            catalog.AssignVariations(GameplayAudioEvent.WarriorAttack, warriorVariations);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString(), catalog);
        }

        private static AudioClip LoadRequired(string path)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException($"Missing gameplay SFX: {path}");
            return clip;
        }

        private static void ConfigureImporter(string path, ImportProfile profile)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                throw new InvalidOperationException($"Expected AudioImporter at {path}");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioClipLoadType loadType = profile switch
            {
                ImportProfile.StreamingLoop => AudioClipLoadType.Streaming,
                ImportProfile.ResultTheme => AudioClipLoadType.CompressedInMemory,
                _ => AudioClipLoadType.DecompressOnLoad
            };
            bool preload = profile != ImportProfile.StreamingLoop;
            bool background = profile == ImportProfile.StreamingLoop;
            bool changed = settings.loadType != loadType
                || settings.preloadAudioData != preload
                || importer.loadInBackground != background;
            if (!changed) return;
            settings.loadType = loadType;
            settings.preloadAudioData = preload;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = background;
            importer.SaveAndReimport();
        }

        private static void AppendObservation(StringBuilder report, string label,
            string path, AudioClip clip)
        {
            report.Append("- ").Append(label).Append(": ").Append(path)
                .Append(" | ").Append(clip.length.ToString("0.###"))
                .Append("s, ").Append(clip.channels).Append(" channel(s), ")
                .Append(clip.frequency).AppendLine(" Hz");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
