using System;
using System.Collections.Generic;
using TinyKingdom.Economy;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    [Serializable]
    public sealed class EconomyCommanderPolicy
    {
        private static readonly ProfilerMarker ResourceTargetSearchMarker =
            new("TinyKingdom.AI.ResourceTargetSearch");
        private static readonly Vector2Int[] RememberedApproachOffsets =
        {
            new(0, -1), new(-1, 0), new(1, 0), new(0, 1),
            new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)
        };

        private enum AssignmentMode { Gathering, SeekingMemory }

        private sealed class WorkerAssignment
        {
            public ResourceType Type;
            public EntityId TargetId;
            public Vector3 LastKnownPosition;
            public AssignmentMode Mode;
        }

        [SerializeField, Range(0f, 1f)] private float foodRatio = 0.40f;
        [SerializeField, Range(0f, 1f)] private float woodRatio = 0.35f;
        [SerializeField, Range(0f, 1f)] private float goldRatio = 0.25f;
        [SerializeField, Min(0.5f)] private float unreachableRetrySeconds = 4f;

        private readonly Dictionary<UnitController, WorkerAssignment> assignments = new();
        private readonly Dictionary<EntityId, float> blockedUntil = new();
        private readonly List<UnitController> workers = new();
        private readonly List<UnitController> removals = new();

        public float FoodRatio => foodRatio;
        public float WoodRatio => woodRatio;
        public float GoldRatio => goldRatio;
        public int AssignmentCount => assignments.Count;
        public int FoodAssignments { get; private set; }
        public int WoodAssignments { get; private set; }
        public int GoldAssignments { get; private set; }

        public void Reset()
        {
            assignments.Clear();
            blockedUntil.Clear();
            workers.Clear();
            removals.Clear();
            FoodAssignments = WoodAssignments = GoldAssignments = 0;
        }

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles)
        {
            CollectWorkers(context.Knowledge, roles);
            PruneAssignments(context, roles);
            CountAssignments();
            DesiredCounts(workers.Count, out int food, out int wood, out int gold);

            for (int i = 0; i < workers.Count; i++)
            {
                UnitController worker = workers[i];
                if (assignments.ContainsKey(worker)) continue;
                ResourceType type = NextDeficit(food, wood, gold);
                if (!TryAssign(context, worker, type)) continue;
                Increment(type);
            }
        }

        private void CollectWorkers(FactionKnowledge knowledge, CommanderPawnRoles roles)
        {
            workers.Clear();
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit == null || !unit.IsAlive || unit.GetComponent<PawnGatherer>() == null
                    || roles == null
                    || roles.GetRole(unit) != CommanderPawnRole.Economy) continue;
                workers.Add(unit);
            }
            workers.Sort(CompareUnits);
        }

        private void PruneAssignments(CommanderDecisionContext context,
            CommanderPawnRoles roles)
        {
            removals.Clear();
            foreach (KeyValuePair<UnitController, WorkerAssignment> item in assignments)
            {
                UnitController worker = item.Key;
                if (worker == null || !worker.IsAlive || worker.Faction != context.Faction
                    || roles == null
                    || roles.GetRole(worker) != CommanderPawnRole.Economy)
                {
                    removals.Add(worker);
                    continue;
                }

                PawnGatherer gatherer = worker.GetComponent<PawnGatherer>();
                UnitMovement movement = worker.GetComponent<UnitMovement>();
                WorkerAssignment assignment = item.Value;
                if (assignment.Mode == AssignmentMode.Gathering
                    && gatherer != null && gatherer.State != WorkerState.Idle
                    && gatherer.Target != null
                    && gatherer.Target.ResourceType == assignment.Type)
                {
                    assignment.TargetId = gatherer.Target.GetEntityId();
                    assignment.LastKnownPosition = gatherer.Target.transform.position;
                    continue;
                }

                if (assignment.Mode == AssignmentMode.SeekingMemory)
                {
                    if (context.Knowledge.TryGetVisibleResource(assignment.TargetId,
                        out ResourceNode visible))
                    {
                        CommanderCommandResult result = context.Commands.TryGather(worker, visible);
                        if (result == CommanderCommandResult.Accepted)
                        {
                            assignment.Mode = AssignmentMode.Gathering;
                            continue;
                        }
                        Block(assignment.TargetId, context.Time);
                    }
                    else if (movement != null && movement.IsMoving) continue;
                }
                removals.Add(worker);
            }
            for (int i = 0; i < removals.Count; i++) assignments.Remove(removals[i]);
        }

        private bool TryAssign(CommanderDecisionContext context, UnitController worker,
            ResourceType type)
        {
            using (ResourceTargetSearchMarker.Auto())
            {
                ResourceNode best = FindBestVisible(context, worker, type);
                while (best != null)
                {
                    EntityId id = best.GetEntityId();
                    CommanderCommandResult result = context.Commands.TryGather(worker, best);
                    if (result == CommanderCommandResult.Accepted)
                    {
                        assignments[worker] = new WorkerAssignment
                        {
                            Type = type,
                            TargetId = id,
                            LastKnownPosition = best.transform.position,
                            Mode = AssignmentMode.Gathering
                        };
                        return true;
                    }
                    Block(id, context.Time);
                    best = FindBestVisible(context, worker, type);
                }

                if (!TryFindRemembered(context, worker, type,
                    out ResourceKnowledgeSnapshot remembered)) return false;
                if (!TryMoveNearRemembered(context, worker, remembered))
                {
                    Block(remembered.RuntimeId, context.Time);
                    return false;
                }
                assignments[worker] = new WorkerAssignment
                {
                    Type = type,
                    TargetId = remembered.RuntimeId,
                    LastKnownPosition = remembered.LastSeenPosition,
                    Mode = AssignmentMode.SeekingMemory
                };
                return true;
            }
        }

        private static bool TryMoveNearRemembered(CommanderDecisionContext context,
            UnitController worker, ResourceKnowledgeSnapshot remembered)
        {
            int triedMask = 0;
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return false;
            for (int attempt = 0; attempt < RememberedApproachOffsets.Length; attempt++)
            {
                int best = -1;
                float bestDistance = float.PositiveInfinity;
                for (int i = 0; i < RememberedApproachOffsets.Length; i++)
                {
                    if ((triedMask & (1 << i)) != 0) continue;
                    Vector2Int cell = remembered.LogicalCell + RememberedApproachOffsets[i];
                    GridCell candidate = grid.GetCell(cell.x, cell.y);
                    if (candidate == null || !candidate.Walkable
                        || !context.Knowledge.IsCellExplored(cell)) continue;
                    float distance = ((Vector2)(candidate.WorldPosition
                        - worker.transform.position)).sqrMagnitude;
                    if (best < 0 || distance < bestDistance - 0.0001f)
                    {
                        best = i;
                        bestDistance = distance;
                    }
                }
                if (best < 0) return false;
                triedMask |= 1 << best;
                Vector2Int destination = remembered.LogicalCell
                    + RememberedApproachOffsets[best];
                if (context.Commands.TryMove(worker,
                    grid.GridToWorld(destination.x, destination.y))
                    == CommanderCommandResult.Accepted) return true;
            }
            return false;
        }

        private ResourceNode FindBestVisible(CommanderDecisionContext context,
            UnitController worker, ResourceType type)
        {
            ResourceNode best = null;
            float bestDistance = float.PositiveInfinity;
            EntityId bestId = default;
            IReadOnlyList<ResourceNode> resources = context.Knowledge.VisibleResources;
            for (int i = 0; i < resources.Count; i++)
            {
                ResourceNode node = resources[i];
                if (node == null || !node.IsAvailable || node.ResourceType != type) continue;
                EntityId id = node.GetEntityId();
                if (IsBlocked(id, context.Time)) continue;
                float distance = ((Vector2)(node.transform.position
                    - worker.transform.position)).sqrMagnitude;
                if (best == null || distance < bestDistance - 0.0001f
                    || (Mathf.Abs(distance - bestDistance) <= 0.0001f
                        && id.CompareTo(bestId) < 0))
                {
                    best = node;
                    bestDistance = distance;
                    bestId = id;
                }
            }
            return best;
        }

        private bool TryFindRemembered(CommanderDecisionContext context,
            UnitController worker, ResourceType type,
            out ResourceKnowledgeSnapshot result)
        {
            result = default;
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            IReadOnlyList<ResourceKnowledgeSnapshot> resources = context.Knowledge.KnownResources;
            for (int i = 0; i < resources.Count; i++)
            {
                ResourceKnowledgeSnapshot item = resources[i];
                if (item.ResourceType != type || item.LastSeenRemainingAmount <= 0
                    || IsBlocked(item.RuntimeId, context.Time)) continue;
                if (context.Knowledge.TryGetVisibleResource(item.RuntimeId, out _)) continue;
                float distance = ((Vector2)(item.LastSeenPosition
                    - worker.transform.position)).sqrMagnitude;
                if (!found || distance < bestDistance - 0.0001f
                    || (Mathf.Abs(distance - bestDistance) <= 0.0001f
                        && item.RuntimeId.CompareTo(result.RuntimeId) < 0))
                {
                    found = true;
                    bestDistance = distance;
                    result = item;
                }
            }
            return found;
        }

        private void CountAssignments()
        {
            FoodAssignments = WoodAssignments = GoldAssignments = 0;
            foreach (WorkerAssignment item in assignments.Values) Increment(item.Type);
        }

        private void Increment(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Food: FoodAssignments++; break;
                case ResourceType.Wood: WoodAssignments++; break;
                case ResourceType.Gold: GoldAssignments++; break;
            }
        }

        private ResourceType NextDeficit(int food, int wood, int gold)
        {
            int foodDeficit = food - FoodAssignments;
            int woodDeficit = wood - WoodAssignments;
            int goldDeficit = gold - GoldAssignments;
            if (foodDeficit >= woodDeficit && foodDeficit >= goldDeficit)
                return ResourceType.Food;
            return woodDeficit >= goldDeficit ? ResourceType.Wood : ResourceType.Gold;
        }

        private void DesiredCounts(int count, out int food, out int wood, out int gold)
        {
            if (count <= 0) { food = wood = gold = 0; return; }
            if (count == 1) { food = 1; wood = gold = 0; return; }
            if (count == 2) { food = wood = 1; gold = 0; return; }
            if (count == 3) { food = wood = gold = 1; return; }
            if (count == 4) { food = 2; wood = gold = 1; return; }

            float total = Mathf.Max(0.001f, foodRatio + woodRatio + goldRatio);
            float foodExact = count * foodRatio / total;
            float woodExact = count * woodRatio / total;
            float goldExact = count * goldRatio / total;
            food = Mathf.FloorToInt(foodExact);
            wood = Mathf.FloorToInt(woodExact);
            gold = Mathf.FloorToInt(goldExact);
            int remaining = count - food - wood - gold;
            while (remaining-- > 0)
            {
                float foodFraction = foodExact - food;
                float woodFraction = woodExact - wood;
                float goldFraction = goldExact - gold;
                if (foodFraction >= woodFraction && foodFraction >= goldFraction) food++;
                else if (woodFraction >= goldFraction) wood++;
                else gold++;
            }
        }

        private void Block(EntityId id, float time)
            => blockedUntil[id] = time + unreachableRetrySeconds;

        private bool IsBlocked(EntityId id, float time)
            => blockedUntil.TryGetValue(id, out float until) && time < until;

        private static int CompareUnits(UnitController first, UnitController second)
            => first.GetEntityId().CompareTo(second.GetEntityId());
    }
}
