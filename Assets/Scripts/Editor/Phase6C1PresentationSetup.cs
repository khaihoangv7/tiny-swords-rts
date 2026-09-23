#if UNITY_EDITOR
#pragma warning disable 0618
using System;
using System.IO;
using System.Linq;
using TinyKingdom.Presentation;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class Phase6C1PresentationSetup
    {
        public const string RallyFlagPath =
            "Assets/UI Elements/UI Elements/Icons/Icon_09.png";
        public const string ShadowPath = "Assets/Terrain/Tileset/Shadow.png";
        private const string CatalogPath =
            "Assets/Resources/Presentation/Phase5C21Presentation.asset";

        static Phase6C1PresentationSetup() => EditorApplication.delayCall += EnsureAssets;

        [MenuItem("Tools/Tiny Kingdom/Prepare Phase 6C.1 Presentation Assets")]
        public static void EnsureAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureAssets;
                return;
            }
            try
            {
                Phase5C21AssetSetup.EnsureAssets();
                EnsureShadowAlpha();
                TextureImporter importer = AssetImporter.GetAtPath(RallyFlagPath)
                    as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("Missing verified Rally flag asset.");
                SpriteMetaData[] slices = importer.spritesheet;
                if (importer.spriteImportMode != SpriteImportMode.Multiple
                    || slices == null || slices.Length != 1
                    || slices[0].name != "Icon_09_0")
                    throw new InvalidOperationException(
                        "Rally marker slicing changed; expected Icon_09_0.");
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                int required = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(width, height)),
                    32, 8192);
                bool changed = importer.textureType != TextureImporterType.Sprite
                    || importer.filterMode != FilterMode.Point || importer.mipmapEnabled
                    || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || importer.wrapMode != TextureWrapMode.Clamp
                    || importer.maxTextureSize < required;
                importer.textureType = TextureImporterType.Sprite;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, required);
                if (changed) importer.SaveAndReimport();

                PresentationAssetCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<PresentationAssetCatalog>(CatalogPath);
                Sprite flag = AssetDatabase.LoadAllAssetsAtPath(RallyFlagPath)
                    .OfType<Sprite>().FirstOrDefault(sprite => sprite.name == "Icon_09_0");
                if (catalog == null || flag == null)
                    throw new InvalidOperationException(
                        "Rally marker catalog or Icon_09_0 sprite could not be loaded.");
                catalog.SetRallyFlag(flag);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 6C.1 presentation setup failed: " + exception);
            }
        }

        private static void EnsureShadowAlpha()
        {
            if (!File.Exists(ShadowPath))
                throw new InvalidOperationException("Missing dedicated terrain Shadow texture.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(ShadowPath)))
                    throw new InvalidOperationException(
                        "Dedicated terrain Shadow texture could not be decoded.");
                Color32[] pixels = texture.GetPixels32();
                bool changed = false;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a == 0 || pixels[i].a == 153) continue;
                    pixels[i].a = 153;
                    changed = true;
                }
                if (!changed) return;
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(ShadowPath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(ShadowPath,
                    ImportAssetOptions.ForceSynchronousImport
                    | ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
#endif
