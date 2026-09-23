using UnityEngine;

namespace TinyKingdom.Buildings
{
    // Presentation only. BuildingGarrison remains the authoritative count.
    public sealed class BuildingGarrisonWorldCount : MonoBehaviour
    {
        public const int WorldFontSize = 48;
        public const float WorldCharacterSize = 0.05f;

        private BuildingGarrison garrison;
        private GameObject root;
        private TextMesh label;

        public void Configure(BuildingGarrison source, float localY)
        {
            if (garrison != null) garrison.Changed -= Refresh;
            garrison = source;
            if (root == null)
            {
                root = new GameObject("InternalGarrisonWorldCount");
                root.transform.SetParent(transform, false);
                label = root.AddComponent<TextMesh>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = WorldFontSize;
                label.characterSize = WorldCharacterSize;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.color = Color.white;
                MeshRenderer renderer = root.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = label.font.material;
                renderer.sortingOrder = 23;
            }
            root.transform.localPosition = new Vector3(0f, localY, -0.1f);
            garrison.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (garrison != null) garrison.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (root == null || garrison == null) return;
            bool visible = garrison.IsReady && garrison.InternalCount > 0;
            root.SetActive(visible);
            if (visible)
                label.text = $"{garrison.InternalCount}/{garrison.InternalCapacity}";
        }
    }
}
