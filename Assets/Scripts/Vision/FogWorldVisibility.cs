using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Debugging;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using Unity.Profiling;
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
        private static readonly ProfilerMarker RefreshMarker =
            new("TinyKingdom.Fog.WorldVisibilityRefresh");
        private readonly Dictionary<UnityEngine.Object, FogPresentationGate> gates = new();
        private readonly List<UnityEngine.Object> staleGateKeys = new();
        private TeamVisionSystem vision;
        private Faction playerViewFaction = Faction.Blue;
        private float nextRefresh;

        public static FogWorldVisibility Instance { get; private set; }
        public Faction PlayerViewFaction => playerViewFaction;
        public int RegisteredGateCount => gates.Count;

        private void Awake()
        {
            Instance = this;
            DebugObserverView.Changed += HandleObserverViewChanged;
        }

        private void OnDestroy()
        {
            DebugObserverView.Changed -= HandleObserverViewChanged;
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
            => DebugObserverView.IsEnabled || faction == playerViewFaction || vision == null
                || vision.IsVisible(playerViewFaction, position);

        public bool CanObserveResource(Vector3 position)
            => DebugObserverView.IsEnabled || vision == null
                || vision.IsVisible(playerViewFaction, position);

        public bool ShouldPresent(Faction faction, Vector3 position)
            => DebugObserverView.IsEnabled || CanObserve(faction, position);

        public bool ShouldPresentResource(Vector3 position)
            => DebugObserverView.IsEnabled || CanObserveResource(position);

        public static bool ShouldPresentDeath(Faction faction, Vector3 position)
            => Instance == null || Instance.ShouldPresent(faction, position);

        public void RefreshNow() => RefreshAll();

        private void RefreshAll()
        {
            if (vision == null) return;
            using var marker = RefreshMarker.Auto();
            nextRefresh = Time.unscaledTime + vision.UpdateInterval;
            PruneDestroyedGates();
            foreach (UnitController unit in UnitController.AllUnits)
            {
                if (unit == null) continue;
                Gate(unit, ShouldPresent(unit.Faction, unit.transform.position));
            }

            IReadOnlyList<IVisionSource> sources = VisionSourceRegistry.ActiveSources;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] is not BuildingVisionSource building || building == null) continue;
                Gate(building, ShouldPresent(building.Faction, building.VisionPosition));
            }

            IReadOnlyList<ResourceNode> resources = ResourceNode.ActiveNodes;
            for (int i = 0; i < resources.Count; i++)
            {
                ResourceNode resource = resources[i];
                if (resource != null)
                    Gate(resource, ShouldPresentResource(resource.transform.position));
            }
            IReadOnlyList<GoldMineObjective> mines = GoldMineObjective.Active;
            for (int i = 0; i < mines.Count; i++)
            {
                GoldMineObjective mine = mines[i];
                if (mine != null) Gate(mine, DebugObserverView.IsEnabled
                    || (mine.HasOwner && mine.Faction == playerViewFaction)
                    || CanObserveResource(mine.transform.position));
            }
        }

        private void HandleObserverViewChanged(bool _) => RefreshAll();

        private void PruneDestroyedGates()
        {
            staleGateKeys.Clear();
            foreach (KeyValuePair<UnityEngine.Object, FogPresentationGate> pair in gates)
                if (pair.Key == null || pair.Value == null) staleGateKeys.Add(pair.Key);
            for (int i = 0; i < staleGateKeys.Count; i++)
                gates.Remove(staleGateKeys[i]);
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
