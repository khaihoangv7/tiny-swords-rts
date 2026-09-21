using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TinyKingdom.Input
{
    public sealed class PlayerCommandController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SelectionManager selectionManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField, Min(1)] private int candidateMultiplier = 4;
        [SerializeField, Min(1)] private int maximumSearchRadius = 8;
        [SerializeField] private bool blockWhenPointerIsOverUI = true;

        private readonly List<UnitSelection> commandUnits = new();

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (selectionManager == null) selectionManager = FindAnyObjectByType<SelectionManager>();
            if (gridManager == null) gridManager = FindAnyObjectByType<GridManager>();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame || IsPointerOverUI())
                return;

            Vector2 pointer = mouse.position.ReadValue();
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y,
                -worldCamera.transform.position.z));
            UnitController enemy = FindEnemyAtScreenPosition(pointer);
            if (enemy != null)
                IssueAttackCommand(enemy);
            else
                IssueMoveCommand(world);
        }

        public int IssueAttackCommand(UnitController target)
        {
            if (selectionManager == null || target == null || !target.IsAlive
                || target.Faction == selectionManager.PlayerFaction)
                return 0;

            selectionManager.CopySelectedUnits(commandUnits);
            int issued = 0;
            foreach (UnitSelection unit in commandUnits)
            {
                if (unit == null || !unit.Controller.IsAlive
                    || unit.Controller.Faction != selectionManager.PlayerFaction)
                    continue;
                CombatController combat = unit.GetComponent<CombatController>();
                if (combat != null && combat.Attack(target))
                    issued++;
            }
            return issued;
        }

        private UnitController FindEnemyAtScreenPosition(Vector2 screenPosition)
        {
            if (worldCamera == null || selectionManager == null)
                return null;
            UnitController closest = null;
            float closestDistance = float.MaxValue;
            foreach (UnitSelection unit in UnitSelection.ActiveUnits)
            {
                if (unit == null || unit.Controller.Faction == selectionManager.PlayerFaction
                    || !unit.Controller.IsAlive)
                    continue;
                Vector3 point = worldCamera.WorldToScreenPoint(unit.transform.position);
                if (point.z < 0f) continue;
                float distance = Vector2.Distance(screenPosition, point);
                if (distance <= unit.ClickRadiusPixels && distance < closestDistance)
                {
                    closest = unit.Controller;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        public int IssueMoveCommand(Vector3 worldPosition)
        {
            if (selectionManager == null || gridManager == null)
                return 0;

            selectionManager.CopySelectedUnits(commandUnits);
            if (commandUnits.Count == 0)
                return 0;

            Vector2Int center = gridManager.WorldToGrid(worldPosition);
            List<GridCell> candidates = DestinationDistributor.FindNearbyDestinations(
                gridManager, center, commandUnits.Count * candidateMultiplier, maximumSearchRadius);
            if (candidates.Count == 0)
                return 0;

            int issued = 0;
            var assigned = new HashSet<GridCell>();
            foreach (UnitSelection selectedUnit in commandUnits)
            {
                if (selectedUnit == null || !selectedUnit.Controller.IsAlive
                    || selectedUnit.Controller.Faction != selectionManager.PlayerFaction)
                    continue;

                foreach (GridCell candidate in candidates)
                {
                    if (assigned.Contains(candidate))
                        continue;
                    if (!selectedUnit.Movement.TryIssueMove(new Vector2Int(candidate.GridX, candidate.GridY)))
                        continue;

                    assigned.Add(candidate);
                    issued++;
                    break;
                }
            }

            return issued;
        }

        public void Configure(Camera cameraReference, SelectionManager selection, GridManager grid)
        {
            worldCamera = cameraReference;
            selectionManager = selection;
            gridManager = grid;
        }

        private bool IsPointerOverUI()
        {
            return blockWhenPointerIsOverUI && EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
