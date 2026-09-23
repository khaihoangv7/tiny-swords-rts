using System;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase6B2Validation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6B.2 Presentation")]
        public static void Run()
        {
            Phase6B2PresentationSetup.EnsureAssets();
            ValidateAssets();
            ValidatePresentationProfiles();
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 6B.2 Presentation validation in Play Mode.");

            UnitMovement[] movements = UnityEngine.Object.FindObjectsByType<UnitMovement>();
            Require(movements.Length > 0, "No active UnitMovement found.");
            Require(movements.All(item => Mathf.Approximately(
                item.CommandDestinationEpsilon, 0.2f)),
                "Move command destination epsilon must be 0.2 world units.");

            foreach (BuildingSite site in UnityEngine.Object.FindObjectsByType<BuildingSite>())
            {
                Require(site.GetComponent<BuildingHealthBar>()?.IsConfigured == true,
                    $"{site.name} has no configured large building Life Bar.");
                BuildingDamagePresentation damage =
                    site.GetComponent<BuildingDamagePresentation>();
                Require(damage?.IsConfigured == true,
                    $"{site.name} has no configured damage presentation.");
                if (site.State == BuildingState.UnderConstruction)
                    Require(!damage.IsBurning,
                        $"{site.name} incorrectly burns while under construction.");
            }

            foreach (CastleDropOff castle in UnityEngine.Object.FindObjectsByType<CastleDropOff>())
            {
                Require(castle.GetComponent<BuildingHealthBar>()?.IsConfigured == true,
                    $"{castle.name} has no configured large building Life Bar.");
                Require(castle.GetComponent<BuildingDamagePresentation>()?.IsConfigured == true,
                    $"{castle.name} has no configured damage presentation.");
            }

            Require(Mathf.Approximately(BuildingDamagePresentation.FireThreshold, 0.70f),
                "Building fire threshold must be 70 percent.");
            Require(Mathf.Approximately(BuildingDamagePresentation.FramesPerSecond, 10f),
                "Building Fire/Explosion playback must be 10 FPS.");
            Require(BuildingHealthBar.FramePixelWidth == 114
                && BuildingHealthBar.FramePixelHeight == 53
                && BuildingHealthBar.InnerPixelX == 12
                && BuildingHealthBar.InnerPixelY == 13
                && BuildingHealthBar.InnerPixelWidth == 90
                && BuildingHealthBar.InnerPixelHeight == 26
                && BuildingHealthBar.VisibleFillPixelHeight == 24,
                "Building Life Bar runtime geometry must remain 114x53 with a "
                + "90x26 inner container at (12,13) and 24 visible fill rows.");
            Require(BuildingHealthBar.CalculateFillPixelWidth(1f) == 90
                && BuildingHealthBar.CalculateFillPixelWidth(0.9f) == 81
                && BuildingHealthBar.CalculateFillPixelWidth(0.5f) == 45
                && BuildingHealthBar.CalculateFillPixelWidth(0f) == 0,
                "Building Life Bar must map HP ratios across all 90 inner pixels.");

            Debug.Log("Phase 6B.2 automated presentation validation passed: verified "
                + "3 Fire atlases, 2 Explosion atlases, BigBar mapping, 0.2 movement "
                + "epsilon, 90-pixel building fill, size profiles and no construction Fire. Manual input, "
                + "rapid-click, facing, Fog and destruction playback tests remain required.");
        }

        private static void ValidatePresentationProfiles()
        {
            BuildingPresentationProfile small =
                BuildingPresentationProfiles.For(BuildingType.House);
            BuildingPresentationProfile tower =
                BuildingPresentationProfiles.For(BuildingType.Tower);
            BuildingPresentationProfile medium =
                BuildingPresentationProfiles.For(BuildingType.Barracks);
            BuildingPresentationProfile castle =
                BuildingPresentationProfiles.For(BuildingType.Castle);

            Require(Mathf.Approximately(small.FireScale, 1.8f)
                && Mathf.Approximately(tower.FireScale, small.FireScale)
                && Mathf.Approximately(medium.FireScale, 2.4f)
                && Mathf.Approximately(castle.FireScale, 3.2f),
                "Building Fire size profiles are invalid.");
            Require(Mathf.Approximately(small.ExplosionScale, 1.35f)
                && Mathf.Approximately(medium.ExplosionScale, 1.75f)
                && Mathf.Approximately(castle.ExplosionScale, 2.4f),
                "Building Explosion size profiles are invalid.");
            Require(Mathf.Approximately(small.LifeBarWidthScale, 1f)
                && Mathf.Approximately(medium.LifeBarWidthScale, 1.25f)
                && Mathf.Approximately(castle.LifeBarWidthScale, 1.8f),
                "Building Life Bar size profiles are invalid.");
            Require(small.FireAnchor(0) != small.FireAnchor(1)
                && medium.FireAnchor(0) != medium.FireAnchor(2)
                && castle.FireAnchor(0) != castle.FireAnchor(2),
                "Building Fire emitters must use distinct profile anchors.");
        }

        private static void ValidateAssets()
        {
            BuildingPresentationCatalog catalog = BuildingPresentationCatalog.Load();
            Require(catalog != null, "Phase 6B.2 building presentation catalog is missing.");
            Require(catalog.Fire01.Length == 8 && catalog.Fire02.Length == 10
                && catalog.Fire03.Length == 12,
                "Fire frame mapping must be 8/10/12 frames.");
            Require(catalog.Explosion01.Length == 8 && catalog.Explosion02.Length == 10,
                "Explosion frame mapping must be 8/10 frames.");
            Require(catalog.BigBarLeft != null && catalog.BigBarMiddle != null
                && catalog.BigBarRight != null && catalog.BigBarFill != null,
                "BigBar frame/fill mapping is incomplete.");

            string[] paths =
            {
                "Assets/Particle FX/Fire_01.png",
                "Assets/Particle FX/Fire_02.png",
                "Assets/Particle FX/Fire_03.png",
                "Assets/Particle FX/Explosion_01.png",
                "Assets/Particle FX/Explosion_02.png",
                "Assets/UI Elements/UI Elements/Bars/BigBar_Base.png",
                "Assets/UI Elements/UI Elements/Bars/BigBar_Fill.png"
            };
            foreach (string path in paths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer != null, "Missing imported texture: " + path);
                Require(importer.textureType == TextureImporterType.Sprite
                    && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled
                    && importer.textureCompression == TextureImporterCompression.Uncompressed
                    && importer.wrapMode == TextureWrapMode.Clamp,
                    "Pixel-art import settings are invalid: " + path);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
