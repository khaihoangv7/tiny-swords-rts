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
    public static class Phase6CMatchResultSetup
    {
        private const string CatalogFolder = "Assets/Resources/Presentation";
        private const string CatalogPath = CatalogFolder + "/Phase6CMatchResultPresentation.asset";
        private const string Ribbons = "Assets/UI Elements/UI Elements/Ribbons/BigRibbons.png";
        private const string Panel = "Assets/UI Elements/UI Elements/Papers/SpecialPaper.png";
        private const string BlueRegular = "Assets/UI Elements/UI Elements/Buttons/SmallBlueSquareButton_Regular.png";
        private const string BluePressed = "Assets/UI Elements/UI Elements/Buttons/SmallBlueSquareButton_Pressed.png";
        private const string RedRegular = "Assets/UI Elements/UI Elements/Buttons/SmallRedSquareButton_Regular.png";
        private const string RedPressed = "Assets/UI Elements/UI Elements/Buttons/SmallRedSquareButton_Pressed.png";

        static Phase6CMatchResultSetup() => EditorApplication.delayCall += EnsureAssets;

        [MenuItem("Tools/Tiny Kingdom/Prepare Phase 6C Result UI Assets")]
        public static void EnsureAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureAssets;
                return;
            }
            try
            {
                ConfigurePixelArt(Ribbons, 15);
                ConfigurePixelArt(Panel, 9);
                ConfigurePixelArt(BlueRegular, 1);
                ConfigurePixelArt(BluePressed, 1);
                ConfigurePixelArt(RedRegular, 1);
                ConfigurePixelArt(RedPressed, 1);
                EnsureFolder(CatalogFolder);

                MatchResultAssetCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<MatchResultAssetCatalog>(CatalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<MatchResultAssetCatalog>();
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }
                Sprite[] ribbons = LoadSprites(Ribbons);
                catalog.SetData(ribbons.Take(3).ToArray(), ribbons.Skip(3).Take(3).ToArray(),
                    LoadSprites(Panel), First(BlueRegular), First(BluePressed),
                    First(RedRegular), First(RedPressed));
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 6C result UI asset setup failed: " + exception);
            }
        }

        private static void ConfigurePixelArt(string path, int expectedSprites)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing UI texture: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            SpriteMetaData[] slices = importer.spritesheet;
            if (importer.spriteImportMode != SpriteImportMode.Multiple
                || slices == null || slices.Length != expectedSprites)
                throw new InvalidOperationException(
                    $"Verified slicing changed at {path}; expected {expectedSprites} sprites.");
            int maxSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(width, height)), 32, 8192);
            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.filterMode != FilterMode.Point || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.maxTextureSize < maxSize;
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = maxSize;
            if (changed) importer.SaveAndReimport();
        }

        private static Sprite First(string path) => LoadSprites(path).FirstOrDefault();

        private static Sprite[] LoadSprites(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite => NumericSuffix(sprite.name)).ToArray();

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
