using System;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.Vision
{
    [DefaultExecutionOrder(-120)]
    public sealed class TeamVisionSystem : MonoBehaviour
    {
        private static readonly ProfilerMarker RecomputeMarker =
            new("TinyKingdom.Fog.TeamVisionRecompute");
        [SerializeField] private GridManager gridManager;
        [SerializeField, Min(0.02f)] private float updateInterval = 0.10f;
        private TeamVisionGrid model;
        private float nextUpdate;

        public static TeamVisionSystem Instance { get; private set; }
        public float UpdateInterval => updateInterval;
        public GridManager GridManager => gridManager;
        public bool IsReady => model != null;
        public event Action VisionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (gridManager != null) InitializeModel();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (model == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + updateInterval;
            RecomputeNow();
        }

        public void Configure(GridManager grid, float interval = 0.10f)
        {
            gridManager = grid;
            updateInterval = Mathf.Max(0.02f, interval);
            InitializeModel();
            RecomputeNow();
        }

        public void RecomputeNow()
        {
            if (model == null) return;
            using (RecomputeMarker.Auto())
            {
                if (model.Recompute(VisionSourceRegistry.ActiveSources))
                    VisionChanged?.Invoke();
            }
        }

        public VisionState GetVisionState(Faction faction, Vector2Int cell)
            => model != null ? model.GetState(faction, cell) : VisionState.Unexplored;

        public VisionState GetVisionState(Faction faction, Vector3 worldPosition)
            => model != null ? model.GetState(faction, worldPosition) : VisionState.Unexplored;

        public bool IsVisible(Faction faction, Vector3 worldPosition)
            => model != null && model.IsVisible(faction, worldPosition);

        public bool IsExplored(Faction faction, Vector3 worldPosition)
            => model != null && model.IsExplored(faction, worldPosition);

        private void InitializeModel()
        {
            if (gridManager == null || gridManager.NavigationData == null) return;
            NavigationGridData data = gridManager.NavigationData;
            model = new TeamVisionGrid(data.Width, data.Height, data.WorldOrigin, data.CellSize);
            nextUpdate = 0f;
        }
    }
}
