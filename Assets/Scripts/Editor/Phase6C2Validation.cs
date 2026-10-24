#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TinyKingdom.EditorTools
{
    public static class Phase6C2Validation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6C.2 Strategic Objective")]
        public static void Run()
        {
            Phase6C2AssetCatalog catalog = Phase6C2AssetCatalog.Load();
            Require(catalog != null, "Phase 6C.2 catalog is missing. Run Prepare first.");
            Require(catalog.Monk(Faction.Blue) != null && catalog.Monk(Faction.Red) != null,
                "Phase 6C.2 Monk animation-set references are incomplete.");
            Require(catalog.HealEffect(Faction.Blue).Length == 11
                && catalog.HealEffect(Faction.Red).Length == 11,
                "Phase 6C.2 Monk healing frame references are incomplete.");
            Require(catalog.GoldMineInactive != null && catalog.GoldMineActive != null
                && catalog.GoldMineDestroyed != null, "Gold Mine visual references are incomplete.");
            CheckFrames("Assets/Units/Blue Units/Monk/Idle.png", 6);
            CheckFrames("Assets/Units/Blue Units/Monk/Run.png", 4);
            CheckFrames("Assets/Units/Blue Units/Monk/Heal.png", 11);
            CheckFrames("Assets/Units/Blue Units/Monk/Heal_Effect.png", 11);
            CheckFrames("Assets/Units/Red Units/Monk/Heal.png", 11);
            CheckSingle("Assets/Terrain/Resources/Gold/Gold Mine/GoldMine_Inactive.png");
            CheckSingle("Assets/Terrain/Resources/Gold/Gold Mine/GoldMine_Active.png");
            CheckSingle("Assets/Terrain/Resources/Gold/Gold Mine/GoldMine_Destroyed.png");
            CheckImporter("Assets/Terrain/Tileset/Tilemap_color2.png");
            Require(AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/Terrain/Tileset/Tilemap_color2.png")
                .OfType<Sprite>().Count() == 44,
                "Tilemap_color2 slicing changed; expected 44 verified terrain sprites.");
            ValidateStrategicObjectiveTerrain();
            ValidateStrategicObjectiveNavigation();

            UnitCombatData monk = LoadData("MonkCombatData");
            Require(monk.UnitType == UnitType.Monk && monk.MaxHP == 80 && monk.MoveSpeed == 3f,
                "Monk identity/HP/speed mismatch.");
            Require(monk.FoodCost == 60 && monk.GoldCost == 60 && monk.PopulationCost == 1
                && monk.TrainingSeconds == 7f, "Monk production values mismatch.");
            Require(!monk.AutoAcquireTargets && !monk.CanRetaliateWhenDamaged,
                "Monk must have no attack acquisition/retaliation.");
            Require(Mathf.Approximately(MonkHealer.HealAmount, 6.25f)
                && MonkHealer.HealInterval == 2f
                && MonkHealer.HealRange == 2f && MonkHealer.AutoDetectionRange == 4f,
                "Monk healing values mismatch.");
            Require(GoldMineObjective.MaximumHP == 1500
                && GoldMineObjective.MaximumGold == 2000
                && GoldMineObjective.PawnCapacity == 3
                && Mathf.Approximately(GoldMineObjective.MineLoadMultiplier, 2.5f)
                && GoldMineObjective.MineGoldPerTrip(20) == 50
                && Mathf.Approximately(GoldMineObjective.MiningSeconds(20, 2, 1.5f), 15f)
                && GoldMineObjective.FireThresholdRatio == 0.60f,
                "Gold Mine authoritative values mismatch.");
            Require(Mathf.Approximately(GoldMineObjective.VisualScale, 2.60f)
                && Mathf.Approximately(GoldMineObjective.VisualGroundOffsetY, 0.92f),
                "Gold Mine visual scale/ground offset mismatch.");
            BuildingPresentationProfile mineProfile =
                BuildingPresentationProfiles.For(BuildingType.GoldMine);
            Require(Mathf.Approximately(mineProfile.FireScale, 2.47f)
                && Mathf.Approximately(mineProfile.ExplosionScale, 2.08f)
                && mineProfile.FireAnchor(0) == new Vector2(-0.585f, 0.13f)
                && mineProfile.FireAnchor(1) == new Vector2(0f, 0.546f)
                && mineProfile.FireAnchor(2) == new Vector2(0.585f, 0.156f),
                "Gold Mine enlarged Fire/Explosion presentation profile mismatch.");
            Require(UnitProductionQueue.CanProduce(BuildingType.Monastery, UnitType.Monk),
                "Monastery does not expose Monk production.");
            BuildingData castle = AssetDatabase.FindAssets("t:BuildingData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingData>)
                .Single(item => item != null && item.Type == BuildingType.Castle);
            Require(castle.MaxHP == 2000, "Castle MaxHP must be 2000.");

            if (Application.isPlaying)
            {
                GoldMineObjective mine = UnityEngine.Object.FindAnyObjectByType<GoldMineObjective>();
                Require(mine != null && mine.MaxHP == 1500 && mine.RemainingGold == 2000,
                    "Play Mode Gold Mine missing or has wrong initial values.");
                Require(!UnityEngine.Object.FindObjectsByType<UnitController>()
                        .Any(unit => unit != null && unit.Faction == Faction.Goblin),
                    "Goblin units must remain disabled during the UI/UX phase.");
                SpriteRenderer mineVisual = mine.GetComponentInChildren<SpriteRenderer>();
                Require(mineVisual != null
                    && Mathf.Approximately(mineVisual.transform.lossyScale.x,
                        GoldMineObjective.VisualScale)
                    && Mathf.Approximately(mineVisual.transform.lossyScale.y,
                        GoldMineObjective.VisualScale),
                    "Live Gold Mine does not use the configured uniform visual scale.");
            }

            Debug.Log("Phase 6C.2.1 validation passed for the temporary UI/UX configuration: "
                + "symmetric stair-connected paths, Gold Mine presentation and Monk values verified; "
                + "Goblin gameplay requirements are disabled. "
                + (Application.isPlaying ? "Play Mode objective is present without Goblin guards."
                    : "Run again in Play Mode for spawned-object checks."));
        }

        private static void CheckGoblinSheet(string path, int expectedWidth,
            int expectedHeight, int expectedCells)
        {
            CheckFrames(path, expectedCells);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Require(width == expectedWidth && height == expectedHeight,
                $"{path}: expected {expectedWidth}x{expectedHeight}, got {width}x{height}.");
            Require(importer.spriteImportMode == SpriteImportMode.Multiple
                && Mathf.Approximately(importer.spritePixelsPerUnit, 100f),
                path + ": expected Multiple mode at 100 PPU.");

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>().OrderBy(sprite => NumericSuffix(sprite.name)).ToArray();
            string stem = System.IO.Path.GetFileNameWithoutExtension(path);
            const int columns = 7;
            for (int index = 0; index < sprites.Length; index++)
            {
                Sprite sprite = sprites[index];
                int rowFromTop = index / columns;
                Rect expectedRect = new((index % columns) * 192,
                    expectedHeight - (rowFromTop + 1) * 192, 192, 192);
                Require(sprite != null && sprite.name == $"{stem}_{index}"
                    && sprite.rect == expectedRect
                    && sprite.pivot == new Vector2(96f, 96f),
                    $"{path}: invalid slice {index}; expected {stem}_{index} at {expectedRect}.");
            }
        }

        private static void ValidateGoblinAnimationSets(Phase6C2AssetCatalog catalog,
            string torchPath, string tntPath)
        {
            UnitAnimationSet torch = catalog.GoblinTorch;
            Require(torch != null && torch.FramesPerSecond == 10f
                && torch.AttackFramesPerSecond == 10f && torch.FrameCellSize == 192,
                "Goblin Torch animation timing/cell size is invalid.");
            RequireFrames("Goblin Torch Idle", torch.Idle, torchPath, Range(0, 7));
            RequireFrames("Goblin Torch Run", torch.Run, torchPath, Range(7, 6));
            RequireFrames("Goblin Torch Attack Right", torch.Attack, torchPath, Range(14, 6));
            RequireFrames("Goblin Torch Attack Down", torch.AttackDown, torchPath, Range(21, 6));
            RequireFrames("Goblin Torch Attack Down Right", torch.AttackDownRight,
                torchPath, Range(21, 6));
            RequireFrames("Goblin Torch Attack Up", torch.AttackUp, torchPath, Range(28, 6));
            RequireFrames("Goblin Torch Attack Up Right", torch.AttackUpRight,
                torchPath, Range(28, 6));

            UnitAnimationSet tnt = catalog.GoblinTnt;
            Require(tnt != null && tnt.FramesPerSecond == 10f
                && tnt.AttackFramesPerSecond == 10f && tnt.FrameCellSize == 192,
                "Goblin TNT animation timing/cell size is invalid.");
            RequireFrames("Goblin TNT Idle", tnt.Idle, tntPath, Range(0, 6));
            RequireFrames("Goblin TNT Run", tnt.Run, tntPath, Range(7, 6));
            RequireFrames("Goblin TNT Throw", tnt.Attack, tntPath, Range(14, 7));
            Require(tnt.AttackDown.Length == 0 && tnt.AttackDownRight.Length == 0
                && tnt.AttackUp.Length == 0 && tnt.AttackUpRight.Length == 0,
                "Goblin TNT has no verified directional rows; stale directional arrays must be empty.");
        }

        private static void RequireFrames(string label, Sprite[] actual,
            string expectedPath, int[] expectedIndices)
        {
            Require(actual != null && actual.Length == expectedIndices.Length,
                $"{label}: expected {expectedIndices.Length} non-empty frames.");
            string stem = System.IO.Path.GetFileNameWithoutExtension(expectedPath);
            for (int i = 0; i < expectedIndices.Length; i++)
            {
                Sprite sprite = actual[i];
                Require(sprite != null
                    && AssetDatabase.GetAssetPath(sprite) == expectedPath
                    && sprite.name == $"{stem}_{expectedIndices[i]}",
                    $"{label}: frame {i} must be {stem}_{expectedIndices[i]} and non-null.");
            }
        }

        private static int[] Range(int start, int count)
            => Enumerable.Range(start, count).ToArray();

        private static int NumericSuffix(string value)
        {
            int separator = value.LastIndexOf('_');
            return separator >= 0 && int.TryParse(value[(separator + 1)..], out int number)
                ? number : -1;
        }

        private static void ValidateStrategicObjectiveTerrain()
        {
            BattlefieldMapValidation.ValidateTerraces();
        }

        private static void ValidateStairTile(Tilemap tilemap, int x, int y,
            string expectedName, bool upper, TerrainStairOrientation orientation)
        {
            Tile tile = tilemap.GetTile<Tile>(new Vector3Int(x, y, 0));
            Require(tile != null && tile.sprite != null && tile.sprite.name == expectedName,
                $"Terrain stair at ({x}, {y}) must use {expectedName}.");
            Sprite sprite = tile.sprite;
            Require(AssetDatabase.GetAssetPath(sprite)
                    == "Assets/Terrain/Tileset/Tilemap_color2.png",
                expectedName + " does not come from the verified terrain sheet.");
            float expectedX = orientation == TerrainStairOrientation.DescendsLeft ? 0f : 192f;
            float expectedY = upper ? 64f : 0f;
            Require(sprite.rect == new Rect(expectedX, expectedY, 64f, 64f),
                $"{expectedName} slicing changed; expected 64x64 rect at ({expectedX}, {expectedY}).");
            Require(Mathf.Approximately(sprite.pixelsPerUnit, 64f)
                && sprite.pivot == new Vector2(32f, 32f),
                expectedName + " must keep 64 PPU and a centered pivot.");
            Require(tile.colliderType == Tile.ColliderType.None,
                expectedName + " must remain visual-only; navigation owns walkability.");
        }

        private static void ValidateStrategicObjectiveNavigation()
        {
            BattlefieldMapValidation.ValidateTerraces();
        }

        private static UnitCombatData LoadData(string name)
        {
            UnitCombatData data = AssetDatabase.LoadAssetAtPath<UnitCombatData>($"Assets/Data/{name}.asset");
            Require(data != null, name + " is missing. Run Prepare first."); return data;
        }

        private static void CheckFrames(string path, int count)
        {
            CheckImporter(path);
            int actual = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count();
            Require(actual == count, $"{path}: expected {count} sprites, got {actual}.");
        }

        private static void CheckSingle(string path)
        {
            CheckImporter(path);
            Require(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == 1,
                path + ": expected one sprite.");
        }

        private static void CheckImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null && importer.textureType == TextureImporterType.Sprite,
                path + ": not Sprite import.");
            Require(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.wrapMode == TextureWrapMode.Clamp, path + ": unsafe pixel-art import.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Require(importer.maxTextureSize >= Mathf.Max(width, height), path + ": source is downscaled.");
        }

        private static void CheckPrefab(string name, Type requiredComponent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/Units/{name}.prefab");
            Require(prefab != null, name + " prefab is missing.");
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0,
                name + " prefab contains a missing script.");
            Require(prefab.GetComponent(requiredComponent) != null,
                $"{name} prefab is missing {requiredComponent.Name}.");
            FogPresentationGate gate = prefab.GetComponent<FogPresentationGate>();
            Require(gate != null && gate.HidesUntilFirstVisibilityEvaluation,
                name + " must start hidden until its first Fog visibility evaluation.");
        }

        private static void CheckGoblinPrefab(string name, Type requiredComponent,
            UnitAnimationSet expectedAnimationSet)
        {
            CheckPrefab(name, requiredComponent);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/Units/{name}.prefab");
            UnitAnimator animator = prefab.GetComponent<UnitAnimator>();
            SpriteRenderer sourceRenderer = prefab.GetComponent<SpriteRenderer>();
            Require(animator != null && animator.AnimationSet == expectedAnimationSet,
                name + " prefab does not reference the verified Goblin animation set.");
            Require(sourceRenderer != null && sourceRenderer.sprite != null,
                name + " prefab needs a non-null source sprite for the runtime AnimatedVisual.");
        }

        private sealed class NavigationDataGrid : IGridCellProvider
        {
            private readonly GridCell[,] cells;
            public int Width { get; }
            public int Height { get; }

            public NavigationDataGrid(NavigationGridData data)
            {
                Width = data.Width;
                Height = data.Height;
                cells = new GridCell[Width, Height];
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    Require(data.TryGetCellData(x, y, out NavigationCellData cell),
                        $"Navigation cell ({x}, {y}) is missing.");
                    Vector3 world = new(data.WorldOrigin.x + (x + 0.5f) * data.CellSize,
                        data.WorldOrigin.y + (y + 0.5f) * data.CellSize, 0f);
                    cells[x, y] = new GridCell(x, y, world,
                        cell.terrainType, cell.heightLevel);
                }
            }

            public bool IsInsideGrid(int x, int y)
                => x >= 0 && x < Width && y >= 0 && y < Height;

            public GridCell GetCell(int x, int y)
                => IsInsideGrid(x, y) ? cells[x, y] : null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
