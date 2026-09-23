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

    public sealed class GameCursorController : MonoBehaviour
    {
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
        [SerializeField, Min(0.05f)] private float validityRefreshSeconds = 0.2f;

        private readonly List<RaycastResult> uiHits = new();
        private PointerEventData pointerEvent;
        private EventSystem pointerEventSystem;
        private CursorVisual? currentVisual;
        private Vector2Int cachedCell;
        private bool hasCachedCell;
        private bool cachedCanMove;
        private float nextValidityRefresh;
        private BuildingPlacementController placement;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (selectionManager == null) selectionManager = GetComponent<SelectionManager>();
            if (commandController == null) commandController = GetComponent<PlayerCommandController>();
            if (gridManager == null) gridManager = FindAnyObjectByType<GridManager>();
            placement = GetComponent<BuildingPlacementController>();
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
            if (selectionManager.SelectedUnits.Count == 0)
            {
                Component selected = selectionManager.SelectedBuilding != null
                    ? selectionManager.SelectedBuilding : selectionManager.SelectedCastle;
                if (selected != null && selected.GetComponent<BuildingRallyPoint>() != null)
                {
                    Vector2Int rallyCell = gridManager != null
                        ? gridManager.WorldToGrid(world) : default;
                    if (!hasCachedCell || rallyCell != cachedCell
                        || Time.unscaledTime >= nextValidityRefresh)
                    {
                        cachedCell = rallyCell;
                        hasCachedCell = true;
                        nextValidityRefresh = Time.unscaledTime + validityRefreshSeconds;
                        cachedCanMove = commandController != null
                            && commandController.CanSetSelectedBuildingRallyPoint(world);
                    }
                    Apply(cachedCanMove ? CursorVisual.Hand : CursorVisual.Forbidden);
                    return;
                }
                Apply(IsFriendlyHover(pointer, world) ? CursorVisual.Hand : CursorVisual.Default);
                return;
            }

            // Enemy hover retains the existing arrow so an Attack target is not
            // presented as a ground Move destination.
            if (commandController != null && commandController.IsEnemyAtScreenPosition(pointer))
            {
                Apply(CursorVisual.Default);
                return;
            }

            if (commandController != null && commandController.IsResourceAtScreenPosition(pointer))
            {
                Apply(commandController.CanGatherAtScreenPosition(pointer)
                    ? CursorVisual.Hand : CursorVisual.Default);
                return;
            }

            if (IsFriendlyTargetAt(world))
            {
                Apply(CursorVisual.Hand);
                return;
            }

            if (commandController == null)
            {
                Apply(CursorVisual.Hand);
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
            Apply(cachedCanMove ? CursorVisual.Hand : CursorVisual.Forbidden);
        }

        private bool IsFriendlyHover(Vector2 pointer, Vector3 world)
        {
            if (selectionManager.FindFriendlyUnitAtScreenPosition(pointer) != null)
                return true;

            foreach (Collider2D hit in Physics2D.OverlapPointAll(world))
                if (hit.GetComponent<IFriendlyCursorTarget>()?.IsFriendlyCursorTarget(
                        selectionManager.PlayerFaction) == true)
                    return true;
            return false;
        }

        private bool IsFriendlyTargetAt(Vector3 world)
        {
            foreach (Collider2D hit in Physics2D.OverlapPointAll(world))
                if (hit.GetComponent<IFriendlyCursorTarget>()?.IsFriendlyCursorTarget(
                        selectionManager.PlayerFaction) == true) return true;
            return false;
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
                CursorVisual.Hand => handCursor,
                CursorVisual.Forbidden => forbiddenCursor,
                _ => defaultCursor
            };
            Vector2 hotspot = visual switch
            {
                CursorVisual.Hand => handHotspot,
                CursorVisual.Forbidden => forbiddenHotspot,
                _ => defaultHotspot
            };
            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
            currentVisual = visual;
        }
    }
}
