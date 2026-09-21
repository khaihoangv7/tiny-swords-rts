using System;
using System.IO;
using System.Linq;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class Phase3UnitBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Units/BlueWarriorTest.prefab";
        private const string ScenePath = "Assets/Scenes/Map.unity";
        private const string IdleSheetPath = "Assets/Units/Blue Units/Warrior/Warrior_Idle.png";
        private const string IdleSpriteName = "Warrior_Idle_0";

        private static readonly Vector2Int[] SpawnCells =
        {
            new(5, 5),
            new(6, 5),
            new(5, 6)
        };

        static Phase3UnitBuilder()
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

            if (!File.Exists(PrefabPath))
                Build();
        }

        [MenuItem("Tools/Tiny Kingdom/Build Phase 3 Units")]
        public static void Build()
        {
            ConfigureIdleSheetImport();
            Sprite idleSprite = LoadIdleSprite();
            GameObject prefab = CreateOrReplacePrefab(idleSprite);

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject map = GameObject.Find("Map");
            GameObject cameraObject = GameObject.Find("Main Camera");
            if (map == null || cameraObject == null)
                throw new MissingReferenceException("Map scene requires Map and Main Camera objects.");

            Camera camera = cameraObject.GetComponent<Camera>();
            Transform navigation = map.transform.Find("Navigation");
            GridManager grid = navigation != null ? navigation.GetComponent<GridManager>() : null;
            PathfindingSystem pathfinding = navigation != null ? navigation.GetComponent<PathfindingSystem>() : null;
            if (camera == null || grid == null || pathfinding == null)
                throw new MissingReferenceException("Phase 3 requires the Phase 1 camera and Phase 2 navigation objects.");

            GameObject controls = GetOrCreateChild(map.transform, "PlayerControls");
            SelectionManager selection = GetOrAdd<SelectionManager>(controls);
            PlayerCommandController commands = GetOrAdd<PlayerCommandController>(controls);
            selection.Configure(camera, Faction.Blue);
            commands.Configure(camera, selection, grid);

            GameObject unitGroup = GetOrCreateChild(map.transform, "BlueTestUnits");
            ClearChildren(unitGroup.transform);
            for (int i = 0; i < SpawnCells.Length; i++)
            {
                Vector2Int cell = SpawnCells[i];
                GridCell gridCell = grid.GetCell(cell.x, cell.y);
                if (gridCell == null || !gridCell.Walkable || gridCell.HeightLevel != 0)
                    throw new InvalidOperationException($"Test spawn cell ({cell.x}, {cell.y}) is not flat walkable ground.");

                GameObject unit = (GameObject)PrefabUtility.InstantiatePrefab(prefab, unitGroup.transform);
                unit.name = $"BlueWarriorTest_{i + 1}";
                unit.transform.position = grid.GridToWorld(cell.x, cell.y);
                unit.GetComponent<UnitController>().Configure(Faction.Blue);
                unit.GetComponent<UnitMovement>().Configure(grid, pathfinding);
            }

            EditorUtility.SetDirty(controls);
            EditorUtility.SetDirty(unitGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Phase3Validation.Run(true);
            Debug.Log("Phase 3 unit selection and movement generated and validated.");
        }

        private static void ConfigureIdleSheetImport()
        {
            TextureImporter importer = AssetImporter.GetAtPath(IdleSheetPath) as TextureImporter;
            if (importer == null)
                throw new FileNotFoundException("Verified Warrior idle sheet was not found.", IdleSheetPath);

            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.filterMode != FilterMode.Point
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.mipmapEnabled;
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            if (changed)
                importer.SaveAndReimport();
        }

        private static Sprite LoadIdleSprite()
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(IdleSheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(asset => asset.name == IdleSpriteName);
            if (sprite == null)
                throw new MissingReferenceException($"Sprite '{IdleSpriteName}' was not found in {IdleSheetPath}.");
            return sprite;
        }

        private static GameObject CreateOrReplacePrefab(Sprite idleSprite)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && existing.GetComponent<CombatController>() != null)
                return existing;

            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Units");

            var source = new GameObject("BlueWarriorTest");
            try
            {
                SpriteRenderer renderer = source.AddComponent<SpriteRenderer>();
                renderer.sprite = idleSprite;
                renderer.sortingOrder = 10;
                source.AddComponent<UnitController>().Configure(Faction.Blue);
                source.AddComponent<UnitMovement>();
                source.AddComponent<UnitSelection>();
                return PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
