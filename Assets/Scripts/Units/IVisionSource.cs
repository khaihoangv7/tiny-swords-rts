using System.Collections.Generic;
using UnityEngine;

namespace TinyKingdom.Units
{
    public interface IVisionSource
    {
        Faction Faction { get; }
        bool IsVisionActive { get; }
        float VisionRadius { get; }
        Vector3 VisionPosition { get; }
    }

    public static class VisionSourceRegistry
    {
        private static readonly List<IVisionSource> Sources = new();
        public static IReadOnlyList<IVisionSource> ActiveSources => Sources;

        public static void Register(IVisionSource source)
        {
            if (source != null && !Sources.Contains(source)) Sources.Add(source);
        }

        public static void Unregister(IVisionSource source)
        {
            if (source != null) Sources.Remove(source);
        }
    }
}
