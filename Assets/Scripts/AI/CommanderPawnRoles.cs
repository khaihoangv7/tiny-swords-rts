using System;
using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Units;

namespace TinyKingdom.AI
{
    public enum CommanderPawnRole
    {
        Economy,
        Scout,
        Builder
    }

    [Serializable]
    public sealed class CommanderPawnRoles
    {
        private readonly Dictionary<UnitController, CommanderPawnRole> roles = new();
        private readonly List<UnitController> removals = new();

        public int EconomyCount { get; private set; }
        public int ScoutCount { get; private set; }
        public int BuilderCount { get; private set; }

        public void Reset()
        {
            roles.Clear();
            removals.Clear();
            EconomyCount = ScoutCount = BuilderCount = 0;
        }

        public void Synchronize(FactionKnowledge knowledge)
        {
            removals.Clear();
            foreach (KeyValuePair<UnitController, CommanderPawnRole> item in roles)
                if (!IsPawn(item.Key, knowledge.Faction)) removals.Add(item.Key);
            for (int i = 0; i < removals.Count; i++) roles.Remove(removals[i]);

            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController pawn = units[i];
                if (!IsPawn(pawn, knowledge.Faction)) continue;
                PawnBuilder builder = pawn.GetComponent<PawnBuilder>();
                if (builder != null && builder.HasJob)
                    roles[pawn] = CommanderPawnRole.Builder;
                else if (!roles.TryGetValue(pawn, out CommanderPawnRole role)
                    || role == CommanderPawnRole.Builder)
                    roles[pawn] = CommanderPawnRole.Economy;
            }
            RefreshCounts();
        }

        public CommanderPawnRole GetRole(UnitController pawn)
            => pawn != null && roles.TryGetValue(pawn, out CommanderPawnRole role)
                ? role : CommanderPawnRole.Economy;

        public bool IsRole(UnitController pawn, CommanderPawnRole role)
            => pawn != null && roles.TryGetValue(pawn, out CommanderPawnRole current)
                && current == role;

        public bool TryAssign(UnitController pawn, CommanderPawnRole role)
        {
            if (pawn == null || pawn.GetComponent<PawnGatherer>() == null) return false;
            roles[pawn] = role;
            RefreshCounts();
            return true;
        }

        public void Release(UnitController pawn, CommanderPawnRole role)
        {
            if (pawn == null || !roles.TryGetValue(pawn, out CommanderPawnRole current)
                || current != role) return;
            roles[pawn] = CommanderPawnRole.Economy;
            RefreshCounts();
        }

        private void RefreshCounts()
        {
            EconomyCount = ScoutCount = BuilderCount = 0;
            foreach (CommanderPawnRole role in roles.Values)
            {
                switch (role)
                {
                    case CommanderPawnRole.Economy: EconomyCount++; break;
                    case CommanderPawnRole.Scout: ScoutCount++; break;
                    case CommanderPawnRole.Builder: BuilderCount++; break;
                }
            }
        }

        private static bool IsPawn(UnitController unit, Faction faction)
            => unit != null && unit.IsAlive && unit.IsWorldParticipant
                && unit.Faction == faction && unit.GetComponent<PawnGatherer>() != null;
    }
}
