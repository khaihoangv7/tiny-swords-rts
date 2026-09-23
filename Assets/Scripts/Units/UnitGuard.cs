using UnityEngine;
using TinyKingdom.Match;

namespace TinyKingdom.Units
{
    [RequireComponent(typeof(UnitController), typeof(UnitMovement), typeof(CombatController))]
    public sealed class UnitGuard : MonoBehaviour
    {
        private UnitController controller;
        private UnitMovement movement;
        private CombatController combat;

        public bool IsGuarding => controller != null && controller.CurrentState == UnitState.Guarding;

        private void Awake()
        {
            controller = GetComponent<UnitController>();
            movement = GetComponent<UnitMovement>();
            combat = GetComponent<CombatController>();
        }

        public bool BeginGuard()
        {
            if (!MatchManager.AllowsGameplay) return false;
            if (controller == null) Awake();
            UnitStationing stationing = GetComponent<UnitStationing>();
            if (stationing?.IsStationed == true) return false;
            stationing?.CancelPending();
            if (!controller.IsAlive || GetComponent<HealthComponent>()?.CombatData?.UnitType != UnitType.Warrior)
                return false;
            if (IsGuarding) return true;
            combat.StopCombat();
            movement.Stop();
            controller.SetState(UnitState.Guarding);
            return true;
        }

        public void EndGuard()
        {
            if (IsGuarding) controller.SetState(UnitState.Idle);
        }
    }
}
