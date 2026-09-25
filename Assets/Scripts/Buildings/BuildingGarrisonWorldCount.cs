using UnityEngine;
using TinyKingdom.Economy;
using TinyKingdom.Vision;

namespace TinyKingdom.Buildings
{
    // Presentation only. BuildingGarrison remains the authoritative count.
    public sealed class BuildingGarrisonWorldCount : MonoBehaviour
    {
        public const int WorldFontSize = 48;
        public const float WorldCharacterSize = 0.05f;
        private const float VisibilityRefreshInterval = 0.10f;

        private BuildingGarrison garrison;
        private GoldMineObjective mine;
        private GameObject root;
        private TextMesh label;
        private float nextVisibilityRefresh;
        private int displayedCount = -1;
        private int displayedCapacity = -1;

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

        public void Configure(GoldMineObjective source, float localY)
        {
            if (mine != null) mine.Changed -= Refresh;
            mine = source;
            EnsureVisual();
            root.transform.localPosition = new Vector3(0f, localY, -0.1f);
            mine.Changed += Refresh;
            Refresh();
        }

        private void EnsureVisual()
        {
            if (root != null) return;
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

        private void OnDestroy()
        {
            if (garrison != null) garrison.Changed -= Refresh;
            if (mine != null) mine.Changed -= Refresh;
        }

        private void Update()
        {
            if (mine == null || Time.unscaledTime < nextVisibilityRefresh) return;
            nextVisibilityRefresh = Time.unscaledTime + VisibilityRefreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            if (root == null) return;
            if (mine != null)
            {
                nextVisibilityRefresh = Time.unscaledTime + VisibilityRefreshInterval;
                bool observed = PlayerViewRelation.IsFriendlyToView(mine.Faction)
                    && (FogWorldVisibility.Instance == null
                        || FogWorldVisibility.Instance.CanObserve(mine.Faction, mine.transform.position));
                bool mineVisible = mine.State == GoldMineState.Owned
                    && mine.OccupantCount > 0 && observed;
                if (root.activeSelf != mineVisible) root.SetActive(mineVisible);
                if (mineVisible) SetCount(mine.OccupantCount, GoldMineObjective.PawnCapacity);
                return;
            }
            if (garrison == null) return;
            bool visible = garrison.IsReady && garrison.InternalCount > 0;
            if (root.activeSelf != visible) root.SetActive(visible);
            if (visible) SetCount(garrison.InternalCount, garrison.InternalCapacity);
        }

        private void SetCount(int count, int capacity)
        {
            if (displayedCount == count && displayedCapacity == capacity) return;
            displayedCount = count;
            displayedCapacity = capacity;
            label.text = $"{count}/{capacity}";
        }
    }
}
