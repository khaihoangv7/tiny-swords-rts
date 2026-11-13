#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Terrain;
using TinyKingdom.UI;
using TinyKingdom.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TinyKingdom.EditorTools
{
    public static class MainMenuSetup
    {
        private const string Buttons = "Assets/UI Elements/UI Elements/Buttons/";
        private const string Previews = "Assets/UI Elements/MainMenu/Previews";

        [MenuItem("Tools/Tiny Kingdom/Prepare Main Menu")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Prepare the main menu outside Play Mode.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene changes before capturing menu previews.");
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(Previews);
                AssetDatabase.Refresh();
                string[] scenes = { "Map", "LinearLane", "GoblinIsland" };
                // Scene loading/reimport can unload asset instances. Capture first,
                // then acquire fresh references and commit the catalog once.
                for (int i = 0; i < scenes.Length; i++)
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/" + scenes[i] + ".unity");
                    Capture(scenes[i], new Vector3(i == 2 ? 33f : 30f, 22f, -10f), 22f, 960, 540);
                    if (i == 0) BuildBackdrop();
                }
                Sprite[] blue = Load("BigBlueButton_Regular", 9);
                Sprite[] blueDown = Load("BigBlueButton_Pressed", 9);
                Sprite[] red = Load("BigRedButton_Regular", 9);
                Sprite[] redDown = Load("BigRedButton_Pressed", 9);
                Sprite back = Load("SmallBlueSquareButton_Regular", 1)[0];
                Sprite backDown = Load("SmallBlueSquareButton_Pressed", 1)[0];
                Sprite close = Load("SmallRedSquareButton_Regular", 1)[0];
                Sprite closeDown = Load("SmallRedSquareButton_Pressed", 1)[0];
                MainMenuAssetCatalog art = AssetDatabase.LoadAssetAtPath<MainMenuAssetCatalog>(
                    "Assets/Resources/Presentation/MainMenuAssets.asset");
                if (art == null)
                {
                    art = ScriptableObject.CreateInstance<MainMenuAssetCatalog>();
                    AssetDatabase.CreateAsset(art, "Assets/Resources/Presentation/MainMenuAssets.asset");
                }
                art.blueRegular = blue;
                art.bluePressed = blueDown;
                art.redRegular = red;
                art.redPressed = redDown;
                art.backRegular = back;
                art.backPressed = backDown;
                art.closeRegular = close;
                art.closePressed = closeDown;
                art.mapPreviews = scenes.Select(name => AssetDatabase.LoadAssetAtPath<Sprite>(Previews + "/" + name + ".png")).ToArray();
                art.backdropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MainMenuBackdrop.prefab");
                art.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Grenze-Bold.ttf");
                art.menuFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Grenze-Semibold.ttf");
                art.defaultCursor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI Elements/UI Elements/Cursors/Cursor_01.png");
                art.backdropTerrain = AssetDatabase.LoadAssetAtPath<NavigationGridData>("Assets/Data/Navigation/MapNavigation.asset");
                art.idleGuards = new[] { "Warrior", "Archer", "Lancer" }
                    .Select(name => AssetDatabase.LoadAssetAtPath<UnitAnimationSet>("Assets/UnitAnimations/Blue" + name + ".asset"))
                    .ToArray();
                AssignVillageAssets(art);
                EditorUtility.SetDirty(art);
                const string difficultyPath = "Assets/Resources/MatchDifficulty.asset";
                if (AssetDatabase.LoadAssetAtPath<MatchDifficultyCatalog>(difficultyPath) == null)
                    AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MatchDifficultyCatalog>(), difficultyPath);
                AssetDatabase.SaveAssets();
                Debug.Log("MAIN_MENU_SETUP_PASSED: live backdrop, embedded serif fonts, map previews and difficulty profiles prepared.");
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        [MenuItem("Tools/Tiny Kingdom/Refresh Menu Village Art")]
        public static void RefreshVillageArt()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before refreshing menu art.");
            MainMenuAssetCatalog art = MainMenuAssetCatalog.Load();
            AssignVillageAssets(art);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log("MENU_VILLAGE_ART_READY: logo, seven buildings, native river tiles and verified ambient loops.");
        }

        private static Sprite[] Sprites(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>().OrderBy(sprite =>
            {
                string suffix = sprite.name[(sprite.name.LastIndexOf('_') + 1)..];
                return int.TryParse(suffix, out int index) ? index : 0;
            }).ToArray();

        private static void AssignVillageAssets(MainMenuAssetCatalog art)
        {
            art.logo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Menu/TinySwordsLogo.png");
            art.villageBuildings = new[] { "House1", "House2", "House3", "Barracks", "Archery", "Monastery", "Tower" }
                .Select(name => Sprites("Assets/Buildings/Blue Buildings/" + name + ".png")[0]).ToArray();
            art.villageTreeFrames = Enumerable.Range(1, 4).SelectMany(i =>
                Sprites("Assets/Terrain/Resources/Wood/Trees/Tree" + i + ".png")).ToArray();
            art.villageBushFrames = Enumerable.Range(1, 4).SelectMany(i =>
                Sprites("Assets/Terrain/Decorations/Bushes/Bushe" + i + ".png")).ToArray();
            art.villageDuckFrames = Sprites("Assets/Terrain/Decorations/Rubber Duck/Rubber duck.png");
            art.villageWaterFrames = Sprites("Assets/Terrain/Decorations/Rocks in the Water/Water Rocks_02.png");
            Sprite[] bridges = Sprites("Assets/Terrain/GoblinIsland/Bridge.png");
            art.villageBridges = new[] { bridges.First(s => s.name == "Bridge_Horizontal"), bridges.First(s => s.name == "Bridge_Vertical") };
            art.villageShore = Enumerable.Range(0, 9).Select(i => AssetDatabase.LoadAssetAtPath<TileBase>(
                $"Assets/Tiles/Terrain/Color1/Tilemap_color1_r{i / 3}c{i % 3}.asset")).ToArray();
            art.villageMeadow = Enumerable.Range(0, 9).Select(i => AssetDatabase.LoadAssetAtPath<TileBase>(
                $"Assets/Tiles/Terrain/Color3/Tilemap_color3_r{i / 3}c{i % 3}.asset")).ToArray();
            art.villageFoam = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Water/AnimatedFoam.asset");
        }

        private static GameObject BuildBackdrop()
        {
            Transform map = GameObject.Find("Map")?.transform;
            if (map == null) throw new InvalidOperationException("The authored Map visual root is missing.");
            var root = new GameObject("MainMenuBackdrop");
            root.SetActive(false);
            try
            {
                foreach (string name in new[] { "Grid", "BattlefieldScenery", "Battlefield_EditorPreview" })
                {
                    Transform source = map.Find(name);
                    if (source == null) throw new InvalidOperationException("Missing backdrop visuals: " + name);
                    GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, root.transform);
                    copy.name = name;
                }
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null) continue;
                    child.gameObject.tag = "Untagged";
                    if (child.name is "Blue_Pawn" or "Red_Pawn")
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                        continue;
                    }
                    foreach (Component component in child.GetComponents<Component>())
                    {
                        if (component is Transform or Grid or Tilemap or TilemapRenderer or SpriteRenderer
                            or AmbientCloudDrift or AmbientSpriteLoop) continue;
                        if (component != null) UnityEngine.Object.DestroyImmediate(component);
                    }
                }
                root.SetActive(true);
                const string path = "Assets/Prefabs/UI/MainMenuBackdrop.prefab";
                Directory.CreateDirectory("Assets/Prefabs/UI");
                AssetDatabase.Refresh();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Sprite[] Load(string name, int count)
        {
            string path = Buttons + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing menu artwork: " + path);
            bool changed = importer.filterMode != FilterMode.Point || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (changed) importer.SaveAndReimport();
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite => int.Parse(sprite.name[(sprite.name.LastIndexOf('_') + 1)..])).ToArray();
            if (sprites.Length != count) throw new InvalidOperationException("Unexpected menu slicing: " + path);
            return sprites;
        }

        private static Sprite Capture(string name, Vector3 position, float zoom, int width, int height)
        {
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Battlefield camera is missing.");
            Vector3 oldPosition = camera.transform.position;
            float oldZoom = camera.orthographicSize;
            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24);
            var output = new Texture2D(width, height, TextureFormat.RGB24, false);
            string path = Previews + "/" + name + ".png";
            try
            {
                camera.transform.position = position;
                camera.orthographicSize = zoom;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                output.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                output.Apply();
                File.WriteAllBytes(path, output.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.transform.position = oldPosition;
                camera.orthographicSize = oldZoom;
                RenderTexture.active = oldActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(output);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
