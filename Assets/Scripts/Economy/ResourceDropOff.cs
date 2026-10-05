using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Economy
{
    public interface IResourceDropOff
    {
        Faction Faction { get; }
        bool IsAvailable { get; }
        float ArrivalTolerance { get; }
        Vector3 WorldPosition { get; }
        EntityId RuntimeId { get; }

        bool TryReserve(UnitController worker, GridManager navigation,
            PathfindingSystem paths, IPathTraversalPolicy traversalPolicy,
            out Vector3 position);
        void Release(UnitController worker);
        bool Deposit(ResourceType type, int amount);
    }

    public static class ResourceDropOffRegistry
    {
        private static readonly List<IResourceDropOff> registered = new();

        public static IReadOnlyList<IResourceDropOff> Active => registered;

        public static void Register(IResourceDropOff dropOff)
        {
            if (dropOff != null && !registered.Contains(dropOff)) registered.Add(dropOff);
        }

        public static void Unregister(IResourceDropOff dropOff)
        {
            if (dropOff != null) registered.Remove(dropOff);
        }

        public static bool IsUsable(IResourceDropOff dropOff, Faction faction)
        {
            if (dropOff == null) return false;
            if (dropOff is Object unityObject && unityObject == null) return false;
            return dropOff.Faction == faction && dropOff.IsAvailable;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => registered.Clear();
    }
}
