using System;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using Unity.Profiling;
using UnityEngine;

namespace TinyKingdom.Vision
{
    // High-resolution presentation mask. Gameplay visibility remains authoritative
    // in TeamVisionGrid and never samples this texture.
    public sealed class FogRenderer : MonoBehaviour
    {
        public const int DefaultSamplesPerCell = 4;
        public const float DefaultFeatherWidthCells = 0.25f;
        private const int FactionCount = 2;
        private const ulong HashOffset = 1469598103934665603UL;
        private const ulong HashPrime = 1099511628211UL;
        private static readonly ProfilerMarker RefreshMarker =
            new("TinyKingdom.Fog.VisualMaskRefresh");

        [SerializeField] private Faction displayedFaction = Faction.Blue;
        [SerializeField] private Color32 exploredColor = new(0, 0, 0, 135);
        [SerializeField] private Color32 unexploredColor = new(0, 0, 0, 235);
        [SerializeField, Range(2, 8)] private int samplesPerCell = DefaultSamplesPerCell;
        [SerializeField, Range(0f, 0.5f)] private float featherWidthCells =
            DefaultFeatherWidthCells;
        [SerializeField] private int sortingOrder = 8;

        private TeamVisionSystem vision;
        private GridManager grid;
        private Texture2D texture;
        private Sprite sprite;
        private SpriteRenderer fogVisual;
        private Color32[] pixels;
        private byte[][] revealCoverage;
        private bool[][] exploredSamples;
        private int maskWidth;
        private int maskHeight;
        private float nextMaskUpdate;
        private ulong lastSourceSignature;
        private bool refreshRequested = true;

        public Faction DisplayedFaction => displayedFaction;
        public bool IsFogVisible => fogVisual != null && fogVisual.enabled;
        public int SamplesPerCell => samplesPerCell;
        public float FeatherWidthCells => featherWidthCells;
        public int MaskWidth => maskWidth;
        public int MaskHeight => maskHeight;
        public FilterMode MaskFilterMode => texture != null ? texture.filterMode : FilterMode.Point;

        public void Configure(TeamVisionSystem source, GridManager navigation,
            Faction faction)
        {
            if (vision != null) vision.VisionChanged -= RequestRefresh;
            vision = source;
            grid = navigation;
            displayedFaction = faction;
            CreateVisual();
            if (vision != null) vision.VisionChanged += RequestRefresh;
            RefreshNow();
        }

        public void SetDisplayedFaction(Faction faction)
        {
            displayedFaction = faction;
            RenderDisplayedFaction();
        }

        public void SetFogVisible(bool value)
        {
            if (fogVisual != null) fogVisual.enabled = value;
        }

        public void RefreshNow()
        {
            if (grid == null || grid.NavigationData == null || texture == null) return;
            using (RefreshMarker.Auto())
            {
                RasterizeSources();
                RenderDisplayedFaction();
                lastSourceSignature = CalculateSourceSignature();
                refreshRequested = false;
            }
        }

        private void Update()
        {
            if (vision == null || texture == null || Time.unscaledTime < nextMaskUpdate) return;
            nextMaskUpdate = Time.unscaledTime + vision.UpdateInterval;
            ulong signature = CalculateSourceSignature();
            if (!refreshRequested && signature == lastSourceSignature) return;
            RefreshNow();
        }

        private void OnDestroy()
        {
            if (vision != null) vision.VisionChanged -= RequestRefresh;
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }

        private void RequestRefresh() => refreshRequested = true;

