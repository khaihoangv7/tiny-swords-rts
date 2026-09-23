using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using UnityEngine;

namespace TinyKingdom.Economy
{
    [RequireComponent(typeof(ResourceInteractionSlots))]
    public sealed class ResourceNode : MonoBehaviour
    {
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

        public static IReadOnlyList<ResourceNode> ActiveNodes => registeredNodes;
        public ResourceType ResourceType => resourceType;
        public int MaximumAmount => maximumAmount;
        public int RemainingAmount => remainingAmount;
        public bool IsDepleted => remainingAmount <= 0;
        public bool IsAvailable => isActiveAndEnabled && !IsDepleted;
        public Vector2Int OccupiedCell => occupiedCell;
        public SpriteRenderer PresentationRenderer => visual;
        public bool HasMemoryPresentation => resourceType == ResourceType.Wood || !IsDepleted;
        public event Action<ResourceNode> Depleted;

        private void OnEnable()
        {
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
            ClearOccupancy();
            GetComponent<ResourceInteractionSlots>()?.ReleaseAll();
        }

        public void Configure(ResourceType type, int capacity, SpriteRenderer renderer,
            Sprite stump = null)
        {
            resourceType = type;
            maximumAmount = Mathf.Max(1, capacity);
            remainingAmount = maximumAmount;
            visual = renderer;
            depletedWoodSprite = stump;
            centerVisualY = type != ResourceType.Wood;
        }

        public Vector3 InteractionPosition(Vector2Int reservedCell, GridManager navigation)
        {
            Vector3 center = navigation.GridToWorld(reservedCell.x, reservedCell.y);
            // Approach the resource-facing edge while remaining inside the
            // reserved walkable cell (the resource's own cell is occupied).
            float offset = navigation.NavigationData.CellSize * 0.49f;
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
            if (taken > 0 && resourceType == ResourceType.Gold)
                GetComponent<GoldHarvestHighlight>()?.Play();
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
            visual.transform.localPosition = new Vector3(
                (sprite.pivot.x - sprite.rect.width * 0.5f) / sprite.pixelsPerUnit,
                (sprite.pivot.y - (centerVisualY ? sprite.rect.height * 0.5f : 0f))
                    / sprite.pixelsPerUnit, 0f);
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
    }
}
