using System;
using UnityEngine;

namespace TinyKingdom.Units
{
    public sealed class UnitController : MonoBehaviour
    {
        [SerializeField] private Faction faction = Faction.Blue;
        [SerializeField] private UnitState currentState = UnitState.Idle;

        public event Action<UnitState> StateChanged;

        public Faction Faction => faction;
        public UnitState CurrentState => currentState;
        public bool IsAlive => currentState != UnitState.Dead;

        public void Configure(Faction owner)
        {
            faction = owner;
            SetState(UnitState.Idle);
        }

        public void SetState(UnitState newState)
        {
            if (currentState == UnitState.Dead)
                return;
            if (currentState == newState)
                return;

            currentState = newState;
            StateChanged?.Invoke(currentState);
        }

        public void MarkDead() => SetState(UnitState.Dead);
    }
}
