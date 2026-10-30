using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Vision
{
    // Gameplay-only visibility storage. Rendering never feeds data back into this model.
    public sealed class TeamVisionGrid
    {
        private const int FactionCount = 2;
        private readonly int width;
        private readonly int height;
        private readonly Vector2 worldOrigin;
        private readonly float cellSize;
        private readonly VisionElevationMap elevation;
        private bool[][] visible;
        private bool[][] nextVisible;
        private readonly bool[][] explored;

        public int Width => width;
        public int Height => height;

        public TeamVisionGrid(int gridWidth, int gridHeight, Vector2 origin, float size,
            NavigationGridData navigationData = null)
        {
            if (gridWidth <= 0 || gridHeight <= 0 || size <= 0f)
                throw new ArgumentOutOfRangeException(nameof(gridWidth));
            width = gridWidth;
            height = gridHeight;
            worldOrigin = origin;
            cellSize = size;
            elevation = new VisionElevationMap(gridWidth, gridHeight, origin, size,
                navigationData);
            int count = width * height;
            visible = NewFactionArrays(count);
            nextVisible = NewFactionArrays(count);
            explored = NewFactionArrays(count);
        }

        public bool Recompute(IReadOnlyList<IVisionSource> sources)
        {
            for (int faction = 0; faction < FactionCount; faction++)
                Array.Clear(nextVisible[faction], 0, nextVisible[faction].Length);

            if (sources != null)
                for (int i = 0; i < sources.Count; i++)
                {
                    IVisionSource source = sources[i];
                    if (!IsUsable(source)) continue;
                    Reveal(source.Faction, source.VisionPosition, source.VisionRadius,
                        source.VisibleHeightAboveSource);
                }

            bool changed = false;
            for (int faction = 0; faction < FactionCount; faction++)
            for (int index = 0; index < visible[faction].Length; index++)
            {
                bool value = nextVisible[faction][index];
                if (value && !explored[faction][index])
                {
                    explored[faction][index] = true;
                    changed = true;
                }
                if (visible[faction][index] != value) changed = true;
            }

            (visible, nextVisible) = (nextVisible, visible);
            return changed;
        }

        public VisionState GetState(Faction faction, Vector2Int cell)
        {
            if (!IsInside(cell)) return VisionState.Unexplored;
            int index = cell.y * width + cell.x;
            int team = TeamIndex(faction);
            if (visible[team][index]) return VisionState.Visible;
            return explored[team][index] ? VisionState.Explored : VisionState.Unexplored;
        }

        public VisionState GetState(Faction faction, Vector3 worldPosition)
            => GetState(faction, WorldToCell(worldPosition));

        public bool IsVisible(Faction faction, Vector3 worldPosition)
            => GetState(faction, worldPosition) == VisionState.Visible;

        public bool IsExplored(Faction faction, Vector3 worldPosition)
            => GetState(faction, worldPosition) != VisionState.Unexplored;

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            Vector2 offset = (Vector2)worldPosition - worldOrigin;
            return new Vector2Int(Mathf.FloorToInt(offset.x / cellSize),
                Mathf.FloorToInt(offset.y / cellSize));
        }

        private void Reveal(Faction faction, Vector3 position, float radius,
            int visibleHeightAboveSource)
        {
            int team = TeamIndex(faction);
            int sourceHeight = elevation.HeightAt(position);
            float radiusSquared = radius * radius + 0.0001f;
            Vector2Int minimum = WorldToCell(position - new Vector3(radius, radius));
            Vector2Int maximum = WorldToCell(position + new Vector3(radius, radius));
            int minX = Mathf.Max(0, minimum.x);
            int minY = Mathf.Max(0, minimum.y);
            int maxX = Mathf.Min(width - 1, maximum.x);
            int maxY = Mathf.Min(height - 1, maximum.y);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                if (!elevation.CanReveal(sourceHeight, visibleHeightAboveSource, x, y))
                    continue;
                Vector2 center = worldOrigin + new Vector2((x + 0.5f) * cellSize,
                    (y + 0.5f) * cellSize);
                if ((center - (Vector2)position).sqrMagnitude <= radiusSquared)
                    nextVisible[team][y * width + x] = true;
            }
        }

        private bool IsInside(Vector2Int cell)
            => cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;

        private static int TeamIndex(Faction faction) => faction == Faction.Red ? 1 : 0;

        private static bool IsUsable(IVisionSource source)
        {
            if (source == null || !source.IsVisionActive || source.VisionRadius <= 0f)
                return false;
            return source is not UnityEngine.Object unityObject || unityObject != null;
        }

        private static bool[][] NewFactionArrays(int count)
            => new[] { new bool[count], new bool[count] };
    }

    // Shared by authoritative cell vision and the higher-resolution fog mask.
    public sealed class VisionElevationMap
    {
        // A short, rounded view onto the upper landing, not the full plateau.
        public const float StairGlimpseRadiusCells = 1.75f;
        public const float StairGlimpseFeatherCells = 0.5f;
        private readonly int width;
        private readonly int height;
        private readonly Vector2 origin;
        private readonly float cellSize;
        private readonly int[] levels;
        private readonly bool[] upperStairEndpoints;
        private readonly Vector2[] nearestUpperStair;
        private readonly bool[] hasUpperStair;

        public VisionElevationMap(int width, int height, Vector2 origin, float cellSize,
            NavigationGridData navigationData)
        {
            this.width = width;
            this.height = height;
            this.origin = origin;
            this.cellSize = cellSize;
            levels = new int[width * height];
            upperStairEndpoints = new bool[levels.Length];
            nearestUpperStair = new Vector2[levels.Length];
            hasUpperStair = new bool[levels.Length];
            if (navigationData == null) return;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!navigationData.TryGetCellData(x, y, out NavigationCellData cell))
                    continue;
                int index = y * width + x;
                levels[index] = cell.heightLevel;
                if (cell.terrainType != TerrainType.Stairs || !cell.hasHeightConnection
                    || !navigationData.TryGetCellData(cell.heightConnection.x,
                        cell.heightConnection.y, out NavigationCellData partner))
                    continue;
                upperStairEndpoints[index] = partner.hasHeightConnection
                    && partner.heightConnection == new Vector2Int(x, y)
                    && cell.heightLevel == partner.heightLevel + 1;
            }

            var stairEntries = new List<(int Height, Vector2 Center)>();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (upperStairEndpoints[y * width + x])
                    stairEntries.Add((levels[y * width + x], origin
                        + new Vector2((x + 0.5f) * cellSize,
                            (y + 0.5f) * cellSize)));

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                Vector2 center = origin + new Vector2((x + 0.5f) * cellSize,
                    (y + 0.5f) * cellSize);
                float bestDistance = float.PositiveInfinity;
                foreach ((int stairHeight, Vector2 stairCenter) in stairEntries)
                {
                    if (stairHeight != levels[index]) continue;
                    float distance = (center - stairCenter).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    nearestUpperStair[index] = stairCenter;
                    hasUpperStair[index] = true;
                }
            }
        }

        public int HeightAt(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt((worldPosition.x - origin.x) / cellSize);
            int y = Mathf.FloorToInt((worldPosition.y - origin.y) / cellSize);
            return x >= 0 && x < width && y >= 0 && y < height
                ? levels[y * width + x] : 0;
        }

        public bool CanReveal(int sourceHeight, int allowedRise, int x, int y)
        {
            int index = y * width + x;
            int targetHeight = levels[index];
            if (targetHeight <= sourceHeight + allowedRise) return true;
            if (allowedRise != 0 || targetHeight != sourceHeight + 1
                || !hasUpperStair[index]) return false;
            Vector2 center = origin + new Vector2((x + 0.5f) * cellSize,
                (y + 0.5f) * cellSize);
            float radius = StairGlimpseRadiusCells * cellSize;
            return (center - nearestUpperStair[index]).sqrMagnitude
                <= radius * radius + 0.0001f;
        }

        public float VisualCoverage(int sourceHeight, int allowedRise, int x, int y,
            Vector2 samplePosition, out bool insideGlimpse)
        {
            int index = y * width + x;
            int targetHeight = levels[index];
            if (targetHeight <= sourceHeight + allowedRise)
            {
                insideGlimpse = true;
                return 1f;
            }
            if (allowedRise != 0 || targetHeight != sourceHeight + 1
                || !hasUpperStair[index])
            {
                insideGlimpse = false;
                return 0f;
            }

            float radius = StairGlimpseRadiusCells * cellSize;
            float distanceSquared = (samplePosition - nearestUpperStair[index]).sqrMagnitude;
            insideGlimpse = distanceSquared <= radius * radius;
            if (insideGlimpse) return 1f;
            float feather = StairGlimpseFeatherCells * cellSize;
            float outerRadius = radius + feather;
            if (distanceSquared >= outerRadius * outerRadius) return 0f;
            return (outerRadius - Mathf.Sqrt(distanceSquared)) / feather;
        }
    }
}
