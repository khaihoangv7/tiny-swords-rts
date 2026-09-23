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
    public static class Phase6C1PresentationSetup
    {
        public const string RallyFlagPath =
            "Assets/UI Elements/UI Elements/Icons/Icon_07.png";
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
                TextureImporter importer = AssetImporter.GetAtPath(RallyFlagPath)
                    as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("Missing verified Rally flag asset.");
                SpriteMetaData[] slices = importer.spritesheet;
                if (importer.spriteImportMode != SpriteImportMode.Multiple
                    || slices == null || slices.Length != 1
                    || slices[0].name != "Icon_07_0")
                    throw new InvalidOperationException(
                        "Rally flag slicing changed; expected the verified Icon_07_0 sprite.");
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
                    .OfType<Sprite>().FirstOrDefault(sprite => sprite.name == "Icon_07_0");
                if (catalog == null || flag == null)
                    throw new InvalidOperationException(
                        "Rally flag catalog or Icon_07_0 sprite could not be loaded.");
                catalog.SetRallyFlag(flag);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 6C.1 presentation setup failed: " + exception);
            }
        }
    }
}
#endif
