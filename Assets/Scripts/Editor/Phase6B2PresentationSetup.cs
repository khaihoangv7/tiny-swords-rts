#if UNITY_EDITOR
#pragma warning disable 0618
using System;
using System.Linq;
using TinyKingdom.Presentation;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class Phase6B2PresentationSetup
    {
        private const string CatalogFolder = "Assets/Resources/Presentation";
        private const string CatalogPath = CatalogFolder + "/Phase6B2BuildingPresentation.asset";
        private const string Fire01 = "Assets/Particle FX/Fire_01.png";
        private const string Fire02 = "Assets/Particle FX/Fire_02.png";
        private const string Fire03 = "Assets/Particle FX/Fire_03.png";
        private const string Explosion01 = "Assets/Particle FX/Explosion_01.png";
        private const string Explosion02 = "Assets/Particle FX/Explosion_02.png";
        private const string BigBarBase = "Assets/UI Elements/UI Elements/Bars/BigBar_Base.png";
        private const string BigBarFill = "Assets/UI Elements/UI Elements/Bars/BigBar_Fill.png";

        static Phase6B2PresentationSetup() => EditorApplication.delayCall += EnsureAssets;

        [MenuItem("Tools/Tiny Kingdom/Prepare Phase 6B.2 Presentation Assets")]
        public static void EnsureAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureAssets;
                return;
            }
            try
            {
                ConfigureAtlas(Fire01, 64, 8);
                ConfigureAtlas(Fire02, 64, 10);
                ConfigureAtlas(Fire03, 64, 12);
                ConfigureAtlas(Explosion01, 192, 8);
                ConfigureAtlas(Explosion02, 192, 10);
                ConfigureBar(BigBarBase);
                ConfigureBar(BigBarFill);
                EnsureFolder(CatalogFolder);

                BuildingPresentationCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(CatalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<BuildingPresentationCatalog>();
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }
                Sprite[] bar = LoadFrames(BigBarBase);
                Sprite[] fill = LoadFrames(BigBarFill);
                catalog.SetData(LoadFrames(Fire01), LoadFrames(Fire02), LoadFrames(Fire03),
                    LoadFrames(Explosion01), LoadFrames(Explosion02),
                    bar.ElementAtOrDefault(0), bar.ElementAtOrDefault(1),
                    bar.ElementAtOrDefault(2), fill.ElementAtOrDefault(0));
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 6B.2 presentation asset setup failed: " + exception);
            }
        }

        private static void ConfigureAtlas(string path, int cellSize, int frameCount)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing atlas: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != cellSize * frameCount || height != cellSize)
                throw new InvalidOperationException($"Unexpected atlas size at {path}: {width}x{height}");
            bool changed = ConfigurePixelArt(importer, width);
            SpriteMetaData[] existing = importer.spritesheet;
            bool validSlices = importer.spriteImportMode == SpriteImportMode.Multiple
                && existing != null && existing.Length == frameCount;
            if (validSlices)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    Rect rect = existing[i].rect;
                    if (existing[i].name != Name(path, i) || rect.x != i * cellSize
                        || rect.y != 0 || rect.width != cellSize || rect.height != cellSize)
                    {
                        validSlices = false;
                        break;
                    }
                }
            }
            if (!validSlices)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                var slices = new SpriteMetaData[frameCount];
                for (int i = 0; i < frameCount; i++)
                {
                    slices[i] = new SpriteMetaData
                    {
                        name = Name(path, i),
                        rect = new Rect(i * cellSize, 0, cellSize, cellSize),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                }
                importer.spritesheet = slices;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        private static void ConfigureBar(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing Life Bar: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out _);
            bool changed = ConfigurePixelArt(importer, width);
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }
            // BigBar slicing is already verified (3 static frame pieces + 1 fill).
            if (changed) importer.SaveAndReimport();
        }

        private static bool ConfigurePixelArt(TextureImporter importer, int sourceWidth)
        {
            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.filterMode != FilterMode.Point || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.spritePixelsPerUnit != 100f
                || importer.maxTextureSize < Mathf.NextPowerOfTwo(sourceWidth);
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(sourceWidth), 32, 8192);
            return changed;
        }

        private static Sprite[] LoadFrames(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite => NumericSuffix(sprite.name)).ToArray();

        private static string Name(string path, int index)
            => System.IO.Path.GetFileNameWithoutExtension(path) + "_" + index;

        private static int NumericSuffix(string name)
        {
            int split = name.LastIndexOf('_');
            return split >= 0 && int.TryParse(name[(split + 1)..], out int value)
                ? value : int.MaxValue;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path[..path.LastIndexOf('/')];
            string name = path[(path.LastIndexOf('/') + 1)..];
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
