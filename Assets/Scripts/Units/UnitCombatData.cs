using UnityEngine;

namespace TinyKingdom.Units
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/Unit Combat Data")]
    public sealed class UnitCombatData : ScriptableObject
    {
        [SerializeField] private UnitType unitType = UnitType.Warrior;
        [SerializeField] private AttackDelivery attackDelivery = AttackDelivery.Melee;
        [SerializeField, Min(1)] private int maxHP = 260;
        [SerializeField, Min(1)] private int baseDamage = 28;
        [SerializeField, Min(0.01f)] private float attackRange = 1f;
        [SerializeField, Min(0.01f)] private float attackInterval = 1.1f;
        [SerializeField, Min(0.01f)] private float moveSpeed = 3f;
        [SerializeField, Min(0)] private int foodCost = 100;
        [SerializeField, Min(0)] private int woodCost = 0;
        [SerializeField, Min(0)] private int goldCost = 80;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 8f;

        public UnitType UnitType => unitType;
        public AttackDelivery AttackDelivery => attackDelivery;
        public int MaxHP => maxHP;
        public int BaseDamage => baseDamage;
        public float AttackRange => attackRange;
        public float AttackInterval => attackInterval;
        public float MoveSpeed => moveSpeed;
        public int FoodCost => foodCost;
        public int WoodCost => woodCost;
        public int GoldCost => goldCost;
        public float ProjectileSpeed => projectileSpeed;
    }
}
