using TinyKingdom.Units;
using TinyKingdom.Buildings;
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
            ICombatDamageReceiver target, bool allowVisibleBuildingEdge = false)
        {
            if (!IsUsable(target)) return false;
            return CanFactionCurrentlyObservePosition(observerFaction, target.TargetTransform.position)
                || allowVisibleBuildingEdge && target is BuildingCombatTarget building
                    && building.IsFootprintVisibleTo(observerFaction, TeamVisionSystem.Instance);
        }

        internal static bool IsBuildingFootprintVisible(Faction faction, TeamVisionSystem vision,
            Vector2Int origin, Vector2Int size)
        {
            if (vision == null || !vision.IsReady) return false;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                if (vision.GetVisionState(faction, origin + new Vector2Int(x, y)) == VisionState.Visible)
                    return true;
            return false;
        }

        public static bool CanFactionCurrentlyObservePosition(Faction observerFaction,
            Vector3 worldPosition)
        {
            if (observerFaction == Faction.Goblin) return true;
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