        private void CreateVisual()
        {
            if (grid == null || grid.NavigationData == null || texture != null) return;
            NavigationGridData data = grid.NavigationData;
            samplesPerCell = Mathf.Clamp(samplesPerCell, 2, 8);
            featherWidthCells = Mathf.Clamp(featherWidthCells, 0f, 0.5f);
            maskWidth = data.Width * samplesPerCell;
            maskHeight = data.Height * samplesPerCell;
            int sampleCount = maskWidth * maskHeight;
            texture = new Texture2D(maskWidth, maskHeight, TextureFormat.RGBA32, false)
            {
                name = "FogOfWarVisualMask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            pixels = new Color32[sampleCount];
            revealCoverage = NewFactionArrays<byte>(sampleCount);
            exploredSamples = NewFactionArrays<bool>(sampleCount);
            float pixelsPerUnit = samplesPerCell / data.CellSize;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, maskWidth, maskHeight),
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

        private void RasterizeSources()
        {
            for (int faction = 0; faction < FactionCount; faction++)
                Array.Clear(revealCoverage[faction], 0, revealCoverage[faction].Length);

            var sources = VisionSourceRegistry.ActiveSources;
            for (int i = 0; i < sources.Count; i++)
            {
                IVisionSource source = sources[i];
                if (!IsUsable(source)) continue;
                RasterizeCircle(TeamIndex(source.Faction), source.VisionPosition,
                    source.VisionRadius);
            }
        }

        private void RasterizeCircle(int team, Vector3 worldPosition, float radius)
        {
            NavigationGridData data = grid.NavigationData;
            float sampleSize = data.CellSize / samplesPerCell;
            float featherWorld = featherWidthCells * data.CellSize;
            float outerRadius = radius + featherWorld;
            Vector2 local = (Vector2)worldPosition - data.WorldOrigin;
            int minX = Mathf.Max(0, Mathf.FloorToInt((local.x - outerRadius) / sampleSize));
            int minY = Mathf.Max(0, Mathf.FloorToInt((local.y - outerRadius) / sampleSize));
            int maxX = Mathf.Min(maskWidth - 1,
                Mathf.FloorToInt((local.x + outerRadius) / sampleSize));
            int maxY = Mathf.Min(maskHeight - 1,
                Mathf.FloorToInt((local.y + outerRadius) / sampleSize));
            float radiusSquared = radius * radius + 0.0001f;
            float outerSquared = outerRadius * outerRadius + 0.0001f;

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 sampleCenter = data.WorldOrigin + new Vector2(
                    (x + 0.5f) * sampleSize, (y + 0.5f) * sampleSize);
                float distanceSquared = (sampleCenter - (Vector2)worldPosition).sqrMagnitude;
                if (distanceSquared > outerSquared) continue;
                int index = y * maskWidth + x;
                byte coverage;
                if (distanceSquared <= radiusSquared)
                {
                    coverage = byte.MaxValue;
                    exploredSamples[team][index] = true;
                }
                else if (featherWorld > 0f)
                {
                    float distance = Mathf.Sqrt(distanceSquared);
                    coverage = (byte)Mathf.Clamp(Mathf.RoundToInt(
                        (outerRadius - distance) / featherWorld * byte.MaxValue), 0, 255);
                }
                else coverage = 0;

                if (coverage > revealCoverage[team][index])
                    revealCoverage[team][index] = coverage;
            }
        }

        private void RenderDisplayedFaction()
        {
            if (texture == null || pixels == null) return;
            int team = TeamIndex(displayedFaction);
            byte[] coverage = revealCoverage[team];
            bool[] explored = exploredSamples[team];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 baseColor = explored[i] ? exploredColor : unexploredColor;
                int alpha = Mathf.RoundToInt(baseColor.a * (255 - coverage[i]) / 255f);
                pixels[i] = new Color32(baseColor.r, baseColor.g, baseColor.b, (byte)alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        private ulong CalculateSourceSignature()
        {
            if (grid == null || grid.NavigationData == null) return 0UL;
            NavigationGridData data = grid.NavigationData;
            float samplesPerWorldUnit = samplesPerCell / data.CellSize;
            ulong hash = HashOffset;
            int activeCount = 0;
            var sources = VisionSourceRegistry.ActiveSources;
            for (int i = 0; i < sources.Count; i++)
            {
                IVisionSource source = sources[i];
                if (!IsUsable(source)) continue;
                activeCount++;
                Vector3 position = source.VisionPosition;
                Mix(ref hash, TeamIndex(source.Faction));
                Mix(ref hash, Mathf.RoundToInt((position.x - data.WorldOrigin.x)
                    * samplesPerWorldUnit));
                Mix(ref hash, Mathf.RoundToInt((position.y - data.WorldOrigin.y)
                    * samplesPerWorldUnit));
                Mix(ref hash, Mathf.RoundToInt(source.VisionRadius * 1024f));
            }
            Mix(ref hash, activeCount);
            return hash;
        }

        private static bool IsUsable(IVisionSource source)
        {
            if (source == null || !source.IsVisionActive || source.VisionRadius <= 0f)
                return false;
            return source is not UnityEngine.Object unityObject || unityObject != null;
        }

        private static int TeamIndex(Faction faction) => faction == Faction.Red ? 1 : 0;

        private static void Mix(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= HashPrime;
            }
        }

        private static T[][] NewFactionArrays<T>(int count)
            => new[] { new T[count], new T[count] };
    }
}
