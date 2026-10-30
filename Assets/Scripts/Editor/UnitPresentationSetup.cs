using System;
using System.Linq;
using TinyKingdom.Input;
using TinyKingdom.UI;
using TinyKingdom.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyKingdom.EditorTools
{
    public static class UnitPresentationSetup
    {
        private const string ScenePath = "Assets/Scenes/Map.unity";
        private const string SetFolder = "Assets/UnitAnimations";

        [MenuItem("Tools/Tiny Kingdom/Setup Unit Presentation")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            if (!AssetDatabase.IsValidFolder(SetFolder))
                AssetDatabase.CreateFolder("Assets", "UnitAnimations");

            foreach (string faction in new[] { "Blue", "Red" })
            foreach (string type in new[] { "Warrior", "Archer", "Lancer" })
                CreateSet(faction, type);

            foreach (string faction in new[] { "Blue", "Red" })
            foreach (string type in new[] { "Warrior", "Archer", "Lancer" })
                ConfigurePrefab(faction, type);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (UnitController controller in UnityEngine.Object.FindObjectsByType<UnitController>())
                ConfigureUnit(controller.gameObject);

            SelectionManager selection = UnityEngine.Object.FindAnyObjectByType<SelectionManager>();
            if (selection == null) throw new InvalidOperationException("Map has no SelectionManager");
            GuardInputController guard = selection.GetComponent<GuardInputController>();
            if (guard == null) guard = selection.gameObject.AddComponent<GuardInputController>();
            guard.Configure(selection);

            SelectionSummaryUI summary = UnityEngine.Object.FindAnyObjectByType<SelectionSummaryUI>();
            if (summary == null)
            {
                var canvasObject = new GameObject("UnitSelectionCanvas", typeof(RectTransform),
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                summary = canvasObject.AddComponent<SelectionSummaryUI>();
            }
            Sprite shield = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/UI Elements/UI Elements/Icons/Icon_06.png");
            if (shield == null) throw new InvalidOperationException("Guard shield icon missing");
            summary.Configure(selection, guard, GetSet("Blue", "Warrior"),
                GetSet("Blue", "Archer"), GetSet("Blue", "Lancer"), shield);

            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            // These test units permit mixed-selection and friendly directional
            // Lancer attacks without replacing existing Phase 4B1 examples.
            AddTestUnitIfMissing(scene, "Blue", "Lancer", new Vector3(7.5f, 7.5f, 0f));
            AddTestUnitIfMissing(scene, "Red", "Archer", new Vector3(12.5f, 6.5f, 0f));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Unit presentation setup complete: six animation sets, prefabs, Map units, Canvas and EventSystem.");
        }

        private static UnitAnimationSet GetSet(string faction, string type)
            => AssetDatabase.LoadAssetAtPath<UnitAnimationSet>($"{SetFolder}/{faction}{type}.asset");

        private static void CreateSet(string faction, string type)
        {
            UnitAnimationSet set = GetSet(faction, type);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<UnitAnimationSet>();
                AssetDatabase.CreateAsset(set, $"{SetFolder}/{faction}{type}.asset");
            }
            var serialized = new SerializedObject(set);
            int cell = type == "Lancer" ? 320 : 192;
            serialized.FindProperty("frameCellSize").intValue = cell;
            serialized.FindProperty("framesPerSecond").floatValue = 10f;
            serialized.FindProperty("attackFramesPerSecond").floatValue = type == "Archer"
                ? 6.666667f : 10f;
            serialized.FindProperty("visualAnchorPixels").vector2Value = type switch
            {
                "Warrior" => new Vector2(101.5f, 99.5f),
                "Archer" => new Vector2(93f, 100f),
                _ => new Vector2(149.5f, 166.5f)
            };
            SetFrames(serialized, "idle", faction, type, "Idle", type switch
            {
                "Warrior" => 8, "Archer" => 6, _ => 12
            });
            SetFrames(serialized, "run", faction, type, "Run", type == "Archer" ? 4 : 6);
            if (type == "Warrior")
            {
                SetFrames(serialized, "attack", faction, type, "Attack1", 4);
                SetFrames(serialized, "attackAlternate", faction, type, "Attack2", 4);
                SetFrames(serialized, "guard", faction, type, "Guard", 6);
            }
            if (type == "Archer") SetFrames(serialized, "attack", faction, type, "Shoot", 8);
            if (type == "Lancer")
            {
                SetFrames(serialized, "attack", faction, type, "Right_Attack", 3);
                SetFrames(serialized, "attackDown", faction, type, "Down_Attack", 3);
                SetFrames(serialized, "attackDownRight", faction, type, "DownRight_Attack", 3);
                SetFrames(serialized, "attackUp", faction, type, "Up_Attack", 3);
                SetFrames(serialized, "attackUpRight", faction, type, "UpRight_Attack", 3);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
        }

        private static void SetFrames(SerializedObject data, string property, string faction,
            string type, string action, int count)
        {
            string stem = $"{type}_{action}";
            string path = $"Assets/Units/{faction} Units/{type}/{stem}.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length != count)
                throw new InvalidOperationException($"{path}: expected {count} sprites, got {sprites.Length}");
            SerializedProperty frames = data.FindProperty(property);
            frames.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = sprites.SingleOrDefault(item => item.name == $"{stem}_{i}");
                if (sprite == null) throw new InvalidOperationException($"Missing {stem}_{i} in {path}");
                frames.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
            }
        }

        private static void ConfigurePrefab(string faction, string type)
        {
            string path = $"Assets/Prefabs/Units/{faction}{type}Test.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureUnit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureUnit(GameObject root)
        {
            HealthComponent health = root.GetComponent<HealthComponent>();
            if (health == null || health.CombatData == null) return;
            string faction = root.GetComponent<UnitController>().Faction.ToString();
            string type = health.CombatData.UnitType.ToString();
            UnitAnimator animator = root.GetComponent<UnitAnimator>();
            if (animator == null) animator = root.AddComponent<UnitAnimator>();
            animator.Configure(GetSet(faction, type));
            if (root.GetComponent<DamageTextEmitter>() == null)
                root.AddComponent<DamageTextEmitter>();
            if (type == "Warrior" && root.GetComponent<UnitGuard>() == null)
                root.AddComponent<UnitGuard>();
        }

        private static void AddTestUnitIfMissing(Scene scene, string faction, string type,
            Vector3 location)
        {
            string name = $"{faction}{type}Test_1";
            if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Any(transform => transform.name == name)) return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/Units/{faction}{type}Test.prefab");
            if (prefab == null) throw new InvalidOperationException($"Missing prefab for {name}");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            instance.transform.position = location;
            Transform group = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(transform => transform.name == $"{faction}TestUnits");
            instance.transform.SetParent(group, true);
        }
    }
}
