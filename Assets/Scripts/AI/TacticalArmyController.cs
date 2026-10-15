using System;
using System.Collections.Generic;
using System.Text;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    public enum TacticalArmyState
    {
        Assembling,
        Advancing,
        Engaging,
        LocalRegroup,
        ExistingStrategicRetreat
    }

    public enum TacticalArmyRole
    {
        Frontline,
        Backline,
        Support,
        Excluded
    }

    public readonly struct ArmyTacticalSnapshot
    {
        public ArmyTacticalSnapshot(int warriors, int lancers, int archers, int monks)
        {
            WarriorCount = warriors;
            LancerCount = lancers;
            ArcherCount = archers;
            MonkCount = monks;
        }

        public int WarriorCount { get; }
        public int LancerCount { get; }
        public int ArcherCount { get; }
        public int MonkCount { get; }
        public int FrontlineCount => WarriorCount + LancerCount;
        public int BacklineCount => ArcherCount;
        public int SupportCount => MonkCount;
        public int TotalCount => FrontlineCount + BacklineCount + SupportCount;
    }

    [Serializable]
    public sealed class TacticalArmyController
    {
        private readonly struct TargetAssignment
        {
            public TargetAssignment(EntityId targetId, float score, float holdUntil)
            {
                TargetId = targetId;
                Score = score;
                HoldUntil = holdUntil;
            }

            public EntityId TargetId { get; }
            public float Score { get; }
            public float HoldUntil { get; }
        }

        private static readonly ProfilerMarker EvaluationMarker =
            new("TinyKingdom.AI.TacticalArmy.Evaluate");
        private static readonly ProfilerMarker FormationMarker =
            new("TinyKingdom.AI.FormationDistribution");
        private static readonly ProfilerMarker TargetingMarker =
            new("TinyKingdom.AI.TargetAssignment");
        private static readonly ProfilerMarker CohesionMarker =
            new("TinyKingdom.AI.CohesionEvaluation");
        private static readonly ProfilerMarker LocalRegroupMarker =
            new("TinyKingdom.AI.LocalRegroupEvaluation");

        [Header("Phase 7E.4 tactical cadence and cohesion")]
        [SerializeField, Min(0.1f)] private float evaluationInterval = 0.50f;
        [SerializeField, Min(1f)] private float armyCohesionRadius = 6f;
        [SerializeField, Min(1f)] private float stragglerRadius = 8f;
        [SerializeField, Min(1f)] private float localEngagementRadius = 9f;
        [SerializeField, Min(0f)] private float engagementExitDelay = 1f;
        [Header("Formation")]
        [SerializeField, Min(RoleFormationPlanner.MinimumSpacing)]
        private float formationSpacing = RoleFormationPlanner.DefaultSpacing;
        [SerializeField, Min(1)] private int formationCandidateMultiplier = 4;
        [SerializeField, Min(1)] private int formationSearchRadius = 8;
        [SerializeField, Min(0.1f)] private float formationDestinationThreshold = 0.75f;
        [Header("Target scoring")]
        [SerializeField, Min(0f)] private float baseTargetScore = 100f;
        [SerializeField, Min(0f)] private float distancePenalty = 7f;
        [SerializeField, Min(0f)] private float matchupWeight = 30f;
        [SerializeField, Min(0f)] private float lowHealthWeight = 20f;
        [SerializeField, Min(0f)] private float threatWeight = 14f;
        [SerializeField, Min(0f)] private float monkPriority = 18f;
        [SerializeField, Min(0f)] private float archerPriority = 8f;
        [SerializeField, Min(0f)] private float targetStickiness = 14f;
        [SerializeField, Min(0f)] private float retargetThreshold = 8f;
        [SerializeField, Min(0f)] private float minimumTargetHoldSeconds = 1.5f;
        [SerializeField, Min(0f)] private float meleeSaturationPenalty = 18f;
        [SerializeField, Min(0f)] private float rangedSaturationPenalty = 8f;
        [SerializeField, Range(4, 48)] private int maxLocalTargetCandidates = 24;
        [Header("Backline survival")]
        [SerializeField, Min(0.5f)] private float archerDangerRadius = 2f;
        [SerializeField, Min(0.5f)] private float monkDangerRadius = 2.5f;
        [SerializeField, Min(0.5f)] private float backlineRepositionDistance = 2f;
        [SerializeField, Min(0.1f)] private float archerRepositionCooldown = 2.5f;
        [SerializeField, Min(0.1f)] private float monkRepositionCooldown = 2f;
        [SerializeField, Min(0.1f)] private float commandRetrySeconds = 0.75f;

        private readonly List<UnitSelection> formationUnits = new();
        private readonly List<FormationAssignment> formationAssignments = new();
        private readonly Dictionary<UnitController, Vector3> formationDestinations = new();
        private readonly List<VisibleUnitObservation> localEnemies = new();
        private readonly Dictionary<EntityId, int> targetSaturation = new();
        private readonly Dictionary<UnitController, TargetAssignment> targetAssignments = new();
        private readonly Dictionary<UnitController, float> retryAfter = new();
        private readonly Dictionary<UnitController, float> repositionAfter = new();
        private readonly Dictionary<UnitController, Vector3> repositionDestinations = new();
        private readonly List<UnitController> staleActors = new();
        private readonly List<float> anchorX = new();
        private readonly List<float> anchorY = new();

        private float nextEvaluationTime;
        private float lastThreatSeenTime = float.NegativeInfinity;
        private int formationMembershipSignature;
        private bool hasFormationPlan;
        private Vector3 formationAnchor;

        public float EvaluationInterval => Mathf.Max(0.1f, evaluationInterval);
        public float ArmyCohesionRadius => Mathf.Max(1f, armyCohesionRadius);
        public float StragglerRadius => Mathf.Max(ArmyCohesionRadius, stragglerRadius);
        public float LocalEngagementRadius => Mathf.Max(1f, localEngagementRadius);
        public float EngagementExitDelay => Mathf.Max(0f, engagementExitDelay);
        public float TargetStickiness => Mathf.Max(0f, targetStickiness);
        public float RetargetThreshold => Mathf.Max(0f, retargetThreshold);
        public float MinimumTargetHoldSeconds => Mathf.Max(0f, minimumTargetHoldSeconds);
        public float MeleeSaturationPenalty => Mathf.Max(0f, meleeSaturationPenalty);
        public float RangedSaturationPenalty => Mathf.Max(0f, rangedSaturationPenalty);
        public int MaxLocalTargetCandidates => Mathf.Clamp(maxLocalTargetCandidates, 4, 48);
        public FormationType AdvanceFormation => FormationType.Compact;
        public ArmyTacticalSnapshot Snapshot { get; private set; }
        public TacticalArmyState State { get; private set; } = TacticalArmyState.Assembling;
        public Vector3 ArmyAnchor { get; private set; }
        public int StragglerCount { get; private set; }
        public int VisibleEnemyCount { get; private set; }
        public int AttackCommandsIssued { get; private set; }
        public int RetargetCount { get; private set; }
        public int FormationUpdateCount { get; private set; }
        public int RepositionCommandCount { get; private set; }
        public float LastAcceptedCommandTime { get; private set; } = -1f;
        public float LastRejectedCommandTime { get; private set; } = -1f;
        public int LocalCandidateCount => localEnemies.Count;
        public int TargetAssignmentCount => targetAssignments.Count;
        public float LastEvaluationTime { get; private set; } = -1f;
        public IReadOnlyDictionary<UnitController, Vector3> FormationDestinations
            => formationDestinations;

        public string UnitDebugSummary(IReadOnlyList<UnitController> members, float now)
        {
            var summary = new StringBuilder(256);
            for (int i = 0; i < members.Count; i++)
            {
                UnitController member = members[i];
                if (member == null) continue;
                UnitType? type = TypeOf(member);
                summary.Append(member.GetEntityId().ToString()).Append(" | role=")
                    .Append(type.HasValue ? RoleFor(type.Value) : TacticalArmyRole.Excluded)
                    .Append(" | target=");
                if (targetAssignments.TryGetValue(member, out TargetAssignment target))
                    summary.Append(target.TargetId.ToString()).Append(" score=")
                        .Append(target.Score.ToString("0.0")).Append(" hold=")
                        .Append(Mathf.Max(0f, target.HoldUntil - now).ToString("0.0"));
                else summary.Append("None");
                summary.Append(" | slot=");
                if (formationDestinations.TryGetValue(member, out Vector3 slot))
                    summary.Append(slot);
                else summary.Append("None");
                summary.AppendLine();
            }
            return summary.ToString();
        }

        public string TargetSaturationSummary()
        {
            var summary = new StringBuilder(128);
            foreach (KeyValuePair<EntityId, int> item in targetSaturation)
                summary.Append(item.Key.ToString()).Append(':')
                    .Append(item.Value).Append(' ');
            return summary.Length > 0 ? summary.ToString() : "None";
        }

        public static bool IsTacticalMilitaryType(UnitType type)
            => type is UnitType.Warrior or UnitType.Lancer
                or UnitType.Archer or UnitType.Monk;

        public static TacticalArmyRole RoleFor(UnitType type)
            => type switch
            {
                UnitType.Warrior or UnitType.Lancer => TacticalArmyRole.Frontline,
                UnitType.Archer => TacticalArmyRole.Backline,
                UnitType.Monk => TacticalArmyRole.Support,
                _ => TacticalArmyRole.Excluded
            };

        public void Refresh(IReadOnlyList<UnitController> members,
            FactionKnowledge knowledge, ArmyGroupState strategicState, float now)
        {
            using var marker = EvaluationMarker.Auto();
            LastEvaluationTime = now;
            RemoveStaleActors(members);
            int warriors = 0;
            int lancers = 0;
            int archers = 0;
            int monks = 0;
            for (int i = 0; i < members.Count; i++)
            {
                UnitType? type = TypeOf(members[i]);
                switch (type)
                {
                    case UnitType.Warrior: warriors++; break;
                    case UnitType.Lancer: lancers++; break;
                    case UnitType.Archer: archers++; break;
                    case UnitType.Monk: monks++; break;
                }
            }
            Snapshot = new ArmyTacticalSnapshot(warriors, lancers, archers, monks);
            using (CohesionMarker.Auto())
            {
                ArmyAnchor = CalculateRobustAnchor(members, ArmyAnchor);
                StragglerCount = CountOutside(members, ArmyAnchor, StragglerRadius);
            }
            VisibleEnemyCount = CountLocalEnemies(knowledge, ArmyAnchor,
                LocalEngagementRadius);

            if (strategicState is ArmyGroupState.Retreating or ArmyGroupState.Recovering)
                State = TacticalArmyState.ExistingStrategicRetreat;
            else if (VisibleEnemyCount > 0
                || now - lastThreatSeenTime <= Mathf.Max(0f, engagementExitDelay))
                State = TacticalArmyState.Engaging;
            else if (StragglerCount > 0)
            {
                using var regroup = LocalRegroupMarker.Auto();
                int severeThreshold = Mathf.Max(1, members.Count / 3);
                State = StragglerCount > severeThreshold
                    ? TacticalArmyState.LocalRegroup
                    : strategicState is ArmyGroupState.Advancing
                        or ArmyGroupState.Assaulting
                        ? TacticalArmyState.Advancing : TacticalArmyState.Assembling;
            }
            else if (strategicState is ArmyGroupState.Advancing or ArmyGroupState.Assaulting)
                State = TacticalArmyState.Advancing;
            else State = TacticalArmyState.Assembling;
        }

        public bool BuildFormationAssignments(FactionKnowledge knowledge,
            IReadOnlyList<UnitController> members, Vector3 destination,
            IDictionary<UnitController, Vector3> output, bool force = false)
        {
            using var marker = FormationMarker.Auto();
            if (output == null) return false;
            int signature = MembershipSignature(members);
            bool destinationChanged = !hasFormationPlan
                || Vector2.Distance(formationAnchor, destination)
                    > Mathf.Max(0.1f, formationDestinationThreshold);
            if (force || destinationChanged || signature != formationMembershipSignature)
            {
                formationDestinations.Clear();
                GridManager grid = knowledge?.TeamVision?.GridManager;
                if (grid == null) return false;
                formationUnits.Clear();
                for (int i = 0; i < members.Count; i++)
                {
                    UnitController member = members[i];
                    UnitSelection selection = member != null
                        ? member.GetComponent<UnitSelection>() : null;
                    if (selection != null && member.IsAlive
                        && IsTacticalMilitaryType(TypeOf(member) ?? UnitType.Pawn))
                        formationUnits.Add(selection);
                }

                if (formationUnits.Count == 1)
                {
                    List<GridCell> cells = DestinationDistributor.FindNearbyDestinations(
                        grid, grid.WorldToGrid(destination), 1, formationSearchRadius, true);
                    if (cells.Count > 0)
                        formationDestinations[formationUnits[0].Controller] =
                            cells[0].WorldPosition;
                }
                else if (formationUnits.Count > 1)
                {
                    Vector2 forward = RoleFormationPlanner.ResolveForward(ArmyAnchor,
                        destination);
                    if (RoleFormationPlanner.TryPlan(grid, formationUnits, destination,
                            formationSpacing, formationCandidateMultiplier,
                            formationSearchRadius, true, AdvanceFormation, forward,
                            formationAssignments))
                    {
                        for (int i = 0; i < formationAssignments.Count; i++)
                        {
                            FormationAssignment assignment = formationAssignments[i];
                            if (assignment.Unit?.Controller != null && assignment.Cell != null)
                                formationDestinations[assignment.Unit.Controller] =
                                    assignment.Cell.WorldPosition;
                        }
                    }
                }

                if (formationDestinations.Count == 0)
                {
                    List<GridCell> cells = DestinationDistributor.FindNearbyDestinations(
                        grid, grid.WorldToGrid(destination), members.Count,
                        formationSearchRadius, true);
                    int count = Mathf.Min(cells.Count, members.Count);
                    for (int i = 0; i < count; i++)
                        if (members[i] != null)
                            formationDestinations[members[i]] = cells[i].WorldPosition;
                }

                formationMembershipSignature = signature;
                formationAnchor = destination;
                hasFormationPlan = formationDestinations.Count > 0;
                if (hasFormationPlan) FormationUpdateCount++;
            }

            output.Clear();
            foreach (KeyValuePair<UnitController, Vector3> pair in formationDestinations)
                output[pair.Key] = pair.Value;
            return output.Count > 0;
        }

        public bool TryEngageVisibleEnemies(CommanderDecisionContext context,
            IReadOnlyList<UnitController> members, Vector3 engagementCenter,
            float engagementRadius, EntityId priorityTarget,
            IPathTraversalPolicy traversalPolicy)
        {
            float radius = Mathf.Max(1f, engagementRadius);
            GatherLocalEnemies(context.Knowledge, engagementCenter, radius);
            if (localEnemies.Count == 0)
            {
                bool lingering = context.Time - lastThreatSeenTime
                    <= Mathf.Max(0f, engagementExitDelay);
                if (!lingering && State == TacticalArmyState.Engaging)
                    State = StragglerCount > 0 ? TacticalArmyState.LocalRegroup
                        : TacticalArmyState.Advancing;
                return lingering;
            }

            lastThreatSeenTime = context.Time;
            State = TacticalArmyState.Engaging;
            if (context.Time < nextEvaluationTime) return true;
            nextEvaluationTime = context.Time + EvaluationInterval;
            using var marker = TargetingMarker.Auto();
            BuildSaturation(members);

            for (int i = 0; i < members.Count; i++)
            {
                UnitController actor = members[i];
                UnitType? type = TypeOf(actor);
                if (actor == null || !actor.IsAlive || !type.HasValue) continue;
                if (type == UnitType.Monk)
                {
                    TryRepositionSupport(context, actor, traversalPolicy);
                    continue;
                }
                if (type is not (UnitType.Warrior or UnitType.Lancer or UnitType.Archer))
                    continue;
                CombatController combat = actor.GetComponent<CombatController>();
                if (combat == null || combat.IsMeleeAttackCommitted) continue;
                if (type == UnitType.Archer
                    && TryRepositionArcher(context, actor, traversalPolicy)) continue;

                EntityId currentId = default;
                VisibleUnitObservation current = default;
                bool hasCurrent = combat.Target != null
                    && TryFindLocal(combat.Target.GetEntityId(), out current);
                if (hasCurrent) currentId = current.RuntimeId;
                VisibleUnitObservation selected = default;
                float selectedScore = float.NegativeInfinity;
                bool found = false;
                for (int targetIndex = 0; targetIndex < localEnemies.Count; targetIndex++)
                {
                    VisibleUnitObservation candidate = localEnemies[targetIndex];
                    float score = ScoreTarget(actor, type.Value, candidate,
                        hasCurrent && candidate.RuntimeId.CompareTo(currentId) == 0,
                        priorityTarget);
                    if (!found || score > selectedScore + 0.0001f
                        || Mathf.Abs(score - selectedScore) <= 0.0001f
                            && candidate.RuntimeId.CompareTo(selected.RuntimeId) < 0)
                    {
                        selected = candidate;
                        selectedScore = score;
                        found = true;
                    }
                }
                if (!found) continue;

                if (hasCurrent)
                {
                    float currentScore = ScoreTarget(actor, type.Value, current, true,
                        priorityTarget);
                    bool held = targetAssignments.TryGetValue(actor,
                        out TargetAssignment assignment)
                        && assignment.TargetId.CompareTo(currentId) == 0
                        && context.Time < assignment.HoldUntil;
                    if (held || selectedScore < currentScore + RetargetThreshold)
                    {
                        targetAssignments[actor] = new TargetAssignment(currentId,
                            currentScore, held ? assignment.HoldUntil : context.Time);
                        continue;
                    }
                }
                if (context.Time < RetryTime(actor)) continue;
                CommanderCommandResult result = context.Commands.TryAttack(actor,
                    selected.RuntimeId);
                if (result == CommanderCommandResult.Accepted)
                {
                    LastAcceptedCommandTime = context.Time;
                    if (hasCurrent && currentId.CompareTo(selected.RuntimeId) != 0)
                    {
                        RetargetCount++;
                        RemoveSaturation(currentId);
                    }
                    AttackCommandsIssued++;
                    targetAssignments[actor] = new TargetAssignment(selected.RuntimeId,
                        selectedScore, context.Time + MinimumTargetHoldSeconds);
                    AddSaturation(selected.RuntimeId);
                }
                else
                {
                    LastRejectedCommandTime = context.Time;
                    retryAfter[actor] = context.Time + commandRetrySeconds;
                }
            }
            return true;
        }

        public void Reset()
        {
            formationUnits.Clear();
            formationAssignments.Clear();
            formationDestinations.Clear();
            localEnemies.Clear();
            targetSaturation.Clear();
            targetAssignments.Clear();
            retryAfter.Clear();
            repositionAfter.Clear();
            repositionDestinations.Clear();
            staleActors.Clear();
            anchorX.Clear();
            anchorY.Clear();
            nextEvaluationTime = 0f;
            lastThreatSeenTime = float.NegativeInfinity;
            formationMembershipSignature = 0;
            hasFormationPlan = false;
            formationAnchor = default;
            Snapshot = default;
            State = TacticalArmyState.Assembling;
            ArmyAnchor = default;
            StragglerCount = VisibleEnemyCount = 0;
            AttackCommandsIssued = RetargetCount = FormationUpdateCount = 0;
            RepositionCommandCount = 0;
            LastAcceptedCommandTime = LastRejectedCommandTime = -1f;
            LastEvaluationTime = -1f;
        }

        private void GatherLocalEnemies(FactionKnowledge knowledge, Vector3 center,
            float radius)
        {
            localEnemies.Clear();
            if (knowledge == null) return;
            float radiusSquared = radius * radius;
            IReadOnlyList<VisibleUnitObservation> visible = knowledge.VisibleEnemyUnits;
            for (int i = 0; i < visible.Count; i++)
            {
                VisibleUnitObservation candidate = visible[i];
                if (!IsMeaningfulTacticalTarget(candidate)
                    || DistanceSquared(candidate.Position, center) > radiusSquared) continue;
                AddBoundedLocalEnemy(candidate, center);
            }
        }

        private void AddBoundedLocalEnemy(VisibleUnitObservation candidate, Vector3 center)
        {
            if (localEnemies.Count < MaxLocalTargetCandidates)
            {
                localEnemies.Add(candidate);
                return;
            }
            int worstIndex = -1;
            float worstDistance = float.NegativeInfinity;
            for (int i = 0; i < localEnemies.Count; i++)
            {
                float distance = DistanceSquared(localEnemies[i].Position, center);
                if (distance > worstDistance + 0.0001f
                    || Mathf.Abs(distance - worstDistance) <= 0.0001f
                        && (worstIndex < 0 || localEnemies[i].RuntimeId.CompareTo(
                            localEnemies[worstIndex].RuntimeId) > 0))
                {
                    worstDistance = distance;
                    worstIndex = i;
                }
            }
            float candidateDistance = DistanceSquared(candidate.Position, center);
            if (worstIndex >= 0 && (candidateDistance < worstDistance - 0.0001f
                || Mathf.Abs(candidateDistance - worstDistance) <= 0.0001f
                    && candidate.RuntimeId.CompareTo(localEnemies[worstIndex].RuntimeId) < 0))
                localEnemies[worstIndex] = candidate;
        }

        private int CountLocalEnemies(FactionKnowledge knowledge, Vector3 center,
            float radius)
        {
            if (knowledge == null) return 0;
            int count = 0;
            float radiusSquared = radius * radius;
            IReadOnlyList<VisibleUnitObservation> visible = knowledge.VisibleEnemyUnits;
            for (int i = 0; i < visible.Count; i++)
                if (IsMeaningfulTacticalTarget(visible[i])
                    && DistanceSquared(visible[i].Position, center) <= radiusSquared) count++;
            return count;
        }

        private static bool IsMeaningfulTacticalTarget(VisibleUnitObservation target)
            => target.UnitType is UnitType.Warrior or UnitType.Lancer
                or UnitType.Archer or UnitType.Monk
                || target.ThreatActivity != ObservedThreatActivity.None;

        private float ScoreTarget(UnitController actor, UnitType attacker,
            VisibleUnitObservation target, bool current, EntityId priorityTarget)
        {
            float distance = Vector2.Distance(actor.transform.position, target.Position);
            UnitCombatData data = actor.GetComponent<HealthComponent>()?.CombatData;
            float range = data != null ? Mathf.Max(0.1f, data.AttackRange) : 1f;
            float score = baseTargetScore - distance * distancePenalty;
            score += (CombatDamage.Multiplier(attacker, target.UnitType) - 1f)
                * matchupWeight;
            float healthRatio = target.MaxHP > 0
                ? Mathf.Clamp01((float)target.CurrentHP / target.MaxHP) : 1f;
            score += (1f - healthRatio) * lowHealthWeight;
            if (target.ThreatActivity != ObservedThreatActivity.None) score += threatWeight;
            if (target.UnitType == UnitType.Monk) score += monkPriority;
            else if (target.UnitType == UnitType.Archer) score += archerPriority;
            if (priorityTarget.CompareTo(default) != 0
                && target.RuntimeId.CompareTo(priorityTarget) == 0)
                score += threatWeight;
            if (current) score += TargetStickiness;
            int saturation = targetSaturation.TryGetValue(target.RuntimeId, out int assigned)
                ? assigned : 0;
            if (current && saturation > 0) saturation--;
            score -= saturation * (attacker == UnitType.Archer
                ? RangedSaturationPenalty : MeleeSaturationPenalty);
            if (attacker != UnitType.Archer && distance > range + 2f)
                score -= (distance - range - 2f) * distancePenalty;
            return score;
        }

        private void BuildSaturation(IReadOnlyList<UnitController> members)
        {
            targetSaturation.Clear();
            for (int i = 0; i < members.Count; i++)
            {
                CombatController combat = members[i] != null
                    ? members[i].GetComponent<CombatController>() : null;
                if (combat?.Target == null
                    || !TryFindLocal(combat.Target.GetEntityId(), out VisibleUnitObservation target))
                    continue;
                AddSaturation(target.RuntimeId);
            }
        }

        private void AddSaturation(EntityId id)
        {
            targetSaturation.TryGetValue(id, out int count);
            targetSaturation[id] = count + 1;
        }

        private void RemoveSaturation(EntityId id)
        {
            if (!targetSaturation.TryGetValue(id, out int count)) return;
            if (count <= 1) targetSaturation.Remove(id);
            else targetSaturation[id] = count - 1;
        }

        private bool TryFindLocal(EntityId id, out VisibleUnitObservation observation)
        {
            for (int i = 0; i < localEnemies.Count; i++)
                if (localEnemies[i].RuntimeId.CompareTo(id) == 0)
                {
                    observation = localEnemies[i];
                    return true;
                }
            observation = default;
            return false;
        }

        private bool TryRepositionArcher(CommanderDecisionContext context,
            UnitController archer, IPathTraversalPolicy traversalPolicy)
        {
            if (context.Time < RepositionTime(archer)) return false;
            HealthComponent health = archer.GetComponent<HealthComponent>();
            float attackRange = health?.CombatData != null
                ? health.CombatData.AttackRange : 1f;
            if (!TryNearestVisibleMelee(archer.transform.position, archerDangerRadius,
                    attackRange,
                    out VisibleUnitObservation threat)) return false;
            return TryIssueRearMove(context, archer, threat.Position,
                archerRepositionCooldown, traversalPolicy);
        }

        private bool TryRepositionSupport(CommanderDecisionContext context,
            UnitController monk, IPathTraversalPolicy traversalPolicy)
        {
            if (context.Time < RepositionTime(monk)) return false;
            if (!TryNearestVisibleMelee(monk.transform.position, monkDangerRadius,
                    monkDangerRadius,
                    out VisibleUnitObservation threat)) return false;
            return TryIssueRearMove(context, monk, threat.Position,
                monkRepositionCooldown, traversalPolicy);
        }

        private bool TryNearestVisibleMelee(Vector3 position, float radius,
            float preferredRange,
            out VisibleUnitObservation nearest)
        {
            nearest = default;
            bool found = false;
            float best = float.PositiveInfinity;
            for (int i = 0; i < localEnemies.Count; i++)
            {
                VisibleUnitObservation candidate = localEnemies[i];
                if (candidate.UnitType is not (UnitType.Warrior or UnitType.Lancer)) continue;
                float distance = DistanceSquared(position, candidate.Position);
                float dangerRange = Mathf.Min(Mathf.Max(radius,
                        candidate.AttackRange + 0.5f),
                    Mathf.Max(radius, preferredRange * 0.65f));
                if (distance > dangerRange * dangerRange || distance > best) continue;
                best = distance;
                nearest = candidate;
                found = true;
            }
            return found;
        }

        private bool TryIssueRearMove(CommanderDecisionContext context,
            UnitController actor, Vector3 threatPosition, float cooldown,
            IPathTraversalPolicy traversalPolicy)
        {
            if (context.Time < RetryTime(actor)) return false;
            GridManager grid = context.Knowledge?.TeamVision?.GridManager;
            if (grid == null) return false;
            Vector2 away = (Vector2)(actor.transform.position - threatPosition);
            if (away.sqrMagnitude < 0.001f)
                away = (Vector2)(ArmyAnchor - threatPosition);
            if (away.sqrMagnitude < 0.001f) away = Vector2.left;
            Vector3 requested = actor.transform.position
                + (Vector3)(away.normalized * backlineRepositionDistance);
            Vector2Int requestedCell = grid.WorldToGrid(requested);
            Vector2Int actorCellPosition = grid.WorldToGrid(actor.transform.position);
            GridCell actorCell = grid.GetCell(actorCellPosition.x, actorCellPosition.y);
            int requiredHeight = actorCell?.HeightLevel ?? 0;
            GridCell best = null;
            float bestScore = float.NegativeInfinity;
            for (int radius = 0; radius <= 3; radius++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                if (radius > 0 && Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != radius)
                    continue;
                GridCell candidate = grid.GetCell(requestedCell.x + x,
                    requestedCell.y + y);
                if (candidate == null || !candidate.Walkable || candidate.Occupied
                    || candidate.HeightLevel != requiredHeight
                    || !TerrainRules.IsStableStandingCell(candidate)) continue;
                float threatDistance = Vector2.Distance(candidate.WorldPosition,
                    threatPosition);
                float anchorDistance = Vector2.Distance(candidate.WorldPosition,
                    ArmyAnchor);
                float score = threatDistance - Mathf.Max(0f,
                    anchorDistance - ArmyCohesionRadius) * 2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            if (best == null) return false;
            Vector3 destination = best.WorldPosition;
            if (repositionDestinations.TryGetValue(actor, out Vector3 existing)
                && Vector2.Distance(existing, destination) <= 0.5f
                && actor.GetComponent<UnitMovement>()?.IsMoving == true) return true;
            CommanderCommandResult result = context.Commands.TryMilitaryMove(actor,
                destination, traversalPolicy);
            if (result != CommanderCommandResult.Accepted)
            {
                LastRejectedCommandTime = context.Time;
                retryAfter[actor] = context.Time + commandRetrySeconds;
                return false;
            }
            LastAcceptedCommandTime = context.Time;
            repositionDestinations[actor] = destination;
            repositionAfter[actor] = context.Time + Mathf.Max(0.1f, cooldown);
            RepositionCommandCount++;
            return true;
        }

        private Vector3 CalculateRobustAnchor(IReadOnlyList<UnitController> members,
            Vector3 fallback)
        {
            anchorX.Clear();
            anchorY.Clear();
            bool hasNonSupport = false;
            for (int i = 0; i < members.Count; i++)
            {
                UnitController member = members[i];
                UnitType? type = TypeOf(member);
                if (member == null || !member.IsAlive || !type.HasValue
                    || type == UnitType.Monk) continue;
                anchorX.Add(member.transform.position.x);
                anchorY.Add(member.transform.position.y);
                hasNonSupport = true;
            }
            if (!hasNonSupport)
                for (int i = 0; i < members.Count; i++)
                {
                    UnitController member = members[i];
                    if (member == null || !member.IsAlive) continue;
                    anchorX.Add(member.transform.position.x);
                    anchorY.Add(member.transform.position.y);
                }
            if (anchorX.Count == 0) return fallback;
            anchorX.Sort();
            anchorY.Sort();
            return new Vector3(Median(anchorX), Median(anchorY), 0f);
        }

        private static float Median(List<float> values)
        {
            int middle = values.Count / 2;
            return (values.Count & 1) == 1 ? values[middle]
                : 0.5f * (values[middle - 1] + values[middle]);
        }

        private static int CountOutside(IReadOnlyList<UnitController> members,
            Vector3 center, float radius)
        {
            int count = 0;
            float squared = radius * radius;
            for (int i = 0; i < members.Count; i++)
                if (members[i] != null && members[i].IsAlive
                    && DistanceSquared(members[i].transform.position, center) > squared)
                    count++;
            return count;
        }

        private static int MembershipSignature(IReadOnlyList<UnitController> members)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < members.Count; i++)
                    hash = hash * 31 + (members[i] != null
                        ? members[i].GetEntityId().GetHashCode() : 0);
                return hash;
            }
        }

        private void RemoveStaleActors(IReadOnlyList<UnitController> members)
        {
            RemoveStale(targetAssignments, members);
            RemoveStale(retryAfter, members);
            RemoveStale(repositionAfter, members);
            RemoveStale(repositionDestinations, members);
        }

        private void RemoveStale<T>(Dictionary<UnitController, T> dictionary,
            IReadOnlyList<UnitController> members)
        {
            staleActors.Clear();
            foreach (UnitController actor in dictionary.Keys)
            {
                bool found = false;
                for (int i = 0; i < members.Count; i++)
                    if (members[i] == actor) { found = true; break; }
                if (!found) staleActors.Add(actor);
            }
            for (int i = 0; i < staleActors.Count; i++)
                dictionary.Remove(staleActors[i]);
        }

        private float RetryTime(UnitController actor)
            => retryAfter.TryGetValue(actor, out float value) ? value : 0f;

        private float RepositionTime(UnitController actor)
            => repositionAfter.TryGetValue(actor, out float value) ? value : 0f;

        private static UnitType? TypeOf(UnitController unit)
            => unit?.GetComponent<HealthComponent>()?.CombatData?.UnitType;

        private static float DistanceSquared(Vector3 first, Vector3 second)
            => ((Vector2)(first - second)).sqrMagnitude;
    }
}
