using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;

namespace TinyKingdom.Economy
{
    // Lightweight deterministic ambient presentation. ResourceNode remains authoritative.
    public sealed class SheepPresentation : MonoBehaviour
    {
        private enum VisualState { Idle, Grass, Moving }

        [SerializeField, Min(0f)] private float wanderRadius = 1.75f;
        [SerializeField, Min(0.05f)] private float moveSpeed = 0.45f;
        [SerializeField, Min(0.1f)] private float stationarySeconds = 2.4f;
        [SerializeField, Min(1f)] private float framesPerSecond = 10f;

        private static readonly Vector2Int[] directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        private ResourceNode node;
        private ResourceInteractionSlots slots;
        private SpriteRenderer renderer;
        private Sprite[] idle;
        private Sprite[] grass;
        private Sprite[] move;
        private GridManager grid;
        private Vector2Int originCell;
        private Vector3 destination;
        private VisualState state;
        private float stateTimer;
        private float frameTimer;
        private int frame;
        private int directionIndex;
        private bool configured;
        private bool evacuatingBuildingFootprint;
        private Vector2Int evacuationCell;

        public bool IsEvacuatingBuildingFootprint => evacuatingBuildingFootprint;

        public void Configure(ResourceNode source, SpriteRenderer visual,
            Sprite[] idleFrames, Sprite[] grassFrames, Sprite[] moveFrames)
        {
            node = source;
            slots = GetComponent<ResourceInteractionSlots>();
            renderer = visual;
            idle = idleFrames;
            grass = grassFrames;
            move = moveFrames;
            configured = node != null && renderer != null && idle?.Length > 0
                && grass?.Length > 0 && move?.Length > 0;
            SetState(VisualState.Idle);
        }

        private void Start()
        {
            grid = FindAnyObjectByType<GridManager>();
            if (grid != null) originCell = grid.WorldToGrid(transform.position);
        }

        private void Update()
        {
            if (!configured || node == null || !node.IsAvailable) return;
            if (!evacuatingBuildingFootprint && slots != null && slots.ReservationCount > 0)
            {
                StopForHarvest();
                Animate(Time.deltaTime);
                return;
            }

            if (state == VisualState.Moving)
            {
                transform.position = Vector3.MoveTowards(transform.position, destination,
                    moveSpeed * Time.deltaTime);
                if ((transform.position - destination).sqrMagnitude <= 0.0001f)
                {
                    transform.position = destination;
                    if (evacuatingBuildingFootprint)
                    {
                        evacuatingBuildingFootprint = false;
                        originCell = evacuationCell;
                    }
                    SetState(directionIndex % 2 == 0 ? VisualState.Idle : VisualState.Grass);
                }
            }
            else
            {
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f && !TryBeginWander())
                    SetState(state == VisualState.Idle ? VisualState.Grass : VisualState.Idle);
            }
            Animate(Time.deltaTime);
        }

        public void StopForHarvest()
        {
            if (!configured || evacuatingBuildingFootprint
                || state != VisualState.Moving) return;
            node.SnapToOccupiedCell();
            SetState(VisualState.Idle);
        }

        public bool TryEvacuateBuildingFootprint(Vector2Int safeCell)
        {
            if (!configured || node == null || !node.IsAvailable) return false;
            grid ??= FindAnyObjectByType<GridManager>();
            if (grid == null || !node.TryMoveOccupancy(safeCell, out destination))
                return false;
            slots?.ReleaseAll();
            evacuationCell = safeCell;
            evacuatingBuildingFootprint = true;
            renderer.flipX = destination.x < transform.position.x;
            SetState(VisualState.Moving);
            return true;
        }

        private bool TryBeginWander()
        {
            if (grid == null || wanderRadius < 1f) return false;
            if (node.OccupiedCell != originCell)
            {
                if (ContainsLivingUnit(originCell)) return false;
                if (!node.TryMoveOccupancy(originCell, out destination)) return false;
                renderer.flipX = destination.x < transform.position.x;
                SetState(VisualState.Moving);
                return true;
            }
            for (int attempt = 0; attempt < directions.Length; attempt++)
            {
                Vector2Int offset = directions[directionIndex++ % directions.Length];
                Vector2Int candidate = originCell + offset;
                Vector3 origin = grid.GridToWorld(originCell.x, originCell.y);
                Vector3 candidateWorld = grid.GridToWorld(candidate.x, candidate.y);
                if (Vector2.Distance(origin, candidateWorld) > wanderRadius
                    || ContainsLivingUnit(candidate)
                    || !node.TryMoveOccupancy(candidate, out destination)) continue;
                renderer.flipX = destination.x < transform.position.x;
                SetState(VisualState.Moving);
                return true;
            }
            return false;
        }

        private bool ContainsLivingUnit(Vector2Int candidate)
        {
            foreach (UnitController unit in UnitController.ActiveUnits)
            {
                if (unit != null && unit.IsAlive
                    && grid.WorldToGrid(unit.transform.position) == candidate)
                    return true;
            }
            return false;
        }

        private void SetState(VisualState next)
        {
            state = next;
            stateTimer = stationarySeconds;
            frameTimer = 0f;
            frame = 0;
            ShowFrame();
        }

        private void Animate(float deltaTime)
        {
            Sprite[] frames = CurrentFrames();
            if (frames == null || frames.Length < 2) return;
            frameTimer += deltaTime;
            float duration = 1f / framesPerSecond;
            while (frameTimer >= duration)
            {
                frameTimer -= duration;
                frame = (frame + 1) % frames.Length;
                ShowFrame();
            }
        }

        private void ShowFrame()
        {
            Sprite[] frames = CurrentFrames();
            if (frames == null || frames.Length == 0) return;
            node.SetPresentationSprite(frames[Mathf.Clamp(frame, 0, frames.Length - 1)]);
        }

        private Sprite[] CurrentFrames() => state switch
        {
            VisualState.Grass => grass,
            VisualState.Moving => move,
            _ => idle
        };
    }
}
