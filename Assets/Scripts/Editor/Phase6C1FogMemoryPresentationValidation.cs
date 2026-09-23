#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TinyKingdom.EditorTools
{
    public static class Phase6C1FogMemoryPresentationValidation
    {
        private static readonly float[] DeathDurations =
        {
            0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.5f,
            0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f
        };

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6C.1 Fog Memory & Presentation")]
        public static void Run()
        {
            Phase6C1PresentationSetup.EnsureAssets();
            ValidateStaticAssets();
            if (!Application.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 6C.1 Fog Memory & Presentation validation in Play Mode.");

            FogMemorySystem[] memories = UnityEngine.Object.FindObjectsByType<FogMemorySystem>();
            Require(memories.Length == 1 && memories[0].IsInitialized
                && memories[0].InitializedFactionCount == 2,
                "Exactly one initialized two-faction FogMemorySystem is required.");

            CastleDropOff[] castles = UnityEngine.Object.FindObjectsByType<CastleDropOff>();
            Require(castles.Length == 2, "Blue and Red Main Castles are required.");
            foreach (CastleDropOff castle in castles)
                Require(castle.GetComponent<BuildingRallyPoint>() != null,
                    $"{castle.name} is missing Castle Rally support.");

            foreach (BuildingRallyPoint rally in
                UnityEngine.Object.FindObjectsByType<BuildingRallyPoint>())
                Require(rally.FlagSprite != null && rally.FlagSprite.name == "Icon_09_0"
                    && rally.MarkerCount == 1,
                    $"{rally.name} Rally marker is missing, duplicated or incorrectly mapped.");

            foreach (HealthComponent health in UnityEngine.Object.FindObjectsByType<HealthComponent>())
            {
                if (health.CombatData == null || health.CombatData.UnitType
                    is not (UnitType.Warrior or UnitType.Lancer)) continue;
                CombatController combat = health.GetComponent<CombatController>();
                UnitAnimator animator = health.GetComponent<UnitAnimator>();
                Require(combat != null && animator != null
                    && animator.IsAttackPresentationConnected
                    && combat.HasAttackPresentationListener,
                    $"{health.name} building attack presentation is not connected.");
            }

            foreach (BuildingVisionSource building in
                VisionSourceRegistry.ActiveSources.OfType<BuildingVisionSource>())
            {
                FogPresentationGate gate = building.GetComponent<FogPresentationGate>();
                Require(gate != null,
                    $"{building.name} live building presentation is not Fog-gated.");
                if (building.Faction != memories[0].PlayerViewFaction
                    && !memories[0].IsCurrentlyVisible(memories[0].PlayerViewFaction,
                        building.VisionPosition))
                    Require(!gate.IsPresented,
                        $"{building.name} leaks live presentation outside current vision.");
            }
            foreach (ResourceNode resource in ResourceNode.ActiveNodes)
            {
                if (resource == null) continue;
                FogPresentationGate gate = resource.GetComponent<FogPresentationGate>();
                Require(gate != null,
                    $"{resource.name} live resource presentation is not Fog-gated.");
                if (!memories[0].IsCurrentlyVisible(memories[0].PlayerViewFaction,
                    resource.transform.position))
                    Require(!gate.IsPresented,
                        $"{resource.name} leaks live presentation outside current vision.");
            }

            foreach (Faction faction in new[] { Faction.Blue, Faction.Red })
            {
                foreach (BuildingMemorySnapshot snapshot in
                    memories[0].GetLastSeenBuildings(faction))
                    Require(snapshot != null && snapshot.Sprite != null,
                        $"{faction} contains an invalid last-seen building snapshot.");
                foreach (ResourceMemorySnapshot snapshot in
                    memories[0].GetLastSeenResources(faction))
                    Require(snapshot != null && snapshot.Sprite != null,
                        $"{faction} contains an invalid last-seen resource snapshot.");
            }

            Transform ghosts = memories[0].transform.Find("FogMemoryGhosts");
            Require(ghosts != null && ghosts.GetComponentsInChildren<Collider2D>(true).Length == 0,
                "Last-seen ghosts must exist under FogMemoryGhosts without colliders.");
            Require(ghosts.GetComponentsInChildren<UnitController>(true).Length == 0,
                "Enemy units must never receive last-seen ghost presentation.");

            Tilemap shadow = UnityEngine.Object.FindObjectsByType<Tilemap>()
                .FirstOrDefault(item => item.name == "Shadow_L1");
            Color expected = new(0.28f, 0.32f, 0.42f, 1f);
            Require(shadow != null && Approximately(shadow.color, expected),
                "Shadow_L1 must use the verified darker neutral tint.");

            Debug.Log("Phase 6C.1 automated validation passed: 14-frame Dead/Buried VFX, "
                + "Warrior/Lancer attack presentation connection, Castle Rally, one verified "
                + "Icon_09 Rally marker per host, two-faction Fog memory, non-interactive "
                + "building/resource ghosts and darker Shadow_L1. Manual visual and hidden-state "
                + "tests remain required.");
        }

        private static void ValidateStaticAssets()
        {
            PresentationAssetCatalog catalog = PresentationAssetCatalog.Load();
            Require(catalog != null && catalog.DeadFrames.Length == 14
                && catalog.DeadFrames.All(sprite => sprite != null),
                "Presentation catalog must map all 14 Dead/Buried frames.");
            Require(UnitDeathVfx.RuntimeFrameCount == 14,
                "UnitDeathVfx must resolve all 14 frames.");
            for (int i = 0; i < DeathDurations.Length; i++)
                Require(Mathf.Approximately(UnitDeathVfx.FrameDurationAt(i), DeathDurations[i]),
                    $"Dead/Buried frame {i} duration is incorrect.");
            Require(Mathf.Approximately(UnitDeathVfx.RuntimeTotalDuration, 1.8f),
                "Dead/Buried total duration must be 1.8 seconds.");
            Require(catalog.RallyFlag != null && catalog.RallyFlag.name == "Icon_09_0",
                "Icon_09_0 Rally marker is missing from the presentation catalog.");

            const string shadowPath = "Assets/Terrain/Tileset/Shadow.png";
            Require(File.Exists(shadowPath), "Dedicated terrain Shadow texture is missing.");
            var shadowTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Require(ImageConversion.LoadImage(shadowTexture, File.ReadAllBytes(shadowPath)),
                    "Dedicated terrain Shadow texture could not be decoded.");
                Color32[] pixels = shadowTexture.GetPixels32();
                Require(pixels.Any(pixel => pixel.a == 153)
                    && pixels.All(pixel => pixel.a == 0 || pixel.a == 153),
                    "Dedicated terrain Shadow alpha must be the corrected value 153/255.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shadowTexture);
            }

            TextureImporter flag = AssetImporter.GetAtPath(
                Phase6C1PresentationSetup.RallyFlagPath) as TextureImporter;
            Require(flag != null && flag.filterMode == FilterMode.Point
                && !flag.mipmapEnabled
                && flag.textureCompression == TextureImporterCompression.Uncompressed
                && flag.wrapMode == TextureWrapMode.Clamp,
                "Rally flag pixel-art import settings are invalid.");

            RequireDocumentation("VISION_FOG_SPEC.md", "last-seen");
            RequireDocumentation("GAME_SPEC.md", "Phase 6C.1");
            RequireDocumentation("ASSET_MANIFEST.md", "Icon_09_0");
        }

        private static void RequireDocumentation(string path, string value)
        {
            Require(File.Exists(path) && File.ReadAllText(path)
                .Contains(value, StringComparison.OrdinalIgnoreCase),
                $"{path} is missing required Phase 6C.1 documentation: {value}");
        }

        private static bool Approximately(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.001f && Mathf.Abs(a.g - b.g) < 0.001f
                && Mathf.Abs(a.b - b.b) < 0.001f && Mathf.Abs(a.a - b.a) < 0.001f;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
