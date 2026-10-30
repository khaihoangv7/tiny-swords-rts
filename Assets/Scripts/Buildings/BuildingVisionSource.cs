using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public sealed class BuildingVisionSource : MonoBehaviour, IVisionSource
    {
        private IDefensiveBuildingHost host;

        public Faction Faction => host.Faction;
        public bool IsVisionActive => host != null && host.IsAlive && host.IsDefenseReady;
        public float VisionRadius => host?.DefenseData?.VisionRadius ?? 0f;
        public int VisibleHeightAboveSource => host?.Type == BuildingType.Tower ? 1 : 0;
        public Vector3 VisionPosition
        {
            get
            {
                TinyKingdom.Navigation.NavigationGridData navigation =
                    TeamVisionSystem.Instance?.GridManager?.NavigationData;
                if (host == null || navigation == null) return transform.position;
                Vector2Int origin = host.FootprintOrigin;
                Vector2Int size = host.Footprint;
                return new Vector3(
                    navigation.WorldOrigin.x + (origin.x + size.x * 0.5f)
                        * navigation.CellSize,
                    navigation.WorldOrigin.y + (origin.y + size.y * 0.5f)
                        * navigation.CellSize,
                    transform.position.z);
            }
        }
        public IDefensiveBuildingHost Host => host;

        public void Configure(IDefensiveBuildingHost owner)
        {
            host = owner;
            VisionSourceRegistry.Register(this);
        }

        private void OnEnable()
        {
            if (host != null) VisionSourceRegistry.Register(this);
        }

        private void OnDisable() => VisionSourceRegistry.Unregister(this);
    }
}
