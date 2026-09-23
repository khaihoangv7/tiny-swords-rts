using System;
using UnityEngine;

namespace TinyKingdom.Units
{
    public interface ICombatDamageReceiver
    {
        Faction Faction { get; }
        bool IsAlive { get; }
        bool IsTargetable { get; }
        Transform TargetTransform { get; }
        event Action<ICombatDamageReceiver> CombatDied;
        bool ReceiveAttack(int baseDamage, UnitType attacker, AttackDelivery delivery,
            float sourceMultiplier = 1f, bool applyCounterMultiplier = true,
            UnitController damageSource = null);
    }
}
