#if UNITY_EDITOR
using System;
using System.Linq;
using TinyKingdom.Economy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinyKingdom.EditorTools
{
    public static class Phase7ASetup
    {
        private const string AnimationPath = "Assets/UnitAnimations/RedPawn.asset";
        private const string ScenePath = "Assets/Scenes/Map.unity";
        private const string PawnFolder = "Assets/Units/Red Units/Pawn/";
        private static bool running;

        [MenuItem("Tools/Tiny Kingdom/Prepare Phase 7A Commander")]
        public static void Ensure()
        {
            if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Ensure;
                return;
            }
            running = true;
            try
            {
                PawnAnimationSet red = GetOrCreateAnimationSet();
                ConfigureAnimationSet(red);
                AssignScene(red);
                AssetDatabase.SaveAssets();
                Debug.Log("Phase 7A setup complete: Red Pawn animation data and Map reference prepared.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Phase 7A setup failed: " + exception);
            }
            finally { running = false; }
        }

        private static PawnAnimationSet GetOrCreateAnimationSet()
        {
            PawnAnimationSet set = AssetDatabase.LoadAssetAtPath<PawnAnimationSet>(AnimationPath);
            if (set != null) return set;
            set = ScriptableObject.CreateInstance<PawnAnimationSet>();
            set.name = "RedPawn";
            AssetDatabase.CreateAsset(set, AnimationPath);
            return set;
        }

        private static void ConfigureAnimationSet(PawnAnimationSet set)
        {
            var data = new SerializedObject(set);
            Put(data, "idle", Frames("Pawn_Idle.png"));
            Put(data, "run", Frames("Pawn_Run.png"));
            Put(data, "chop", Frames("Pawn_Interact Axe.png"));
            Put(data, "mine", Frames("Pawn_Interact Pickaxe.png"));
            Put(data, "knife", Frames("Pawn_Interact Knife.png"));
            Put(data, "idleAxe", Frames("Pawn_Idle Axe.png"));
            Put(data, "idlePickaxe", Frames("Pawn_Idle Pickaxe.png"));
            Put(data, "idleKnife", Frames("Pawn_Idle Knife.png"));
            Put(data, "idleWood", Frames("Pawn_Idle Wood.png"));
            Put(data, "idleGold", Frames("Pawn_Idle Gold.png"));
            Put(data, "idleFood", Frames("Pawn_Idle Meat.png"));
            Put(data, "runWood", Frames("Pawn_Run Wood.png"));
            Put(data, "runGold", Frames("Pawn_Run Gold.png"));
            Put(data, "runFood", Frames("Pawn_Run Meat.png"));
            Put(data, "runAxe", ToolRunFrames("Pawn_Run Axe.png"));
            Put(data, "runPickaxe", ToolRunFrames("Pawn_Run Pickaxe.png"));
            Put(data, "runKnife", ToolRunFrames("Pawn_Run Knife.png"));
            Put(data, "hammer", Frames("Pawn_Interact Hammer.png"));
            Put(data, "idleHammer", Frames("Pawn_Idle Hammer.png"));
            Put(data, "runHammer", Frames("Pawn_Run Hammer.png")
                .Where(sprite => !sprite.name.EndsWith("_5", StringComparison.Ordinal))
                .ToArray());
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
        }

        private static Sprite[] Frames(string file)
        {
            string path = PawnFolder + file;
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>().OrderBy(SpriteIndex).ToArray();
            if (sprites.Length == 0) throw new InvalidOperationException("Missing sprites: " + path);
            return sprites;
        }

        private static Sprite[] ToolRunFrames(string file)
        {
            Sprite[] sprites = Frames(file);
            if (sprites.Length != 6)
                throw new InvalidOperationException("Prepare the six complete Pawn tool-run frames first: " + file);
            return sprites;
        }

        private static int SpriteIndex(Sprite sprite)
        {
            int separator = sprite.name.LastIndexOf('_');
            return separator >= 0 && int.TryParse(sprite.name[(separator + 1)..], out int value)
                ? value : int.MaxValue;
        }

        private static void Put(SerializedObject target, string propertyName, Sprite[] sprites)
        {
            SerializedProperty property = target.FindProperty(propertyName)
                ?? throw new InvalidOperationException("Missing PawnAnimationSet field: " + propertyName);
            property.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        private static void AssignScene(PawnAnimationSet red)
        {
            Scene current = SceneManager.GetActiveScene();
            bool restore = current.IsValid() && current.path != ScenePath;
            string previous = restore ? current.path : null;
            Scene map = current.IsValid() && current.path == ScenePath
                ? current : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EconomyTestSetup setup = UnityEngine.Object.FindAnyObjectByType<EconomyTestSetup>();
            if (setup == null) throw new InvalidOperationException("Map has no EconomyTestSetup.");
            var serialized = new SerializedObject(setup);
            SerializedProperty property = serialized.FindProperty("redPawnAnimations")
                ?? throw new InvalidOperationException("EconomyTestSetup has no Red Pawn field.");
            if (property.objectReferenceValue != red)
            {
                property.objectReferenceValue = red;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(setup);
                EditorSceneManager.MarkSceneDirty(map);
                EditorSceneManager.SaveScene(map);
            }
            if (restore && !string.IsNullOrEmpty(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
    }
}
#endif
