using TinyKingdom.Navigation;
using TinyKingdom.Terrain;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace TinyKingdom.Presentation
{
    // All world objects share one projected ground-depth domain. Logical elevation
    // shifts a ground contact south by one visual cell per level before Y sorting.
    public static class GroundSorting
    {
        public const int WorldOrder = -1024;
        public const int ProjectedWorldBaseOrder = -2048;
        public const int ProjectedDepthStepsPerCell = 32;
        public const int MaxProjectedDepthOffset = 1535;
        public const int MaxAdditionalOrder = 7;
        public const int DefaultEntityAdditionalOrder = 1;
        public const int CliffFaceAdditionalOrder = 2;
        public const int MountedPresentationAdditionalOrder = 6;
        public const int MaxSupportedElevation = 3;
        public const float ConnectorCorridorWidthCells = 0.65f;
        public const int TerrainBackgroundBaseOrder = -2080;

        private static GridManager cachedGrid;
        private static ulong configuredSceneHandle = ulong.MaxValue;
        private static bool configuringScene;

        public static int MinimumExpectedOrder => ProjectedWorldBaseOrder;
        public static int MaximumExpectedOrder => ProjectedWorldBaseOrder
            + MaxProjectedDepthOffset + MaxAdditionalOrder;

        public static ElevationSortingGroup Ensure(GameObject root, bool trackMovement = false,
            float? groundAnchorYOffset = null)
        {
            if (root == null) return null;
            ElevationSortingGroup sorter = root.GetComponent<ElevationSortingGroup>();
            if (sorter == null) sorter = root.AddComponent<ElevationSortingGroup>();
            sorter.Configure(trackMovement, groundAnchorYOffset);
            EnsureSceneConfigured();
            return sorter;
        }

        public static int TerrainBackgroundOrder(int offset)
            => TerrainBackgroundBaseOrder + Mathf.Clamp(offset, 0, 31);

        public static int ElevatedTerrainBackgroundOrder(int elevation, bool surface)
            => TerrainBackgroundOrder(8 + Mathf.Clamp(elevation, 0, MaxSupportedElevation) * 2
                + (surface ? 1 : 0));

        public static float ProjectedGroundY(GridManager grid, float logicalElevation,
            float groundAnchorY)
        {
            if (grid == null || grid.NavigationData == null) return groundAnchorY;
            return groundAnchorY - logicalElevation
                * Mathf.Max(0.01f, grid.NavigationData.CellSize);
        }

        public static int CalculateOrder(GridManager grid, float logicalElevation,
            float groundAnchorY, int additionalOrder = DefaultEntityAdditionalOrder)
        {
            if (grid == null || grid.NavigationData == null) return WorldOrder;
            float cellSize = Mathf.Max(0.01f, grid.NavigationData.CellSize);
            float mapTop = grid.NavigationData.WorldOrigin.y + grid.Height * cellSize;
            float projectedY = ProjectedGroundY(grid, logicalElevation, groundAnchorY);
            int depth = Mathf.Clamp(Mathf.RoundToInt(
                (mapTop - projectedY) / cellSize * ProjectedDepthStepsPerCell),
                0, MaxProjectedDepthOffset);
            return ProjectedWorldBaseOrder + depth
                + Mathf.Clamp(additionalOrder, 0, MaxAdditionalOrder);
        }

        public static int ElevationAt(GridManager grid, Vector3 worldPosition)
        {
            if (grid == null) return 0;
            Vector2Int coordinate = grid.WorldToGrid(worldPosition);
            return grid.GetCell(coordinate.x, coordinate.y)?.HeightLevel ?? 0;
        }

        public static float ProjectedElevationAt(GridManager grid, Vector3 worldPosition,
            bool interpolateConnector)
        {
            if (grid == null || grid.NavigationData == null) return 0f;
            Vector2Int coordinate = grid.WorldToGrid(worldPosition);
            GridCell current = grid.GetCell(coordinate.x, coordinate.y);
            if (current == null || !interpolateConnector) return current?.HeightLevel ?? 0;

            float cellSize = Mathf.Max(0.01f, grid.NavigationData.CellSize);
            float maximumDistanceSquared = cellSize * ConnectorCorridorWidthCells;
            maximumDistanceSquared *= maximumDistanceSquared;
            float bestDistanceSquared = float.PositiveInfinity;
            float bestElevation = current.HeightLevel;

            // Only reciprocal connector cells interpolate. This stays local and
            // allocation free; ordinary cells retain their integer height.
            for (int y = coordinate.y - 1; y <= coordinate.y + 1; y++)
            for (int x = coordinate.x - 1; x <= coordinate.x + 1; x++)
            {
                GridCell lower = grid.GetCell(x, y);
                if (lower == null || !lower.HeightConnection.HasValue) continue;
                Vector2Int upperCoordinate = lower.HeightConnection.Value;
                GridCell upper = grid.GetCell(upperCoordinate.x, upperCoordinate.y);
                if (upper == null || lower.HeightLevel >= upper.HeightLevel
                    || upper.HeightConnection != new Vector2Int(lower.GridX, lower.GridY)) continue;
                if (coordinate != new Vector2Int(lower.GridX, lower.GridY)
                    && coordinate != upperCoordinate) continue;

                Vector2 from = lower.WorldPosition;
                Vector2 segment = (Vector2)upper.WorldPosition - from;
                float lengthSquared = segment.sqrMagnitude;
                if (lengthSquared <= 0.0001f) continue;
                float t = Mathf.Clamp01(Vector2.Dot((Vector2)worldPosition - from, segment)
                    / lengthSquared);
                Vector2 closest = from + segment * t;
                float distanceSquared = ((Vector2)worldPosition - closest).sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared
                    || distanceSquared >= bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                bestElevation = Mathf.Lerp(lower.HeightLevel, upper.HeightLevel, t);
            }
            return bestElevation;
        }

        public static float GroundOffsetFromVisual(Transform root, SpriteRenderer visual)
        {
            if (root == null || visual == null || visual.sprite == null) return 0f;
            Vector3 contact = visual.transform.TransformPoint(
                new Vector3(0f, visual.sprite.bounds.min.y, 0f));
            return contact.y - root.position.y;
        }

        public static void ApplyStatic(Renderer renderer, Vector3 elevationSamplePosition,
            float groundAnchorYOffset, GridManager grid = null)
        {
            if (renderer == null) return;
            grid ??= ResolveGrid();
            float elevation = ProjectedElevationAt(grid, elevationSamplePosition, false);
            renderer.sortingOrder = CalculateOrder(grid, elevation,
                elevationSamplePosition.y + groundAnchorYOffset);
        }

        public static GridManager ResolveGrid()
        {
            if (cachedGrid == null) cachedGrid = Object.FindAnyObjectByType<GridManager>();
            cachedGrid?.EnsureInitialized();
            return cachedGrid;
        }

        public static void EnsureSceneConfigured()
        {
            if (configuringScene) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || configuredSceneHandle == scene.handle.GetRawData()) return;
            GridManager grid = ResolveGrid();
            if (grid == null) return;

            configuringScene = true;
            try
            {
                GameObject map = GameObject.Find("Map");
                Transform visualGrid = map?.transform.Find("Grid");
                SetTilemapOrder(visualGrid, "BG_Color", TerrainBackgroundOrder(0));
                SetTilemapOrder(visualGrid, "WaterFoam", TerrainBackgroundOrder(1));
                SetTilemapOrder(visualGrid, "FlatGround", TerrainBackgroundOrder(2));
                SetTilemapOrder(visualGrid, "MeadowsAndPaths", TerrainBackgroundOrder(3));
                SetTilemapOrder(visualGrid, "Decorations", TerrainBackgroundOrder(4));
                for (int level = 1; level <= MaxSupportedElevation; level++)
                {
                    SetTilemapOrder(visualGrid, "Shadow_L" + level,
                        ElevatedTerrainBackgroundOrder(level, false));
                    SetTilemapOrder(visualGrid, "ElevatedGround_L" + level,
                        ElevatedTerrainBackgroundOrder(level, true));
                    // LinearLane's front stair copies must stay above terrain,
                    // but below the depth-sorted units crossing the ramp.
                    SetTilemapOrder(visualGrid, "StairFront_L" + level,
                        TerrainBackgroundOrder(16 + level));
                }
                EnsureProjectedTerrainForeground(map?.transform, visualGrid, grid);

                Transform scenery = map?.transform.Find("BattlefieldScenery");
                if (scenery != null)
                {
                    PresentationAssetCatalog catalog = PresentationAssetCatalog.Load();
                    SpriteRenderer[] renderers = scenery.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        SpriteRenderer renderer = renderers[i];
                        if (renderer == null || renderer.name.StartsWith("Cloud_")
                            || renderer.name.StartsWith("Bridge_")) continue;
                        Vector3 groundPosition = renderer.transform.position;
                        if (TryBushVariant(renderer.sprite, out int variant)
                            && catalog != null && catalog.BushFrames(variant).Length == 8)
                        {
                            float phase = DeterministicPhase(renderer.transform.position, variant);
                            BushPresentation bush = BushPresentation.Ensure(renderer.gameObject,
                                renderer, catalog.BushFrames(variant), variant, phase);
                            if (bush != null)
                            {
                                renderer = bush.Visual;
                                groundPosition = bush.transform.position;
                            }
                        }
                        ApplyStatic(renderer, groundPosition, 0f, grid);
                    }
                }
                configuredSceneHandle = scene.handle.GetRawData();
            }
            finally
            {
                configuringScene = false;
            }
        }

        public static void ResetSceneCacheForTests()
        {
            configuredSceneHandle = ulong.MaxValue;
            cachedGrid = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetSceneCacheForTests();

        private static void EnsureProjectedTerrainForeground(Transform map, Transform visualGrid,
            GridManager grid)
        {
            if (map == null || visualGrid == null) return;
            Transform root = map.Find("ProjectedTerrainForeground");
            if (root == null)
            {
                GameObject rootObject = new("ProjectedTerrainForeground");
                rootObject.transform.SetParent(map, false);
                root = rootObject.transform;
            }

            bool island = map.gameObject.scene.name == "GoblinIsland";
            TerraceDefinition[] platforms = island ? GoblinIslandLayout.Platforms : BattlefieldTerraces.Platforms;
            if(island)
            {
                for(int level=1;level<=GoblinIslandLayout.MaxHeight;level++)
                {
                    Tilemap surface=visualGrid.Find("ElevatedGround_L"+level)?.GetComponent<Tilemap>();
                    for(int y=0;y<grid.Height;y++) for(int x=0;x<grid.Width;x++)
                        if(GoblinIslandLayout.IsCliff(x,y,level)
                            && GoblinIslandLayout.Cell(x,y).terrainType==TerrainType.Cliff)
                            EnsureForegroundSprite(root,surface,new(x,y,0),$"IslandCliff_{level}_{x}_{y}",
                                ProjectedTerrainRole.CliffFace,level-1,CliffFaceAdditionalOrder,grid);
                }
            }
            for (int terraceIndex = 0; !island && terraceIndex < platforms.Length;
                terraceIndex++)
            {
                TerraceDefinition terrace = platforms[terraceIndex];
                Tilemap source = visualGrid.Find("ElevatedGround_L" + terrace.Height)
                    ?.GetComponent<Tilemap>();
                for (int x = terrace.Surface.xMin; x < terrace.Surface.xMax; x++)
                {
                    Vector3Int cell = new(x, terrace.Surface.yMin - 1, 0);
                    EnsureForegroundSprite(root, source, cell,
                        $"Cliff_{terraceIndex}_{x}", ProjectedTerrainRole.CliffFace,
                        terrace.Height - 1, CliffFaceAdditionalOrder, grid);
                }
            }

            int connectorCount=island?GoblinIslandLayout.ConnectorCount:BattlefieldTerraces.ConnectorCount;
            for (int i = 0; i < connectorCount; i++)
            {
                TerrainHeightConnectorDefinition stair = island ? GoblinIslandLayout.Connector(i)
                    : BattlefieldTerraces.Connector(i);
                Tilemap source = visualGrid.Find("ElevatedGround_L" + (stair.LowerHeight + 1))
                    ?.GetComponent<Tilemap>();
                EnsureForegroundSprite(root, source,
                    new Vector3Int(stair.VisualX, stair.LowerY, 0), $"Stair_{i}_Lower",
                    ProjectedTerrainRole.Stair, stair.LowerHeight, 0, grid);
                EnsureForegroundSprite(root, source,
                    new Vector3Int(stair.VisualX, stair.UpperY, 0), $"Stair_{i}_Upper",
                    ProjectedTerrainRole.Stair, stair.LowerHeight + 1, 0, grid);
            }
        }

        private static void EnsureForegroundSprite(Transform root, Tilemap source,
            Vector3Int cell, string objectName, ProjectedTerrainRole role, int logicalElevation,
            int additionalOrder, GridManager grid)
        {
            if (source == null) return;
            Sprite sprite = source.GetSprite(cell);
            if (sprite == null) return;
            Transform child = root.Find(objectName);
            if (child == null)
            {
                GameObject instance = new(objectName);
                instance.transform.SetParent(root, false);
                child = instance.transform;
            }
            child.position = source.GetCellCenterWorld(cell);
            child.rotation = source.transform.rotation;
            child.localScale = source.transform.lossyScale;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            TilemapRenderer sourceRenderer = source.GetComponent<TilemapRenderer>();
            renderer.sprite = sprite;
            renderer.color = source.GetColor(cell);
            if (sourceRenderer != null)
            {
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }
            renderer.sortingOrder = CalculateOrder(grid, logicalElevation,
                child.position.y, additionalOrder);

            ProjectedTerrainForeground marker = child.GetComponent<ProjectedTerrainForeground>();
            if (marker == null) marker = child.gameObject.AddComponent<ProjectedTerrainForeground>();
            marker.Configure(role, logicalElevation, cell, renderer.sortingOrder);
        }

        private static void SetTilemapOrder(Transform root, string name, int order)
        {
            TilemapRenderer renderer = root?.Find(name)?.GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.sortingOrder = order;
        }

        private static bool TryBushVariant(Sprite sprite, out int variant)
        {
            variant = -1;
            string name = sprite != null ? sprite.name : string.Empty;
            if (!name.StartsWith("Bushe") || name.Length < 6) return false;
            return int.TryParse(name.Substring(5, 1), out int oneBased)
                && (variant = oneBased - 1) >= 0 && variant < 4;
        }

        private static float DeterministicPhase(Vector3 position, int variant)
        {
            int x = Mathf.RoundToInt(position.x * 100f);
            int y = Mathf.RoundToInt(position.y * 100f);
            uint hash = unchecked((uint)(x * 73856093 ^ y * 19349663 ^ variant * 83492791));
            return (hash % 800u) * 0.001f;
        }
    }

    public enum ProjectedTerrainRole { CliffFace, Stair }

    [DisallowMultipleComponent]
    public sealed class ProjectedTerrainForeground : MonoBehaviour
    {
        [SerializeField] private ProjectedTerrainRole role;
        [SerializeField] private int logicalElevation;
        [SerializeField] private Vector3Int sourceCell;
        [SerializeField] private int calculatedOrder;

        public ProjectedTerrainRole Role => role;
        public int LogicalElevation => logicalElevation;
        public Vector3Int SourceCell => sourceCell;
        public int CalculatedOrder => calculatedOrder;

        public void Configure(ProjectedTerrainRole value, int elevation, Vector3Int cell, int order)
        {
            role = value;
            logicalElevation = elevation;
            sourceCell = cell;
            calculatedOrder = order;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SortingGroup))]
    public sealed class ElevationSortingGroup : MonoBehaviour
    {
        [SerializeField] private bool trackMovement;
        [SerializeField] private bool hasExplicitGroundAnchor;
        [SerializeField] private float groundAnchorYOffset;
        [SerializeField, Range(0, GroundSorting.MaxAdditionalOrder)]
        private int additionalOrder = GroundSorting.DefaultEntityAdditionalOrder;
        [SerializeField] private bool drawDebugGizmo = false;

        private SortingGroup sortingGroup;
        private GridManager grid;
        private UnitSelection unitSelection;
        private int lastElevation = int.MinValue;
        private float lastProjectedElevation = float.NaN;
        private float lastProjectedGroundY = float.NaN;
        private int lastOrder = int.MinValue;
        private Vector3 lastSamplePosition;
        private bool hasLastSamplePosition;

        public bool TrackMovement => trackMovement;
        public float GroundAnchorYOffset => ResolveGroundAnchorYOffset();
        public int ElevationLevel => lastElevation;
        public float ProjectedElevation => lastProjectedElevation;
        public float ProjectedGroundY => lastProjectedGroundY;
        public int CalculatedOrder => lastOrder;
        public int AdditionalOrder => additionalOrder;
        public SortingGroup Group => sortingGroup;

        public void Configure(bool shouldTrackMovement, float? explicitGroundAnchor)
        {
            trackMovement = shouldTrackMovement;
            if (explicitGroundAnchor.HasValue)
            {
                hasExplicitGroundAnchor = true;
                groundAnchorYOffset = explicitGroundAnchor.Value;
            }
            Resolve();
            RefreshNow(true);
        }

        public void SetAdditionalOrder(int value)
        {
            additionalOrder = Mathf.Clamp(value, 0, GroundSorting.MaxAdditionalOrder);
            RefreshNow(true);
        }

        public void RefreshNow(bool force = false)
        {
            Resolve();
            if (sortingGroup == null || grid == null) return;
            int logicalElevation = GroundSorting.ElevationAt(grid, transform.position);
            float projectedElevation = GroundSorting.ProjectedElevationAt(grid,
                transform.position, trackMovement);
            float groundAnchorY = transform.position.y + ResolveGroundAnchorYOffset();
            float projectedGroundY = GroundSorting.ProjectedGroundY(grid,
                projectedElevation, groundAnchorY);
            int order = GroundSorting.CalculateOrder(grid, projectedElevation,
                groundAnchorY, additionalOrder);
            lastSamplePosition = transform.position;
            hasLastSamplePosition = true;
            if (!force && logicalElevation == lastElevation && order == lastOrder
                && Mathf.Approximately(projectedElevation, lastProjectedElevation)) return;
            sortingGroup.sortingOrder = order;
            lastElevation = logicalElevation;
            lastProjectedElevation = projectedElevation;
            lastProjectedGroundY = projectedGroundY;
            lastOrder = order;
        }

        private void OnEnable()
        {
            Resolve();
            RefreshNow(true);
        }

        private void LateUpdate()
        {
            if (!trackMovement) return;
            Vector3 position = transform.position;
            if (!hasLastSamplePosition
                || ((Vector2)(position - lastSamplePosition)).sqrMagnitude > 0.000001f)
                RefreshNow();
        }

        private void Resolve()
        {
            if (sortingGroup == null) sortingGroup = GetComponent<SortingGroup>();
            if (grid == null) grid = GroundSorting.ResolveGrid();
            if (unitSelection == null) unitSelection = GetComponent<UnitSelection>();
            if (sortingGroup == null)
                Debug.LogError($"Could not attach SortingGroup to {name}.", this);
        }

        private float ResolveGroundAnchorYOffset()
        {
            if (hasExplicitGroundAnchor) return groundAnchorYOffset;
            if (unitSelection == null) unitSelection = GetComponent<UnitSelection>();
            return unitSelection != null ? unitSelection.MarkerYOffset : -0.6f;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmo) return;
            Vector3 ground = transform.position + Vector3.up * ResolveGroundAnchorYOffset();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ground, 0.08f);
            if (!float.IsNaN(lastProjectedGroundY))
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(ground, new Vector3(ground.x, lastProjectedGroundY, ground.z));
                Gizmos.DrawWireSphere(new Vector3(ground.x, lastProjectedGroundY, ground.z), 0.06f);
            }
#if UNITY_EDITOR
            string layer = sortingGroup != null
                ? SortingLayer.IDToName(sortingGroup.sortingLayerID) : "unresolved";
            float projectedOffset = ground.y - lastProjectedGroundY;
            UnityEditor.Handles.Label(ground + Vector3.up * 0.25f,
                $"World Y: {transform.position.y:F3}\n"
                + $"Ground anchor Y: {ground.y:F3}\n"
                + $"Logical elevation: {lastElevation}\n"
                + $"Projected elevation: {lastProjectedElevation:F3}\n"
                + $"Elevation offset: {projectedOffset:F3}\n"
                + $"Projected ground Y: {lastProjectedGroundY:F3}\n"
                + $"SortingGroup: {layer} / {lastOrder}");
#endif
        }
    }
}
