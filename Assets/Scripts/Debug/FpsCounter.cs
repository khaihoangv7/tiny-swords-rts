using UnityEngine;
using UnityEngine.UI;

namespace TinyKingdom.Debugging
{
    [DisallowMultipleComponent]
    public sealed class FpsCounter : MonoBehaviour
    {
        private const string OverlayName = "DebugOverlay";
        private const string CounterName = "FPSCounter";
        private const float DefaultUpdateInterval = 0.25f;
        private const float DefaultSmoothingFactor = 0.10f;

        private static readonly Color NormalColor = new(0.92f, 0.95f, 1f, 0.95f);
        private static readonly Color WarningColor = new(1f, 0.78f, 0.25f, 0.95f);
        private static readonly Color DangerColor = new(1f, 0.32f, 0.28f, 0.95f);

        [SerializeField, Min(0.05f)] private float displayUpdateInterval =
            DefaultUpdateInterval;
        [SerializeField, Range(0.01f, 1f)] private float smoothingFactor =
            DefaultSmoothingFactor;

        private Text label;
        private float smoothedDeltaTime;
        private float displayTimer;
        private int colorBand = -1;

        public float DisplayUpdateInterval => displayUpdateInterval;
        public float SmoothedDeltaTime => smoothedDeltaTime;

        public static FpsCounter Ensure(Transform canvasRoot)
        {
            if (canvasRoot == null || (!Application.isEditor && !UnityEngine.Debug.isDebugBuild))
                return null;

            Transform overlayTransform = canvasRoot.Find(OverlayName);
            RectTransform overlay;
            if (overlayTransform == null)
            {
                var overlayObject = new GameObject(OverlayName, typeof(RectTransform),
                    typeof(CanvasGroup));
                overlay = (RectTransform)overlayObject.transform;
                overlay.SetParent(canvasRoot, false);
                Stretch(overlay);
                CanvasGroup group = overlayObject.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                group.ignoreParentGroups = false;
            }
            else
            {
                overlay = overlayTransform as RectTransform;
                if (overlay == null) return null;
            }
            CanvasGroup overlayGroup = overlay.GetComponent<CanvasGroup>()
                ?? overlay.gameObject.AddComponent<CanvasGroup>();
            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;
            overlayGroup.ignoreParentGroups = false;
            overlay.SetAsLastSibling();

            Transform counterTransform = overlay.Find(CounterName);
            FpsCounter counter = counterTransform != null
                ? counterTransform.GetComponent<FpsCounter>() : null;
            if (counter != null)
            {
                counter.ResolveLabel();
                RectTransform existing = counter.transform as RectTransform;
                if (existing != null)
                {
                    existing.anchoredPosition = new Vector2(-16f, -72f);
                    existing.sizeDelta = new Vector2(160f, 43f);
                }
                if (counter.label != null) counter.label.fontSize = 17;
                return counter;
            }

            var counterObject = new GameObject(CounterName, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text), typeof(FpsCounter));
            RectTransform rect = (RectTransform)counterObject.transform;
            rect.SetParent(overlay, false);
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-16f, -72f);
            rect.sizeDelta = new Vector2(160f, 43f);

            Text text = counterObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 17;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperRight;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.color = NormalColor;
            text.text = "FPS: --\n-- ms";

            Outline outline = counterObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = Vector2.one;
            outline.useGraphicAlpha = true;

            counter = counterObject.GetComponent<FpsCounter>();
            counter.label = text;
            return counter;
        }

        private void Awake()
        {
            if (!Application.isEditor && !UnityEngine.Debug.isDebugBuild)
            {
                gameObject.SetActive(false);
                return;
            }
            ResolveLabel();
        }

        private void OnEnable()
        {
            smoothedDeltaTime = 0f;
            displayTimer = 0f;
            colorBand = -1;
        }

        private void Update()
        {
            Rect area = Screen.safeArea;
            Vector2 corner = new(area.xMax / Mathf.Max(1f, Screen.width),
                area.yMax / Mathf.Max(1f, Screen.height));
            RectTransform rect = transform as RectTransform;
            if (rect != null && rect.anchorMin != corner)
                rect.anchorMin = rect.anchorMax = corner;
            float frameTime = Time.unscaledDeltaTime;
            if (frameTime <= 0f) return;
            smoothedDeltaTime = smoothedDeltaTime <= 0f
                ? frameTime
                : smoothedDeltaTime + (frameTime - smoothedDeltaTime) * smoothingFactor;

            displayTimer += frameTime;
            if (displayTimer < displayUpdateInterval || label == null) return;
            displayTimer %= displayUpdateInterval;

            float fps = 1f / Mathf.Max(0.000001f, smoothedDeltaTime);
            float milliseconds = smoothedDeltaTime * 1000f;
            label.text = $"FPS: {fps:0}\n{milliseconds:0.0} ms";
            RefreshColor(fps);
        }

        private void ResolveLabel()
        {
            if (label == null) label = GetComponent<Text>();
            if (label != null) label.raycastTarget = false;
        }

        private void RefreshColor(float fps)
        {
            int nextBand = fps >= 60f ? 0 : fps >= 30f ? 1 : 2;
            if (nextBand == colorBand) return;
            colorBand = nextBand;
            label.color = nextBand switch
            {
                0 => NormalColor,
                1 => WarningColor,
                _ => DangerColor
            };
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
