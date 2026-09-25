using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyKingdom.Navigation;
using TinyKingdom.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;
using UnityEngine.SceneManagement;

// Creates the first visual-only map using Unity's own asset and scene serializers.
[InitializeOnLoad]
public static class Phase1MapBuilder
{
    private const string SourceFolder = "Assets/Terrain/Tileset";
    private const string TileFolder = "Assets/Tiles";
    private const string ScenePath = "Assets/Scenes/Map.unity";
    private const int Width = 40;
    private const int Height = 28;

    static Phase1MapBuilder()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    private static void BuildIfNeeded()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildIfNeeded;
            return;
        }

        if (!File.Exists(ScenePath))
            Build();
    }

    [MenuItem("Tools/Tiny Kingdom/Build Phase 1 Map")]
    public static void Build()
    {
        EnsureFolder(TileFolder);
        EnsureFolder(TileFolder + "/Terrain");
        EnsureFolder(TileFolder + "/Palettes");

        var tiles = new Dictionary<string, Tile>();
        for (int color = 1; color <= 5; color++)
        {
            string stem = "Tilemap_color" + color;
            string path = SourceFolder + "/" + stem + ".png";
            ConfigureTexture(path, MakeTerrainRects(stem), 4096);
            CreateTiles(path, "Terrain/Color" + color, tiles);
        }

        ConfigureTexture(SourceFolder + "/Water Background color.png", null, 4096);
        CreateTiles(SourceFolder + "/Water Background color.png", "Water", tiles);

        // This source is 16 animation frames of 192x192, rather than a 64x64 terrain grid.
        ConfigureTexture(SourceFolder + "/Water Foam.png", MakeFoamRects(), 4096);
        CreateTiles(SourceFolder + "/Water Foam.png", "Water", tiles, true);

        // Shadow is a single 192x192 composite visual.
        ConfigureTexture(SourceFolder + "/Shadow.png", null, 4096);
        CreateTiles(SourceFolder + "/Shadow.png", "Shadows", tiles);

        AnimatedFoamTile animatedFoam = CreateAnimatedFoamTile();
        CreatePalette(tiles);
        CreateScene(tiles, animatedFoam);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Phase 1 map generated: " + ScenePath);
    }

    // Kept as an explicit menu entry for repairing an open scene. Validation
    // synchronizes only the two visual effect Tilemaps when they differ.
    [MenuItem("Tools/Tiny Kingdom/Repair Terrain Visuals")]
    public static void RepairTerrainVisuals()
    {
        TinyKingdom.EditorTools.TerrainVisualValidation.Run();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static List<SpriteRect> MakeTerrainRects(string stem)
    {
        var result = new List<SpriteRect>();
        for (int row = 0; row < 6; row++)
        for (int column = 0; column < 9; column++)
        {
            // Column 4 is a transparent gutter. The lower-left half has only corner art.
            if (column == 4 || (row >= 4 && (column == 1 || column == 2))) continue;
            result.Add(new SpriteRect
            {
                name = stem + "_r" + row + "c" + column,
                rect = new Rect(column * 64, (5 - row) * 64, 64, 64),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate()
            });
        }
        return result;
    }

    private static List<SpriteRect> MakeFoamRects()
    {
        var result = new List<SpriteRect>();
        for (int frame = 0; frame < 16; frame++)
            result.Add(new SpriteRect
            {
                name = "WaterFoam_Frame" + frame.ToString("00"),
                rect = new Rect(frame * 192, 0, 192, 192),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate()
            });
        return result;
    }

    private static void ConfigureTexture(string path, List<SpriteRect> newRects, int maxSize)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Missing terrain texture: " + path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = newRects == null ? SpriteImportMode.Single : SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 64;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = maxSize;
        importer.SaveAndReimport();

        if (newRects == null) return;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var oldIds = provider.GetSpriteRects().ToDictionary(rect => rect.name, rect => rect.spriteID);
        foreach (var rect in newRects)
            if (oldIds.TryGetValue(rect.name, out GUID oldId)) rect.spriteID = oldId;

        provider.SetSpriteRects(newRects.ToArray());
        var idProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        idProvider.SetNameFileIdPairs(newRects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }

    private static void CreateTiles(string source, string subfolder, Dictionary<string, Tile> tiles, bool onlyFirst = false)
    {
        string folder = TileFolder + "/" + subfolder;
        EnsureFolder(folder);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(source).OfType<Sprite>().OrderBy(sprite => sprite.name);
        foreach (var sprite in sprites)
        {
            if (onlyFirst && sprite.name != "WaterFoam_Frame00") continue;
            string path = folder + "/" + sprite.name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            tiles[sprite.name] = tile;
        }
    }

    private static AnimatedFoamTile CreateAnimatedFoamTile()
    {
        const string path = TileFolder + "/Water/AnimatedFoam.asset";
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SourceFolder + "/Water Foam.png")
            .OfType<Sprite>()
            .Where(sprite => sprite.name.StartsWith("WaterFoam_Frame", StringComparison.Ordinal))
            .OrderBy(sprite => sprite.name)
            .ToArray();
        if (frames.Length != 16)
            throw new InvalidOperationException("Water Foam must contain the 16 inspected 192x192 frames.");

        AnimatedFoamTile foam = AssetDatabase.LoadAssetAtPath<AnimatedFoamTile>(path);
        if (foam == null)
        {
            foam = ScriptableObject.CreateInstance<AnimatedFoamTile>();
            AssetDatabase.CreateAsset(foam, path);
        }
        foam.Configure(frames, 10f);
        EditorUtility.SetDirty(foam);
        return foam;
    }

    private static void CreatePalette(Dictionary<string, Tile> tiles)
    {
        const string path = TileFolder + "/Palettes/Tiny Swords Terrain.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            GridPaletteUtility.CreateNewPalette(TileFolder + "/Palettes", "Tiny Swords Terrain",
                GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Manual,
                Vector3.one, GridLayout.CellSwizzle.XYZ);

        var contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var paletteMap = contents.GetComponentInChildren<Tilemap>();
            paletteMap.ClearAllTiles();
            for (int color = 1; color <= 5; color++)
            for (int row = 0; row < 6; row++)
            for (int column = 0; column < 9; column++)
            {
                string key = "Tilemap_color" + color + "_r" + row + "c" + column;
                if (tiles.TryGetValue(key, out Tile tile))
                    paletteMap.SetTile(new Vector3Int((color - 1) * 10 + column, 5 - row, 0), tile);
            }
            paletteMap.SetTile(new Vector3Int(0, -2, 0), tiles["Water Background color"]);
            paletteMap.SetTile(new Vector3Int(2, -2, 0), tiles["WaterFoam_Frame00"]);
            paletteMap.SetTile(new Vector3Int(5, -2, 0), tiles["Shadow"]);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static void CreateScene(Dictionary<string, Tile> tiles, AnimatedFoamTile animatedFoam)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var map = GameObject.Find("Map") ?? new GameObject("Map");
        Transform existingGrid = map.transform.Find("Grid");
        var gridObject = existingGrid != null ? existingGrid.gameObject : new GameObject("Grid", typeof(Grid));
        gridObject.transform.SetParent(map.transform, false);
        var bg = AddLayer(gridObject, "BG_Color", 0);
        var foam = AddLayer(gridObject, "WaterFoam", 1);
        var flat = AddLayer(gridObject, "FlatGround", 2);
        var shadow = AddLayer(gridObject, "Shadow_L1", 3);
        var elevated = AddLayer(gridObject, "ElevatedGround_L1", 4);
        AddLayer(gridObject, "Decorations", 5);

        bg.ClearAllTiles();
        foam.ClearAllTiles();
        flat.ClearAllTiles();
        shadow.ClearAllTiles();
        elevated.ClearAllTiles();

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            bg.SetTile(new Vector3Int(x, y, 0), tiles["Water Background color"]);

        // Mirrored clear base areas remain on the large flat island.
        PaintPlatform(flat, tiles, "Tilemap_color1", 2, 2, 36, 24, false);
        PaintShorelineFoam(foam, animatedFoam, 2, 2, 36, 24);

        // One central raised plateau. The two bottom rows are its cliff face.
        PaintPlatform(elevated, tiles, "Tilemap_color2", 16, 13, 8, 7, true);
        PaintOffsetShadow(shadow, tiles["Shadow"], 16, 13, 8, 7);
        // The cliff face occupies y=12 and y=11. A contact row at y=11 lets
        // the 80px shadow artwork extend 8px below the visible cliff foot.
        for (int x = 16; x <= 23; x++)
            if (!StrategicObjectiveMapLayout.IsConnectorColumn(x))
                shadow.SetTile(new Vector3Int(x, 11, 0), tiles["Shadow"]);
        for (int x = 16; x <= 23; x++)
        {
            if (StrategicObjectiveMapLayout.TryGetConnector(x,
                out TerrainHeightConnectorDefinition connector))
            {
                elevated.SetTile(new Vector3Int(x, connector.UpperY, 0),
                    tiles[connector.UpperSpriteName]);
                elevated.SetTile(new Vector3Int(x, connector.LowerY, 0),
                    tiles[connector.LowerSpriteName]);
                continue;
            }

            int column = x == 16 ? 5 : x == 23 ? 7 : 6;
            elevated.SetTile(new Vector3Int(x, 12, 0), tiles["Tilemap_color2_r4c" + column]);
            elevated.SetTile(new Vector3Int(x, 11, 0), tiles["Tilemap_color2_r5c" + column]);
        }

        if (GameObject.Find("Main Camera") == null)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(MapCameraController));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(20f, 14f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.29f, 0.62f, 0.63f);
        }

        if (GameObject.Find("Global Light 2D") == null)
        {
            var lightObject = new GameObject("Global Light 2D", typeof(Light2D));
            lightObject.GetComponent<Light2D>().lightType = Light2D.LightType.Global;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static Tilemap AddLayer(GameObject parent, string name, int sortingOrder)
    {
        Transform existing = parent.transform.Find(name);
        var layer = existing != null ? existing.gameObject : new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        layer.transform.SetParent(parent.transform, false);
        layer.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
        return layer.GetComponent<Tilemap>();
    }

    private static void PaintShorelineFoam(Tilemap layer, AnimatedFoamTile tile,
        int left, int bottom, int width, int height)
    {
        for (int y = bottom; y < bottom + height; y++)
        for (int x = left; x < left + width; x++)
            if (x == left || x == left + width - 1 || y == bottom || y == bottom + height - 1)
                layer.SetTile(new Vector3Int(x, y, 0), tile);
    }

    private static void PaintOffsetShadow(Tilemap layer, Tile tile,
        int left, int bottom, int width, int height)
    {
        for (int y = bottom; y < bottom + height; y++)
        for (int x = left; x < left + width; x++)
            layer.SetTile(new Vector3Int(x, y - 1, 0), tile);
    }

    private static void PaintPlatform(Tilemap layer, Dictionary<string, Tile> tiles, string stem,
        int left, int bottom, int width, int height, bool secondGroup)
    {
        for (int y = bottom; y < bottom + height; y++)
        for (int x = left; x < left + width; x++)
        {
            int row = y == bottom + height - 1 ? 0 : y == bottom ? 2 : 1;
            int column = x == left ? 0 : x == left + width - 1 ? 2 : 1;
            if (secondGroup) column += 5;
            layer.SetTile(new Vector3Int(x, y, 0), tiles[stem + "_r" + row + "c" + column]);
        }
    }
}
