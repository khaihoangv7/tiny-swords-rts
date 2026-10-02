using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Input;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Economy
{
    // Presentation-only ellipse shared by normal resources and the Gold Mine.
    // It never participates in clicks, occupancy, gathering or Fog authority.
    public sealed class ResourceSelectionIndicator : MonoBehaviour
    {
        private const int SegmentCount = 40;
        private LineRenderer ring;
        private Material material;

        public bool IsConfigured => ring != null;
        public bool IsSelected => ring != null && ring.enabled;
        public float LocalY => ring != null ? ring.transform.localPosition.y : 0f;

        public void Configure(float width, float height, float localY)
        {
            bool wasSelected = IsSelected;
            if (ring == null)
            {
                var child = new GameObject("ResourceSelectionIndicator");
                child.transform.SetParent(transform, false);
                ring = child.AddComponent<LineRenderer>();
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = SegmentCount;
                ring.startWidth = 0.045f;
                ring.endWidth = 0.045f;
                ring.numCornerVertices = 2;
                ring.sortingOrder = 9;
                material = new Material(Shader.Find("Sprites/Default"));
                material.color = new Color(0.2f, 1f, 0.25f, 0.95f);
                ring.sharedMaterial = material;
            }
            ring.transform.localPosition = new Vector3(0f, localY, 0f);
            for (int i = 0; i < SegmentCount; i++)
            {
                float angle = i * Mathf.PI * 2f / SegmentCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * width * 0.5f,
                    Mathf.Sin(angle) * height * 0.5f, 0f));
            }
            ring.enabled = wasSelected;
        }

        public void SetLocalY(float localY)
        {
            if (ring != null)
                ring.transform.localPosition = new Vector3(0f, localY, 0f);
        }

        public void SetSelected(bool selected)
        {
            if (ring != null) ring.enabled = selected;
        }

        private void OnDisable() => SetSelected(false);

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }

    [RequireComponent(typeof(ResourceInteractionSlots))]
    public sealed class ResourceNode : MonoBehaviour
    {
        public const float DefaultInteractionOffsetFactor = 0.49f;
        public const float GoldInteractionOffsetFactor = 0.20f;
        public const float LargeGoldInteractionOffsetFactor = -0.22f;
        public const float SheepSelectionPadding = 0.08f;

        private static readonly List<ResourceNode> registeredNodes = new();
        [SerializeField] private ResourceType resourceType;
        [SerializeField, Min(1)] private int maximumAmount = 100;
        [SerializeField, Min(0)] private int remainingAmount = 100;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Sprite depletedWoodSprite;
        private GridManager grid;
        private Vector2Int occupiedCell;
        private bool registered;
        private bool centerVisualY;
        private float presentationScale = UnitVisualPresentation.Scale;
        private ResourceSelectionIndicator selectionIndicator;
        private WorldSelectionHitArea selectionHitArea;

        public static IReadOnlyList<ResourceNode> ActiveNodes => registeredNodes;
        public ResourceType ResourceType => resourceType;
        public int MaximumAmount => maximumAmount;
        public int RemainingAmount => remainingAmount;
        public bool IsDepleted => remainingAmount <= 0;
        public bool IsAvailable => isActiveAndEnabled && !IsDepleted;
        public Vector2Int OccupiedCell => occupiedCell;
        public SpriteRenderer PresentationRenderer => visual;
        public float PresentationScale => presentationScale;
        public bool HasMemoryPresentation => resourceType == ResourceType.Wood || !IsDepleted;
        public bool IsSelected => selectionIndicator != null && selectionIndicator.IsSelected;
        public event Action<ResourceNode> Depleted;
        public event Action<ResourceNode, int> Harvested;

        private void OnEnable()
        {
            GroundSorting.Ensure(gameObject, resourceType == ResourceType.Food,
                SortGroundAnchorYOffset());
            EnsureSelectionIndicator();
            EnsureSelectionHitArea();
            if (!registeredNodes.Contains(this)) registeredNodes.Add(this);
        }

        private void Start()
        {
            if (IsDepleted) return;
            grid = FindAnyObjectByType<GridManager>();
            if (grid == null) return;
            occupiedCell = grid.WorldToGrid(transform.position);
            registered = grid.SetOccupied(occupiedCell.x, occupiedCell.y, true);
        }

        private void OnDisable()
        {
            registeredNodes.Remove(this);
            selectionIndicator?.SetSelected(false);
            ClearOccupancy();
            GetComponent<ResourceInteractionSlots>()?.ReleaseAll();
        }

        public void Configure(ResourceType type, int capacity, SpriteRenderer renderer,
            Sprite stump = null, float visualScale = UnitVisualPresentation.Scale)
        {
            resourceType = type;
            maximumAmount = Mathf.Max(1, capacity);
            remainingAmount = maximumAmount;
            visual = renderer;
            presentationScale = Mathf.Max(0.01f, visualScale);
            depletedWoodSprite = stump;
            centerVisualY = type != ResourceType.Wood;
            EnsureSelectionIndicator();
            (float width, float height, float y) = type switch
            {
                ResourceType.Wood => (0.95f, 0.30f, 0f),
                ResourceType.Gold => (0.80f, 0.26f, -0.20f),
                _ => (0.60f, 0.22f, -0.37f)
            };
            selectionIndicator.Configure(width * presentationScale,
                height * presentationScale, y);
            EnsureSelectionHitArea();
            GroundSorting.Ensure(gameObject, type == ResourceType.Food,
                SortGroundAnchorYOffset());
        }

        public void SetSelected(bool selected)
            => selectionIndicator?.SetSelected(selected && HasMemoryPresentation);

        public Vector3 InteractionPosition(Vector2Int reservedCell, GridManager navigation)
        {
            Vector3 center = navigation.GridToWorld(reservedCell.x, reservedCell.y);
            // Approach the resource-facing edge while remaining inside the
            // reserved walkable cell (the resource's own cell is occupied).
            // Gold Stone 1-5 use a smaller forward inset so the Pawn stays close.
            // The much larger Stone 6 uses the inverse inset so the Pawn remains
            // visually outside the rock instead of standing inside its art.
            float factor = resourceType == ResourceType.Gold
                ? presentationScale >= 1.5f
                    ? LargeGoldInteractionOffsetFactor
                    : GoldInteractionOffsetFactor
                : DefaultInteractionOffsetFactor;
            float offset = navigation.NavigationData.CellSize * factor;
            Vector2 towardNode = (Vector2)(transform.position - center);
            return center + new Vector3(
                Mathf.Abs(towardNode.x) > 0.001f ? Mathf.Sign(towardNode.x) * offset : 0f,
                Mathf.Abs(towardNode.y) > 0.001f ? Mathf.Sign(towardNode.y) * offset : 0f,
                0f);
        }

        public int Take(int requested)
        {
            if (requested <= 0 || !IsAvailable) return 0;
            int taken = Mathf.Min(requested, remainingAmount);
            remainingAmount -= taken;
            ResourceHarvestFlash flash = resourceType == ResourceType.Food
                ? GetComponent<ResourceHarvestFlash>() : null;
            if (taken > 0) flash?.Play(IsDepleted);
            if (taken > 0 && resourceType == ResourceType.Food)
            {
                GetComponent<HitSquashStretchPresentation>()?.Trigger();
            }
            if (taken > 0 && resourceType == ResourceType.Gold)
                GetComponent<GoldHarvestHighlight>()?.Play();
            if (taken > 0) Harvested?.Invoke(this, taken);
            if (IsDepleted)
            {
                ClearOccupancy();
                if (visual != null)
                {
                    if (resourceType == ResourceType.Wood && depletedWoodSprite != null)
                        SetPresentationSprite(depletedWoodSprite);
                    else if (flash == null) visual.enabled = false;
                }
                GetComponent<ResourceInteractionSlots>()?.ReleaseAll();
                Depleted?.Invoke(this);
            }
            return taken;
        }

        public void SetPresentationSprite(Sprite sprite)
        {
            if (visual == null || sprite == null) return;
            visual.sprite = sprite;
            Vector2 alignedOffset = new(
                (sprite.pivot.x - sprite.rect.width * 0.5f) / sprite.pixelsPerUnit,
                (sprite.pivot.y - (centerVisualY ? sprite.rect.height * 0.5f : 0f))
                    / sprite.pixelsPerUnit);
            UnitVisualPresentation.ApplyAlignedScale(visual, alignedOffset,
                presentationScale);
            EnsureSelectionHitArea();
            selectionHitArea?.SyncToVisual(visual);
        }

        public bool TryMoveOccupancy(Vector2Int destination, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (!registered || grid == null || IsDepleted) return false;
            if (destination == occupiedCell)
            {
                worldPosition = grid.GridToWorld(destination.x, destination.y);
                return true;
            }
            GridCell from = grid.GetCell(occupiedCell.x, occupiedCell.y);
            GridCell to = grid.GetCell(destination.x, destination.y);
            if (from == null || to == null || to.Occupied || !to.Walkable
                || to.TerrainType != TerrainType.FlatGround
                || to.HeightLevel != from.HeightLevel) return false;
            grid.SetOccupied(occupiedCell.x, occupiedCell.y, false);
            if (!grid.SetOccupied(destination.x, destination.y, true))
            {
                grid.SetOccupied(occupiedCell.x, occupiedCell.y, true);
                return false;
            }
            occupiedCell = destination;
            worldPosition = grid.GridToWorld(destination.x, destination.y);
            return true;
        }

        public void SnapToOccupiedCell()
        {
            if (registered && grid != null)
                transform.position = grid.GridToWorld(occupiedCell.x, occupiedCell.y);
        }

        private void ClearOccupancy()
        {
            if (!registered || grid == null) return;
            grid.SetOccupied(occupiedCell.x, occupiedCell.y, false);
            registered = false;
        }

        private void EnsureSelectionIndicator()
        {
            if (selectionIndicator != null) return;
            if (!TryGetComponent(out selectionIndicator))
                selectionIndicator = gameObject.AddComponent<ResourceSelectionIndicator>();
            if (!selectionIndicator.IsConfigured)
                selectionIndicator.Configure(0.7f, 0.24f, -0.35f);
        }

        private void EnsureSelectionHitArea()
        {
            if (visual == null) return;
            float padding = resourceType == ResourceType.Food ? SheepSelectionPadding : 0f;
            if (selectionHitArea == null)
                selectionHitArea = WorldSelectionHitArea.Ensure(this, visual, padding);
            else selectionHitArea.SetSelectionPadding(padding, visual);
        }

        private float SortGroundAnchorYOffset()
            => resourceType switch
            {
                ResourceType.Wood => 0f,
                ResourceType.Gold => -0.20f * presentationScale,
                _ => -0.37f * presentationScale
            };
    }
}
