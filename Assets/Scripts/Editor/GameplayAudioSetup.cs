using System;
using TinyKingdom.Audio;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class GameplayAudioSetup
    {
        public const string CatalogPath = "Assets/Resources/Audio/GameplayAudioCatalog.asset";

        private readonly struct ClipMapping
        {
            public ClipMapping(GameplayAudioEvent eventId, string path)
            {
                EventId = eventId;
                Path = path;
            }

            public GameplayAudioEvent EventId { get; }
            public string Path { get; }
        }

        private static readonly ClipMapping[] Clips =
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
            new(GameplayAudioEvent.SheepHurt,
                "Assets/Audio/SFX/Economy/Food/sfx_sheep_hurt_01.mp3")
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
            for (int i = 0; i < Clips.Length; i++)
            {
                ClipMapping mapping = Clips[i];
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(mapping.Path);
                if (clip == null)
                    throw new InvalidOperationException($"Missing gameplay SFX: {mapping.Path}");
                if (!catalog.AssignClip(mapping.EventId, clip))
                    throw new InvalidOperationException($"Catalog has no {mapping.EventId} cue.");
                ConfigureImporter(mapping.Path);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Gameplay Audio prepared: seven verified clips assigned; "
                + "existing cue tuning preserved.", catalog);
        }

        private static void ConfigureImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                throw new InvalidOperationException($"Expected AudioImporter at {path}");
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool changed = settings.loadType != AudioClipLoadType.DecompressOnLoad
                || !settings.preloadAudioData || importer.loadInBackground;
            if (!changed) return;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
