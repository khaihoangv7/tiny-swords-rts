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
        [SerializeField, Min(0f)] private float rangedReleaseDelay = 0f;
        [SerializeField] private bool commitsMeleeAttack = false;
        [SerializeField, Min(0f)] private float committedMeleeHitDelay = 0f;
        [SerializeField, Min(0f)] private float committedMeleeDuration = 0f;
        [SerializeField, Min(0.01f)] private float moveSpeed = 3f;
        [SerializeField, Min(0)] private int foodCost = 100;
        [SerializeField, Min(0)] private int woodCost = 0;
        [SerializeField, Min(0)] private int goldCost = 80;
        [SerializeField, Min(0.01f)] private float projectileSpeed = 8f;
        [SerializeField, Min(0.1f)] private float acquisitionRadius = 4f;
        [SerializeField, Min(0.05f)] private float acquisitionInterval = 0.25f;
        [SerializeField, Min(0.1f)] private float autoChaseLimit = 6f;
        [SerializeField] private bool autoAcquireTargets = true;
        [SerializeField] private bool canRetaliateWhenDamaged = false;
        [SerializeField, Min(0.1f)] private float retaliationLeash = 10f;
        [SerializeField, Min(1)] private int populationCost = 1;
        [SerializeField, Min(0.1f)] private float trainingSeconds = 6f;
        [SerializeField, Min(0f)] private float visionRadius = 5f;

        public UnitType UnitType => unitType;
        public AttackDelivery AttackDelivery => attackDelivery;
        public int MaxHP => maxHP;
        public int BaseDamage => baseDamage;
        public float AttackRange => attackRange;
        public float AttackInterval => attackInterval;
        public float RangedReleaseDelay => rangedReleaseDelay;
        public bool CommitsMeleeAttack => commitsMeleeAttack
            && attackDelivery == AttackDelivery.Melee;
        public float CommittedMeleeHitDelay => committedMeleeHitDelay;
        public float CommittedMeleeDuration => Mathf.Max(committedMeleeHitDelay,
            committedMeleeDuration);
        public float MoveSpeed => moveSpeed;
        public int FoodCost => foodCost;
        public int WoodCost => woodCost;
        public int GoldCost => goldCost;
        public float ProjectileSpeed => projectileSpeed;
        public float AcquisitionRadius => acquisitionRadius;
        public float AcquisitionInterval => acquisitionInterval;
        public float AutoChaseLimit => autoChaseLimit;
        public bool AutoAcquireTargets => autoAcquireTargets;
        public bool CanRetaliateWhenDamaged => canRetaliateWhenDamaged
            && unitType is UnitType.Warrior or UnitType.Archer or UnitType.Lancer;
        public float RetaliationLeash => retaliationLeash;
        public int PopulationCost => populationCost;
        public float TrainingSeconds => trainingSeconds;
        public float VisionRadius => visionRadius;
    }
}
