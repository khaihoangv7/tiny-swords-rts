using UnityEngine;

namespace TinyKingdom.Presentation
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/Building Presentation Catalog")]
    public sealed class BuildingPresentationCatalog : ScriptableObject
    {
        [SerializeField] private Sprite[] fire01 = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] fire02 = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] fire03 = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] explosion01 = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] explosion02 = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite bigBarLeft;
        [SerializeField] private Sprite bigBarMiddle;
        [SerializeField] private Sprite bigBarRight;
        [SerializeField] private Sprite bigBarFill;

        public Sprite[] Fire01 => fire01;
        public Sprite[] Fire02 => fire02;
        public Sprite[] Fire03 => fire03;
        public Sprite[] Explosion01 => explosion01;
        public Sprite[] Explosion02 => explosion02;
        public Sprite BigBarLeft => bigBarLeft;
        public Sprite BigBarMiddle => bigBarMiddle;
        public Sprite BigBarRight => bigBarRight;
        public Sprite BigBarFill => bigBarFill;

        public static BuildingPresentationCatalog Load()
            => Resources.Load<BuildingPresentationCatalog>(
                "Presentation/Phase6B2BuildingPresentation");

#if UNITY_EDITOR
        public void SetData(Sprite[] fire1, Sprite[] fire2, Sprite[] fire3,
            Sprite[] explosion1, Sprite[] explosion2, Sprite barLeft,
            Sprite barMiddle, Sprite barRight, Sprite barFill)
        {
            fire01 = fire1;
            fire02 = fire2;
            fire03 = fire3;
            explosion01 = explosion1;
            explosion02 = explosion2;
            bigBarLeft = barLeft;
            bigBarMiddle = barMiddle;
            bigBarRight = barRight;
            bigBarFill = barFill;
        }
#endif
    }
}
