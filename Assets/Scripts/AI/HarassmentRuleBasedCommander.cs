using System.Collections.Generic;
using System.Linq;
using TinyKingdom.Buildings;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.AI
{
    // Owns only the raiding party. The shared commander still runs the economy,
    // trains units, defends with the reserve and commands the full assault.
    internal sealed class HarassmentRuleBasedCommander
    {
        internal enum Phase
        {
            EconomicSetup, EarlyHarassment, Retreat, Recover, ReorganizeArmy,
            RepeatedHarassment, FullAssault, DefendAndRebuild
        }

        private enum TargetKind { Recon, Worker, EconomicBuilding, Production, SmallGroup, Barracks, Castle }

        private readonly struct RaidTarget
        {
            internal readonly TargetKind Kind;
            internal readonly EntityId Id;
            internal readonly Vector3 Position;
            internal readonly float Health;

            internal RaidTarget(TargetKind kind, EntityId id, Vector3 position, float health)
            {
                Kind = kind;
                Id = id;
                Position = position;
                Health = health;
            }
        }

        private const int RaidSize = 2;
        private const int ReserveSize = 1;
        private const float RaidHealthFloor = 0.70f;
        internal const float CastleAssaultHealthFloor = 0.60f;
        private const float LocalThreatRadius = 7f;
        private const float OrderInterval = 2f;
        private const float RaidNoProgressSeconds = 18f;
        private const float RecoverySeconds = 8f;
        private const float MaxRetreatSeconds = 35f;
        private readonly RuleBasedCommander commander;
        private readonly HashSet<UnitController> retreatOrders = new();
        private RaidTarget? target;
        private Vector3? previousRaidSite;
        private float phaseSince;
        private float nextOrder;
        private float lastProgress;
        private float bestDistance;
        private float bestTargetHealth;
        private float nextRaidAllowed;
        private bool foundAssaultOpening;
        private bool raidDamageObserved;
        private bool attackOrderLogged;
        private bool reconOrderLogged;
        private float nextTargetSearch;
        private int raidCount;
        private int survivors;
        private int dispatchedRaidSize;

        internal HashSet<UnitController> DetachedUnits { get; } = new();
        internal Phase CurrentPhase { get; private set; } = Phase.EconomicSetup;
        internal string Status { get; private set; } = "Developing economy";

        internal HarassmentRuleBasedCommander(RuleBasedCommander commander)
            => this.commander = commander;

        internal StrategicIntent Tick(CommanderDecisionContext context)
        {
            MilitaryCommanderPolicy military = commander.MilitaryPolicy;
            FactionKnowledge knowledge = context.Knowledge;
            DetachedUnits.RemoveWhere(unit => unit == null || !unit.IsAlive || !unit.IsWorldParticipant);

            bool defending = military.HasCurrentThreat || commander.StrategicIntent.Situation.BaseThreatened;
            if (defending && CurrentPhase is (Phase.EarlyHarassment or Phase.RepeatedHarassment))
                BeginRetreat(context, "enemy threatens home");
            if (CurrentPhase == Phase.Retreat)
            {
                TickRetreat(context);
                return defending ? StrategicIntent.Fortify : StrategicIntent.Recover;
            }
            if (defending)
            {
                SetPhase(context, Phase.DefendAndRebuild, "defending the home economy");
                return StrategicIntent.Fortify;
            }
            if (CurrentPhase is Phase.EarlyHarassment or Phase.RepeatedHarassment)
            {
                TickRaid(context);
                if (CurrentPhase == Phase.Retreat)
                {
                    TickRetreat(context);
                    return StrategicIntent.Recover;
                }
                return StrategicIntent.Scout;
            }
            if (CurrentPhase == Phase.FullAssault && military.Army.State
                is ArmyGroupState.PreparingAttack or ArmyGroupState.Advancing or ArmyGroupState.Assaulting)
            {
                BuildingType objectiveType = military.OffensiveTarget.BuildingType;
                Status = military.Army.State == ArmyGroupState.Assaulting && military.DemoBuildingOrderDispatched
                    ? $"Attack order accepted: {objectiveType}"
                    : military.Army.State == ArmyGroupState.Advancing && military.DemoRouteDispatched
                        ? $"March orders issued toward {objectiveType}"
                        : $"Finding an approach to {objectiveType}";
                return StrategicIntent.Attack;
            }
            if (military.Army.State is ArmyGroupState.Retreating or ArmyGroupState.Recovering)
            {
                SetPhase(context, Phase.Recover, "rebuilding after the main army withdrew");
                return StrategicIntent.Recover;
            }

            List<UnitController> force = knowledge.OwnUnits
                .Where(unit => ArmyGroup.IsEligible(unit, context.Faction, commander.CurrentScout))
                .ToList();
            int workers = commander.ProductionPolicy.LivingPawnCount;
            bool producer = knowledge.OwnBuildings.Any(building => building.IsProductionReady
                && building.Type is BuildingType.Barracks or BuildingType.Archery);
            if (raidCount > 0 && workers >= 4 && producer)
                commander.ProductionPolicy.RequestStrategicMilitaryPopulation(10, context.Time);

            StrategicTargetSnapshot objective = commander.StrategicIntent.StrategicTarget;
            int healthyAttackers = force.Count(IsReadyCastleAttacker);
            if (CanAssault(context, force, objective))
            {
                SetPhase(context, Phase.FullAssault, $"assembling for {objective.BuildingType}");
                Status = $"Assembling for {objective.BuildingType}";
                return StrategicIntent.Attack;
            }
            if (CurrentPhase == Phase.Recover && context.Time < nextRaidAllowed)
            {
                Status = $"Regrouping · {healthyAttackers} healthy combat units";
                return StrategicIntent.Scout;
            }
            if (CurrentPhase is Phase.Recover or Phase.DefendAndRebuild or Phase.FullAssault)
                SetPhase(context, Phase.ReorganizeArmy, "reassessing targets and replacing losses");
            int groupSize = healthyAttackers >= MilitaryCommanderPolicy.DemoCastleAssaultMinimum
                ? Mathf.Min(healthyAttackers, force.Count - ReserveSize, MilitaryCommanderPolicy.DemoCastleAssaultMinimum)
                : RaidSize;
            float healthFloor = groupSize > RaidSize ? CastleAssaultHealthFloor : RaidHealthFloor;
            List<UnitController> raiders = force.Where(unit => IsDirectCombatant(unit)
                    && Health(unit) >= healthFloor)
                .OrderByDescending(unit => unit.GetComponent<UnitMovement>()?.MovementSpeed ?? 0f)
                .ThenBy(unit => unit.GetEntityId())
                .Take(groupSize).ToList();
            if (force.Count < groupSize + ReserveSize || raiders.Count < groupSize)
            {
                Status = $"Preparing healthy raiders ({raiders.Count}/{groupSize}) and a reserve";
                return StrategicIntent.Scout;
            }
            if (context.Time < nextRaidAllowed || context.Time < nextTargetSearch)
                return StrategicIntent.Scout;

            Vector3 origin = Centroid(raiders);
            float strength = CombatStrengthEvaluator.OwnArmy(raiders, military.CombatStrength, out _, out _);
            RaidTarget? candidate = FindTarget(context, origin, strength);
            candidate ??= FindForwardPosition(context, raiders, origin, strength);
            if (!candidate.HasValue)
            {
                nextTargetSearch = context.Time + OrderInterval;
                Status = "Army ready · searching for a safe route";
                return StrategicIntent.Scout;
            }

            DetachedUnits.UnionWith(raiders);
            dispatchedRaidSize = raiders.Count;
            raidCount++;
            raidDamageObserved = false;
            attackOrderLogged = false;
            reconOrderLogged = false;
            SetPhase(context, raidCount == 1 ? Phase.EarlyHarassment : Phase.RepeatedHarassment,
                $"sending {dispatchedRaidSize} raiders, {force.Count - dispatchedRaidSize} troops at home");
            SelectTarget(context, candidate.Value, origin);
            IssueRaidOrders(context);
            return StrategicIntent.Scout;
        }

        private bool CanAssault(CommanderDecisionContext context, List<UnitController> force,
            StrategicTargetSnapshot objective)
        {
            if (!objective.IsValid || !FactionRelations.IsPlayable(objective.Faction))
                return false;
            if (objective.BuildingType is BuildingType.Castle or BuildingType.Barracks)
                return force.Count(IsReadyCastleAttacker)
                    >= MilitaryCommanderPolicy.DemoCastleAssaultMinimum;
            if (force.Count < commander.MilitaryPolicy.MinOffensiveMilitaryPopulation
                || force.Average(Health) < 0.60f)
                return false;
            float strength = CombatStrengthEvaluator.OwnArmy(force,
                commander.MilitaryPolicy.CombatStrength, out _, out _);
            float defense = LocalThreat(context.Knowledge, objective.LastKnownPosition);
            return strength >= Mathf.Max(3f, defense * (foundAssaultOpening ? 1.05f : 1.15f));
        }

        // Straight Lane runs east-west. Advance through explored ground toward
        // the far side even when the regular scout has no usable destination.
        private RaidTarget? FindForwardPosition(CommanderDecisionContext context,
            IReadOnlyCollection<UnitController> raiders, Vector3 origin, float strength)
        {
            FactionKnowledge knowledge = context.Knowledge;
            GridManager grid = knowledge.TeamVision.GridManager;
            PathfindingSystem paths = grid.GetComponent<PathfindingSystem>();
            if (paths == null) return null;
            Vector2Int from = grid.WorldToGrid(origin);
            int direction = grid.WorldToGrid(commander.MilitaryPolicy.BaseAnchor).x < grid.Width / 2 ? 1 : -1;
            var candidates = new List<(GridCell cell, float score)>();
            for (int advance = 3; advance <= 14; advance++)
            for (int offset = -12; offset <= 12; offset++)
            {
                var point = new Vector2Int(from.x + direction * advance, from.y + offset);
                GridCell cell = grid.GetCell(point.x, point.y);
                if (cell == null || !TerrainRules.IsStableStandingCell(cell)
                    || !knowledge.IsCellExplored(point)
                    || commander.StrategicSafety.RiskAt(point, StrategicPathKind.Army) >= StrategicRisk.Dangerous)
                    continue;
                if (previousRaidSite.HasValue && Vector2.Distance(cell.WorldPosition, previousRaidSite.Value) < 4f)
                    continue;
                float score = advance - 0.45f * Mathf.Abs(offset)
                    - 0.20f * Mathf.Abs(point.y - grid.Height / 2);
                candidates.Add((cell, score));
            }
            foreach (var candidate in candidates.OrderByDescending(item => item.score).Take(36))
            {
                Vector3 destination = candidate.cell.WorldPosition;
                if (LocalThreat(knowledge, destination) > strength * 0.65f) continue;
                Vector2Int point = grid.WorldToGrid(destination);
                if (raiders.All(unit => paths.FindPath(grid.WorldToGrid(unit.transform.position),
                        point, commander.StrategicSafety.ArmyTraversal).Count > 0))
                    return new RaidTarget(TargetKind.Recon, default, destination, 1f);
            }
            return null;
        }

        private RaidTarget? FindTarget(CommanderDecisionContext context, Vector3 origin, float strength)
        {
            FactionKnowledge knowledge = context.Knowledge;
            RaidTarget? best = null;
            float bestScore = float.NegativeInfinity;
            foreach (VisibleUnitObservation unit in knowledge.VisibleEnemyUnits)
            {
                if (!FactionRelations.IsPlayable(unit.Faction)) continue;
                bool worker = unit.UnitType == UnitType.Pawn;
                Consider(new RaidTarget(worker ? TargetKind.Worker : TargetKind.SmallGroup,
                    unit.RuntimeId, unit.Position, CombatStrengthEvaluator.HealthRatio(unit.CurrentHP, unit.MaxHP)),
                    worker ? 5f : 1f);
            }
            foreach (BuildingKnowledgeSnapshot building in knowledge.VisibleEnemyBuildings)
            {
                float priority = building.BuildingType switch
                {
                    BuildingType.House => 3.5f,
                    BuildingType.Barracks or BuildingType.Archery or BuildingType.Monastery => 2f,
                    _ => 0f
                };
                if (priority <= 0f || !FactionRelations.IsPlayable(building.Faction)) continue;
                Consider(new RaidTarget(building.BuildingType == BuildingType.House
                    ? TargetKind.EconomicBuilding : TargetKind.Production, building.RuntimeId,
                    building.LastSeenPosition, building.LastSeenHealthRatio), priority);
            }
            return best;

            void Consider(RaidTarget candidate, float priority)
            {
                float danger = LocalThreat(knowledge, candidate.Position);
                if (danger >= strength * 0.80f
                    || commander.StrategicSafety.RiskAt(candidate.Position, StrategicPathKind.Army)
                        >= StrategicRisk.Dangerous)
                    return;
                float repeat = previousRaidSite.HasValue
                    && Vector2.Distance(candidate.Position, previousRaidSite.Value) < 5f ? 1.5f : 0f;
                float score = priority + (1f - candidate.Health) - danger
                    - 0.07f * Vector2.Distance(origin, candidate.Position) - repeat;
                if (score <= bestScore) return;
                bestScore = score;
                best = candidate;
            }
        }

        private RaidTarget? FindFrontlineBuilding(CommanderDecisionContext context, Vector3 origin)
        {
            RaidTarget? barracks = null;
            RaidTarget? castle = null;
            float nearestBarracks = float.PositiveInfinity;
            float nearestCastle = float.PositiveInfinity;
            foreach (BuildingKnowledgeSnapshot building in context.Knowledge.VisibleEnemyBuildings)
            {
                if (!FactionRelations.IsPlayable(building.Faction) || !building.WasCompleted
                    || building.LastSeenHealthRatio <= 0f)
                    continue;
                float distance = Vector2.Distance(origin, building.LastSeenPosition);
                if (building.BuildingType == BuildingType.Barracks && distance <= 7f && distance < nearestBarracks)
                {
                    nearestBarracks = distance;
                    barracks = new RaidTarget(TargetKind.Barracks, building.RuntimeId,
                        building.LastSeenPosition, building.LastSeenHealthRatio);
                }
                else if (building.BuildingType == BuildingType.Castle && distance <= 12f && distance < nearestCastle)
                {
                    nearestCastle = distance;
                    castle = new RaidTarget(TargetKind.Castle, building.RuntimeId,
                        building.LastSeenPosition, building.LastSeenHealthRatio);
                }
            }
            return barracks ?? castle;
        }

        private void TickRaid(CommanderDecisionContext context)
        {
            if (DetachedUnits.Count == 0)
            {
                BeginRetreat(context, "raiding party lost");
                return;
            }
            Vector3 origin = Centroid(DetachedUnits);
            float strength = CombatStrengthEvaluator.OwnArmy(DetachedUnits.ToList(),
                commander.MilitaryPolicy.CombatStrength, out _, out _);
            RaidTarget current = target.Value;
            bool attackingBase = current.Kind is TargetKind.Barracks or TargetKind.Castle;
            if (!attackingBase && FindFrontlineBuilding(context, origin) is { } frontline)
            {
                SelectTarget(context, frontline, origin);
                current = frontline;
                attackingBase = true;
            }
            else if (current.Kind == TargetKind.Castle
                && FindFrontlineBuilding(context, origin) is { Kind: TargetKind.Barracks } blocker
                && Vector2.Dot((Vector2)(blocker.Position - origin),
                    (Vector2)(current.Position - blocker.Position)) > 0f)
            {
                SelectTarget(context, blocker, origin);
                current = blocker;
            }
            if (DetachedUnits.Count <= dispatchedRaidSize / 2 || DetachedUnits.Average(Health) < 0.52f
                || LocalThreat(context.Knowledge, origin, includeBuildings: !attackingBase) > strength * 1.10f)
            {
                BeginRetreat(context, "losses, low health or stronger defenders");
                return;
            }
            if (current.Kind == TargetKind.Recon)
            {
                RaidTarget? revealed = FindTarget(context, origin, strength);
                if (revealed.HasValue) SelectTarget(context, revealed.Value, origin);
                else if (Vector2.Distance(origin, current.Position) < 2f)
                {
                    RaidTarget? forward = FindForwardPosition(context, DetachedUnits, origin, strength);
                    if (!forward.HasValue)
                    {
                        BeginRetreat(context, "no exposed target or safe forward route");
                        return;
                    }
                    SelectTarget(context, forward.Value, origin);
                }
            }
            else if (TryRefreshTarget(context.Knowledge, current, out RaidTarget observed))
            {
                target = observed;
                if (observed.Health < bestTargetHealth - 0.01f)
                {
                    bestTargetHealth = observed.Health;
                    lastProgress = context.Time;
                    if (observed.Kind != TargetKind.SmallGroup && !raidDamageObserved)
                    {
                        raidDamageObserved = true;
                        Log(context, $"economic target health fell target={observed.Kind}");
                    }
                }
                if (!attackingBase && LocalThreat(context.Knowledge, observed.Position) > strength * 0.95f)
                {
                    BeginRetreat(context, "target reinforced");
                    return;
                }
            }
            else if (attackingBase && FindFrontlineBuilding(context, origin) is { } nextBuilding)
            {
                SelectTarget(context, nextBuilding, origin);
            }
            else
            {
                BeginRetreat(context, "target no longer visible");
                return;
            }
            float distance = Vector2.Distance(origin, target.Value.Position);
            if (distance < bestDistance - 0.5f)
            {
                bestDistance = distance;
                lastProgress = context.Time;
            }
            if (context.Time - lastProgress > RaidNoProgressSeconds
                || (!attackingBase && context.Time - phaseSince > 65f))
            {
                BeginRetreat(context, "raid stalled or reached its exposure limit");
                return;
            }
            if (context.Time >= nextOrder) IssueRaidOrders(context);
        }

        private static bool TryRefreshTarget(FactionKnowledge knowledge, RaidTarget current, out RaidTarget result)
        {
            if (current.Kind is TargetKind.Worker or TargetKind.SmallGroup)
            {
                if (knowledge.TryGetVisibleEnemy(current.Id, out VisibleUnitObservation enemy))
                {
                    result = new RaidTarget(current.Kind, current.Id, enemy.Position,
                        CombatStrengthEvaluator.HealthRatio(enemy.CurrentHP, enemy.MaxHP));
                    return true;
                }
            }
            else if (knowledge.TryGetVisibleEnemyBuilding(current.Id, out BuildingKnowledgeSnapshot building))
            {
                result = new RaidTarget(current.Kind, current.Id, building.LastSeenPosition,
                    building.LastSeenHealthRatio);
                return true;
            }
            result = default;
            return false;
        }

        private void SelectTarget(CommanderDecisionContext context, RaidTarget candidate, Vector3 origin)
        {
            target = candidate;
            bestDistance = Vector2.Distance(origin, candidate.Position);
            bestTargetHealth = candidate.Health;
            lastProgress = context.Time;
            nextOrder = context.Time;
            attackOrderLogged = false;
            Status = candidate.Kind == TargetKind.Recon ? "Reconnaissance for an opening"
                : $"Targeting {candidate.Kind}";
            Log(context, $"target={candidate.Kind} position={candidate.Position}");
        }

        private void IssueRaidOrders(CommanderDecisionContext context)
        {
            RaidTarget current = target.Value;
            foreach (UnitController unit in DetachedUnits)
            {
                CombatController combat = unit.GetComponent<CombatController>();
                UnitMovement movement = unit.GetComponent<UnitMovement>();
                CommanderCommandResult result = CommanderCommandResult.RejectedCommandFailed;
                if (current.Kind == TargetKind.Recon)
                {
                    GridManager grid = context.Knowledge.TeamVision.GridManager;
                    if (movement != null && (!movement.IsMoving
                        || movement.CommandDestinationCell != grid.WorldToGrid(current.Position)))
                        result = context.Commands.TryMilitaryMove(unit, current.Position, commander.StrategicSafety.ArmyTraversal);
                }
                else if (current.Kind is TargetKind.Worker or TargetKind.SmallGroup)
                {
                    if (combat?.Target == null || combat.Target.GetEntityId().CompareTo(current.Id) != 0)
                        result = context.Commands.TryAttack(unit, current.Id);
                }
                else if (combat?.BuildingTarget == null || combat.BuildingTarget.GetEntityId().CompareTo(current.Id) != 0)
                    result = context.Commands.TryAttackBuilding(unit, current.Id, searchFullPerimeter: true);
                if (result == CommanderCommandResult.Accepted && current.Kind != TargetKind.Recon && !attackOrderLogged)
                {
                    attackOrderLogged = true;
                    Log(context, $"raid attack accepted target={current.Kind}");
                }
                if (result == CommanderCommandResult.Accepted && current.Kind == TargetKind.Recon && !reconOrderLogged)
                {
                    reconOrderLogged = true;
                    Log(context, $"raiders moving to {current.Position}");
                }
            }
            nextOrder = context.Time + OrderInterval;
        }

        private void BeginRetreat(CommanderDecisionContext context, string reason)
        {
            previousRaidSite = target?.Position;
            foundAssaultOpening |= raidDamageObserved && DetachedUnits.Count > 0;
            survivors = DetachedUnits.Count;
            SetPhase(context, Phase.Retreat, $"{reason}, {survivors}/{dispatchedRaidSize} survived");
            retreatOrders.Clear();
            nextOrder = context.Time;
        }

        private void TickRetreat(CommanderDecisionContext context)
        {
            MilitaryCommanderPolicy military = commander.MilitaryPolicy;
            Vector3 staging = military.StagingPosition;
            if (!military.HasStagingPosition)
            {
                staging = military.BaseAnchor;
                if (commander.StrategicSafety.TryFindLocalRetreatCell(context.Knowledge,
                        staging, StrategicPathKind.Army, 6, false, default, out Vector2Int cell))
                    staging = context.Knowledge.TeamVision.GridManager.GridToWorld(cell.x, cell.y);
            }
            bool allHome = DetachedUnits.All(unit => Vector2.Distance(unit.transform.position, staging) <= 4f);
            if (allHome || context.Time - phaseSince >= MaxRetreatSeconds)
            {
                foreach (UnitController unit in DetachedUnits)
                {
                    CombatController combat = unit.GetComponent<CombatController>();
                    combat?.EndRetreat();
                    if (!retreatOrders.Contains(unit)) combat?.StopCombat();
                }
                Log(context, $"recovery survivors={DetachedUnits.Count}/{survivors} reachedHome={allHome}");
                nextRaidAllowed = context.Time + (DetachedUnits.Count < dispatchedRaidSize ? 2 * RecoverySeconds : RecoverySeconds);
                DetachedUnits.Clear();
                retreatOrders.Clear();
                target = null;
                SetPhase(context, Phase.Recover, "restoring a healthy raiding party");
                return;
            }
            if (context.Time < nextOrder) return;
            foreach (UnitController unit in DetachedUnits)
            {
                if (Vector2.Distance(unit.transform.position, staging) <= 4f)
                {
                    if (!retreatOrders.Contains(unit)) unit.GetComponent<CombatController>()?.StopCombat();
                    continue;
                }
                UnitMovement movement = unit.GetComponent<UnitMovement>();
                if (retreatOrders.Contains(unit) && movement != null && movement.IsMoving) continue;
                if (context.Commands.TryRetreatMove(unit, staging, commander.StrategicSafety.ArmyTraversal)
                    == CommanderCommandResult.Accepted && retreatOrders.Add(unit) && retreatOrders.Count == 1)
                    Log(context, "retreat movement accepted");
            }
            nextOrder = context.Time + OrderInterval;
        }

        private float LocalThreat(FactionKnowledge knowledge, Vector3 position, bool includeBuildings = true)
        {
            CombatStrengthTuning tuning = commander.MilitaryPolicy.CombatStrength;
            float threat = 0f;
            foreach (VisibleUnitObservation unit in knowledge.VisibleEnemyUnits)
                if (Vector2.Distance(position, unit.Position) <= LocalThreatRadius)
                    threat += CombatStrengthEvaluator.VisibleEnemyUnit(unit, tuning);
            if (!includeBuildings) return threat;
            foreach (BuildingKnowledgeSnapshot building in knowledge.KnownEnemyBuildings)
                if (building.WasCompleted && building.DefensiveAttackRange > 0f
                    && Vector2.Distance(position, building.LastSeenPosition) <= building.DefensiveAttackRange + 1f)
                    threat += CombatStrengthEvaluator.VisibleDefensiveBuilding(building, tuning);
            return threat;
        }

        private static bool IsDirectCombatant(UnitController unit)
            => unit.GetComponent<HealthComponent>()?.CombatData?.UnitType
                is UnitType.Warrior or UnitType.Archer or UnitType.Lancer;

        internal static bool IsReadyCastleAttacker(UnitController unit)
            => IsDirectCombatant(unit) && Health(unit) >= CastleAssaultHealthFloor;

        private static float Health(UnitController unit)
        {
            HealthComponent health = unit.GetComponent<HealthComponent>();
            return health == null ? 0f : CombatStrengthEvaluator.HealthRatio(health.CurrentHP, health.MaxHP);
        }

        private static Vector3 Centroid(IEnumerable<UnitController> units)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (UnitController unit in units)
            {
                sum += unit.transform.position;
                count++;
            }
            return count > 0 ? sum / count : sum;
        }

        private void SetPhase(CommanderDecisionContext context, Phase phase, string reason)
        {
            if (CurrentPhase == phase) return;
            CurrentPhase = phase;
            phaseSince = context.Time;
            Status = reason;
            Log(context, $"phase={phase} {reason}");
        }

        private static void Log(CommanderDecisionContext context, string message)
            => Debug.Log($"DEMO_RB01 Blue t={context.Time:F1} {message}");
    }
}
