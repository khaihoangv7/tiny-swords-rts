using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    public enum StrategicRisk : byte
    {
        Safe = 0,
        Risky = 1,
        Dangerous = 2,
        ForbiddenForCivilian = 3
    }

    public enum StrategicPathKind
    {
        Civilian,
        Scout,
        Army
    }

    // A reusable, knowledge-only AI decision aid. It never changes gameplay
    // visibility, occupancy, combat or normal player pathfinding.
    [Serializable]
    public sealed class StrategicSafetyMap
    {
        private static readonly ProfilerMarker UpdateMarker =
            new("TinyKingdom.AI.StrategicSafetyMapUpdate");
        private const float DefensiveSafetyMargin = 1.5f;
        private const float MilitaryReactionBuffer = 2.5f;
        private const float EnemyBaseOuterBuffer = 3f;

        [SerializeField, Min(1f)] private float homeEconomicRadius = 11f;

        private byte[] civilianRisk = Array.Empty<byte>();
        private byte[] scoutRisk = Array.Empty<byte>();
        private byte[] armyRisk = Array.Empty<byte>();
        private byte[] explored = Array.Empty<byte>();
        private GridManager grid;
        private int width;
        private int height;
        private int lastSignature;
        private bool initialized;
        private readonly StrategicTraversalPolicy civilianTraversal;
        private readonly StrategicTraversalPolicy scoutTraversal;
        private readonly StrategicTraversalPolicy civilianRetreatTraversal;
        private readonly StrategicTraversalPolicy scoutRetreatTraversal;
        private readonly StrategicTraversalPolicy armyTraversal;

        public StrategicSafetyMap()
        {
            civilianTraversal = new StrategicTraversalPolicy(this,
                StrategicPathKind.Civilian, false);
            scoutTraversal = new StrategicTraversalPolicy(this,
                StrategicPathKind.Scout, false);
            civilianRetreatTraversal = new StrategicTraversalPolicy(this,
                StrategicPathKind.Civilian, true);
            scoutRetreatTraversal = new StrategicTraversalPolicy(this,
                StrategicPathKind.Scout, true);
            armyTraversal = new StrategicTraversalPolicy(this,
                StrategicPathKind.Army, false);
        }

        public float HomeEconomicRadius => homeEconomicRadius;
        public bool HasHomeAnchor { get; private set; }
        public Vector3 HomeAnchor { get; private set; }
        public int KnownStaticDangerCount { get; private set; }
        public int VisibleDynamicThreatCount { get; private set; }
        public IPathTraversalPolicy CivilianTraversal => civilianTraversal;
        public IPathTraversalPolicy ScoutTraversal => scoutTraversal;
        public IPathTraversalPolicy CivilianRetreatTraversal
            => civilianRetreatTraversal;
        public IPathTraversalPolicy ScoutRetreatTraversal => scoutRetreatTraversal;
        public IPathTraversalPolicy ArmyTraversal => armyTraversal;

        public void Reset()
        {
            initialized = false;
            lastSignature = 0;
            HasHomeAnchor = false;
            KnownStaticDangerCount = 0;
            VisibleDynamicThreatCount = 0;
            if (civilianRisk.Length > 0) Array.Clear(civilianRisk, 0, civilianRisk.Length);
            if (scoutRisk.Length > 0) Array.Clear(scoutRisk, 0, scoutRisk.Length);
            if (armyRisk.Length > 0) Array.Clear(armyRisk, 0, armyRisk.Length);
            if (explored.Length > 0) Array.Clear(explored, 0, explored.Length);
        }

        public void Refresh(FactionKnowledge knowledge)
        {
            using var marker = UpdateMarker.Auto();
            GridManager candidateGrid = knowledge?.TeamVision?.GridManager;
            if (candidateGrid == null)
            {
                Reset();
                grid = null;
                return;
            }
            EnsureBuffers(candidateGrid);
            ResolveHomeAnchor(knowledge);
            int signature = ComputeSignature(knowledge);
            if (initialized && signature == lastSignature) return;
            initialized = true;
            lastSignature = signature;
            Array.Clear(civilianRisk, 0, civilianRisk.Length);
            Array.Clear(scoutRisk, 0, scoutRisk.Length);
            Array.Clear(armyRisk, 0, armyRisk.Length);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                explored[y * width + x] = knowledge.IsCellExplored(
                    new Vector2Int(x, y)) ? (byte)1 : (byte)0;
            KnownStaticDangerCount = 0;
            VisibleDynamicThreatCount = 0;

            IReadOnlyList<BuildingKnowledgeSnapshot> buildings =
                knowledge.KnownEnemyBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingKnowledgeSnapshot item = buildings[i];
                if (!item.WasCompleted || item.LastSeenHealthRatio <= 0f) continue;
                if (item.BuildingType is BuildingType.Tower or BuildingType.Castle)
                {
                    float defenseRadius = Mathf.Max(0f, item.DefensiveAttackRange)
                        + DefensiveSafetyMargin;
                    Rasterize(item.LastSeenPosition, defenseRadius,
                        StrategicRisk.ForbiddenForCivilian,
                        StrategicRisk.ForbiddenForCivilian,
                        StrategicRisk.Dangerous);
                    Rasterize(item.LastSeenPosition,
                        defenseRadius + EnemyBaseOuterBuffer,
                        StrategicRisk.Dangerous, StrategicRisk.Risky,
                        StrategicRisk.Risky);
                    KnownStaticDangerCount++;
                }
                else if (item.BuildingType is BuildingType.Barracks
                    or BuildingType.Archery or BuildingType.Monastery)
                {
                    Rasterize(item.LastSeenPosition, 3.5f,
                        StrategicRisk.Risky, StrategicRisk.Risky,
                        StrategicRisk.Risky);
                    KnownStaticDangerCount++;
                }
            }

            IReadOnlyList<VisibleUnitObservation> units = knowledge.VisibleEnemyUnits;
            for (int i = 0; i < units.Count; i++)
            {
                VisibleUnitObservation item = units[i];
                if (item.UnitType is UnitType.Warrior or UnitType.Archer
                    or UnitType.Lancer or UnitType.GoblinTorch
                    or UnitType.GoblinTNT or UnitType.GoblinBarrel)
                {
                    float radius = Mathf.Max(1f, item.AttackRange)
                        + MilitaryReactionBuffer;
                    Rasterize(item.Position, radius,
                        StrategicRisk.ForbiddenForCivilian, StrategicRisk.Dangerous,
                        StrategicRisk.Dangerous);
                    VisibleDynamicThreatCount++;
                }
                else if (item.UnitType == UnitType.Monk)
                {
                    Rasterize(item.Position, 3f,
                        StrategicRisk.Dangerous, StrategicRisk.Risky,
                        StrategicRisk.Risky);
                    VisibleDynamicThreatCount++;
                }
                else if (item.UnitType == UnitType.Pawn)
                    Rasterize(item.Position, 1.5f,
                        StrategicRisk.Risky, StrategicRisk.Safe,
                        StrategicRisk.Safe);
            }
        }

        public StrategicRisk RiskAt(Vector3 worldPosition, StrategicPathKind kind)
        {
            if (grid == null) return StrategicRisk.Safe;
            Vector2Int cell = grid.WorldToGrid(worldPosition);
            return RiskAt(cell, kind);
        }

        public StrategicRisk RiskAt(Vector2Int cell, StrategicPathKind kind)
        {
            if (grid == null || !grid.IsInsideGrid(cell.x, cell.y))
                return StrategicRisk.ForbiddenForCivilian;
            byte[] buffer = kind switch
            {
                StrategicPathKind.Civilian => civilianRisk,
                StrategicPathKind.Scout => scoutRisk,
                _ => armyRisk
            };
            return (StrategicRisk)buffer[cell.y * width + cell.x];
        }

        public bool IsEligibleEconomicPosition(FactionKnowledge knowledge,
            Vector3 worldPosition)
        {
            if (!HasHomeAnchor || grid == null
                || Vector2.Distance(worldPosition, HomeAnchor) > homeEconomicRadius)
                return false;
            Vector2Int cell = grid.WorldToGrid(worldPosition);
            return knowledge != null && knowledge.IsCellExplored(cell)
                && RiskAt(cell, StrategicPathKind.Civilian) <= StrategicRisk.Risky;
        }

        public bool IsPathSafe(IReadOnlyList<Vector3> path, StrategicPathKind kind,
            int startIndex = 0)
        {
            if (path == null) return false;
            for (int i = Mathf.Clamp(startIndex, 0, path.Count); i < path.Count; i++)
            {
                StrategicRisk risk = RiskAt(path[i], kind);
                if (kind == StrategicPathKind.Army
                    ? risk >= StrategicRisk.ForbiddenForCivilian
                    : risk >= StrategicRisk.Dangerous) return false;
            }
            return true;
        }

        public bool TryFindRetreatCell(FactionKnowledge knowledge, Vector3 origin,
            StrategicPathKind kind, out Vector2Int result)
            => TryFindRetreatCell(knowledge, origin, kind, 0, false, default, out result);

        public bool TryFindLocalRetreatCell(FactionKnowledge knowledge, Vector3 origin,
            StrategicPathKind kind, int cellRadius, bool hasThreatCenter,
            Vector3 threatCenter, out Vector2Int result)
            => TryFindRetreatCell(knowledge, origin, kind, Mathf.Max(1, cellRadius),
                hasThreatCenter, threatCenter, out result);

        private bool TryFindRetreatCell(FactionKnowledge knowledge, Vector3 origin,
            StrategicPathKind kind, int cellRadius, bool hasThreatCenter,
            Vector3 threatCenter, out Vector2Int result)
        {
            result = default;
            if (grid == null || knowledge == null || !HasHomeAnchor) return false;
            bool found = false;
            float bestScore = float.PositiveInfinity;
            Vector2Int originCell = grid.WorldToGrid(origin);
            int minX = cellRadius > 0 ? Mathf.Max(0, originCell.x - cellRadius) : 0;
            int maxX = cellRadius > 0 ? Mathf.Min(width - 1, originCell.x + cellRadius)
                : width - 1;
            int minY = cellRadius > 0 ? Mathf.Max(0, originCell.y - cellRadius) : 0;
            int maxY = cellRadius > 0 ? Mathf.Min(height - 1, originCell.y + cellRadius)
                : height - 1;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var cell = new Vector2Int(x, y);
                GridCell navigation = grid.GetCell(x, y);
                if (navigation == null || !navigation.Walkable || navigation.Occupied
                    || !knowledge.IsCellExplored(cell)
                    || RiskAt(cell, kind) != StrategicRisk.Safe) continue;
                Vector3 position = navigation.WorldPosition;
                float homeDistance = Vector2.Distance(position, HomeAnchor);
                if (kind == StrategicPathKind.Civilian
                    && homeDistance > homeEconomicRadius) continue;
                float threatSeparation = hasThreatCenter
                    ? Vector2.Distance(position, threatCenter) : 0f;
                float score = Vector2.Distance(position, origin) + homeDistance * 0.2f
                    - threatSeparation * 0.75f;
                if (!found || score < bestScore - 0.0001f
                    || Mathf.Abs(score - bestScore) <= 0.0001f
                    && (y < result.y || y == result.y && x < result.x))
                {
                    found = true;
                    bestScore = score;
                    result = cell;
                }
            }
            return found;
        }

        private void EnsureBuffers(GridManager candidate)
        {
            grid = candidate;
            if (width == grid.Width && height == grid.Height
                && civilianRisk.Length == width * height
                && armyRisk.Length == width * height) return;
            width = grid.Width;
            height = grid.Height;
            civilianRisk = new byte[width * height];
            scoutRisk = new byte[width * height];
            armyRisk = new byte[width * height];
            explored = new byte[width * height];
            initialized = false;
        }

        private void ResolveHomeAnchor(FactionKnowledge knowledge)
        {
            HasHomeAnchor = false;
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost item = buildings[i];
                if (item == null || !item.IsAlive || item.Type != BuildingType.Castle) continue;
                HomeAnchor = item.HostTransform.position;
                HasHomeAnchor = true;
                return;
            }
        }

        private int ComputeSignature(FactionKnowledge knowledge)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + width;
                hash = hash * 31 + height;
                hash = hash * 31 + (HasHomeAnchor ? Quantize(HomeAnchor.x) : 0);
                hash = hash * 31 + (HasHomeAnchor ? Quantize(HomeAnchor.y) : 0);
                hash = hash * 31 + knowledge.ExploredCellCount;
                IReadOnlyList<BuildingKnowledgeSnapshot> buildings =
                    knowledge.KnownEnemyBuildings;
                hash = hash * 31 + buildings.Count;
                for (int i = 0; i < buildings.Count; i++)
                {
                    BuildingKnowledgeSnapshot item = buildings[i];
                    hash = hash * 31 + (int)item.BuildingType;
                    hash = hash * 31 + Quantize(item.LastSeenPosition.x);
                    hash = hash * 31 + Quantize(item.LastSeenPosition.y);
                    hash = hash * 31 + Quantize(item.DefensiveAttackRange);
                    hash = hash * 31 + Quantize(item.LastSeenHealthRatio);
                }
                IReadOnlyList<VisibleUnitObservation> units = knowledge.VisibleEnemyUnits;
                hash = hash * 31 + units.Count;
                for (int i = 0; i < units.Count; i++)
                {
                    VisibleUnitObservation item = units[i];
                    hash = hash * 31 + (int)item.UnitType;
                    hash = hash * 31 + Quantize(item.Position.x);
                    hash = hash * 31 + Quantize(item.Position.y);
                    hash = hash * 31 + Quantize(item.AttackRange);
                }
                return hash;
            }
        }

        private void Rasterize(Vector3 center, float radius, StrategicRisk civilian,
            StrategicRisk scout, StrategicRisk army)
        {
            if (grid == null || radius <= 0f) return;
            Vector2Int min = grid.WorldToGrid(center - new Vector3(radius, radius));
            Vector2Int max = grid.WorldToGrid(center + new Vector3(radius, radius));
            float radiusSqr = radius * radius;
            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(height - 1, max.y); y++)
            for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(width - 1, max.x); x++)
            {
                if (((Vector2)(grid.GridToWorld(x, y) - center)).sqrMagnitude > radiusSqr)
                    continue;
                int index = y * width + x;
                civilianRisk[index] = Math.Max(civilianRisk[index], (byte)civilian);
                scoutRisk[index] = Math.Max(scoutRisk[index], (byte)scout);
                armyRisk[index] = Math.Max(armyRisk[index], (byte)army);
            }
        }

        private static int Quantize(float value) => Mathf.RoundToInt(value * 4f);

        private sealed class StrategicTraversalPolicy : IPathTraversalPolicy
        {
            private readonly StrategicSafetyMap owner;
            private readonly StrategicPathKind kind;
            private readonly bool retreat;

            public StrategicTraversalPolicy(StrategicSafetyMap owner,
                StrategicPathKind kind, bool retreat)
            {
                this.owner = owner;
                this.kind = kind;
                this.retreat = retreat;
            }

            public bool CanEnter(GridCell cell)
            {
                if (cell == null) return false;
                bool explored = owner.IsExplored(cell.GridX, cell.GridY);
                if ((kind == StrategicPathKind.Civilian || retreat) && !explored)
                    return false;
                if (retreat) return true;
                StrategicRisk risk = owner.RiskAt(
                    new Vector2Int(cell.GridX, cell.GridY), kind);
                return kind == StrategicPathKind.Army
                    ? risk < StrategicRisk.ForbiddenForCivilian
                    : risk < StrategicRisk.Dangerous;
            }

            public double AdditionalCost(GridCell from, GridCell to)
            {
                StrategicRisk risk = owner.RiskAt(
                    new Vector2Int(to.GridX, to.GridY), kind);
                if (retreat)
                    return risk switch
                    {
                        StrategicRisk.Risky => 20d,
                        StrategicRisk.Dangerous => 100d,
                        StrategicRisk.ForbiddenForCivilian => 300d,
                        _ => 0d
                    };
                return risk switch
                {
                    StrategicRisk.Risky => kind switch
                    {
                        StrategicPathKind.Scout => 4d,
                        StrategicPathKind.Army => 2d,
                        _ => 8d
                    },
                    StrategicRisk.Dangerous => kind switch
                    {
                        StrategicPathKind.Scout => 25d,
                        StrategicPathKind.Army => 8d,
                        _ => 1000d
                    },
                    StrategicRisk.ForbiddenForCivilian => kind == StrategicPathKind.Army
                        ? 18d : 0d,
                    _ => 0d
                };
            }
        }

        private bool IsExplored(int x, int y)
            => x >= 0 && y >= 0 && x < width && y < height
                && explored[y * width + x] != 0;
    }
}
