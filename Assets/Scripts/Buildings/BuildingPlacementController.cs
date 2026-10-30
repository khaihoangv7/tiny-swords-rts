using System.Collections.Generic;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TinyKingdom.Match;
using TinyKingdom.Audio;
using TinyKingdom.Economy;
using TinyKingdom.Vision;

namespace TinyKingdom.Buildings
{
    [DefaultExecutionOrder(-200)]
    public sealed class BuildingPlacementController : MonoBehaviour
    {
        private BuildingSystem system;
        private SelectionManager selection;
        private GridManager grid;
        private Camera worldCamera;
        private GameObject previewRoot;
        private SpriteRenderer preview;
        private readonly SpriteRenderer[] outline = new SpriteRenderer[4];
        private static Sprite whitePixel;
        private readonly List<PawnBuilder> builders = new();
        private readonly List<PawnBuilder> pendingBuilders = new();
        private readonly Dictionary<PawnBuilder, Vector2Int> pendingDestinations = new();
        private readonly Dictionary<PawnBuilder, float> pendingCommandTimes = new();
        private readonly HashSet<Vector2Int> triedRevealStages = new();
        private readonly HashSet<PawnBuilder> triedPendingScouts = new();
        private BuildingType? activeType;
        private HouseVariant activeVariant;
        private BuildingType? pendingType;
        private HouseVariant pendingVariant;
        private Vector2Int pendingOrigin;
        private Vector2Int pendingStage;
        private PawnBuilder pendingScout;
        private float pendingStartedAt;
        private float nextPendingCheck;
        private int pendingPathFailures;
        private bool[] pendingObservedCells;
        private int pendingObservedCount;
        private bool pendingStageIssued;
        private int consumedFrame = -1;

        public bool IsActive => activeType.HasValue;
        public BuildingSystem System => system;
        public bool ConsumedInputThisFrame => consumedFrame == Time.frameCount;
        public bool IsValidPreview { get; private set; }

        public void Configure(BuildingSystem authority, SelectionManager selected,
            GridManager navigation, Camera cameraReference)
        {
            system = authority;
            selection = selected;
            grid = navigation;
            worldCamera = cameraReference;
        }

        public bool Begin(BuildingType type, HouseVariant variant = HouseVariant.A)
        {
            if (!MatchManager.AllowsGameplay) return false;
            BuildingData data = system != null ? system.GetData(type) : null;
            if (data == null || !system.CanAfford(type) || selection == null
                || data.GetSprite(selection.PlayerFaction, variant) == null) return false;
            CollectBuilders();
            if (builders.Count == 0) return false;
            if (pendingType.HasValue) CancelPending(true);
            activeType = type;
            activeVariant = variant;
            if (preview == null)
            {
                previewRoot = new GameObject("BuildingPlacementPreview");
                var visual = new GameObject("VisualRoot");
                visual.transform.SetParent(previewRoot.transform, false);
                preview = visual.AddComponent<SpriteRenderer>();
                preview.sortingOrder = 30;
                whitePixel ??= Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                for (int i = 0; i < outline.Length; i++)
                {
                    var edge = new GameObject("FootprintEdge" + i);
                    edge.transform.SetParent(previewRoot.transform, false);
                    outline[i] = edge.AddComponent<SpriteRenderer>();
                    outline[i].sprite = whitePixel;
                    outline[i].sortingOrder = 31;
                }
            }
            preview.sprite = data.GetSprite(selection.PlayerFaction, variant);
            BuildingVisualPresentation.Apply(preview, data.Type, data.VisualOffset);
            previewRoot.SetActive(true);
            return true;
        }

        public void Cancel()
        {
            activeType = null;
            IsValidPreview = false;
            if (previewRoot != null) previewRoot.SetActive(false);
        }

