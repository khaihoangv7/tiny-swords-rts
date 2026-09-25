using System.Collections.Generic;
using System;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Match;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    // Shared gameplay authority for player input and future computer commanders.
    public sealed class BuildingSystem : MonoBehaviour
    {
        private readonly Dictionary<BuildingType, BuildingData> definitions = new();
        private GridManager grid;
        private PathfindingSystem paths;
        private ResourceStockpile stockpile;
        private FactionPopulation population;
        private Faction owner;
        public event Action<BuildingSite> BuildingPlaced;

        public Faction Owner => owner;
        public GridManager Grid => grid;

        public bool IsReady => grid != null && paths != null && stockpile != null
            && HasValidData(BuildingType.House)
            && HasValidData(BuildingType.Barracks)
            && HasValidData(BuildingType.Tower)
            && HasValidData(BuildingType.Archery)
            && HasValidData(BuildingType.Monastery);

        public void Configure(GridManager navigation, ResourceStockpile resources,
            FactionPopulation population, IEnumerable<BuildingData> data)
        {
            grid = navigation;
            paths = grid != null ? grid.GetComponent<PathfindingSystem>() : null;
            stockpile = resources;
            this.population = population;
            owner = resources != null ? resources.Faction : Faction.Blue;
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

        public bool CanPlace(BuildingType type, Vector2Int origin)
        {
            BuildingData data = GetData(type);
            if (data == null || grid == null || data.Footprint.x < 1 || data.Footprint.y < 1)
                return false;
            int height = -1;
            for (int y = 0; y < data.Footprint.y; y++)
            for (int x = 0; x < data.Footprint.x; x++)
            {
                Vector2Int cellPosition = origin + new Vector2Int(x, y);
                GridCell cell = grid.GetCell(cellPosition.x, cellPosition.y);
                if (cell == null || cell.Occupied
                    || (cell.TerrainType != TerrainType.FlatGround
                        && cell.TerrainType != TerrainType.ElevatedGround)
                    || cell.HeightLevel != TerrainRules.DefaultHeight(cell.TerrainType)
                    || (height >= 0 && cell.HeightLevel != height)) return false;
                height = cell.HeightLevel;
            }
            Vector3 first = grid.GridToWorld(origin.x, origin.y);
            Vector2 center = first + new Vector3(
                (data.Footprint.x - 1) * grid.NavigationData.CellSize * 0.5f,
                (data.Footprint.y - 1) * grid.NavigationData.CellSize * 0.5f, 0f);
            Vector2 size = new(data.Footprint.x * grid.NavigationData.CellSize,
                data.Footprint.y * grid.NavigationData.CellSize);
            foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, size, 0f))
                if (hit.GetComponent<ResourceNode>()?.IsAvailable == true) return false;
            foreach (UnitController unit in UnitController.ActiveUnits)
            {
                if (unit == null || !unit.IsAlive) continue;
                Vector2Int cell = grid.WorldToGrid(unit.transform.position);
                if (cell.x >= origin.x && cell.x < origin.x + data.Footprint.x
                    && cell.y >= origin.y && cell.y < origin.y + data.Footprint.y)
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
            if (!stockpile.Spend(data.FoodCost, data.WoodCost, data.GoldCost)) return false;

            var obj = new GameObject($"{owner}{type}{(type == BuildingType.House ? variant.ToString() : string.Empty)}");
            obj.transform.SetParent(transform, false);
            site = obj.AddComponent<BuildingSite>();
            site.Configure(data, owner, origin, grid, population, variant);
            int assigned = AssignAll(site, candidates, false);
            if (assigned > 0)
            {
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
            var ordered = new List<PawnBuilder>();
            foreach (PawnBuilder pawn in candidates)
                if (IsEligible(pawn) && site.CanAssign(pawn)) ordered.Add(pawn);
            ordered.Sort((a, b) =>
            {
                float da = ((Vector2)(a.transform.position - site.transform.position)).sqrMagnitude;
                float db = ((Vector2)(b.transform.position - site.transform.position)).sqrMagnitude;
                int distance = da.CompareTo(db);
                return distance != 0 ? distance : string.CompareOrdinal(a.name, b.name);
            });
            int assigned = 0;
            foreach (PawnBuilder pawn in ordered)
                if (pawn.Assign(site, repair)) assigned++;
            return assigned;
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
            int footprintHeight = grid.GetCell(origin.x, origin.y).HeightLevel;
            int slotCount = data.Footprint.x * 2 + data.Footprint.y * 2;
            ulong tried = 0UL;
            for (int attempt = 0; attempt < slotCount && attempt < 64; attempt++)
            {
                int bestIndex = -1;
                Vector2Int bestTarget = default;
                Vector3 bestPoint = default;
                float bestDistance = float.PositiveInfinity;
                int index = 0;
                for (int y = -1; y <= data.Footprint.y; y++)
                for (int x = -1; x <= data.Footprint.x; x++)
                {
                    // Edge-adjacent cells only; no diagonal interaction through corners.
                    bool edge = (x == -1 || x == data.Footprint.x)
                            && y >= 0 && y < data.Footprint.y
                        || (y == -1 || y == data.Footprint.y)
                            && x >= 0 && x < data.Footprint.x;
                    if (!edge) continue;
                    int candidateIndex = index++;
                    if (candidateIndex >= 64) continue;
                    if ((tried & (1UL << candidateIndex)) != 0) continue;
                    Vector2Int target = origin + new Vector2Int(x, y);
                    GridCell cell = grid.GetCell(target.x, target.y);
                    if (cell == null || !cell.Walkable || cell.Occupied
                        || cell.HeightLevel != footprintHeight) continue;
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

        private void SetFootprint(Vector2Int origin, Vector2Int size, bool occupied)
        {
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                grid.SetOccupied(origin.x + x, origin.y + y, occupied);
        }
    }
}
