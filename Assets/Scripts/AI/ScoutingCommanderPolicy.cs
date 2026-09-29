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
    [Serializable]
    public sealed class ScoutingCommanderPolicy
    {
        private static readonly ProfilerMarker FrontierSearchMarker =
            new("TinyKingdom.AI.FrontierSearch");
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.35f;
        [SerializeField, Min(0.5f)] private float unreachableRetrySeconds = 5f;
        [SerializeField, Min(1)] private int minimumPawnsForWorkerScout = 4;

        private readonly Dictionary<int, float> blockedUntil = new();
        private UnitController scout;
        private Vector2Int destinationCell;
        private bool hasDestination;

        public UnitController Scout => scout;
        public bool HasDestination => hasDestination;
        public Vector2Int DestinationCell => destinationCell;

        public int MinimumPawnsForWorkerScout => minimumPawnsForWorkerScout;

        public void Reset()
        {
            scout = null;
            hasDestination = false;
            blockedUntil.Clear();
        }

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles)
        {
            UnitController preferred = ChooseScout(context.Knowledge, roles);
            if (!IsUsableScout(context, scout)
                || (preferred != null && ScoutPriority(preferred) < ScoutPriority(scout)))
            {
                ReleasePawnScout(roles);
                scout = preferred;
                if (IsPawn(scout) && (roles == null
                    || !roles.TryAssign(scout, CommanderPawnRole.Scout))) scout = null;
                hasDestination = false;
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

            if (hasDestination)
            {
                Vector3 destination = grid.GridToWorld(destinationCell.x, destinationCell.y);
                bool reached = Vector2.Distance(scout.transform.position, destination)
                    <= arrivalDistance;
                if (movement.IsMoving && !reached && IsFrontier(context, destinationCell)) return;
                hasDestination = false;
            }

            const int maxAttemptsPerThink = 4;
            for (int attempt = 0; attempt < maxAttemptsPerThink; attempt++)
            {
                if (!TryFindFrontier(context, scout, out Vector2Int frontier))
                {
                    // Exploration is complete or every remaining frontier is on
                    // cooldown. A complete map releases the role; cooldown-only
                    // cases are reconsidered without command spam on a later tick.
                    if (!HasAnyFrontier(context))
                    {
                        ReleasePawnScout(roles);
                        scout = null;
                    }
                    hasDestination = false;
                    return;
                }
                CommanderCommandResult result = context.Commands.TryMove(scout,
                    grid.GridToWorld(frontier.x, frontier.y));
                if (result == CommanderCommandResult.Accepted)
                {
                    destinationCell = frontier;
                    hasDestination = true;
                    return;
                }
                blockedUntil[CellKey(frontier, grid.Width)] =
                    context.Time + unreachableRetrySeconds;
            }
            hasDestination = false;
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
            out Vector2Int result)
        {
            using (FrontierSearchMarker.Auto())
            {
                result = default;
                TeamVisionSystem vision = context.Knowledge.TeamVision;
                GridManager grid = vision != null ? vision.GridManager : null;
                if (grid == null) return false;
                bool found = false;
                int bestUnknown = -1;
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
                    float distance = ((Vector2)(navigationCell.WorldPosition
                        - unit.transform.position)).sqrMagnitude;
                    if (!found || unknown > bestUnknown
                        || (unknown == bestUnknown && distance < bestDistance - 0.0001f)
                        || (unknown == bestUnknown
                            && Mathf.Abs(distance - bestDistance) <= 0.0001f
                            && (y < result.y || (y == result.y && x < result.x))))
                    {
                        found = true;
                        bestUnknown = unknown;
                        bestDistance = distance;
                        result = cell;
                    }
                }
                return found;
            }
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
