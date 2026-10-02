using System;
using System.Collections.Generic;
using TinyKingdom.Units;
using TinyKingdom.UI;
using TinyKingdom.Buildings;
using TinyKingdom.Economy;
using TinyKingdom.Audio;
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
        private Vector2 dragStart;
        private bool leftPointerActive;
        private bool suppressNextLeftClick;
        private BuildingSite selectedBuilding;
        private CastleDropOff selectedCastle;
        private GoldMineObjective selectedGoldMine;
        private ResourceNode selectedResourceNode;

        public IReadOnlyList<UnitSelection> SelectedUnits => selectedUnits;
        public BuildingSite SelectedBuilding => selectedBuilding;
        public CastleDropOff SelectedCastle => selectedCastle;
        public GoldMineObjective SelectedGoldMine => selectedGoldMine;
        public ResourceNode SelectedResourceNode => selectedResourceNode;
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
            if (hit == null && !additive && worldCamera != null)
            {
                Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x,
                    screenPosition.y, -worldCamera.transform.position.z));
                foreach (Collider2D collider in Physics2D.OverlapPointAll(world))
                {
                    BuildingSite building = WorldSelectionQuery.Resolve<BuildingSite>(collider);
                    if (building != null && building.IsFriendlyCursorTarget(playerFaction))
                    {
                        ClearSelection();
                        selectedBuilding = building;
                        BuildingSelectionPulse.Ensure(building.gameObject,
                            building.PresentationRenderer)?.Trigger();
                        SelectionChanged?.Invoke();
                        PlayBuildingSelection(building.Type);
                        return;
                    }
                    CastleDropOff castle = WorldSelectionQuery.Resolve<CastleDropOff>(collider);
                    if (castle != null && castle.IsFriendlyCursorTarget(playerFaction))
                    {
                        ClearSelection();
                        selectedCastle = castle;
                        BuildingSelectionPulse.Ensure(castle.gameObject,
                            castle.PresentationRenderer)?.Trigger();
                        SelectionChanged?.Invoke();
                        PlayBuildingSelection(BuildingType.Castle);
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
            ApplyClickSelection(hit, additive);
        }

        public void ApplyClickSelection(UnitSelection unit, bool additive)
        {
            RemoveInvalidSelections();
            if (!IsFriendly(unit))
            {
                if (!additive)
                    ClearSelection();
                return;
            }

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
            RemoveInvalidSelections();
            if (!additive)
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
                || selectedResourceNode != null;
            foreach (UnitSelection unit in selectedUnits)
            {
                if (unit == null) continue;
                unit.BecameUnavailable -= HandleUnavailable;
                unit.SetSelected(false);
            }
            selectedUnits.Clear();
            if (selectedGoldMine != null) selectedGoldMine.SetSelected(false);
            if (selectedResourceNode != null) selectedResourceNode.SetSelected(false);
            selectedBuilding = null;
            selectedCastle = null;
            selectedGoldMine = null;
            selectedResourceNode = null;
            if (changed) SelectionChanged?.Invoke();
        }

        public void CopySelectedUnits(List<UnitSelection> destination)
        {
            RemoveInvalidSelections();
            destination.Clear();
            destination.AddRange(selectedUnits);
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

        private bool IsFriendly(UnitSelection unit)
        {
            return unit != null && unit.isActiveAndEnabled && unit.Controller != null
                && unit.Controller.IsAlive && unit.Controller.Faction == playerFaction;
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
