using System.Collections.Generic;
using System;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Match;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public enum BuildingPlacementFailureReason
    {
        None,
        InvalidFootprint,
        BlockedByResource,
        OccupiedByUnit,
        WouldTrapFriendlyUnits,
        WouldSealWalkablePocket,
        NoConstructionAccess
    }

    // Shared gameplay authority for player input and future computer commanders.
    public sealed class BuildingSystem : MonoBehaviour
    {
        public const int SheepEscapeMaximumRing = 4;

        private readonly struct SheepEscapePlan
        {
            public readonly ResourceNode Sheep;
            public readonly Vector2Int Destination;

            public SheepEscapePlan(ResourceNode sheep, Vector2Int destination)
            {
                Sheep = sheep;
                Destination = destination;
            }
        }

        private readonly Dictionary<BuildingType, BuildingData> definitions = new();
        private readonly List<SheepEscapePlan> sheepEscapePlans = new();
        private readonly HashSet<Vector2Int> reservedSheepEscapeCells = new();
        private readonly BuildingTrapValidator trapValidator = new();
        private readonly List<PawnBuilder> orderedBuilders = new();
        private GridManager grid;
        private PathfindingSystem paths;
        private ResourceStockpile stockpile;
        private FactionPopulation population;
        private EconomyConfig economyConfig;
        private Faction owner;
        public event Action<BuildingSite> BuildingPlaced;

        public Faction Owner => owner;
        public GridManager Grid => grid;
        public BuildingPlacementFailureReason LastPlacementFailureReason { get; private set; }
        public int LastPlacementTrappedUnitCount { get; private set; }
        public int LastPlacementSealedCellCount { get; private set; }
        public int LastReachableBuilderSlotCount { get; private set; }
        public string LastPlacementDiagnostic => LastPlacementFailureReason switch
        {
            BuildingPlacementFailureReason.WouldTrapFriendlyUnits =>
                $"Placement rejected: would trap {LastPlacementTrappedUnitCount} friendly units",
            BuildingPlacementFailureReason.WouldSealWalkablePocket =>
                $"Placement rejected: would seal {LastPlacementSealedCellCount} walkable cells",
            _ => LastPlacementFailureReason.ToString()
        };

        public bool IsReady => grid != null && paths != null && stockpile != null
            && HasValidData(BuildingType.House)
            && HasValidData(BuildingType.Barracks)
            && HasValidData(BuildingType.Tower)
            && HasValidData(BuildingType.Archery)
            && HasValidData(BuildingType.Monastery);

        public void Configure(GridManager navigation, ResourceStockpile resources,
            FactionPopulation population, IEnumerable<BuildingData> data,
            EconomyConfig economy = null)
        {
            grid = navigation;
            paths = grid != null ? grid.GetComponent<PathfindingSystem>() : null;
            stockpile = resources;
            this.population = population;
            economyConfig = economy;
            owner = resources != null ? resources.Faction : Faction.Blue;
            trapValidator.Configure(grid);
            definitions.Clear();
            foreach (BuildingData item in data)
            {
                if (item == null) continue;
                if (definitions.ContainsKey(item.Type))
                    Debug.LogError($"Duplicate BuildingData for {item.Type}.", this);
                definitions[item.Type] = item;
            }
        }

        private bool HasValidData(BuildingType type)
        {
            BuildingData data = GetData(type);
            return data != null && data.GetSprite(owner) != null
                && (type != BuildingType.House
                    || (data.GetSprite(owner, HouseVariant.B) != null
                        && data.GetSprite(owner, HouseVariant.C) != null))
                && data.Footprint.x > 0 && data.Footprint.y > 0
                && data.ConstructionSeconds > 0f && data.MaxHP > 0;
        }

        public BuildingData GetData(BuildingType type)
            => definitions.TryGetValue(type, out BuildingData value) ? value : null;

        public bool CanAfford(BuildingType type)
        {
            BuildingData data = GetData(type);
            return MatchManager.AllowsGameplay && data != null && stockpile != null
                && stockpile.CanAfford(data.FoodCost, data.WoodCost, data.GoldCost);
        }

        public static bool IsValidPlacementCell(GridCell cell, int requiredHeight)
            => TerrainRules.IsStableStandingCell(cell)
                && (requiredHeight < 0 || cell.HeightLevel == requiredHeight);

        // Fog preview may inspect only authored terrain. Dynamic occupancy,
        // resources, units, paths and builder access stay unknown until observed.
        public bool CanPlaceOnKnownTerrain(BuildingType type, Vector2Int origin)
        {
            BuildingData data = GetData(type);
            if (data == null || grid == null) return false;
            int height = -1;
            for (int y = 0; y < data.Footprint.y; y++)
            for (int x = 0; x < data.Footprint.x; x++)
            {
                GridCell cell = grid.GetCell(origin.x + x, origin.y + y);
                if (cell == null || !cell.Walkable ||
                    TerrainRules.IsConnectorCorridor(cell) ||
                    cell.TerrainType is not (TerrainType.FlatGround
                        or TerrainType.ElevatedGround) ||
                    (height >= 0 && cell.HeightLevel != height)) return false;
                height = cell.HeightLevel;
            }
            return true;
        }

        public bool CanPlace(BuildingType type, Vector2Int origin)
        {
            LastPlacementFailureReason = BuildingPlacementFailureReason.None;
            LastPlacementTrappedUnitCount = 0;
            LastPlacementSealedCellCount = 0;
            LastReachableBuilderSlotCount = 0;
            BuildingData data = GetData(type);
            if (data == null || grid == null || data.Footprint.x < 1 || data.Footprint.y < 1)
            {
                LastPlacementFailureReason = BuildingPlacementFailureReason.InvalidFootprint;
                return false;
            }
            int height = -1;
            for (int y = 0; y < data.Footprint.y; y++)
            for (int x = 0; x < data.Footprint.x; x++)
            {
                Vector2Int cellPosition = origin + new Vector2Int(x, y);
                GridCell cell = grid.GetCell(cellPosition.x, cellPosition.y);
                // Use the authored level: elevated courts can be higher than level 1.
                if (!IsValidPlacementCell(cell, height))
                {
                    LastPlacementFailureReason = BuildingPlacementFailureReason.InvalidFootprint;
                    return false;
                }
                height = cell.HeightLevel;
            }
            Vector3 first = grid.GridToWorld(origin.x, origin.y);
            Vector2 center = first + new Vector3(
                (data.Footprint.x - 1) * grid.NavigationData.CellSize * 0.5f,
                (data.Footprint.y - 1) * grid.NavigationData.CellSize * 0.5f, 0f);
            Vector2 size = new(data.Footprint.x * grid.NavigationData.CellSize,
                data.Footprint.y * grid.NavigationData.CellSize);
            foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, size, 0f))
            {
                ResourceNode resource = hit.GetComponent<ResourceNode>();
                if (resource != null && resource.IsAvailable
                    && !resource.IsSoftNavigationEntity)
                {
                    LastPlacementFailureReason = BuildingPlacementFailureReason.BlockedByResource;
                    return false;
                }
            }
            foreach (UnitController unit in UnitController.ActiveUnits)
            {
                if (unit == null || !unit.IsAlive) continue;
                Vector2Int cell = grid.WorldToGrid(unit.transform.position);
                if (cell.x >= origin.x && cell.x < origin.x + data.Footprint.x
                    && cell.y >= origin.y && cell.y < origin.y + data.Footprint.y)
                {
                    LastPlacementFailureReason = BuildingPlacementFailureReason.OccupiedByUnit;
                    return false;
                }
            }
            bool trapsUnits = trapValidator.WouldTrapFriendlyUnits(grid, owner, origin,
                data.Footprint, out int trappedUnits, out int sealedCells);
            LastPlacementSealedCellCount = sealedCells;
            if (trapsUnits)
            {
                LastPlacementFailureReason =
                    BuildingPlacementFailureReason.WouldTrapFriendlyUnits;
                LastPlacementTrappedUnitCount = trappedUnits;
                return false;
            }
            if (sealedCells > 0)
            {
                LastPlacementFailureReason =
                    BuildingPlacementFailureReason.WouldSealWalkablePocket;
                return false;
            }
            SetFootprint(origin, data.Footprint, true);
            try
            {
                LastReachableBuilderSlotCount = CountPotentialConstructionSlots(data, origin);
            }
            finally
            {
                SetFootprint(origin, data.Footprint, false);
            }
            if (LastReachableBuilderSlotCount == 0)
            {
                LastPlacementFailureReason =
                    BuildingPlacementFailureReason.NoConstructionAccess;
                return false;
            }
            return true;
        }

        public bool TryBuild(BuildingType type, Vector2Int origin,
            IReadOnlyList<PawnBuilder> candidates, out BuildingSite site)
            => TryBuild(type, HouseVariant.A, origin, candidates, out site);

        public bool TryBuild(BuildingType type, HouseVariant variant, Vector2Int origin,
            IReadOnlyList<PawnBuilder> candidates, out BuildingSite site)
        {
            site = null;
            if (!MatchManager.AllowsGameplay) return false;
            BuildingData data = GetData(type);
            if (!CanAfford(type) || !CanPlace(type, origin) || candidates == null
                || data.GetSprite(owner, variant) == null) return false;

            // Test access with the prospective footprint blocked, then restore it.
            SetFootprint(origin, data.Footprint, true);
            PawnBuilder chosen = null;
            float bestDistance = float.PositiveInfinity;
            foreach (PawnBuilder pawn in candidates)
            {
                if (!IsEligible(pawn) || !TryFindInteraction(data, origin, pawn, out Vector3 point))
                    continue;
                float distance = ((Vector2)(pawn.transform.position - point)).sqrMagnitude;
                if (distance < bestDistance)
                {
                    chosen = pawn;
                    bestDistance = distance;
                }
            }
            SetFootprint(origin, data.Footprint, false);
            if (chosen == null || !CanPlace(type, origin)) return false;
            if (!TryPlanSheepEscapes(origin, data.Footprint)) return false;
            if (!stockpile.Spend(data.FoodCost, data.WoodCost, data.GoldCost)) return false;

            var obj = new GameObject($"{owner}{type}{(type == BuildingType.House ? variant.ToString() : string.Empty)}");
            obj.transform.SetParent(transform, false);
            site = obj.AddComponent<BuildingSite>();
            site.Configure(data, owner, origin, grid, population, variant,
                stockpile, economyConfig);
            int assigned = AssignAll(site, candidates, false);
            if (assigned > 0)
            {
                EvacuatePlannedSheep();
                BuildingPlaced?.Invoke(site);
                return true;
            }

            // A route can become unavailable between the preflight and assignment.
            site.ReleaseOccupancy();
            Destroy(obj);
            if (data.FoodCost > 0) stockpile.Add(ResourceType.Food, data.FoodCost);
            if (data.WoodCost > 0) stockpile.Add(ResourceType.Wood, data.WoodCost);
            if (data.GoldCost > 0) stockpile.Add(ResourceType.Gold, data.GoldCost);
            site = null;
            return false;
        }

        private bool TryPlanSheepEscapes(Vector2Int origin, Vector2Int footprint)
        {
            sheepEscapePlans.Clear();
            reservedSheepEscapeCells.Clear();
            GridCell footprintCell = grid != null ? grid.GetCell(origin.x, origin.y) : null;
            if (footprintCell == null) return false;

            IReadOnlyList<ResourceNode> resources = ResourceNode.ActiveNodes;
            for (int i = 0; i < resources.Count; i++)
            {
                ResourceNode sheep = resources[i];
                if (sheep == null || !sheep.IsAvailable
                    || !sheep.IsSoftNavigationEntity) continue;
                Vector2Int logicalCell = sheep.OccupiedCell;
                Vector2Int physicalCell = grid.WorldToGrid(sheep.transform.position);
                if (!Contains(origin, footprint, logicalCell)
                    && !Contains(origin, footprint, physicalCell)) continue;
                if (sheep.GetComponent<SheepPresentation>() == null
                    || !TryFindSheepEscape(origin, footprint,
                        footprintCell.HeightLevel, sheep.transform.position,
                        out Vector2Int destination))
                {
                    sheepEscapePlans.Clear();
                    reservedSheepEscapeCells.Clear();
                    return false;
                }
                reservedSheepEscapeCells.Add(destination);
                sheepEscapePlans.Add(new SheepEscapePlan(sheep, destination));
            }
            return true;
        }

        private bool TryFindSheepEscape(Vector2Int origin, Vector2Int footprint,
            int heightLevel, Vector3 sheepPosition, out Vector2Int result)
        {
            result = default;
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            for (int ring = 1; ring <= SheepEscapeMaximumRing; ring++)
            {
                int minX = origin.x - ring;
                int maxX = origin.x + footprint.x - 1 + ring;
                int minY = origin.y - ring;
                int maxY = origin.y + footprint.y - 1 + ring;
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (x > minX && x < maxX && y > minY && y < maxY) continue;
                    Vector2Int candidate = new(x, y);
                    GridCell cell = grid.GetCell(x, y);
                    if (!TerrainRules.IsStableStandingCell(cell)
                        || cell.HeightLevel != heightLevel
                        || reservedSheepEscapeCells.Contains(candidate)
                        || ContainsLivingUnit(candidate)
                        || ContainsOtherSheep(candidate)) continue;
                    float distance = ((Vector2)(cell.WorldPosition
                        - sheepPosition)).sqrMagnitude;
                    if (found && distance >= bestDistance) continue;
                    found = true;
                    bestDistance = distance;
                    result = candidate;
                }
                if (found) return true;
            }
            return false;
        }

        private void EvacuatePlannedSheep()
        {
            for (int i = 0; i < sheepEscapePlans.Count; i++)
            {
                SheepEscapePlan plan = sheepEscapePlans[i];
                if (plan.Sheep == null || !plan.Sheep.IsAvailable) continue;
                IReadOnlyList<UnitController> units = UnitController.ActiveUnits;
                for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
                {
                    PawnGatherer gatherer = units[unitIndex] != null
                        ? units[unitIndex].GetComponent<PawnGatherer>() : null;
                    if (gatherer != null && gatherer.Target == plan.Sheep)
                        gatherer.CancelJob();
                }
                plan.Sheep.GetComponent<SheepPresentation>()
                    ?.TryEvacuateBuildingFootprint(plan.Destination);
            }
            sheepEscapePlans.Clear();
            reservedSheepEscapeCells.Clear();
        }

        private bool ContainsLivingUnit(Vector2Int candidate)
        {
            IReadOnlyList<UnitController> units = UnitController.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit != null && unit.IsAlive
                    && grid.WorldToGrid(unit.transform.position) == candidate) return true;
            }
            return false;
        }

        private static bool Contains(Vector2Int origin, Vector2Int size,
            Vector2Int point)
            => point.x >= origin.x && point.x < origin.x + size.x
                && point.y >= origin.y && point.y < origin.y + size.y;

        private static bool ContainsOtherSheep(Vector2Int candidate)
        {
            IReadOnlyList<ResourceNode> resources = ResourceNode.ActiveNodes;
            for (int i = 0; i < resources.Count; i++)
            {
                ResourceNode resource = resources[i];
                if (resource != null && resource.IsAvailable
                    && resource.IsSoftNavigationEntity
                    && resource.OccupiedCell == candidate) return true;
            }
            return false;
        }

        public bool CanStartConstruction(BuildingType type, Vector2Int origin,
            IReadOnlyList<PawnBuilder> candidates)
        {
            if (!MatchManager.AllowsGameplay) return false;
            BuildingData data = GetData(type);
            if (!CanAfford(type) || !CanPlace(type, origin) || candidates == null) return false;
            SetFootprint(origin, data.Footprint, true);
            bool reachable = false;
            foreach (PawnBuilder pawn in candidates)
                if (IsEligible(pawn) && TryFindInteraction(data, origin, pawn, out _))
                {
                    reachable = true;
                    break;
                }
            SetFootprint(origin, data.Footprint, false);
            return reachable;
        }

        public bool TryAssign(BuildingSite site, IReadOnlyList<PawnBuilder> candidates)
        {
            if (!MatchManager.AllowsGameplay || site == null || !site.IsAlive
                || site.Faction != owner
                || candidates == null || (site.State == BuildingState.Completed && !site.NeedsRepair))
                return false;
            return AssignAll(site, candidates, site.State == BuildingState.Completed) > 0;
        }

        private int AssignAll(BuildingSite site, IReadOnlyList<PawnBuilder> candidates, bool repair)
        {
            orderedBuilders.Clear();
            foreach (PawnBuilder pawn in candidates)
                if (IsEligible(pawn) && site.CanAssign(pawn)) orderedBuilders.Add(pawn);
            Vector3 sitePosition = site.transform.position;
            for (int i = 1; i < orderedBuilders.Count; i++)
            {
                PawnBuilder value = orderedBuilders[i];
                float valueDistance = ((Vector2)(value.transform.position - sitePosition))
                    .sqrMagnitude;
                int j = i - 1;
                while (j >= 0 && ComesAfter(orderedBuilders[j], value,
                    sitePosition, valueDistance))
                {
                    orderedBuilders[j + 1] = orderedBuilders[j];
                    j--;
                }
                orderedBuilders[j + 1] = value;
            }
            int assigned = 0;
            for (int i = 0; i < orderedBuilders.Count
                && site.FreeBuilderSlotCount > 0; i++)
            {
                PawnBuilder pawn = orderedBuilders[i];
                if (pawn.Assign(site, repair)) assigned++;
            }
            return assigned;
        }

        private static bool ComesAfter(PawnBuilder current, PawnBuilder candidate,
            Vector3 sitePosition, float candidateDistance)
        {
            float currentDistance = ((Vector2)(current.transform.position - sitePosition))
                .sqrMagnitude;
            return currentDistance > candidateDistance + 0.0001f
                || Mathf.Abs(currentDistance - candidateDistance) <= 0.0001f
                    && string.CompareOrdinal(current.name, candidate.name) > 0;
        }

        private bool IsEligible(PawnBuilder pawn)
        {
            UnitController unit = pawn != null ? pawn.GetComponent<UnitController>() : null;
            return unit != null && unit.IsAlive && unit.Faction == owner;
        }

        private bool TryFindInteraction(BuildingData data, Vector2Int origin,
            PawnBuilder pawn, out Vector3 point)
        {
            point = default;
            if (grid == null || paths == null) return false;
            Vector2Int start = grid.WorldToGrid(pawn.transform.position);
            int slotCount = ConstructionSlotUtility.SlotCount(data.Footprint);
            ulong tried = 0UL;
            for (int attempt = 0; attempt < slotCount && attempt < 64; attempt++)
            {
                int bestIndex = -1;
                Vector2Int bestTarget = default;
                Vector3 bestPoint = default;
                float bestDistance = float.PositiveInfinity;
                for (int candidateIndex = 0; candidateIndex < slotCount;
                    candidateIndex++)
                {
                    if ((tried & (1UL << candidateIndex)) != 0) continue;
                    if (!ConstructionSlotUtility.IsValid(grid, origin,
                        data.Footprint, candidateIndex)) continue;
                    Vector2Int target = ConstructionSlotUtility.CellAt(origin,
                        data.Footprint, candidateIndex);
                    GridCell cell = grid.GetCell(target.x, target.y);
                    Vector3 candidatePoint = cell.WorldPosition;
                    float distance = ((Vector2)(candidatePoint
                        - pawn.transform.position)).sqrMagnitude;
                    if (bestIndex >= 0 && distance >= bestDistance) continue;
                    bestIndex = candidateIndex;
                    bestTarget = target;
                    bestPoint = candidatePoint;
                    bestDistance = distance;
                }
                if (bestIndex < 0) return false;
                tried |= 1UL << bestIndex;
                if (paths.FindPath(start, bestTarget).Count == 0) continue;
                point = bestPoint;
                return true;
            }
            return false;
        }

        private int CountPotentialConstructionSlots(BuildingData data, Vector2Int origin)
        {
            int count = 0;
            int slotCount = ConstructionSlotUtility.SlotCount(data.Footprint);
            for (int slot = 0; slot < slotCount; slot++)
                if (ConstructionSlotUtility.IsValid(grid, origin,
                    data.Footprint, slot)) count++;
            return count;
        }

        private void SetFootprint(Vector2Int origin, Vector2Int size, bool occupied)
        {
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                grid.SetOccupied(origin.x + x, origin.y + y, occupied);
        }
    }
}
