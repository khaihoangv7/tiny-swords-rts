using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Buildings
{
    public sealed class BuildingVisionSource : MonoBehaviour, IVisionSource
    {
        private IDefensiveBuildingHost host;

        public Faction Faction => host.Faction;
        public bool IsVisionActive => host != null && host.IsAlive && host.IsDefenseReady;
        public float VisionRadius => host?.DefenseData?.VisionRadius ?? 0f;
        public Vector3 VisionPosition => transform.position;
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
