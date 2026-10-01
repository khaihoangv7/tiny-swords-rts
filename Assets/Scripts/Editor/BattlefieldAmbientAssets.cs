using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class BattlefieldAmbientAssets
    {
        // Fixed cells preserve the entire ripple and its stable pivot. Automatic
        // slicing split detached pixels from the native three-frame duck strip.
        public static Sprite[] Frames(string path, int cellSize, int count)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing ambient art: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != cellSize * count || height != cellSize)
                throw new InvalidOperationException("Verified ambient sheet dimensions changed: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 64;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            string stem = System.IO.Path.GetFileNameWithoutExtension(path);
            var rects = new SpriteRect[count];
            for (int i = 0; i < count; i++)
            {
                string name = stem + "_Ambient_" + i.ToString("00");
                rects[i] = new SpriteRect
                {
                    name = name, rect = new Rect(i * cellSize, 0, cellSize, cellSize),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(.5f,.5f),
                    spriteID = previous.TryGetValue(name, out GUID id) ? id : GUID.Generate()
                };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(r => new SpriteNameFileIdPair(r.name,r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        }
    }
}
