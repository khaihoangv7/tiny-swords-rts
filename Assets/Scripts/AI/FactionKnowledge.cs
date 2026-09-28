using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEngine;

namespace TinyKingdom.AI
{
    public enum ObservedThreatActivity
    {
        None = 0,
        AttackingUnit = 1,
        AttackingBuilding = 2,
        AttackingCastle = 3
    }

    public readonly struct VisibleUnitObservation
    {
        public VisibleUnitObservation(EntityId id, Faction faction, UnitType type,
            Vector3 position, int currentHP, int maxHP, UnitState state,
            ObservedThreatActivity threatActivity, float attackRange, float moveSpeed)
        {
            RuntimeId = id;
            Faction = faction;
            UnitType = type;
            Position = position;
            CurrentHP = currentHP;
            MaxHP = maxHP;
            State = state;
            ThreatActivity = threatActivity;
            AttackRange = attackRange;
            MoveSpeed = moveSpeed;
        }

        public EntityId RuntimeId { get; }
        public Faction Faction { get; }
        public UnitType UnitType { get; }
        public Vector3 Position { get; }
        public int CurrentHP { get; }
        public int MaxHP { get; }
        public UnitState State { get; }
        public ObservedThreatActivity ThreatActivity { get; }
        public float AttackRange { get; }
        public float MoveSpeed { get; }
    }

    public readonly struct BuildingKnowledgeSnapshot
    {
        public BuildingKnowledgeSnapshot(EntityId id, BuildingType type, Faction faction,
            Vector3 position, Vector2Int footprintOrigin, Vector2Int footprint,
            bool completed, float constructionProgress, float healthRatio,
            float defensiveAttackRange)
        {
            RuntimeId = id;
            BuildingType = type;
            Faction = faction;
            LastSeenPosition = position;
            FootprintOrigin = footprintOrigin;
            Footprint = footprint;
            WasCompleted = completed;
            LastSeenConstructionProgress = constructionProgress;
            LastSeenHealthRatio = healthRatio;
            DefensiveAttackRange = defensiveAttackRange;
        }

        public EntityId RuntimeId { get; }
        public BuildingType BuildingType { get; }
        public Faction Faction { get; }
        public Vector3 LastSeenPosition { get; }
        public Vector2Int FootprintOrigin { get; }
        public Vector2Int Footprint { get; }
        public bool WasCompleted { get; }
        public float LastSeenConstructionProgress { get; }
        public float LastSeenHealthRatio { get; }
        public float DefensiveAttackRange { get; }
    }

    public readonly struct ResourceKnowledgeSnapshot
    {
        public ResourceKnowledgeSnapshot(EntityId id, ResourceType type, Vector3 position,
            Vector2Int cell, int remainingAmount, int maximumAmount)
        {
            RuntimeId = id;
            ResourceType = type;
            LastSeenPosition = position;
            LogicalCell = cell;
            LastSeenRemainingAmount = remainingAmount;
            MaximumAmount = maximumAmount;
        }

        public EntityId RuntimeId { get; }
        public ResourceType ResourceType { get; }
        public Vector3 LastSeenPosition { get; }
        public Vector2Int LogicalCell { get; }
        public int LastSeenRemainingAmount { get; }
        public int MaximumAmount { get; }
    }

    // The only observation surface used by Commanders. Enemy observations are copied
    // from current team vision and rebuilt without live references on every refresh.
    public sealed class FactionKnowledge : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Red;
        [SerializeField] private TeamVisionSystem teamVision;
        [SerializeField] private FogMemorySystem fogMemory;
        [SerializeField] private ResourceStockpile stockpile;
        [SerializeField] private FactionPopulation population;

        private readonly List<UnitController> ownUnits = new();
        private readonly List<IDefensiveBuildingHost> ownBuildings = new();
        private readonly List<VisibleUnitObservation> visibleEnemyUnits = new();
        private readonly List<BuildingKnowledgeSnapshot> visibleEnemyBuildings = new();
        private readonly List<BuildingKnowledgeSnapshot> knownEnemyBuildings = new();
        private readonly List<ResourceNode> visibleResources = new();
        private readonly List<ResourceKnowledgeSnapshot> knownResources = new();

