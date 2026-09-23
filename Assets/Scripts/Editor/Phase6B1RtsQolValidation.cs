using System;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase6B1RtsQolValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6B.1 RTS QoL")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 6B.1 RTS QoL validation in Play Mode.");

            ValidateCamera();
            ValidateVisionValues();
            ValidateBuildingPresentationAndRally();

            Debug.Log("Phase 6B.1 automated RTS QoL validation passed: Arrow/edge "
                + "camera configuration, final vision values, enlarged garrison count, "
                + "construction bar lifecycle roots, Rally-enabled roster and Castle "
                + "exclusion. Manual input, deposit and output movement tests remain required.");
        }

        private static void ValidateCamera()
        {
            MapCameraController camera = UnityEngine.Object
                .FindAnyObjectByType<MapCameraController>();
            Require(camera != null, "MapCameraController is missing.");
            Require(camera.EdgeScrollEnabled, "Mouse-edge scrolling is disabled.");
            Require(camera.EdgeScrollThicknessPixels >= 10f
                && camera.EdgeScrollThicknessPixels <= 20f,
                "Edge-scroll thickness must remain in the intended 10-20 pixel range.");
            Require(camera.MovementSpeed > 0f, "Camera movement speed is invalid.");
        }

        private static void ValidateVisionValues()
        {
            BuildingData[] buildings = Resources.LoadAll<BuildingData>("Buildings");
            Require(Mathf.Approximately(buildings.Single(item => item.Type
                == BuildingType.Castle).VisionRadius, 9f), "Castle VisionRadius must be 9.");
            Require(Mathf.Approximately(buildings.Single(item => item.Type
                == BuildingType.Tower).VisionRadius, 7f), "Tower VisionRadius must remain 7.");

            UnitCombatData[] units = AssetDatabase.FindAssets("t:UnitCombatData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UnitCombatData>)
                .Where(item => item != null).ToArray();
            CheckUnit(units, UnitType.Pawn, 3f);
            CheckUnit(units, UnitType.Warrior, 3.5f);
            CheckUnit(units, UnitType.Archer, 4f);
            CheckUnit(units, UnitType.Lancer, 3.5f);
        }

        private static void ValidateBuildingPresentationAndRally()
        {
            Require(BuildingRallyPoint.Supports(BuildingType.Barracks)
                && BuildingRallyPoint.Supports(BuildingType.Archery)
                && BuildingRallyPoint.Supports(BuildingType.Monastery)
                && BuildingRallyPoint.Supports(BuildingType.Tower),
                "Required Rally-enabled building roster is incomplete.");
            Require(!BuildingRallyPoint.Supports(BuildingType.Castle)
                && !BuildingRallyPoint.Supports(BuildingType.House),
                "Castle/Home and House must remain excluded from Rally Points.");

            foreach (BuildingSite site in UnityEngine.Object.FindObjectsByType<BuildingSite>())
            {
                bool supported = BuildingRallyPoint.Supports(site.Type);
                Require((site.GetComponent<BuildingRallyPoint>() != null) == supported,
                    $"{site.name} Rally component does not match its BuildingType.");
                Transform progress = site.transform.Find("ConstructionProgressBar");
                Require(progress != null, $"{site.name} has no construction progress root.");
                Require(progress.gameObject.activeSelf
                        == (site.State == BuildingState.UnderConstruction),
                    $"{site.name} construction progress visibility is out of sync.");
            }

            foreach (CastleDropOff castle in UnityEngine.Object.FindObjectsByType<CastleDropOff>())
                Require(castle.GetComponent<BuildingRallyPoint>() == null,
                    $"{castle.name} main Castle/Home must not have a Rally Point.");

            Require(BuildingGarrisonWorldCount.WorldFontSize == 48
                && Mathf.Approximately(BuildingGarrisonWorldCount.WorldCharacterSize, 0.05f),
                "Garrison world count size does not match Phase 6B.1.");
        }

        private static void CheckUnit(UnitCombatData[] data, UnitType type, float radius)
        {
            Require(Mathf.Approximately(data.Single(item => item.UnitType == type)
                .VisionRadius, radius), $"{type} VisionRadius changed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
