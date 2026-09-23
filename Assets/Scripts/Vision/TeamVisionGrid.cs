using System;
using System.Collections.Generic;
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
        private bool[][] visible;
        private bool[][] nextVisible;
        private readonly bool[][] explored;

        public int Width => width;
        public int Height => height;

        public TeamVisionGrid(int gridWidth, int gridHeight, Vector2 origin, float size)
        {
            if (gridWidth <= 0 || gridHeight <= 0 || size <= 0f)
                throw new ArgumentOutOfRangeException(nameof(gridWidth));
            width = gridWidth;
            height = gridHeight;
            worldOrigin = origin;
            cellSize = size;
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
                    Reveal(source.Faction, source.VisionPosition, source.VisionRadius);
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

        private void Reveal(Faction faction, Vector3 position, float radius)
        {
            int team = TeamIndex(faction);
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
}
