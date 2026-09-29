using System.Collections.Generic;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using TinyKingdom.Match;
using TinyKingdom.Economy;
using TinyKingdom.Presentation;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    // Per-building gameplay destination plus a selection-only presentation marker.
    public sealed class BuildingRallyPoint : MonoBehaviour
    {
        private const int CandidateCount = 12;
        private const int CandidateRadius = 6;
        private readonly HashSet<GridCell> assigned = new();
        private IProductionHost owner;
        private Component ownerComponent;
        private GridManager grid;
        private PathfindingSystem paths;
        private SelectionManager selection;
        private GameObject marker;
        private Vector2Int rallyCell;
        private int nextDestinationIndex;

        public bool HasRallyPoint { get; private set; }
        public IProductionHost ProductionHost => owner;
        public Vector2Int RallyCell => rallyCell;
        public Vector3 RallyWorldPosition => grid != null
            ? grid.GridToWorld(rallyCell.x, rallyCell.y) : transform.position;
        public Sprite FlagSprite => marker != null
            ? marker.GetComponent<SpriteRenderer>()?.sprite : null;
        public int MarkerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < transform.childCount; i++)
                    if (transform.GetChild(i).name == "RallyFlagMarker") count++;
                return count;
            }
        }

        public static bool Supports(BuildingType type)
            => type is BuildingType.Castle or BuildingType.Barracks or BuildingType.Tower
                or BuildingType.Archery or BuildingType.Monastery;

        public void Configure(BuildingSite owner, GridManager navigation,
            SelectionManager selectionManager)
            => ConfigureOwner(owner, owner, navigation, selectionManager);

        public void Configure(CastleDropOff owner, GridManager navigation,
            SelectionManager selectionManager)
            => ConfigureOwner(owner, owner, navigation, selectionManager);

        private void ConfigureOwner(Component component, IProductionHost productionHost,
            GridManager navigation, SelectionManager selectionManager)
        {
            if (selection != null) selection.SelectionChanged -= RefreshMarker;
            owner = productionHost;
            ownerComponent = component;
            grid = navigation;
            paths = grid != null ? grid.GetComponent<PathfindingSystem>() : null;
            selection = selectionManager;
            if (selection != null) selection.SelectionChanged += RefreshMarker;
            EnsureMarker();
            RefreshMarker();
        }

        private void OnDestroy()
        {
            if (selection != null) selection.SelectionChanged -= RefreshMarker;
        }

        public bool CanSetRallyPoint(Vector3 worldPosition)
            => selection != null && CanSetRallyPoint(selection.PlayerFaction, worldPosition);

        public bool CanSetRallyPoint(Faction issuingFaction, Vector3 worldPosition)
        {
            if (!MatchManager.AllowsGameplay || owner == null || ownerComponent == null
                || grid == null || paths == null || !owner.IsProductionReady
                || owner.Faction != issuingFaction)
                return false;

            Vector2Int destination = grid.WorldToGrid(worldPosition);
            GridCell cell = grid.GetCell(destination.x, destination.y);
            if (cell == null || !cell.Walkable) return false;

            TeamVisionSystem vision = TeamVisionSystem.Instance;
            if (vision == null || !vision.IsReady
                || vision.GetVisionState(owner.Faction, destination) == VisionState.Unexplored)
                return false;

            if (cell.Occupied && FogMemorySystem.Instance != null
                && FogMemorySystem.Instance.IsKnownOccupied(
                    owner.Faction, destination)) return false;

            return HasReachablePath(destination);
        }

        public bool TrySetRallyPoint(Vector3 worldPosition)
            => selection != null && TrySetRallyPoint(selection.PlayerFaction, worldPosition);

        public bool TrySetRallyPoint(Faction issuingFaction, Vector3 worldPosition)
        {
            if (!CanSetRallyPoint(issuingFaction, worldPosition)) return false;
            rallyCell = grid.WorldToGrid(worldPosition);
            HasRallyPoint = true;
            nextDestinationIndex = 0;
            EnsureMarker();
            marker.transform.position = RallyWorldPosition + new Vector3(0f, 0f, -0.2f);
            RefreshMarker();
            return true;
        }

        public bool IssueRallyMove(UnitController unit)
        {
            if (!MatchManager.AllowsGameplay || unit == null) return false;
            return IssueRallyMoves(new[] { unit }) > 0;
        }

        public int IssueRallyMoves(IReadOnlyList<UnitController> units)
        {
            if (!MatchManager.AllowsGameplay || !HasRallyPoint || grid == null
                || units == null || units.Count == 0)
                return 0;
            List<GridCell> candidates = DestinationDistributor.FindNearbyDestinations(
                grid, rallyCell, Mathf.Max(CandidateCount, units.Count * 4), CandidateRadius,
                true);
            if (candidates.Count == 0) return 0;

            assigned.Clear();
            int issued = 0;
            int cursor = nextDestinationIndex % candidates.Count;
            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                UnitController unit = units[unitIndex];
                UnitMovement movement = unit != null ? unit.GetComponent<UnitMovement>() : null;
                if (movement == null || !unit.IsAlive || unit.Faction != owner.Faction) continue;
                for (int offset = 0; offset < candidates.Count; offset++)
                {
                    int index = (cursor + offset) % candidates.Count;
                    GridCell candidate = candidates[index];
                    if (assigned.Contains(candidate)
                        || !movement.TryIssueMove(new Vector2Int(candidate.GridX, candidate.GridY)))
                        continue;
                    assigned.Add(candidate);
                    cursor = (index + 1) % candidates.Count;
                    issued++;
                    break;
                }
            }
            nextDestinationIndex = cursor;
            return issued;
        }

        private void EnsureMarker()
        {
            if (marker != null) return;
            marker = new GameObject("RallyFlagMarker");
            marker.transform.SetParent(ownerComponent != null ? ownerComponent.transform : transform,
                true);
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = PresentationAssetCatalog.Load()?.RallyFlag;
            renderer.sortingOrder = 25;
            renderer.color = Color.white;
            marker.SetActive(false);
        }

        private bool HasReachablePath(Vector2Int destination)
        {
            Vector2Int origin = owner.FootprintOrigin;
            Vector2Int size = owner.Footprint;
            GridCell footprint = grid.GetCell(origin.x, origin.y);
            if (footprint == null) return false;
            List<GridCell> destinations = DestinationDistributor.FindNearbyDestinations(
                grid, destination, CandidateCount, CandidateRadius, true);
            if (destinations.Count == 0) return false;
            for (int ring = 1; ring <= CandidateRadius; ring++)
            {
                for (int x = origin.x; x < origin.x + size.x; x++)
                {
                    if (CanReachAny(new Vector2Int(x, origin.y - ring), footprint, destinations)
                        || CanReachAny(new Vector2Int(x, origin.y + size.y - 1 + ring),
                            footprint, destinations)) return true;
                }
                for (int y = origin.y; y < origin.y + size.y; y++)
                {
                    if (CanReachAny(new Vector2Int(origin.x - ring, y), footprint, destinations)
                        || CanReachAny(new Vector2Int(origin.x + size.x - 1 + ring, y),
                            footprint, destinations)) return true;
                }
            }
            return false;
        }

        private bool CanReachAny(Vector2Int start, GridCell footprint,
            IReadOnlyList<GridCell> destinations)
        {
            for (int i = 0; i < destinations.Count; i++)
                if (CanReach(start, footprint,
                    new Vector2Int(destinations[i].GridX, destinations[i].GridY))) return true;
            return false;
        }

        private bool CanReach(Vector2Int start, GridCell footprint, Vector2Int destination)
        {
            GridCell cell = grid.GetCell(start.x, start.y);
            return cell != null && cell.Walkable && !cell.Occupied
                && cell.HeightLevel == footprint.HeightLevel
                && paths.FindPath(start, destination).Count > 0;
        }

        private void RefreshMarker()
        {
            if (marker == null) return;
            bool selected = HasRallyPoint && selection != null
                && ((ownerComponent is BuildingSite building
                        && selection.SelectedBuilding == building)
                    || (ownerComponent is CastleDropOff castle
                        && selection.SelectedCastle == castle));
            marker.SetActive(selected);
            if (selected)
            {
                Sprite sprite = marker.GetComponent<SpriteRenderer>()?.sprite;
                float halfHeight = sprite != null
                    ? sprite.rect.height / sprite.pixelsPerUnit * 0.5f : 0f;
                marker.transform.position = RallyWorldPosition
                    + new Vector3(0f, halfHeight, -0.2f);
            }
        }
    }
}
