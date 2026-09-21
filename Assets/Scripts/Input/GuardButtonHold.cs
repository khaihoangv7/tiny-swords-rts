using UnityEngine;
using UnityEngine.EventSystems;

namespace TinyKingdom.Input
{
    public sealed class GuardButtonHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private GuardInputController commands;

        public void Configure(GuardInputController input) => commands = input;
        public void OnPointerDown(PointerEventData data) => commands?.BeginMouseGuard();
        public void OnPointerUp(PointerEventData data) => commands?.EndMouseGuard();
        private void OnDisable() => commands?.EndMouseGuard();
    }
}