        private void Update()
        {
            if (!MatchManager.AllowsGameplay)
            {
                CancelPending(true);
                Cancel();
                return;
            }
            if (pendingType.HasValue)
            {
                if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                    CancelPending(true);
                else ProcessPending();
            }
            if (GameInputBindings.ModalOpen) return;
            if (!activeType.HasValue || system == null || grid == null || worldCamera == null)
                return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true
                || mouse.rightButton.wasPressedThisFrame)
            {
                consumedFrame = Time.frameCount;
                Cancel();
                return;
            }
            Vector2 pointer = mouse.position.ReadValue();
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y,
                -worldCamera.transform.position.z));
            Vector2Int origin = grid.WorldToGrid(world);
            BuildingData data = system.GetData(activeType.Value);
            previewRoot.transform.position = grid.GridToWorld(origin.x, origin.y)
                + new Vector3((data.Footprint.x - 1) * grid.NavigationData.CellSize * 0.5f,
                    (data.Footprint.y - 1) * grid.NavigationData.CellSize * 0.5f, 0f);
            float width = data.Footprint.x * grid.NavigationData.CellSize;
            float height = data.Footprint.y * grid.NavigationData.CellSize;
            float left = -width * 0.5f;
            float right = width * 0.5f;
            float bottom = -height * 0.5f;
            float top = height * 0.5f;
            SetEdge(0, new Vector2(0.5f * (left + right), bottom), new Vector2(width, 0.035f));
            SetEdge(1, new Vector2(0.5f * (left + right), top), new Vector2(width, 0.035f));
            SetEdge(2, new Vector2(left, 0.5f * (bottom + top)), new Vector2(0.035f, height));
            SetEdge(3, new Vector2(right, 0.5f * (bottom + top)), new Vector2(0.035f, height));
            CollectBuilders();
            bool observed = IsFootprintVisible(data, origin);
            IsValidPreview = !IsOverUI() && (observed
                ? system.CanStartConstruction(activeType.Value, origin, builders)
                : system.CanAfford(activeType.Value) && builders.Count > 0
                    && system.CanPlaceOnKnownTerrain(activeType.Value, origin));
            preview.color = IsValidPreview
                ? new Color(0.45f, 1f, 0.45f, 0.7f)
                : new Color(1f, 0.35f, 0.35f, 0.7f);
            foreach (SpriteRenderer edge in outline)
                edge.color = IsValidPreview ? Color.green : Color.red;
            if (!mouse.leftButton.wasPressedThisFrame) return;
            consumedFrame = Time.frameCount;
            selection.SuppressNextLeftClick();
            if (!IsValidPreview) return;
            if (!observed)
            {
                if (BeginPending(activeType.Value, activeVariant, origin))
                {
                    GameplayAudioService.RequestUi(GameplayAudioEvent.BuildingPlan);
                    activeType = null;
                    IsValidPreview = false;
                    preview.color = new Color(0.9f, 0.85f, 0.52f, 0.58f);
                    foreach (SpriteRenderer edge in outline)
                        edge.color = new Color(1f, 0.82f, 0.3f, 0.9f);
                }
                return;
            }
            if (system.TryBuild(activeType.Value, activeVariant, origin, builders,
                    out BuildingSite site))
            {
                if (selection.PlayerFaction == PlayerViewRelation.PlayerViewFaction)
                {
                    GameplayAudioService.RequestUi(GameplayAudioEvent.BuildingPlan);
                    GetComponent<PlayerCommandController>()
                        ?.AcknowledgeAcceptedBuildingPlacementClick(
                            site != null ? site.ActiveBuilderCount : 1);
                }
                Cancel();
            }
        }

        private bool IsFootprintVisible(BuildingData data, Vector2Int origin)
        {
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            if (vision == null || !vision.IsReady) return false;
            for (int y = 0; y < data.Footprint.y; y++)
            for (int x = 0; x < data.Footprint.x; x++)
                if (!vision.IsVisible(selection.PlayerFaction,
                    grid.GridToWorld(origin.x + x, origin.y + y))) return false;
            return true;
        }

        private bool BeginPending(BuildingType type, HouseVariant variant,
            Vector2Int origin)
        {
            CancelPending(true);
            PawnBuilder closest = null;
            float best = float.PositiveInfinity;
            Vector3 target = grid.GridToWorld(origin.x, origin.y);
            foreach (PawnBuilder pawn in builders)
            {
                if (pawn == null || !pawn.GetComponent<UnitController>().IsAlive) continue;
                float distance = (pawn.transform.position - target).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                closest = pawn;
            }
            if (closest == null) return false;
            pendingType = type;
            pendingVariant = variant;
            pendingOrigin = origin;
            pendingScout = closest;
            pendingBuilders.AddRange(builders);
            pendingDestinations.Clear();
            pendingCommandTimes.Clear();
            // Every selected builder accepts the plan now. One leads the reveal;
            // the others approach separate nearby cells and join construction
            // when the footprint has actually been observed and validated.
            foreach (PawnBuilder pawn in pendingBuilders)
            {
                if (pawn == null) continue;
                pawn.CancelJob();
                pawn.GetComponent<PawnGatherer>()?.CancelJob();
                pawn.GetComponent<UnitMovement>()?.Stop();
            }
            triedPendingScouts.Clear();
            triedPendingScouts.Add(closest);
            BuildingData data = system.GetData(type);
            pendingObservedCells = new bool[data.Footprint.x * data.Footprint.y];
            pendingObservedCount = 0;
            triedRevealStages.Clear();
            pendingStageIssued = false;
            pendingStartedAt = Time.time;
            nextPendingCheck = 0f;
            pendingPathFailures = 0;
            return true;
        }

        private void ProcessPending()
        {
            if (system == null || grid == null || Time.time - pendingStartedAt > 60f)
            {
                CancelPending(true);
                return;
            }
            if (pendingScout == null
                || !pendingScout.GetComponent<UnitController>().IsAlive)
            {
                if (!TryNextPendingScout()) CancelPending(true);
                return;
            }
            UnitMovement movement = pendingScout.GetComponent<UnitMovement>();
            if (movement == null) { CancelPending(false); return; }
            if (movement.IsMoving && pendingStageIssued
                && movement.CommandDestinationCell != pendingStage)
            {
                // A newer player command supersedes the pending construction.
                CancelPending(true);
                return;
            }
            PruneRedirectedBuilders();
            if (Time.time < nextPendingCheck) return;
            nextPendingCheck = Time.time + 0.2f;
            BuildingData data = system.GetData(pendingType.Value);
            if (data == null) { CancelPending(true); return; }
            TeamVisionSystem vision = TeamVisionSystem.Instance;
            ObservePendingFootprint(data, vision);
            if (pendingObservedCount == pendingObservedCells.Length)
            {
                bool built = system.TryBuild(pendingType.Value, pendingVariant,
                    pendingOrigin, pendingBuilders, out _);
                CancelPending(!built);
                return;
            }
            if (movement.IsMoving) return;
            pendingStageIssued = false;
            pendingScout.CancelJob();
            pendingScout.GetComponent<PawnGatherer>()?.CancelJob();
            // A route can initially move away from the plan to use a stair.
            // Try several bounded reveal positions instead of one nearest cell.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (!TryChooseRevealStage(data, vision, out Vector2Int candidate))
                    break;
                triedRevealStages.Add(candidate);
                if (!movement.TryIssueMove(candidate)) continue;
                pendingStage = candidate;
                pendingStageIssued = true;
                pendingPathFailures = 0;
                RememberPendingMove(pendingScout, candidate);
                MovePendingBuildersNear(candidate, data);
                return;
            }
            if (++pendingPathFailures >= 3 && !TryNextPendingScout())
                CancelPending(true);
        }

        private bool TryNextPendingScout()
        {
            PawnBuilder next = null;
            float best = float.PositiveInfinity;
            Vector3 target = grid.GridToWorld(pendingOrigin.x, pendingOrigin.y);
            foreach (PawnBuilder candidate in pendingBuilders)
            {
                if (candidate == null || triedPendingScouts.Contains(candidate)
                    || !candidate.GetComponent<UnitController>().IsAlive) continue;
                float distance = (candidate.transform.position - target).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                next = candidate;
            }
            if (next == null) return false;
            pendingScout = next;
            triedPendingScouts.Add(next);
            triedRevealStages.Clear();
            pendingStageIssued = false;
            pendingPathFailures = 0;
            nextPendingCheck = 0f;
            next.CancelJob();
            next.GetComponent<PawnGatherer>()?.CancelJob();
            next.GetComponent<UnitMovement>()?.Stop();
            return true;
        }

        private void MovePendingBuildersNear(Vector2Int stage, BuildingData data)
        {
            List<GridCell> candidates = DestinationDistributor.FindNearbyDestinations(
                grid, stage, Mathf.Min(64, pendingBuilders.Count * 8), 6, true);
            var reserved = new HashSet<Vector2Int> { stage };
            foreach (PawnBuilder pawn in pendingBuilders)
            {
                if (pawn == null || pawn == pendingScout) continue;
                UnitMovement movement = pawn.GetComponent<UnitMovement>();
                if (movement == null || !pawn.GetComponent<UnitController>().IsAlive)
                    continue;
                foreach (GridCell cell in candidates)
                {
                    Vector2Int destination = new(cell.GridX, cell.GridY);
                    if (reserved.Contains(destination)
                        || destination.x >= pendingOrigin.x
                            && destination.x < pendingOrigin.x + data.Footprint.x
                            && destination.y >= pendingOrigin.y
                            && destination.y < pendingOrigin.y + data.Footprint.y
                        || !movement.TryIssueMove(destination)) continue;
                    reserved.Add(destination);
                    RememberPendingMove(pawn, destination);
                    break;
                }
            }
        }

        private void RememberPendingMove(PawnBuilder pawn, Vector2Int destination)
        {
            pendingDestinations[pawn] = destination;
            pendingCommandTimes[pawn] = pawn.GetComponent<UnitMovement>()
                .LastAcceptedCommandTime;
        }

        private void PruneRedirectedBuilders()
        {
            for (int i = pendingBuilders.Count - 1; i >= 0; i--)
            {
                PawnBuilder pawn = pendingBuilders[i];
                if (pawn == null || pawn == pendingScout) continue;
                UnitMovement movement = pawn.GetComponent<UnitMovement>();
                if (movement == null || !pawn.GetComponent<UnitController>().IsAlive)
                {
                    pendingBuilders.RemoveAt(i);
                    continue;
                }
                if (!pendingDestinations.TryGetValue(pawn, out Vector2Int destination)
                    || !pendingCommandTimes.TryGetValue(pawn, out float issuedAt))
                    continue;
                if (movement.LastAcceptedCommandTime <= issuedAt + 0.0001f
                    && (!movement.IsMoving
                        || movement.CommandDestinationCell == destination)) continue;
                pendingBuilders.RemoveAt(i);
                pendingDestinations.Remove(pawn);
                pendingCommandTimes.Remove(pawn);
            }
        }

        private void ObservePendingFootprint(BuildingData data,
            TeamVisionSystem vision)
        {
            if (vision == null || !vision.IsReady || pendingObservedCells == null)
                return;
            for (int y = 0; y < data.Footprint.y; y++)
            for (int x = 0; x < data.Footprint.x; x++)
            {
                int index = y * data.Footprint.x + x;
                if (pendingObservedCells[index] || !vision.IsVisible(
                        selection.PlayerFaction,
                        grid.GridToWorld(pendingOrigin.x + x, pendingOrigin.y + y)))
                    continue;
                pendingObservedCells[index] = true;
                pendingObservedCount++;
            }
        }

        private bool TryChooseRevealStage(BuildingData data,
            TeamVisionSystem vision, out Vector2Int result)
        {
            result = default;
            if (vision == null || !vision.IsReady) return false;
            float radius = Mathf.Max(0.5f,
                pendingScout.GetComponent<UnitController>().VisionRadius - 0.15f);
            float radiusSquared = radius * radius;
            int margin = Mathf.CeilToInt(radius / grid.NavigationData.CellSize) + 1;
            Vector2Int current = grid.WorldToGrid(pendingScout.transform.position);
            float bestScore = float.NegativeInfinity;
            for (int y = Mathf.Max(0, pendingOrigin.y - margin);
                y < Mathf.Min(grid.Height, pendingOrigin.y + data.Footprint.y + margin); y++)
            for (int x = Mathf.Max(0, pendingOrigin.x - margin);
                x < Mathf.Min(grid.Width, pendingOrigin.x + data.Footprint.x + margin); x++)
            {
                Vector2Int stage = new(x, y);
                if (stage == current || triedRevealStages.Contains(stage)
                    || (x >= pendingOrigin.x && x < pendingOrigin.x + data.Footprint.x
                        && y >= pendingOrigin.y && y < pendingOrigin.y + data.Footprint.y))
                    continue;
                GridCell cell = grid.GetCell(x, y);
                // Candidate ranking uses authored terrain only. Hidden live
                // occupancy is left to the movement command's path validation.
                if (cell == null || !cell.Walkable
                    || TerrainRules.IsConnectorCorridor(cell)
                    || cell.TerrainType is not (TerrainType.FlatGround
                        or TerrainType.ElevatedGround)) continue;
                int revealCount = 0;
                for (int fy = 0; fy < data.Footprint.y; fy++)
                for (int fx = 0; fx < data.Footprint.x; fx++)
                {
                    if (pendingObservedCells[fy * data.Footprint.x + fx]) continue;
                    Vector3 target = grid.GridToWorld(pendingOrigin.x + fx,
                        pendingOrigin.y + fy);
                    if ((cell.WorldPosition - target).sqrMagnitude <= radiusSquared)
                        revealCount++;
                }
                if (revealCount == 0) continue;
                float distance = Vector2.Distance(
                    pendingScout.transform.position, cell.WorldPosition);
                float score = revealCount * 8f - distance;
                if (score <= bestScore) continue;
                bestScore = score;
                result = stage;
            }
            return bestScore > float.NegativeInfinity;
        }

        private void CancelPending(bool stopScout)
        {
            if (stopScout)
            {
                foreach (KeyValuePair<PawnBuilder, Vector2Int> order in pendingDestinations)
                {
                    if (order.Key == null) continue;
                    UnitMovement movement = order.Key.GetComponent<UnitMovement>();
                    if (movement != null && movement.IsMoving
                        && movement.CommandDestinationCell == order.Value
                        && pendingCommandTimes.TryGetValue(order.Key, out float issuedAt)
                        && movement.LastAcceptedCommandTime <= issuedAt + 0.0001f)
                        movement.Stop();
                }
            }
            pendingType = null;
            pendingScout = null;
            pendingBuilders.Clear();
            pendingDestinations.Clear();
            pendingCommandTimes.Clear();
            pendingStage = default;
            pendingStageIssued = false;
            pendingObservedCells = null;
            pendingObservedCount = 0;
            triedRevealStages.Clear();
            triedPendingScouts.Clear();
            if (!activeType.HasValue && previewRoot != null)
                previewRoot.SetActive(false);
        }

        private void CollectBuilders()
        {
            builders.Clear();
            if (selection == null) return;
            foreach (UnitSelection unit in selection.SelectedUnits)
            {
                PawnBuilder pawn = unit != null ? unit.GetComponent<PawnBuilder>() : null;
                if (pawn != null && unit.Controller.IsAlive
                    && unit.Controller.Faction == selection.PlayerFaction)
                    builders.Add(pawn);
            }
        }

        private void SetEdge(int index, Vector2 position, Vector2 size)
        {
            outline[index].transform.localPosition = position;
            outline[index].transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static bool IsOverUI()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void OnDestroy()
        {
            CancelPending(false);
            if (previewRoot != null) Destroy(previewRoot);
        }
    }
}
