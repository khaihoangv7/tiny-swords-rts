using System.Collections.Generic;
using System.IO;
using TinyKingdom.Navigation;
using TinyKingdom.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class Phase2NavigationBuilder
    {
        public const string DataPath = "Assets/Data/Navigation/MapNavigation.asset";
        private const string ScenePath = "Assets/Scenes/Map.unity";
        private const int Width = BattlefieldLayout.Width;
        private const int Height = BattlefieldLayout.Height;

        static Phase2NavigationBuilder()
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

            if (!File.Exists(DataPath))
                Build();
        }

        [MenuItem("Tools/Tiny Kingdom/Build Phase 2 Navigation")]
        public static void Build()
        {
            NavigationGridData data = RebuildDataAsset();
            AssetDatabase.SaveAssets();

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject map = GameObject.Find("Map");
            if (map == null)
                throw new MissingReferenceException("Map scene must contain a root object named 'Map'.");

            Transform navigationTransform = map.transform.Find("Navigation");
            GameObject navigation = navigationTransform != null
                ? navigationTransform.gameObject
                : new GameObject("Navigation");
            navigation.transform.SetParent(map.transform, false);

            GridManager gridManager = GetOrAdd<GridManager>(navigation);
            PathfindingSystem pathfinding = GetOrAdd<PathfindingSystem>(navigation);
            PathfindingDebug debug = GetOrAdd<PathfindingDebug>(navigation);
            gridManager.SetNavigationData(data);
            pathfinding.SetGridManager(gridManager);
            debug.Configure(gridManager, pathfinding);

            EditorUtility.SetDirty(gridManager);
            EditorUtility.SetDirty(pathfinding);
            EditorUtility.SetDirty(debug);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Phase2NavigationValidation.Run(data, true);
            Debug.Log("Phase 2 navigation generated and validated.");
        }

        public static NavigationGridData RebuildDataAsset()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Navigation");

            NavigationGridData data = AssetDatabase.LoadAssetAtPath<NavigationGridData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<NavigationGridData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }

            data.Configure(Width, Height, Vector2.zero, 1f, CreateMapCells());
            EditorUtility.SetDirty(data);
            return data;
        }

        public static List<NavigationCellData> CreateMapCells()
        {
            var result = new List<NavigationCellData>(Width * Height);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                result.Add(BattlefieldTerraces.Cell(x, y));
            }

            return result;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
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
