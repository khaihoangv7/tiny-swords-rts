using System.Collections;
using TinyKingdom.Input;
using TinyKingdom.Match;
using TinyKingdom.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinyKingdom.UI
{
    [RequireComponent(typeof(Canvas))]
    public sealed class MatchResultUI : MonoBehaviour
    {
        private const float OverlayAlpha = 0.55f;
        private MatchManager manager;
        private SelectionManager selection;
        private MatchResultAssetCatalog catalog;
        private RectTransform root;
        private RectTransform panel;
        private CanvasGroup canvasGroup;
        private Text title;
        private Text subtitle;
        private Image restartImage;
        private Text restartLabel;
        private bool showing;

        public bool ReferencesValid => manager != null && selection != null
            && catalog != null && catalog.IsComplete && root != null && restartImage != null;
        public bool HasRibbonReference => catalog != null
            && catalog.VictoryRibbon.Length == 3 && catalog.DefeatRibbon.Length == 3;
        public bool HasRestartButtonReference => catalog != null
            && catalog.VictoryButtonRegular != null && catalog.VictoryButtonPressed != null
            && catalog.DefeatButtonRegular != null && catalog.DefeatButtonPressed != null;
        public bool IsShowing => showing;
        public float DarkOverlayAlpha => OverlayAlpha;

        public void Configure(MatchManager authority, SelectionManager selected,
            MatchResultAssetCatalog assets)
        {
            if (manager != null) manager.MatchEnded -= HandleMatchEnded;
            manager = authority;
            selection = selected;
            catalog = assets;
            if (catalog == null || !catalog.IsComplete)
            {
                Debug.LogError("Phase 6C result UI asset catalog is missing or incomplete.", this);
                return;
            }
            Build();
            if (manager != null)
            {
                manager.MatchEnded += HandleMatchEnded;
                if (manager.IsMatchEnded) HandleMatchEnded(manager.Result);
            }
        }

        private void OnDestroy()
        {
            if (manager != null) manager.MatchEnded -= HandleMatchEnded;
        }

        private void HandleMatchEnded(MatchResult result)
        {
            if (!showing) StartCoroutine(ShowAfterDelay(result));
        }

        private IEnumerator ShowAfterDelay(MatchResult result)
        {
            showing = true;
            float delay = manager != null ? manager.ResultPresentationDelay : 0.85f;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            bool victory = manager != null && manager.WinningFaction == manager.HumanFaction;
            title.text = victory ? "VICTORY" : "DEFEAT";
            subtitle.text = victory ? "Enemy Castle Destroyed" : "Your Castle Has Fallen";
            SetRibbon(victory ? catalog.VictoryRibbon : catalog.DefeatRibbon);
            restartImage.sprite = victory
                ? catalog.VictoryButtonRegular : catalog.DefeatButtonRegular;
            MatchResultButtonVisual visual = restartImage.GetComponent<MatchResultButtonVisual>();
            visual.Configure(restartImage, restartLabel,
                victory ? catalog.VictoryButtonRegular : catalog.DefeatButtonRegular,
                victory ? catalog.VictoryButtonPressed : catalog.DefeatButtonPressed);

            selection?.ClearSelection();
            if (selection != null) selection.enabled = false;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != root) child.gameObject.SetActive(false);
            }
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            canvasGroup.alpha = 0f;
            panel.localScale = new Vector3(0.92f, 0.92f, 1f);
            float elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = t;
                panel.localScale = Vector3.Lerp(new Vector3(0.92f, 0.92f, 1f),
                    Vector3.one, t);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            panel.localScale = Vector3.one;
        }

        private void Build()
        {
            if (root != null) Destroy(root.gameObject);
            root = Rect("MatchResultOverlay", transform);
            Stretch(root);
            canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            Image overlay = root.gameObject.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, OverlayAlpha);
            overlay.raycastTarget = true;

            panel = Rect("ResultPanel", root);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(520f, 350f);
            panel.anchoredPosition = Vector2.zero;
            BuildNinePiece("SpecialPaper", panel, catalog.ResultPanel);

            RectTransform ribbon = Rect("ResultRibbon", panel);
            ribbon.anchorMin = ribbon.anchorMax = ribbon.pivot = new Vector2(0.5f, 0.5f);
            ribbon.sizeDelta = new Vector2(440f, 105f);
            ribbon.anchoredPosition = new Vector2(0f, 92f);
            BuildRibbon(ribbon, catalog.VictoryRibbon);

            title = Label("ResultTitle", ribbon, "", 42, FontStyle.Bold);
            Stretch(title.rectTransform);
            title.alignment = TextAnchor.MiddleCenter;
            AddOutline(title, new Color(0.08f, 0.07f, 0.12f, 0.95f), 2f);

            subtitle = Label("ResultSubtitle", panel, "", 20, FontStyle.Bold);
            subtitle.rectTransform.anchorMin = subtitle.rectTransform.anchorMax =
                subtitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            subtitle.rectTransform.sizeDelta = new Vector2(420f, 38f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, 18f);
            subtitle.alignment = TextAnchor.MiddleCenter;
            AddOutline(subtitle, new Color(0.08f, 0.07f, 0.12f, 0.9f), 1f);

            RectTransform buttonRect = Rect("RestartMatch", panel);
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot =
                new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(112f, 96f);
            buttonRect.anchoredPosition = new Vector2(-93f, -92f);
            restartImage = buttonRect.gameObject.AddComponent<Image>();
            restartImage.sprite = catalog.VictoryButtonRegular;
            restartImage.preserveAspect = true;
            restartImage.raycastTarget = true;
            Button restart = buttonRect.gameObject.AddComponent<Button>();
            restart.transition = Selectable.Transition.None;
            restart.targetGraphic = restartImage;
            restart.onClick.AddListener(RestartMatch);
            restartLabel = Label("Label", buttonRect, "RESTART", 15, FontStyle.Bold);
            Stretch(restartLabel.rectTransform);
            restartLabel.alignment = TextAnchor.MiddleCenter;
            AddOutline(restartLabel, new Color(0.08f, 0.07f, 0.12f, 0.95f), 1f);
            buttonRect.gameObject.AddComponent<MatchResultButtonVisual>().Configure(
                restartImage, restartLabel, catalog.VictoryButtonRegular,
                catalog.VictoryButtonPressed);

            RectTransform mapsRect = Rect("ChooseMap", panel);
            mapsRect.anchorMin = mapsRect.anchorMax = mapsRect.pivot =
                new Vector2(0.5f, 0.5f);
            mapsRect.sizeDelta = new Vector2(112f, 96f);
            mapsRect.anchoredPosition = new Vector2(93f, -92f);
            Image mapsImage = mapsRect.gameObject.AddComponent<Image>();
            mapsImage.sprite = catalog.VictoryButtonRegular;
            mapsImage.preserveAspect = true;
            Button mapsButton = mapsRect.gameObject.AddComponent<Button>();
            mapsButton.transition = Selectable.Transition.None;
            mapsButton.targetGraphic = mapsImage;
            mapsButton.onClick.AddListener(ChooseMap);
            Text mapsLabel = Label("Label", mapsRect, "MAPS", 15, FontStyle.Bold);
            Stretch(mapsLabel.rectTransform);
            mapsLabel.alignment = TextAnchor.MiddleCenter;
            AddOutline(mapsLabel, new Color(0.08f, 0.07f, 0.12f, 0.95f), 1f);
            mapsRect.gameObject.AddComponent<MatchResultButtonVisual>().Configure(
                mapsImage, mapsLabel, catalog.VictoryButtonRegular,
                catalog.VictoryButtonPressed);

            root.gameObject.SetActive(false);
        }

        private void SetRibbon(Sprite[] sprites)
        {
            Transform ribbon = panel.Find("ResultRibbon");
            if (ribbon == null) return;
            for (int i = ribbon.childCount - 1; i >= 0; i--)
            {
                Transform child = ribbon.GetChild(i);
                if (child.name.StartsWith("RibbonPiece")) Destroy(child.gameObject);
            }
            BuildRibbon((RectTransform)ribbon, sprites);
            title.transform.SetAsLastSibling();
        }

        private static void BuildRibbon(RectTransform parent, Sprite[] sprites)
        {
            if (sprites == null || sprites.Length != 3) return;
            float left = sprites[0].rect.width;
            float right = sprites[2].rect.width;
            float middleHeight = sprites[1].rect.height;
            Image leftImage = AnchoredImage("RibbonPieceLeft", parent, sprites[0],
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(left, sprites[0].rect.height));
            leftImage.rectTransform.anchoredPosition = new Vector2(left * 0.5f, 0f);
            Image middle = AnchoredImage("RibbonPieceMiddle", parent, sprites[1],
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(left, -middleHeight * 0.5f),
                new Vector2(-right, middleHeight * 0.5f));
            middle.type = Image.Type.Tiled;
            Image rightImage = AnchoredImage("RibbonPieceRight", parent, sprites[2],
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(right, sprites[2].rect.height));
            rightImage.rectTransform.anchoredPosition = new Vector2(-right * 0.5f, 0f);
        }

        private static void BuildNinePiece(string prefix, RectTransform parent, Sprite[] s)
        {
            if (s == null || s.Length != 9) return;
            float left = Mathf.Max(s[0].rect.width, s[3].rect.width, s[6].rect.width);
            float right = Mathf.Max(s[2].rect.width, s[5].rect.width, s[8].rect.width);
            float top = Mathf.Max(s[0].rect.height, s[1].rect.height, s[2].rect.height);
            float bottom = Mathf.Max(s[6].rect.height, s[7].rect.height, s[8].rect.height);
            for (int i = 0; i < 9; i++)
            {
                int column = i % 3;
                int row = i / 3;
                Vector2 min = new(column == 0 ? 0f : column == 1 ? 0f : 1f,
                    row == 0 ? 1f : 0f);
                Vector2 max = new(column == 0 ? 0f : column == 1 ? 1f : 1f,
                    row == 2 ? 0f : 1f);
                Vector2 offsetMin = new(column == 0 ? 0f : column == 1 ? left : -right,
                    row == 0 ? -top : row == 1 ? bottom : 0f);
                Vector2 offsetMax = new(column == 0 ? left : column == 1 ? -right : 0f,
                    row == 0 ? 0f : row == 1 ? -top : bottom);
                Image image = AnchoredImage(prefix + i, parent, s[i], min, max,
                    offsetMin, offsetMax);
                if (column == 1 || row == 1) image.type = Image.Type.Tiled;
            }
        }

        private static Image AnchoredImage(string name, Transform parent, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(string name, Transform parent, string value, int size,
            FontStyle style)
        {
            Text text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = new Color(1f, 0.94f, 0.74f);
            text.text = value;
            UiLanguage.Bind(text);
            text.raycastTarget = false;
            return text;
        }

        private static void AddOutline(Text text, Color color, float distance)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void RestartMatch()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        private static void ChooseMap() => SceneManager.LoadScene("MapSelection");
    }

    public sealed class MatchResultButtonVisual : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Image image;
        private Text label;
        private Sprite regular;
        private Sprite pressed;
        private bool hovering;

        public void Configure(Image target, Text text, Sprite normal, Sprite down)
        {
            image = target;
            label = text;
            regular = normal;
            pressed = down;
            SetRegular();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            if (image != null && image.sprite == regular)
                image.color = new Color(1f, 1f, 0.78f, 1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            SetRegular();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (image != null) image.sprite = pressed;
            if (image != null) image.color = Color.white;
            if (label != null) label.rectTransform.anchoredPosition = new Vector2(0f, -3f);
        }

        public void OnPointerUp(PointerEventData eventData) => SetRegular();

        private void SetRegular()
        {
            if (image != null)
            {
                image.sprite = regular;
                image.color = hovering ? new Color(1f, 1f, 0.78f, 1f) : Color.white;
            }
            if (label != null) label.rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
