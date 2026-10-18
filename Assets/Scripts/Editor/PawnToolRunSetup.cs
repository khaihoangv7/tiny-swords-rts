#if UNITY_EDITOR
using System;
using System.Linq;
using TinyKingdom.Economy;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class PawnToolRunSetup
    {
        private const int FrameSize = 192;
        private const int FrameCount = 6;

        [MenuItem("Tools/Tiny Kingdom/Prepare Pawn Tool Run Animations")]
        public static void Run()
        {
            foreach (string faction in new[] { "Blue", "Red" })
            {
                string folder = $"Assets/Units/{faction} Units/Pawn/";
                PawnAnimationSet set = AssetDatabase.LoadAssetAtPath<PawnAnimationSet>(
                    $"Assets/UnitAnimations/{faction}Pawn.asset");
                if (set == null)
                    throw new InvalidOperationException($"Missing {faction} Pawn animation set.");

                var serialized = new SerializedObject(set);
                Assign(serialized, "runAxe", Slice(folder + "Pawn_Run Axe.png"));
                Assign(serialized, "runPickaxe", Slice(folder + "Pawn_Run Pickaxe.png"));
                Assign(serialized, "runKnife", Slice(folder + "Pawn_Run Knife.png"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(set);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Pawn tool-run animation mapping PASS: six complete Axe, Pickaxe and Knife frames per faction.");
        }

        private static Sprite[] Slice(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException("Missing Pawn tool-run sheet: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != FrameSize * FrameCount || height != FrameSize
                || !Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
                throw new InvalidOperationException("Unexpected Pawn tool-run source layout: " + path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects()
                .ToDictionary(rect => rect.name, rect => rect.spriteID);
            string stem = System.IO.Path.GetFileNameWithoutExtension(path);
            var rects = new SpriteRect[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                string name = stem + "_" + i;
                rects[i] = new SpriteRect
                {
                    name = name,
                    rect = new Rect(i * FrameSize, 0, FrameSize, FrameSize),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = previous.TryGetValue(name, out GUID id) ? id : GUID.Generate()
                };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();

            Sprite[] imported = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>().ToArray();
            if (imported.Length != FrameCount)
                throw new InvalidOperationException("Pawn tool-run slice count is not six: " + path);
            return Enumerable.Range(0, FrameCount)
                .Select(index => imported.Single(sprite => sprite.name == stem + "_" + index))
                .ToArray();
        }

        private static void Assign(SerializedObject set, string propertyName, Sprite[] frames)
        {
            SerializedProperty property = set.FindProperty(propertyName)
                ?? throw new InvalidOperationException("Missing Pawn animation field: " + propertyName);
            property.arraySize = FrameCount;
            for (int i = 0; i < FrameCount; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }
    }
}
#endif
