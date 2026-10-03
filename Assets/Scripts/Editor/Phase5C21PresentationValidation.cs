#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Presentation;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase5C21PresentationValidation
    {
        private static readonly int[] GoldAmounts = { 100, 150, 250, 300, 400, 500 };

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 5C2.1 Presentation")]
        public static void Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException(
                    "Run Phase 5C2.1 presentation validation in Play Mode.");

            BuildingData tower = Resources.LoadAll<BuildingData>("Buildings")
                .First(data => data.Type == BuildingType.Tower);
            BuildingData castle = Resources.LoadAll<BuildingData>("Buildings")
                .First(data => data.Type == BuildingType.Castle);
            RequireAnchors(tower.RooftopAnchors(TinyKingdom.Units.Faction.Blue),
                new[] { new Vector2(0f, 0.83f) }, "Tower");
            RequireAnchors(tower.RooftopAnchors(TinyKingdom.Units.Faction.Red),
                new[] { new Vector2(0f, 0.83f) }, "Red Tower");
            RequireAnchors(castle.RooftopAnchors(TinyKingdom.Units.Faction.Blue),
                new[] { new Vector2(-0.95f, 0.83f), new Vector2(0f, 0.83f),
                    new Vector2(0.95f, 0.83f) }, "Castle");
            RequireAnchors(castle.RooftopAnchors(TinyKingdom.Units.Faction.Red),
                new[] { new Vector2(-0.95f, 0.83f), new Vector2(0f, 0.83f),
                    new Vector2(0.95f, 0.83f) }, "Red Castle");

            PresentationAssetCatalog catalog = PresentationAssetCatalog.Load();
            Require(catalog != null, "Presentation asset catalog is missing.");
            Require(catalog.SheepIdle.Length == 6 && catalog.SheepGrass.Length == 12
                && catalog.SheepMove.Length == 4, "Sheep frame mapping is incomplete.");
            Require(TinyKingdom.Units.UnitDeathVfx.RuntimeFrameCount == 14,
                "Dead VFX must resolve fourteen valid Dead/Buried runtime frames.");
            for (int i = 0; i < 4; i++)
                Require(catalog.TreeFrames(i).Length == 8 && catalog.Stump(i) != null,
                    $"Tree/Stump {i + 1} mapping is incomplete.");
            for (int i = 0; i < 6; i++)
                Require(catalog.Gold(i) != null && catalog.GoldHighlight(i).Length == 6,
                    $"Gold Stone {i + 1} mapping is incomplete.");

            foreach (PawnGatherer gatherer in UnityEngine.Object.FindObjectsByType<PawnGatherer>())
                Require(Mathf.Approximately(gatherer.AutoResourceSearchRadius, 0f),
                    $"{gatherer.name} must search all legally explored resource memory.");

            var nodes = new Dictionary<string, ResourceNode>();
            foreach (ResourceNode node in UnityEngine.Object.FindObjectsByType<ResourceNode>())
                nodes[node.name] = node;
            for (int i = 0; i < 4; i++)
            {
                Require(nodes.TryGetValue($"Tree_{i + 1}", out ResourceNode tree),
                    $"Tree_{i + 1} is missing.");
                Require(tree.GetComponent<TreePresentation>() != null,
                    $"Tree_{i + 1} animation presentation is missing.");
            }
            for (int i = 0; i < 6; i++)
            {
                Require(nodes.TryGetValue($"Gold_{i + 1}", out ResourceNode gold),
                    $"Gold_{i + 1} is missing.");
                Require(gold.MaximumAmount == GoldAmounts[i],
                    $"Gold_{i + 1} expected {GoldAmounts[i]}, found {gold.MaximumAmount}.");
                GoldHarvestHighlight highlight = gold.GetComponent<GoldHarvestHighlight>();
                Require(highlight != null,
                    $"Gold_{i + 1} highlight presentation is missing.");
                Require(Mathf.Approximately(highlight.AmbientHighlightDelay, 10f),
                    $"Gold_{i + 1} ambient Highlight delay is not 10 seconds.");
            }

            foreach (TinyKingdom.Units.HealthComponent health in
                UnityEngine.Object.FindObjectsByType<TinyKingdom.Units.HealthComponent>())
            {
                TinyKingdom.Units.UnitCombatData data = health.CombatData;
                if (data == null) continue;
                bool expected = data.UnitType is TinyKingdom.Units.UnitType.Warrior
                    or TinyKingdom.Units.UnitType.Archer or TinyKingdom.Units.UnitType.Lancer;
                Require(data.CanRetaliateWhenDamaged == expected,
                    $"{data.UnitType} retaliation capability is incorrect.");
                Require(Mathf.Approximately(data.RetaliationLeash, 10f),
                    $"{data.UnitType} retaliation leash is not 10.");
            }
            foreach (ResourceNode sheep in nodes.Values.Where(node => node.ResourceType == ResourceType.Food))
                Require(sheep.GetComponent<SheepPresentation>() != null,
                    $"{sheep.name} Sheep presentation is missing.");

            foreach (BuildingGarrison garrison in UnityEngine.Object.FindObjectsByType<BuildingGarrison>())
                Require(garrison.GetComponent<BuildingGarrisonWorldCount>() != null,
                    $"{garrison.name} world garrison count is missing.");
            foreach (BuildingSite site in UnityEngine.Object.FindObjectsByType<BuildingSite>())
                Require(site.GetComponent<BuildingInteractionSlots>() != null,
                    $"{site.name} builder interaction slots are missing.");

            Debug.Log("Phase 5C2.1 presentation validation passed: rooftop anchors, catalog, "
                + "world garrison counters, builder slots, explored resource chaining, Sheep, "
                + "four Tree/Stump pairs, six Gold capacities/ambient highlights, valid Dead "
                + "VFX frames and unit retaliation configuration.");
        }

        private static void RequireAnchors(Vector2[] actual, Vector2[] expected, string label)
        {
            Require(actual.Length == expected.Length, $"{label} anchor count is incorrect.");
            for (int i = 0; i < expected.Length; i++)
                Require(Vector2.Distance(actual[i], expected[i]) < 0.001f,
                    $"{label} anchor {i} expected {expected[i]}, found {actual[i]}.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