        public Faction Faction => faction;
        public TeamVisionSystem TeamVision => teamVision;
        public FogMemorySystem FogMemory => fogMemory;
        public ResourceStockpile Stockpile => stockpile;
        public FactionPopulation Population => population;
        public IReadOnlyList<UnitController> OwnUnits => ownUnits;
        public IReadOnlyList<IDefensiveBuildingHost> OwnBuildings => ownBuildings;
        public IReadOnlyList<VisibleUnitObservation> VisibleEnemyUnits => visibleEnemyUnits;
        public IReadOnlyList<BuildingKnowledgeSnapshot> VisibleEnemyBuildings
            => visibleEnemyBuildings;
        public IReadOnlyList<BuildingKnowledgeSnapshot> KnownEnemyBuildings
            => knownEnemyBuildings;
        public IReadOnlyList<ResourceNode> VisibleResources => visibleResources;
        public IReadOnlyList<ResourceKnowledgeSnapshot> KnownResources => knownResources;
        public int ExploredCellCount { get; private set; }
        public int TotalCellCount => teamVision != null && teamVision.GridManager != null
            ? teamVision.GridManager.Width * teamVision.GridManager.Height : 0;
        public float ExploredPercent => TotalCellCount > 0
            ? 100f * ExploredCellCount / TotalCellCount : 0f;
        public bool IsReady => FactionRelations.IsPlayable(faction)
            && teamVision != null && teamVision.IsReady && fogMemory != null
            && fogMemory.IsInitialized && stockpile != null && stockpile.Faction == faction
            && population != null && population.Faction == faction;

        public void Configure(Faction owner, TeamVisionSystem vision, FogMemorySystem memory,
            ResourceStockpile resources, FactionPopulation factionPopulation)
        {
            faction = owner;
            teamVision = vision;
            fogMemory = memory;
            stockpile = resources;
            population = factionPopulation;
            Refresh();
        }

