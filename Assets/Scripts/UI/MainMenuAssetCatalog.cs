using UnityEngine;
using UnityEngine.Tilemaps;
using TinyKingdom.Navigation;
using TinyKingdom.Units;

namespace TinyKingdom.UI
{
    [CreateAssetMenu(menuName = "Tiny Kingdom/UI/Main Menu Assets")]
    public sealed class MainMenuAssetCatalog : ScriptableObject
    {
        public Sprite[] blueRegular;
        public Sprite[] bluePressed;
        public Sprite[] redRegular;
        public Sprite[] redPressed;
        public Sprite backRegular;
        public Sprite backPressed;
        public Sprite closeRegular;
        public Sprite closePressed;
        public Sprite[] mapPreviews;
        public GameObject backdropPrefab;
        public NavigationGridData backdropTerrain;
        public UnitAnimationSet[] idleGuards;
        public Font titleFont;
        public Font menuFont;
        public Texture2D defaultCursor;
        public Sprite logo;
        public Sprite[] villageBuildings;
        public Sprite[] villageTreeFrames;
        public Sprite[] villageBushFrames;
        public Sprite[] villageDuckFrames;
        public Sprite[] villageWaterFrames;
        public Sprite[] villageBridges;
        public TileBase[] villageShore;
        public TileBase[] villageMeadow;
        public TileBase villageFoam;

        public static MainMenuAssetCatalog Load()
            => Resources.Load<MainMenuAssetCatalog>("Presentation/MainMenuAssets");
    }
}
