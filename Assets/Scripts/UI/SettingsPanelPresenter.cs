using System;
using System.Collections;
using TinyKingdom.Audio;
using TinyKingdom.Debugging;
using TinyKingdom.Economy;
using TinyKingdom.Input;
using TinyKingdom.Units;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    public sealed class SettingsPanelPresenter : MonoBehaviour
    {
        private static readonly string[] TabNames =
        {
            "Tổng quan", "Điều khiển", "Đồ họa", "Âm thanh",
            "Nội dung thêm"
        };
        private static readonly int[] FrameRates = { 30, 60, 120, -1 };
        private static readonly GameAction[] ControlActions =
        {
            GameAction.AttackMove, GameAction.Guard,
            GameAction.GroupAssign1, GameAction.Group1,
            GameAction.GroupAssign2, GameAction.Group2,
            GameAction.GroupAssign3, GameAction.Group3,
            GameAction.GroupAssign4, GameAction.Group4,
            GameAction.Ungroup,
            GameAction.Speed1, GameAction.Speed2,
            GameAction.Speed5, GameAction.Speed10,
            GameAction.PanLeft, GameAction.PanRight,
            GameAction.PanUp, GameAction.PanDown
        };
        private static Sprite roundedSliderSprite;
        private static Sprite circularHandleSprite;
        private SelectionManager selection;
        private ResourceStockpile stockpile;
        private DebugMatchControlsPresenter debugControls;
        private RtsHudAssetCatalog assets;
        private RectTransform modal;
        private RectTransform panel;
        private RectTransform iconsRoot;
        private RectTransform content;
        private Text status;
        private readonly Button[] tabButtons = new Button[5];
        private GameAction? awaiting;
        private int currentTab;
        private float controlsScrollPosition = 1f;
        private bool displayChangePending;
        private int previousWidth;
        private int previousHeight;
        private bool previousFullscreen;
        private int requestedWidth;
        private int requestedHeight;
        private bool requestedFullscreen;
        private float displayRevertAt;

        public bool IsOpen => modal != null && modal.gameObject.activeSelf;

        public void Configure(SelectionManager manager, ResourceStockpile resources,
            DebugMatchControlsPresenter debug, RtsHudAssetCatalog catalog)
        {
            selection = manager;
            stockpile = resources;
            debugControls = debug;
            assets = catalog;
            if (modal != null) Destroy(modal.gameObject);
            Transform oldIcons = transform.Find("SettingsIcons");
            if (oldIcons != null) Destroy(oldIcons.gameObject);
            ApplySavedDisplayAndGraphics();
            BuildIcons();
            BuildModal();
            Close();
        }

        public void ConfigureForMainMenu(RtsHudAssetCatalog catalog)
        {
            assets = catalog;
            ApplySavedDisplayAndGraphics();
            BuildModal();
            Close();
        }

        private void BuildIcons()
        {
            RectTransform icons = RtsUiFactory.Rect("SettingsIcons", transform);
            iconsRoot = icons;
            RtsUiFactory.Anchor(icons, Vector2.one, Vector2.one,
                new Vector2(-16f, -15f), new Vector2(150f, 46f));
            Sprite[] sprites = { assets.InfoIcon, assets.MusicIcon, assets.SettingsIcon };
            for (int i = 0; i < sprites.Length; i++)
            {
                Image icon = RtsUiFactory.Image("Icon" + i, icons, sprites[i]);
                RtsUiFactory.Anchor(icon.rectTransform, Vector2.zero, Vector2.zero,
                    new Vector2(i * 50f, 0f), new Vector2(44f, 44f));
                icon.raycastTarget = true;
                Button button = icon.gameObject.AddComponent<Button>();
                button.targetGraphic = icon;
                int tab = i == 0 ? 4 : i == 1 ? 3 : -1;
                button.onClick.AddListener(() =>
                {
                    GameplayAudioService.RequestUi(GameplayAudioEvent.CommandClick);
                    if (tab >= 0) currentTab = tab;
                    Open();
                });
            }
        }

        private void BuildModal()
        {
            modal = RtsUiFactory.Rect("SettingsModal", transform);
            RtsUiFactory.Stretch(modal);
            Image shade = modal.gameObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.65f);
            shade.raycastTarget = true;
            modal.SetAsLastSibling();
            panel = RtsUiFactory.Rect("SettingsPanel", modal);
            RtsUiFactory.Anchor(panel, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1200f, 680f));
            RtsUiFactory.BuildPanelFrame("Banner", panel, assets.Banner, 0.9f, true);
            AddInset("NavigationSurface", new Vector2(20f, -96f),
                new Vector2(266f, 534f), new Color(0.22f, 0.32f, 0.35f, 0.17f));
            AddInset("ContentSurface", new Vector2(294f, -96f),
                new Vector2(874f, 538f), new Color(1f, 0.98f, 0.83f, 0.32f));
            Label(panel, "NAVIGATION", 19,
                new Vector2(52f, -90f), new Vector2(195f, 22f));
            RectTransform titleRibbon = RtsUiFactory.Rect("TitleRibbon", panel);
            RtsUiFactory.Anchor(titleRibbon, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -20f),
                new Vector2(420f, 78f));
            RtsUiFactory.BuildRibbon("Title", titleRibbon, assets.BigRibbons,
                RibbonStart(true), 0.74f);
            Text heading = RtsUiFactory.Label("Heading", panel, "SETTINGS", 31,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RtsUiFactory.Anchor(heading.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -32f),
                new Vector2(350f, 38f));
            StyleText(heading);
            CreateButton(panel, "X", new Vector2(1110f, -25f),
                new Vector2(46f, 42f), Close);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int tab = i;
                tabButtons[i] = CreateRibbonButton(panel, TabNames[i],
                    new Vector2(20f, -118f - i * 104f),
                    new Vector2(260f, 76f), () => ShowTab(tab), true);
                tabButtons[i].gameObject.name = "Tab" + i;
            }
            content = RtsUiFactory.Rect("TabContent", panel);
            RtsUiFactory.Anchor(content, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(300f, -108f),
                new Vector2(848f, 526f));
            status = RtsUiFactory.Label("Status", panel, "", 16,
                TextAnchor.MiddleLeft);
            StyleText(status);
            RtsUiFactory.Anchor(status.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(300f, -77f),
                new Vector2(815f, 24f));
            Label(panel, "Tiny Kingdom RL  |  Unity 2D RTS", 15,
                new Vector2(316f, -642f), new Vector2(600f, 20f));
            FitPanelToViewport();
        }

        public void Open()
        {
            if (modal == null) return;
            modal.gameObject.SetActive(true);
            modal.SetAsLastSibling();
            FitPanelToViewport();
            GameInputBindings.ModalOpen = true;
            status.text = "";
            ShowTab(currentTab);
        }

        public void Close()
        {
            if (displayChangePending) RevertDisplayChange();
            awaiting = null;
            GameInputBindings.Capturing = false;
            GameInputBindings.ModalOpen = false;
            if (IsOpen) PlayerPrefs.Save();
            if (modal != null) modal.gameObject.SetActive(false);
        }

        private void OnDisable() => Close();
        private void OnDestroy() => Close();

        private void Update()
        {
            if (IsOpen) FitPanelToViewport();
            if (iconsRoot != null)
                iconsRoot.anchorMin = iconsRoot.anchorMax =
                    RtsUiFactory.SafeCorner(true, true);
            if (displayChangePending)
            {
                if (Time.unscaledTime >= displayRevertAt)
                {
                    RevertDisplayChange();
                    status.text = "Display change reverted after 15 seconds.";
                    if (IsOpen && currentTab == 2) ShowTab(2);
                }
                else if (IsOpen && currentTab == 2)
                    status.text = "Keep display settings within "
                        + Mathf.CeilToInt(displayRevertAt - Time.unscaledTime) + "s.";
            }
            if (!IsOpen || Keyboard.current == null) return;
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (awaiting.HasValue)
                {
                    awaiting = null;
                    GameInputBindings.Capturing = false;
                    status.text = "Binding cancelled.";
                    ShowTab(1);
                }
                else Close();
                return;
            }
            if (!awaiting.HasValue) return;
            foreach (KeyControl control in Keyboard.current.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;
                if (control.keyCode is Key.LeftShift or Key.RightShift
                    or Key.LeftCtrl or Key.RightCtrl
                    or Key.LeftAlt or Key.RightAlt)
                    continue;
                GameAction action = awaiting.Value;
                awaiting = null;
                GameInputBindings.Capturing = false;
                bool accepted = GameInputBindings.TrySet(action, control.keyCode,
                    Keyboard.current.shiftKey.isPressed,
                    Keyboard.current.ctrlKey.isPressed,
                    out string reason);
                status.text = accepted
                    ? $"{GameInputBindings.Label(action)}: {GameInputBindings.Display(action)}"
                    : reason;
                ShowTab(1);
                return;
            }
        }

        private void ShowTab(int tab)
        {
            if (content == null) return;
            currentTab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
            for (int i = 0; i < tabButtons.Length; i++)
                if (tabButtons[i] != null)
                    TintRibbon(tabButtons[i], i == currentTab
                        ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f));
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            Label(content, currentTab == 4 ? "About the project" : TabNames[currentTab], 29,
                new Vector2(8f, 0f), new Vector2(810f, 38f));
            AddContentRule();
            switch (currentTab)
            {
                case 0: BuildOverview(); break;
                case 1: BuildControls(); break;
                case 2: BuildGraphics(); break;
                case 3: BuildAudio(); break;
                case 4: BuildCredits(); break;
            }
        }

        private void BuildOverview()
        {
            Label(content, "Language", 24,
                new Vector2(12f, -54f), new Vector2(340f, 34f));
            Button english = CreateButton(content, "English",
                new Vector2(12f, -96f), new Vector2(170f, 52f), () =>
                {
                    UiLanguage.Set(GameUiLanguage.English);
                    ShowTab(0);
                });
            Button vietnamese = CreateButton(content, "Vietnamese",
                new Vector2(196f, -96f), new Vector2(170f, 52f), () =>
                {
                    UiLanguage.Set(GameUiLanguage.Vietnamese);
                    ShowTab(0);
                });
            if (UiLanguage.Current == GameUiLanguage.English)
                TintRibbon(vietnamese, new Color(0.68f, 0.68f, 0.68f));
            else TintRibbon(english, new Color(0.68f, 0.68f, 0.68f));
            Label(content, "Gameplay", 24,
                new Vector2(12f, -177f), new Vector2(340f, 34f));
            CreateButton(content, "Damage text: "
                + (DamageTextEmitter.GlobalEnabled ? "ON" : "OFF"),
                new Vector2(12f, -219f), new Vector2(320f, 54f), () =>
                {
                    DamageTextEmitter.GlobalEnabled = !DamageTextEmitter.GlobalEnabled;
                    ShowTab(0);
                });
            Label(content, "Show damage numbers during combat", 16,
                new Vector2(12f, -277f), new Vector2(380f, 28f));
            if (debugControls == null)
            {
                Label(content, "Match controls are available during a game.", 18,
                    new Vector2(430f, -96f), new Vector2(390f, 100f));
                return;
            }
            Label(content, "Debug tools", 24,
                new Vector2(430f, -50f), new Vector2(355f, 34f));
            CreateButton(content, "Allow Cheats: "
                + (CheatAccess.Allowed ? "ON" : "OFF"),
                new Vector2(430f, -94f), new Vector2(320f, 54f), () =>
                {
                    CheatAccess.Allowed = !CheatAccess.Allowed;
                    ShowTab(0);
                });
            Label(content, "Match speed", 23,
                new Vector2(12f, -330f), new Vector2(340f, 32f));
            var speeds = Debugging.MatchSpeedController.SupportedSpeeds;
            for (int i = 0; i < speeds.Count; i++)
            {
                float speed = speeds[i];
                string title = "x" + speed.ToString("0");
                if (Mathf.Approximately(debugControls.SpeedController.DesiredSpeed, speed))
                    title += " ✓";
                Button speedButton = CreateButton(content, title,
                    new Vector2(12f + 100f * i, -370f),
                    new Vector2(96f, 54f), () =>
                    {
                        debugControls.SpeedController.SetSpeed(speed);
                        ShowTab(0);
                    });
                if (!Mathf.Approximately(debugControls.SpeedController.DesiredSpeed, speed))
                    TintRibbon(speedButton, new Color(0.72f, 0.72f, 0.72f));
            }
            Label(content, "Fog display is available when cheats are enabled", 16,
                new Vector2(430f, -154f), new Vector2(390f, 28f));
            Button fogButton = CreateButton(content, debugControls.IsBlueFogHidden
                ? "Fog: OFF" : "Fog: ON", new Vector2(430f, -225f),
                new Vector2(235f, 54f), () =>
                {
                    debugControls.SetHideBlueFog(!debugControls.IsBlueFogHidden);
                    ShowTab(0);
                });
            fogButton.interactable = CheatAccess.Allowed;
            if (CheatAccess.Allowed && !debugControls.IsBlueFogHidden)
                TintRibbon(fogButton, new Color(0.82f, 0.82f, 0.82f));
            if (!CheatAccess.Allowed)
            {
                TintRibbon(fogButton, new Color(0.55f, 0.55f, 0.55f));
                Label(content, "Allow Cheats to change Fog", 15,
                    new Vector2(430f, -282f), new Vector2(370f, 30f));
                return;
            }
            Label(content, "Add 100 resources to your faction", 19,
                new Vector2(430f, -422f), new Vector2(390f, 30f));
            ResourceType[] types =
                { ResourceType.Food, ResourceType.Wood, ResourceType.Gold };
            for (int i = 0; i < types.Length; i++)
            {
                ResourceType type = types[i];
                CreateButton(content, "+100 " + type,
                    new Vector2(40f + 275f * i, -458f), new Vector2(220f, 48f),
                    () =>
                    {
                        if (!CheatAccess.Allowed) return;
                        ResourceStockpile playerStockpile = GetPlayerStockpile();
                        playerStockpile?.Add(type, 100);
                    });
            }
        }

        private ResourceStockpile GetPlayerStockpile()
        {
            if (selection == null) return null;
            if (stockpile != null && stockpile.Faction == selection.PlayerFaction)
                return stockpile;
            foreach (ResourceStockpile candidate in UnityEngine.Object
                .FindObjectsByType<ResourceStockpile>())
                if (candidate != null && candidate.Faction == selection.PlayerFaction)
                    return candidate;
            return null;
        }

        private void BuildControls()
        {
            float restorePosition = controlsScrollPosition;
            Label(content, "Right Click: move / attack / gather. Esc: cancel command.",
                15, new Vector2(8f, -51f), new Vector2(790f, 28f));

            RectTransform viewport = RtsUiFactory.Rect("ControlsViewport", content);
            RtsUiFactory.Anchor(viewport, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(8f, -86f),
                new Vector2(784f, 428f));
            Image hitSurface = viewport.gameObject.AddComponent<Image>();
            hitSurface.color = new Color(1f, 1f, 1f, 0.001f);
            hitSurface.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            const float rowHeight = 50f;
            float listHeight = ControlActions.Length * rowHeight + 66f;
            RectTransform list = RtsUiFactory.Rect("ControlsList", viewport);
            RtsUiFactory.Anchor(list, new Vector2(0f, 1f),
                new Vector2(0f, 1f), Vector2.zero,
                new Vector2(766f, listHeight));
            for (int i = 0; i < ControlActions.Length; i++)
            {
                GameAction action = ControlActions[i];
                float y = -8f - i * rowHeight;
                if ((i & 1) != 0)
                {
                    RectTransform stripe = RtsUiFactory.Rect("RowTint", list);
                    RtsUiFactory.Anchor(stripe, new Vector2(0f, 1f),
                        new Vector2(0f, 1f), new Vector2(4f, y),
                        new Vector2(748f, 44f));
                    Image tint = stripe.gameObject.AddComponent<Image>();
                    tint.color = new Color(0.25f, 0.42f, 0.46f, 0.08f);
                    tint.raycastTarget = false;
                }
                Label(list, GameInputBindings.Label(action), 18,
                    new Vector2(18f, y), new Vector2(455f, 42f));
                CreateBindingButton(list, action,
                    new Vector2(500f, y + 2f), new Vector2(230f, 40f));
            }
            CreateButton(list, "Restore defaults",
                new Vector2(18f, -12f - ControlActions.Length * rowHeight),
                new Vector2(250f, 44f), () =>
                {
                    GameInputBindings.RestoreDefaults();
                    status.text = "Default keys restored.";
                    ShowTab(1);
                });

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = list;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 42f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            RectTransform track = RtsUiFactory.Rect("ControlsScrollbar", content);
            RtsUiFactory.Anchor(track, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(798f, -86f),
                new Vector2(14f, 428f));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0.20f, 0.32f, 0.35f, 0.25f);
            RectTransform handle = RtsUiFactory.Rect("Handle", track);
            RtsUiFactory.Stretch(handle, 2f, 0f, 2f, 0f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.22f, 0.53f, 0.62f, 0.85f);
            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            bar.targetGraphic = handleImage;
            bar.handleRect = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.Permanent;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = restorePosition;
            scroll.onValueChanged.AddListener(value =>
                controlsScrollPosition = value.y);
        }

        private void CreateBindingButton(Transform parent, GameAction action,
            Vector2 position, Vector2 size)
        {
            Button button = CreateButton(parent, GameInputBindings.Display(action),
                position, size, () =>
                {
                    awaiting = action;
                    GameInputBindings.Capturing = true;
                    status.text = "Press a shortcut for "
                        + GameInputBindings.Label(action) + " (Esc cancels).";
                });
            Text label = button.GetComponentInChildren<Text>();
            if (label == null) return;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 13;
            label.resizeTextMaxSize = 18;
        }

        private void BuildDisplay()
        {
            Label(content, "Display", 19, new Vector2(12f, -46f),
                new Vector2(500f, 30f));
            bool fullscreen = displayChangePending
                ? requestedFullscreen : Screen.fullScreen;
            int shownWidth = displayChangePending ? requestedWidth : Screen.width;
            int shownHeight = displayChangePending ? requestedHeight : Screen.height;
            CreateButton(content, "Fullscreen: " + (fullscreen ? "ON" : "OFF"),
                new Vector2(12f, -82f), new Vector2(290f, 44f), () =>
                {
                    BeginDisplayChange(shownWidth, shownHeight, !fullscreen);
                    ShowTab(2);
                });
            Resolution[] options = Screen.resolutions;
            if (options == null || options.Length == 0)
            {
                Label(content, "No display resolutions reported by this device.",
                    16, new Vector2(330f, -82f), new Vector2(500f, 44f));
            }
            else CreateButton(content, $"Resolution: {shownWidth} x {shownHeight}  >",
                new Vector2(330f, -82f), new Vector2(410f, 44f), () =>
                {
                    if (TryNextResolution(options, shownWidth, shownHeight,
                        out Resolution next))
                        BeginDisplayChange(next.width, next.height, fullscreen);
                    ShowTab(2);
                });
            if (!displayChangePending) return;
            CreateButton(content, "Keep display settings",
                new Vector2(12f, -147f), new Vector2(325f, 44f), () =>
                {
                    ConfirmDisplayChange();
                    ShowTab(2);
                });
            CreateButton(content, "Revert",
                new Vector2(354f, -147f), new Vector2(175f, 44f), () =>
                {
                    RevertDisplayChange();
                    ShowTab(2);
                });
        }

        private void BuildGraphics()
        {
            BuildDisplay();
            Label(content, "Performance", 19, new Vector2(12f, -218f),
                new Vector2(500f, 30f));
            int fps = Application.targetFrameRate;
            CreateButton(content, "FPS limit: " + (fps < 0 ? "Unlimited" : fps.ToString()),
                new Vector2(12f, -252f), new Vector2(310f, 44f), () =>
                {
                    int index = Array.IndexOf(FrameRates, Application.targetFrameRate);
                    int next = FrameRates[(index + 1 + FrameRates.Length) % FrameRates.Length];
                    ApplyFrameRate(next);
                    PlayerPrefs.SetInt("TinyKingdom.FpsLimit", next);
                    PlayerPrefs.Save();
                    ShowTab(2);
                });
            string[] names = QualitySettings.names;
            if (names.Length == 0) return;
            CreateButton(content, "Quality: " + names[QualitySettings.GetQualityLevel()],
                new Vector2(340f, -252f), new Vector2(400f, 44f), () =>
                {
                    int next = (QualitySettings.GetQualityLevel() + 1) % names.Length;
                    QualitySettings.SetQualityLevel(next, true);
                    PlayerPrefs.SetInt("TinyKingdom.Quality", next);
                    PlayerPrefs.Save();
                    ShowTab(2);
                });
        }

        private static bool TryNextResolution(Resolution[] options, int width,
            int height, out Resolution next)
        {
            int current = -1;
            for (int i = 0; i < options.Length; i++)
                if (options[i].width == width && options[i].height == height)
                {
                    current = i;
                    break;
                }
            for (int step = 1; step <= options.Length; step++)
            {
                int index = (current + step + options.Length) % options.Length;
                if (options[index].width == width && options[index].height == height)
                    continue;
                next = options[index];
                return true;
            }
            next = default;
            return false;
        }

        private void BeginDisplayChange(int width, int height, bool fullscreen)
        {
            if (fullscreen && !IsSupportedResolution(width, height))
            {
                Resolution[] options = Screen.resolutions;
                if (options.Length == 0) return;
                int closest = 0;
                long best = long.MaxValue;
                for (int i = 0; i < options.Length; i++)
                {
                    long distance = Math.Abs(options[i].width - width)
                        + Math.Abs(options[i].height - height);
                    if (distance >= best) continue;
                    best = distance;
                    closest = i;
                }
                width = options[closest].width;
                height = options[closest].height;
            }
            if (!displayChangePending)
            {
                previousWidth = Screen.width;
                previousHeight = Screen.height;
                previousFullscreen = Screen.fullScreen;
            }
            requestedWidth = width;
            requestedHeight = height;
            requestedFullscreen = fullscreen;
            displayChangePending = true;
            displayRevertAt = Time.unscaledTime + 15f;
            Screen.SetResolution(width, height, fullscreen);
        }

        private void ConfirmDisplayChange()
        {
            if (!displayChangePending) return;
            displayChangePending = false;
            PlayerPrefs.SetInt("TinyKingdom.Fullscreen", requestedFullscreen ? 1 : 0);
            PlayerPrefs.SetInt("TinyKingdom.Width", requestedWidth);
            PlayerPrefs.SetInt("TinyKingdom.Height", requestedHeight);
            PlayerPrefs.Save();
            status.text = "Display settings saved.";
        }

        private void RevertDisplayChange()
        {
            if (!displayChangePending) return;
            displayChangePending = false;
            Screen.SetResolution(previousWidth, previousHeight, previousFullscreen);
        }

        private void BuildAudio()
        {
            GameplayAudioService audio = GameplayAudioService.Instance;
            if (audio == null)
            {
                Label(content, "Audio service unavailable.", 16,
                    new Vector2(12f, -50f), new Vector2(500f, 30f));
                return;
            }
            CreateSlider("Master", -88f, audio.MasterVolume,
                value => audio.MasterVolume = value);
            CreateSlider("Sound effects", -168f, audio.SfxVolume,
                value => audio.SfxVolume = value);
            CreateSlider("Music", -248f, audio.MusicVolume,
                value => audio.MusicVolume = value);
            if (!audio.HasMusic)
                Label(content, "No music clip is configured.", 14,
                    new Vector2(12f, -312f), new Vector2(700f, 30f));
        }

        private void BuildCredits()
        {
            Label(content, "About Tiny Kingdom RL", 22,
                new Vector2(12f, -50f), new Vector2(780f, 36f));
            Label(content, UiLanguage.Text(
                "Tiny Kingdom RL is a compact 2D real-time strategy game made in "
                + "Unity. Lead the Blue faction, gather food, wood and gold, build "
                + "a base, train units, and destroy the opposing Castle.",
                "Tiny Kingdom RL là game chiến thuật thời gian thực 2D làm bằng "
                + "Unity. Dẫn dắt phe Xanh, thu thập thức ăn, gỗ và vàng, xây căn "
                + "cứ, huấn luyện quân và phá hủy Lâu đài đối phương."),
                18, new Vector2(12f, -92f), new Vector2(780f, 82f));
            Label(content, "How the match works", 21,
                new Vector2(12f, -190f), new Vector2(780f, 34f));
            Label(content, UiLanguage.Text(
                "Explore terrain under Fog of War, protect your economy, and "
                + "contest valuable Gold Mines. The Red faction uses a rule-based "
                + "commander, so a full match works without reinforcement learning.",
                "Khám phá bản đồ trong sương mù chiến tranh, bảo vệ kinh tế và "
                + "tranh giành Mỏ vàng. Phe Đỏ dùng chỉ huy theo luật, nên trận "
                + "đấu vẫn hoạt động đầy đủ khi chưa có học tăng cường."),
                18, new Vector2(12f, -230f), new Vector2(780f, 82f));
            Label(content, "Project and artwork", 21,
                new Vector2(12f, -326f), new Vector2(780f, 34f));
            Label(content, UiLanguage.Text(
                "Created by Huỳnh Trần Khải Hoàng and Nguyễn Minh Duy at FPT "
                + "University. Art: Tiny Swords by Pixel Frog. Future reinforcement "
                + "learning is intended for high-level strategy; normal game systems "
                + "control individual units.",
                "Thực hiện bởi Huỳnh Trần Khải Hoàng và Nguyễn Minh Duy tại "
                + "Đại học FPT. Đồ họa: Tiny Swords của Pixel Frog. Học tăng cường "
                + "trong tương lai chỉ quyết định chiến lược cấp cao; hệ thống game "
                + "vẫn điều khiển từng đơn vị."),
                17, new Vector2(12f, -367f), new Vector2(780f, 104f));
        }

        private void CreateSlider(string name, float y, float initial,
            Action<float> changed)
        {
            Label(content, name, 19, new Vector2(12f, y), new Vector2(280f, 40f));
            RectTransform bar = RtsUiFactory.Rect(name + "Slider", content);
            RtsUiFactory.Anchor(bar, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(305f, y), new Vector2(420f, 34f));
            Image background = bar.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            background.raycastTarget = true;
            RectTransform track = RtsUiFactory.Rect("Track", bar);
            RtsUiFactory.Stretch(track, 0f, 8f, 0f, 8f);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = RoundedSliderSprite;
            trackImage.type = Image.Type.Sliced;
            trackImage.color = new Color(0.20f, 0.34f, 0.39f, 1f);
            trackImage.raycastTarget = false;
            RectTransform fill = RtsUiFactory.Rect("Fill", bar);
            RtsUiFactory.Stretch(fill, 0f, 8f, 0f, 8f);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = RoundedSliderSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = new Color(0.34f, 0.56f, 0.64f, 1f);
            fillImage.raycastTarget = false;
            RectTransform handleArea = RtsUiFactory.Rect("HandleSlideArea", bar);
            RtsUiFactory.Stretch(handleArea, 11f, 0f, 11f, 0f);
            RectTransform handle = RtsUiFactory.Rect("Handle", handleArea);
            handle.anchorMin = new Vector2(0.5f, 0f);
            handle.anchorMax = new Vector2(0.5f, 1f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(22f, -12f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = CircularHandleSprite;
            handleImage.color = new Color(0.17f, 0.35f, 0.41f, 1f);
            handleImage.raycastTarget = false;
            RectTransform handleCenter = RtsUiFactory.Rect("HandleCenter", handle);
            RtsUiFactory.Stretch(handleCenter, 3f, 3f, 3f, 3f);
            Image handleCenterImage = handleCenter.gameObject.AddComponent<Image>();
            handleCenterImage.sprite = CircularHandleSprite;
            handleCenterImage.color = new Color(0.61f, 0.76f, 0.79f, 1f);
            handleCenterImage.raycastTarget = false;
            Slider slider = bar.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.SetValueWithoutNotify(initial);
            Text valueText = Label(content, Mathf.RoundToInt(initial * 100f) + "%",
                18, new Vector2(745f, y), new Vector2(80f, 34f));
            slider.onValueChanged.AddListener(value =>
            {
                changed(value);
                valueText.text = Mathf.RoundToInt(value * 100f) + "%";
            });
        }

        private static Sprite RoundedSliderSprite =>
            roundedSliderSprite != null ? roundedSliderSprite :
            roundedSliderSprite = CreateSliderShape(8f, 8f);

        private static Sprite CircularHandleSprite =>
            circularHandleSprite != null ? circularHandleSprite :
            circularHandleSprite = CreateSliderShape(16f, 0f);

        private static Sprite CreateSliderShape(float radius, float border)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            float inner = size * 0.5f - radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - inner, 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - inner, 0f);
                float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color32(255, 255, 255,
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        private static Text Label(Transform parent, string value, int size,
            Vector2 position, Vector2 dimensions)
        {
            Text label = RtsUiFactory.Label("Label", parent, value, size,
                TextAnchor.MiddleLeft, size >= 18 ? FontStyle.Bold : FontStyle.Normal);
            RtsUiFactory.Anchor(label.rectTransform, new Vector2(0f, 1f),
                new Vector2(0f, 1f), position, dimensions);
            StyleText(label);
            return label;
        }

        private void AddInset(string name, Vector2 position, Vector2 size, Color tint)
        {
            RectTransform rect = RtsUiFactory.Rect(name, panel);
            RtsUiFactory.Anchor(rect, new Vector2(0f, 1f),
                new Vector2(0f, 1f), position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = tint;
            image.raycastTarget = false;
            Outline edge = rect.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.31f, 0.23f, 0.16f, 0.4f);
            edge.effectDistance = new Vector2(2f, -2f);
        }

        private void AddContentRule()
        {
            RectTransform rule = RtsUiFactory.Rect("HeadingRule", content);
            RtsUiFactory.Anchor(rule, new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(12f, -42f),
                new Vector2(790f, 2f));
            Image line = rule.gameObject.AddComponent<Image>();
            line.color = new Color(0.56f, 0.39f, 0.17f, 0.56f);
            line.raycastTarget = false;
        }

        private int RibbonStart(bool big)
            => selection != null && selection.PlayerFaction == Faction.Red
                ? big ? 3 : 6 : 0;

        private Button CreateButton(Transform parent, string title,
            Vector2 position, Vector2 dimensions, UnityEngine.Events.UnityAction action)
            => CreateRibbonButton(parent, title, position, dimensions, action, false);

        private Button CreateRibbonButton(Transform parent, string title,
            Vector2 position, Vector2 dimensions,
            UnityEngine.Events.UnityAction action, bool big)
        {
            RectTransform rect = RtsUiFactory.Rect("Button", parent);
            RtsUiFactory.Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f),
                position, dimensions);
            bool square = dimensions.x < 80f;
            if (!square)
                RtsUiFactory.BuildRibbon("Ribbon", rect,
                    big ? assets.BigRibbons : assets.SmallRibbons,
                    RibbonStart(big),
                    big ? 0.72f : 0.68f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = square ? assets.CommandSlot : null;
            image.color = square ? Color.white : new Color(1f, 1f, 1f, 0.01f);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                GameplayAudioService.RequestUi(GameplayAudioEvent.CommandClick);
                StartCoroutine(AnimateButtonClick(rect, button, action));
            });
            Text text = RtsUiFactory.Label("Text", rect, title,
                big ? 23 : 18,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RtsUiFactory.Stretch(text.rectTransform,
                square ? 3f : big ? 40f : 14f, 3f,
                square ? 3f : big ? 40f : 14f, 3f);
            StyleText(text);
            return button;
        }

        private IEnumerator AnimateButtonClick(RectTransform target, Button button,
            UnityEngine.Events.UnityAction action)
        {
            button.interactable = false;
            Vector3 normal = target.localScale;
            const float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null) yield break;
                if (!IsOpen)
                {
                    target.localScale = normal;
                    button.interactable = true;
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float stretch = t < 0.4f
                    ? Mathf.Lerp(1f, 1.09f, t / 0.4f)
                    : t < 0.7f
                        ? Mathf.Lerp(1.09f, 0.94f, (t - 0.4f) / 0.3f)
                        : Mathf.Lerp(0.94f, 1f, (t - 0.7f) / 0.3f);
                target.localScale = new Vector3(normal.x * stretch,
                    normal.y, normal.z);
                yield return null;
            }
            if (target == null) yield break;
            target.localScale = normal;
            button.interactable = true;
            if (!IsOpen) yield break;
            action?.Invoke();
        }

        private static void StyleText(Text text)
        {
            text.color = new Color(0.11f, 0.09f, 0.08f, 1f);
            Outline outline = text.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }

        private static void TintRibbon(Button button, Color color)
        {
            if (button == null) return;
            foreach (Image image in button.GetComponentsInChildren<Image>())
                if (image.name.StartsWith("Ribbon", StringComparison.Ordinal))
                    image.color = color;
        }

        private static void ApplyFrameRate(int value)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = value;
        }

        private static void ApplySavedDisplayAndGraphics()
        {
            ApplyFrameRate(PlayerPrefs.GetInt("TinyKingdom.FpsLimit", 60));
            int quality = PlayerPrefs.GetInt("TinyKingdom.Quality", -1);
            if (quality >= 0 && quality < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel(quality, true);
            if (PlayerPrefs.HasKey("TinyKingdom.Fullscreen"))
                Screen.fullScreen = PlayerPrefs.GetInt("TinyKingdom.Fullscreen") != 0;
            int width = PlayerPrefs.GetInt("TinyKingdom.Width", 0);
            int height = PlayerPrefs.GetInt("TinyKingdom.Height", 0);
            if (width > 0 && height > 0
                && (!Screen.fullScreen || IsSupportedResolution(width, height)))
                Screen.SetResolution(width, height, Screen.fullScreenMode);
        }

        private static bool IsSupportedResolution(int width, int height)
        {
            foreach (Resolution option in Screen.resolutions)
                if (option.width == width && option.height == height) return true;
            return false;
        }

        private void FitPanelToViewport()
        {
            if (modal == null || panel == null) return;
            Vector2 available = modal.rect.size;
            float scale = Mathf.Min(1f, (available.x - 32f) / 1200f,
                (available.y - 32f) / 680f);
            scale = Mathf.Max(0.25f, scale);
            if (!Mathf.Approximately(panel.localScale.x, scale))
                panel.localScale = Vector3.one * scale;
        }
    }
}
