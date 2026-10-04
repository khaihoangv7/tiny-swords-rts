using System;
using System.Collections.Generic;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Match;
using TinyKingdom.Audio;
using TinyKingdom.Presentation;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public sealed class BuildingSite : MonoBehaviour, IFriendlyCursorTarget, IProductionHost,
        IDefensiveBuildingHost
    {
        private BuildingData data;
        private GridManager grid;
        private FactionPopulation population;
        private SpriteRenderer visual;
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
        private BuildingInteractionSlots interactionSlots;

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
        public bool CanAssign(PawnBuilder pawn) => pawn != null && !assignedBuilders.Contains(pawn);
        public event Action<BuildingSite> Changed;
        public event Action DefenseHostDestroyed;
        public event Action DefenseHostHealthChanged;

        private void OnEnable() => GroundSorting.Ensure(gameObject);

        public void Configure(BuildingData definition, Faction owner, Vector2Int gridOrigin,
            GridManager navigation, FactionPopulation ownerPopulation,
            HouseVariant variant = HouseVariant.A)
        {
            data = definition;
            Faction = owner;
            origin = gridOrigin;
            grid = navigation;
            population = ownerPopulation;
            Variant = variant;
            currentHP = 1f;
            completionAudioPlayed = false;
            Vector3 first = grid.GridToWorld(origin.x, origin.y);
            transform.position = first + new Vector3((data.Footprint.x - 1) * grid.NavigationData.CellSize * 0.5f,
                (data.Footprint.y - 1) * grid.NavigationData.CellSize * 0.5f, 0f);
            var child = new GameObject("VisualRoot");
            child.transform.SetParent(transform, false);
            visual = child.AddComponent<SpriteRenderer>();
            visual.sprite = data.GetSprite(owner, variant);
            visual.sortingOrder = 9;
            BuildingVisualPresentation.Apply(visual, data.Type, data.VisualOffset);
            BuildingSelectionPulse.Ensure(gameObject, visual);
            visual.color = new Color(1f, 1f, 1f, 0.55f);
            GroundSorting.Ensure(gameObject);
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
            constructionElapsed = Mathf.Min(data.ConstructionSeconds,
                constructionElapsed + Mathf.Max(0f, seconds));
            currentHP = Mathf.Max(1f, data.MaxHP * ConstructionProgress);
            if (constructionElapsed >= data.ConstructionSeconds)
            {
                State = BuildingState.Completed;
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

        public void Claim(PawnBuilder pawn)
        {
            if (pawn != null) assignedBuilders.Add(pawn);
        }

        public void Release(PawnBuilder pawn)
        {
            if (pawn == null) return;
            assignedBuilders.Remove(pawn);
            interactionSlots?.Release(pawn);
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
