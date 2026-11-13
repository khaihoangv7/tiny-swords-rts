using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using TinyKingdom.Buildings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TinyKingdom.Input
{
    // Future friendly buildings can implement this on a Collider2D without changing cursor logic.
    public interface IFriendlyCursorTarget
    {
        bool IsFriendlyCursorTarget(Faction playerFaction);
    }

    // Query-only pointer coverage for artwork that extends beyond its logical
    // navigation/interaction footprint. This trigger never owns gameplay authority.
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class WorldSelectionHitArea : MonoBehaviour
    {
        private const string ObjectName = "SelectionHitArea";

        [SerializeField] private Transform targetRoot;
        [SerializeField, Min(0f)] private float selectionPadding;

        public Transform TargetRoot => targetRoot;
        public BoxCollider2D HitCollider => GetComponent<BoxCollider2D>();
        public float SelectionPadding => selectionPadding;

        public static WorldSelectionHitArea Ensure(Component target, SpriteRenderer visual,
            float worldPadding = 0f)
        {
            if (target == null || visual == null) return null;
            Transform child = target.transform.Find(ObjectName);
            WorldSelectionHitArea area;
            if (child == null)
            {
                var hitObject = new GameObject(ObjectName);
                hitObject.transform.SetParent(target.transform, false);
                child = hitObject.transform;
            }
            child.gameObject.layer = target.gameObject.layer;

            BoxCollider2D collider = child.GetComponent<BoxCollider2D>();
            if (collider == null)
                collider = child.gameObject.AddComponent<BoxCollider2D>();
            area = child.GetComponent<WorldSelectionHitArea>();
            if (area == null)
                area = child.gameObject.AddComponent<WorldSelectionHitArea>();
            area.Configure(target.transform, visual, collider, worldPadding);
            return area;
        }

        public void SyncToVisual(SpriteRenderer visual)
        {
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            if (visual == null || collider == null) return;

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            Bounds worldBounds = visual.bounds;
            Vector3 localMin = transform.InverseTransformPoint(worldBounds.min);
            Vector3 localMax = transform.InverseTransformPoint(worldBounds.max);
            Vector2 min = Vector2.Min(localMin, localMax);
            Vector2 max = Vector2.Max(localMin, localMax);
            Vector3 scale = transform.lossyScale;
            Vector2 localPadding = new(
                selectionPadding / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                selectionPadding / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
            collider.offset = (min + max) * 0.5f;
            collider.size = max - min + localPadding * 2f;
        }

        public void SetSelectionPadding(float worldPadding, SpriteRenderer visual)
        {
            float next = Mathf.Max(0f, worldPadding);
            if (Mathf.Approximately(selectionPadding, next)) return;
            selectionPadding = next;
            SyncToVisual(visual);
        }

        public bool Covers(SpriteRenderer visual)
        {
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            if (visual == null || collider == null) return false;
            Bounds hitBounds = collider.bounds;
            Bounds visualBounds = visual.bounds;
            const float epsilon = 0.01f;
            return hitBounds.min.x <= visualBounds.min.x + epsilon
                && hitBounds.max.x >= visualBounds.max.x - epsilon
                && hitBounds.min.y <= visualBounds.min.y + epsilon
                && hitBounds.max.y >= visualBounds.max.y - epsilon;
        }

        private void Configure(Transform root, SpriteRenderer visual, BoxCollider2D collider,
            float worldPadding)
        {
            targetRoot = root;
            selectionPadding = Mathf.Max(0f, worldPadding);
            if (collider == null) return;
            collider.isTrigger = true;
            SyncToVisual(visual);
        }
    }

    public static class WorldSelectionQuery
    {
        public static T Resolve<T>(Collider2D hit) where T : Component
        {
            if (hit == null) return null;
            T direct = hit.GetComponent<T>();
            if (direct != null) return direct;
            WorldSelectionHitArea area = hit.GetComponent<WorldSelectionHitArea>();
            return area != null && area.TargetRoot != null
                ? area.TargetRoot.GetComponent<T>() : null;
        }

        public static IFriendlyCursorTarget ResolveFriendly(Collider2D hit)
        {
            if (hit == null) return null;
            IFriendlyCursorTarget direct = hit.GetComponent<IFriendlyCursorTarget>();
            if (direct != null) return direct;
            WorldSelectionHitArea area = hit.GetComponent<WorldSelectionHitArea>();
            return area != null && area.TargetRoot != null
                ? area.TargetRoot.GetComponent<IFriendlyCursorTarget>() : null;
        }
    }

    public sealed class GameCursorController : MonoBehaviour
    {
        public const float PresentationScale = 1.5f;

        private enum CursorVisual { Default, Hand, Forbidden }

        [SerializeField] private Camera worldCamera;
        [SerializeField] private SelectionManager selectionManager;
        [SerializeField] private PlayerCommandController commandController;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private Texture2D defaultCursor = null;
        [SerializeField] private Texture2D handCursor = null;
        [SerializeField] private Texture2D forbiddenCursor = null;
        [SerializeField] private Vector2 defaultHotspot = new Vector2(22f, 17f);
        [SerializeField] private Vector2 handHotspot = new Vector2(24f, 18f);
        [SerializeField] private Vector2 forbiddenHotspot = new Vector2(32f, 31f);
        [SerializeField, Range(1f, 2f)] private float cursorScale = PresentationScale;
        [SerializeField, Min(0.05f)] private float validityRefreshSeconds = 0.2f;

        private readonly List<RaycastResult> uiHits = new();
        private readonly Collider2D[] hoverHits = new Collider2D[64];
        private PointerEventData pointerEvent;
        private EventSystem pointerEventSystem;
        private CursorVisual? currentVisual;
        private Vector2Int cachedCell;
        private bool hasCachedCell;
        private bool cachedCanMove;
        private float nextValidityRefresh;
        private BuildingPlacementController placement;
        private Texture2D scaledDefaultCursor;
        private Texture2D scaledHandCursor;
        private Texture2D scaledForbiddenCursor;
        private ContactFilter2D hoverFilter;
        private bool hoverFilterConfigured;

        // The front end uses the same arrow/scaling without gameplay selection systems.
        public void ConfigureDefaultCursor(Texture2D texture)
        {
            DestroyScaledCursor(scaledDefaultCursor);
            defaultCursor = texture;
            scaledDefaultCursor = CreateScaledCursor(defaultCursor);
            currentVisual = null;
            Apply(CursorVisual.Default);
        }

        public bool HoverQueryUsesTriggers
        {
            get { EnsureHoverFilter(); return hoverFilter.useTriggers; }
        }
        public bool HoverQueryIncludesLayer(int layer)
        {
            EnsureHoverFilter();
            return layer >= 0 && layer <= 31 && (!hoverFilter.useLayerMask
                || (hoverFilter.layerMask & (1 << layer)) != 0);
        }

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (selectionManager == null) selectionManager = GetComponent<SelectionManager>();
            if (commandController == null) commandController = GetComponent<PlayerCommandController>();
            if (gridManager == null) gridManager = FindAnyObjectByType<GridManager>();
            placement = GetComponent<BuildingPlacementController>();
            EnsureHoverFilter();
            scaledDefaultCursor = CreateScaledCursor(defaultCursor);
            scaledHandCursor = CreateScaledCursor(handCursor);
            scaledForbiddenCursor = CreateScaledCursor(forbiddenCursor);
            Apply(CursorVisual.Default);
        }

        private void OnEnable()
        {
            if (selectionManager != null)
                selectionManager.SelectionChanged += InvalidateMoveCache;
        }

        private void OnDisable()
        {
            if (selectionManager != null)
                selectionManager.SelectionChanged -= InvalidateMoveCache;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            currentVisual = null;
        }

        private void OnDestroy()
        {
            DestroyScaledCursor(scaledDefaultCursor);
            DestroyScaledCursor(scaledHandCursor);
            DestroyScaledCursor(scaledForbiddenCursor);
        }

        private void Update()
        {
            placement ??= GetComponent<BuildingPlacementController>();
            Mouse mouse = Mouse.current;
            if (mouse == null || worldCamera == null || selectionManager == null)
            {
                Apply(CursorVisual.Default);
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            if (IsOverUI(pointer))
            {
                Apply(CursorVisual.Default);
                return;
            }

            if (placement != null && placement.IsActive)
            {
                Apply(placement.IsValidPreview ? CursorVisual.Hand : CursorVisual.Forbidden);
                return;
            }

            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y,
                -worldCamera.transform.position.z));
            if (commandController != null && commandController.IsAttackMoveTargeting)
            {
                Apply(commandController.CanIssueAttackMoveCommand(world)
                    ? CursorVisual.Hand : CursorVisual.Forbidden);
                return;
            }
            if (IsNormalSelectableHover(pointer, world))
            {
                Apply(CursorVisual.Hand);
                return;
            }

            if (selectionManager.SelectedUnits.Count == 0)
            {
                // Selecting a production building does not enter a targeting mode.
                // Rally points remain a contextual right-click action.
                Apply(CursorVisual.Default);
                return;
            }

            // Enemy hover retains the existing arrow so an Attack target is not
            // presented as a ground Move destination.
            if (commandController != null && commandController.IsEnemyAtScreenPosition(pointer))
            {
                Apply(CursorVisual.Default);
                return;
            }

            if (commandController == null)
            {
                Apply(CursorVisual.Default);
                return;
            }

            Vector2Int hoveredCell = gridManager != null
                ? gridManager.WorldToGrid(world) : default;
            if (!hasCachedCell || hoveredCell != cachedCell || Time.unscaledTime >= nextValidityRefresh)
            {
                cachedCell = hoveredCell;
                hasCachedCell = true;
                nextValidityRefresh = Time.unscaledTime + validityRefreshSeconds;
                cachedCanMove = commandController.CanIssueMoveCommand(world);
            }
            Apply(cachedCanMove ? CursorVisual.Default : CursorVisual.Forbidden);
        }

        private bool IsNormalSelectableHover(Vector2 pointer, Vector3 world)
        {
            if (selectionManager.FindFriendlyUnitAtScreenPosition(pointer) != null)
                return true;

            int hitCount = Physics2D.OverlapPoint(world, hoverFilter, hoverHits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = hoverHits[i];
                IFriendlyCursorTarget friendly = WorldSelectionQuery.ResolveFriendly(hit);
                if (friendly?.IsFriendlyCursorTarget(selectionManager.PlayerFaction) == true)
                    return true;
                Economy.ResourceNode resource = WorldSelectionQuery.Resolve<Economy.ResourceNode>(hit);
                if (resource != null && resource.IsAvailable
                    && (Vision.FogWorldVisibility.Instance == null
                        || Vision.FogWorldVisibility.Instance.CanObserveResource(
                            resource.transform.position))) return true;
            }
            return false;
        }

        private void EnsureHoverFilter()
        {
            if (hoverFilterConfigured) return;
            hoverFilter = ContactFilter2D.noFilter;
            hoverFilter.useTriggers = true;
            hoverFilterConfigured = true;
        }

        private bool IsOverUI(Vector2 pointer)
        {
            EventSystem events = EventSystem.current;
            if (events == null) return false;
            if (pointerEvent == null || pointerEventSystem != events)
            {
                pointerEvent = new PointerEventData(events);
                pointerEventSystem = events;
            }
            pointerEvent.position = pointer;
            uiHits.Clear();
            events.RaycastAll(pointerEvent, uiHits);
            return uiHits.Count > 0;
        }

        private void InvalidateMoveCache() => hasCachedCell = false;

        private void Apply(CursorVisual visual)
        {
            if (currentVisual == visual) return;
            Texture2D texture = visual switch
            {
                CursorVisual.Hand => scaledHandCursor != null ? scaledHandCursor : handCursor,
                CursorVisual.Forbidden => scaledForbiddenCursor != null
                    ? scaledForbiddenCursor : forbiddenCursor,
                _ => scaledDefaultCursor != null ? scaledDefaultCursor : defaultCursor
            };
            Vector2 hotspot = visual switch
            {
                CursorVisual.Hand => handHotspot,
                CursorVisual.Forbidden => forbiddenHotspot,
                _ => defaultHotspot
            };
            if (texture != null && texture != defaultCursor && texture != handCursor
                && texture != forbiddenCursor)
                hotspot *= Mathf.Max(1f, cursorScale);
            Cursor.SetCursor(texture, hotspot, CursorMode.ForceSoftware);
            currentVisual = visual;
        }

        private Texture2D CreateScaledCursor(Texture2D source)
        {
            float scale = Mathf.Max(1f, cursorScale);
            if (source == null || Mathf.Approximately(scale, 1f)) return source;

            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            Color32[] sourcePixels = source.GetPixels32();
            var scaledPixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int sourceY = Mathf.Min(source.height - 1, Mathf.FloorToInt(y / scale));
                int targetRow = y * width;
                int sourceRow = sourceY * source.width;
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Mathf.Min(source.width - 1, Mathf.FloorToInt(x / scale));
                    scaledPixels[targetRow + x] = sourcePixels[sourceRow + sourceX];
                }
            }

            var result = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = source.name + "_CursorScaled",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            result.SetPixels32(scaledPixels);
            result.Apply(false, false);
            return result;
        }

        private static void DestroyScaledCursor(Texture2D texture)
        {
            if (texture != null && texture.name.EndsWith("_CursorScaled"))
                Destroy(texture);
        }
    }
}
