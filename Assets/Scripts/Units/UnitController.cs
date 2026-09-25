using System;
using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Units
{
    public sealed class UnitController : MonoBehaviour, IVisionSource
    {
        private static readonly List<UnitController> RegisteredUnits = new();
        private static readonly List<UnitController> RegisteredWorldUnits = new();
        [SerializeField] private Faction faction = Faction.Blue;
        [SerializeField] private UnitState currentState = UnitState.Idle;

        public event Action<UnitState> StateChanged;

        public Faction Faction => faction;
        public static IReadOnlyList<UnitController> ActiveUnits => RegisteredWorldUnits;
        public static IReadOnlyList<UnitController> AllUnits => RegisteredUnits;
        public UnitState CurrentState => currentState;
        public bool IsAlive => currentState != UnitState.Dead;
        public bool IsWorldParticipant { get; private set; } = true;
        public bool IsProtected { get; private set; }
        public bool IsTargetable => IsAlive && IsWorldParticipant && !IsProtected;
        public bool IsVisionActive => FactionRelations.IsPlayable(faction)
            && IsAlive && IsWorldParticipant;
        public float VisionRadius => GetComponent<HealthComponent>()?.CombatData?.VisionRadius ?? 0f;
        public Vector3 VisionPosition => transform.position;

        private void OnEnable()
        {
            if (IsAlive && !RegisteredUnits.Contains(this)) RegisteredUnits.Add(this);
            if (IsAlive && IsWorldParticipant && !RegisteredWorldUnits.Contains(this))
                RegisteredWorldUnits.Add(this);
            VisionSourceRegistry.Register(this);
        }

        private void OnDisable()
        {
            RegisteredUnits.Remove(this);
            RegisteredWorldUnits.Remove(this);
            VisionSourceRegistry.Unregister(this);
        }

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

        public void MarkDead()
        {
            SetState(UnitState.Dead);
            RegisteredUnits.Remove(this);
            RegisteredWorldUnits.Remove(this);
            VisionSourceRegistry.Unregister(this);
        }

        public void SetWorldParticipation(bool participates, bool protectedFromDamage)
        {
            IsWorldParticipant = participates;
            IsProtected = protectedFromDamage;
            if (!isActiveAndEnabled || !IsAlive) return;
            if (participates)
            {
                if (!RegisteredWorldUnits.Contains(this)) RegisteredWorldUnits.Add(this);
            }
            else RegisteredWorldUnits.Remove(this);
        }
    }
}
