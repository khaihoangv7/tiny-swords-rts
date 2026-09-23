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
            => Calculate(baseDamage, attacker, defender, delivery, guarding, 1f, true);

        public static int Calculate(int baseDamage, UnitType attacker, UnitType defender,
            AttackDelivery delivery, bool guarding, float sourceMultiplier,
            bool applyCounterMultiplier)
        {
            if (baseDamage <= 0) return 0;
            double counter = applyCounterMultiplier ? Multiplier(attacker, defender) : 1d;
            int counterAdjusted = (int)Math.Floor(baseDamage * counter + 0.5d);
            int modified = (int)Math.Floor(counterAdjusted
                * Math.Max(0d, sourceMultiplier) + 0.5d);
            double guard = guarding
                ? (delivery == AttackDelivery.Projectile ? 0.50d : 0.75d) : 1d;
            // Gameplay HP is integral; round positive half values up consistently.
            return (int)Math.Floor(modified * guard + 0.5d);
        }
    }
}
