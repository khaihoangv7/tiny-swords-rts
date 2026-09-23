using UnityEngine;

namespace TinyKingdom.Economy
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        // Phase 5A gather timing; animation playback is independent.
        [SerializeField, Min(0.01f)] private float gatherSecondsPerUnit = 1.5f;
        [SerializeField, Min(0f)] private float gatherImpactDelay = 0.45f;
        [SerializeField, Min(1)] private int amountPerStep = 1;
        [SerializeField, Min(1)] private int sheepCapacity = 60;
        [SerializeField, Min(1)] private int treeCapacity = 100;
        [SerializeField, Min(1)] private int goldCapacity = 100;
        [SerializeField] private int[] goldStoneCapacities = { 100, 150, 250, 300, 400, 500 };

        public float GatherSecondsPerUnit => gatherSecondsPerUnit;
        public float GatherImpactDelay => Mathf.Min(gatherImpactDelay, gatherSecondsPerUnit);
        public int AmountPerStep => amountPerStep;
        public int SheepCapacity => sheepCapacity;
        public int TreeCapacity => treeCapacity;
        public int GoldCapacity => goldCapacity;
        public int GetGoldStoneCapacity(int zeroBasedVariant)
            => goldStoneCapacities != null && zeroBasedVariant >= 0
                && zeroBasedVariant < goldStoneCapacities.Length
                ? Mathf.Max(1, goldStoneCapacities[zeroBasedVariant]) : goldCapacity;
    }
}
