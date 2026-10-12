using UnityEngine;

namespace TinyKingdom.Buildings
{
    [CreateAssetMenu(menuName="Tiny Kingdom/Goblin Hut Data")]
    public sealed class GoblinHutData : ScriptableObject
    {
        [Min(1)] public int maxHP=400;
        public Vector2Int footprint=new(2,2);
        [Min(0.1f)] public float visualScale=2f;
        public Sprite intact;
        public Sprite wreck;
    }
}
