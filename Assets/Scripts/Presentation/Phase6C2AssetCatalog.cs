using UnityEngine;
using TinyKingdom.Units;

namespace TinyKingdom.Presentation
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/Phase 6C2 Asset Catalog")]
    public sealed class Phase6C2AssetCatalog : ScriptableObject
    {
        [SerializeField] private UnitAnimationSet blueMonk;
        [SerializeField] private UnitAnimationSet redMonk;
        [SerializeField] private UnitAnimationSet goblinTorch;
        [SerializeField] private UnitAnimationSet goblinTnt;
        [SerializeField] private Sprite[] blueHealEffect = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] redHealEffect = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] dynamite = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] explosion03 = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite goldMineInactive;
        [SerializeField] private Sprite goldMineActive;
        [SerializeField] private Sprite goldMineDestroyed;

        public UnitAnimationSet Monk(Faction faction) => faction == Faction.Red ? redMonk : blueMonk;
        public UnitAnimationSet GoblinTorch => goblinTorch;
        public UnitAnimationSet GoblinTnt => goblinTnt;
        public Sprite[] HealEffect(Faction faction)
            => faction == Faction.Red ? redHealEffect : blueHealEffect;
        public Sprite[] Dynamite => dynamite;
        public Sprite[] Explosion03 => explosion03;
        public Sprite GoldMineInactive => goldMineInactive;
        public Sprite GoldMineActive => goldMineActive;
        public Sprite GoldMineDestroyed => goldMineDestroyed;

        public static Phase6C2AssetCatalog Load()
            => Resources.Load<Phase6C2AssetCatalog>("Presentation/Phase6C2Assets");

#if UNITY_EDITOR
        public void Set(UnitAnimationSet blue, UnitAnimationSet red,
            UnitAnimationSet torch, UnitAnimationSet tnt, Sprite[] blueHeal, Sprite[] redHeal,
            Sprite[] thrown,
            Sprite[] explosion, Sprite inactive, Sprite active, Sprite destroyed)
        {
            blueMonk = blue; redMonk = red; goblinTorch = torch; goblinTnt = tnt;
            blueHealEffect = blueHeal; redHealEffect = redHeal;
            dynamite = thrown; explosion03 = explosion;
            goldMineInactive = inactive; goldMineActive = active; goldMineDestroyed = destroyed;
        }

        public void SetNonGoblin(UnitAnimationSet blue, UnitAnimationSet red,
            Sprite[] blueHeal, Sprite[] redHeal,
            Sprite inactive, Sprite active, Sprite destroyed)
        {
            blueMonk = blue;
            redMonk = red;
            blueHealEffect = blueHeal;
            redHealEffect = redHeal;
            goldMineInactive = inactive;
            goldMineActive = active;
            goldMineDestroyed = destroyed;
        }
#endif
    }

    public sealed class OneShotSpriteVfx : MonoBehaviour
    {
        private Sprite[] frames;
        private SpriteRenderer visual;
        private float elapsed;
        private float fps;
        private Faction observedOwner;

        public static void Spawn(string objectName, Vector3 position, Sprite[] source,
            float framesPerSecond, float scale, int sortingOrder, Faction owner)
        {
            if (source == null || source.Length == 0) return;
            var obj = new GameObject(objectName);
            obj.transform.position = new Vector3(position.x, position.y, -0.15f);
            obj.transform.localScale = Vector3.one * scale;
            OneShotSpriteVfx player = obj.AddComponent<OneShotSpriteVfx>();
            player.frames = source;
            player.fps = Mathf.Max(1f, framesPerSecond);
            player.observedOwner = owner;
            player.visual = obj.AddComponent<SpriteRenderer>();
            player.visual.sortingOrder = sortingOrder;
            player.visual.sprite = source[0];
            player.visual.enabled = Vision.FogWorldVisibility.Instance == null
                || Vision.FogWorldVisibility.Instance.ShouldPresent(owner, position);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(elapsed * fps);
            if (frame >= frames.Length) { Destroy(gameObject); return; }
            visual.sprite = frames[frame];
            visual.enabled = Vision.FogWorldVisibility.Instance == null
                || Vision.FogWorldVisibility.Instance.ShouldPresent(
                    observedOwner, transform.position);
        }
    }
}
