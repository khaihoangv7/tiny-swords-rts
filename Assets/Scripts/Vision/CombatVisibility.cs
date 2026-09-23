using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Vision
{
    public enum CombatAwarenessReason
    {
        TeamVision = 0,
        Retaliation = 1
    }

    // One authoritative gameplay query for combat awareness. Presentation state
    // and rendered fog opacity never participate in these decisions.
    public static class CombatVisibility
    {
        public static bool CanFactionCurrentlyObserveTarget(Faction observerFaction,
            ICombatDamageReceiver target)
        {
            if (!IsUsable(target)) return false;
            return CanFactionCurrentlyObservePosition(observerFaction,
                target.TargetTransform.position);
        }

        public static bool CanFactionCurrentlyObservePosition(Faction observerFaction,
            Vector3 worldPosition)
        {
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            return vision != null && vision.IsReady
                && vision.IsVisible(observerFaction, worldPosition);
        }

        public static bool CanContinueTarget(Faction observerFaction,
            ICombatDamageReceiver target, CombatAwarenessReason reason)
        {
            if (!IsUsable(target)) return false;
            return reason == CombatAwarenessReason.Retaliation
                || CanFactionCurrentlyObserveTarget(observerFaction, target);
        }

        private static bool IsUsable(ICombatDamageReceiver target)
        {
            if (target == null || !target.IsAlive || !target.IsTargetable
                || target.TargetTransform == null) return false;
            return target is not Object unityObject || unityObject != null;
        }
    }
}
