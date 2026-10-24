using System;
using System.Collections.Generic;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Match;
using TinyKingdom.Audio;
using TinyKingdom.Presentation;
using TinyKingdom.Economy;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public enum ConstructionRecoveryState
    {
        Stable,
        WaitingForProgress,
        ReturningBuilders,
        AccessFailure
    }

    public sealed class BuildingSite : MonoBehaviour, IFriendlyCursorTarget, IProductionHost,
        IDefensiveBuildingHost
    {
        private static readonly List<BuildingSite> RegisteredSites = new();
        public static IReadOnlyList<BuildingSite> ActiveSites => RegisteredSites;
        public const float ConstructionStallSeconds = 3f;
        public const float RecoveryCooldownSeconds = 2f;
        private BuildingData data;
        private GridManager grid;
        private FactionPopulation population;
        private SpriteRenderer visual;
        private Transform constructionVisualRoot;
        private static Sprite pixelSprite;
        private SpriteRenderer progressBackground;
        private SpriteRenderer progressFill;
        private GameObject progressBarRoot;
        private Vector2Int origin;
        private float constructionElapsed;
        private float currentHP;
        private bool occupied;
        private bool bonusApplied;
        private bool completionAudioPlayed;
        private readonly HashSet<PawnBuilder> assignedBuilders = new();
        private readonly List<PawnBuilder> staleBuilders = new();
        private BuildingInteractionSlots interactionSlots;
        private float lastProgressTime;
        private float nextRecoveryTime;
        [SerializeField] private bool drawBuilderSlots = false;

        public BuildingData Data => data;
        public BuildingType Type => data.Type;
        public HouseVariant Variant { get; private set; }
        public BuildingState State { get; private set; } = BuildingState.UnderConstruction;
        public Faction Faction { get; private set; }
        public Vector2Int Origin => origin;
        public Vector2Int FootprintOrigin => origin;
        public Vector2Int Footprint => data.Footprint;
        public bool IsProductionReady => IsAlive && State == BuildingState.Completed;
        public bool IsDefenseReady => IsProductionReady;
        public float FireThreshold => BuildingDamagePresentation.FireThreshold;
        public BuildingData DefenseData => data;
        public Transform HostTransform => transform;
        public Collider2D InteractionCollider => GetComponent<Collider2D>();
        public float ConstructionProgress => data == null ? 0f : Mathf.Clamp01(constructionElapsed / data.ConstructionSeconds);
        public float CurrentHP => currentHP;
        public int MaxHP => data.MaxHP;
        public bool IsAlive => currentHP > 0f;
        public bool NeedsRepair => State == BuildingState.Completed && currentHP < data.MaxHP;
        public SpriteRenderer PresentationRenderer => visual;
        public int ActiveBuilderCount => assignedBuilders.Count;
        public int ReservedBuilderSlotCount => interactionSlots != null
            ? interactionSlots.ReservationCount : 0;
        public int ReachableBuilderSlotCount => interactionSlots != null
            ? interactionSlots.ValidSlotCount : 0;
        public int FreeBuilderSlotCount => interactionSlots != null
            ? interactionSlots.FreeSlotCount : 0;
        public int WorkingBuilderCount
        {
            get
            {
                int count = 0;
                foreach (PawnBuilder builder in assignedBuilders)
                    if (builder != null && builder.IsWorking) count++;
                return count;
            }
        }
        public float LastProgressTime => lastProgressTime;
        public ConstructionRecoveryState RecoveryState { get; private set; }
        public string LastRecoveryReason { get; private set; } = "None";
        public float LastRecoveryTime { get; private set; } = -1f;
        public uint BuilderCommandRequests { get; private set; }
        public uint DeduplicatedBuilderCommands { get; private set; }
        public uint BuilderPathRequests { get; private set; }
        public static long TotalBuilderCommandRequests { get; private set; }
        public static long TotalDeduplicatedBuilderCommands { get; private set; }
        public static long TotalBuilderPathRequests { get; private set; }
        public bool CanAssign(PawnBuilder pawn) => pawn != null && !assignedBuilders.Contains(pawn);
        public event Action<BuildingSite> Changed;
        public event Action<BuildingSite> Completed;
        public event Action DefenseHostDestroyed;
        public event Action DefenseHostHealthChanged;

        private void OnEnable()
        {
            if (!RegisteredSites.Contains(this)) RegisteredSites.Add(this);
            GroundSorting.Ensure(gameObject);
        }

        private void OnDisable()
        {
            RegisteredSites.Remove(this);
            ResetConstructionVisual();
        }

        public void Configure(BuildingData definition, Faction owner, Vector2Int gridOrigin,
            GridManager navigation, FactionPopulation ownerPopulation,
            HouseVariant variant = HouseVariant.A, ResourceStockpile ownerResources = null,
            EconomyConfig economyConfig = null)
        {
            data = definition;
            Faction = owner;
            origin = gridOrigin;
            grid = navigation;
            population = ownerPopulation;
            Variant = variant;
            currentHP = 1f;
            lastProgressTime = Time.time;
            nextRecoveryTime = 0f;
            RecoveryState = ConstructionRecoveryState.Stable;
            LastRecoveryReason = "None";
            completionAudioPlayed = false;
            Vector3 first = grid.GridToWorld(origin.x, origin.y);
            transform.position = first + new Vector3((data.Footprint.x - 1) * grid.NavigationData.CellSize * 0.5f,
                (data.Footprint.y - 1) * grid.NavigationData.CellSize * 0.5f, 0f);
            var pulseRoot = new GameObject("ConstructionVisualRoot");
            constructionVisualRoot = pulseRoot.transform;
            constructionVisualRoot.SetParent(transform, false);
            var child = new GameObject("VisualRoot");
            child.transform.SetParent(constructionVisualRoot, false);
            visual = child.AddComponent<SpriteRenderer>();
            visual.sprite = data.GetSprite(owner, variant);
            visual.sortingOrder = 9;
            BuildingVisualPresentation.Apply(visual, data.Type, data.VisualOffset);
            BuildingSelectionPulse.Ensure(gameObject, visual);
            WorldSelectionHitArea.Ensure(this, visual);
            visual.color = new Color(1f, 1f, 1f, 0.55f);
            GroundSorting.Ensure(gameObject, false,
                GroundSorting.GroundOffsetFromVisual(transform, visual));
            MakeBar();
            (GetComponent<BuildingHealthBar>()
                ?? gameObject.AddComponent<BuildingHealthBar>()).Configure(this,
                    BuildingVisualPresentation.TopLocalY(visual) + 0.3f);
            (GetComponent<BuildingDamagePresentation>()
                ?? gameObject.AddComponent<BuildingDamagePresentation>()).Configure(this);
            BoxCollider2D hit = gameObject.AddComponent<BoxCollider2D>();
            hit.size = new Vector2(data.Footprint.x * grid.NavigationData.CellSize,
                data.Footprint.y * grid.NavigationData.CellSize);
            for (int y = 0; y < data.Footprint.y; y++)
            for (int x = 0; x < data.Footprint.x; x++)
                grid.SetOccupied(origin.x + x, origin.y + y, true);
            occupied = true;
            interactionSlots = gameObject.AddComponent<BuildingInteractionSlots>();
            interactionSlots.Configure(this, grid);
            if (data.Type == BuildingType.Tower)
                (GetComponent<BuildingResourceDropOff>()
                    ?? gameObject.AddComponent<BuildingResourceDropOff>()).Configure(
                        this, ownerResources, grid);
            else if (data.Type == BuildingType.House && economyConfig != null)
                (GetComponent<HouseSheepSpawner>()
                    ?? gameObject.AddComponent<HouseSheepSpawner>()).Configure(
                        this, grid, economyConfig, PresentationAssetCatalog.Load());
            (GetComponent<BuildingVisionSource>()
                ?? gameObject.AddComponent<BuildingVisionSource>()).Configure(this);
            if (BuildingRallyPoint.Supports(data.Type))
                (GetComponent<BuildingRallyPoint>()
                    ?? gameObject.AddComponent<BuildingRallyPoint>()).Configure(
                        this, grid, FindAnyObjectByType<SelectionManager>());
            UpdateBar();
            Changed?.Invoke(this);
            DefenseHostHealthChanged?.Invoke();
        }

        public void AdvanceConstruction(float seconds)
        {
            if (!MatchManager.AllowsGameplay || State != BuildingState.UnderConstruction
                || !IsAlive) return;
            float before = constructionElapsed;
            constructionElapsed = Mathf.Min(data.ConstructionSeconds,
                constructionElapsed + Mathf.Max(0f, seconds));
            if (constructionElapsed > before)
            {
                lastProgressTime = Time.time;
                RecoveryState = ConstructionRecoveryState.Stable;
                LastRecoveryReason = "Progressing";
            }
            currentHP = Mathf.Max(1f, data.MaxHP * ConstructionProgress);
            if (constructionElapsed >= data.ConstructionSeconds)
            {
                State = BuildingState.Completed;
                ResetConstructionVisual();
                currentHP = data.MaxHP;
                visual.color = Color.white;
                if (!completionAudioPlayed)
                {
                    completionAudioPlayed = true;
                    GameplayAudioService.Request(GameplayAudioEvent.BuildingCompleted,
                        transform.position, Faction);
                }
                if (!bonusApplied && data.PopulationBonus > 0)
                {
                    population?.AddCap(data.PopulationBonus);
                    bonusApplied = true;
                }
                Completed?.Invoke(this);
            }
            UpdateBar();
            Changed?.Invoke(this);
            DefenseHostHealthChanged?.Invoke();
        }

        private void LateUpdate()
        {
            // Fog gating and gameplay bar activation are independent. Reapply the
            // authoritative construction/health state after all Update work.
            UpdateBar();
            UpdateConstructionVisual();
        }

        private void UpdateConstructionVisual()
        {
            if (constructionVisualRoot == null) return;
            if (State != BuildingState.UnderConstruction || !IsAlive)
            {
                ResetConstructionVisual();
                return;
            }
            if (!MatchManager.AllowsGameplay) return;
            float factor = 1f + Mathf.Sin(Time.time * 7f) * 0.025f;
            constructionVisualRoot.localScale = new Vector3(1f, factor, 1f);
        }

        private void ResetConstructionVisual()
        {
            if (constructionVisualRoot != null)
                constructionVisualRoot.localScale = Vector3.one;
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay || State != BuildingState.UnderConstruction
                || !IsAlive) return;
            PruneBuilders();
            if (assignedBuilders.Count == 0)
            {
                if (RecoveryState == ConstructionRecoveryState.AccessFailure) return;
                RecoveryState = ReachableBuilderSlotCount > 0
                    ? ConstructionRecoveryState.WaitingForProgress
                    : ConstructionRecoveryState.AccessFailure;
                LastRecoveryReason = ReachableBuilderSlotCount > 0
                    ? "Waiting for a builder" : "No valid construction-access slot";
                return;
            }
            if (WorkingBuilderCount > 0
                || Time.time - lastProgressTime < ConstructionStallSeconds)
            {
                if (WorkingBuilderCount == 0)
                    RecoveryState = ConstructionRecoveryState.WaitingForProgress;
                return;
            }
            if (Time.time < nextRecoveryTime) return;
            nextRecoveryTime = Time.time + RecoveryCooldownSeconds;
            LastRecoveryTime = Time.time;
            if (ReachableBuilderSlotCount == 0)
            {
                RecoveryState = ConstructionRecoveryState.AccessFailure;
                LastRecoveryReason = "No valid construction-access slot";
                return;
            }
            RecoveryState = ConstructionRecoveryState.ReturningBuilders;
            LastRecoveryReason = "No progress; returning displaced builders to reserved slots";
            staleBuilders.Clear();
            foreach (PawnBuilder builder in assignedBuilders)
                if (builder != null && !builder.IsWorking) staleBuilders.Add(builder);
            for (int i = 0; i < staleBuilders.Count; i++)
                staleBuilders[i].RequestSlotRecovery("Site progress watchdog");
        }

        public void CompleteImmediatelyForTestSetup()
        {
            if (State == BuildingState.UnderConstruction && MatchManager.AllowsGameplay)
            {
                // Scenario setup creates already-complete starting structures; it is
                // not a player construction completion and must stay silent.
                completionAudioPlayed = true;
                AdvanceConstruction(data.ConstructionSeconds);
            }
        }

        public float Repair(float seconds)
        {
            if (!MatchManager.AllowsGameplay || !NeedsRepair) return 0f;
            float before = currentHP;
            currentHP = Mathf.Min(data.MaxHP, currentHP + data.RepairHPPerSecond * Mathf.Max(0f, seconds));
            UpdateBar();
            Changed?.Invoke(this);
            DefenseHostHealthChanged?.Invoke();
            return currentHP - before;
        }

        public void TakeDamage(float damage)
        {
            if (!IsAlive || damage <= 0f) return;
            currentHP = Mathf.Max(0f, currentHP - damage);
            UpdateBar();
            Changed?.Invoke(this);
            DefenseHostHealthChanged?.Invoke();
            if (currentHP <= 0f)
            {
                ReleaseOccupancy();
                DefenseHostDestroyed?.Invoke();
                Destroy(gameObject);
            }
        }

        public bool ReceiveBuildingDamage(int damage)
        {
            if (!IsAlive || damage <= 0) return false;
            TakeDamage(damage);
            return true;
        }

        [ContextMenu("Test Damage 25 HP")]
        private void TestDamage() => TakeDamage(25f);

        public bool IsFriendlyCursorTarget(Faction playerFaction) => Faction == playerFaction && IsAlive;

        public bool TryReserveInteraction(PawnBuilder pawn, out Vector3 position)
        {
            position = default;
            return interactionSlots != null && interactionSlots.TryReserve(pawn, out position);
        }

        public bool TryReassignInteraction(PawnBuilder pawn, out Vector3 position)
        {
            position = default;
            return interactionSlots != null
                && interactionSlots.TryReassign(pawn, out position);
        }

        public bool TryGetBuilderReservation(PawnBuilder pawn, out int slotId,
            out Vector2Int cell, out Vector3 position)
        {
            if (interactionSlots != null)
                return interactionSlots.TryGetReservation(pawn, out slotId,
                    out cell, out position);
            slotId = -1;
            cell = default;
            position = default;
            return false;
        }

        public bool HasBuilderReservation(PawnBuilder pawn)
            => interactionSlots != null && interactionSlots.HasReservation(pawn);

        public bool IsAssigned(PawnBuilder pawn)
            => pawn != null && assignedBuilders.Contains(pawn);

        public void RecordBuilderCommand(bool deduplicated)
        {
            BuilderCommandRequests++;
            TotalBuilderCommandRequests++;
            if (deduplicated)
            {
                DeduplicatedBuilderCommands++;
                TotalDeduplicatedBuilderCommands++;
            }
        }

        public void RecordBuilderPathRequest()
        {
            BuilderPathRequests++;
            TotalBuilderPathRequests++;
        }

        public void ReportConstructionAccessFailure(string reason)
        {
            RecoveryState = ConstructionRecoveryState.AccessFailure;
            LastRecoveryReason = string.IsNullOrWhiteSpace(reason)
                ? "No reachable construction-access slot" : reason;
            LastRecoveryTime = Time.time;
        }

        public static void ResetPerformanceCounters()
        {
            TotalBuilderCommandRequests = 0;
            TotalDeduplicatedBuilderCommands = 0;
            TotalBuilderPathRequests = 0;
        }

        public void Claim(PawnBuilder pawn)
        {
            if (pawn == null) return;
            assignedBuilders.Add(pawn);
            lastProgressTime = Time.time;
            RecoveryState = ConstructionRecoveryState.WaitingForProgress;
            LastRecoveryReason = "Builder assigned";
        }

        public void Release(PawnBuilder pawn)
        {
            if (pawn == null) return;
            assignedBuilders.Remove(pawn);
            interactionSlots?.Release(pawn);
        }

        private void PruneBuilders()
        {
            interactionSlots?.PruneInvalidReservations();
            staleBuilders.Clear();
            foreach (PawnBuilder builder in assignedBuilders)
            {
                UnitController controller = builder != null
                    ? builder.GetComponent<UnitController>() : null;
                if (builder == null || controller == null || !controller.IsAlive
                    || builder.Target != this || !HasBuilderReservation(builder))
                    staleBuilders.Add(builder);
            }
            for (int i = 0; i < staleBuilders.Count; i++)
            {
                assignedBuilders.Remove(staleBuilders[i]);
                interactionSlots?.Release(staleBuilders[i]);
            }
        }

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            if (!drawBuilderSlots || interactionSlots == null || grid == null || data == null)
                return;
            int count = ConstructionSlotUtility.SlotCount(Footprint);
            for (int slot = 0; slot < count; slot++)
            {
                Vector2Int cell = ConstructionSlotUtility.CellAt(Origin, Footprint, slot);
                Vector3 position = grid.GridToWorld(cell.x, cell.y);
                PawnBuilder owner = null;
                if (!ConstructionSlotUtility.IsValid(grid, Origin, Footprint, slot))
                    Gizmos.color = Color.red;
                else if (!interactionSlots.TryGetOwner(slot, out owner))
                    Gizmos.color = Color.green;
                else if (owner.IsWorking)
                    Gizmos.color = Color.yellow;
                else if (owner.GetComponent<UnitMovement>()?.IsMoving == true)
                    Gizmos.color = Color.cyan;
                else Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(position, 0.16f);
                if (owner != null)
                    UnityEditor.Handles.Label(position + Vector3.up * 0.18f,
                        $"{slot}: {owner.GetComponent<UnitController>()?.GetEntityId().ToString()}");
            }
