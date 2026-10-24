using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Input;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Economy
{
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
        private SelectionCursorMarker selectionIndicator;
        private WorldSelectionHitArea selectionHitArea;

        public static IReadOnlyList<ResourceNode> ActiveNodes => registeredNodes;
        public ResourceType ResourceType => resourceType;
        public int MaximumAmount => maximumAmount;
        public int RemainingAmount => remainingAmount;
        public bool IsDepleted => remainingAmount <= 0;
        public bool IsAvailable => isActiveAndEnabled && !IsDepleted;
        public bool IsSoftNavigationEntity => resourceType == ResourceType.Food;
        public Vector2Int OccupiedCell => occupiedCell;
        public SpriteRenderer PresentationRenderer => visual;
        public float PresentationScale => presentationScale;
        public bool HasMemoryPresentation => resourceType == ResourceType.Wood || !IsDepleted;
        public bool IsSelected => selectionIndicator != null && selectionIndicator.IsSelected;
        public event Action<ResourceNode> Depleted;
        public event Action<ResourceNode> Removed;
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
            TryRegisterOccupancy(FindAnyObjectByType<GridManager>());
        }

        private void OnDisable()
        {
            Removed?.Invoke(this);
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
            BoxCollider2D gameplayCollider = GetComponent<BoxCollider2D>();
            if (gameplayCollider != null && IsSoftNavigationEntity)
                gameplayCollider.isTrigger = true;
            centerVisualY = type != ResourceType.Wood;
            EnsureSelectionIndicator();
            RefreshSelectionIndicator();
            EnsureSelectionHitArea();
            GroundSorting.Ensure(gameObject, type == ResourceType.Food,
                SortGroundAnchorYOffset());
        }

        public void SetSelected(bool selected)
        {
            EnsureSelectionIndicator();
            selectionIndicator?.SetSelected(selected && HasMemoryPresentation);
        }

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

        public bool TryRegisterOccupancy(GridManager navigation)
        {
            if (registered) return true;
            if (navigation == null || IsDepleted) return false;
            grid = navigation;
            occupiedCell = grid.WorldToGrid(transform.position);
            GridCell cell = grid.GetCell(occupiedCell.x, occupiedCell.y);
            if (cell == null || !cell.Walkable
                || (!IsSoftNavigationEntity && cell.Occupied)) return false;
            registered = IsSoftNavigationEntity
                || grid.SetOccupied(occupiedCell.x, occupiedCell.y, true);
            return registered;
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
            RefreshSelectionIndicator();
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
            bool validTerrain = IsSoftNavigationEntity
                ? to != null && (to.TerrainType == TerrainType.FlatGround
                    || to.TerrainType == TerrainType.ElevatedGround)
                : to != null && to.TerrainType == TerrainType.FlatGround;
            if (from == null || to == null || to.Occupied || !to.Walkable
                || !validTerrain
                || to.HeightLevel != from.HeightLevel) return false;
            if (IsSoftNavigationEntity)
            {
                occupiedCell = destination;
                worldPosition = grid.GridToWorld(destination.x, destination.y);
                return true;
            }
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
            if (!IsSoftNavigationEntity)
                grid.SetOccupied(occupiedCell.x, occupiedCell.y, false);
            registered = false;
        }

        private void EnsureSelectionIndicator()
        {
            if (selectionIndicator == null && !TryGetComponent(out selectionIndicator))
                selectionIndicator = gameObject.AddComponent<SelectionCursorMarker>();
            if (selectionIndicator != null && !selectionIndicator.IsConfigured)
                RefreshSelectionIndicator();
        }

        private void RefreshSelectionIndicator()
        {
            if (selectionIndicator == null || visual == null) return;
            (float width, float height, float y) = resourceType switch
            {
                ResourceType.Wood => (0.95f, 0.30f, 0f),
                ResourceType.Gold => (0.80f, 0.26f, -0.20f),
                _ => (0.60f, 0.22f, -0.37f)
            };
            float shallowHeight = Mathf.Max(0.48f, height * presentationScale);
            bool surroundResource = resourceType is ResourceType.Wood or ResourceType.Gold;
            float depth = surroundResource
                ? Mathf.Max(shallowHeight, (resourceType == ResourceType.Wood
                    ? 0.55f : 0.40f) * presentationScale)
                : shallowHeight;
            // Keep the near corners at their existing ground contact while the
            // far pair recedes behind the tree or ore sprite.
            float centerY = y + (depth - shallowHeight) * 0.5f;
            selectionIndicator.Configure(width * presentationScale,
                depth, centerY, visual, 0.8f,
                surroundBuilding: surroundResource,
                localX: visual.bounds.center.x - transform.position.x);
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
