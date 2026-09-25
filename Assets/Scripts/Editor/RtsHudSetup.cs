#if UNITY_EDITOR
#pragma warning disable 0618
using System;
using System.Linq;
using TinyKingdom.UI;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class RtsHudSetup
    {
        private const string Folder = "Assets/Resources/Presentation";
        private const string CatalogPath = Folder + "/RtsHudAssets.asset";
        private const string WoodTable = "Assets/UI Elements/UI Elements/Wood Table/WoodTable.png";
        private const string Slot = "Assets/UI Elements/UI Elements/Wood Table/WoodTable_Slots.png";
        private const string Paper = "Assets/UI Elements/UI Elements/Papers/SpecialPaper.png";
        private const string Icons = "Assets/UI Elements/UI Elements/Icons/";
        private const string Avatars = "Assets/UI Elements/UI Elements/Human Avatars/";

        static RtsHudSetup() => EditorApplication.delayCall += Ensure;

        [MenuItem("Tools/Tiny Kingdom/Prepare RTS HUD")]
        public static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Ensure;
                return;
            }
            try
            {
                Configure(WoodTable, 9);
                Configure(Slot, 1);
                Configure(Paper, 9);
                foreach (int number in new[] { 1, 2, 3, 4, 6, 9 })
                    Configure(Icons + $"Icon_{number:00}.png", 1);
                for (int number = 1; number <= 5; number++)
                    Configure(Avatars + $"Avatars_{number:00}.png", 1);
                EnsureFolder(Folder);
                RtsHudAssetCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<RtsHudAssetCatalog>(CatalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<RtsHudAssetCatalog>();
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }
                catalog.Set(Load(WoodTable), Load(Paper), First(Slot),
                    First(Icons + "Icon_01.png"), First(Icons + "Icon_02.png"),
                    First(Icons + "Icon_03.png"), First(Icons + "Icon_04.png"),
                    First(Icons + "Icon_09.png"), First(Icons + "Icon_06.png"),
                    First(Avatars + "Avatars_01.png"),
                    First(Avatars + "Avatars_02.png"),
                    First(Avatars + "Avatars_03.png"),
                    First(Avatars + "Avatars_04.png"),
                    First(Avatars + "Avatars_05.png"));
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                Debug.Log("RTS HUD setup complete: verified Tiny Swords panels, icons and avatars prepared.");
            }
            catch (Exception exception)
            {
                Debug.LogError("RTS HUD setup failed: " + exception);
            }
        }

        private static void Configure(string path, int expectedSprites)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing HUD texture: " + path);
            SpriteMetaData[] slices = importer.spritesheet;
            if (importer.spriteImportMode != SpriteImportMode.Multiple
                || slices == null || slices.Length != expectedSprites)
                throw new InvalidOperationException(
                    $"Verified HUD slicing changed at {path}; expected {expectedSprites} sprites.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
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

        private static Sprite[] Load(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite => NumericSuffix(sprite.name)).ToArray();

        private static Sprite First(string path) => Load(path).FirstOrDefault();

        private static int NumericSuffix(string name)
        {
            int split = name.LastIndexOf('_');
            return split >= 0 && int.TryParse(name[(split + 1)..], out int value)
                ? value : int.MaxValue;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path[..split];
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path[(split + 1)..]);
        }
    }
}
#endif
