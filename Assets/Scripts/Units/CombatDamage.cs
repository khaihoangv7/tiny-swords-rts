using System;

namespace TinyKingdom.Units
{
    public static class CombatDamage
    {
        public static float Multiplier(UnitType attacker, UnitType defender)
        {
            if (attacker == UnitType.Warrior && defender == UnitType.Archer) return 1.25f;
            if (attacker == UnitType.Archer && defender == UnitType.Warrior) return 0.8f;
            if (attacker == UnitType.Archer && defender == UnitType.Lancer) return 1.25f;
            if (attacker == UnitType.Lancer && defender == UnitType.Archer) return 0.8f;
            if (attacker == UnitType.Lancer && defender == UnitType.Warrior) return 1.5f;
            return 1f;
        }

        public static int Calculate(int baseDamage, UnitType attacker, UnitType defender)
            => Calculate(baseDamage, attacker, defender, AttackDelivery.Melee, false);

        public static int Calculate(int baseDamage, UnitType attacker, UnitType defender,
            AttackDelivery delivery, bool guarding)
        {
            if (baseDamage <= 0) return 0;
            double guard = guarding
                ? (delivery == AttackDelivery.Projectile ? 0.50d : 0.75d) : 1d;
            // Gameplay HP is integral; round positive half values up consistently.
            return (int)Math.Floor(baseDamage * (double)Multiplier(attacker, defender) * guard + 0.5d);
        }
    }
}
