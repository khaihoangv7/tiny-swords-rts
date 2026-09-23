using UnityEngine;

namespace TinyKingdom.Buildings
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/Building Data")]
    public sealed class BuildingData : ScriptableObject
    {
        [SerializeField] private BuildingType buildingType = BuildingType.House;
        [SerializeField, Min(1)] private int maxHP = 150;
        [SerializeField, Min(0)] private int foodCost = 0;
        [SerializeField, Min(0)] private int woodCost = 0;
        [SerializeField, Min(0)] private int goldCost = 0;
        [SerializeField] private Vector2Int footprint = new(2, 2);
        [SerializeField, Min(0.1f)] private float constructionSeconds = 5f;
        [SerializeField, Min(0f)] private float repairHPPerSecond = 20f;
        [SerializeField, Min(0)] private int populationBonus = 0;
        [SerializeField] private Sprite blueSprite = null;
        [SerializeField] private Sprite redSprite = null;
        [SerializeField] private Sprite[] blueHouseVariants = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] redHouseVariants = System.Array.Empty<Sprite>();
        [SerializeField] private Vector2 visualOffset = Vector2.zero;
        [SerializeField, Min(0f)] private float visionRadius = 5f;
        [Header("Phase 5C2 defense (provisional where noted)")]
        [SerializeField, Min(0)] private int internalGarrisonCapacity = 0;
        [SerializeField, Min(0)] private int mountedArcherCapacity = 0;
        [SerializeField, Min(0)] private int defensiveArrowDamage = 5;
        [SerializeField, Min(0.1f)] private float defensiveVolleyInterval = 1.5f;
        [SerializeField, Min(0f)] private float defensiveAttackRange = 0f;
        [SerializeField, Min(1f)] private float mountedDamageMultiplier = 1.2f;
        [SerializeField, Min(1f)] private float mountedRangeMultiplier = 1.25f;
        [SerializeField] private Vector2 defensiveProjectileOrigin = Vector2.zero;
        [SerializeField] private Vector2[] blueRooftopAnchors = System.Array.Empty<Vector2>();
        [SerializeField] private Vector2[] redRooftopAnchors = System.Array.Empty<Vector2>();

        public BuildingType Type => buildingType;
        public int MaxHP => maxHP;
        public int FoodCost => foodCost;
        public int WoodCost => woodCost;
        public int GoldCost => goldCost;
        public Vector2Int Footprint => footprint;
        public float ConstructionSeconds => constructionSeconds;
        public float RepairHPPerSecond => repairHPPerSecond;
        public int PopulationBonus => populationBonus;
        public Vector2 VisualOffset => visualOffset;
        public float VisionRadius => visionRadius;
        public int InternalGarrisonCapacity => internalGarrisonCapacity;
        public int MountedArcherCapacity => mountedArcherCapacity;
        public int DefensiveArrowDamage => defensiveArrowDamage;
        public float DefensiveVolleyInterval => defensiveVolleyInterval;
        public float DefensiveAttackRange => defensiveAttackRange;
        public float MountedDamageMultiplier => mountedDamageMultiplier;
        public float MountedRangeMultiplier => mountedRangeMultiplier;
        public Vector2 DefensiveProjectileOrigin => defensiveProjectileOrigin;
        public Vector2[] RooftopAnchors(TinyKingdom.Units.Faction faction)
            => faction == TinyKingdom.Units.Faction.Blue
                ? blueRooftopAnchors : redRooftopAnchors;
        public Sprite GetSprite(TinyKingdom.Units.Faction faction)
            => faction == TinyKingdom.Units.Faction.Blue ? blueSprite : redSprite;
        public Sprite GetSprite(TinyKingdom.Units.Faction faction, HouseVariant variant)
        {
            if (buildingType != BuildingType.House) return GetSprite(faction);
            Sprite[] variants = faction == TinyKingdom.Units.Faction.Blue
                ? blueHouseVariants : redHouseVariants;
            int index = (int)variant;
            return index >= 0 && index < variants.Length && variants[index] != null
                ? variants[index] : index == 0 ? GetSprite(faction) : null;
        }
    }
}
