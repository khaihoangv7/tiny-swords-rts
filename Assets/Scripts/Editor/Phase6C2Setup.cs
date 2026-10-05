#if UNITY_EDITOR
#pragma warning disable 0618
using System;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TinyKingdom.EditorTools
{
    [InitializeOnLoad]
    public static class Phase6C2Setup
    {
        private const string CatalogPath = "Assets/Resources/Presentation/Phase6C2Assets.asset";
        private const string SetFolder = "Assets/UnitAnimations";
        private const string DataFolder = "Assets/Data";
        private const string PrefabFolder = "Assets/Prefabs/Units";
        private const string ScenePath = "Assets/Scenes/Map.unity";
        private static bool running;

        static Phase6C2Setup() => EditorApplication.delayCall += Ensure;

        [MenuItem("Tools/Tiny Kingdom/Prepare Phase 6C.2 Strategic Objective")]
        public static void Ensure()
        {
            if (running) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            { EditorApplication.delayCall += Ensure; return; }
            running = true;
            try
            {
                ConfigureImports();
                Phase2NavigationBuilder.RebuildDataAsset();
                UnitAnimationSet blueMonk = MakeMonkSet("Blue");
                UnitAnimationSet redMonk = MakeMonkSet("Red");
                UnitCombatData monk = MakeData("MonkCombatData", UnitType.Monk,
                    80, 1, 2f, 2f, 3f, 60, 0, 60, false, 4f, 7f, 4f);
                GameObject monkPrefab = MakePrefab("BlueMonkTest", "BlueWarriorTest",
                    Faction.Blue, monk, blueMonk, UnitType.Monk);
                MakePrefab("RedMonkTest", "RedWarriorTest",
                    Faction.Red, monk, redMonk, UnitType.Monk);
                EnsureCatalog(blueMonk, redMonk);
                AssignScene(monk, monkPrefab);
                SetCastleHP();
                AssetDatabase.SaveAssets();
                Debug.Log("Phase 6C.2 setup complete: verified assets, data, prefabs and Map references prepared.");
            }
            catch (Exception exception) { Debug.LogError("Phase 6C.2 setup failed: " + exception); }
            finally { running = false; }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.delayCall += Ensure;
        }

        private const string TorchPath = "Assets/Units/Goblins Units/Goblins_Torch/Torch_Yellow.png";
        private const string TntPath = "Assets/Units/Goblins Units/Goblins_TNT/TNT_Red.png";
        private const string DynamitePath = "Assets/Units/Goblins Units/Goblins_TNT/Dynamite.png";
        private const string ExplosionPath = "Assets/Particle FX/Explosion_03.png";
        private const string MineFolder = "Assets/Terrain/Resources/Gold/Gold Mine/";

        private static void ConfigureImports()
        {
            foreach (string faction in new[] { "Blue", "Red" })
            {
                Slice($"Assets/Units/{faction} Units/Monk/Idle.png", 192, 192, 6);
                Slice($"Assets/Units/{faction} Units/Monk/Run.png", 192, 192, 4);
                Slice($"Assets/Units/{faction} Units/Monk/Heal.png", 192, 192, 11);
                Slice($"Assets/Units/{faction} Units/Monk/Heal_Effect.png", 192, 192, 11);
            }
            Single(MineFolder + "GoldMine_Inactive.png");
            Single(MineFolder + "GoldMine_Active.png");
            Single(MineFolder + "GoldMine_Destroyed.png");
        }

        private static void Pixel(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            importer.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(width, height)), 32, 8192);
        }

        private static void Slice(string path, int cellWidth, int cellHeight, int count)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException("Missing texture: " + path);
            Pixel(importer); importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            int columns = width / cellWidth;
            string stem = System.IO.Path.GetFileNameWithoutExtension(path);
            var slices = new SpriteMetaData[count];
            for (int i = 0; i < count; i++)
            {
                int rowFromTop = i / columns;
                slices[i] = new SpriteMetaData
                {
                    name = stem + "_" + i,
                    rect = new Rect((i % columns) * cellWidth,
                        height - (rowFromTop + 1) * cellHeight, cellWidth, cellHeight),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
            }
            importer.spritesheet = slices;
            importer.SaveAndReimport();
        }

        private static void Single(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException("Missing texture: " + path);
            Pixel(importer); importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        private static UnitAnimationSet MakeMonkSet(string faction)
        {
            UnitAnimationSet set = GetOrCreateSet(faction + "Monk");
            SerializedObject data = new(set);
            CommonSet(data, new Vector2(96f, 96f));
            Put(data, "idle", Frames($"Assets/Units/{faction} Units/Monk/Idle.png"));
            Put(data, "run", Frames($"Assets/Units/{faction} Units/Monk/Run.png"));
            Put(data, "attack", Frames($"Assets/Units/{faction} Units/Monk/Heal.png"));
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(set); return set;
        }

        private static UnitAnimationSet MakeGoblinSet(string name, string path,
            int[] idle, int[] run, int[] attack, int[] attackDown = null,
            int[] attackUp = null)
        {
            UnitAnimationSet set = GetOrCreateSet(name);
            SerializedObject data = new(set); CommonSet(data, new Vector2(96f, 96f));
            Sprite[] all = Frames(path);
            Sprite[] idleFrames = SelectFrames(all, idle, name + " Idle");
            Sprite[] runFrames = SelectFrames(all, run, name + " Run");
            Sprite[] attackFrames = SelectFrames(all, attack, name + " Attack");
            Sprite[] downFrames = attackDown != null
                ? SelectFrames(all, attackDown, name + " Attack Down")
                : Array.Empty<Sprite>();
            Sprite[] upFrames = attackUp != null
                ? SelectFrames(all, attackUp, name + " Attack Up")
                : Array.Empty<Sprite>();
            Put(data, "idle", idleFrames);
            Put(data, "run", runFrames);
            Put(data, "attack", attackFrames);
            Put(data, "guard", Array.Empty<Sprite>());
            Put(data, "attackDown", downFrames);
            Put(data, "attackDownRight", downFrames);
            Put(data, "attackUp", upFrames);
            Put(data, "attackUpRight", upFrames);
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(set); return set;
        }

        private static Sprite[] SelectFrames(Sprite[] source, int[] indices, string label)
        {
            var result = new Sprite[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                if (index < 0 || index >= source.Length || source[index] == null)
                    throw new InvalidOperationException(
                        $"{label} requires valid sprite index {index}; imported count is {source.Length}.");
                result[i] = source[index];
            }
            return result;
        }

        private static void CommonSet(SerializedObject data, Vector2 anchor)
        {
            data.FindProperty("framesPerSecond").floatValue = 10f;
            data.FindProperty("attackFramesPerSecond").floatValue = 10f;
            data.FindProperty("frameCellSize").intValue = 192;
            data.FindProperty("visualAnchorPixels").vector2Value = anchor;
        }

        private static UnitAnimationSet GetOrCreateSet(string name)
        {
            string path = $"{SetFolder}/{name}.asset";
            UnitAnimationSet set = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(path);
            if (set != null) return set;
            set = ScriptableObject.CreateInstance<UnitAnimationSet>();
            AssetDatabase.CreateAsset(set, path); return set;
        }

        private static UnitCombatData MakeData(string name, UnitType type, int hp, int damage,
            float range, float interval, float speed, int food, int wood, int gold,
            bool autoAcquire, float acquisition, float training, float vision)
        {
            string path = $"{DataFolder}/{name}.asset";
            UnitCombatData data = AssetDatabase.LoadAssetAtPath<UnitCombatData>(path);
            if (data == null) { data = ScriptableObject.CreateInstance<UnitCombatData>(); AssetDatabase.CreateAsset(data, path); }
            SerializedObject so = new(data);
            so.FindProperty("unitType").enumValueIndex = (int)type;
            so.FindProperty("attackDelivery").enumValueIndex = (int)(type == UnitType.GoblinTNT ? AttackDelivery.Projectile : AttackDelivery.Melee);
            so.FindProperty("maxHP").intValue = hp; so.FindProperty("baseDamage").intValue = damage;
            so.FindProperty("attackRange").floatValue = range; so.FindProperty("attackInterval").floatValue = interval;
            so.FindProperty("moveSpeed").floatValue = speed; so.FindProperty("foodCost").intValue = food;
            so.FindProperty("woodCost").intValue = wood; so.FindProperty("goldCost").intValue = gold;
            so.FindProperty("acquisitionRadius").floatValue = acquisition;
            so.FindProperty("autoChaseLimit").floatValue = 7f;
            so.FindProperty("autoAcquireTargets").boolValue = autoAcquire;
            so.FindProperty("canRetaliateWhenDamaged").boolValue = false;
            so.FindProperty("populationCost").intValue = type == UnitType.Monk ? 1 : 0;
            so.FindProperty("trainingSeconds").floatValue = training;
            so.FindProperty("visionRadius").floatValue = vision;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(data); return data;
        }

        private static GameObject MakePrefab(string name, string sourceName, Faction faction,
            UnitCombatData combatData, UnitAnimationSet animations, UnitType type)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{sourceName}.prefab");
                GameObject sourceRoot = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
                try { PrefabUtility.SaveAsPrefabAsset(sourceRoot, path); }
                finally { PrefabUtility.UnloadPrefabContents(sourceRoot); }
            }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
                root.name = name;
                root.GetComponent<UnitController>().Configure(faction);
                root.GetComponent<HealthComponent>().Configure(combatData);
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = animations.Idle.FirstOrDefault(); renderer.enabled = true;
                UnitAnimator animator = root.GetComponent<UnitAnimator>() ?? root.AddComponent<UnitAnimator>();
                animator.Configure(animations);
                UnitGuard guard = root.GetComponent<UnitGuard>(); if (guard != null) UnityEngine.Object.DestroyImmediate(guard, true);
                CombatController combat = root.GetComponent<CombatController>();
                if (type == UnitType.GoblinTorch)
                {
                    if (combat == null) combat = root.AddComponent<CombatController>();
                    combat.Configure(combatData);
                    if (root.GetComponent<DelayedRegeneration>() == null) root.AddComponent<DelayedRegeneration>();
                }
                else
                {
                    if (combat != null) UnityEngine.Object.DestroyImmediate(combat, true);
                    if (type == UnitType.Monk && root.GetComponent<MonkHealer>() == null) root.AddComponent<MonkHealer>();
                    if (type == UnitType.GoblinTNT && root.GetComponent<GoblinTntCombat>() == null) root.AddComponent<GoblinTntCombat>();
                }
                if (type is UnitType.GoblinTorch or UnitType.GoblinTNT)
                {
                    FogPresentationGate gate = root.GetComponent<FogPresentationGate>()
                        ?? root.AddComponent<FogPresentationGate>();
                    gate.ConfigureInitialVisibility(true);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void EnsureCatalog(UnitAnimationSet blue, UnitAnimationSet red)
        {
            Phase6C2AssetCatalog catalog = AssetDatabase.LoadAssetAtPath<Phase6C2AssetCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<Phase6C2AssetCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.SetNonGoblin(blue, red,
                Frames("Assets/Units/Blue Units/Monk/Heal_Effect.png"),
                Frames("Assets/Units/Red Units/Monk/Heal_Effect.png"),
                First(MineFolder + "GoldMine_Inactive.png"), First(MineFolder + "GoldMine_Active.png"),
                First(MineFolder + "GoldMine_Destroyed.png"));
            EditorUtility.SetDirty(catalog);
        }

        private static void AssignScene(UnitCombatData monk, GameObject monkPrefab)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EconomyTestSetup setup = UnityEngine.Object.FindAnyObjectByType<EconomyTestSetup>();
            if (setup == null) throw new InvalidOperationException("Map has no EconomyTestSetup.");
            SerializedObject so = new(setup);
            so.FindProperty("monkCombatData").objectReferenceValue = monk;
            so.FindProperty("blueMonkPrefab").objectReferenceValue = monkPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EnsureSceneStairs();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureSceneStairs()
        {
            GameObject map = GameObject.Find("Map");
            Transform grid = map != null ? map.transform.Find("Grid") : null;
            Tilemap elevated = grid != null
                ? grid.Find("ElevatedGround_L1")?.GetComponent<Tilemap>() : null;
            Tilemap shadow = grid != null
                ? grid.Find("Shadow_L1")?.GetComponent<Tilemap>() : null;
            if (elevated == null || shadow == null)
                throw new InvalidOperationException("Map terrain Tilemaps are missing.");

            BattlefieldTerrainPainter.Paint(grid);
        }

        private static void SetTerrainTile(Tilemap tilemap, int x, int y, string spriteName)
        {
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(
                $"Assets/Tiles/Terrain/Color2/{spriteName}.asset");
            if (tile == null || tile.sprite == null || tile.sprite.name != spriteName)
                throw new InvalidOperationException("Missing verified terrain Tile: " + spriteName);
            tilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }

        private static void SetCastleHP()
        {
            string guid = AssetDatabase.FindAssets("t:BuildingData Castle").FirstOrDefault();
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Castle BuildingData missing.");
            BuildingData castle = AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(guid));
            SerializedObject so = new(castle); so.FindProperty("maxHP").intValue = 2000;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(castle);
        }

        private static Sprite[] Frames(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>().OrderBy(sprite => Suffix(sprite.name)).ToArray();
        private static Sprite First(string path) => Frames(path).FirstOrDefault();
        private static int Suffix(string value) { int i = value.LastIndexOf('_'); return i >= 0 && int.TryParse(value[(i + 1)..], out int n) ? n : 0; }
        private static int[] Range(int start, int count) => Enumerable.Range(start, count).ToArray();
        private static void Put(SerializedObject so, string name, Sprite[] frames)
        {
            SerializedProperty property = so.FindProperty(name); property.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }
    }
}
#endif
