using System;
using System.Collections.Generic;
using TinyKingdom.Units;

namespace TinyKingdom.AI
{
    public enum ArmyGroupState
    {
        Forming,
        Ready,
        PreparingAttack,
        Advancing,
        Assaulting,
        Defending,
        Regrouping
    }

    [Serializable]
    public sealed class ArmyGroup
    {
        private readonly List<UnitController> members = new();
        private readonly List<UnitController> removals = new();

        public IReadOnlyList<UnitController> Members => members;
        public ArmyGroupState State { get; private set; } = ArmyGroupState.Forming;
        public int MembershipVersion { get; private set; }

        public void Synchronize(FactionKnowledge knowledge, UnitController activeScout)
        {
            removals.Clear();
            for (int i = 0; i < members.Count; i++)
                if (!IsEligible(members[i], knowledge.Faction, activeScout))
                    removals.Add(members[i]);
            bool changed = removals.Count > 0;
            for (int i = 0; i < removals.Count; i++) members.Remove(removals[i]);

            IReadOnlyList<UnitController> units = knowledge.OwnUnits;
            for (int i = 0; i < units.Count; i++)
            {
                UnitController unit = units[i];
                if (!IsEligible(unit, knowledge.Faction, activeScout)
                    || members.Contains(unit)) continue;
                members.Add(unit);
                changed = true;
            }
            if (!changed) return;
            members.Sort(CompareMembers);
            MembershipVersion++;
        }

        public void SetState(ArmyGroupState value) => State = value;

        public void Reset()
        {
            members.Clear();
            removals.Clear();
            State = ArmyGroupState.Forming;
            MembershipVersion = 0;
        }

        public static bool IsEligible(UnitController unit, Faction owner,
            UnitController activeScout)
        {
            if (unit == null || unit == activeScout || !unit.IsAlive
                || !unit.IsWorldParticipant || unit.Faction != owner) return false;
            UnitStationing stationing = unit.GetComponent<UnitStationing>();
            if (stationing != null && stationing.IsStationed) return false;
            UnitType? type = unit.GetComponent<HealthComponent>()?.CombatData?.UnitType;
            return type is UnitType.Warrior or UnitType.Archer
                or UnitType.Lancer or UnitType.Monk;
        }

        private static int CompareMembers(UnitController first, UnitController second)
            => first.GetEntityId().CompareTo(second.GetEntityId());
    }
}
