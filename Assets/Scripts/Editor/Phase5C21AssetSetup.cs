#if UNITY_EDITOR
#pragma warning disable 0618
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyKingdom.Presentation;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class Phase5C21AssetSetup
    {
        private const string CatalogFolder = "Assets/Resources/Presentation";
        private const string CatalogPath = CatalogFolder + "/Phase5C21Presentation.asset";
        private const string DeadPath = "Assets/Particle FX/Dead.png";
        private const string DeadRuntimePath = CatalogFolder + "/DeadRuntime.bytes";

        private static readonly string[] PixelArtPaths = BuildPixelArtPaths();

        static Phase5C21AssetSetup() => EditorApplication.delayCall += EnsureAssets;

        [MenuItem("Tools/Tiny Kingdom/Prepare Phase 5C2.1 Presentation Assets")]
        public static void EnsureAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureAssets;
                return;
            }
            try
            {
                foreach (string path in PixelArtPaths) ConfigurePixelArt(path, path == DeadPath);
                EnsureFolder(CatalogFolder);
                EnsureDeadRuntimeAtlas();
                PresentationAssetCatalog catalog = AssetDatabase.LoadAssetAtPath<PresentationAssetCatalog>(CatalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<PresentationAssetCatalog>();
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }

                SpriteFrameSet[] trees = Enumerable.Range(1, 4).Select(i =>
                {
                    var set = new SpriteFrameSet();
                    set.SetFrames(LoadFrames($"Assets/Terrain/Resources/Wood/Trees/Tree{i}.png"));
                    return set;
                }).ToArray();
                Sprite[] stumps = Enumerable.Range(1, 4)
                    .Select(i => LoadFrames($"Assets/Terrain/Resources/Wood/Trees/Stump {i}.png").FirstOrDefault())
                    .ToArray();
                Sprite[] gold = Enumerable.Range(1, 6)
                    .Select(i => LoadFrames($"Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone {i}.png").FirstOrDefault())
                    .ToArray();
                SpriteFrameSet[] highlights = Enumerable.Range(1, 6).Select(i =>
                {
                    var set = new SpriteFrameSet();
                    set.SetFrames(LoadFrames($"Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone {i}_Highlight.png"));
                    return set;
                }).ToArray();
                Sprite[] dead = LoadFrames(DeadPath).Take(14).ToArray();
                if (File.Exists(DeadPath) && (dead.Length != 14 || dead.Any(sprite => sprite == null)))
                {
                    AssetDatabase.ImportAsset(DeadPath,
                        ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    dead = LoadFrames(DeadPath).Take(14).ToArray();
                }
                if (dead.Length != 14 || dead.Any(sprite => sprite == null))
                {
                    Sprite[] existing = catalog.DeadFrames;
                    if (existing != null && existing.Length == 14
                        && existing.All(sprite => sprite != null))
                        dead = existing;
                    else
                    {
                        // UnitDeathVfx uses the verified Resources atlas fallback when
                        // TextureImporter sprite subassets are not ready yet.
                        dead = Array.Empty<Sprite>();
                    }
                }
                catalog.SetData(
                    LoadFrames("Assets/Terrain/Resources/Meat/Sheep/Sheep_Idle.png"),
                    LoadFrames("Assets/Terrain/Resources/Meat/Sheep/Sheep_Grass.png"),
                    LoadFrames("Assets/Terrain/Resources/Meat/Sheep/Sheep_Move.png"),
                    trees, stumps, gold, highlights, dead);
                SpriteFrameSet[] bushes = Enumerable.Range(1, 4).Select(i =>
                {
                    var set = new SpriteFrameSet();
                    set.SetFrames(LoadFrames($"Assets/Terrain/Decorations/Bushes/Bushe{i}.png"));
                    return set;
                }).ToArray();
                catalog.SetBushes(bushes);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 5C2.1 presentation asset setup failed: " + exception);
            }
        }

        private static void ConfigurePixelArt(string path, bool sliceDeath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.filterMode != FilterMode.Point || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.wrapMode != TextureWrapMode.Clamp;
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            int required = Mathf.NextPowerOfTwo(Mathf.Max(width, height));
            required = Mathf.Clamp(required, 32, 8192);
            if (importer.maxTextureSize < required)
            {
                importer.maxTextureSize = required;
                changed = true;
            }
            if (sliceDeath && (importer.spriteImportMode != SpriteImportMode.Multiple
                || importer.spritesheet == null || importer.spritesheet.Length != 14))
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                var slices = new SpriteMetaData[14];
                for (int i = 0; i < slices.Length; i++)
                {
                    int row = i / 7;
                    int column = i % 7;
                    slices[i] = new SpriteMetaData
                    {
                        name = "Dead_" + i,
                        rect = new Rect(column * 128, (1 - row) * 128, 128, 128),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                }
                importer.spritesheet = slices;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        private static Sprite[] LoadFrames(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite => NumericSuffix(sprite.name)).ToArray();

        private static int NumericSuffix(string name)
        {
            int split = name.LastIndexOf('_');
            return split >= 0 && int.TryParse(name[(split + 1)..], out int value)
                ? value : int.MaxValue;
        }

        private static string[] BuildPixelArtPaths()
        {
            var paths = new List<string>
            {
                "Assets/Terrain/Resources/Meat/Sheep/Sheep_Idle.png",
                "Assets/Terrain/Resources/Meat/Sheep/Sheep_Grass.png",
                "Assets/Terrain/Resources/Meat/Sheep/Sheep_Move.png",
                DeadPath
            };
            for (int i = 1; i <= 4; i++)
            {
                paths.Add($"Assets/Terrain/Resources/Wood/Trees/Tree{i}.png");
                paths.Add($"Assets/Terrain/Resources/Wood/Trees/Stump {i}.png");
            }
            for (int i = 1; i <= 6; i++)
            {
                paths.Add($"Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone {i}.png");
                paths.Add($"Assets/Terrain/Resources/Gold/Gold Stones/Gold Stone {i}_Highlight.png");
            }
            for (int i = 1; i <= 4; i++)
                paths.Add($"Assets/Terrain/Decorations/Bushes/Bushe{i}.png");
            return paths.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path[..path.LastIndexOf('/')];
            string name = path[(path.LastIndexOf('/') + 1)..];
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void EnsureDeadRuntimeAtlas()
        {
            // Local source artwork may be absent; the shipped verified atlas is
            // already the runtime fallback and should not break unrelated map setup.
            if (!File.Exists(DeadPath) && File.Exists(DeadRuntimePath)) return;
            byte[] source = File.ReadAllBytes(DeadPath);
            if (File.Exists(DeadRuntimePath)
                && File.ReadAllBytes(DeadRuntimePath).SequenceEqual(source)) return;
            File.WriteAllBytes(DeadRuntimePath, source);
            AssetDatabase.ImportAsset(DeadRuntimePath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
    }
}
#endif
