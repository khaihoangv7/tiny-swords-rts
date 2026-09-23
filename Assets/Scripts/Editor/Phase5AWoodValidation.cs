using System;
using System.Linq;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Units;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    // A focused Play Mode check for the reported Tree command failure.
    public static class Phase5AWoodValidation
    {
        private static PawnGatherer pawn;
        private static ResourceNode tree;
        private static ResourceStockpile stockpile;
        private static int startingTree;
        private static int startingWood;
        private static double started;

        [MenuItem("Tools/Tiny Kingdom/Validate Phase 5A Wood Loop")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Run Phase 5A Wood Loop validation in Play Mode.");
            EditorApplication.update -= Check;
            pawn = UnityEngine.Object.FindObjectsByType<PawnGatherer>()
                .FirstOrDefault(p => p.GetComponent<UnitController>()?.Faction == Faction.Blue);
            tree = UnityEngine.Object.FindObjectsByType<ResourceNode>()
                .FirstOrDefault(n => n.ResourceType == ResourceType.Wood && n.IsAvailable);
            stockpile = UnityEngine.Object.FindObjectsByType<ResourceStockpile>()
                .FirstOrDefault(s => s.Faction == Faction.Blue);
            var selection = UnityEngine.Object.FindAnyObjectByType<SelectionManager>();
            var commands = UnityEngine.Object.FindAnyObjectByType<PlayerCommandController>();
            if (pawn == null || tree == null || stockpile == null
                || selection == null || commands == null)
                throw new InvalidOperationException("Phase 5A Map test objects are missing.");

            SpriteRenderer treeVisual = tree.GetComponentInChildren<SpriteRenderer>();
            if (treeVisual == null)
                throw new InvalidOperationException("Tree sprite is missing.");
            Vector3 trunk = new(tree.transform.position.x, treeVisual.bounds.min.y + 0.1f, 0f);
            Physics2D.SyncTransforms();
            if (!Physics2D.OverlapPointAll(trunk).Any(c => c.GetComponent<ResourceNode>() == tree))
                throw new InvalidOperationException("Tree collider does not cover its visible trunk.");
            selection.ApplyClickSelection(pawn.GetComponent<UnitSelection>(), false);
            Vector2 pointer = Camera.main.WorldToScreenPoint(trunk);
            if (!commands.CanGatherAtScreenPosition(pointer)
                || commands.IssueGatherCommand(tree) != 1)
                throw new InvalidOperationException("Tree hover or generic Gather command was rejected.");

            startingTree = tree.RemainingAmount;
            startingWood = stockpile.GetAmount(ResourceType.Wood);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Check;
            Debug.Log("Phase 5A Wood validation: Tree collider and Gather command accepted; waiting for Wood deposit.");
        }

        private static void Check()
        {
            if (!EditorApplication.isPlaying || pawn == null || tree == null || stockpile == null)
            {
                EditorApplication.update -= Check;
                return;
            }
            if (pawn.State == WorkerState.Gathering
                && Vector2.Distance(pawn.transform.position, tree.transform.position) > 1f)
            {
                EditorApplication.update -= Check;
                Debug.LogError("Phase 5A Wood loop FAILED: Pawn started chopping before reaching the Tree.");
                return;
            }
            if (stockpile.GetAmount(ResourceType.Wood) > startingWood)
            {
                EditorApplication.update -= Check;
                Debug.Log($"Phase 5A Wood loop PASS: Tree {startingTree} -> {tree.RemainingAmount}, "
                    + $"stockpile Wood {startingWood} -> {stockpile.GetAmount(ResourceType.Wood)}.");
                return;
            }
            if (EditorApplication.timeSinceStartup - started < 25d) return;
            EditorApplication.update -= Check;
            Debug.LogError($"Phase 5A Wood loop FAILED: Tree {startingTree} -> "
                + $"{tree.RemainingAmount}, Pawn state {pawn.State}, cargo "
                + $"{pawn.CarriedResourceType} {pawn.CarriedAmount}, position {pawn.transform.position}, "
                + $"stockpile Wood {startingWood} -> {stockpile.GetAmount(ResourceType.Wood)}.");
        }
    }
}
