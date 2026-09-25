#if UNITY_EDITOR
using System;
using System.Linq;
using TinyKingdom.Input;
using TinyKingdom.UI;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.EditorTools
{
    public static class RtsHudValidation
    {
        private const string WoodTable = "Assets/UI Elements/UI Elements/Wood Table/WoodTable.png";
        private const string Slot = "Assets/UI Elements/UI Elements/Wood Table/WoodTable_Slots.png";
        private const string Paper = "Assets/UI Elements/UI Elements/Papers/SpecialPaper.png";
        private const string Icons = "Assets/UI Elements/UI Elements/Icons/";
        private const string Avatars = "Assets/UI Elements/UI Elements/Human Avatars/";

        [MenuItem("Tools/Tiny Kingdom/Validate RTS HUD")]
        public static void Run()
        {
            Check(WoodTable, 448, 448, 9);
            Check(Slot, 192, 192, 1);
            Check(Paper, 320, 320, 9);
            foreach (int number in new[] { 1, 2, 3, 4, 6, 9 })
                Check(Icons + $"Icon_{number:00}.png", 64, 64, 1);
            for (int number = 1; number <= 5; number++)
                Check(Avatars + $"Avatars_{number:00}.png", 256, 256, 1);

            RtsHudAssetCatalog catalog = RtsHudAssetCatalog.Load();
            Require(catalog != null && catalog.IsComplete,
                "RTS HUD catalog is missing or incomplete. Run Prepare RTS HUD.");
            Require(catalog.WoodTable.Length == 9 && catalog.SpecialPaper.Length == 9,
                "RTS HUD nine-piece panel references are incomplete.");
            RequireSprite(catalog.PopulationIcon, Icons + "Icon_01.png", "Icon_01_0");
            RequireSprite(catalog.WoodIcon, Icons + "Icon_02.png", "Icon_02_0");
            RequireSprite(catalog.GoldIcon, Icons + "Icon_03.png", "Icon_03_0");
            RequireSprite(catalog.FoodIcon, Icons + "Icon_04.png", "Icon_04_0");
            RequireSprite(catalog.Avatar(UnitType.Warrior), Avatars + "Avatars_01.png", "Avatars_01_0");
            RequireSprite(catalog.Avatar(UnitType.Lancer), Avatars + "Avatars_02.png", "Avatars_02_0");
            RequireSprite(catalog.Avatar(UnitType.Archer), Avatars + "Avatars_03.png", "Avatars_03_0");
            RequireSprite(catalog.Avatar(UnitType.Monk), Avatars + "Avatars_04.png", "Avatars_04_0");
            RequireSprite(catalog.Avatar(UnitType.Pawn), Avatars + "Avatars_05.png", "Avatars_05_0");

            if (Application.isPlaying)
            {
                RtsHudController hud = UnityEngine.Object.FindAnyObjectByType<RtsHudController>();
                Require(hud != null && hud.IsReady, "Runtime RTS HUD is missing or incomplete.");
                Require(hud.ResourcePanel.Root.gameObject.activeInHierarchy,
                    "Resource panel must always remain visible during gameplay.");
                Require(RectMatches(hud.HudRoot, new Vector2(1258f, 160f))
                    && hud.HudRoot.anchorMin == Vector2.zero
                    && hud.HudRoot.anchorMax == Vector2.zero
                    && hud.HudRoot.pivot == Vector2.zero
                    && hud.HudRoot.anchoredPosition.y >= 8f,
                    "Bottom HUD root must be bottom-left anchored, 1258x160 and inside the viewport.");
                Require(RectMatches(hud.ResourcePanel.Root, new Vector2(220f, 160f))
                    && hud.ResourcePanel.Root.anchoredPosition.y >= 0f,
                    "Resource panel must be compact, baseline-aligned and fully on-screen.");
                Require(RectMatches(hud.SelectionPanel.Root, new Vector2(380f, 160f))
                    && RectMatches(hud.CommandPanel.Root, new Vector2(650f, 160f)),
                    "Selection and command panels do not match the compact HUD layout.");
                CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
                Require(scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                    && scaler.referenceResolution == new Vector2(1920f, 1080f)
                    && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight
                    && Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f),
                    "RTS HUD CanvasScaler configuration is invalid.");
                RectTransform action = hud.CommandPanel.Root.Find("Action0") as RectTransform;
                Require(action != null && action.rect.width >= 52f && action.rect.width <= 60f
                    && action.rect.height >= 52f && action.rect.height <= 60f,
                    "Command button dimensions must remain within the intended 52-60 px range.");
                Require(CountNamed(hud.ResourcePanel.Root, "WoodTableSurface") == 1
                    && CountNamed(hud.SelectionPanel.Root, "SpecialPaperSurface") == 1
                    && CountNamed(hud.CommandPanel.Root, "SpecialPaperSurface") == 1,
                    "Each panel must have exactly one continuous surface background.");
                Require(hud.ResourcePanel.Root.Find("WoodTableSurface")
                        .GetComponent<Image>().raycastTarget
                    && hud.SelectionPanel.Root.Find("SpecialPaperSurface")
                        .GetComponent<Image>().raycastTarget
                    && hud.CommandPanel.Root.Find("SpecialPaperSurface")
                        .GetComponent<Image>().raycastTarget,
                    "Visible HUD panel surfaces must block world clicks underneath.");
                Require(!UnityEngine.Object.FindObjectsByType<UnitController>()
                        .Any(unit => unit != null && unit.Faction == Faction.Goblin),
                    "Goblin gameplay must remain disabled during the UI/UX phase.");
                SelectionManager selection = UnityEngine.Object.FindAnyObjectByType<SelectionManager>();
                Require(selection != null, "SelectionManager is missing.");
            }

            Debug.Log("RTS HUD validation passed: pixel imports, panel assets, resource icons, "
                + "unit avatars and authoritative presenter configuration verified. "
                + (Application.isPlaying ? "Runtime HUD is present and Goblins are disabled."
                    : "Run again in Play Mode for live panel checks."));
        }

        private static void Check(string path, int expectedWidth, int expectedHeight,
            int expectedSprites)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null && importer.textureType == TextureImporterType.Sprite,
                path + ": expected Sprite texture type.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Require(width == expectedWidth && height == expectedHeight,
                $"{path}: expected {expectedWidth}x{expectedHeight}, got {width}x{height}.");
            Require(importer.spriteImportMode == SpriteImportMode.Multiple
                && AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count() == expectedSprites,
                $"{path}: expected {expectedSprites} verified sprites.");
            Require(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.wrapMode == TextureWrapMode.Clamp,
                path + ": unsafe pixel-art import settings.");
            Require(importer.maxTextureSize >= Mathf.Max(width, height),
                path + ": source texture is downscaled.");
        }

        private static void RequireSprite(Sprite sprite, string path, string name)
            => Require(sprite != null && AssetDatabase.GetAssetPath(sprite) == path
                && sprite.name == name, $"Expected {name} from {path}.");

        private static bool RectMatches(RectTransform rect, Vector2 expected)
            => rect != null && Vector2.Distance(rect.rect.size, expected) < 0.01f;

        private static int CountNamed(RectTransform root, string name)
            => root.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name == name);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
