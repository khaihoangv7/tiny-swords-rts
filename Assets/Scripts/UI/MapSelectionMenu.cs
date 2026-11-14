using TinyKingdom.Audio;
using TinyKingdom.AI;
using TinyKingdom.Match;
using TinyKingdom.Input;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    // The first build scene owns the front-end flow, without running a match.
    public sealed class MapSelectionMenu : MonoBehaviour
    {
        private static readonly string[] Scenes = { "Map", "LinearLane", "GoblinIsland" };
        private static readonly string[] MapNames = { "TWO KINGDOMS", "STRAIGHT LANE", "GOBLIN ISLAND" };
        private static readonly Color Ink = new(0.18f, 0.16f, 0.13f);
        private static readonly Color Cream = new(1f, 0.93f, 0.74f);
        private MainMenuAssetCatalog art;
        private RtsHudAssetCatalog hud;
        private RectTransform canvasRect;
        private RectTransform layout;
        private GameObject home;
        private GameObject difficultyPage;
        private GameObject mapPage;
        private SettingsPanelPresenter settings;
        private readonly MenuButtonFeedback[] difficulties = new MenuButtonFeedback[3];
        private readonly MenuButtonFeedback[] maps = new MenuButtonFeedback[3];
        private Text matchSummary;
        private Button startButton;
        private MatchDifficulty selectedDifficulty;
        private int selectedMap;
        private int page;
        private bool loading;
        private Text demoButtonLabel;
        private int demoMode;
        private const int RuleBasedDemoMode = 3;

        public int CurrentPage => page;
        public MatchDifficulty SelectedDifficulty => selectedDifficulty;
        public string SelectedScene => Scenes[selectedMap];

        private void Start()
        {
            RuleBasedDemo.Selected = false;
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            art = MainMenuAssetCatalog.Load();
            hud = RtsHudAssetCatalog.Load();
            if (art == null || hud == null)
            {
                Debug.LogError("Main menu assets are missing. Run Tools/Tiny Kingdom/Prepare Main Menu.");
                return;
            }
            gameObject.AddComponent<GameCursorController>().ConfigureDefaultCursor(art.defaultCursor);
            selectedDifficulty = MatchDifficultyCatalog.Selected;
            selectedMap = Mathf.Clamp(PlayerPrefs.GetInt("TinyKingdom.SelectedMap", 0), 0, 2);
            var root = new GameObject("Main Menu Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)root.transform;
            gameObject.AddComponent<MainMenuBackdrop>().Configure(art);
            Shade(root.transform);
            layout = Rect("Safe Menu Layout", root.transform, Vector2.zero, new Vector2(1600f, 900f));
            BuildHome();
            BuildDifficulty();
            BuildMaps();
            settings = root.AddComponent<SettingsPanelPresenter>();
            settings.ConfigureForMainMenu(hud);
            SelectDifficulty(selectedDifficulty);
            SelectMap(selectedMap);
            ShowPage(0);
            FitLayout();
        }

        private void BuildHome()
        {
            home = Rect("Home", layout, Vector2.zero, new Vector2(1600f, 900f)).gameObject;
            RectTransform logoRect = Rect("Tiny Swords Logo", home.transform,
                new Vector2(-430f, 282f), new Vector2(948f, 312f));
            Image logo = logoRect.gameObject.AddComponent<Image>();
            logo.sprite = art.logo;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            Outline outerGlow = logoRect.gameObject.AddComponent<Outline>();
            outerGlow.effectColor = new Color(1f, 0.84f, 0.42f, 0.26f);
            outerGlow.effectDistance = new Vector2(8f, -8f);
            outerGlow.useGraphicAlpha = true;
            Outline innerGlow = logoRect.gameObject.AddComponent<Outline>();
            innerGlow.effectColor = new Color(1f, 0.96f, 0.77f, 0.78f);
            innerGlow.effectDistance = new Vector2(3f, -3f);
            innerGlow.useGraphicAlpha = true;
            RectTransform panel = Panel("Main Menu", home.transform,
                new Vector2(472f, -30f), new Vector2(480f, 600f));
            Heading(panel, "MAIN MENU", new Vector2(0f, 220f), 44, 410f);
            MakeButton("Play Game", panel, "PLAY GAME", new Vector2(0f, 102f),
                new Vector2(344f, 94f), () => ShowPage(1));
            MakeButton("Settings", panel, "SETTINGS", new Vector2(0f, -14f),
                new Vector2(344f, 94f), () => settings.Open());
            MakeButton("Exit Game", panel, "EXIT GAME", new Vector2(0f, -130f),
                new Vector2(344f, 94f), QuitGame, true);
            IconButton("Quit", home.transform, "\u00d7", new Vector2(737f, 388f), QuitGame, true);
        }

        private RectTransform SetupPage(string name, string title)
        {
            RectTransform pageRoot = Rect(name, layout, Vector2.zero, new Vector2(1600f, 900f));
            RectTransform panel = Panel(name + " Panel", pageRoot, Vector2.zero, new Vector2(1430f, 720f));
            Heading(panel, title, new Vector2(0f, 280f), 42, 600f);
            IconButton("Back", panel, "\u2039", new Vector2(-618f, 280f), GoBack);
            IconButton("Close", panel, "\u00d7", new Vector2(618f, 280f), () => ShowPage(0), true);
            return panel;
        }

        private void BuildDifficulty()
        {
            RectTransform panel = SetupPage("Difficulty", "CHOOSE DIFFICULTY");
            difficultyPage = panel.parent.gameObject;
            string[] names = { "EASY", "NORMAL", "HARD" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Button card = MakeButton("Difficulty " + names[i], panel, "",
                    new Vector2((i - 1) * 430f, 5f), new Vector2(385f, 300f),
                    () => SelectDifficulty((MatchDifficulty)index));
                difficulties[i] = card.GetComponent<MenuButtonFeedback>();
                for (int mark = 0; mark <= i; mark++)
                {
                    RectTransform diamond = Rect("Difficulty Emblem", card.transform,
                        new Vector2((mark - i * 0.5f) * 44f, 65f), new Vector2(21f, 21f));
                    diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    Image image = diamond.gameObject.AddComponent<Image>();
                    image.color = Cream;
                    image.raycastTarget = false;
                }
                Label(card.transform, names[i], 52, new Vector2(0f, -26f),
                    new Vector2(340f, 82f), Cream);
            }
            MakeButton("Continue", panel, "CHOOSE MAP", new Vector2(0f, -254f),
                new Vector2(350f, 88f), () => ShowPage(2));
        }

        private void BuildMaps()
        {
            RectTransform panel = SetupPage("Map Selection", "CHOOSE BATTLEFIELD");
            mapPage = panel.parent.gameObject;
            for (int i = 0; i < Scenes.Length; i++)
            {
                int index = i;
                Button card = MakeButton(MapNames[i], panel, "", new Vector2((i - 1) * 430f, 5f),
                    new Vector2(390f, 370f), () => SelectMap(index));
                maps[i] = card.GetComponent<MenuButtonFeedback>();
                Sprite preview = art.mapPreviews != null && i < art.mapPreviews.Length ? art.mapPreviews[i] : null;
                RectTransform viewport = Rect("Preview Viewport", card.transform,
                    new Vector2(0f, 33f), new Vector2(346f, 246f));
                viewport.gameObject.AddComponent<RectMask2D>();
                Image picture = RtsUiFactory.Image("Map Preview", viewport, preview, false);
                RtsUiFactory.Stretch(picture.rectTransform);
                FitIllustration(picture);
                Label(card.transform, MapNames[i], 36, new Vector2(0f, -126f),
                    new Vector2(354f, 66f), Cream);
            }
            matchSummary = Label(panel, "", 32, new Vector2(-440f, -253f),
                new Vector2(380f, 66f), Ink, false);
            startButton = MakeButton("Start Game", panel, "START GAME", new Vector2(444f, -254f),
                new Vector2(350f, 88f), StartGame);
            Button demoButton = MakeButton("AI Demo Mode", panel, "AI DEMO: OFF",
                new Vector2(0f, -254f), new Vector2(360f, 88f), CycleDemoMode);
            demoButtonLabel = demoButton.GetComponentInChildren<Text>();
        }

        private void CycleDemoMode()
        {
            demoMode = (demoMode + 1) % (RuleBasedDemoMode + 1);
            demoButtonLabel.text = demoMode switch
            {
                1 => "Q-LEARNING VS PPO",
                2 => "PPO VS Q-LEARNING",
                RuleBasedDemoMode => RuleBasedDemo.Label,
                _ => "AI DEMO: OFF"
            };
            UpdateSummary();
        }

#if UNITY_EDITOR
        public bool TryStartRuleBasedDemoValidation()
        {
            if (home == null || demoButtonLabel == null || loading) return false;
            ShowPage(2);
            SelectMap(1);
            for (int i = 0; i < RuleBasedDemoMode; i++) CycleDemoMode();
            StartGame();
            return loading;
        }
#endif

        public void SelectDifficulty(MatchDifficulty value)
        {
            selectedDifficulty = (MatchDifficulty)Mathf.Clamp((int)value, 0, 2);
            for (int i = 0; i < difficulties.Length; i++)
                difficulties[i]?.SetSelected(i == (int)selectedDifficulty);
            UpdateSummary();
        }

        public void SelectMap(int index)
        {
            selectedMap = Mathf.Clamp(index, 0, Scenes.Length - 1);
            for (int i = 0; i < maps.Length; i++)
                maps[i]?.SetSelected(i == selectedMap);
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (matchSummary != null)
                matchSummary.text = demoMode == RuleBasedDemoMode
                    ? "STRAIGHT LANE · NORMAL"
                    : UiLanguage.Translate(selectedDifficulty.ToString().ToUpperInvariant());
        }

        public void ShowPage(int value)
        {
            if (loading || home == null) return;
            page = Mathf.Clamp(value, 0, 2);
            home.SetActive(page == 0);
            difficultyPage.SetActive(page == 1);
            mapPage.SetActive(page == 2);
            UpdateSummary();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void GoBack() => ShowPage(page - 1);

        public void StartGame()
        {
            if (loading || page != 2) return;
            string scene = demoMode == RuleBasedDemoMode ? RuleBasedDemo.SceneName : Scenes[selectedMap];
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                matchSummary.text = "Battlefield unavailable. Check Build Settings.";
                return;
            }
            if (demoMode is 1 or 2 && !QlExperiment.TrySelectDemo(demoMode == 2, out string error))
            {
                matchSummary.text = error;
                return;
            }
            RuleBasedDemo.Selected = demoMode == RuleBasedDemoMode;
            if (demoMode is 0 or RuleBasedDemoMode) QlExperiment.ClearDemo();
            if (!RuleBasedDemo.Selected)
            {
                MatchDifficultyCatalog.Selected = selectedDifficulty;
                PlayerPrefs.SetInt("TinyKingdom.SelectedMap", selectedMap);
                PlayerPrefs.Save();
            }
            loading = true;
            startButton.interactable = false;
            startButton.GetComponentInChildren<Text>().text = UiLanguage.Text("LOADING...", "ĐANG TẢI...");
            SceneManager.LoadSceneAsync(scene);
        }

        private static void QuitGame()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            FitLayout();
            if ((settings != null && settings.IsOpen) || loading) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && page > 0)
                GoBack();
        }

        private void FitLayout()
        {
            if (layout == null || canvasRect == null) return;
            Rect safe = Screen.safeArea;
            float width = Mathf.Max(1, Screen.width);
            float height = Mathf.Max(1, Screen.height);
            Vector2 available = canvasRect.rect.size;
            layout.anchoredPosition = new Vector2((safe.center.x / width - 0.5f) * available.x,
                (safe.center.y / height - 0.5f) * available.y);
            float scale = Mathf.Min(available.x * safe.width / width / 1600f,
                available.y * safe.height / height / 900f);
            layout.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }

        private RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, position, size);
            RtsUiFactory.BuildPanelFrame("Parchment", rect, hud.Banner, 0.65f, true);
            return rect;
        }

        private void Heading(Transform parent, string title, Vector2 position, int fontSize, float width)
        {
            RectTransform ribbon = Rect("Title Ribbon", parent, position, new Vector2(width, 100f));
            RtsUiFactory.BuildRibbon("Ribbon", ribbon, hud.BigRibbons, 0, 0.9f);
            Label(ribbon, title, fontSize, Vector2.zero, new Vector2(width - 80f, 65f), Cream);
        }

        private Button MakeButton(string name, Transform parent, string title, Vector2 position,
            Vector2 size, UnityAction action, bool red = false)
        {
            RectTransform rect = Rect(name, parent, position, size);
            Sprite[] normal = red ? art.redRegular : art.blueRegular;
            Sprite[] pressed = red ? art.redPressed : art.bluePressed;
            Image[] pieces = new Image[9];
            RtsUiFactory.BuildPanelFrame("Frame", rect, normal, 0.48f);
            string[] partNames = { "TopLeft", "Top", "TopRight", "Left", "Surface", "Right", "BottomLeft", "Bottom", "BottomRight" };
            for (int i = 0; i < pieces.Length; i++) pieces[i] = rect.Find("Frame" + partNames[i]).GetComponent<Image>();
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                GameplayAudioService.RequestUi(GameplayAudioEvent.CommandClick);
                action?.Invoke();
            });
            rect.gameObject.AddComponent<MenuButtonFeedback>().Configure(button, pieces, normal, pressed);
            if (!string.IsNullOrEmpty(title)) Label(rect, title, 38, Vector2.zero, size - new Vector2(44f, 14f), Cream);
            return button;
        }

        private void IconButton(string name, Transform parent, string glyph, Vector2 position,
            UnityAction action, bool red = false)
        {
            RectTransform rect = Rect(name, parent, position, new Vector2(62f, 62f));
            Sprite normal = red ? art.closeRegular : art.backRegular;
            Sprite pressed = red ? art.closePressed : art.backPressed;
            Image face = rect.gameObject.AddComponent<Image>();
            face.sprite = normal;
            face.preserveAspect = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            rect.gameObject.AddComponent<MenuButtonFeedback>().Configure(button, new[] { face }, new[] { normal }, new[] { pressed });
            button.onClick.AddListener(() => { GameplayAudioService.RequestUi(GameplayAudioEvent.CommandClick); action?.Invoke(); });
            bool back = glyph == "\u2039";
            for (int i = 0; i < 2; i++)
            {
                RectTransform stroke = Rect("Icon Stroke " + i, rect,
                    back ? new Vector2(-1f, i == 0 ? 6f : -6f) : Vector2.zero,
                    new Vector2(5f, back ? 20f : 26f));
                stroke.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -45f : 45f);
                Image image = stroke.gameObject.AddComponent<Image>();
                image.color = Cream;
                image.raycastTarget = false;
            }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = RtsUiFactory.Rect(name, parent);
            Place(rect, position, size);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
            => RtsUiFactory.Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);

        private Text Label(Transform parent, string value, int fontSize, Vector2 position,
            Vector2 size, Color color, bool outline = true)
        {
            Text text = RtsUiFactory.Label("Label", parent, value, fontSize, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(text.rectTransform, position, size);
            text.font = fontSize >= 68 ? art.titleFont : art.menuFont;
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.GetComponent<Outline>().enabled = outline;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private static void Shade(Transform parent)
        {
            RectTransform rect = RtsUiFactory.Rect("Background Shade", parent);
            RtsUiFactory.Stretch(rect);
            rect.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.07f, 0.09f, 0.15f);
        }

        private static void FitIllustration(Image image)
        {
            if (image.sprite == null) return;
            AspectRatioFitter fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
        }
    }
}
