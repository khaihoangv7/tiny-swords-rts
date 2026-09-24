using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Vision
{
    // One shared player-view relationship for presentation code. Gameplay ownership
    // remains on UnitController/building hosts and is never inferred from colors.
    public static class PlayerViewRelation
    {
        private static Faction playerViewFaction = Faction.Blue;

        public static Faction PlayerViewFaction => playerViewFaction;
        public static event Action<Faction> Changed;

        public static bool IsFriendlyToView(Faction ownerFaction)
            => ownerFaction == playerViewFaction;

        internal static void SetPlayerViewFaction(Faction faction)
        {
            if (playerViewFaction == faction) return;
            playerViewFaction = faction;
            Changed?.Invoke(faction);
        }
    }

    // Presentation and human observability adapter. TeamVisionSystem remains authoritative.
    public sealed class FogWorldVisibility : MonoBehaviour
    {
        private readonly Dictionary<UnityEngine.Object, FogPresentationGate> gates = new();
        private TeamVisionSystem vision;
        private Faction playerViewFaction = Faction.Blue;
        private float nextRefresh;

        public static FogWorldVisibility Instance { get; private set; }
        public Faction PlayerViewFaction => playerViewFaction;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (vision != null) vision.VisionChanged -= RefreshAll;
            if (Instance == this) Instance = null;
        }

        public void Configure(TeamVisionSystem source, Faction playerFaction)
        {
            if (vision != null) vision.VisionChanged -= RefreshAll;
            vision = source;
            playerViewFaction = playerFaction;
            PlayerViewRelation.SetPlayerViewFaction(playerFaction);
            vision.VisionChanged += RefreshAll;
            RefreshAll();
        }

        private void Update()
        {
            if (vision == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + vision.UpdateInterval;
            RefreshAll();
        }

        public bool CanObserve(Faction faction, Vector3 position)
            => faction == playerViewFaction || vision == null
                || vision.IsVisible(playerViewFaction, position);

        public bool CanObserveResource(Vector3 position)
            => vision == null || vision.IsVisible(playerViewFaction, position);

        public static bool ShouldPresentDeath(Faction faction, Vector3 position)
            => Instance == null || Instance.CanObserve(faction, position);

        public void RefreshNow() => RefreshAll();

        private void RefreshAll()
        {
            if (vision == null) return;
            foreach (UnitController unit in UnitController.AllUnits)
            {
                if (unit == null) continue;
                Gate(unit, CanObserve(unit.Faction, unit.transform.position));
            }

            IReadOnlyList<IVisionSource> sources = VisionSourceRegistry.ActiveSources;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] is not BuildingVisionSource building || building == null) continue;
                Gate(building, CanObserve(building.Faction, building.VisionPosition));
            }

            IReadOnlyList<ResourceNode> resources = ResourceNode.ActiveNodes;
            for (int i = 0; i < resources.Count; i++)
            {
                ResourceNode resource = resources[i];
                if (resource != null) Gate(resource, CanObserveResource(resource.transform.position));
            }
        }

        private void Gate(Component target, bool presented)
        {
            if (!gates.TryGetValue(target, out FogPresentationGate gate) || gate == null)
            {
                gate = target.GetComponent<FogPresentationGate>()
                    ?? target.gameObject.AddComponent<FogPresentationGate>();
                gates[target] = gate;
            }
            gate.SetPresented(presented);
        }
    }
}
