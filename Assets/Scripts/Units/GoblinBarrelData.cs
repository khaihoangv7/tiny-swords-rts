using UnityEngine;

namespace TinyKingdom.Units
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/Goblin Barrel Data")]
    public sealed class GoblinBarrelData : ScriptableObject
    {
        [Min(0.1f)] public float detectionRadius = 6f;
        [Min(0.1f)] public float homeLeash = 7f;
        [Min(0.1f)] public float fuseDistance = 1.1f;
        [Min(0.01f)] public float fuseSeconds = 0.3f;
        [Min(0.1f)] public float explosionRadius = 1.75f;
        [Min(0f)] public float knockback = 0.6f;
        [Min(0.01f)] public float emergeSeconds = 0.6f;
        [Min(0.01f)] public float withdrawSeconds = 0.6f;
        [Min(0.01f)] public float explosionScale = 0.8f;
        public Sprite[] emerge = System.Array.Empty<Sprite>();
        public Sprite[] withdraw = System.Array.Empty<Sprite>();
    }
}
