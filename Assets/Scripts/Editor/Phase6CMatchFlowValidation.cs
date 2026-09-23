using System;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Match;
using TinyKingdom.Presentation;
using TinyKingdom.UI;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase6CMatchFlowValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6C Match Flow")]
        public static void Run()
        {
            Phase6CMatchResultSetup.EnsureAssets();
            ValidateAssets();
            Require(MatchManager.ResultForDestroyedCastle(Faction.Red)
                    == MatchResult.BlueVictory
                && MatchManager.ResultForDestroyedCastle(Faction.Blue)
                    == MatchResult.RedVictory,
                "Blue/Red Main Castle result mapping is incorrect.");
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 6C Match Flow validation in Play Mode.");

            MatchManager[] managers = UnityEngine.Object.FindObjectsByType<MatchManager>();
            Require(managers.Length == 1, "Exactly one active MatchManager is required.");
            MatchManager manager = managers[0];
            Require(manager.HasBothMainCastles, "Blue and Red Main Castles must be registered.");
            Require(manager.State == MatchState.Playing || manager.State == MatchState.Ended,
                "MatchManager must leave Preparing after both Main Castles register.");
            Require(manager.BlueMainCastle.Faction == Faction.Blue
                && manager.RedMainCastle.Faction == Faction.Red
                && manager.BlueMainCastle.Type == BuildingType.Castle
                && manager.RedMainCastle.Type == BuildingType.Castle,
                "Main Castle registration is invalid.");
            Require(manager.ResultPresentationDelay >= 0.75f
                && manager.ResultPresentationDelay <= 1f,
                "Result presentation delay must remain between 0.75 and 1.0 seconds.");
            MatchResultUI[] resultUis = UnityEngine.Object.FindObjectsByType<MatchResultUI>(
                FindObjectsInactive.Include);
            Require(resultUis.Length == 1 && resultUis[0].ReferencesValid,
                "Exactly one configured MatchResultUI is required.");
            Require(resultUis[0].HasRibbonReference
                && resultUis[0].HasRestartButtonReference,
                "Result Ribbon or Restart button references are invalid.");

            Debug.Log("Phase 6C automated validation passed: one MatchManager, explicit "
                + "Blue/Red Main Castles, deterministic result mapping, 0.85-second "
                + "presentation delay and complete Tiny Swords result UI references. "
                + "Manual Castle destruction, post-match blocking, UI and Restart tests remain required.");
        }

        private static void ValidateAssets()
        {
            MatchResultAssetCatalog catalog = MatchResultAssetCatalog.Load();
            Require(catalog != null && catalog.IsComplete,
                "Phase 6C Match Result asset catalog is missing or incomplete.");
            (string Path, int SliceCount)[] textures =
            {
                ("Assets/UI Elements/UI Elements/Ribbons/BigRibbons.png", 15),
                ("Assets/UI Elements/UI Elements/Papers/SpecialPaper.png", 9),
                ("Assets/UI Elements/UI Elements/Buttons/SmallBlueSquareButton_Regular.png", 1),
                ("Assets/UI Elements/UI Elements/Buttons/SmallBlueSquareButton_Pressed.png", 1),
                ("Assets/UI Elements/UI Elements/Buttons/SmallRedSquareButton_Regular.png", 1),
                ("Assets/UI Elements/UI Elements/Buttons/SmallRedSquareButton_Pressed.png", 1)
            };
            foreach ((string path, int expectedSlices) in textures)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer != null && importer.textureType == TextureImporterType.Sprite
                    && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
                    && importer.textureCompression == TextureImporterCompression.Uncompressed
                    && importer.wrapMode == TextureWrapMode.Clamp,
                    "Pixel-art Result UI import settings are invalid: " + path);
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                Require(importer.maxTextureSize >= Mathf.Max(width, height),
                    "Result UI texture is being downscaled: " + path);
                Require(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count()
                        == expectedSlices,
                    "Verified Result UI slicing changed: " + path);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
