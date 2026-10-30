using System;
using System.Collections.Generic;
using TinyKingdom.Units;
using TinyKingdom.UI;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Audio;
using TinyKingdom.Match;
using TinyKingdom.Presentation;
using TinyKingdom.Vision;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TinyKingdom.Input
{
    public sealed class SelectionManager : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Faction playerFaction = Faction.Blue;
        [SerializeField, Min(1f)] private float dragThresholdPixels = 8f;
        [SerializeField] private bool blockWhenPointerIsOverUI = true;
        [SerializeField] private SelectionDragVisual dragVisual;

        private readonly List<UnitSelection> selectedUnits = new();
        private readonly List<BuildingSite> selectedBuildings = new();
        private readonly List<UnitSelection>[] controlGroups =
        {
            new(), new(), new(), new()
        };
        private UnityEngine.Object lastClickedTarget;
        private float lastClickTime = -10f;
        private Vector2 lastClickPosition;
        private int activeControlGroup = -1;
        private const float DoubleClickSeconds = 0.32f;
        private const float DoubleClickPixels = 18f;
        private static readonly GameAction[] GroupActions =
        {
            GameAction.Group1, GameAction.Group2,
            GameAction.Group3, GameAction.Group4
        };
        private static readonly GameAction[] GroupAssignmentActions =
        {
            GameAction.GroupAssign1, GameAction.GroupAssign2,
            GameAction.GroupAssign3, GameAction.GroupAssign4
        };
        private Vector2 dragStart;
        private bool leftPointerActive;
        private bool suppressNextLeftClick;
        private BuildingSite selectedBuilding;
        private CastleDropOff selectedCastle;
        private GoldMineObjective selectedGoldMine;
        private ResourceNode selectedResourceNode;
        private UnitSelection inspectedEnemyUnit;
        private BuildingSite inspectedEnemyBuilding;
        private CastleDropOff inspectedEnemyCastle;

        public IReadOnlyList<UnitSelection> SelectedUnits => selectedUnits;
        public IReadOnlyList<BuildingSite> SelectedBuildings => selectedBuildings;
        public int ActiveControlGroup => activeControlGroup;
        public BuildingSite SelectedBuilding => selectedBuilding;
        public CastleDropOff SelectedCastle => selectedCastle;
        public GoldMineObjective SelectedGoldMine => selectedGoldMine;
        public ResourceNode SelectedResourceNode => selectedResourceNode;
        public UnitSelection InspectedEnemyUnit => inspectedEnemyUnit;
        public BuildingSite InspectedEnemyBuilding => inspectedEnemyBuilding;
        public CastleDropOff InspectedEnemyCastle => inspectedEnemyCastle;
        public Faction PlayerFaction => playerFaction;
        public event Action SelectionChanged;

        public void Configure(Camera cameraReference, Faction faction)
        {
            worldCamera = cameraReference;
            playerFaction = faction;
        }

        public void SuppressNextLeftClick()
        {
            suppressNextLeftClick = true;
            leftPointerActive = false;
            dragVisual?.Hide();
        }

        public void CancelLeftClickSuppression()
        {
            suppressNextLeftClick = false;
            leftPointerActive = false;
            dragVisual?.Hide();
        }

        private void Awake()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (dragVisual == null)
                dragVisual = FindAnyObjectByType<SelectionDragVisual>();
        }

        private void OnDisable()
        {
            leftPointerActive = false;
            dragVisual?.Hide();
        }

        private void Update()
        {
            RemoveInvalidResourceSelection();
            RemoveInvalidEnemyInspection();
            if (GameInputBindings.ModalOpen)
            {
                leftPointerActive = false;
                dragVisual?.Hide();
                return;
            }
            HandleControlGroupKeys();
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                dragVisual?.Hide();
                return;
            }

            if (leftPointerActive && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                leftPointerActive = false;
                dragVisual?.Hide();
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (suppressNextLeftClick)
                {
                    leftPointerActive = false;
                    dragVisual?.Hide();
                    return;
                }
                leftPointerActive = !IsPointerOverUI();
                if (leftPointerActive)
                    dragStart = mouse.position.ReadValue();
                dragVisual?.Hide();
            }

            if (mouse.leftButton.wasReleasedThisFrame && suppressNextLeftClick)
            {
                suppressNextLeftClick = false;
                leftPointerActive = false;
                dragVisual?.Hide();
                return;
            }

            if (leftPointerActive && mouse.leftButton.isPressed)
            {
                Vector2 current = mouse.position.ReadValue();
                if (Vector2.Distance(dragStart, current) >= dragThresholdPixels)
                    dragVisual?.Show(dragStart, current);
                else
                    dragVisual?.Hide();
            }

            if (!mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame)
            {
                leftPointerActive = false;
                dragVisual?.Hide();
            }
            if (!mouse.leftButton.wasReleasedThisFrame || !leftPointerActive)
                return;

            leftPointerActive = false;
            dragVisual?.Hide();
            Vector2 releasePosition = mouse.position.ReadValue();
            bool additive = Keyboard.current != null
                && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

            if (Vector2.Distance(dragStart, releasePosition) >= dragThresholdPixels)
                SelectInScreenRect(CreateScreenRect(dragStart, releasePosition), additive);
            else
                SelectAtScreenPosition(releasePosition, additive);
        }

        public void SelectAtScreenPosition(Vector2 screenPosition, bool additive)
        {
            RemoveInvalidSelections();
            UnitSelection hit = FindFriendlyUnitAtScreenPosition(screenPosition);
            if (hit != null && !additive)
            {
                if (IsDoubleClick(hit, screenPosition))
                {
                    SelectAllUnitsOfType(hit);
                    return;
                }
                RememberClick(hit, screenPosition);
            }
            if (hit == null && !additive && worldCamera != null)
            {
                UnitSelection enemyUnit = FindInspectableEnemyUnitAtScreenPosition(
                    screenPosition);
                if (enemyUnit != null)
                {
                    InspectEnemyUnit(enemyUnit);
                    return;
                }
                Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x,
                    screenPosition.y, -worldCamera.transform.position.z));
                foreach (Collider2D collider in Physics2D.OverlapPointAll(world))
                {
                    BuildingSite building = WorldSelectionQuery.Resolve<BuildingSite>(collider);
                    if (building != null && building.IsFriendlyCursorTarget(playerFaction))
                    {
                        if (IsDoubleClick(building, screenPosition))
                            SelectAllBuildingsOfType(building);
                        else
                        {
                            RememberClick(building, screenPosition);
                            SelectBuilding(building);
                        }
                        PlayBuildingSelection(building.Type);
                        return;
                    }
                    if (CanInspectEnemyBuilding(building))
                    {
                        InspectEnemyBuilding(building);
                        return;
                    }
                    CastleDropOff castle = WorldSelectionQuery.Resolve<CastleDropOff>(collider);
                    if (castle != null && castle.IsFriendlyCursorTarget(playerFaction))
                    {
                        if (IsDoubleClick(castle, screenPosition))
                            SelectAllCastles();
                        else
                        {
                            RememberClick(castle, screenPosition);
                            SelectCastle(castle);
                        }
                        PlayBuildingSelection(BuildingType.Castle);
                        return;
                    }
                    if (CanInspectEnemyCastle(castle))
                    {
                        InspectEnemyCastle(castle);
                        return;
                    }
                    GoldMineObjective mine = WorldSelectionQuery.Resolve<GoldMineObjective>(collider);
                    if (mine != null && CanObserveResource(mine.transform.position))
                    {
                        ClearSelection();
                        selectedGoldMine = mine;
                        mine.SetSelected(true);
                        SelectionChanged?.Invoke();
                        return;
                    }
                    ResourceNode resource = WorldSelectionQuery.Resolve<ResourceNode>(collider);
                    if (resource != null && CanSelectResource(resource))
                    {
                        ClearSelection();
                        selectedResourceNode = resource;
                        resource.SetSelected(true);
                        SelectionChanged?.Invoke();
                        return;
                    }
                }
            }
            if (hit == null) lastClickedTarget = null;
            ApplyClickSelection(hit, additive);
        }

        public void SelectBuilding(BuildingSite building)
        {
            if (building == null || !building.IsFriendlyCursorTarget(playerFaction)) return;
            ClearSelection();
            selectedBuilding = building;
            selectedBuildings.Add(building);
            SetBuildingMarker(building.gameObject, building.PresentationRenderer, true);
            BuildingSelectionPulse.Ensure(building.gameObject,
                building.PresentationRenderer)?.Trigger();
            SelectionChanged?.Invoke();
        }

        public void SelectCastle(CastleDropOff castle)
        {
            if (castle == null || !castle.IsFriendlyCursorTarget(playerFaction)) return;
            ClearSelection();
            selectedCastle = castle;
            SetBuildingMarker(castle.gameObject, castle.PresentationRenderer, true);
            BuildingSelectionPulse.Ensure(castle.gameObject,
                castle.PresentationRenderer)?.Trigger();
            SelectionChanged?.Invoke();
        }

        public void InspectEnemyUnit(UnitSelection unit)
        {
            if (!IsInspectableEnemyUnit(unit)) return;
            ClearSelection();
            inspectedEnemyUnit = unit;
            unit.BecameUnavailable += HandleInspectedEnemyUnavailable;
            unit.SetSelected(true);
            SelectionChanged?.Invoke();
        }

        private void InspectEnemyBuilding(BuildingSite building)
        {
            ClearSelection();
            inspectedEnemyBuilding = building;
            SetBuildingMarker(building.gameObject, building.PresentationRenderer, true);
            SelectionChanged?.Invoke();
        }

        private void InspectEnemyCastle(CastleDropOff castle)
        {
            ClearSelection();
            inspectedEnemyCastle = castle;
            SetBuildingMarker(castle.gameObject, castle.PresentationRenderer, true);
            SelectionChanged?.Invoke();
        }

        private bool IsDoubleClick(UnityEngine.Object target, Vector2 position)
            => target != null && lastClickedTarget == target
                && Time.unscaledTime - lastClickTime <= DoubleClickSeconds
                && Vector2.Distance(position, lastClickPosition) <= DoubleClickPixels;

        private void RememberClick(UnityEngine.Object target, Vector2 position)
        {
            lastClickedTarget = target;
            lastClickTime = Time.unscaledTime;
            lastClickPosition = position;
        }

        private void SelectAllUnitsOfType(UnitSelection target)
        {
            UnitType? type = target.GetComponent<HealthComponent>()?.CombatData?.UnitType;
            if (!type.HasValue) return;
            ClearSelection();
            foreach (UnitSelection unit in UnitSelection.ActiveUnits)
                if (IsFriendly(unit)
                    && unit.GetComponent<HealthComponent>()?.CombatData?.UnitType == type)
                    SetSelected(unit, true);
            lastClickedTarget = null;
        }

        private void SelectAllBuildingsOfType(BuildingSite target)
        {
            ClearSelection();
            foreach (BuildingSite building in BuildingSite.ActiveSites)
            {
                if (building == null || !building.IsFriendlyCursorTarget(playerFaction)
                    || building.Type != target.Type) continue;
                selectedBuildings.Add(building);
                if (selectedBuilding == null) selectedBuilding = building;
                SetBuildingMarker(building.gameObject, building.PresentationRenderer, true);
                BuildingSelectionPulse.Ensure(building.gameObject,
                    building.PresentationRenderer)?.Trigger();
            }
            lastClickedTarget = null;
            SelectionChanged?.Invoke();
        }

        private void SelectAllCastles()
        {
            ClearSelection();
            foreach (CastleDropOff castle in CastleDropOff.Active)
                if (castle != null && castle.IsFriendlyCursorTarget(playerFaction))
                {
                    selectedCastle = castle;
                    SetBuildingMarker(castle.gameObject, castle.PresentationRenderer, true);
                    BuildingSelectionPulse.Ensure(castle.gameObject,
                        castle.PresentationRenderer)?.Trigger();
                }
            lastClickedTarget = null;
            SelectionChanged?.Invoke();
        }

        public void ApplyClickSelection(UnitSelection unit, bool additive)
        {
            activeControlGroup = -1;
            RemoveInvalidSelections();
            if (!IsFriendly(unit))
            {
                if (!additive)
                    ClearSelection();
                return;
            }

            if (inspectedEnemyUnit != null || inspectedEnemyBuilding != null
                || inspectedEnemyCastle != null) ClearSelection();

            if (additive)
                SetSelected(unit, !unit.IsSelected);
            else
            {
                ClearSelection();
                SetSelected(unit, true);
            }
        }

        public void SelectInScreenRect(Rect screenRect, bool additive)
        {
            RemoveInvalidSelections();
            var containedUnits = new List<UnitSelection>();
            foreach (UnitSelection unit in UnitSelection.ActiveUnits)
            {
                if (!IsFriendly(unit))
                    continue;

                Vector3 screenPoint = worldCamera.WorldToScreenPoint(unit.transform.position);
                if (screenPoint.z >= 0f && screenRect.Contains(screenPoint))
                    containedUnits.Add(unit);
            }

            ApplyBoxSelection(containedUnits, additive);
        }

        public void ApplyBoxSelection(IEnumerable<UnitSelection> units, bool additive)
        {
            activeControlGroup = -1;
            RemoveInvalidSelections();
            if (!additive || inspectedEnemyUnit != null
                || inspectedEnemyBuilding != null || inspectedEnemyCastle != null)
                ClearSelection();

            if (units == null)
                return;

            foreach (UnitSelection unit in units)
                if (IsFriendly(unit)) SetSelected(unit, true);
        }

        public void ClearSelection()
        {
            bool changed = selectedUnits.Count > 0 || selectedBuilding != null
                || selectedCastle != null || selectedGoldMine != null
                || selectedResourceNode != null
                || !ReferenceEquals(inspectedEnemyUnit, null)
                || !ReferenceEquals(inspectedEnemyBuilding, null)
                || !ReferenceEquals(inspectedEnemyCastle, null);
            foreach (UnitSelection unit in selectedUnits)
            {
                if (unit == null) continue;
                unit.BecameUnavailable -= HandleUnavailable;
                unit.SetSelected(false);
            }
            selectedUnits.Clear();
            foreach (BuildingSite building in selectedBuildings)
                if (building != null)
                    building.GetComponent<SelectionCursorMarker>()?.SetSelected(false);
            selectedBuildings.Clear();
            activeControlGroup = -1;
            if (selectedCastle != null)
                selectedCastle.GetComponent<SelectionCursorMarker>()?.SetSelected(false);
            if (selectedGoldMine != null) selectedGoldMine.SetSelected(false);
            if (selectedResourceNode != null) selectedResourceNode.SetSelected(false);
            if (inspectedEnemyUnit != null)
            {
                inspectedEnemyUnit.BecameUnavailable -= HandleInspectedEnemyUnavailable;
                inspectedEnemyUnit.SetSelected(false);
            }
            if (inspectedEnemyBuilding != null)
                inspectedEnemyBuilding.GetComponent<SelectionCursorMarker>()?.SetSelected(false);
            if (inspectedEnemyCastle != null)
                inspectedEnemyCastle.GetComponent<SelectionCursorMarker>()?.SetSelected(false);
            selectedBuilding = null;
            selectedCastle = null;
            selectedGoldMine = null;
            selectedResourceNode = null;
            inspectedEnemyUnit = null;
            inspectedEnemyBuilding = null;
            inspectedEnemyCastle = null;
            if (changed) SelectionChanged?.Invoke();
        }

        private static void SetBuildingMarker(GameObject root,
            SpriteRenderer visual, bool selected)
        {
            if (root == null || visual == null) return;
            SelectionCursorMarker marker = root.GetComponent<SelectionCursorMarker>()
                ?? root.AddComponent<SelectionCursorMarker>();
            Vector2Int footprint = root.TryGetComponent(out BuildingSite site)
                ? site.Footprint : root.TryGetComponent(out CastleDropOff castle)
                    ? castle.Footprint : Vector2Int.one;
            float cellSize = GroundSorting.ResolveGrid()?.NavigationData.CellSize ?? 1f;
            float depth = Mathf.Max(0.8f, footprint.y * cellSize);
            float width = Mathf.Max(1.2f, visual.bounds.size.x + 0.22f);
            float frontY = visual.bounds.min.y - root.transform.position.y
                + (root.TryGetComponent<CastleDropOff>(out _) ? 0.14f : -0.10f);
            float centerX = visual.bounds.center.x - root.transform.position.x;
            if (marker.Configure(width, depth, frontY + depth * 0.5f,
                    visual, 1.15f, true, centerX))
                marker.SetSelected(selected);
        }

        public void CopySelectedUnits(List<UnitSelection> destination)
        {
            RemoveInvalidSelections();
            destination.Clear();
            destination.AddRange(selectedUnits);
        }

        public int CopyControlGroup(int groupIndex, List<UnitSelection> destination)
        {
            destination.Clear();
            if (groupIndex < 0 || groupIndex >= controlGroups.Length) return 0;
            List<UnitSelection> group = controlGroups[groupIndex];
            group.RemoveAll(unit => !IsFriendly(unit));
            destination.AddRange(group);
            return destination.Count;
        }

        public bool AssignControlGroup(int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= controlGroups.Length) return false;
            RemoveInvalidSelections();
            if (selectedUnits.Count == 0) return false;
            List<UnitSelection> group = controlGroups[groupIndex];
            group.Clear();
            group.AddRange(selectedUnits);
            activeControlGroup = groupIndex;
            SelectionChanged?.Invoke();
            return true;
        }

        public bool SelectControlGroup(int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= controlGroups.Length) return false;
            List<UnitSelection> group = controlGroups[groupIndex];
            group.RemoveAll(unit => !IsFriendly(unit));
            if (group.Count == 0) return false;
            ClearSelection();
            foreach (UnitSelection unit in group) SetSelected(unit, true);
            activeControlGroup = groupIndex;
            SelectionChanged?.Invoke();
            return true;
        }

        public bool ClearSelectedControlGroup()
        {
            if (activeControlGroup < 0) return false;
            controlGroups[activeControlGroup].Clear();
            activeControlGroup = -1;
            SelectionChanged?.Invoke();
            return true;
        }

        private void HandleControlGroupKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (!MatchManager.AllowsGameplay || keyboard == null) return;
            for (int index = 0; index < controlGroups.Length; index++)
            {
                if (GameInputBindings.WasPressed(GroupAssignmentActions[index]))
                {
                    AssignControlGroup(index);
                    return;
                }
                if (GameInputBindings.WasPressed(GroupActions[index]))
                {
                    SelectControlGroup(index);
                    return;
                }
            }
            if (GameInputBindings.WasPressed(GameAction.Ungroup))
                ClearSelectedControlGroup();
        }

        public UnitSelection FindFriendlyUnitAtScreenPosition(Vector2 screenPosition)
        {
            UnitSelection closest = null;
            float closestDistance = float.MaxValue;
            foreach (UnitSelection unit in UnitSelection.ActiveUnits)
            {
                if (!IsFriendly(unit))
                    continue;

                Vector3 unitScreenPoint = worldCamera.WorldToScreenPoint(unit.transform.position);
                if (unitScreenPoint.z < 0f)
                    continue;

                float distance = Vector2.Distance(screenPosition, unitScreenPoint);
                if (distance <= unit.ClickRadiusPixels && distance < closestDistance)
                {
                    closest = unit;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        public UnitSelection FindInspectableEnemyUnitAtScreenPosition(Vector2 screenPosition)
        {
            if (worldCamera == null) return null;
            UnitSelection closest = null;
            float closestDistance = float.MaxValue;
            foreach (UnitSelection unit in UnitSelection.ActiveUnits)
            {
                if (!IsInspectableEnemyUnit(unit)) continue;
                Vector3 point = worldCamera.WorldToScreenPoint(unit.transform.position);
                if (point.z < 0f) continue;
                float distance = Vector2.Distance(screenPosition, point);
                if (distance > unit.ClickRadiusPixels || distance >= closestDistance) continue;
                closest = unit;
                closestDistance = distance;
            }
            return closest;
        }

        private bool IsInspectableEnemyUnit(UnitSelection unit)
            => unit != null && unit.isActiveAndEnabled && unit.IsWorldSelectable
                && unit.Controller != null && unit.Controller.IsAlive
                && CanInspectEnemy(unit.Controller.Faction, unit.transform.position);

        public bool CanInspectEnemyBuilding(BuildingSite building)
            => building != null && building.isActiveAndEnabled && building.IsAlive
                && CanInspectEnemy(building.Faction,
                    building.GetComponent<BuildingVisionSource>()?.VisionPosition
                        ?? building.transform.position);

        public bool CanInspectEnemyCastle(CastleDropOff castle)
            => castle != null && castle.isActiveAndEnabled && castle.IsAlive
                && CanInspectEnemy(castle.Faction,
                    castle.GetComponent<BuildingVisionSource>()?.VisionPosition
                        ?? castle.transform.position);

        private bool CanInspectEnemy(Faction faction, Vector3 position)
        {
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            if (faction == playerFaction || vision == null || !vision.IsReady) return false;
            return FogWorldVisibility.Instance != null
                ? FogWorldVisibility.Instance.CanObserve(faction, position)
                : vision.IsVisible(playerFaction, position);
        }

        private void RemoveInvalidEnemyInspection()
        {
            if (!ReferenceEquals(inspectedEnemyUnit, null)
                && !IsInspectableEnemyUnit(inspectedEnemyUnit)
                || !ReferenceEquals(inspectedEnemyBuilding, null)
                    && !CanInspectEnemyBuilding(inspectedEnemyBuilding)
                || !ReferenceEquals(inspectedEnemyCastle, null)
                    && !CanInspectEnemyCastle(inspectedEnemyCastle))
                ClearSelection();
        }

        private void HandleInspectedEnemyUnavailable(UnitSelection unit)
        {
            if (unit == inspectedEnemyUnit) ClearSelection();
        }

        private bool IsFriendly(UnitSelection unit)
        {
            return unit != null && unit.isActiveAndEnabled && unit.Controller != null
                && unit.IsWorldSelectable && unit.Controller.IsAlive
                && unit.Controller.Faction == playerFaction;
        }

        private void SetSelected(UnitSelection unit, bool selected)
        {
            if (unit == null)
                return;

            bool wasSelected = selectedUnits.Contains(unit);
            unit.SetSelected(selected);
            if (selected)
            {
                if (!selectedUnits.Contains(unit))
                {
                    selectedUnits.Add(unit);
                    unit.BecameUnavailable += HandleUnavailable;
                }
            }
            else
            {
                selectedUnits.Remove(unit);
                unit.BecameUnavailable -= HandleUnavailable;
            }
            if (wasSelected != selectedUnits.Contains(unit)) SelectionChanged?.Invoke();
        }

        private void RemoveInvalidSelections()
        {
            bool changed = false;
            for (int i = selectedUnits.Count - 1; i >= 0; i--)
            {
                UnitSelection unit = selectedUnits[i];
                if (unit == null || !unit.isActiveAndEnabled || !IsFriendly(unit))
                {
                    if (unit != null) unit.BecameUnavailable -= HandleUnavailable;
                    selectedUnits.RemoveAt(i);
                    changed = true;
                }
            }
            for (int i = selectedBuildings.Count - 1; i >= 0; i--)
            {
                BuildingSite site = selectedBuildings[i];
                if (site != null && site.isActiveAndEnabled && site.IsAlive) continue;
                selectedBuildings.RemoveAt(i);
                changed = true;
            }
            if (selectedBuilding != null && !selectedBuildings.Contains(selectedBuilding))
                selectedBuilding = selectedBuildings.Count > 0 ? selectedBuildings[0] : null;
            if (changed) SelectionChanged?.Invoke();
        }

        private bool CanSelectResource(ResourceNode resource)
            => resource != null && resource.isActiveAndEnabled
                && (!resource.IsDepleted || resource.ResourceType == ResourceType.Wood)
                && CanObserveResource(resource.transform.position);

        private static bool CanObserveResource(Vector3 position)
            => Vision.FogWorldVisibility.Instance == null
                || Vision.FogWorldVisibility.Instance.CanObserveResource(position);

        private void RemoveInvalidResourceSelection()
        {
            if (selectedResourceNode == null) return;
            if (CanSelectResource(selectedResourceNode)) return;
            selectedResourceNode.SetSelected(false);
            selectedResourceNode = null;
            SelectionChanged?.Invoke();
        }

        private void HandleUnavailable(UnitSelection unit)
        {
            if (unit != null) unit.BecameUnavailable -= HandleUnavailable;
            if (selectedUnits.Remove(unit)) SelectionChanged?.Invoke();
        }

        private bool IsPointerOverUI()
        {
            return blockWhenPointerIsOverUI && EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject();
        }

        private static Rect CreateScreenRect(Vector2 start, Vector2 end)
        {
            return Rect.MinMaxRect(
                Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y),
                Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
        }

        private static void PlayBuildingSelection(BuildingType type)
        {
            GameplayAudioEvent? eventId = type switch
            {
                BuildingType.Castle => GameplayAudioEvent.CastleSelect,
                BuildingType.House => GameplayAudioEvent.HouseSelect,
                BuildingType.Barracks => GameplayAudioEvent.BarracksSelect,
                BuildingType.Archery => GameplayAudioEvent.ArcherySelect,
                BuildingType.Monastery => GameplayAudioEvent.MonasterySelect,
                BuildingType.Tower => GameplayAudioEvent.TowerSelect,
                _ => null
            };
            if (eventId.HasValue) GameplayAudioService.RequestUi(eventId.Value);
        }
    }
}
