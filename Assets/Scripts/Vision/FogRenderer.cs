using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Vision
{
    // A single point-filtered 40x28 world-space texture; no per-cell Update methods.
    public sealed class FogRenderer : MonoBehaviour
    {
        [SerializeField] private Faction displayedFaction = Faction.Blue;
        [SerializeField] private Color32 exploredColor = new(0, 0, 0, 135);
        [SerializeField] private Color32 unexploredColor = new(0, 0, 0, 235);
        [SerializeField] private int sortingOrder = 8;
        private TeamVisionSystem vision;
        private GridManager grid;
        private Texture2D texture;
        private Sprite sprite;
        private SpriteRenderer fogVisual;
        private Color32[] pixels;

        public Faction DisplayedFaction => displayedFaction;
        public bool IsFogVisible => fogVisual != null && fogVisual.enabled;

        public void Configure(TeamVisionSystem source, GridManager navigation,
            Faction faction)
        {
            if (vision != null) vision.VisionChanged -= Refresh;
            vision = source;
            grid = navigation;
            displayedFaction = faction;
            CreateVisual();
            vision.VisionChanged += Refresh;
            Refresh();
        }

        public void SetDisplayedFaction(Faction faction)
        {
            displayedFaction = faction;
            Refresh();
        }

        public void SetFogVisible(bool value)
        {
            if (fogVisual != null) fogVisual.enabled = value;
        }

        private void OnDestroy()
        {
            if (vision != null) vision.VisionChanged -= Refresh;
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }

        private void CreateVisual()
        {
            if (grid == null || grid.NavigationData == null || texture != null) return;
            NavigationGridData data = grid.NavigationData;
            texture = new Texture2D(data.Width, data.Height, TextureFormat.RGBA32, false)
            {
                name = "FogOfWarGrid",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            pixels = new Color32[data.Width * data.Height];
            float pixelsPerUnit = 1f / data.CellSize;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, data.Width, data.Height),
                new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            var visual = new GameObject("FogRenderer");
            visual.transform.SetParent(transform, false);
            visual.transform.position = new Vector3(
                data.WorldOrigin.x + data.Width * data.CellSize * 0.5f,
                data.WorldOrigin.y + data.Height * data.CellSize * 0.5f, 0f);
            fogVisual = visual.AddComponent<SpriteRenderer>();
            fogVisual.sprite = sprite;
            fogVisual.sortingOrder = sortingOrder;
        }

        private void Refresh()
        {
            if (vision == null || grid == null || texture == null) return;
            int width = grid.Width;
            int height = grid.Height;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = vision.GetVisionState(displayedFaction,
                    new Vector2Int(x, y)) switch
                {
                    VisionState.Visible => new Color32(0, 0, 0, 0),
                    VisionState.Explored => exploredColor,
                    _ => unexploredColor
                };
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }
    }
}
