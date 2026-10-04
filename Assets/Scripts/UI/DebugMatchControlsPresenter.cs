using System.Collections.Generic;
using TinyKingdom.Audio;
using TinyKingdom.Debugging;
using TinyKingdom.Units;
using TinyKingdom.Vision;
using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    [DisallowMultipleComponent]
    public sealed class DebugMatchControlsPresenter : MonoBehaviour
    {
        public const float PanelWidth = 400f;
        public const float PanelHeight = 58f;
        public const float SafeMargin = 12f;
        public const float FpsReservedTop = 58f;
        public const float HorizontalPadding = 10f;
        public const float ControlSpacing = 6f;
        public const float FogButtonWidth = 116f;
        public const float SpeedButtonWidth = 58f;
        public const float RequiredButtonRowWidth = FogButtonWidth
            + 4f * SpeedButtonWidth + 4f * ControlSpacing;

        private static readonly Color ActiveColor = new(1f, 0.86f, 0.42f, 1f);
        private static readonly Color InactiveColor = Color.white;
        private static readonly Color ActiveMarkerColor = new(1f, 0.72f, 0.18f, 1f);

        private readonly Dictionary<float, ButtonView> speedViews = new();
        private MatchSpeedController speedController;
        private FogRenderer blueFogRenderer;
        private RectTransform root;
        private ButtonView fogView;
        private bool hideBlueFog;
        private Rect lastSafeArea;
        private int lastScreenWidth;
        private int lastScreenHeight;

        private sealed class ButtonView
        {
            public Button Button;
            public Image Background;
            public Image ActiveMarker;
            public Text Label;
        }

        public RectTransform Root => root;
        public MatchSpeedController SpeedController => speedController;
        public FogRenderer BlueFogRenderer => blueFogRenderer;
        public bool IsBlueFogHidden => hideBlueFog;
        public int SpeedButtonCount => speedViews.Count;
        public bool BlocksWorldInput => root != null
            && root.GetComponentInChildren<Image>()?.raycastTarget == true;

        public void Configure(RtsHudAssetCatalog catalog)
        {
            if (catalog == null || !catalog.IsComplete) return;
            speedController = GetComponent<MatchSpeedController>()
                ?? gameObject.AddComponent<MatchSpeedController>();
            if (root != null) Destroy(root.gameObject);
            Build(catalog);
            speedController.SpeedChanged -= HandleSpeedChanged;
            speedController.SpeedChanged += HandleSpeedChanged;
            hideBlueFog = false;
            DebugObserverView.SetEnabled(false);
            ResolveBlueFogRenderer();
            ApplyFogVisibility();
            RefreshSpeedSelection(speedController.DesiredSpeed);
            ApplySafeArea(true);
        }

        private void OnDestroy()
        {
            DebugObserverView.SetEnabled(false);
            if (speedController != null)
                speedController.SpeedChanged -= HandleSpeedChanged;
        }

        private void Update()
        {
            if (blueFogRenderer == null)
            {
                ResolveBlueFogRenderer();
                ApplyFogVisibility();
            }
            ApplySafeArea(false);
        }

        public void SetHideBlueFog(bool hide)
        {
            hideBlueFog = hide;
            DebugObserverView.SetEnabled(hide);
            ApplyFogVisibility();
            RefreshFogButton();
        }

        private void Build(RtsHudAssetCatalog catalog)
        {
            root = RtsUiFactory.Rect("DebugMatchControls", transform);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            root.SetAsLastSibling();
            RtsUiFactory.BuildPanelFrame("DebugPaper", root,
                catalog.SpecialPaper, 0.45f);

            RectTransform content = RtsUiFactory.Rect("Controls", root);
            RtsUiFactory.Stretch(content, HorizontalPadding, 9f,
                HorizontalPadding, 9f);
            HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = ControlSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            fogView = CreateButton("FogToggle", content, catalog.CommandSlot,
                "Fog: ON", FogButtonWidth);
            fogView.Button.onClick.AddListener(() =>
            {
                SetHideBlueFog(!hideBlueFog);
                GameplayAudioService.RequestUi(GameplayAudioEvent.CommandClick);
            });

            IReadOnlyList<float> speeds = MatchSpeedController.SupportedSpeeds;
            for (int i = 0; i < speeds.Count; i++)
            {
                float speed = speeds[i];
                ButtonView view = CreateButton($"Speed{speed:0}", content,
                    catalog.CommandSlot, $"x{speed:0}", SpeedButtonWidth);
                view.Button.onClick.AddListener(() =>
                {
                    if (!speedController.SetSpeed(speed)) return;
                    GameplayAudioService.RequestUi(GameplayAudioEvent.CommandClick);
                    RefreshSpeedSelection(speedController.DesiredSpeed);
                });
                speedViews.Add(speed, view);
            }
            RefreshFogButton();
        }

        private static ButtonView CreateButton(string name, Transform parent,
            Sprite sprite, string value, float width)
        {
            RectTransform rect = RtsUiFactory.Rect(name, parent);
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            Image background = rect.gameObject.AddComponent<Image>();
            background.sprite = sprite;
            background.preserveAspect = false;
            background.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            Text label = RtsUiFactory.Label("Label", rect, value, 15,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RtsUiFactory.Stretch(label.rectTransform, 4f, 3f, 4f, 3f);
            Image marker = RtsUiFactory.Image("ActiveMarker", rect, null, false);
            marker.rectTransform.anchorMin = Vector2.zero;
            marker.rectTransform.anchorMax = new Vector2(1f, 0f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0f);
            marker.rectTransform.offsetMin = new Vector2(8f, 3f);
            marker.rectTransform.offsetMax = new Vector2(-8f, 7f);
            marker.color = ActiveMarkerColor;
            marker.raycastTarget = false;
            marker.enabled = false;
            return new ButtonView
            {
                Button = button,
                Background = background,
                ActiveMarker = marker,
                Label = label
            };
        }

        private void HandleSpeedChanged(float speed) => RefreshSpeedSelection(speed);

        private void RefreshSpeedSelection(float selected)
        {
            foreach (KeyValuePair<float, ButtonView> pair in speedViews)
                ApplyActiveStyle(pair.Value, Mathf.Approximately(pair.Key, selected));
        }

        private void RefreshFogButton()
        {
            if (fogView == null) return;
            fogView.Label.text = hideBlueFog ? "Fog: OFF" : "Fog: ON";
            ApplyActiveStyle(fogView, hideBlueFog);
        }

        private static void ApplyActiveStyle(ButtonView view, bool active)
        {
            if (view == null) return;
            view.Background.color = active ? ActiveColor : InactiveColor;
            view.ActiveMarker.enabled = active;
        }

        private void ResolveBlueFogRenderer()
        {
            FogRenderer[] renderers = FindObjectsByType<FogRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null
                    || renderers[i].DisplayedFaction != Faction.Blue) continue;
                blueFogRenderer = renderers[i];
                return;
            }
        }

        private void ApplyFogVisibility()
        {
            if (blueFogRenderer != null)
                blueFogRenderer.SetFogVisible(!hideBlueFog);
        }

        private void ApplySafeArea(bool force)
        {
            if (root == null) return;
            Rect safeArea = Screen.safeArea;
            if (!force && safeArea == lastSafeArea && Screen.width == lastScreenWidth
                && Screen.height == lastScreenHeight) return;
            lastSafeArea = safeArea;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            Canvas canvas = GetComponent<Canvas>();
            float scale = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
            float rightInset = Mathf.Max(0f, Screen.width - safeArea.xMax) / scale;
            float topInset = Mathf.Max(0f, Screen.height - safeArea.yMax) / scale;
            root.anchoredPosition = new Vector2(-SafeMargin - rightInset,
                -FpsReservedTop - SafeMargin - topInset);
        }
    }
}
