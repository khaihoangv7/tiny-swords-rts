#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEditor;
using UnityEngine;

namespace TinyKingdom.EditorTools
{
    public static class Phase6H1EconomyValidation
    {
        [MenuItem("Tools/Tiny Kingdom/Validate Phase 6H.1 Economy & Worker Logistics")]
        public static void Run()
        {
            EconomyConfig economy = AssetDatabase.LoadAssetAtPath<EconomyConfig>(
                "Assets/Data/Economy/Phase5AEconomy.asset");
            Require(economy != null, "Authoritative EconomyConfig is missing.");
            Require(economy.PawnCarryCapacity == 20, "Pawn carry capacity must be 20.");
            Require(economy.AmountPerStep == 2, "Gather amount must be 2 per work tick.");
            Require(Mathf.Approximately(economy.HouseSheepRespawnSeconds, 90f),
                "House Sheep respawn delay must be 90 gameplay seconds.");

            BuildingData[] buildings = Resources.LoadAll<BuildingData>("Buildings");
            BuildingData house = buildings.Single(item => item.Type == BuildingType.House);
            Require(house.WoodCost == 80, "House authoritative Wood cost must be 80.");
            foreach (Faction faction in new[] { Faction.Blue, Faction.Red })
            foreach (HouseVariant variant in Enum.GetValues(typeof(HouseVariant)))
                Require(house.GetSprite(faction, variant) != null,
                    $"House {variant} for {faction} is missing from the shared House data.");

            Require(typeof(IResourceDropOff).IsAssignableFrom(typeof(CastleDropOff)),
                "Castle does not implement the generic resource drop-off contract.");
            Require(typeof(IResourceDropOff).IsAssignableFrom(typeof(BuildingResourceDropOff)),
                "Tower drop-off adapter does not implement the generic contract.");
            Require(typeof(PawnGatherer).GetMethod(nameof(PawnGatherer.Gather),
                    new[] { typeof(ResourceNode), typeof(Predicate<ResourceNode>),
                        typeof(Predicate<Vector3>), typeof(IPathTraversalPolicy) }) != null,
                "Pawn Gather is missing legal position filtering for remembered resources.");
            Require(typeof(ResourceInteractionSlots).GetMethod(
                    nameof(ResourceInteractionSlots.HasAvailableCapacity)) != null,
                "Resource slot capacity query is missing.");
            Require(Enum.IsDefined(typeof(WorkerState),
                    WorkerState.MovingToRememberedResource),
                "Explored resource continuation state is missing.");
            Require(typeof(PawnGatherer).GetMethod("TryAutoRetarget",
                    BindingFlags.Instance | BindingFlags.NonPublic) != null,
                "Resource overflow/depletion continuation is missing.");

            if (!Application.isPlaying)
            {
                Debug.Log("Phase 6H.1 Edit Mode validation passed: House 80W, carry 20, "
                    + "gather +2, 90-second House Sheep timer, generic Castle/Tower drop-off, "
                    + "slot-aware same-type continuation and explored-memory architecture.");
                return;
            }

            PawnGatherer[] pawns = UnityEngine.Object.FindObjectsByType<PawnGatherer>();
            Require(pawns.Length > 0, "No runtime PawnGatherer exists.");
            foreach (PawnGatherer pawn in pawns)
            {
                Require(pawn.CarryCapacity == 20, $"{pawn.name} carry capacity is not 20.");
                Require(pawn.AmountPerGather == 2, $"{pawn.name} gather amount is not 2.");
                Require(Mathf.Approximately(pawn.AutoResourceSearchRadius, 0f),
                    $"{pawn.name} does not search the full legally explored map.");
            }

            CastleDropOff[] castles = UnityEngine.Object.FindObjectsByType<CastleDropOff>();
            Require(castles.Length == 2, "Expected both runtime Castles.");
            foreach (CastleDropOff castle in castles)
                Require(castle.IsAvailable && ResourceDropOffRegistry.Active.Any(
                    item => ReferenceEquals(item, castle)),
                    $"{castle.name} is not a registered valid drop-off.");

            foreach (BuildingSite site in UnityEngine.Object.FindObjectsByType<BuildingSite>())
            {
                if (site.Type == BuildingType.Tower)
                {
                    BuildingResourceDropOff tower = site.GetComponent<BuildingResourceDropOff>();
                    Require(tower != null, $"{site.name} is missing Tower drop-off.");
                    Faction enemy = site.Faction == Faction.Blue ? Faction.Red : Faction.Blue;
                    Require(!tower.IsValidFor(enemy), $"{site.name} accepts enemy Pawns.");
                    if (site.State == BuildingState.UnderConstruction)
                        Require(!tower.IsAvailable,
                            $"Incomplete {site.name} is incorrectly available.");
                    else Require(tower.IsAvailable,
                        $"Completed {site.name} is not available.");
                }
                if (site.Type != BuildingType.House) continue;
                HouseSheepSpawner spawner = site.GetComponent<HouseSheepSpawner>();
                Require(spawner != null, $"{site.name} is missing House Sheep ownership.");
                Require(Mathf.Approximately(spawner.RespawnDelaySeconds, 90f),
                    $"{site.name} Sheep respawn delay is not 90 seconds.");
                if (site.State == BuildingState.Completed)
                    Require(spawner.OwnedSheep != null || spawner.IsRespawnPending,
                        $"{site.name} has neither one living Sheep nor a pending replacement.");
            }

            TeamVisionSystem vision = TeamVisionSystem.Instance;
            Require(vision != null && vision.IsReady, "TeamVisionSystem is not ready.");
            GridManager grid = vision.GridManager;
            bool foundUnexplored = false;
            for (int y = 0; y < grid.Height && !foundUnexplored; y++)
            for (int x = 0; x < grid.Width && !foundUnexplored; x++)
            {
                Vector2Int cell = new(x, y);
                if (vision.GetVisionState(Faction.Blue, cell) != VisionState.Unexplored)
                    continue;
                foundUnexplored = true;
                Require(!PawnGatherer.IsAutomaticResourcePositionKnown(Faction.Blue,
                    grid.GridToWorld(x, y)),
                    "An unexplored cell is legal for automatic resource targeting.");
            }
            Require(foundUnexplored, "Fog test needs at least one unexplored Blue cell.");

            Debug.Log("Phase 6H.1 Play Mode validation passed: authoritative balance, "
                + "Castle/Tower ownership and completion rules, House Sheep ownership, "
                + "slot-aware continuation, explored-memory legality and runtime Pawn values.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
