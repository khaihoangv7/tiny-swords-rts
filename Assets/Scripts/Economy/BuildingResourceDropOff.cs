using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Economy
{
    [DisallowMultipleComponent]
    public sealed class BuildingResourceDropOff : MonoBehaviour, IResourceDropOff
    {
        private readonly Dictionary<UnitController, Vector2Int> reservations = new();
        private BuildingSite site;
        private ResourceStockpile stockpile;
        private GridManager grid;
        private PathfindingSystem paths;
        private bool registered;

        public Faction Faction => site != null ? site.Faction : Faction.Blue;
        public bool IsAvailable => site != null && site.Type == BuildingType.Tower
            && site.IsAlive && site.State == BuildingState.Completed
            && stockpile != null && stockpile.Faction == site.Faction;
        public float ArrivalTolerance => 0.15f;
        public Vector3 WorldPosition => site != null ? site.transform.position : transform.position;
        public EntityId RuntimeId => this.GetEntityId();
        public int ReservationCount => reservations.Count;

        private void OnEnable()
        {
            if (site == null || registered) return;
            ResourceDropOffRegistry.Register(this);
            registered = true;
        }

        public void Configure(BuildingSite owner, ResourceStockpile resources,
            GridManager navigation)
        {
            site = owner;
            stockpile = resources;
            grid = navigation;
            paths = grid != null ? grid.GetComponent<PathfindingSystem>() : null;
            if (!registered)
            {
                ResourceDropOffRegistry.Register(this);
                registered = true;
            }
        }

        private void OnDisable()
        {
            if (registered) ResourceDropOffRegistry.Unregister(this);
            registered = false;
            reservations.Clear();
        }

        public bool IsValidFor(Faction workerFaction)
            => workerFaction == Faction && IsAvailable;

        public bool TryReserve(UnitController worker, GridManager navigation,
            PathfindingSystem pathfinder, IPathTraversalPolicy traversalPolicy,
            out Vector3 position)
        {
            position = default;
            if (worker == null || !IsValidFor(worker.Faction) || grid == null || paths == null)
                return false;
            if (reservations.TryGetValue(worker, out Vector2Int existing))
            {
                position = grid.GridToWorld(existing.x, existing.y);
                return true;
            }

            Vector2Int start = grid.WorldToGrid(worker.transform.position);
            GridCell footprintCell = grid.GetCell(site.Origin.x, site.Origin.y);
            if (footprintCell == null) return false;
            Vector2Int size = site.Footprint;
            int slotCount = size.x * 2 + size.y * 2;
            ulong tried = 0UL;
            for (int attempt = 0; attempt < slotCount && attempt < 64; attempt++)
            {
                int bestIndex = -1;
                Vector2Int bestCell = default;
                float bestDistance = float.PositiveInfinity;
                int index = 0;
                for (int y = -1; y <= size.y; y++)
                for (int x = -1; x <= size.x; x++)
                {
                    bool edge = ((x == -1 || x == size.x) && y >= 0 && y < size.y)
                        || ((y == -1 || y == size.y) && x >= 0 && x < size.x);
                    if (!edge) continue;
                    int candidateIndex = index++;
                    if (candidateIndex >= 64
                        || (tried & (1UL << candidateIndex)) != 0) continue;
                    Vector2Int candidate = site.Origin + new Vector2Int(x, y);
                    if (reservations.ContainsValue(candidate)) continue;
                    GridCell cell = grid.GetCell(candidate.x, candidate.y);
                    if (!TerrainRules.IsStableStandingCell(cell)
                        || cell.HeightLevel != footprintCell.HeightLevel) continue;
                    float distance = ((Vector2)(cell.WorldPosition
                        - worker.transform.position)).sqrMagnitude;
                    if (bestIndex >= 0 && distance >= bestDistance) continue;
                    bestIndex = candidateIndex;
                    bestCell = candidate;
                    bestDistance = distance;
                }
                if (bestIndex < 0) return false;
                tried |= 1UL << bestIndex;
                if (paths.FindPath(start, bestCell, traversalPolicy).Count == 0) continue;
                reservations.Add(worker, bestCell);
                position = grid.GridToWorld(bestCell.x, bestCell.y);
                return true;
            }
            return false;
        }

        public void Release(UnitController worker)
        {
            if (!ReferenceEquals(worker, null)) reservations.Remove(worker);
        }

        public bool Deposit(ResourceType type, int amount)
        {
            if (amount <= 0 || !IsAvailable) return false;
            stockpile.Add(type, amount);
            return true;
        }
    }
}
