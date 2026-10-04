using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    // The site is the single authoritative owner of construction-slot reservations.
    public sealed class BuildingInteractionSlots : MonoBehaviour
    {
        private readonly Dictionary<PawnBuilder, int> reservations = new();
        private readonly List<PawnBuilder> staleBuilders = new();
        private BuildingSite site;
        private GridManager grid;
        private PathfindingSystem paths;

        public int ReservationCount => reservations.Count;
        public int ValidSlotCount
        {
            get
            {
                if (site == null || grid == null) return 0;
                int valid = 0;
                int count = ConstructionSlotUtility.SlotCount(site.Footprint);
                for (int slot = 0; slot < count; slot++)
                    if (ConstructionSlotUtility.IsValid(grid, site.Origin,
                        site.Footprint, slot)) valid++;
                return valid;
            }
        }
        public int FreeSlotCount => Mathf.Max(0, ValidSlotCount - reservations.Count);

        public void Configure(BuildingSite owner, GridManager navigation)
        {
            site = owner;
            grid = navigation;
            paths = grid != null ? grid.GetComponent<PathfindingSystem>() : null;
        }

        public bool TryReserve(PawnBuilder builder, out Vector3 position)
        {
            position = default;
            if (builder == null || site == null || grid == null || paths == null)
                return false;
            if (reservations.TryGetValue(builder, out int existing))
            {
                if (ConstructionSlotUtility.IsValid(grid, site.Origin,
                    site.Footprint, existing))
                {
                    position = SlotWorld(existing);
                    return true;
                }
                reservations.Remove(builder);
            }
            if (!TryFindReachableFreeSlot(builder, -1, out int slot)) return false;
            reservations.Add(builder, slot);
            position = SlotWorld(slot);
            return true;
        }

        public bool TryReassign(PawnBuilder builder, out Vector3 position)
        {
            position = default;
            if (builder == null || site == null || grid == null || paths == null)
                return false;
            int existing = reservations.TryGetValue(builder, out int value) ? value : -1;
            if (!TryFindReachableFreeSlot(builder, existing, out int replacement))
                return false;
            reservations[builder] = replacement;
            position = SlotWorld(replacement);
            return true;
        }

        public bool TryGetReservation(PawnBuilder builder, out int slotId,
            out Vector2Int cell, out Vector3 position)
        {
            slotId = -1;
            cell = default;
            position = default;
            if (builder == null || site == null || grid == null
                || !reservations.TryGetValue(builder, out slotId)) return false;
            cell = ConstructionSlotUtility.CellAt(site.Origin, site.Footprint, slotId);
            position = grid.GridToWorld(cell.x, cell.y);
            return true;
        }

        public bool HasReservation(PawnBuilder builder)
            => builder != null && reservations.ContainsKey(builder);

        public bool TryGetOwner(int slotId, out PawnBuilder owner)
        {
            foreach (KeyValuePair<PawnBuilder, int> item in reservations)
                if (item.Value == slotId)
                {
                    owner = item.Key;
                    return owner != null;
                }
            owner = null;
            return false;
        }

        public void PruneInvalidReservations()
        {
            staleBuilders.Clear();
            foreach (KeyValuePair<PawnBuilder, int> item in reservations)
            {
                PawnBuilder builder = item.Key;
                UnitController controller = builder != null
                    ? builder.GetComponent<UnitController>() : null;
                if (builder == null || controller == null || !controller.IsAlive
                    || builder.Target != site
                    || !ConstructionSlotUtility.IsValid(grid, site.Origin,
                        site.Footprint, item.Value))
                    staleBuilders.Add(builder);
            }
            for (int i = 0; i < staleBuilders.Count; i++)
                reservations.Remove(staleBuilders[i]);
        }

        public void Release(PawnBuilder builder)
        {
            if (builder != null) reservations.Remove(builder);
        }

        public void ReleaseAll()
        {
            reservations.Clear();
            staleBuilders.Clear();
        }

        private bool TryFindReachableFreeSlot(PawnBuilder builder, int excludedSlot,
            out int result)
        {
            result = -1;
            Vector2Int start = grid.WorldToGrid(builder.transform.position);
            int count = ConstructionSlotUtility.SlotCount(site.Footprint);
            ulong tried = 0UL;
            for (int attempt = 0; attempt < count; attempt++)
            {
                int bestSlot = -1;
                float bestDistance = float.PositiveInfinity;
                for (int slot = 0; slot < count; slot++)
                {
                    if (slot == excludedSlot || (tried & (1UL << slot)) != 0
                        || IsOwnedByOther(slot, builder)
                        || !ConstructionSlotUtility.IsValid(grid, site.Origin,
                            site.Footprint, slot)) continue;
                    Vector3 world = SlotWorld(slot);
                    float distance = ((Vector2)(world - builder.transform.position))
                        .sqrMagnitude;
                    if (bestSlot >= 0 && distance >= bestDistance) continue;
                    bestSlot = slot;
                    bestDistance = distance;
                }
                if (bestSlot < 0) return false;
                tried |= 1UL << bestSlot;
                Vector2Int target = ConstructionSlotUtility.CellAt(site.Origin,
                    site.Footprint, bestSlot);
                if (paths.FindPath(start, target).Count == 0) continue;
                result = bestSlot;
                return true;
            }
            return false;
        }

        private bool IsOwnedByOther(int slot, PawnBuilder builder)
        {
            foreach (KeyValuePair<PawnBuilder, int> item in reservations)
                if (item.Key != builder && item.Value == slot) return true;
            return false;
        }

        private Vector3 SlotWorld(int slot)
        {
            Vector2Int cell = ConstructionSlotUtility.CellAt(site.Origin,
                site.Footprint, slot);
            return grid.GridToWorld(cell.x, cell.y);
        }
    }
}