#endif
        }

        private void MakeBar()
        {
            pixelSprite ??= Sprite.Create(Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            float topY = BuildingVisualPresentation.TopLocalY(visual);
            MakeBarPair("ConstructionProgress", topY + 0.3f,
                out progressBarRoot, out progressBackground, out progressFill);
        }

        private void MakeBarPair(string name, float y,
            out GameObject root,
            out SpriteRenderer backgroundRenderer, out SpriteRenderer fillRenderer)
        {
            root = new GameObject(name + "Bar");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, y, -0.1f);
            var background = new GameObject(name + "Background");
            background.transform.SetParent(root.transform, false);
            background.transform.localScale = new Vector3(1.3f, 0.14f, 1f);
            backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = pixelSprite;
            backgroundRenderer.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            backgroundRenderer.sortingOrder = 20;
            var fill = new GameObject(name + "Fill");
            fill.transform.SetParent(root.transform, false);
            fill.transform.localPosition = new Vector3(-0.63f, 0f, 0f);
            fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = pixelSprite;
            fillRenderer.sortingOrder = 21;
        }

        private void UpdateBar()
        {
            if (progressBackground == null) return;
            bool constructing = State == BuildingState.UnderConstruction;
            progressBarRoot.SetActive(constructing);
            if (constructing)
                SetFill(progressFill, ConstructionProgress, new Color(1f, 0.78f, 0.2f));
        }

        private static void SetFill(SpriteRenderer renderer, float ratio, Color color)
        {
            ratio = Mathf.Clamp01(ratio);
            renderer.transform.localScale = new Vector3(1.26f * ratio, 0.1f, 1f);
            renderer.transform.localPosition = new Vector3(-0.63f + 0.63f * ratio,
                renderer.transform.localPosition.y, 0f);
            renderer.color = color;
        }

        private void OnDestroy()
        {
            if (bonusApplied) population?.AddCap(-data.PopulationBonus);
            interactionSlots?.ReleaseAll();
            ReleaseOccupancy();
        }

        public void ReleaseOccupancy()
        {
            if (!occupied || grid == null || data == null) return;
            for (int y = 0; y < data.Footprint.y; y++)
            for (int x = 0; x < data.Footprint.x; x++)
                grid.SetOccupied(origin.x + x, origin.y + y, false);
            occupied = false;
        }
    }
}
