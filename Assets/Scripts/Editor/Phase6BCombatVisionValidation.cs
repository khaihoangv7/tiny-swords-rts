using System;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase6BCombatVisionValidation
    {
        private sealed class TestTarget : ICombatDamageReceiver
        {
            public Faction Faction { get; set; }
            public bool IsAlive { get; set; } = true;
            public bool IsTargetable { get; set; } = true;
            public Transform TargetTransform { get; set; }
            public event Action<ICombatDamageReceiver> CombatDied
            {
                add { }
                remove { }
            }
            public bool ReceiveAttack(int baseDamage, UnitType attacker,
                AttackDelivery delivery, float sourceMultiplier = 1f,
                bool applyCounterMultiplier = true, UnitController damageSource = null)
                => IsAlive && IsTargetable && baseDamage > 0;
        }

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6B Combat Vision")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 6B Combat Vision validation in Play Mode.");

            TeamVisionSystem vision = UnityEngine.Object.FindAnyObjectByType<TeamVisionSystem>();
            GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
            Require(vision != null && vision.IsReady && grid != null,
                "Phase 6B requires the active TeamVisionSystem and logical grid.");
            vision.RecomputeNow();

            ValidateFactionQuery(vision, grid, Faction.Blue);
            ValidateFactionQuery(vision, grid, Faction.Red);
            ValidateCombatData();
            ValidateRuntimeWiring();

            Debug.Log("Phase 6B automated combat-vision validation passed: authoritative "
                + "Blue/Red observation query, hidden normal-target rejection, local "
                + "retaliation exception without map reveal, final vision values, aggro "
                + "values, retaliation exclusions/leash and runtime wiring.");
        }

        private static void ValidateFactionQuery(TeamVisionSystem vision, GridManager grid,
            Faction faction)
        {
            Vector3 visible = FindCell(vision, grid, faction, VisionState.Visible);
            Vector3 hidden = FindHiddenCell(vision, grid, faction);
            var targetObject = new GameObject("Phase6BValidationTarget");
            try
            {
                var target = new TestTarget
                {
                    Faction = faction == Faction.Blue ? Faction.Red : Faction.Blue,
                    TargetTransform = targetObject.transform
                };
                targetObject.transform.position = visible;
                Require(CombatVisibility.CanFactionCurrentlyObserveTarget(faction, target),
                    $"{faction} rejected a Visible target.");

                targetObject.transform.position = hidden;
                VisionState before = vision.GetVisionState(faction, hidden);
                Require(!CombatVisibility.CanFactionCurrentlyObserveTarget(faction, target),
                    $"{faction} accepted a hidden normal target.");
                Require(!CombatVisibility.CanContinueTarget(faction, target,
                        CombatAwarenessReason.TeamVision),
                    $"{faction} continued a hidden TeamVision target.");
                Require(CombatVisibility.CanContinueTarget(faction, target,
                        CombatAwarenessReason.Retaliation),
                    $"{faction} direct retaliation awareness was rejected.");
                Require(vision.GetVisionState(faction, hidden) == before,
                    "Retaliation awareness modified the faction vision map.");
            }
            finally
            {
                UnityEngine.Object.Destroy(targetObject);
            }
        }

        private static Vector3 FindCell(TeamVisionSystem vision, GridManager grid,
            Faction faction, VisionState required)
        {
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (vision.GetVisionState(faction, new Vector2Int(x, y)) == required)
                    return grid.GridToWorld(x, y);
            throw new InvalidOperationException(
                $"No {required} test cell exists for {faction} in the current Play Mode setup.");
        }

        private static Vector3 FindHiddenCell(TeamVisionSystem vision, GridManager grid,
            Faction faction)
        {
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                Vector2Int position = new(x, y);
                if (vision.GetVisionState(faction, position) != VisionState.Visible)
                    return grid.GridToWorld(x, y);
            }
            throw new InvalidOperationException(
                $"No currently hidden test cell exists for {faction} in the current Play Mode setup.");
        }

        private static void ValidateCombatData()
        {
            UnitCombatData[] data = AssetDatabase.FindAssets("t:UnitCombatData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UnitCombatData>)
                .Where(item => item != null).ToArray();
            Check(data, UnitType.Pawn, 3f, 4f, false);
            Check(data, UnitType.Warrior, 3.5f, 4f, true);
            Check(data, UnitType.Archer, 4f, 5.5f, true);
            Check(data, UnitType.Lancer, 3.5f, 4f, true);

            BuildingData[] buildings = Resources.LoadAll<BuildingData>("Buildings");
            Require(Mathf.Approximately(buildings.Single(item => item.Type == BuildingType.Tower)
                .VisionRadius, 7f), "Tower VisionRadius changed from 7.");
            Require(Mathf.Approximately(buildings.Single(item => item.Type == BuildingType.Castle)
                .VisionRadius, 9f), "Castle VisionRadius changed from 9.");
        }

        private static void Check(UnitCombatData[] data, UnitType type, float vision,
            float aggro, bool retaliates)
        {
            UnitCombatData item = data.Single(candidate => candidate.UnitType == type);
            Require(Mathf.Approximately(item.VisionRadius, vision),
                $"{type} VisionRadius changed.");
            Require(Mathf.Approximately(item.AcquisitionRadius, aggro),
                $"{type} acquisition radius changed.");
            Require(item.CanRetaliateWhenDamaged == retaliates,
                $"{type} retaliation eligibility changed.");
            Require(Mathf.Approximately(item.RetaliationLeash, 10f),
                $"{type} retaliation leash changed from 10.");
        }

        private static void ValidateRuntimeWiring()
        {
            Require(UnityEngine.Object.FindAnyObjectByType<BuildingDefense>() != null,
                "No active Tower/Castle BuildingDefense exists for validation.");
            foreach (CombatController combat in UnityEngine.Object.FindObjectsByType<CombatController>())
            {
                Require(combat.CombatData != null, $"{combat.name} has no UnitCombatData.");
                if (combat.IsMounted)
                    Require(combat.EffectiveDamageMultiplier >= 1.2f,
                        "Mounted Archer damage context is missing.");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
