using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Input;
using TinyKingdom.Match;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
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
        }
        public const int MaximumHP = 1500;
        public const int MaximumGold = 2000;
        public const int PawnCapacity = 3;
        public const int IncomePerPawn = 15;
        public const float IncomeSeconds = 5f;
        public const float FireThresholdRatio = 0.60f;
        public const float VisualScale = 1.30f;
        public const float VisualGroundOffsetY = 0.17f;
        public const float GuardVisualClearance = 2f;
        private static readonly List<GoldMineObjective> active = new();
        private readonly List<UnitController> occupants = new();
        private readonly List<PendingEntry> pending = new();
        private readonly List<Vector3> spawnPoints = new();
        private GridManager grid;
        private ResourceStockpile blue;
        private ResourceStockpile red;
        private EconomyFeedback feedback;
        private SpriteRenderer visual;
        private Collider2D interaction;
        private Phase6C2AssetCatalog assets;
        private BuildingData data;
        private Faction owner;
        private bool hasOwner;
        private int currentHP = MaximumHP;
        private int remainingGold = MaximumGold;
        private float incomeRemaining = IncomeSeconds;
        private GoldMineState state = GoldMineState.Neutral;
        private Vector2Int origin;
        private ResourceSelectionIndicator selectionIndicator;

        public static IReadOnlyList<GoldMineObjective> Active => active;
        public GoldMineState State => state;
        public bool HasOwner => hasOwner;
        public int RemainingGold => remainingGold;
        public int OccupantCount => occupants.Count;
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
            origin = gridOrigin; grid = navigation; blue = blueStockpile; red = redStockpile;
            feedback = economyFeedback; data = definition;
            assets = Phase6C2AssetCatalog.Load();
            currentHP = MaximumHP; remainingGold = MaximumGold; state = GoldMineState.Neutral;
            hasOwner = false; incomeRemaining = IncomeSeconds;
            visual = GetComponentInChildren<SpriteRenderer>();
            GroundSorting.Ensure(gameObject);
            interaction = GetComponent<Collider2D>();
            if (!TryGetComponent(out selectionIndicator))
                selectionIndicator = gameObject.AddComponent<ResourceSelectionIndicator>();
            selectionIndicator.Configure(1.7f, 0.5f, -0.65f);
            SetVisual(assets?.GoldMineInactive);
            for (int y = 0; y < Footprint.y; y++)
            for (int x = 0; x < Footprint.x; x++) grid?.SetOccupied(origin.x + x, origin.y + y, true);
            (GetComponent<BuildingCombatTarget>() ?? gameObject.AddComponent<BuildingCombatTarget>())
                .Configure(this);
            (GetComponent<BuildingDamagePresentation>()
                ?? gameObject.AddComponent<BuildingDamagePresentation>()).Configure(this);
            (GetComponent<BuildingHealthBar>() ?? gameObject.AddComponent<BuildingHealthBar>())
                .Configure(this, 1.05f);
            (GetComponent<BuildingGarrisonWorldCount>()
                ?? gameObject.AddComponent<BuildingGarrisonWorldCount>()).Configure(this, 1.45f);
        }

        private void OnEnable()
        {
            GroundSorting.Ensure(gameObject);
            if (!active.Contains(this)) active.Add(this);
        }
        private void OnDisable() => active.Remove(this);

        public void SetSelected(bool selected)
            => selectionIndicator?.SetSelected(selected && IsAlive);

        private void Update()
        {
            if (!MatchManager.AllowsGameplay || state is GoldMineState.Depleted or GoldMineState.Destroyed)
                return;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingEntry entry = pending[i];
                UnitController pawn = entry.Unit;
                if (!CanEnter(pawn, false)) { CancelPending(pawn); continue; }
                UnitMovement movement = pawn.GetComponent<UnitMovement>();
                if (movement != null && movement.IsMoving) continue;
                if (Vector2.Distance(pawn.transform.position, entry.Position) > 0.12f)
                { CancelPending(pawn); continue; }
                pending.RemoveAt(i);
                UnitStationing station = pawn.GetComponent<UnitStationing>()
                    ?? pawn.gameObject.AddComponent<UnitStationing>();
                station.ClearPending(this);
                if (!station.EnterInternal(this)) continue;
                occupants.Add(pawn);
                if (!hasOwner) { owner = pawn.Faction; hasOwner = true; state = GoldMineState.Owned; }
                RefreshVisual(); Changed?.Invoke();
            }
            occupants.RemoveAll(unit => unit == null || !unit.IsAlive);
            if (occupants.Count == 0) { incomeRemaining = IncomeSeconds; return; }
            incomeRemaining -= Time.deltaTime;
            if (incomeRemaining > 0f) return;
            incomeRemaining += IncomeSeconds;
            int payout = Mathf.Min(remainingGold, occupants.Count * IncomePerPawn);
            ResourceStockpile stockpile = owner == Faction.Blue ? blue : red;
            stockpile?.Add(ResourceType.Gold, payout);
            remainingGold -= payout;
            feedback?.Show(ResourceType.Gold, payout, transform.position + Vector3.up * 1.1f);
            Changed?.Invoke();
            if (remainingGold <= 0) Deplete();
        }

        public bool TryEnter(UnitController pawn)
        {
            if (!MatchManager.AllowsGameplay || !CanEnter(pawn, true)) return false;
            if (!TryFindEntry(pawn, out Vector3 position)) return false;
            UnitMovement movement = pawn.GetComponent<UnitMovement>();
            if (movement == null || !movement.TryIssueMove(position)) return false;
            pawn.GetComponent<PawnGatherer>()?.CancelJob();
            pawn.GetComponent<PawnBuilder>()?.CancelJob();
            pawn.GetComponent<MonkHealer>()?.CancelExplicitOrder();
            UnitStationing station = pawn.GetComponent<UnitStationing>()
                ?? pawn.gameObject.AddComponent<UnitStationing>();
            station.BeginPending(this);
            pending.Add(new PendingEntry { Unit = pawn, Position = position });
            return true;
        }

        public bool CanAccept(UnitController pawn) => CanEnter(pawn, true);

        public int EjectAll()
        {
            if (!MatchManager.AllowsGameplay || occupants.Count == 0) return 0;
            int maximumRing = grid != null ? Mathf.Max(grid.Width, grid.Height) : 10;
            BuildingSpawnPoint.TryFindMany(this, grid, occupants.Count, spawnPoints, maximumRing);
            int count = Mathf.Min(occupants.Count, spawnPoints.Count);
            for (int i = 0; i < count; i++)
                occupants[i]?.GetComponent<UnitStationing>()?.RestoreToWorld(spawnPoints[i]);
            occupants.RemoveRange(0, count);
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
            if (!FactionRelations.IsPlayable(pawn.Faction)) return false;
            if (hasOwner && pawn.Faction != owner) return false;
            UnitStationing station = pawn.GetComponent<UnitStationing>();
            if (station != null && (station.IsStationed || (includeCapacity && station.IsPending))) return false;
            return !includeCapacity || occupants.Count + pending.Count < PawnCapacity;
        }

        private bool TryFindEntry(UnitController pawn, out Vector3 position)
        {
            position = default;
            BuildingSpawnPoint.TryFindMany(this, grid, 12, spawnPoints, 3);
            float best = float.PositiveInfinity;
            foreach (Vector3 candidate in spawnPoints)
            {
                if (pending.Exists(item => Vector2.Distance(item.Position, candidate) < 0.1f))
                    continue;
                float distance = ((Vector2)(candidate - pawn.transform.position)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; position = candidate;
            }
            return best < float.PositiveInfinity;
        }

        private void Deplete()
        {
            state = GoldMineState.Depleted; hasOwner = false;
            EjectAllForced(); RefreshVisual(); DefenseHostHealthChanged?.Invoke(); Changed?.Invoke();
        }

        private void DestroyByCombat()
        {
            state = GoldMineState.Destroyed;
            foreach (UnitController pawn in occupants)
                pawn?.GetComponent<HealthComponent>()?.KillAt(transform.position);
            occupants.Clear();
            foreach (PendingEntry entry in pending.ToArray()) CancelPending(entry.Unit);
            SetVisual(assets?.GoldMineDestroyed);
            DefenseHostDestroyed?.Invoke(); Changed?.Invoke();
        }

        private void EjectAllForced()
        {
            bool allowed = MatchManager.AllowsGameplay;
            int maximumRing = grid != null ? Mathf.Max(grid.Width, grid.Height) : 10;
            BuildingSpawnPoint.TryFindMany(this, grid, occupants.Count, spawnPoints, maximumRing);
            for (int i = 0; i < occupants.Count && i < spawnPoints.Count; i++)
                occupants[i]?.GetComponent<UnitStationing>()?.RestoreToWorld(spawnPoints[i]);
            occupants.Clear();
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
