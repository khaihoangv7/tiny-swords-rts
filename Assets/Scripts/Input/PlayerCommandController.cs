using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Economy;
using TinyKingdom.Buildings;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using TinyKingdom.Match;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TinyKingdom.Input
{
    [DefaultExecutionOrder(-150)]
    public sealed class PlayerCommandController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SelectionManager selectionManager;
        [SerializeField] private GridManager gridManager;
        [SerializeField, Min(1)] private int candidateMultiplier = 4;
        [SerializeField, Min(1)] private int maximumSearchRadius = 8;
        [SerializeField] private bool blockWhenPointerIsOverUI = true;

        private readonly List<UnitSelection> commandUnits = new();
        private bool attackMovePending;
        private BuildingPlacementController placement;

        public bool IsAttackMoveTargeting => attackMovePending;

        public bool BeginAttackMoveTargeting()
        {
            if (!MatchManager.AllowsGameplay || !HasSelectedCombatUnits()) return false;
            attackMovePending = true;
            return true;
        }

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (selectionManager == null) selectionManager = FindAnyObjectByType<SelectionManager>();
            if (gridManager == null) gridManager = FindAnyObjectByType<GridManager>();
            placement = GetComponent<BuildingPlacementController>();
        }

        private void OnDisable()
        {
            attackMovePending = false;
            selectionManager?.CancelLeftClickSuppression();
        }

        private void Update()
        {
            placement ??= GetComponent<BuildingPlacementController>();
            if (!MatchManager.AllowsGameplay)
            {
                attackMovePending = false;
                placement?.Cancel();
                selectionManager?.CancelLeftClickSuppression();
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.aKey.wasPressedThisFrame
                && HasSelectedCombatUnits())
            {
                attackMovePending = true;
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                attackMovePending = false;
                selectionManager?.CancelLeftClickSuppression();
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (placement != null && (placement.IsActive || placement.ConsumedInputThisFrame)) return;

            if (attackMovePending && mouse.leftButton.wasPressedThisFrame)
            {
                if (!HasSelectedCombatUnits())
                {
                    attackMovePending = false;
                    return;
                }
                // SelectionManager updates later. Consume this press/release pair so
                // AttackMove targeting never also changes the current selection.
                selectionManager?.SuppressNextLeftClick();
                if (!IsPointerOverUI())
                {
                    Vector2 click = mouse.position.ReadValue();
                    Vector3 ground = worldCamera.ScreenToWorldPoint(new Vector3(click.x, click.y,
                        -worldCamera.transform.position.z));
                    if (IssueAttackMoveCommand(ground) > 0)
                    {
                        AttackMoveCommandFeedback.Spawn(ground);
                        attackMovePending = false;
                    }
                }
                return;
            }

            if (attackMovePending && mouse.rightButton.wasPressedThisFrame)
            {
                // Right Click cancels targeting, then deliberately falls through to
                // the normal contextual command handling for this same click.
                attackMovePending = false;
                selectionManager?.CancelLeftClickSuppression();
            }

            if (!mouse.rightButton.wasPressedThisFrame) return;
            if (IsPointerOverUI()) return;

            Vector2 pointer = mouse.position.ReadValue();
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y,
                -worldCamera.transform.position.z));
            if (selectionManager != null && selectionManager.SelectedUnits.Count == 0
                && SelectedRallyPoint() is BuildingRallyPoint rally)
            {
                if (CanSetSelectedBuildingRallyPoint(world))
                    rally.TrySetRallyPoint(world);
                return;
            }
            HealthComponent friendlyHealTarget = FindFriendlyDamagedUnit(pointer);
            if (friendlyHealTarget != null && IssueHealCommand(friendlyHealTarget) > 0)
                return;
            UnitController enemy = FindEnemyAtScreenPosition(pointer);
            if (enemy != null)
                IssueAttackCommand(enemy);
            else if (FindEnemyBuilding(world) is BuildingCombatTarget enemyBuilding)
                IssueAttackCommand(enemyBuilding);
            else if (FindFriendlyGarrison(world) is BuildingGarrison garrison)
            {
                BuildingSite site = garrison.GetComponent<BuildingSite>();
                if (site != null && (site.State == BuildingState.UnderConstruction
                    || site.NeedsRepair)) IssueBuildingCommand(site);
                else IssueGarrisonCommand(garrison);
            }
            else if (FindFriendlyBuilding(world) is BuildingSite building)
                IssueBuildingCommand(building);
            else if (FindGoldMine(world) is GoldMineObjective mine)
                IssueGoldMineCommand(mine);
            else if (FindResourceAtScreenPosition(pointer) is ResourceNode node)
                IssueGatherCommand(node);
            else if (IssueMoveCommand(world) > 0)
                MoveCommandFeedback.Spawn(world);
        }

        public int IssueAttackCommand(UnitController target)
        {
            if (!MatchManager.AllowsGameplay || selectionManager == null
                || target == null || !target.IsAlive
                || target.Faction == selectionManager.PlayerFaction
                || !CombatVisibility.CanFactionCurrentlyObserveTarget(
                    selectionManager.PlayerFaction, target.GetComponent<HealthComponent>()))
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
                {
                    unit.GetComponent<MonkHealer>()?.CancelExplicitOrder();
                    unit.GetComponent<PawnGatherer>()?.CancelJob();
                    unit.GetComponent<PawnBuilder>()?.CancelJob();
                    issued++;
                }
            }
            if (issued > 0) AttackCommandFeedback.Spawn(target.transform);
            return issued;
        }

        public int IssueAttackCommand(BuildingCombatTarget target)
        {
            if (!MatchManager.AllowsGameplay || selectionManager == null
                || target == null || !target.IsTargetable
                || target.Faction == selectionManager.PlayerFaction
                || !CombatVisibility.CanFactionCurrentlyObserveTarget(
                    selectionManager.PlayerFaction, target)) return 0;
            selectionManager.CopySelectedUnits(commandUnits);
            int issued = 0;
            foreach (UnitSelection unit in commandUnits)
            {
                if (unit == null || !unit.Controller.IsTargetable
                    || unit.Controller.Faction != selectionManager.PlayerFaction) continue;
                CombatController combat = unit.GetComponent<CombatController>();
                if (combat == null || !combat.Attack(target)) continue;
                unit.GetComponent<MonkHealer>()?.CancelExplicitOrder();
                unit.GetComponent<PawnGatherer>()?.CancelJob();
                unit.GetComponent<PawnBuilder>()?.CancelJob();
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
                    || !unit.Controller.IsTargetable
                    || !CanPlayerObserve(unit.Controller.Faction, unit.transform.position))
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

        public bool IsEnemyAtScreenPosition(Vector2 screenPosition)
        {
            if (FindEnemyAtScreenPosition(screenPosition) != null) return true;
            if (worldCamera == null) return false;
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x,
                screenPosition.y, -worldCamera.transform.position.z));
            return FindEnemyBuilding(world) != null;
        }

        public bool CanGatherAtScreenPosition(Vector2 screenPosition)
        {
            ResourceNode node = FindResourceAtScreenPosition(screenPosition);
            if (node == null || node.IsDepleted || selectionManager == null) return false;
            foreach (UnitSelection unit in selectionManager.SelectedUnits)
                if (unit != null && unit.Controller.IsAlive
                    && unit.Controller.Faction == selectionManager.PlayerFaction
                    && unit.GetComponent<PawnGatherer>() != null)
                    return true;
            return false;
        }

        public bool IsResourceAtScreenPosition(Vector2 screenPosition)
            => FindResourceAtScreenPosition(screenPosition) != null;

        public int IssueGatherCommand(ResourceNode target)
        {
            if (!MatchManager.AllowsGameplay || target == null || target.IsDepleted
                || selectionManager == null) return 0;
            selectionManager.CopySelectedUnits(commandUnits);
            int issued = 0;
            foreach (UnitSelection unit in commandUnits)
            {
                if (unit == null || !unit.Controller.IsAlive
                    || unit.Controller.Faction != selectionManager.PlayerFaction) continue;
                PawnGatherer gatherer = unit.GetComponent<PawnGatherer>();
                unit.GetComponent<UnitStationing>()?.CancelPending();
                if (gatherer != null && gatherer.Gather(target))
                {
                    unit.GetComponent<MonkHealer>()?.CancelExplicitOrder();
                    unit.GetComponent<PawnBuilder>()?.CancelJob();
                    issued++;
                }
            }
            return issued;
        }

        public int IssueGoldMineCommand(GoldMineObjective mine)
        {
            if (!MatchManager.AllowsGameplay || mine == null || selectionManager == null
                || !CanPlayerObserveResource(mine.transform.position)) return 0;
            selectionManager.CopySelectedUnits(commandUnits);
            int issued = 0;
            foreach (UnitSelection selected in commandUnits)
                if (selected != null && selected.Controller.Faction == selectionManager.PlayerFaction
                    && mine.TryEnter(selected.Controller)) issued++;
            return issued;
        }

        public bool CanEnterGoldMineAt(Vector3 world)
        {
            GoldMineObjective mine = FindGoldMine(world);
            if (mine == null || selectionManager == null
                || !CanPlayerObserveResource(mine.transform.position)) return false;
            foreach (UnitSelection selected in selectionManager.SelectedUnits)
                if (selected != null && mine.CanAccept(selected.Controller))
                    return true;
            return false;
        }

        private GoldMineObjective FindGoldMine(Vector3 world)
        {
            foreach (Collider2D hit in Physics2D.OverlapPointAll(world))
            {
                GoldMineObjective mine = WorldSelectionQuery.Resolve<GoldMineObjective>(hit);
                if (mine != null) return mine;
            }
            return null;
        }

        private ResourceNode FindResourceAtScreenPosition(Vector2 screenPosition)
        {
            if (worldCamera == null) return null;
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x,
                screenPosition.y, -worldCamera.transform.position.z));
            ResourceNode best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (Collider2D collider in Physics2D.OverlapPointAll(world))
            {
                ResourceNode node = WorldSelectionQuery.Resolve<ResourceNode>(collider);
                if (node == null || node.IsDepleted || !CanPlayerObserveResource(node.transform.position))
                    continue;
                float distance = ((Vector2)(world - node.transform.position)).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = node;
                bestDistance = distance;
            }
            return best;
        }

        public int IssueHealCommand(HealthComponent target)
        {
            if (!MatchManager.AllowsGameplay || target == null || !target.IsAlive
                || target.CurrentHP >= target.MaxHP || selectionManager == null) return 0;
            UnitController ally = target.GetComponent<UnitController>();
            if (ally == null || ally.Faction != selectionManager.PlayerFaction) return 0;
            selectionManager.CopySelectedUnits(commandUnits);
            int issued = 0;
            foreach (UnitSelection unit in commandUnits)
            {
                MonkHealer healer = unit != null ? unit.GetComponent<MonkHealer>() : null;
                if (healer != null && healer.AssignManual(target)) issued++;
            }
            return issued;
        }

        private HealthComponent FindFriendlyDamagedUnit(Vector2 screenPosition)
        {
            if (worldCamera == null || selectionManager == null) return null;
            UnitSelection hit = selectionManager.FindFriendlyUnitAtScreenPosition(screenPosition);
            HealthComponent health = hit != null ? hit.GetComponent<HealthComponent>() : null;
            return health != null && health.IsAlive && health.CurrentHP < health.MaxHP
                ? health : null;
        }

        public int IssueMoveCommand(Vector3 worldPosition)
            => IssueGroundCommand(worldPosition, false);

        public bool CanIssueMoveCommand(Vector3 worldPosition)
            => CanIssueGroundCommand(worldPosition, false);

        public bool CanIssueAttackMoveCommand(Vector3 worldPosition)
            => CanIssueGroundCommand(worldPosition, true);

        private bool CanIssueGroundCommand(Vector3 worldPosition, bool attackMove)
        {
            if (!MatchManager.AllowsGameplay || selectionManager == null || gridManager == null)
                return false;

            selectionManager.CopySelectedUnits(commandUnits);
            if (commandUnits.Count == 0)
                return false;

            List<GridCell> candidates = FindGroundCandidates(worldPosition, commandUnits.Count);
            foreach (UnitSelection unit in commandUnits)
            {
                if (unit == null || !unit.Controller.IsAlive
                    || unit.Controller.Faction != selectionManager.PlayerFaction)
                    continue;
                if (attackMove && unit.GetComponent<CombatController>()
                    ?.CombatData?.AutoAcquireTargets != true)
                    continue;
                foreach (GridCell candidate in candidates)
                    if (unit.Movement.CanIssueMove(new Vector2Int(candidate.GridX, candidate.GridY)))
                        return true;
            }
            return false;
        }

        public int IssueAttackMoveCommand(Vector3 worldPosition)
            => IssueGroundCommand(worldPosition, true);

        public bool CanSetSelectedBuildingRallyPoint(Vector3 worldPosition)
        {
            if (!MatchManager.AllowsGameplay || selectionManager == null
                || selectionManager.SelectedUnits.Count != 0) return false;
            BuildingRallyPoint rally = SelectedRallyPoint();
            return rally != null && rally.CanSetRallyPoint(worldPosition);
        }

        private BuildingRallyPoint SelectedRallyPoint()
        {
            if (selectionManager == null) return null;
            Component selected = selectionManager.SelectedBuilding != null
                ? selectionManager.SelectedBuilding : selectionManager.SelectedCastle;
            return selected != null ? selected.GetComponent<BuildingRallyPoint>() : null;
        }

        private bool HasSelectedCombatUnits()
        {
            if (selectionManager == null) return false;
            foreach (UnitSelection unit in selectionManager.SelectedUnits)
                if (unit != null && unit.Controller.IsAlive
                    && unit.GetComponent<CombatController>()?.CombatData?.AutoAcquireTargets == true)
                    return true;
            return false;
        }

        private int IssueGroundCommand(Vector3 worldPosition, bool attackMove)
        {
            if (!MatchManager.AllowsGameplay || selectionManager == null || gridManager == null)
                return 0;

            selectionManager.CopySelectedUnits(commandUnits);
            if (commandUnits.Count == 0)
                return 0;

            List<GridCell> candidates = FindGroundCandidates(worldPosition, commandUnits.Count);
            if (candidates.Count == 0)
                return 0;

            int issued = 0;
            var assigned = new HashSet<GridCell>();
            foreach (UnitSelection selectedUnit in commandUnits)
            {
                if (selectedUnit == null || !selectedUnit.Controller.IsAlive
                    || selectedUnit.Controller.Faction != selectionManager.PlayerFaction)
                    continue;
                if (attackMove && selectedUnit.GetComponent<CombatController>()
                    ?.CombatData?.AutoAcquireTargets != true)
                    continue;

                foreach (GridCell candidate in candidates)
                {
                    if (assigned.Contains(candidate))
                        continue;
                    Vector2Int destination = new(candidate.GridX, candidate.GridY);
                    bool accepted = attackMove
                        ? selectedUnit.GetComponent<CombatController>()
                            ?.AttackMove(destination, worldPosition) == true
                        : selectedUnit.Movement.TryIssueMove(destination, worldPosition);
                    if (!accepted)
                        continue;

                    selectedUnit.GetComponent<MonkHealer>()?.CancelExplicitOrder();
                    selectedUnit.GetComponent<PawnGatherer>()?.CancelJob();
                    selectedUnit.GetComponent<PawnBuilder>()?.CancelJob();

                    assigned.Add(candidate);
                    issued++;
                    break;
                }
            }

            return issued;
        }

        private List<GridCell> FindGroundCandidates(Vector3 worldPosition, int unitCount)
        {
            Vector2Int center = gridManager.WorldToGrid(worldPosition);
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            bool concealHiddenOccupancy = vision != null && selectionManager != null
                && vision.GetVisionState(selectionManager.PlayerFaction, center)
                    != VisionState.Visible;
            return DestinationDistributor.FindNearbyDestinations(
                gridManager, center, unitCount * candidateMultiplier, maximumSearchRadius,
                concealHiddenOccupancy);
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

        private BuildingSite FindFriendlyBuilding(Vector3 world)
        {
            if (selectionManager == null) return null;
            foreach (Collider2D hit in Physics2D.OverlapPointAll(world))
            {
                BuildingSite site = WorldSelectionQuery.Resolve<BuildingSite>(hit);
                if (site != null && site.IsAlive
                    && site.Faction == selectionManager.PlayerFaction) return site;
            }
            return null;
        }

        private BuildingGarrison FindFriendlyGarrison(Vector3 world)
        {
            if (selectionManager == null) return null;
            foreach (Collider2D hit in Physics2D.OverlapPointAll(world))
            {
                BuildingGarrison garrison = WorldSelectionQuery.Resolve<BuildingGarrison>(hit);
                if (garrison != null && garrison.IsReady
                    && garrison.Faction == selectionManager.PlayerFaction) return garrison;
            }
            return null;
        }

        private BuildingCombatTarget FindEnemyBuilding(Vector3 world)
        {
            if (selectionManager == null) return null;
            foreach (Collider2D hit in Physics2D.OverlapPointAll(world))
            {
                BuildingCombatTarget target = WorldSelectionQuery.Resolve<BuildingCombatTarget>(hit);
                if (target != null && target.IsTargetable
                    && target.Faction != selectionManager.PlayerFaction
                    && CanPlayerObserve(target.Faction, target.transform.position)) return target;
            }
            return null;
        }

        private bool CanPlayerObserve(Faction faction, Vector3 position)
        {
            if (selectionManager == null || faction == selectionManager.PlayerFaction) return true;
            return CombatVisibility.CanFactionCurrentlyObservePosition(
                selectionManager.PlayerFaction, position);
        }

        private bool CanPlayerObserveResource(Vector3 position)
        {
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            return vision == null || selectionManager == null
                || vision.IsVisible(selectionManager.PlayerFaction, position);
        }

        public int IssueGarrisonCommand(BuildingGarrison target)
        {
            if (!MatchManager.AllowsGameplay || target == null || selectionManager == null)
                return 0;
            selectionManager.CopySelectedUnits(commandUnits);
            commandUnits.Sort((a, b) =>
            {
                float da = a == null ? float.PositiveInfinity
                    : ((Vector2)(a.transform.position - target.transform.position)).sqrMagnitude;
                float db = b == null ? float.PositiveInfinity
                    : ((Vector2)(b.transform.position - target.transform.position)).sqrMagnitude;
                int distance = da.CompareTo(db);
                return distance != 0 ? distance
                    : string.CompareOrdinal(a != null ? a.name : string.Empty,
                        b != null ? b.name : string.Empty);
            });
            int issued = 0;
            foreach (UnitSelection selected in commandUnits)
                if (selected != null && target.TryStation(selected.Controller)) issued++;
            return issued;
        }

        public int IssueBuildingCommand(BuildingSite site)
        {
            if (!MatchManager.AllowsGameplay) return 0;
            BuildingSystem system = FindAnyObjectByType<BuildingSystem>();
            if (system == null || selectionManager == null || site == null
                || site.Faction != selectionManager.PlayerFaction) return 0;
            selectionManager.CopySelectedUnits(commandUnits);
            var pawns = new List<PawnBuilder>();
            foreach (UnitSelection unit in commandUnits)
            {
                PawnBuilder pawn = unit != null ? unit.GetComponent<PawnBuilder>() : null;
                if (pawn != null) pawns.Add(pawn);
            }
            return system.TryAssign(site, pawns) ? 1 : 0;
        }
    }
}
