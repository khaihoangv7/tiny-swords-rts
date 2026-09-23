using System;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public interface IDefensiveBuildingHost : IProductionHost
    {
        BuildingData DefenseData { get; }
        Transform HostTransform { get; }
        Collider2D InteractionCollider { get; }
        float CurrentHP { get; }
        int MaxHP { get; }
        bool IsAlive { get; }
        bool IsDefenseReady { get; }
        event Action DefenseHostDestroyed;
        event Action DefenseHostHealthChanged;
        bool ReceiveBuildingDamage(int damage);
        void ReleaseOccupancy();
    }
}
