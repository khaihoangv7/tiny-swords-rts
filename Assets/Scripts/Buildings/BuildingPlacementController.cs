using System.Collections.Generic;
using TinyKingdom.Input;
using TinyKingdom.Navigation;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TinyKingdom.Match;
using TinyKingdom.Audio;
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
        private BuildingType? activeType;
        private HouseVariant activeVariant;
        private int consumedFrame = -1;

        public bool IsActive => activeType.HasValue;
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
                Cancel();
                return;
            }
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
            IsValidPreview = !IsOverUI() && system.CanStartConstruction(activeType.Value,
                origin, builders);
            preview.color = IsValidPreview
                ? new Color(0.45f, 1f, 0.45f, 0.7f)
                : new Color(1f, 0.35f, 0.35f, 0.7f);
            foreach (SpriteRenderer edge in outline)
                edge.color = IsValidPreview ? Color.green : Color.red;
            if (!mouse.leftButton.wasPressedThisFrame) return;
            consumedFrame = Time.frameCount;
            selection.SuppressNextLeftClick();
            if (!IsValidPreview) return;
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
            if (previewRoot != null) Destroy(previewRoot);
        }
    }
}
