using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.AI
{
    // Executes the strategic mine decision through the existing Pawn station
    // authority. It never awards Gold or controls the mine's payout timer.
    [Serializable]
    public sealed class GoldMineWorkerPolicy
    {
        private static readonly ProfilerMarker AssignmentMarker =
            new("TinyKingdom.AI.GoldMine.AssignWorkers");
        [SerializeField, Min(1)] private int minimumEconomyPawns = 3;
        [SerializeField, Range(1, GoldMineObjective.PawnCapacity)]
        private int maximumAssignedMiners = GoldMineObjective.PawnCapacity;
        [SerializeField, Min(1f)] private float assignmentRetrySeconds = 5f;
        private readonly Dictionary<UnitController, EntityId> pending = new();
        private readonly List<UnitController> removals = new();
        private float nextAttemptTime;
        private float lastSafeObservationTime = -1f;
        public int PendingCount => pending.Count;
        public uint AssignmentSerial { get; private set; }
        public uint EjectionSerial { get; private set; }
        public uint FailedAttemptCount { get; private set; }

        public void Reset(CommanderPawnRoles roles)
        {
            foreach (UnitController worker in pending.Keys)
                roles?.Release(worker, CommanderPawnRole.Mine);
            pending.Clear();
            removals.Clear();
            nextAttemptTime = 0f;
            lastSafeObservationTime = -1f;
            AssignmentSerial = EjectionSerial = FailedAttemptCount = 0;
        }

        public void Tick(CommanderDecisionContext context, CommanderPawnRoles roles,
            GoldMineOpportunity opportunity, StrategicSafetyMap safety)
        {
            using var marker = AssignmentMarker.Auto();
            if (context.Knowledge == null || roles == null) return;
            bool safe = opportunity.IsValid && opportunity.SafeForMiners;
            if (safe) lastSafeObservationTime = context.Time;
            bool observedDanger = opportunity.IsValid
                && (opportunity.KnownGuardStrength > 0f
                    || safety != null && safety.RiskAt(
                        opportunity.Mine.LastSeenPosition,
                        StrategicPathKind.Civilian) >= StrategicRisk.Dangerous);
            bool recaptureControlLost = opportunity.IsValid && !safe
                && opportunity.Mine.HasOwner
                && opportunity.Mine.Owner != context.Faction;
            // Losing sight of a previously cleared mine should not cancel a
            // Pawn already on its way every time army vision flickers. A known
            // threat cancels at once; stale travel expires after a bounded wait.
            bool cancelTravel = observedDanger || recaptureControlLost || !safe
                && (lastSafeObservationTime < 0f
                    || context.Time - lastSafeObservationTime > 30f);
            PrunePending(context.Knowledge, context.Faction, roles, cancelTravel,
                opportunity.IsValid ? opportunity.Mine.RuntimeId : default,
                opportunity.IsValid);
            if (!safe)
            {
                // An observed threat invalidates travel orders. Occupants of a
                // visible friendly mine are ejected before they are attacked.
                if (opportunity.IsValid && IsVisible(context.Knowledge,
                        opportunity.Mine.RuntimeId)
                    && safety != null && safety.RiskAt(
                        opportunity.Mine.LastSeenPosition,
                        StrategicPathKind.Civilian) >= StrategicRisk.Dangerous)
                {
                    GoldMineObjective mine = ResolveVisibleMine(context.Knowledge,
                        opportunity.Mine.RuntimeId);
                    if (mine != null && mine.HasOwner
                        && mine.Faction == context.Faction
                        && mine.OccupantCount > 0 && mine.EjectAll() > 0)
                        EjectionSerial++;
                }
                return;
            }
            if (context.Time < nextAttemptTime
                || roles.EconomyCount <= minimumEconomyPawns
                || pending.Count >= maximumAssignedMiners
                || opportunity.Mine.LastSeenOccupants + pending.Count
                    >= maximumAssignedMiners) return;
            UnitController worker = ChooseWorker(context.Knowledge, roles);
            if (worker == null) return;
            CommanderCommandResult result = context.Commands.TryEnterGoldMine(worker,
                opportunity.Mine.RuntimeId, safety);
            nextAttemptTime = context.Time + assignmentRetrySeconds;
            if (result != CommanderCommandResult.Accepted)
            {
                FailedAttemptCount++;
                return;
            }
            if (!roles.TryAssign(worker, CommanderPawnRole.Mine))
            {
                worker.GetComponent<UnitStationing>()?.CancelPending();
                return;
            }
            pending[worker] = opportunity.Mine.RuntimeId;
            AssignmentSerial++;
        }

        private void PrunePending(FactionKnowledge knowledge, Faction faction,
            CommanderPawnRoles roles, bool cancelTravel, EntityId activeMine,
            bool hasActiveMine)
        {
            removals.Clear();
            foreach (KeyValuePair<UnitController, EntityId> item in pending)
            {
                UnitController worker = item.Key;
                if (worker == null || !worker.IsAlive || !worker.IsWorldParticipant)
                { removals.Add(worker); continue; }
                UnitStationing station = worker.GetComponent<UnitStationing>();
                bool unavailable = false;
                IReadOnlyList<GoldMineKnowledgeSnapshot> visible =
                    knowledge.VisibleGoldMines;
                for (int i = 0; i < visible.Count; i++)
                    if (visible[i].RuntimeId.CompareTo(item.Value) == 0)
                    {
                        unavailable = visible[i].State is GoldMineState.Depleted
                            or GoldMineState.Destroyed;
                        break;
                    }
                if ((cancelTravel || unavailable || hasActiveMine
                        && item.Value.CompareTo(activeMine) != 0)
                    && station != null && station.IsPending)
                    station.CancelPending();
                if (station == null || !station.IsPending)
                    removals.Add(worker);
            }
            for (int i = 0; i < removals.Count; i++)
            {
                UnitController worker = removals[i];
                pending.Remove(worker);
                roles.Release(worker, CommanderPawnRole.Mine);
            }
        }

        private static UnitController ChooseWorker(FactionKnowledge knowledge,
            CommanderPawnRoles roles)
        {
            UnitController best = null;
            int bestPriority = int.MaxValue;
            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (unit == null || !unit.IsAlive
                    || roles.GetRole(unit) != CommanderPawnRole.Economy)
                    continue;
                PawnGatherer gatherer = unit.GetComponent<PawnGatherer>();
                if (gatherer == null || gatherer.CarriedAmount > 0
                    || unit.GetComponent<PawnBuilder>()?.HasJob == true
                    || unit.GetComponent<PawnScoutPromotion>()?.IsPromoted == true)
                    continue;
                int priority = gatherer.State == WorkerState.Idle ? 0
                    : gatherer.Target != null
                        && gatherer.Target.ResourceType == ResourceType.Gold ? 1 : 2;
                if (priority >= bestPriority) continue;
                best = unit;
                bestPriority = priority;
            }
            return best;
        }

        private static bool IsVisible(FactionKnowledge knowledge, EntityId id)
        {
            IReadOnlyList<GoldMineKnowledgeSnapshot> mines = knowledge.VisibleGoldMines;
            for (int i = 0; i < mines.Count; i++)
                if (mines[i].RuntimeId.CompareTo(id) == 0) return true;
            return false;
        }

        private static GoldMineObjective ResolveVisibleMine(FactionKnowledge knowledge,
            EntityId id)
        {
            if (!IsVisible(knowledge, id)) return null;
            IReadOnlyList<GoldMineObjective> mines = GoldMineObjective.Active;
            for (int i = 0; i < mines.Count; i++)
                if (mines[i] != null
                    && mines[i].GetEntityId().CompareTo(id) == 0)
                    return mines[i];
            return null;
        }
    }
}