        public void Refresh()
        {
            ownUnits.Clear();
            ownBuildings.Clear();
            visibleEnemyUnits.Clear();
            visibleEnemyBuildings.Clear();
            knownEnemyBuildings.Clear();
            visibleResources.Clear();
            knownResources.Clear();
            ExploredCellCount = 0;
            if (teamVision == null || !teamVision.IsReady) return;

            IReadOnlyList<UnitController> units = UnitController.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit == null || !unit.IsAlive || !unit.IsWorldParticipant) continue;
                if (unit.Faction == faction) ownUnits.Add(unit);
                else if (FactionRelations.AreHostile(faction, unit.Faction)
                    && teamVision.IsVisible(faction, unit.transform.position))
                {
                    HealthComponent health = unit.GetComponent<HealthComponent>();
                    if (health?.CombatData == null) continue;
                    visibleEnemyUnits.Add(new VisibleUnitObservation(unit.GetEntityId(),
                        unit.Faction, health.CombatData.UnitType,
                        unit.transform.position, health.CurrentHP,
                        health.MaxHP, unit.CurrentState,
                        ObserveThreatActivity(unit), health.CombatData.AttackRange,
                        health.CombatData.MoveSpeed));
                }
            }

            IReadOnlyList<IVisionSource> sources = VisionSourceRegistry.ActiveSources;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] is not BuildingVisionSource source || source == null
                    || source.Host is not IDefensiveBuildingHost host
                    || source.Host is not Component component || !host.IsAlive)
                    continue;
                if (host.Faction == faction) ownBuildings.Add(host);
                else if (teamVision.IsVisible(faction, host.HostTransform.position))
                    visibleEnemyBuildings.Add(CopyVisibleBuilding(host, component));
            }

            IReadOnlyList<ResourceNode> nodes = ResourceNode.ActiveNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                ResourceNode node = nodes[i];
                if (node != null && teamVision.IsVisible(faction, node.transform.position))
                    visibleResources.Add(node);
            }

            if (fogMemory != null)
            {
                foreach (BuildingMemorySnapshot item in fogMemory.GetLastSeenBuildings(faction))
                    knownEnemyBuildings.Add(CopyBuilding(item));
                foreach (ResourceMemorySnapshot item in fogMemory.GetLastSeenResources(faction))
                    knownResources.Add(CopyResource(item));
            }

            GridManager grid = teamVision.GridManager;
            if (grid == null) return;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (teamVision.GetVisionState(faction, new Vector2Int(x, y))
                    != VisionState.Unexplored) ExploredCellCount++;
        }

        public bool IsCellVisible(Vector2Int cell)
            => teamVision != null
                && teamVision.GetVisionState(faction, cell) == VisionState.Visible;

        public bool IsCellExplored(Vector2Int cell)
            => teamVision != null
                && teamVision.GetVisionState(faction, cell) != VisionState.Unexplored;

        public bool IsResourceVisible(ResourceNode target)
        {
            if (target == null || teamVision == null
                || !teamVision.IsVisible(faction, target.transform.position)) return false;
            for (int i = 0; i < visibleResources.Count; i++)
                if (visibleResources[i] == target) return true;
            return false;
        }

        public bool TryGetVisibleResource(EntityId id, out ResourceNode resource)
        {
            for (int i = 0; i < visibleResources.Count; i++)
            {
                ResourceNode candidate = visibleResources[i];
                if (candidate != null && candidate.GetEntityId().CompareTo(id) == 0)
                {
                    resource = candidate;
                    return true;
                }
            }
            resource = null;
            return false;
        }

        public bool TryGetVisibleEnemy(EntityId id, out VisibleUnitObservation observation)
        {
            for (int i = 0; i < visibleEnemyUnits.Count; i++)
            {
                VisibleUnitObservation candidate = visibleEnemyUnits[i];
                if (candidate.RuntimeId.CompareTo(id) != 0) continue;
                observation = candidate;
                return true;
            }
            observation = default;
            return false;
        }

        public bool TryGetVisibleEnemyBuilding(EntityId id,
            out BuildingKnowledgeSnapshot observation)
        {
            for (int i = 0; i < visibleEnemyBuildings.Count; i++)
            {
                BuildingKnowledgeSnapshot candidate = visibleEnemyBuildings[i];
                if (candidate.RuntimeId.CompareTo(id) != 0) continue;
                observation = candidate;
                return true;
            }
            observation = default;
            return false;
        }

        public bool TryGetKnownEnemyBuilding(EntityId id,
            out BuildingKnowledgeSnapshot observation)
        {
            for (int i = 0; i < knownEnemyBuildings.Count; i++)
            {
                BuildingKnowledgeSnapshot candidate = knownEnemyBuildings[i];
                if (candidate.RuntimeId.CompareTo(id) != 0) continue;
                observation = candidate;
                return true;
            }
            observation = default;
            return false;
        }

        private ObservedThreatActivity ObserveThreatActivity(UnitController unit)
        {
            CombatController combat = unit != null ? unit.GetComponent<CombatController>() : null;
            BuildingCombatTarget building = combat?.BuildingTarget;
            if (building != null && building.IsAlive && building.Faction == faction)
                return building.Type == BuildingType.Castle
                    ? ObservedThreatActivity.AttackingCastle
                    : ObservedThreatActivity.AttackingBuilding;
            UnitController target = combat?.Target;
            return target != null && target.IsAlive && target.Faction == faction
                ? ObservedThreatActivity.AttackingUnit
                : ObservedThreatActivity.None;
        }

        private static BuildingKnowledgeSnapshot CopyVisibleBuilding(
            IDefensiveBuildingHost host, Component component)
        {
            float progress = component is BuildingSite site ? site.ConstructionProgress : 1f;
            return new BuildingKnowledgeSnapshot(component.GetEntityId(), host.Type, host.Faction,
                host.HostTransform.position, host.FootprintOrigin, host.Footprint,
                host.IsProductionReady, progress,
                host.MaxHP > 0 ? Mathf.Clamp01(host.CurrentHP / host.MaxHP) : 0f,
                host.DefenseData != null ? host.DefenseData.DefensiveAttackRange : 0f);
        }

        private static BuildingKnowledgeSnapshot CopyBuilding(BuildingMemorySnapshot item)
            => new(item.RuntimeId, item.BuildingType, item.Faction, item.LogicalPosition,
                item.FootprintOrigin, item.Footprint, item.Completed,
                item.ConstructionProgress, item.HealthRatio, item.DefensiveAttackRange);

        private static ResourceKnowledgeSnapshot CopyResource(ResourceMemorySnapshot item)
            => new(item.RuntimeId, item.ResourceType, item.LogicalPosition, item.LogicalCell,
                item.RemainingAmount, item.MaximumAmount);
    }
}
