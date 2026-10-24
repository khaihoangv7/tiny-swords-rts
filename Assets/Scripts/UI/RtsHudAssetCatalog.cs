using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.UI
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/UI/RTS HUD Asset Catalog")]
    public sealed class RtsHudAssetCatalog : ScriptableObject
    {
        [SerializeField] private Sprite[] woodTable = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] specialPaper = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] banner = System.Array.Empty<Sprite>();
        [SerializeField] private Texture2D swords;
        [SerializeField] private Sprite[] swordParts = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] bigRibbons = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] smallRibbons = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] selectionCorners = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite infoIcon;
        [SerializeField] private Sprite musicIcon;
        [SerializeField] private Sprite settingsIcon;
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
        [SerializeField] private Sprite scoutIcon;
        [SerializeField] private Sprite[] scoutFireFrames = System.Array.Empty<Sprite>();

        public Sprite[] WoodTable => woodTable;
        public Sprite[] SpecialPaper => specialPaper;
        public Sprite[] Banner => banner;
        public Texture2D Swords => swords;
        public Sprite[] SwordParts => swordParts;
        public Sprite[] BigRibbons => bigRibbons;
        public Sprite[] SmallRibbons => smallRibbons;
        public Sprite[] SelectionCorners => selectionCorners;
        public Sprite InfoIcon => infoIcon;
        public Sprite MusicIcon => musicIcon;
        public Sprite SettingsIcon => settingsIcon;
        public Sprite CommandSlot => commandSlot;
        public Sprite PopulationIcon => populationIcon;
        public Sprite WoodIcon => woodIcon;
        public Sprite GoldIcon => goldIcon;
        public Sprite FoodIcon => foodIcon;
        public Sprite AttackMoveIcon => attackMoveIcon;
        public Sprite EjectAllIcon => ejectAllIcon;
        public Sprite GuardIcon => guardIcon;
        public Sprite ScoutIcon => scoutIcon;
        public Sprite[] ScoutFireFrames => scoutFireFrames;
        public bool HasScoutPresentation
        {
            get
            {
                if (scoutIcon == null || scoutFireFrames == null
                    || scoutFireFrames.Length != 7) return false;
                for (int i = 0; i < scoutFireFrames.Length; i++)
                    if (scoutFireFrames[i] == null) return false;
                return true;
            }
        }
        public bool IsComplete => woodTable?.Length == 9
            && specialPaper?.Length == 9 && banner?.Length == 9
            && swords != null && swordParts?.Length == 11
            && bigRibbons?.Length == 15 && smallRibbons?.Length == 30
            && selectionCorners?.Length == 4
            && infoIcon != null && musicIcon != null
            && settingsIcon != null
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

        public void SetScout(Sprite scout, Sprite[] scoutFire)
        {
            scoutIcon = scout;
            scoutFireFrames = scoutFire ?? System.Array.Empty<Sprite>();
        }

        public void SetSelectionCorners(Sprite[] corners)
            => selectionCorners = corners ?? System.Array.Empty<Sprite>();

        public void SetTopHud(Sprite[] bannerSprites, Texture2D swordTexture,
            Sprite[] swordSprites, Sprite[] bigRibbonSprites,
            Sprite[] smallRibbonSprites,
            Sprite info, Sprite music, Sprite settings)
        {
            banner = bannerSprites;
            swords = swordTexture;
            swordParts = swordSprites;
            bigRibbons = bigRibbonSprites;
            smallRibbons = smallRibbonSprites;
            infoIcon = info;
            musicIcon = music;
            settingsIcon = settings;
        }
#endif
    }
}
