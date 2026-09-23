using System.Collections.Generic;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using UnityEngine;

namespace TinyKingdom.Units
{
    public enum UnitStationingMode { World, Internal, Mounted }

    [RequireComponent(typeof(UnitController), typeof(UnitMovement), typeof(UnitSelection))]
    public sealed class UnitStationing : MonoBehaviour
    {
        private readonly List<(Renderer renderer, bool enabled)> renderers = new();
        private readonly List<(SpriteRenderer renderer, int order)> spriteOrders = new();
        private readonly List<(Collider2D collider, bool enabled)> colliders = new();
        private UnitController controller;
        private UnitMovement movement;
        private UnitSelection selection;
        private CombatController combat;
        private Transform originalParent;
        private BuildingGarrison pendingHost;

        public UnitStationingMode Mode { get; private set; } = UnitStationingMode.World;
        public BuildingGarrison Host { get; private set; }
        public bool IsStationed => Mode != UnitStationingMode.World;
        public bool IsPending => pendingHost != null;

        private void Awake() => Resolve();

        public void BeginPending(BuildingGarrison host) => pendingHost = host;

        public void ClearPending(BuildingGarrison host)
        {
            if (pendingHost == host) pendingHost = null;
        }

        public void CancelPending()
        {
            BuildingGarrison host = pendingHost;
            pendingHost = null;
            host?.CancelPending(GetComponent<UnitController>());
        }

        public bool EnterInternal(BuildingGarrison host)
        {
            if (!Prepare(host)) return false;
            // Prepare succeeded and no later entry step can reject. Castle cargo is
            // transferred now, after physical arrival but before the Pawn is hidden.
            host.DepositPawnCargoOnEntry(controller);
            Mode = UnitStationingMode.Internal;
            transform.SetParent(host.transform, true);
            foreach ((Renderer renderer, _) in renderers)
                if (renderer != null) renderer.enabled = false;
            combat?.SetStationedInternal(true);
            return true;
        }

        public bool EnterMounted(BuildingGarrison host, Transform anchor,
            float damageMultiplier, float rangeMultiplier)
        {
            if (anchor == null || !Prepare(host)) return false;
            Mode = UnitStationingMode.Mounted;
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            foreach ((SpriteRenderer renderer, _) in spriteOrders)
                if (renderer != null) renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 15);
            combat?.SetMountedContext(true, damageMultiplier, rangeMultiplier);
            return true;
        }

        public void RestoreToWorld(Vector3 position)
        {
            if (!IsStationed) return;
            Resolve();
            transform.SetParent(originalParent, true);
            transform.position = position;
            foreach ((Renderer renderer, bool enabled) in renderers)
                if (renderer != null) renderer.enabled = enabled;
            foreach ((Collider2D collider, bool enabled) in colliders)
                if (collider != null) collider.enabled = enabled;
            foreach ((SpriteRenderer renderer, int order) in spriteOrders)
                if (renderer != null) renderer.sortingOrder = order;
            renderers.Clear();
            colliders.Clear();
            spriteOrders.Clear();
            GetComponent<UnitHealthBar>()?.SetSuppressed(false);
            movement.SetSuspended(false);
            combat?.SetStationedInternal(false);
            combat?.SetMountedContext(false, 1f, 1f);
            controller.SetWorldParticipation(true, false);
            selection.SetWorldSelectable(true);
            selection.SetSelected(false);
            controller.SetState(UnitState.Idle);
            Host = null;
            Mode = UnitStationingMode.World;
        }

        private bool Prepare(BuildingGarrison host)
        {
            Resolve();
            if (host == null || Host != null || !controller.IsAlive) return false;
            pendingHost = null;
            originalParent = transform.parent;
            renderers.Clear();
            colliders.Clear();
            spriteOrders.Clear();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                renderers.Add((renderer, renderer.enabled));
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
                spriteOrders.Add((renderer, renderer.sortingOrder));
            foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
            {
                colliders.Add((collider, collider.enabled));
                collider.enabled = false;
            }
            GetComponent<PawnGatherer>()?.CancelJob();
            GetComponent<PawnBuilder>()?.CancelJob();
            GetComponent<UnitGuard>()?.EndGuard();
            combat?.StopCombat();
            movement.Stop();
            movement.SetSuspended(true);
            selection.SetWorldSelectable(false);
            GetComponent<UnitHealthBar>()?.SetSuppressed(true);
            controller.SetWorldParticipation(false, true);
            Host = host;
            return true;
        }

        private void Resolve()
        {
            controller ??= GetComponent<UnitController>();
            movement ??= GetComponent<UnitMovement>();
            selection ??= GetComponent<UnitSelection>();
            combat ??= GetComponent<CombatController>();
        }
    }
}
