using System.Collections;
using TinyKingdom.Buildings;
using TinyKingdom.Navigation;
using TinyKingdom.Presentation;
using TinyKingdom.Units;
using TinyKingdom.Match;
using UnityEngine;

namespace TinyKingdom.Economy
{
    [DisallowMultipleComponent]
    public sealed class HouseSheepSpawner : MonoBehaviour
    {
        private BuildingSite house;
        private GridManager grid;
        private EconomyConfig economy;
        private PresentationAssetCatalog presentation;
        private ResourceNode ownedSheep;
        private Coroutine pendingSpawn;

        public ResourceNode OwnedSheep => ownedSheep != null && ownedSheep.IsAvailable
            ? ownedSheep : null;
        public bool IsRespawnPending => pendingSpawn != null;
        public float RespawnDelaySeconds => economy != null
            ? economy.HouseSheepRespawnSeconds : 0f;

        public void Configure(BuildingSite owner, GridManager navigation,
            EconomyConfig config, PresentationAssetCatalog assets)
        {
            house = owner;
            grid = navigation;
            economy = config;
            presentation = assets;
            if (house == null || house.Type != BuildingType.House) return;
            house.Completed -= HandleHouseCompleted;
            house.Completed += HandleHouseCompleted;
            if (house.State == BuildingState.Completed) HandleHouseCompleted(house);
        }

        private void OnDestroy()
        {
            if (house != null) house.Completed -= HandleHouseCompleted;
            if (ownedSheep != null)
            {
                ownedSheep.Depleted -= HandleSheepUnavailable;
                ownedSheep.Removed -= HandleSheepUnavailable;
            }
        }

        private void HandleHouseCompleted(BuildingSite completed)
        {
            if (completed != house || OwnedSheep != null || pendingSpawn != null) return;
            if (!TrySpawn()) pendingSpawn = StartCoroutine(SpawnWhenSpaceAvailable(0.5f));
        }

        private void HandleSheepUnavailable(ResourceNode sheep)
        {
            if (sheep != ownedSheep) return;
            sheep.Depleted -= HandleSheepUnavailable;
            sheep.Removed -= HandleSheepUnavailable;
            ownedSheep = null;
            if (pendingSpawn == null && IsHouseValid())
                pendingSpawn = StartCoroutine(SpawnWhenSpaceAvailable(
                    economy.HouseSheepRespawnSeconds));
        }

        private IEnumerator SpawnWhenSpaceAvailable(float initialDelay)
        {
            if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);
            while (IsHouseValid() && OwnedSheep == null)
            {
                if (TrySpawn()) break;
                yield return new WaitForSeconds(1f);
            }
            pendingSpawn = null;
        }

        private bool TrySpawn()
        {
            if (!IsHouseValid() || OwnedSheep != null || presentation == null
                || presentation.SheepIdle == null || presentation.SheepIdle.Length == 0
                || !TryFindSpawnCell(out Vector2Int cell)) return false;

            var obj = new GameObject($"{house.Faction}HouseSheep_{house.GetEntityId()}");
            obj.transform.SetParent(house.transform.parent, false);
            obj.transform.position = grid.GridToWorld(cell.x, cell.y);

            Sprite sprite = presentation.SheepIdle[0];
            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(obj.transform, false);
            visualObject.transform.localPosition = new Vector3(
                (sprite.pivot.x - sprite.rect.width * 0.5f) / sprite.pixelsPerUnit,
                (sprite.pivot.y - sprite.rect.height * 0.5f) / sprite.pixelsPerUnit, 0f);
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 10;
            UnitVisualPresentation.ApplyAlignedScale(renderer,
                renderer.transform.localPosition, UnitVisualPresentation.Scale);

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.65f, 0.65f);
            collider.isTrigger = true;
            obj.AddComponent<ResourceInteractionSlots>().Configure(1);
            obj.AddComponent<ResourceHarvestFlash>().Configure(renderer);
            ResourceNode node = obj.AddComponent<ResourceNode>();
            node.Configure(ResourceType.Food, economy.SheepCapacity, renderer, null,
                UnitVisualPresentation.Scale);
            obj.AddComponent<SheepPresentation>().Configure(node, renderer,
                presentation.SheepIdle, presentation.SheepGrass, presentation.SheepMove);
            obj.AddComponent<HitSquashStretchPresentation>().Configure(renderer);
            if (!node.TryRegisterOccupancy(grid))
            {
                Destroy(obj);
                return false;
            }
            ownedSheep = node;
            ownedSheep.Depleted += HandleSheepUnavailable;
            ownedSheep.Removed += HandleSheepUnavailable;
            return true;
        }

        private bool TryFindSpawnCell(out Vector2Int result)
        {
            result = default;
            if (house == null || grid == null || house.Data == null) return false;
            GridCell footprintCell = grid.GetCell(house.Origin.x, house.Origin.y);
            if (footprintCell == null) return false;
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            for (int radius = 1; radius <= 2; radius++)
            {
                int minX = -radius;
                int maxX = house.Footprint.x - 1 + radius;
                int minY = -radius;
                int maxY = house.Footprint.y - 1 + radius;
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (x > minX && x < maxX && y > minY && y < maxY) continue;
                    Vector2Int candidate = house.Origin + new Vector2Int(x, y);
                    GridCell cell = grid.GetCell(candidate.x, candidate.y);
                    if (cell == null || !cell.Walkable || cell.Occupied
                        || cell.HeightLevel != footprintCell.HeightLevel
                        || ContainsLivingUnit(candidate)) continue;
                    float distance = ((Vector2)(cell.WorldPosition
                        - house.transform.position)).sqrMagnitude;
                    if (found && distance >= bestDistance) continue;
                    found = true;
                    bestDistance = distance;
                    result = candidate;
                }
                if (found) return true;
            }
            return false;
        }

        private bool ContainsLivingUnit(Vector2Int cell)
        {
            var units = UnitController.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit != null && unit.IsAlive
                    && grid.WorldToGrid(unit.transform.position) == cell) return true;
            }
            return false;
        }

        private bool IsHouseValid()
            => house != null && house.IsAlive && house.Type == BuildingType.House
                && house.State == BuildingState.Completed && grid != null && economy != null
                && MatchManager.AllowsGameplay;
    }
}
