using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Input;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.Economy
{
    public enum GoldMineState { Neutral, Owned, Depleted, Destroyed }

    public sealed class GoldMineObjective : MonoBehaviour, IDefensiveBuildingHost,
        IUnitStationHost, IFriendlyCursorTarget
    {
        private sealed class PendingEntry
        {
            public UnitController Unit;
            public Vector3 Position;
            public IPathTraversalPolicy TraversalPolicy;
            public readonly List<Vector3> TriedPositions = new();
        }
        private sealed class MiningOccupant
        {
            public UnitController Unit;
            public float WorkRemaining;
            public IPathTraversalPolicy TraversalPolicy;
        }
        public const int MaximumHP = 1500;
        public const int MaximumGold = 2000;
        public const int PawnCapacity = 3;
        public const float MineLoadMultiplier = 2.5f;
        public const float FireThresholdRatio = 0.60f;
        public const float VisualScale = 2.60f;
        public const float VisualGroundOffsetY = 0.92f;
        public const float GuardVisualClearance = 2f;
        public const float ContestRadius = 7f;
        private const float EntryStallSeconds = 2f;
        private const float EntryArrivalRadius = 0.35f;
        private const float ContestCheckSeconds = 0.5f;
        private static readonly ProfilerMarker ContestMarker =
            new("TinyKingdom.GoldMine.Control");
        private static readonly List<GoldMineObjective> active = new();
        private readonly List<MiningOccupant> occupants = new();
        private readonly List<UnitController> returningMiners = new();
        private readonly List<PendingEntry> pending = new();
        private readonly List<Vector3> spawnPoints = new();
        private GridManager grid;
        private SpriteRenderer visual;
        private Collider2D interaction;
        private Phase6C2AssetCatalog assets;
        private BuildingData data;
        private Faction owner;
        private bool hasOwner;
        private int currentHP = MaximumHP;
        private int remainingGold = MaximumGold;
        private float nextContestCheckTime;
        private float nextExitAttemptTime;
        private GoldMineState state = GoldMineState.Neutral;
        private Vector2Int origin;
        private SelectionCursorMarker selectionIndicator;

        public static IReadOnlyList<GoldMineObjective> Active => active;
        public GoldMineState State => state;
        public bool HasOwner => hasOwner;
        public int RemainingGold => remainingGold;
        public int OccupantCount => occupants.Count;
        public int PendingEntryCount => pending.Count;
        public BuildingType Type => BuildingType.GoldMine;
        public Faction Faction => hasOwner ? owner : TinyKingdom.Units.Faction.Goblin;
        public Vector2Int FootprintOrigin => origin;
        public Vector2Int Footprint => new(2, 2);
        public bool IsProductionReady => false;
        public BuildingData DefenseData => data;
        public Transform HostTransform => transform;
        public SpriteRenderer PresentationRenderer => visual;
        public Transform StationTransform => transform;
        public Collider2D InteractionCollider => interaction;
        public float CurrentHP => currentHP;
        public int MaxHP => MaximumHP;
        public bool IsAlive => state != GoldMineState.Destroyed && currentHP > 0;
        public bool IsSelected => selectionIndicator != null && selectionIndicator.IsSelected;
        public bool IsDefenseReady => state == GoldMineState.Owned && IsAlive;
        public float FireThreshold => FireThresholdRatio;
        public event Action DefenseHostDestroyed;
        public event Action DefenseHostHealthChanged;
        public event Action Changed;

        public void Configure(Vector2Int gridOrigin, GridManager navigation,
            ResourceStockpile blueStockpile, ResourceStockpile redStockpile,
            EconomyFeedback economyFeedback, BuildingData definition = null)
        {
            origin = gridOrigin; grid = navigation; data = definition;
            assets = Phase6C2AssetCatalog.Load();
            currentHP = MaximumHP; remainingGold = MaximumGold; state = GoldMineState.Neutral;
            hasOwner = false;
            occupants.Clear(); returningMiners.Clear(); pending.Clear();
            visual = GetComponentInChildren<SpriteRenderer>();
            GroundSorting.Ensure(gameObject, false,
                GroundSorting.GroundOffsetFromVisual(transform, visual));
            interaction = GetComponent<Collider2D>();
            if (!TryGetComponent(out selectionIndicator))
                selectionIndicator = gameObject.AddComponent<SelectionCursorMarker>();
            SetVisual(assets?.GoldMineInactive);
            float depth = Footprint.y * (grid?.NavigationData.CellSize ?? 1f);
            float width = Mathf.Max(3.4f, (visual?.bounds.size.x ?? 3.4f) + 0.22f);
            float frontY = visual != null
                ? visual.bounds.min.y - transform.position.y - 0.10f : -1.39f;
            float centerX = visual != null
                ? visual.bounds.center.x - transform.position.x : 0f;
            selectionIndicator.Configure(width, depth, frontY + depth * 0.5f,
                visual, 1.2f, true, centerX);
            WorldSelectionHitArea.Ensure(this, visual);
            for (int y = 0; y < Footprint.y; y++)
            for (int x = 0; x < Footprint.x; x++) grid?.SetOccupied(origin.x + x, origin.y + y, true);
            (GetComponent<BuildingCombatTarget>() ?? gameObject.AddComponent<BuildingCombatTarget>())
                .Configure(this);
            (GetComponent<BuildingDamagePresentation>()
                ?? gameObject.AddComponent<BuildingDamagePresentation>()).Configure(this);
            (GetComponent<BuildingHealthBar>() ?? gameObject.AddComponent<BuildingHealthBar>())
                .Configure(this, 2.55f);
            (GetComponent<BuildingGarrisonWorldCount>()
                ?? gameObject.AddComponent<BuildingGarrisonWorldCount>()).Configure(this, 2.95f);
        }

        private void OnEnable()
        {
            GroundSorting.Ensure(gameObject);
            if (!active.Contains(this)) active.Add(this);
        }
        private void OnDisable()
        {
            active.Remove(this);
            CancelReturningMiners();
        }

        public void SetSelected(bool selected)
            => selectionIndicator?.SetSelected(selected && IsAlive);

        private void Update()
        {
            if (!MatchManager.AllowsGameplay || state is GoldMineState.Depleted or GoldMineState.Destroyed)
                return;
            if (hasOwner && (occupants.Count > 0 || returningMiners.Count > 0)
                && Time.time >= nextContestCheckTime)
            {
                nextContestCheckTime = Time.time + ContestCheckSeconds;
                if (HasUnopposedMilitaryControl(OpponentOf(owner)))
                    EjectAll();
            }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingEntry entry = pending[i];
                UnitController pawn = entry.Unit;
                if (!CanEnter(pawn, false)) { CancelPending(pawn); continue; }
                UnitMovement movement = pawn.GetComponent<UnitMovement>();
                if (movement == null) { CancelPending(pawn); continue; }
                if (movement.IsMoving)
                {
                    if (Time.time - movement.LastMovementProgressTime
                        < EntryStallSeconds) continue;
                    RetryPendingEntry(i, entry, movement);
                    continue;
                }
                // The last movement frame can apply soft unit separation after
                // consuming the final waypoint. Accept only a Pawn that remains
                // inside its reserved standing cell and interaction radius.
                if (grid == null
                    || grid.WorldToGrid(pawn.transform.position)
                        != grid.WorldToGrid(entry.Position)
                    || Vector2.Distance(pawn.transform.position, entry.Position)
                        > EntryArrivalRadius)
                {
                    RetryPendingEntry(i, entry, movement);
                    continue;
                }
                pending.RemoveAt(i);
                UnitStationing station = pawn.GetComponent<UnitStationing>()
                    ?? pawn.gameObject.AddComponent<UnitStationing>();
                station.ClearPending(this);
                if (!station.EnterInternal(this)) continue;
                PawnGatherer gatherer = pawn.GetComponent<PawnGatherer>();
                occupants.Add(new MiningOccupant
                {
                    Unit = pawn,
                    WorkRemaining = MiningSeconds(gatherer),
                    TraversalPolicy = entry.TraversalPolicy
                });
                returningMiners.Remove(pawn);
                if (!hasOwner || owner != pawn.Faction)
                {
                    CancelReturningMiners();
                    owner = pawn.Faction; hasOwner = true; state = GoldMineState.Owned;
                }
                RefreshVisual(); Changed?.Invoke();
            }
            occupants.RemoveAll(entry => entry.Unit == null || !entry.Unit.IsAlive);
            for (int i = occupants.Count - 1; i >= 0 && remainingGold > 0; i--)
            {
                MiningOccupant entry = occupants[i];
                entry.WorkRemaining -= Time.deltaTime;
                if (entry.WorkRemaining > 0f || Time.time < nextExitAttemptTime) continue;
                BuildingSpawnPoint.TryFindMany(this, grid, 1, spawnPoints,
                    grid != null ? Mathf.Max(grid.Width, grid.Height) : 10);
                if (spawnPoints.Count == 0)
                {
                    nextExitAttemptTime = Time.time + 1f;
                    continue;
                }
                UnitController pawn = entry.Unit;
                PawnGatherer gatherer = pawn.GetComponent<PawnGatherer>();
                int load = Mathf.Min(remainingGold, MineGoldPerTrip(gatherer));
                occupants.RemoveAt(i);
                pawn.GetComponent<UnitStationing>()?.RestoreToWorld(spawnPoints[0]);
                remainingGold -= load;
                if (load > 0 && gatherer != null)
                {
                    returningMiners.Add(pawn);
                    gatherer.BeginMineDelivery(this, load, entry.TraversalPolicy);
                }
                RefreshVisual(); Changed?.Invoke();
            }
            if (remainingGold <= 0) Deplete();
        }

        public static int MineGoldPerTrip(PawnGatherer gatherer)
            => gatherer == null ? 0
                : MineGoldPerTrip(gatherer.CarryCapacity);

        public static int MineGoldPerTrip(int ordinaryCarryCapacity)
            => Mathf.RoundToInt(Mathf.Max(0, ordinaryCarryCapacity)
                * MineLoadMultiplier);

        public static float MiningSeconds(PawnGatherer gatherer)
            => gatherer == null ? 0f
                : MiningSeconds(gatherer.CarryCapacity,
                    gatherer.AmountPerGather, gatherer.GatherIntervalSeconds);

        public static float MiningSeconds(int ordinaryCarryCapacity,
            int amountPerGather, float gatherIntervalSeconds)
            => Mathf.CeilToInt((float)Mathf.Max(0, ordinaryCarryCapacity)
                / Mathf.Max(1, amountPerGather)) * Mathf.Max(0f, gatherIntervalSeconds);

        public void CancelReturnReservation(UnitController pawn)
        {
            if (pawn != null) returningMiners.Remove(pawn);
        }

        public bool TryEnter(UnitController pawn) => TryEnter(pawn, null);

        public bool TryEnter(UnitController pawn, IPathTraversalPolicy traversalPolicy)
        {
            if (!MatchManager.AllowsGameplay || !CanEnter(pawn, true)) return false;
            PawnGatherer gatherer = pawn.GetComponent<PawnGatherer>();
            if (gatherer.CarriedAmount > 0)
            {
                if (gatherer.MineReturnTarget == this) return true;
                if (!gatherer.QueueMineAfterCargo(this, traversalPolicy)) return false;
                if (!returningMiners.Contains(pawn)) returningMiners.Add(pawn);
                return true;
            }
            UnitMovement movement = pawn.GetComponent<UnitMovement>();
            var entry = new PendingEntry
            {
                Unit = pawn,
                TraversalPolicy = traversalPolicy
            };
            if (movement == null || !TryIssueEntryMove(pawn, movement,
                    traversalPolicy, entry.TriedPositions, out entry.Position))
                return false;
            gatherer.CancelJob();
            pawn.GetComponent<PawnBuilder>()?.CancelJob();
            pawn.GetComponent<MonkHealer>()?.CancelExplicitOrder();
            UnitStationing station = pawn.GetComponent<UnitStationing>()
                ?? pawn.gameObject.AddComponent<UnitStationing>();
            station.BeginPending(this);
            pending.Add(entry);
            return true;
        }

        public bool TryGetPendingEntry(UnitController pawn, out Vector3 position,
            out int attemptedPositions)
        {
            for (int i = 0; i < pending.Count; i++)
                if (pending[i].Unit == pawn)
                {
                    position = pending[i].Position;
                    attemptedPositions = pending[i].TriedPositions.Count;
                    return true;
                }
            position = default;
            attemptedPositions = 0;
            return false;
        }

        public bool CanAccept(UnitController pawn) => CanEnter(pawn, true);

        public int EjectAll()
        {
            if (!MatchManager.AllowsGameplay) return 0;
            if (occupants.Count == 0)
            {
                CancelReturningMiners();
                return 0;
            }
            int maximumRing = grid != null ? Mathf.Max(grid.Width, grid.Height) : 10;
            BuildingSpawnPoint.TryFindMany(this, grid, occupants.Count, spawnPoints, maximumRing);
            int count = Mathf.Min(occupants.Count, spawnPoints.Count);
            for (int i = 0; i < count; i++)
                occupants[i].Unit?.GetComponent<UnitStationing>()?.RestoreToWorld(spawnPoints[i]);
            occupants.RemoveRange(0, count);
            CancelReturningMiners();
            RefreshVisual(); Changed?.Invoke();
            return count;
        }

        public void CancelPending(UnitController unit)
        {
            if (unit == null) return;
            pending.RemoveAll(item => item.Unit == unit);
            unit.GetComponent<UnitStationing>()?.ClearPending(this);
        }

        public void OnUnitEnteringInternal(UnitController unit) { }

        public bool ReceiveBuildingDamage(int damage)
        {
            if (!IsDefenseReady || damage <= 0) return false;
            currentHP = Mathf.Max(0, currentHP - damage);
            DefenseHostHealthChanged?.Invoke();
            if (currentHP == 0) DestroyByCombat();
            return true;
        }

        public void ReleaseOccupancy() { }
        public bool IsFriendlyCursorTarget(Faction playerFaction)
            => state == GoldMineState.Neutral
                ? FogWorldVisibility.Instance == null
                    || FogWorldVisibility.Instance.CanObserveResource(transform.position)
                : state == GoldMineState.Owned && owner == playerFaction;

        private bool CanEnter(UnitController pawn, bool includeCapacity)
        {
            if (pawn == null || !pawn.IsAlive || !pawn.IsTargetable
                || state is GoldMineState.Depleted or GoldMineState.Destroyed) return false;
            if (pawn.GetComponent<HealthComponent>()?.CombatData?.UnitType != UnitType.Pawn) return false;
            if (pawn.GetComponent<PawnGatherer>() == null) return false;
            if (!FactionRelations.IsPlayable(pawn.Faction)) return false;
            if (hasOwner && pawn.Faction != owner)
            {
                if (occupants.Count > 0
                    || !HasUnopposedMilitaryControl(pawn.Faction)) return false;
            }
            else if (hasOwner && HasUnopposedMilitaryControl(OpponentOf(owner)))
                return false;
            UnitStationing station = pawn.GetComponent<UnitStationing>();
            if (station != null && (station.IsStationed || (includeCapacity && station.IsPending))) return false;
            int reservedReturns = hasOwner && owner == pawn.Faction
                ? returningMiners.Count - (returningMiners.Contains(pawn) ? 1 : 0)
                : 0;
            return !includeCapacity || occupants.Count + pending.Count + reservedReturns
                < PawnCapacity;
        }

        // This is gameplay authority, not AI knowledge. Both playable factions
        // obey the same control rule; strategic AI still uses FactionKnowledge.
        private bool HasUnopposedMilitaryControl(Faction challenger)
        {
            using var marker = ContestMarker.Auto();
            if (!FactionRelations.IsPlayable(challenger)) return false;
            bool challengerPresent = false;
            float radiusSquared = ContestRadius * ContestRadius;
            int mineHeight = grid?.GetCell(origin.x, origin.y)?.HeightLevel ?? 0;
            IReadOnlyList<UnitController> units = UnitController.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit == null || !unit.IsTargetable
                    || ((Vector2)(unit.transform.position - transform.position))
                        .sqrMagnitude > radiusSquared) continue;
                if (grid != null)
                {
                    Vector2Int cell = grid.WorldToGrid(unit.transform.position);
                    if (grid.GetCell(cell.x, cell.y)?.HeightLevel != mineHeight)
                        continue;
                }
                UnitType? type = unit.GetComponent<HealthComponent>()?.CombatData?.UnitType;
                if (type is not (UnitType.Warrior or UnitType.Lancer
                    or UnitType.Archer)) continue;
                if (unit.Faction == challenger) challengerPresent = true;
                else if (FactionRelations.IsPlayable(unit.Faction)) return false;
            }
            return challengerPresent;
        }

        private static Faction OpponentOf(Faction faction)
            => faction == Faction.Blue ? Faction.Red : Faction.Blue;

        private void RetryPendingEntry(int index, PendingEntry entry,
            UnitMovement movement)
        {
            pending.RemoveAt(index);
            UnitStationing station = entry.Unit.GetComponent<UnitStationing>();
            station?.ClearPending(this);
            movement.Stop();
            if (entry.TriedPositions.Count >= 12
                || !TryIssueEntryMove(entry.Unit, movement, entry.TraversalPolicy,
                    entry.TriedPositions, out Vector3 position))
                return;
            entry.Position = position;
            station?.BeginPending(this);
            pending.Add(entry);
        }

        private bool TryIssueEntryMove(UnitController pawn, UnitMovement movement,
            IPathTraversalPolicy traversalPolicy, List<Vector3> triedPositions,
            out Vector3 position)
        {
            position = default;
            BuildingSpawnPoint.TryFindMany(this, grid, 12, spawnPoints, 3);
            // A blocked nearest edge must not strand a miner when another mine
            // entrance is reachable. Keep the number of path probes bounded.
            uint tried = 0;
            for (int attempt = 0; attempt < Mathf.Min(4, spawnPoints.Count)
                && triedPositions.Count < 12; attempt++)
            {
                int bestIndex = -1;
                float bestDistance = float.PositiveInfinity;
                for (int i = 0; i < spawnPoints.Count; i++)
                {
                    if ((tried & (1u << i)) != 0) continue;
                    Vector3 candidate = spawnPoints[i];
                    if (triedPositions.Exists(item => Vector2.Distance(item,
                            candidate) < 0.1f)) continue;
                    if (pending.Exists(item => Vector2.Distance(item.Position,
                            candidate) < 0.1f)) continue;
                    float distance = ((Vector2)(candidate
                        - pawn.transform.position)).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestIndex = i;
                }
                if (bestIndex < 0) return false;
                tried |= 1u << bestIndex;
                position = spawnPoints[bestIndex];
                triedPositions.Add(position);
                if (traversalPolicy == null
                    ? movement.TryIssueMove(position)
                    : movement.TryIssueStrategicMove(position, traversalPolicy))
                    return true;
            }
            return false;
        }

        private void Deplete()
        {
            state = GoldMineState.Depleted; hasOwner = false;
            for (int i = pending.Count - 1; i >= 0; i--)
                CancelPending(pending[i].Unit);
            EjectAllForced(); CancelReturningMiners(); RefreshVisual();
            DefenseHostHealthChanged?.Invoke(); Changed?.Invoke();
        }

        private void DestroyByCombat()
        {
            state = GoldMineState.Destroyed;
            foreach (MiningOccupant entry in occupants)
                entry.Unit?.GetComponent<HealthComponent>()?.KillAt(transform.position);
            occupants.Clear();
            CancelReturningMiners();
            foreach (PendingEntry entry in pending.ToArray()) CancelPending(entry.Unit);
            SetVisual(assets?.GoldMineDestroyed);
            DefenseHostDestroyed?.Invoke(); Changed?.Invoke();
        }

        private void EjectAllForced()
        {
            int maximumRing = grid != null ? Mathf.Max(grid.Width, grid.Height) : 10;
            BuildingSpawnPoint.TryFindMany(this, grid, occupants.Count, spawnPoints, maximumRing);
            for (int i = 0; i < occupants.Count && i < spawnPoints.Count; i++)
                occupants[i].Unit?.GetComponent<UnitStationing>()?.RestoreToWorld(spawnPoints[i]);
            occupants.Clear();
        }

        private void CancelReturningMiners()
        {
            for (int i = returningMiners.Count - 1; i >= 0; i--)
                returningMiners[i]?.GetComponent<PawnGatherer>()?.CancelMineRepeat();
            returningMiners.Clear();
        }

        private void RefreshVisual()
        {
            Sprite sprite = state is GoldMineState.Depleted or GoldMineState.Destroyed
                ? assets?.GoldMineDestroyed
                : occupants.Count > 0 ? assets?.GoldMineActive : assets?.GoldMineInactive;
            SetVisual(sprite);
        }

        private void SetVisual(Sprite sprite)
        {
            if (visual != null && sprite != null) visual.sprite = sprite;
        }
    }
}
