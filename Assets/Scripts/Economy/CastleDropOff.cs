using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Buildings;
using TinyKingdom.Input;
using TinyKingdom.Presentation;
using UnityEngine;

namespace TinyKingdom.Economy
{
    public sealed class CastleDropOff : MonoBehaviour, IProductionHost, IFriendlyCursorTarget,
        IDefensiveBuildingHost, IResourceDropOff
    {
        public const int NavigationSidePadding = 1;
        private const int FootprintHalfWidth = 1;
        private const int FootprintHeight = 2;

        private static readonly List<CastleDropOff> Registered = new();
        // Three positions just outside the visible front edge of the Blue Castle.
        private static readonly Vector2[] FrontSlots =
        {
            new(-0.8f, -1.35f), new(0f, -1.35f), new(0.8f, -1.35f)
        };

        [SerializeField] private Faction faction = Faction.Blue;
        [SerializeField] private ResourceStockpile stockpile;
        [SerializeField, Range(0.05f, 0.3f)] private float arrivalTolerance = 0.15f;
        [SerializeField] private BuildingData defenseData;

        private readonly Dictionary<UnitController, int> reservations = new();
        private GridManager grid;
        private Vector2Int center;
        private bool registered;
        private float currentHP;

        public static IReadOnlyList<CastleDropOff> Active => Registered;
        public Faction Faction => faction;
        public BuildingType Type => BuildingType.Castle;
        public Vector2Int FootprintOrigin => center + new Vector2Int(-1, 0);
        public Vector2Int Footprint => new(3, 2);
        public Vector2Int NavigationFootprintOrigin => center
            + new Vector2Int(-FootprintHalfWidth - NavigationSidePadding, 0);
        public Vector2Int NavigationFootprint => new(
            FootprintHalfWidth * 2 + 1 + NavigationSidePadding * 2,
            FootprintHeight);
        public bool IsProductionReady => registered && IsAvailable && IsAlive;
        public bool IsDefenseReady => IsProductionReady;
        public float FireThreshold => BuildingDamagePresentation.FireThreshold;
        public BuildingData DefenseData => defenseData;
        public Transform HostTransform => transform;
        public Collider2D InteractionCollider => GetComponent<Collider2D>();
        public float CurrentHP => currentHP;
        public int MaxHP => defenseData != null ? defenseData.MaxHP : 0;
        public bool IsAlive => defenseData != null && currentHP > 0f;
        public SpriteRenderer PresentationRenderer
            => transform.Find("VisualRoot")?.GetComponent<SpriteRenderer>()
                ?? GetComponentInChildren<SpriteRenderer>();
        public ResourceStockpile Stockpile => stockpile;
        public float ArrivalTolerance => arrivalTolerance;
        public Vector3 WorldPosition => transform.position;
        public EntityId RuntimeId => this.GetEntityId();
        public int ReservationCount => reservations.Count;
        public bool IsAvailable => isActiveAndEnabled && stockpile != null
            && stockpile.Faction == faction && IsAlive;
        public event Action<ResourceType, int> Deposited;
        public event Action DefenseHostDestroyed;
        public event Action DefenseHostHealthChanged;

        private void Start()
        {
            grid = FindAnyObjectByType<GridManager>();
            if (grid == null) return;
            if (defenseData != null && currentHP <= 0f) currentHP = defenseData.MaxHP;
            center = grid.WorldToGrid(transform.position);
            SetNavigationOccupancy(true);
            registered = true;
        }

        private void OnEnable()
        {
            GroundSorting.Ensure(gameObject);
            if (!Registered.Contains(this)) Registered.Add(this);
            ResourceDropOffRegistry.Register(this);
        }

        private void OnDisable()
        {
            Registered.Remove(this);
            ResourceDropOffRegistry.Unregister(this);
            reservations.Clear();
            ReleaseOccupancy();
        }

        public void Configure(Faction owner, ResourceStockpile resources,
            BuildingData castleData = null)
        {
            faction = owner;
            stockpile = resources;
            defenseData = castleData;
            currentHP = castleData != null ? castleData.MaxHP : 0f;
            BuildingSelectionPulse.Ensure(gameObject, PresentationRenderer);
            WorldSelectionHitArea.Ensure(this, PresentationRenderer);
            GroundSorting.Ensure(gameObject, false,
                GroundSorting.GroundOffsetFromVisual(transform, PresentationRenderer));
            if (castleData != null)
                (GetComponent<BuildingVisionSource>()
                    ?? gameObject.AddComponent<BuildingVisionSource>()).Configure(this);
        }

        public bool IsFriendlyCursorTarget(Faction playerFaction)
            => faction == playerFaction && IsAvailable && IsAlive;

        public bool TryReserve(UnitController worker, GridManager navigation,
            PathfindingSystem paths, out Vector3 position)
            => TryReserve(worker, navigation, paths, null, out position);

        public bool TryReserve(UnitController worker, GridManager navigation,
            PathfindingSystem paths, IPathTraversalPolicy traversalPolicy,
            out Vector3 position)
        {
            position = default;
            if (worker == null || navigation == null || paths == null || !IsAvailable)
                return false;
            if (reservations.TryGetValue(worker, out int existing))
            {
                position = transform.position + (Vector3)FrontSlots[existing];
                return true;
            }

            Vector2Int start = navigation.WorldToGrid(worker.transform.position);
            int triedMask = 0;
            for (int attempt = 0; attempt < FrontSlots.Length; attempt++)
            {
                int bestIndex = -1;
                Vector3 bestPosition = default;
                Vector2Int bestCell = default;
                float bestDistance = float.PositiveInfinity;
                for (int i = 0; i < FrontSlots.Length; i++)
                {
                    if ((triedMask & (1 << i)) != 0 || reservations.ContainsValue(i)) continue;
                    Vector3 point = transform.position + (Vector3)FrontSlots[i];
                    Vector2Int cell = navigation.WorldToGrid(point);
                    GridCell destination = navigation.GetCell(cell.x, cell.y);
                    if (!TerrainRules.IsStableStandingCell(destination)
                        || !UnitSeparation.IsSafeSegment(navigation,
                            navigation.GridToWorld(cell.x, cell.y), point)) continue;
                    float distance = ((Vector2)(point - worker.transform.position)).sqrMagnitude;
                    if (bestIndex >= 0 && distance >= bestDistance) continue;
                    bestIndex = i;
                    bestPosition = point;
                    bestCell = cell;
                    bestDistance = distance;
                }
                if (bestIndex < 0) return false;
                triedMask |= 1 << bestIndex;
                if (paths.FindPath(start, bestCell, traversalPolicy).Count == 0) continue;
                reservations.Add(worker, bestIndex);
                position = bestPosition;
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
            Deposited?.Invoke(type, amount);
            return true;
        }

        public bool ReceiveBuildingDamage(int damage)
        {
            if (!IsAlive || damage <= 0) return false;
            currentHP = Mathf.Max(0f, currentHP - damage);
            DefenseHostHealthChanged?.Invoke();
            if (currentHP > 0f) return true;
            ReleaseOccupancy();
            DefenseHostDestroyed?.Invoke();
            SpriteRenderer visual = GetComponentInChildren<SpriteRenderer>();
            if (visual != null) visual.enabled = false;
            Collider2D hit = GetComponent<Collider2D>();
            if (hit != null) hit.enabled = false;
            Destroy(gameObject);
            return true;
        }

        public void ReleaseOccupancy()
        {
            if (!registered || grid == null) return;
            SetNavigationOccupancy(false);
            registered = false;
        }

        private void SetNavigationOccupancy(bool occupied)
        {
            // The gameplay footprint remains 3x2. One extra side cell on the
            // left and right protects the wider 1.7x Castle silhouette from
            // route clipping. The southern row remains open for drop-off and
            // garrison entry positions.
            int horizontal = FootprintHalfWidth + NavigationSidePadding;
            for (int y = 0; y < FootprintHeight; y++)
            for (int x = -horizontal; x <= horizontal; x++)
                grid.SetOccupied(center.x + x, center.y + y, occupied);
        }
    }
}
