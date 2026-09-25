using System;
using System.Collections.Generic;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Match;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public sealed class BuildingGarrison : MonoBehaviour, IUnitStationHost
    {
        private sealed class PendingEntry
        {
            public UnitController Unit;
            public Vector3 Position;
            public bool Mount;
        }

        private readonly List<UnitController> internalOccupants = new();
        private readonly List<PendingEntry> pending = new();
        private readonly List<UnitController> ejectBuffer = new();
        private readonly List<Vector3> ejectPositions = new();
        private IDefensiveBuildingHost host;
        private MonoBehaviour hostBehaviour;
        private GridManager grid;
        private PathfindingSystem paths;
        private BuildingMountedArchers mounted;

        public int InternalCount => internalOccupants.Count;
        public int InternalCapacity => host?.DefenseData?.InternalGarrisonCapacity ?? 0;
        public int MountedCount => mounted != null ? mounted.Count : 0;
        public int MountedCapacity => mounted != null ? mounted.Capacity : 0;
        public Faction Faction => host != null ? host.Faction : Faction.Blue;
        public bool IsReady => host != null && host.IsDefenseReady;
        public float CurrentHP => host?.CurrentHP ?? 0f;
        public int MaxHP => host?.MaxHP ?? 0;
        public IReadOnlyList<UnitController> InternalOccupants => internalOccupants;
        public event Action Changed;
        public Transform StationTransform => transform;

        public void Configure(IDefensiveBuildingHost owner, GridManager navigation)
        {
            host = owner;
            hostBehaviour = owner as MonoBehaviour;
            grid = navigation;
            paths = grid != null ? grid.GetComponent<PathfindingSystem>() : null;
            mounted = GetComponent<BuildingMountedArchers>()
                ?? gameObject.AddComponent<BuildingMountedArchers>();
            mounted.Configure(owner);
            float baseY = owner.Footprint.y
                * (grid != null ? grid.NavigationData.CellSize : 1f) * 0.5f + 0.45f;
            BuildingPresentationProfile profile = BuildingPresentationProfiles.For(owner.Type);
            (GetComponent<BuildingGarrisonWorldCount>()
                ?? gameObject.AddComponent<BuildingGarrisonWorldCount>())
                .Configure(this, baseY + profile.GarrisonCountClearance);
            host.DefenseHostDestroyed += HandleHostDestroyed;
        }

        private void OnDestroy()
        {
            if (host != null) host.DefenseHostDestroyed -= HandleHostDestroyed;
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay)
            {
                if (pending.Count > 0) CancelAllPending();
                return;
            }
            bool occupantChanged = mounted != null && mounted.PruneInvalid();
            for (int i = internalOccupants.Count - 1; i >= 0; i--)
                if (internalOccupants[i] == null || !internalOccupants[i].IsAlive)
                {
                    internalOccupants.RemoveAt(i);
                    occupantChanged = true;
                }
            if (occupantChanged) Changed?.Invoke();
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingEntry entry = pending[i];
                if (entry.Unit == null || !entry.Unit.IsAlive || host == null
                    || !host.IsAlive || !host.IsDefenseReady)
                {
                    RemovePendingAt(i);
                    continue;
                }
                UnitMovement movement = entry.Unit.GetComponent<UnitMovement>();
                if (movement == null || movement.IsMoving) continue;
                if (Vector2.Distance(entry.Unit.transform.position, entry.Position) > 0.12f)
                {
                    RemovePendingAt(i);
                    continue;
                }
                pending.RemoveAt(i);
                UnitStationing stationing = entry.Unit.GetComponent<UnitStationing>()
                    ?? entry.Unit.gameObject.AddComponent<UnitStationing>();
                stationing.ClearPending(this);
                bool entered = entry.Mount
                    ? mounted.Mount(entry.Unit, this)
                    : stationing.EnterInternal(this);
                if (entered && !entry.Mount) internalOccupants.Add(entry.Unit);
                Changed?.Invoke();
            }
        }

        // Shared gameplay API for human input and future commander systems.
        public bool TryStation(UnitController unit)
        {
            if (!MatchManager.AllowsGameplay) return false;
            UnitType? type = unit != null
                ? unit.GetComponent<HealthComponent>()?.CombatData?.UnitType : null;
            if (type == UnitType.Archer && CanReserveMount())
                return TryMountArcher(unit);
            return TryGarrison(unit);
        }

        public bool TryGarrison(UnitController unit)
            => MatchManager.AllowsGameplay && CanReserveInternal()
                && QueueApproach(unit, false);

        public bool TryMountArcher(UnitController unit)
            => MatchManager.AllowsGameplay && CanReserveMount()
                && unit?.GetComponent<HealthComponent>()?.CombatData?.UnitType == UnitType.Archer
                && QueueApproach(unit, true);

        public bool TryUngarrison(UnitController unit)
        {
            if (!MatchManager.AllowsGameplay) return false;
            bool isInternal = unit != null && internalOccupants.Contains(unit);
            bool isMounted = unit != null && mounted != null && mounted.Contains(unit);
            if (!isInternal && !isMounted) return false;
            if (!BuildingSpawnPoint.TryFind(host, grid, out Vector3 position)) return false;
            if (isInternal) internalOccupants.Remove(unit);
            else mounted.Remove(unit);
            unit.GetComponent<UnitStationing>()?.RestoreToWorld(position);
            GetComponent<BuildingRallyPoint>()?.IssueRallyMove(unit);
            Changed?.Invoke();
            return true;
        }

        public int EjectAll() => MatchManager.AllowsGameplay ? EjectAllInternal(true) : 0;

        private int EjectAllInternal(bool followRally)
        {
            CancelAllPending();
            ejectBuffer.Clear();
            ejectBuffer.AddRange(internalOccupants);
            internalOccupants.Clear();
            mounted?.Drain(ejectBuffer);
            if (ejectBuffer.Count == 0) { Changed?.Invoke(); return 0; }
            int maximumRing = grid != null
                ? Mathf.Max(grid.NavigationData.Width, grid.NavigationData.Height) : 10;
            BuildingSpawnPoint.TryFindMany(host, grid, ejectBuffer.Count,
                ejectPositions, maximumRing);
            int restored = 0;
            for (int i = 0; i < ejectBuffer.Count && i < ejectPositions.Count; i++)
            {
                UnitController unit = ejectBuffer[i];
                if (unit == null) continue;
                unit.GetComponent<UnitStationing>()?.RestoreToWorld(ejectPositions[i]);
                restored++;
            }
            if (restored != ejectBuffer.Count)
                Debug.LogError($"Could not safely eject all units from {name}: "
                    + $"{restored}/{ejectBuffer.Count} valid positions.", this);
            if (followRally && restored > 0)
                GetComponent<BuildingRallyPoint>()?.IssueRallyMoves(ejectBuffer);
            Changed?.Invoke();
            return restored;
        }

        public void OnUnitEnteringInternal(UnitController unit)
        {
            if (host is not CastleDropOff castle || unit == null) return;
            unit.GetComponent<PawnGatherer>()?.DepositCargoOnGarrisonEntry(castle);
        }

        public void CancelPending(UnitController unit)
        {
            int index = pending.FindIndex(item => item.Unit == unit);
            if (index >= 0) RemovePendingAt(index);
        }

        private bool QueueApproach(UnitController unit, bool mount)
        {
            if (!IsEligible(unit) || !TryFindInteraction(unit, out Vector3 point)) return false;
            UnitMovement movement = unit.GetComponent<UnitMovement>();
            if (movement == null || !movement.TryIssueMove(point)) return false;
            unit.GetComponent<PawnGatherer>()?.CancelJob();
            unit.GetComponent<PawnBuilder>()?.CancelJob();
            var stationing = unit.GetComponent<UnitStationing>()
                ?? unit.gameObject.AddComponent<UnitStationing>();
            stationing.BeginPending(this);
            pending.Add(new PendingEntry { Unit = unit, Position = point, Mount = mount });
            Changed?.Invoke();
            return true;
        }

        private bool IsEligible(UnitController unit)
        {
            if (unit == null || host == null || hostBehaviour == null || !host.IsDefenseReady
                || !unit.IsAlive || !unit.IsTargetable || unit.Faction != host.Faction) return false;
            UnitType? type = unit.GetComponent<HealthComponent>()?.CombatData?.UnitType;
            if (type is not (UnitType.Pawn or UnitType.Warrior or UnitType.Archer or UnitType.Lancer))
                return false;
            UnitStationing stationing = unit.GetComponent<UnitStationing>();
            return stationing == null || (!stationing.IsStationed && !stationing.IsPending);
        }

        private bool CanReserveInternal()
            => host != null && internalOccupants.Count
                + pending.FindAll(item => !item.Mount).Count < InternalCapacity;

        private bool CanReserveMount()
            => host != null && mounted != null && mounted.Count
                + pending.FindAll(item => item.Mount).Count < MountedCapacity;

        private bool TryFindInteraction(UnitController unit, out Vector3 position)
        {
            position = default;
            if (grid == null || paths == null || host == null) return false;
            Vector2Int origin = host.FootprintOrigin;
            Vector2Int size = host.Footprint;
            GridCell footprint = grid.GetCell(origin.x, origin.y);
            if (footprint == null) return false;
            Vector2Int start = grid.WorldToGrid(unit.transform.position);
            float best = float.PositiveInfinity;
            for (int y = -1; y <= size.y; y++)
            for (int x = -1; x <= size.x; x++)
            {
                bool edge = (x == -1 || x == size.x) && y >= 0 && y < size.y
                    || (y == -1 || y == size.y) && x >= 0 && x < size.x;
                if (!edge) continue;
                Vector2Int candidate = origin + new Vector2Int(x, y);
                if (pending.Exists(item => grid.WorldToGrid(item.Position) == candidate)) continue;
                GridCell cell = grid.GetCell(candidate.x, candidate.y);
                if (cell == null || !cell.Walkable || cell.Occupied
                    || cell.HeightLevel != footprint.HeightLevel
                    || paths.FindPath(start, candidate).Count == 0) continue;
                float distance = ((Vector2)(cell.WorldPosition - unit.transform.position)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                position = cell.WorldPosition;
            }
            return best < float.PositiveInfinity;
        }

        private void RemovePendingAt(int index)
        {
            UnitController unit = pending[index].Unit;
            pending.RemoveAt(index);
            unit?.GetComponent<UnitStationing>()?.ClearPending(this);
            Changed?.Invoke();
        }

        private void CancelAllPending()
        {
            for (int i = pending.Count - 1; i >= 0; i--) RemovePendingAt(i);
        }

        private void HandleHostDestroyed()
        {
            host.ReleaseOccupancy();
            EjectAllInternal(false);
        }
    }
}
