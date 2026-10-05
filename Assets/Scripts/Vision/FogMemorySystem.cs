using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Debugging;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.Vision
{
    [Serializable]
    public sealed class BuildingMemorySnapshot
    {
        public EntityId RuntimeId { get; internal set; }
        public BuildingType BuildingType { get; internal set; }
        public Faction Faction { get; internal set; }
        public Vector3 LogicalPosition { get; internal set; }
        public Vector2Int FootprintOrigin { get; internal set; }
        public Vector2Int Footprint { get; internal set; }
        public Sprite Sprite { get; internal set; }
        public Vector3 VisualPosition { get; internal set; }
        public Quaternion VisualRotation { get; internal set; }
        public Vector3 VisualScale { get; internal set; }
        public Color VisualColor { get; internal set; }
        public int SortingLayerId { get; internal set; }
        public int SortingOrder { get; internal set; }
        public bool FlipX { get; internal set; }
        public bool Completed { get; internal set; }
        public float ConstructionProgress { get; internal set; }
        public float HealthRatio { get; internal set; }
        public float DefensiveAttackRange { get; internal set; }
        public float LastSeenTime { get; internal set; }
    }

    [Serializable]
    public sealed class ResourceMemorySnapshot
    {
        public EntityId RuntimeId { get; internal set; }
        public ResourceType ResourceType { get; internal set; }
        public Vector3 LogicalPosition { get; internal set; }
        public Vector2Int LogicalCell { get; internal set; }
        public Sprite Sprite { get; internal set; }
        public Vector3 VisualPosition { get; internal set; }
        public Quaternion VisualRotation { get; internal set; }
        public Vector3 VisualScale { get; internal set; }
        public Color VisualColor { get; internal set; }
        public int SortingLayerId { get; internal set; }
        public int SortingOrder { get; internal set; }
        public bool FlipX { get; internal set; }
        public float RemainingRatio { get; internal set; }
        public int RemainingAmount { get; internal set; }
        public int MaximumAmount { get; internal set; }
        public float LastSeenTime { get; internal set; }
    }

    // Stores faction-specific presentation snapshots. Public queries return snapshots only;
    // live hidden scene objects never become faction knowledge.
    [DefaultExecutionOrder(-110)]
    public sealed class FogMemorySystem : MonoBehaviour
    {
        public const float GhostAlpha = 0.72f;
        private static readonly Color GhostTint = new(0.72f, 0.76f, 0.82f, GhostAlpha);
        private static readonly ProfilerMarker RefreshMarker =
            new("TinyKingdom.Fog.MemoryRefresh");
        private readonly Dictionary<EntityId, BuildingMemorySnapshot>[] buildings =
        {
            new(), new()
        };
        private readonly Dictionary<EntityId, ResourceMemorySnapshot>[] resources =
        {
            new(), new()
        };
        private readonly HashSet<EntityId>[] visibleBuildings = { new(), new() };
        private readonly HashSet<EntityId>[] visibleResources = { new(), new() };
        private readonly Dictionary<EntityId, SpriteRenderer> buildingGhosts = new();
        private readonly Dictionary<EntityId, SpriteRenderer> resourceGhosts = new();
        private readonly List<EntityId> removal = new();

        private TeamVisionSystem vision;
        private GridManager grid;
        private Faction playerViewFaction = Faction.Blue;
        private Transform ghostRoot;
        private float nextRefresh;

        public static FogMemorySystem Instance { get; private set; }
        public bool IsInitialized => vision != null && grid != null;
        public int InitializedFactionCount => IsInitialized ? 2 : 0;
        public Faction PlayerViewFaction => playerViewFaction;
        public int ActiveBuildingGhostCount => ActiveCount(buildingGhosts);
        public int ActiveResourceGhostCount => ActiveCount(resourceGhosts);
        public event Action MemoryChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DebugObserverView.Changed += HandleObserverViewChanged;
        }

        private void OnDestroy()
        {
            DebugObserverView.Changed -= HandleObserverViewChanged;
            if (vision != null) vision.VisionChanged -= RefreshMemoryNow;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!IsInitialized || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + vision.UpdateInterval;
            RefreshMemoryNow();
        }

        public void Configure(TeamVisionSystem source, Faction displayedFaction)
        {
            if (vision != null) vision.VisionChanged -= RefreshMemoryNow;
            vision = source;
            grid = source != null ? source.GridManager : null;
            playerViewFaction = displayedFaction;
            if (vision != null) vision.VisionChanged += RefreshMemoryNow;
            EnsureGhostRoot();
            RefreshMemoryNow();
        }

        public bool IsCurrentlyVisible(Faction faction, Vector3 position)
            => vision != null && vision.IsVisible(faction, position);

        public bool WasExplored(Faction faction, Vector3 position)
            => vision != null && vision.IsExplored(faction, position);

        public IReadOnlyCollection<BuildingMemorySnapshot> GetLastSeenBuildings(Faction faction)
            => buildings[TeamIndex(faction)].Values;

        public IReadOnlyCollection<ResourceMemorySnapshot> GetLastSeenResources(Faction faction)
            => resources[TeamIndex(faction)].Values;

        public bool TryGetLastSeenBuilding(Faction faction, EntityId runtimeId,
            out BuildingMemorySnapshot snapshot)
            => buildings[TeamIndex(faction)].TryGetValue(runtimeId, out snapshot);

        public bool TryGetLastSeenResource(Faction faction, EntityId runtimeId,
            out ResourceMemorySnapshot snapshot)
            => resources[TeamIndex(faction)].TryGetValue(runtimeId, out snapshot);

        public bool IsKnownOccupied(Faction faction, Vector2Int cell)
        {
            if (!IsInitialized) return false;
            VisionState state = vision.GetVisionState(faction, cell);
            if (state == VisionState.Unexplored) return false;
            if (state == VisionState.Visible)
                return grid.GetCell(cell.x, cell.y)?.Occupied == true;

            foreach (BuildingMemorySnapshot item in buildings[TeamIndex(faction)].Values)
                if (cell.x >= item.FootprintOrigin.x
                    && cell.x < item.FootprintOrigin.x + item.Footprint.x
                    && cell.y >= item.FootprintOrigin.y
                    && cell.y < item.FootprintOrigin.y + item.Footprint.y)
                    return true;
            foreach (ResourceMemorySnapshot item in resources[TeamIndex(faction)].Values)
                if (item.LogicalCell == cell) return true;
            return false;
        }

        public void RefreshMemoryNow()
        {
            if (!IsInitialized) return;
            nextRefresh = Time.unscaledTime + vision.UpdateInterval;
            using (RefreshMarker.Auto())
                RefreshMemoryCore();
        }

        private void RefreshMemoryCore()
        {
            if (!IsInitialized) return;
            for (int i = 0; i < 2; i++)
            {
                visibleBuildings[i].Clear();
                visibleResources[i].Clear();
            }

            IReadOnlyList<IVisionSource> sources = VisionSourceRegistry.ActiveSources;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] is not BuildingVisionSource source || source == null
                    || source.Host is not Component component || component == null)
                    continue;
                IDefensiveBuildingHost host = source.Host;
                EntityId id = component.GetEntityId();
                for (int team = 0; team < 2; team++)
                {
                    Faction viewer = TeamFaction(team);
                    if (host.Faction == viewer
                        || vision.GetVisionState(viewer, host.HostTransform.position)
                            != VisionState.Visible) continue;
                    visibleBuildings[team].Add(id);
                    if (host.IsAlive) CaptureBuilding(team, host, component, id);
                    else buildings[team].Remove(id);
                }
            }

            IReadOnlyList<ResourceNode> nodes = ResourceNode.ActiveNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNode node = nodes[i];
                if (node == null) continue;
                EntityId id = node.GetEntityId();
                for (int team = 0; team < 2; team++)
                {
                    Faction viewer = TeamFaction(team);
                    if (vision.GetVisionState(viewer, node.transform.position)
                        != VisionState.Visible) continue;
                    visibleResources[team].Add(id);
                    if (node.HasMemoryPresentation) CaptureResource(team, node, id);
                    else resources[team].Remove(id);
                }
            }

            IReadOnlyList<GoldMineObjective> mines = GoldMineObjective.Active;
            for (int i = 0; i < mines.Count; i++)
            {
                GoldMineObjective mine = mines[i];
                if (mine == null) continue;
                EntityId id = mine.GetEntityId();
                for (int team = 0; team < 2; team++)
                {
                    Faction viewer = TeamFaction(team);
                    if (mine.HasOwner && mine.Faction == viewer) continue;
                    if (vision.GetVisionState(viewer, mine.transform.position)
                        != VisionState.Visible) continue;
                    visibleBuildings[team].Add(id);
                    CaptureBuilding(team, mine, mine, id);
                }
            }

            for (int team = 0; team < 2; team++)
            {
                ReconcileBuildings(team);
                ReconcileResources(team);
            }
            RefreshGhosts();
            MemoryChanged?.Invoke();
        }

        private void ReconcileBuildings(int team)
        {
            removal.Clear();
            Faction faction = TeamFaction(team);
            foreach (KeyValuePair<EntityId, BuildingMemorySnapshot> item in buildings[team])
                if (!visibleBuildings[team].Contains(item.Key)
                    && vision.GetVisionState(faction, item.Value.LogicalPosition)
                        == VisionState.Visible) removal.Add(item.Key);
            for (int i = 0; i < removal.Count; i++) buildings[team].Remove(removal[i]);
        }

        private void ReconcileResources(int team)
        {
            removal.Clear();
            Faction faction = TeamFaction(team);
            foreach (KeyValuePair<EntityId, ResourceMemorySnapshot> item in resources[team])
                if (!visibleResources[team].Contains(item.Key)
                    && vision.GetVisionState(faction, item.Value.LogicalPosition)
                        == VisionState.Visible) removal.Add(item.Key);
            for (int i = 0; i < removal.Count; i++) resources[team].Remove(removal[i]);
        }

        private void RefreshGhosts()
        {
            int team = TeamIndex(playerViewFaction);
            RefreshBuildingGhosts(buildings[team]);
            RefreshResourceGhosts(resources[team]);
        }

        private void RefreshBuildingGhosts(
            IReadOnlyDictionary<EntityId, BuildingMemorySnapshot> snapshots)
        {
            RemoveMissingGhosts(buildingGhosts, snapshots);
            foreach (KeyValuePair<EntityId, BuildingMemorySnapshot> item in snapshots)
            {
                BuildingMemorySnapshot snapshot = item.Value;
                bool show = !DebugObserverView.IsEnabled
                    && vision.GetVisionState(playerViewFaction, snapshot.LogicalPosition)
                        == VisionState.Explored;
                SpriteRenderer renderer = GetGhost(buildingGhosts, item.Key,
                    "LastSeenBuilding_");
                Apply(renderer, snapshot.Sprite, snapshot.VisualPosition,
                    snapshot.VisualRotation, snapshot.VisualScale, snapshot.VisualColor,
                    snapshot.SortingLayerId, snapshot.SortingOrder, snapshot.FlipX, show);
            }
        }

        private void RefreshResourceGhosts(
            IReadOnlyDictionary<EntityId, ResourceMemorySnapshot> snapshots)
        {
            RemoveMissingGhosts(resourceGhosts, snapshots);
            foreach (KeyValuePair<EntityId, ResourceMemorySnapshot> item in snapshots)
            {
                ResourceMemorySnapshot snapshot = item.Value;
                bool show = !DebugObserverView.IsEnabled
                    && vision.GetVisionState(playerViewFaction, snapshot.LogicalPosition)
                        == VisionState.Explored;
                SpriteRenderer renderer = GetGhost(resourceGhosts, item.Key,
                    "LastSeenResource_");
                Apply(renderer, snapshot.Sprite, snapshot.VisualPosition,
                    snapshot.VisualRotation, snapshot.VisualScale, snapshot.VisualColor,
                    snapshot.SortingLayerId, snapshot.SortingOrder, snapshot.FlipX, show);
            }
        }

        private SpriteRenderer GetGhost(Dictionary<EntityId, SpriteRenderer> ghosts, EntityId id,
            string objectNamePrefix)
        {
            if (ghosts.TryGetValue(id, out SpriteRenderer renderer) && renderer != null)
                return renderer;
            var obj = new GameObject(objectNamePrefix + id);
            obj.transform.SetParent(ghostRoot, true);
            renderer = obj.AddComponent<SpriteRenderer>();
            ghosts[id] = renderer;
            return renderer;
        }

        private static void Apply(SpriteRenderer renderer, Sprite sprite, Vector3 position,
            Quaternion rotation, Vector3 scale, Color sourceColor, int sortingLayer,
            int sortingOrder, bool flipX, bool show)
        {
            renderer.sprite = sprite;
            renderer.transform.SetPositionAndRotation(position, rotation);
            renderer.transform.localScale = scale;
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            renderer.flipX = flipX;
            renderer.color = new Color(sourceColor.r * GhostTint.r,
                sourceColor.g * GhostTint.g, sourceColor.b * GhostTint.b,
                Mathf.Min(sourceColor.a, GhostTint.a));
            renderer.enabled = show && sprite != null;
        }

        private void EnsureGhostRoot()
        {
            if (ghostRoot != null) return;
            var root = new GameObject("FogMemoryGhosts");
            root.transform.SetParent(transform, false);
            ghostRoot = root.transform;
        }

        private void HandleObserverViewChanged(bool _) => RefreshGhosts();

        private bool CaptureBuilding(int team, IDefensiveBuildingHost host,
            Component component, EntityId id)
        {
            if (!buildings[team].TryGetValue(id, out BuildingMemorySnapshot snapshot))
            {
                snapshot = new BuildingMemorySnapshot();
                buildings[team].Add(id, snapshot);
            }
            if (TryCapture(host, component, id, snapshot)) return true;
            buildings[team].Remove(id);
            return false;
        }

        private bool CaptureResource(int team, ResourceNode node, EntityId id)
        {
            if (!resources[team].TryGetValue(id, out ResourceMemorySnapshot snapshot))
            {
                snapshot = new ResourceMemorySnapshot();
                resources[team].Add(id, snapshot);
            }
            if (TryCapture(node, id, snapshot)) return true;
            resources[team].Remove(id);
            return false;
        }

        private static bool TryCapture(IDefensiveBuildingHost host, Component component, EntityId id,
            BuildingMemorySnapshot snapshot)
        {
            SpriteRenderer visual = component is BuildingSite site
                ? site.PresentationRenderer
                : component is CastleDropOff castle ? castle.PresentationRenderer
                : host.HostTransform.GetComponentInChildren<SpriteRenderer>();
            if (visual == null || visual.sprite == null) return false;
            float progress = component is BuildingSite building
                ? building.ConstructionProgress : 1f;
            snapshot.RuntimeId = id;
            snapshot.BuildingType = host.Type;
            snapshot.Faction = host.Faction;
            snapshot.LogicalPosition = host.HostTransform.position;
            snapshot.FootprintOrigin = host.FootprintOrigin;
            snapshot.Footprint = host.Footprint;
            snapshot.Sprite = visual.sprite;
            BuildingSelectionPulse pulse = component.GetComponent<BuildingSelectionPulse>();
            snapshot.VisualPosition = pulse != null && pulse.IsConfigured
                ? pulse.StableWorldPosition : visual.transform.position;
            snapshot.VisualRotation = visual.transform.rotation;
            snapshot.VisualScale = pulse != null && pulse.IsConfigured
                ? pulse.StableLossyScale : visual.transform.lossyScale;
            snapshot.VisualColor = visual.color;
            snapshot.SortingLayerId = visual.sortingLayerID;
            snapshot.SortingOrder = visual.sortingOrder;
            snapshot.FlipX = visual.flipX;
            snapshot.Completed = host.IsProductionReady;
            snapshot.ConstructionProgress = progress;
            snapshot.HealthRatio = host.MaxHP > 0
                ? Mathf.Clamp01(host.CurrentHP / host.MaxHP) : 0f;
            snapshot.DefensiveAttackRange = host.DefenseData != null
                ? host.DefenseData.DefensiveAttackRange : 0f;
            snapshot.LastSeenTime = Time.time;
            return true;
        }

        private bool TryCapture(ResourceNode node, EntityId id,
            ResourceMemorySnapshot snapshot)
        {
            SpriteRenderer visual = node.PresentationRenderer;
            if (visual == null || visual.sprite == null) return false;
            snapshot.RuntimeId = id;
            snapshot.ResourceType = node.ResourceType;
            snapshot.LogicalPosition = node.transform.position;
            snapshot.LogicalCell = grid.WorldToGrid(node.transform.position);
            snapshot.Sprite = visual.sprite;
            snapshot.VisualPosition = visual.transform.position;
            snapshot.VisualRotation = visual.transform.rotation;
            snapshot.VisualScale = visual.transform.lossyScale;
            snapshot.VisualColor = visual.color;
            snapshot.SortingLayerId = visual.sortingLayerID;
            snapshot.SortingOrder = visual.sortingOrder;
            snapshot.FlipX = visual.flipX;
            snapshot.RemainingRatio = node.MaximumAmount > 0
                ? Mathf.Clamp01((float)node.RemainingAmount / node.MaximumAmount) : 0f;
            snapshot.RemainingAmount = node.RemainingAmount;
            snapshot.MaximumAmount = node.MaximumAmount;
            snapshot.LastSeenTime = Time.time;
            return true;
        }

        private void RemoveMissingGhosts<T>(Dictionary<EntityId, SpriteRenderer> ghosts,
            IReadOnlyDictionary<EntityId, T> snapshots)
        {
            removal.Clear();
            foreach (KeyValuePair<EntityId, SpriteRenderer> item in ghosts)
                if (!snapshots.ContainsKey(item.Key)) removal.Add(item.Key);
            for (int i = 0; i < removal.Count; i++)
            {
                if (ghosts[removal[i]] != null) Destroy(ghosts[removal[i]].gameObject);
                ghosts.Remove(removal[i]);
            }
        }

        private static int ActiveCount(Dictionary<EntityId, SpriteRenderer> ghosts)
        {
            int count = 0;
            foreach (SpriteRenderer renderer in ghosts.Values)
                if (renderer != null && renderer.enabled) count++;
            return count;
        }

        private static int TeamIndex(Faction faction) => faction == Faction.Red ? 1 : 0;
        private static Faction TeamFaction(int index) => index == 1 ? Faction.Red : Faction.Blue;
    }
}
