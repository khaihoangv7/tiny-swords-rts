using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Vision
{
    public static class TeamVisionBootstrap
    {
        public static TeamVisionSystem Ensure(Transform mapRoot, GridManager grid,
            Faction playerViewFaction)
        {
            TeamVisionSystem existing = Object.FindAnyObjectByType<TeamVisionSystem>();
            if (existing != null) return existing;
            var root = new GameObject("FogOfWar");
            root.transform.SetParent(mapRoot, false);
            TeamVisionSystem vision = root.AddComponent<TeamVisionSystem>();
            vision.Configure(grid, 0.10f);
            root.AddComponent<FogRenderer>().Configure(vision, grid, playerViewFaction);
            root.AddComponent<FogMemorySystem>().Configure(vision, playerViewFaction);
            root.AddComponent<FogWorldVisibility>().Configure(vision, playerViewFaction);
            return vision;
        }
    }
}
