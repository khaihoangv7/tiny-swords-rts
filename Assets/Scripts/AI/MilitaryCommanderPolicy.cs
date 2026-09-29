using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    [Serializable]
    public sealed class MilitaryCommanderPolicy
    {
        private static readonly ProfilerMarker ThreatEvaluationMarker =
            new("TinyKingdom.AI.ThreatEvaluation");
        private static readonly ProfilerMarker ArmyRegroupMarker =
            new("TinyKingdom.AI.ArmyRegroup");

        [SerializeField, Min(1f)] private float baseDefenseRadius = 9f;
        [SerializeField, Min(1f)] private float defenseLeashRadius = 12f;
        [SerializeField, Min(0.5f)] private float stagingRegionRadius = 3f;
        [SerializeField, Min(0.1f)] private float destinationArrivalRadius = 0.45f;
        [SerializeField, Min(0.5f)] private float commandRetrySeconds = 1.5f;

        private readonly ArmyGroup army = new();
        private readonly Dictionary<UnitController, EntityId> attackIntents = new();
        private readonly Dictionary<UnitController, Vector3> moveIntents = new();
        private readonly Dictionary<UnitController, float> retryAfter = new();
        private readonly List<UnitController> staleActors = new();
        private bool hasCurrentThreat;
        private EntityId currentThreatId;
        private UnitType currentThreatType;
        private Vector3 currentThreatPosition;
        private bool hasBaseAnchor;
        private Vector3 baseAnchor;
        private bool hasStagingPosition;
        private Vector3 stagingPosition;
        private int assignmentMembershipVersion = -1;

        public ArmyGroup Army => army;
        public float BaseDefenseRadius => Mathf.Max(1f, baseDefenseRadius);
        public float DefenseLeashRadius => Mathf.Max(BaseDefenseRadius, defenseLeashRadius);
        public bool HasBaseAnchor => hasBaseAnchor;
        public Vector3 BaseAnchor => baseAnchor;
        public bool HasStagingPosition => hasStagingPosition;
        public Vector3 StagingPosition => stagingPosition;
        public bool HasCurrentThreat => hasCurrentThreat;
        public EntityId CurrentThreatId => currentThreatId;
        public UnitType CurrentThreatType => currentThreatType;
        public Vector3 CurrentThreatPosition => currentThreatPosition;
        public int VisibleThreatCount { get; private set; }

        public void Tick(CommanderDecisionContext context,
            ProductionCommanderPolicy production, UnitController activeScout)
        {
            army.Synchronize(context.Knowledge, activeScout);
            RemoveStaleIntents();
            RefreshAnchorAndStaging(context.Knowledge, production);
            VisibleThreatCount = 0;
            if (!hasBaseAnchor || !hasStagingPosition)
            {
                ClearThreat();
                army.SetState(ArmyGroupState.Forming);
                return;
            }

            bool retained = TryRetainCurrentThreat(context.Knowledge,
                out VisibleUnitObservation threat);
            if (retained)
                VisibleThreatCount = CountVisibleDefenseThreats(context.Knowledge);
            if (!retained) retained = TrySelectThreat(context.Knowledge, out threat);
            if (retained)
            {
                bool changed = !hasCurrentThreat
                    || currentThreatId.CompareTo(threat.RuntimeId) != 0;
                hasCurrentThreat = true;
                currentThreatId = threat.RuntimeId;
                currentThreatType = threat.UnitType;
                currentThreatPosition = threat.Position;
                if (changed)
                {
                    attackIntents.Clear();
                    moveIntents.Clear();
                    retryAfter.Clear();
                }
                army.SetState(ArmyGroupState.Defending);
                Defend(context, threat);
                return;
            }

            bool wasDefending = hasCurrentThreat || army.State == ArmyGroupState.Defending;
            ClearThreat();
            if (army.Members.Count == 0)
            {
                army.SetState(ArmyGroupState.Forming);
                return;
            }
            if (wasDefending)
            {
                army.SetState(ArmyGroupState.Regrouping);
                InvalidateMoveAssignments();
            }
            if (AllMembersInsideStaging())
            {
                army.SetState(ArmyGroupState.Ready);
                moveIntents.Clear();
                retryAfter.Clear();
                return;
            }
            if (army.State != ArmyGroupState.Regrouping)
                army.SetState(ArmyGroupState.Forming);
            Regroup(context, army.State == ArmyGroupState.Regrouping);
        }

        public void Reset()
        {
            army.Reset();
            attackIntents.Clear();
            moveIntents.Clear();
            retryAfter.Clear();
            staleActors.Clear();
            hasCurrentThreat = false;
            hasBaseAnchor = false;
            hasStagingPosition = false;
            VisibleThreatCount = 0;
            assignmentMembershipVersion = -1;
        }

        private bool TryRetainCurrentThreat(FactionKnowledge knowledge,
            out VisibleUnitObservation threat)
        {
            threat = default;
            return hasCurrentThreat
                && knowledge.TryGetVisibleEnemy(currentThreatId, out threat)
                && IsSupportedThreat(threat)
                && DistanceSquared(threat.Position, baseAnchor)
                    <= DefenseLeashRadius * DefenseLeashRadius;
        }

        private bool TrySelectThreat(FactionKnowledge knowledge,
            out VisibleUnitObservation best)
        {
            using (ThreatEvaluationMarker.Auto())
            {
                best = default;
                bool found = false;
                float radiusSquared = BaseDefenseRadius * BaseDefenseRadius;
                float bestDistance = float.PositiveInfinity;
                IReadOnlyList<VisibleUnitObservation> visible = knowledge.VisibleEnemyUnits;
                for (int i = 0; i < visible.Count; i++)
                {
                    VisibleUnitObservation candidate = visible[i];
                    if (!IsSupportedThreat(candidate)) continue;
                    float distance = DistanceSquared(candidate.Position, baseAnchor);
                    if (distance > radiusSquared) continue;
                    VisibleThreatCount++;
                    if (!found || IsHigherPriority(candidate, distance, best, bestDistance))
                    {
                        found = true;
                        best = candidate;
                        bestDistance = distance;
                    }
                }
                return found;
            }
        }

        private void Defend(CommanderDecisionContext context,
            VisibleUnitObservation threat)
        {
            IReadOnlyList<UnitController> members = army.Members;
            for (int i = 0; i < members.Count; i++)
            {
                UnitController member = members[i];
                UnitType? type = member.GetComponent<HealthComponent>()?.CombatData?.UnitType;
                if (type == UnitType.Monk)
                {
                    SupportArmy(context, member, threat.Position);
                    continue;
                }
                CombatController combat = member.GetComponent<CombatController>();
                if (combat?.Target != null
                    && combat.Target.GetEntityId().CompareTo(threat.RuntimeId) == 0)
                {
                    attackIntents[member] = threat.RuntimeId;
                    continue;
                }
                if (context.Time < RetryTime(member)) continue;
                CommanderCommandResult result = context.Commands.TryAttack(member,
                    threat.RuntimeId);
                if (result == CommanderCommandResult.Accepted)
                {
                    attackIntents[member] = threat.RuntimeId;
                    moveIntents.Remove(member);
                }
                else retryAfter[member] = context.Time + commandRetrySeconds;
            }
        }

        private int CountVisibleDefenseThreats(FactionKnowledge knowledge)
        {
            int count = 0;
            float radiusSquared = BaseDefenseRadius * BaseDefenseRadius;
            IReadOnlyList<VisibleUnitObservation> visible = knowledge.VisibleEnemyUnits;
            for (int i = 0; i < visible.Count; i++)
                if (IsSupportedThreat(visible[i])
                    && DistanceSquared(visible[i].Position, baseAnchor) <= radiusSquared) count++;
            return count;
        }

        private void SupportArmy(CommanderDecisionContext context, UnitController monk,
            Vector3 threatPosition)
        {
            UnitMovement movement = monk.GetComponent<UnitMovement>();
            if (movement == null || movement.IsMoving || context.Time < RetryTime(monk)) return;
            Vector2 retreatDirection = (Vector2)(baseAnchor - threatPosition);
            if (retreatDirection.sqrMagnitude < 0.001f) retreatDirection = Vector2.left;
            Vector3 desired = threatPosition + (Vector3)(retreatDirection.normalized * 2f);
            GridManager grid = context.Knowledge.TeamVision?.GridManager;
            if (grid == null) return;
            Vector2Int cell = grid.WorldToGrid(desired);
            GridCell destination = grid.GetCell(cell.x, cell.y);
            if (destination == null || !destination.Walkable || destination.Occupied) return;
            desired = destination.WorldPosition;
            if (Vector2.Distance(monk.transform.position, desired) <= destinationArrivalRadius)
                return;
            if (moveIntents.TryGetValue(monk, out Vector3 existing)
                && Vector2.Distance(existing, desired) <= 0.5f) return;
            CommanderCommandResult result = context.Commands.TryMove(monk, desired);
            if (result == CommanderCommandResult.Accepted) moveIntents[monk] = desired;
            else retryAfter[monk] = context.Time + commandRetrySeconds;
        }

        private void Regroup(CommanderDecisionContext context, bool interruptCombat)
        {
            using (ArmyRegroupMarker.Auto())
            {
                EnsureMoveAssignments(context.Knowledge);
                IReadOnlyList<UnitController> members = army.Members;
                for (int i = 0; i < members.Count; i++)
                {
                    UnitController member = members[i];
                    if (!moveIntents.TryGetValue(member, out Vector3 destination)) continue;
                    if (Vector2.Distance(member.transform.position, destination)
                        <= destinationArrivalRadius) continue;
                    UnitMovement movement = member.GetComponent<UnitMovement>();
                    if (!interruptCombat && movement != null && movement.IsMoving) continue;
                    if (interruptCombat && movement != null && movement.IsMoving
                        && member.CurrentState == UnitState.Moving) continue;
                    if (context.Time < RetryTime(member)) continue;
                    CommanderCommandResult result = context.Commands.TryMove(member, destination);
                    if (result != CommanderCommandResult.Accepted)
                        retryAfter[member] = context.Time + commandRetrySeconds;
                }
            }
        }

        private void EnsureMoveAssignments(FactionKnowledge knowledge)
        {
            if (assignmentMembershipVersion == army.MembershipVersion
                && moveIntents.Count == army.Members.Count) return;
            moveIntents.Clear();
            GridManager grid = knowledge.TeamVision?.GridManager;
            if (grid == null) return;
            Vector2Int center = grid.WorldToGrid(stagingPosition);
            List<GridCell> destinations = DestinationDistributor.FindNearbyDestinations(
                grid, center, army.Members.Count, 4, true);
            int count = Mathf.Min(destinations.Count, army.Members.Count);
            for (int i = 0; i < count; i++)
                moveIntents[army.Members[i]] = destinations[i].WorldPosition;
            assignmentMembershipVersion = army.MembershipVersion;
        }

        private void RefreshAnchorAndStaging(FactionKnowledge knowledge,
            ProductionCommanderPolicy production)
        {
            hasBaseAnchor = TryGetCastleCenter(knowledge, out baseAnchor);
            GridManager grid = knowledge.TeamVision?.GridManager;
            hasStagingPosition = grid != null && production != null
                && production.HasRallyStagingCell;
            if (hasStagingPosition)
                stagingPosition = grid.GridToWorld(production.RallyStagingCell.x,
                    production.RallyStagingCell.y);
        }

        private static bool TryGetCastleCenter(FactionKnowledge knowledge, out Vector3 center)
        {
            GridManager grid = knowledge.TeamVision?.GridManager;
            IReadOnlyList<IDefensiveBuildingHost> buildings = knowledge.OwnBuildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                IDefensiveBuildingHost host = buildings[i];
                if (host == null || !host.IsAlive || host.Type != BuildingType.Castle) continue;
                if (grid == null)
                {
                    center = host.HostTransform.position;
                    return true;
                }
                Vector2Int first = host.FootprintOrigin;
                Vector2Int last = first + host.Footprint - Vector2Int.one;
                center = (grid.GridToWorld(first.x, first.y)
                    + grid.GridToWorld(last.x, last.y)) * 0.5f;
                return true;
            }
            center = default;
            return false;
        }

        private bool AllMembersInsideStaging()
        {
            float radiusSquared = stagingRegionRadius * stagingRegionRadius;
            IReadOnlyList<UnitController> members = army.Members;
            for (int i = 0; i < members.Count; i++)
                if (DistanceSquared(members[i].transform.position, stagingPosition)
                    > radiusSquared) return false;
            return members.Count > 0;
        }

        private void RemoveStaleIntents()
        {
            staleActors.Clear();
            foreach (UnitController actor in attackIntents.Keys)
                if (!ContainsMember(actor)) staleActors.Add(actor);
            for (int i = 0; i < staleActors.Count; i++) attackIntents.Remove(staleActors[i]);
            staleActors.Clear();
            foreach (UnitController actor in moveIntents.Keys)
                if (!ContainsMember(actor)) staleActors.Add(actor);
            for (int i = 0; i < staleActors.Count; i++) moveIntents.Remove(staleActors[i]);
            staleActors.Clear();
            foreach (UnitController actor in retryAfter.Keys)
                if (!ContainsMember(actor)) staleActors.Add(actor);
            for (int i = 0; i < staleActors.Count; i++) retryAfter.Remove(staleActors[i]);
        }

        private bool ContainsMember(UnitController actor)
        {
            IReadOnlyList<UnitController> members = army.Members;
            for (int i = 0; i < members.Count; i++)
                if (members[i] == actor) return true;
            return false;
        }

        private void ClearThreat()
        {
            hasCurrentThreat = false;
            currentThreatId = default;
            currentThreatType = default;
            currentThreatPosition = default;
            attackIntents.Clear();
        }

        private void InvalidateMoveAssignments()
        {
            moveIntents.Clear();
            retryAfter.Clear();
            assignmentMembershipVersion = -1;
        }

        private float RetryTime(UnitController actor)
            => retryAfter.TryGetValue(actor, out float time) ? time : 0f;

        private static bool IsSupportedThreat(VisibleUnitObservation threat)
            => FactionRelations.IsPlayable(threat.Faction)
                && threat.UnitType is UnitType.Pawn or UnitType.Warrior or UnitType.Archer
                    or UnitType.Lancer or UnitType.Monk;

        private static bool IsHigherPriority(VisibleUnitObservation candidate,
            float candidateDistance, VisibleUnitObservation current, float currentDistance)
        {
            int priority = ThreatPriority(candidate);
            int currentPriority = ThreatPriority(current);
            if (priority != currentPriority) return priority < currentPriority;
            if (Mathf.Abs(candidateDistance - currentDistance) > 0.0001f)
                return candidateDistance < currentDistance;
            return candidate.RuntimeId.CompareTo(current.RuntimeId) < 0;
        }

        private static int ThreatPriority(VisibleUnitObservation threat)
            => threat.ThreatActivity switch
            {
                ObservedThreatActivity.AttackingCastle => 0,
                ObservedThreatActivity.AttackingBuilding => 1,
                ObservedThreatActivity.AttackingUnit => 2,
                _ => threat.UnitType == UnitType.Pawn ? 4 : 3
            };

        private static float DistanceSquared(Vector3 first, Vector3 second)
            => ((Vector2)(first - second)).sqrMagnitude;
    }
}
