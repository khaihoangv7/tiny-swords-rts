using System.Collections.Generic;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public sealed class BuildingMountedArchers : MonoBehaviour
    {
        private readonly List<UnitController> occupants = new();
        private readonly List<Transform> anchors = new();
        private IDefensiveBuildingHost host;

        public int Count => occupants.Count;
        public int Capacity => host?.DefenseData?.MountedArcherCapacity ?? 0;
        public IReadOnlyList<UnitController> Occupants => occupants;
        public bool Contains(UnitController unit) => occupants.Contains(unit);

        public void Configure(IDefensiveBuildingHost owner)
        {
            host = owner;
            anchors.Clear();
            Vector2[] offsets = owner.DefenseData.RooftopAnchors(owner.Faction);
            for (int i = 0; i < owner.DefenseData.MountedArcherCapacity; i++)
            {
                var anchor = new GameObject("MountedArcherAnchor" + i).transform;
                anchor.SetParent(owner.HostTransform, false);
                Vector2 offset = i < offsets.Length ? offsets[i] : Vector2.zero;
                anchor.localPosition = BuildingVisualPresentation.ScalePointFromGround(
                    owner.PresentationRenderer, owner.Type, offset);
                anchors.Add(anchor);
            }
        }

        public bool CanAccept(UnitController unit)
            => unit != null && unit.IsAlive && unit.Faction == host.Faction
                && unit.GetComponent<HealthComponent>()?.CombatData?.UnitType == UnitType.Archer
                && Count < Capacity;

        public bool Mount(UnitController unit, BuildingGarrison garrison)
        {
            if (!CanAccept(unit)) return false;
            int slot = occupants.Count;
            UnitStationing stationing = unit.GetComponent<UnitStationing>()
                ?? unit.gameObject.AddComponent<UnitStationing>();
            if (!stationing.EnterMounted(garrison, anchors[slot],
                host.DefenseData.MountedDamageMultiplier,
                host.DefenseData.MountedRangeMultiplier)) return false;
            occupants.Add(unit);
            return true;
        }

        public void Drain(List<UnitController> destination)
        {
            destination.AddRange(occupants);
            occupants.Clear();
        }

        public bool Remove(UnitController unit)
        {
            if (!occupants.Remove(unit)) return false;
            for (int i = 0; i < occupants.Count; i++)
            {
                if (occupants[i] == null) continue;
                occupants[i].transform.SetParent(anchors[i], false);
                occupants[i].transform.localPosition = Vector3.zero;
            }
            return true;
        }

        public bool PruneInvalid()
        {
            bool changed = false;
            for (int i = occupants.Count - 1; i >= 0; i--)
                if (occupants[i] == null || !occupants[i].IsAlive)
                {
                    occupants.RemoveAt(i);
                    changed = true;
                }
            if (!changed) return false;
            for (int i = 0; i < occupants.Count; i++)
            {
                if (occupants[i] == null) continue;
                occupants[i].transform.SetParent(anchors[i], false);
                occupants[i].transform.localPosition = Vector3.zero;
            }
            return true;
        }
    }
}
