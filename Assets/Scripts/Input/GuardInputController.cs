using System.Collections.Generic;
using TinyKingdom.Units;
using TinyKingdom.Match;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinyKingdom.Input
{
    [DefaultExecutionOrder(-100)]
    public sealed class GuardInputController : MonoBehaviour
    {
        [SerializeField] private SelectionManager selectionManager;
        private readonly List<UnitGuard> keyboardGuards = new();
        private readonly List<UnitGuard> mouseGuards = new();
        private bool mouseHeld;

        private void Awake()
        {
            if (selectionManager == null) selectionManager = GetComponent<SelectionManager>();
        }

        public void Configure(SelectionManager selection) => selectionManager = selection;

        private void Update()
        {
            if (!MatchManager.AllowsGameplay || GameInputBindings.ModalOpen)
            {
                End(keyboardGuards, mouseGuards);
                EndMouseGuard();
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (GameInputBindings.WasPressed(GameAction.Guard))
                    Begin(keyboardGuards, mouseGuards);
                if (GameInputBindings.WasReleased(GameAction.Guard))
                    End(keyboardGuards, mouseGuards);
            }
            // Pointer-up outside the button still ends the hold.
            if (mouseHeld && Mouse.current != null && !Mouse.current.leftButton.isPressed)
                EndMouseGuard();
        }

        private void OnDisable()
        {
            End(keyboardGuards, mouseGuards);
            EndMouseGuard();
        }

        public void BeginMouseGuard()
        {
            if (!MatchManager.AllowsGameplay || mouseHeld) return;
            mouseHeld = true;
            Begin(mouseGuards, keyboardGuards);
        }

        public void EndMouseGuard()
        {
            mouseHeld = false;
            End(mouseGuards, keyboardGuards);
        }

        private void Begin(List<UnitGuard> held, List<UnitGuard> other)
        {
            End(held, other);
            if (selectionManager == null) return;
            foreach (UnitSelection selection in selectionManager.SelectedUnits)
            {
                if (selection == null) continue;
                UnitGuard guard = selection.GetComponent<UnitGuard>();
                if (guard != null && guard.BeginGuard()) held.Add(guard);
            }
        }

        private static void End(List<UnitGuard> held, List<UnitGuard> other)
        {
            foreach (UnitGuard guard in held)
                if (guard != null && !other.Contains(guard)) guard.EndGuard();
            held.Clear();
        }
    }
}
