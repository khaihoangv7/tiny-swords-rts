using System;
using System.Collections.Generic;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    public enum ScoutSafetyState { Exploring, Retreating }

    [Serializable]
    public sealed class ScoutingCommanderPolicy
    {
        private static readonly ProfilerMarker FrontierSearchMarker =
            new("TinyKingdom.AI.FrontierSearch");
        private static readonly ProfilerMarker ScoutRiskMarker =
            new("TinyKingdom.AI.ScoutRiskEvaluation");
        private static readonly ProfilerMarker ScoutRetreatMarker =
            new("TinyKingdom.AI.ScoutRetreat");
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.35f;
        [SerializeField, Min(0.5f)] private float unreachableRetrySeconds = 5f;
        [SerializeField, Min(0.5f)] private float frontierRetrySeconds = 2f;
        [SerializeField, Min(1)] private int minimumPawnsForWorkerScout = 4;

        private readonly Dictionary<int, float> blockedUntil = new();
        private UnitController scout;
        private Vector2Int destinationCell;
        private bool hasDestination;
        private ScoutSafetyState safetyState;
        private string dangerReason = "None";
        private Vector2Int retreatCell;
        private bool hasRetreatDestination;
        private bool explorationExhausted;
        private int exploredCellsAtExhaustion;
        private float nextFrontierSearchTime;

        public UnitController Scout => scout;
        public bool HasDestination => hasDestination;
        public Vector2Int DestinationCell => destinationCell;
        public ScoutSafetyState SafetyState => safetyState;
        public string DangerReason => dangerReason;
        public Vector2Int RetreatCell => retreatCell;
        public bool HasRetreatDestination => hasRetreatDestination;

        public int MinimumPawnsForWorkerScout => minimumPawnsForWorkerScout;
        public bool ExplorationExhausted => explorationExhausted;
        public uint SuccessfulDestinationSerial { get; private set; }
        public float LastSuccessfulDestinationTime { get; private set; } = -1f;

        public void Reset()
        {
            scout = null;
            hasDestination = false;
            safetyState = ScoutSafetyState.Exploring;
            dangerReason = "None";
            hasRetreatDestination = false;
            explorationExhausted = false;
            exploredCellsAtExhaustion = 0;
            nextFrontierSearchTime = 0f;
            SuccessfulDestinationSerial = 0;
            LastSuccessfulDestinationTime = -1f;
            blockedUntil.Clear();
        }

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles)
            => Tick(context, roles, null);

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles,
            StrategicSafetyMap safety)
        {
            if (explorationExhausted)
            {
                if (context.Knowledge.ExploredCellCount <= exploredCellsAtExhaustion)
                    return;
                explorationExhausted = false;
            }
            UnitController preferred = ChooseScout(context.Knowledge, roles);
            if (!IsUsableScout(context, scout)
                || (preferred != null && ScoutPriority(preferred) < ScoutPriority(scout)))
            {
                ReleasePawnScout(roles);
                scout = preferred;
                if (IsPawn(scout) && (roles == null
                    || !roles.TryAssign(scout, CommanderPawnRole.Scout))) scout = null;
                hasDestination = false;
                safetyState = ScoutSafetyState.Exploring;
                hasRetreatDestination = false;
            }
            if (scout == null) return;

            UnitMovement movement = scout.GetComponent<UnitMovement>();
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (movement == null || grid == null)
            {
                ReleasePawnScout(roles);
                scout = null;
                hasDestination = false;
                return;
            }

            if (safety != null && ShouldRetreat(scout, movement, safety))
            {
                BeginOrContinueRetreat(context, movement, safety);
                return;
            }
            if (safetyState == ScoutSafetyState.Retreating)
            {
                if (movement.IsMoving) return;
                safetyState = ScoutSafetyState.Exploring;
                hasRetreatDestination = false;
                dangerReason = "None";
            }

            if (hasDestination)
            {
                Vector3 destination = grid.GridToWorld(destinationCell.x, destinationCell.y);
                bool reached = Vector2.Distance(scout.transform.position, destination)
                    <= arrivalDistance;
                if (movement.IsMoving && !reached && IsFrontier(context, destinationCell)
                    && (safety == null || safety.RiskAt(destinationCell,
                        StrategicPathKind.Scout) < StrategicRisk.ForbiddenForCivilian)
                    && (safety == null || safety.IsPathSafe(movement.CurrentPath,
                        StrategicPathKind.Scout, movement.CurrentPathWaypointIndex))) return;
                hasDestination = false;
            }

            if (context.Time < nextFrontierSearchTime) return;

            const int maxAttemptsPerThink = 4;
            for (int attempt = 0; attempt < maxAttemptsPerThink; attempt++)
            {
                if (!TryFindFrontier(context, scout, safety, out Vector2Int frontier))
                {
                    // Exploration is complete or every remaining frontier is on
                    // cooldown. A complete map releases the role; cooldown-only
                    // cases are reconsidered without command spam on a later tick.
                    if (!HasAnyFrontier(context))
                    {
                        ReleasePawnScout(roles);
                        scout = null;
                        explorationExhausted = true;
                        exploredCellsAtExhaustion = context.Knowledge.ExploredCellCount;
                    }
                    else nextFrontierSearchTime = context.Time + frontierRetrySeconds;
                    hasDestination = false;
                    return;
                }
                CommanderCommandResult result = context.Commands.TryMove(scout,
                    grid.GridToWorld(frontier.x, frontier.y), safety?.ScoutTraversal);
                if (result == CommanderCommandResult.Accepted)
                {
                    destinationCell = frontier;
                    hasDestination = true;
                    SuccessfulDestinationSerial++;
                    LastSuccessfulDestinationTime = context.Time;
                    return;
                }
                blockedUntil[CellKey(frontier, grid.Width)] =
                    context.Time + unreachableRetrySeconds;
            }
            hasDestination = false;
            nextFrontierSearchTime = context.Time + frontierRetrySeconds;
        }

        private static bool HasAnyFrontier(CommanderDecisionContext context)
        {
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return false;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                GridCell navigationCell = grid.GetCell(x, y);
                if (navigationCell != null && navigationCell.Walkable
                    && context.Knowledge.IsCellExplored(cell)
                    && CountUnknownNeighbors(context, cell, grid.Width, grid.Height) > 0)
                    return true;
            }
            return false;
        }

        private bool TryFindFrontier(CommanderDecisionContext context, UnitController unit,
            StrategicSafetyMap safety, out Vector2Int result)
        {
            using (FrontierSearchMarker.Auto())
            using (ScoutRiskMarker.Auto())
            {
                result = default;
                TeamVisionSystem vision = context.Knowledge.TeamVision;
                GridManager grid = vision != null ? vision.GridManager : null;
                if (grid == null) return false;
                bool found = false;
                int bestUnknown = -1;
                StrategicRisk bestRisk = StrategicRisk.ForbiddenForCivilian;
                int bestEdgeDistance = int.MaxValue;
                float bestDistance = float.PositiveInfinity;

                for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    GridCell navigationCell = grid.GetCell(x, y);
                    if (navigationCell == null || !navigationCell.Walkable
                        || !context.Knowledge.IsCellExplored(cell)
                        || IsBlocked(cell, grid.Width, context.Time)) continue;
                    int unknown = CountUnknownNeighbors(context, cell, grid.Width, grid.Height);
                    if (unknown <= 0) continue;
                    StrategicRisk risk = safety != null
                        ? safety.RiskAt(cell, StrategicPathKind.Scout)
                        : StrategicRisk.Safe;
                    if (risk >= StrategicRisk.ForbiddenForCivilian) continue;
                    int edgeDistance = Mathf.Min(Mathf.Min(x, grid.Width - 1 - x),
                        Mathf.Min(y, grid.Height - 1 - y));
                    float distance = ((Vector2)(navigationCell.WorldPosition
                        - unit.transform.position)).sqrMagnitude;
                    if (!found || risk < bestRisk
                        || risk == bestRisk && edgeDistance < bestEdgeDistance
                        || risk == bestRisk && edgeDistance == bestEdgeDistance
                            && unknown > bestUnknown
                        || risk == bestRisk && edgeDistance == bestEdgeDistance
                            && unknown == bestUnknown
                            && distance < bestDistance - 0.0001f
                        || risk == bestRisk && edgeDistance == bestEdgeDistance
                            && unknown == bestUnknown
                            && Mathf.Abs(distance - bestDistance) <= 0.0001f
                            && (y < result.y || (y == result.y && x < result.x)))
                    {
                        found = true;
                        bestRisk = risk;
                        bestEdgeDistance = edgeDistance;
                        bestUnknown = unknown;
                        bestDistance = distance;
                        result = cell;
                    }
                }
                return found;
            }
        }

        private bool ShouldRetreat(UnitController unit, UnitMovement movement,
            StrategicSafetyMap safety)
        {
            StrategicRisk current = safety.RiskAt(unit.transform.position,
                StrategicPathKind.Scout);
            if (current >= StrategicRisk.Dangerous)
            {
                dangerReason = "visible or remembered strategic danger";
                return true;
            }
            if (hasDestination && safety.RiskAt(destinationCell, StrategicPathKind.Scout)
                >= StrategicRisk.ForbiddenForCivilian)
            {
                dangerReason = "destination became forbidden";
                return true;
            }
            if (movement.IsMoving && !safety.IsPathSafe(movement.CurrentPath,
                StrategicPathKind.Scout, movement.CurrentPathWaypointIndex))
            {
                dangerReason = "route became unsafe";
                return true;
            }
            return false;
        }

        private void BeginOrContinueRetreat(CommanderDecisionContext context,
            UnitMovement movement, StrategicSafetyMap safety)
        {
            using var marker = ScoutRetreatMarker.Auto();
            if (safetyState == ScoutSafetyState.Retreating && movement.IsMoving
                && safety.IsPathSafe(movement.CurrentPath, StrategicPathKind.Scout,
                    movement.CurrentPathWaypointIndex)) return;
            movement.Stop();
            hasDestination = false;
            safetyState = ScoutSafetyState.Retreating;
            hasRetreatDestination = false;
            if (!safety.TryFindRetreatCell(context.Knowledge, scout.transform.position,
                StrategicPathKind.Scout, out Vector2Int destination)) return;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return;
            if (context.Commands.TryMove(scout,
                grid.GridToWorld(destination.x, destination.y),
                safety.ScoutRetreatTraversal)
                != CommanderCommandResult.Accepted) return;
            retreatCell = destination;
            hasRetreatDestination = true;
        }

        private static int CountUnknownNeighbors(CommanderDecisionContext context,
            Vector2Int cell, int width, int height)
        {
            int count = 0;
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                if ((x == 0 && y == 0) || cell.x + x < 0 || cell.x + x >= width
                    || cell.y + y < 0 || cell.y + y >= height) continue;
                if (!context.Knowledge.IsCellExplored(
                    new Vector2Int(cell.x + x, cell.y + y))) count++;
            }
            return count;
        }

        private static bool IsFrontier(CommanderDecisionContext context, Vector2Int cell)
        {
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            return grid != null && context.Knowledge.IsCellExplored(cell)
                && CountUnknownNeighbors(context, cell, grid.Width, grid.Height) > 0;
        }

        private UnitController ChooseScout(FactionKnowledge knowledge, CommanderPawnRoles roles)
        {
            UnitController best = null;
            int bestPriority = int.MaxValue;
            int pawnCount = CountLivingPawns(knowledge);
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (!IsAvailableCandidate(unit)) continue;
                int priority = ScoutPriority(unit);
                if (priority == 3 && (pawnCount < minimumPawnsForWorkerScout
                    || roles == null
                    || roles.GetRole(unit) != CommanderPawnRole.Economy)) continue;
                if (priority == int.MaxValue) continue;
                if (best == null || priority < bestPriority
                    || (priority == bestPriority
                        && unit.GetEntityId().CompareTo(best.GetEntityId()) < 0))
                {
                    best = unit;
                    bestPriority = priority;
                }
            }
            return best;
        }

        private static bool IsAvailableCandidate(UnitController unit)
        {
            if (unit == null || !unit.IsAlive || !unit.IsWorldParticipant
                || unit.GetComponent<UnitMovement>() == null
                || unit.CurrentState != UnitState.Idle) return false;
            PawnGatherer gatherer = unit.GetComponent<PawnGatherer>();
            return gatherer == null || gatherer.State == WorkerState.Idle;
        }

        private static bool IsUsableScout(CommanderDecisionContext context,
            UnitController unit)
            => unit != null && unit.IsAlive && unit.IsWorldParticipant
                && unit.Faction == context.Faction
                && unit.GetComponent<HealthComponent>()?.CombatData?.UnitType != UnitType.Monk;

        private void ReleasePawnScout(CommanderPawnRoles roles)
        {
            if (IsPawn(scout)) roles?.Release(scout, CommanderPawnRole.Scout);
        }

        private static bool IsPawn(UnitController unit)
            => unit != null && unit.GetComponent<PawnGatherer>() != null;

        private static int CountLivingPawns(FactionKnowledge knowledge)
        {
            int count = 0;
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
                if (IsPawn(units[i])) count++;
            return count;
        }

        private static int ScoutPriority(UnitController unit)
        {
            UnitType type = unit != null
                ? unit.GetComponent<HealthComponent>()?.CombatData?.UnitType ?? UnitType.Monk
                : UnitType.Monk;
            return type switch
            {
                UnitType.Lancer => 0,
                UnitType.Warrior => 1,
                UnitType.Archer => 2,
                UnitType.Pawn => 3,
                _ => int.MaxValue
            };
        }

        private bool IsBlocked(Vector2Int cell, int width, float time)
            => blockedUntil.TryGetValue(CellKey(cell, width), out float until) && time < until;

        private static int CellKey(Vector2Int cell, int width) => cell.y * width + cell.x;
    }
}
