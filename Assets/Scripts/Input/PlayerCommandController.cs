using System;
using System.Collections.Generic;
using TinyKingdom.Navigation;
using TinyKingdom.Economy;
using TinyKingdom.Buildings;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using TinyKingdom.Match;
using TinyKingdom.Audio;
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
        [SerializeField, Min(RoleFormationPlanner.MinimumSpacing)]
        private float formationSpacing = RoleFormationPlanner.DefaultSpacing;
        [SerializeField] private FormationType currentFormation = FormationType.Compact;
        [SerializeField] private bool blockWhenPointerIsOverUI = true;

        private readonly List<UnitSelection> commandUnits = new();
        private readonly List<UnitSelection> eligibleCommandUnits = new();
        private readonly List<UnitSelection> militaryFormationUnits = new();
        private readonly List<UnitSelection> nonFormationUnits = new();
        private readonly List<FormationAssignment> formationAssignments = new();
        private readonly HashSet<GridCell> reservedCommandCells = new();
        private readonly List<UnitController> acceptedCommandUnits = new();
        private readonly HumanCommandVoiceFeedback commandVoice = new();
        private bool attackMovePending;
        private bool commandVoiceAllowedForPointerCommand;
        private BuildingPlacementController placement;
        private Vector2 lastFormationForward = Vector2.right;
        private bool hasFormationForward;

        public bool IsAttackMoveTargeting => attackMovePending;
        public float FormationSpacing => formationSpacing;
        public FormationType CurrentFormation => currentFormation;
        public event Action<FormationType> FormationChanged;

        public bool BeginAttackMoveTargeting()
        {
            if (!MatchManager.AllowsGameplay || !HasSelectedCombatUnits()) return false;
            attackMovePending = true;
            return true;
        }

        public bool SelectFormation(FormationType formationType)
        {
            if (!MatchManager.AllowsGameplay || selectionManager == null
                || gridManager == null
                || !Enum.IsDefined(typeof(FormationType), formationType)) return false;

            selectionManager.CopySelectedUnits(commandUnits);
            CopyEligibleCommandUnits(false);
            if (!MilitaryFormationEligibility.HasFormationCommandGroup(
                    militaryFormationUnits.Count)) return false;

            attackMovePending = false;
            placement ??= GetComponent<BuildingPlacementController>();
            placement?.Cancel();
            selectionManager.CancelLeftClickSuppression();
            if (currentFormation != formationType)
            {
                currentFormation = formationType;
                FormationChanged?.Invoke(currentFormation);
            }

            Vector3 center = RoleFormationPlanner.CalculateGroupOrigin(
                militaryFormationUnits);
            BeginAcceptedCommand();
            int issued = IssueFormationCommand(militaryFormationUnits, center, false,
                hasFormationForward ? lastFormationForward : Vector2.right);
            FinishAcceptedCommand(issued);
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
                    if (IssuePointerCommand(() => IssueAttackMoveCommand(ground)) > 0)
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
            if (friendlyHealTarget != null
                && IssuePointerCommand(() => IssueHealCommand(friendlyHealTarget)) > 0)
                return;
            UnitController enemy = FindEnemyAtScreenPosition(pointer);
            if (enemy != null)
                IssuePointerCommand(() => IssueAttackCommand(enemy));
            else if (FindEnemyBuilding(world) is BuildingCombatTarget enemyBuilding)
                IssuePointerCommand(() => IssueAttackCommand(enemyBuilding));
            else if (FindFriendlyGarrison(world) is BuildingGarrison garrison)
            {
                BuildingSite site = garrison.GetComponent<BuildingSite>();
                if (site != null && (site.State == BuildingState.UnderConstruction
                    || site.NeedsRepair))
                    IssuePointerCommand(() => IssueBuildingCommand(site));
                else IssuePointerCommand(() => IssueGarrisonCommand(garrison));
            }
            else if (FindFriendlyBuilding(world) is BuildingSite building)
                IssuePointerCommand(() => IssueBuildingCommand(building));
            else if (FindGoldMine(world) is GoldMineObjective mine)
                IssuePointerCommand(() => IssueGoldMineCommand(mine));
            else if (FindResourceAtScreenPosition(pointer) is ResourceNode node)
                IssuePointerCommand(() => IssueGatherCommand(node));
            else if (IssuePointerCommand(() => IssueMoveCommand(world)) > 0)
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

            BeginAcceptedCommand();
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
                    AcceptCommand(unit.Controller);
                    issued++;
                }
            }
            if (issued > 0) AttackCommandFeedback.Spawn(target.transform);
            return FinishAcceptedCommand(issued);
        }

        public int IssueAttackCommand(BuildingCombatTarget target)
        {
            if (!MatchManager.AllowsGameplay || selectionManager == null
                || target == null || !target.IsTargetable
                || target.Faction == selectionManager.PlayerFaction
                || !CombatVisibility.CanFactionCurrentlyObserveTarget(
                    selectionManager.PlayerFaction, target)) return 0;
            BeginAcceptedCommand();
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
                AcceptCommand(unit.Controller);
                issued++;
            }
            return FinishAcceptedCommand(issued);
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
            BeginAcceptedCommand();
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
                    AcceptCommand(unit.Controller);
                    issued++;
                }
            }
            return FinishAcceptedCommand(issued);
        }

        public int IssueGoldMineCommand(GoldMineObjective mine)
        {
            if (!MatchManager.AllowsGameplay || mine == null || selectionManager == null
                || !CanPlayerObserveResource(mine.transform.position)) return 0;
            BeginAcceptedCommand();
            selectionManager.CopySelectedUnits(commandUnits);
            int issued = 0;
            foreach (UnitSelection selected in commandUnits)
                if (selected != null && selected.Controller.Faction == selectionManager.PlayerFaction
                    && mine.TryEnter(selected.Controller))
                {
                    AcceptCommand(selected.Controller);
                    issued++;
                }
            return FinishAcceptedCommand(issued);
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
            BeginAcceptedCommand();
            selectionManager.CopySelectedUnits(commandUnits);
            int issued = 0;
            foreach (UnitSelection unit in commandUnits)
            {
                MonkHealer healer = unit != null ? unit.GetComponent<MonkHealer>() : null;
                if (healer != null && healer.AssignManual(target))
                {
                    AcceptCommand(unit.Controller);
                    issued++;
                }
            }
            return FinishAcceptedCommand(issued);
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

            BeginAcceptedCommand();
            selectionManager.CopySelectedUnits(commandUnits);
            CopyEligibleCommandUnits(attackMove);
            if (eligibleCommandUnits.Count == 0)
                return 0;

            if (eligibleCommandUnits.Count == 1)
                return FinishAcceptedCommand(IssueSingleGroundCommand(
                    eligibleCommandUnits[0], worldPosition, attackMove));

            reservedCommandCells.Clear();
            if (!MilitaryFormationEligibility.HasFormationCommandGroup(
                    militaryFormationUnits.Count))
                return FinishAcceptedCommand(IssueDistributedGroundCommands(
                    eligibleCommandUnits, worldPosition, attackMove,
                    reservedCommandCells));

            Vector3 origin = RoleFormationPlanner.CalculateGroupOrigin(
                militaryFormationUnits);
            Vector2 delta = (Vector2)(worldPosition - origin);
            Vector2 forward;
            if (delta.sqrMagnitude >= RoleFormationPlanner.DirectionEpsilon
                * RoleFormationPlanner.DirectionEpsilon)
            {
                forward = delta.normalized;
                lastFormationForward = forward;
                hasFormationForward = true;
            }
            else forward = hasFormationForward ? lastFormationForward : Vector2.right;
            int issued = IssueFormationCommand(militaryFormationUnits,
                worldPosition, attackMove, forward);
            for (int i = 0; i < formationAssignments.Count; i++)
                if (formationAssignments[i].Cell != null)
                    reservedCommandCells.Add(formationAssignments[i].Cell);
            if (nonFormationUnits.Count > 0)
                issued += IssueDistributedGroundCommands(nonFormationUnits,
                    worldPosition, attackMove, reservedCommandCells);
            return FinishAcceptedCommand(issued);
        }

        private int IssueFormationCommand(IReadOnlyList<UnitSelection> units,
            Vector3 anchor, bool attackMove, Vector2 forward)
        {
            if (!RoleFormationPlanner.TryPlan(gridManager, units, anchor,
                    formationSpacing, candidateMultiplier, maximumSearchRadius,
                    ConcealHiddenOccupancy(anchor), currentFormation, forward,
                    formationAssignments))
                return 0;

            int issued = 0;
            for (int i = 0; i < formationAssignments.Count; i++)
            {
                FormationAssignment assignment = formationAssignments[i];
                UnitSelection selectedUnit = assignment.Unit;
                Vector2Int destination = new(assignment.Cell.GridX, assignment.Cell.GridY);
                bool accepted = attackMove
                    ? selectedUnit.GetComponent<CombatController>()
                        ?.AttackMove(destination, anchor) == true
                    : selectedUnit.Movement.TryIssueMove(destination, anchor);
                if (!accepted) continue;
                CancelPreviousUnitJob(selectedUnit);
                AcceptCommand(selectedUnit.Controller);
                issued++;
            }

            return issued;
        }

        private int IssueDistributedGroundCommands(IReadOnlyList<UnitSelection> units,
            Vector3 anchor, bool attackMove, HashSet<GridCell> reserved)
        {
            List<GridCell> candidates = FindGroundCandidates(anchor,
                units.Count + reserved.Count);
            int issued = 0;
            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                UnitSelection unit = units[unitIndex];
                int best = -1;
                float bestDistance = float.PositiveInfinity;
                for (int candidateIndex = 0; candidateIndex < candidates.Count;
                    candidateIndex++)
                {
                    GridCell candidate = candidates[candidateIndex];
                    if (reserved.Contains(candidate)) continue;
                    float distance = ((Vector2)(unit.transform.position
                        - candidate.WorldPosition)).sqrMagnitude;
                    if (distance < bestDistance - 0.0001f
                        || Mathf.Abs(distance - bestDistance) <= 0.0001f
                        && IsEarlierCell(candidate, best >= 0 ? candidates[best] : null))
                    {
                        best = candidateIndex;
                        bestDistance = distance;
                    }
                }
                if (best < 0) continue;

                GridCell destinationCell = candidates[best];
                Vector2Int destination = new(destinationCell.GridX,
                    destinationCell.GridY);
                bool accepted = attackMove
                    ? unit.GetComponent<CombatController>()
                        ?.AttackMove(destination, anchor) == true
                    : unit.Movement.TryIssueMove(destination, anchor);
                if (!accepted) continue;
                reserved.Add(destinationCell);
                CancelPreviousUnitJob(unit);
                AcceptCommand(unit.Controller);
                issued++;
            }
            return issued;
        }

        private int IssueSingleGroundCommand(UnitSelection selectedUnit,
            Vector3 worldPosition, bool attackMove)
        {
            List<GridCell> candidates = FindGroundCandidates(worldPosition, 1);
            for (int i = 0; i < candidates.Count; i++)
            {
                GridCell candidate = candidates[i];
                Vector2Int destination = new(candidate.GridX, candidate.GridY);
                bool accepted = attackMove
                    ? selectedUnit.GetComponent<CombatController>()
                        ?.AttackMove(destination, worldPosition) == true
                    : selectedUnit.Movement.TryIssueMove(destination, worldPosition);
                if (!accepted) continue;
                CancelPreviousUnitJob(selectedUnit);
                AcceptCommand(selectedUnit.Controller);
                return 1;
            }
            return 0;
        }

        private void CopyEligibleCommandUnits(bool attackMove)
        {
            eligibleCommandUnits.Clear();
            militaryFormationUnits.Clear();
            nonFormationUnits.Clear();
            for (int i = 0; i < commandUnits.Count; i++)
            {
                UnitSelection unit = commandUnits[i];
                if (unit == null || !unit.Controller.IsAlive
                    || unit.Movement == null
                    || unit.Controller.Faction != selectionManager.PlayerFaction)
                    continue;
                if (attackMove && unit.GetComponent<CombatController>()
                    ?.CombatData?.AutoAcquireTargets != true)
                    continue;
                eligibleCommandUnits.Add(unit);
                if (MilitaryFormationEligibility.IsFormationMilitaryUnit(unit))
                    militaryFormationUnits.Add(unit);
                else nonFormationUnits.Add(unit);
            }
        }

        private static bool IsEarlierCell(GridCell candidate, GridCell current)
        {
            if (current == null) return true;
            int y = candidate.GridY.CompareTo(current.GridY);
            return y < 0 || y == 0 && candidate.GridX < current.GridX;
        }

        private static void CancelPreviousUnitJob(UnitSelection selectedUnit)
        {
            selectedUnit.GetComponent<MonkHealer>()?.CancelExplicitOrder();
            selectedUnit.GetComponent<PawnGatherer>()?.CancelJob();
            selectedUnit.GetComponent<PawnBuilder>()?.CancelJob();
        }

        private List<GridCell> FindGroundCandidates(Vector3 worldPosition, int unitCount)
        {
            Vector2Int center = gridManager.WorldToGrid(worldPosition);
            return DestinationDistributor.FindNearbyDestinations(
                gridManager, center, unitCount * candidateMultiplier, maximumSearchRadius,
                ConcealHiddenOccupancy(center));
        }

        private bool ConcealHiddenOccupancy(Vector3 worldPosition)
            => ConcealHiddenOccupancy(gridManager.WorldToGrid(worldPosition));

        private bool ConcealHiddenOccupancy(Vector2Int center)
        {
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            return vision != null && selectionManager != null
                && vision.GetVisionState(selectionManager.PlayerFaction, center)
                    != VisionState.Visible;
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
            BeginAcceptedCommand();
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
                if (selected != null && target.TryStation(selected.Controller))
                {
                    AcceptCommand(selected.Controller);
                    issued++;
                }
            return FinishAcceptedCommand(issued);
        }

        public int IssueBuildingCommand(BuildingSite site)
        {
            if (!MatchManager.AllowsGameplay) return 0;
            BuildingSystem system = FindAnyObjectByType<BuildingSystem>();
            if (system == null || selectionManager == null || site == null
                || site.Faction != selectionManager.PlayerFaction) return 0;
            BeginAcceptedCommand();
            selectionManager.CopySelectedUnits(commandUnits);
            var pawns = new List<PawnBuilder>();
            foreach (UnitSelection unit in commandUnits)
            {
                PawnBuilder pawn = unit != null ? unit.GetComponent<PawnBuilder>() : null;
                if (pawn != null) pawns.Add(pawn);
            }
            if (!system.TryAssign(site, pawns)) return 0;
            foreach (PawnBuilder pawn in pawns)
                if (pawn != null && pawn.Target == site)
                    AcceptCommand(pawn.GetComponent<UnitController>());
            FinishAcceptedCommand(acceptedCommandUnits.Count);
            return 1;
        }

        private int IssuePointerCommand(Func<int> issue)
        {
            bool previous = commandVoiceAllowedForPointerCommand;
            commandVoiceAllowedForPointerCommand = true;
            try
            {
                return issue != null ? issue() : 0;
            }
            finally
            {
                commandVoiceAllowedForPointerCommand = previous;
            }
        }

        public void AcknowledgeAcceptedBuildingPlacementClick(int acceptedBuilderCount)
        {
            if (acceptedBuilderCount <= 0
                || Mouse.current?.leftButton.wasPressedThisFrame != true) return;
            commandVoice.TryAcknowledge(acceptedBuilderCount, 0, Time.time);
        }

        private void BeginAcceptedCommand() => acceptedCommandUnits.Clear();

        private void AcceptCommand(UnitController unit)
        {
            if (unit != null && unit.IsAlive && !acceptedCommandUnits.Contains(unit))
                acceptedCommandUnits.Add(unit);
        }

        private int FinishAcceptedCommand(int issued)
        {
            if (issued > 0 && commandVoiceAllowedForPointerCommand)
            {
                int armyCombatCount = 0;
                for (int i = 0; i < acceptedCommandUnits.Count; i++)
                {
                    UnitType type = acceptedCommandUnits[i].GetComponent<HealthComponent>()
                        ?.CombatData?.UnitType ?? UnitType.Pawn;
                    if (HumanCommandVoiceFeedback.IsArmyCombatUnit(type))
                        armyCombatCount++;
                }
                commandVoice.TryAcknowledge(acceptedCommandUnits.Count,
                    armyCombatCount, Time.time);
            }
            acceptedCommandUnits.Clear();
            return issued;
        }
    }
}
