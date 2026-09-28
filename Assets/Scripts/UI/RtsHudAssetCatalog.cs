using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.UI
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/UI/RTS HUD Asset Catalog")]
    public sealed class RtsHudAssetCatalog : ScriptableObject
    {
        [SerializeField] private Sprite[] woodTable = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] specialPaper = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite commandSlot;
        [SerializeField] private Sprite populationIcon;
        [SerializeField] private Sprite woodIcon;
        [SerializeField] private Sprite goldIcon;
        [SerializeField] private Sprite foodIcon;
        [SerializeField] private Sprite attackMoveIcon;
        [SerializeField] private Sprite ejectAllIcon;
        [SerializeField] private Sprite guardIcon;
        [SerializeField] private Sprite warriorAvatar;
        [SerializeField] private Sprite lancerAvatar;
        [SerializeField] private Sprite archerAvatar;
        [SerializeField] private Sprite monkAvatar;
        [SerializeField] private Sprite pawnAvatar;

        public Sprite[] WoodTable => woodTable;
        public Sprite[] SpecialPaper => specialPaper;
        public Sprite CommandSlot => commandSlot;
        public Sprite PopulationIcon => populationIcon;
        public Sprite WoodIcon => woodIcon;
        public Sprite GoldIcon => goldIcon;
        public Sprite FoodIcon => foodIcon;
        public Sprite AttackMoveIcon => attackMoveIcon;
        public Sprite EjectAllIcon => ejectAllIcon;
        public Sprite GuardIcon => guardIcon;
        public bool IsComplete => woodTable.Length == 9 && specialPaper.Length == 9
            && commandSlot != null && populationIcon != null && woodIcon != null
            && goldIcon != null && foodIcon != null && attackMoveIcon != null
            && ejectAllIcon != null
            && guardIcon != null && warriorAvatar != null && lancerAvatar != null
            && archerAvatar != null && monkAvatar != null && pawnAvatar != null;

        public Sprite Avatar(UnitType type) => type switch
        {
            UnitType.Warrior => warriorAvatar,
            UnitType.Lancer => lancerAvatar,
            UnitType.Archer => archerAvatar,
            UnitType.Monk => monkAvatar,
            UnitType.Pawn => pawnAvatar,
            _ => null
        };

        public static RtsHudAssetCatalog Load()
            => Resources.Load<RtsHudAssetCatalog>("Presentation/RtsHudAssets");

#if UNITY_EDITOR
        public void Set(Sprite[] wood, Sprite[] paper, Sprite slot,
            Sprite population, Sprite woodResource, Sprite goldResource,
            Sprite foodResource, Sprite attackMove, Sprite ejectAll, Sprite guard,
            Sprite warrior, Sprite lancer, Sprite archer, Sprite monk, Sprite pawn)
        {
            woodTable = wood;
            specialPaper = paper;
            commandSlot = slot;
            populationIcon = population;
            woodIcon = woodResource;
            goldIcon = goldResource;
            foodIcon = foodResource;
            attackMoveIcon = attackMove;
            ejectAllIcon = ejectAll;
            guardIcon = guard;
            warriorAvatar = warrior;
            lancerAvatar = lancer;
            archerAvatar = archer;
            monkAvatar = monk;
            pawnAvatar = pawn;
        }
#endif
    }
}
