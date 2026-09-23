using UnityEngine;

namespace TinyKingdom.Economy
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/Pawn Animation Set")]
    public sealed class PawnAnimationSet : ScriptableObject
    {
        [SerializeField] private Sprite[] idle = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] run = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] chop = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] mine = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] knife = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] idleAxe = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] idlePickaxe = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] idleKnife = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] idleWood = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] idleGold = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] idleFood = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] runWood = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] runGold = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] runFood = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] hammer = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] idleHammer = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] runHammer = System.Array.Empty<Sprite>();

        public Sprite[] Idle => idle;
        public Sprite[] Run => run;
        public Sprite[] Chop => chop;
        public Sprite[] Mine => mine;
        public Sprite[] Knife => knife;
        public Sprite[] IdleAxe => idleAxe;
        public Sprite[] IdlePickaxe => idlePickaxe;
        public Sprite[] IdleKnife => idleKnife;
        public Sprite[] IdleWood => idleWood;
        public Sprite[] IdleGold => idleGold;
        public Sprite[] IdleFood => idleFood;
        public Sprite[] RunWood => runWood;
        public Sprite[] RunGold => runGold;
        public Sprite[] RunFood => runFood;
        public Sprite[] Hammer => hammer;
        public Sprite[] IdleHammer => idleHammer;
        public Sprite[] RunHammer => runHammer;
    }
}
